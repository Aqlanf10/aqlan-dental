import axios, { type InternalAxiosRequestConfig } from "axios";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

export const portalApi = axios.create({
  baseURL: API_URL,
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
  localStorage.removeItem("portal_token");
  localStorage.removeItem("portal_refresh_token");
  document.cookie = "aqlan_portal_auth=; path=/portal; max-age=0";
  window.location.href = "/portal/login";
}

portalApi.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // If not a 401 or already retried, reject
    if (error.response?.status !== 401 || originalRequest._retry) {
      if (error.response?.status === 401) {
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
      const refreshToken = localStorage.getItem("portal_refresh_token");
      if (!refreshToken) {
        throw new Error("No refresh token");
      }

      // We need the current (expired) access token for the patientId claim
      const currentToken = localStorage.getItem("portal_token");
      const { data } = await axios.post(`${API_URL}/api/portal/auth/refresh-token`, {
        refreshToken,
      }, {
        headers: { Authorization: `Bearer ${currentToken}` },
      });

      // Save new tokens
      localStorage.setItem("portal_token", data.accessToken);
      if (data.refreshToken) {
        localStorage.setItem("portal_refresh_token", data.refreshToken);
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
