import axios, { type InternalAxiosRequestConfig } from "axios";
import { usePatientAuthStore } from "@/stores/patientAuthStore";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export const portalApi = axios.create({
  baseURL: API_URL,
  // SEC-10: required so the HttpOnly `portal_refresh` cookie is sent on every
  // request AND received from Set-Cookie responses (login/refresh/logout).
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
    "Accept-Language": "ar",
  },
});

// Inject portal token on every request
portalApi.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("portal_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// On 401: attempt token refresh, then redirect to portal login if refresh fails
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
  config: InternalAxiosRequestConfig;
}> = [];

function processQueue(error: unknown) {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(portalApi(prom.config));
    }
  });
  failedQueue = [];
}

function clearAuthAndRedirect() {
  if (typeof window === "undefined") return;
  // SEC-10: only the access token is stored locally now — the refresh token
  // lives in an HttpOnly cookie, which the backend `/api/portal/auth/logout`
  // endpoint clears via Set-Cookie. We still call the store logout so the
  // server-side hash is wiped and the cookie deleted.
  localStorage.removeItem("portal_token");
  document.cookie = "aqlan_portal_auth=; path=/portal; max-age=0";
  // Use store logout to keep state consistent
  usePatientAuthStore.getState().logout();
  window.location.href = "/portal/login";
}

portalApi.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Handle 403 with MUST_CHANGE_PASSWORD code — update the store and let
    // the portal layout handle the redirect. Do NOT use window.location.href
    // here because it causes a full page reload, which triggers the Zustand
    // persist rehydration race condition (the layout sees default state
    // {isAuthenticated:false} before rehydration completes, causing an
    // infinite redirect loop between /portal/login and /portal/change-password).
    if (error.response?.status === 403 && error.response?.data?.code === "MUST_CHANGE_PASSWORD") {
      if (typeof window !== "undefined") {
        const store = usePatientAuthStore.getState();
        // Only update and redirect if the store doesn't already know
        if (!store.mustChangePassword) {
          store.setMustChangePassword(true);
        }
        // Use client-side navigation instead of hard reload
        // Only redirect if not already on the change-password page
        if (window.location.pathname !== "/portal/change-password") {
          // Use Next.js router if available, otherwise fall back to location
          // (we can't import useRouter here, so use pushState + popstate)
          window.history.pushState(null, "", "/portal/change-password");
          window.dispatchEvent(new PopStateEvent("popstate", { state: null }));
        }
      }
      return Promise.reject(error);
    }

    // If not a 401, already retried, or this is a portal auth endpoint
    // (login/forgot-password/reset-password), skip the refresh-token flow.
    // Auth endpoints returning 401 mean bad credentials — not an expired token.
    // Without this guard, a wrong-password login would trigger clearAuthAndRedirect()
    // (full page reload) before the catch block in login/page.tsx could setError().
    const isPortalAuthEndpoint = originalRequest.url?.includes('/api/portal/auth/');
    if (error.response?.status !== 401 || originalRequest._retry || isPortalAuthEndpoint) {
      if (error.response?.status === 401 && !isPortalAuthEndpoint) {
        clearAuthAndRedirect();
      }
      return Promise.reject(error);
    }

    // Try to refresh the token
    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        failedQueue.push({ resolve, reject, config: originalRequest });
      });
    }

    originalRequest._retry = true;
    isRefreshing = true;

    try {
      // SEC-10: the refresh token is sent automatically via the HttpOnly
      // `portal_refresh` cookie (withCredentials=true). No body needed.
      // The expired access token is still required in the Authorization
      // header so the backend can extract the patientId claim.
      const currentToken = localStorage.getItem("portal_token");
      const { data } = await portalApi.post(
        "/api/portal/auth/refresh-token",
        undefined,
        { headers: { Authorization: `Bearer ${currentToken}` } }
      );

      // Save the new access token (short-lived, stays in localStorage).
      // SEC-10: refresh-token rotation is handled by the cookie — the
      // Set-Cookie response header replaces `portal_refresh` automatically.
      localStorage.setItem("portal_token", data.accessToken);

      // If the refreshed token indicates mustChangePassword, update the store
      if (data.mustChangePassword) {
        usePatientAuthStore.getState().setMustChangePassword(true);
      }

      // Update the original request with new token
      originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;

      processQueue(null);
      return portalApi(originalRequest);
    } catch (refreshError) {
      processQueue(refreshError);
      clearAuthAndRedirect();
      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  }
);

export default portalApi;
