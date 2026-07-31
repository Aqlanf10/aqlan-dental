import { createApiClient } from "@/lib/apiClient";
import { notifyClinicQueueActionFailure } from "@/lib/clinicQueueActionErrors";

// Staff access tokens are intentionally kept in module memory only. Persisting an
// administrator token in localStorage made it possible to resurrect an old admin
// session after impersonation or logout. The long-lived refresh credential remains
// an HttpOnly cookie owned by the API.
let accessToken: string | null = null;
let impersonatingSession = false;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

export function clearAccessToken() {
  accessToken = null;
}

export function setImpersonatingSession(value: boolean) {
  impersonatingSession = value;
}

export const api = createApiClient();

// Raw axios instance without interceptors — used for refresh-token and session
// rotation calls to avoid recursive interceptor deadlocks.
const apiRaw = createApiClient();

// Inject the in-memory access token on every request. Before starting an
// impersonation, revoke the administrator refresh cookie. The still-valid short-
// lived access token authorizes the impersonation request, while the old long-lived
// administrator session can no longer be replayed from another tab or device.
api.interceptors.request.use(async (config) => {
  const token = getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;

  const url = config.url ?? "";
  const isImpersonationStart =
    config.method?.toLowerCase() === "post" && url.includes("/api/auth/impersonate/");

  if (isImpersonationStart && token) {
    await apiRaw.post("/api/auth/logout", undefined, {
      headers: { Authorization: `Bearer ${token}` },
    });
  }

  return config;
});

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((promise) => {
    if (error) promise.reject(error);
    else promise.resolve(token);
  });
  failedQueue = [];
};

function clearBrowserAuthState() {
  clearAccessToken();
  setImpersonatingSession(false);
  if (typeof window !== "undefined") {
    localStorage.removeItem("access_token");
    localStorage.removeItem("aqlan_original_token");
    document.cookie = "aqlan_auth_status=; path=/; max-age=0";
  }
}

/**
 * An impersonated access token must never be refreshed into an ordinary target
 * session because that new JWT would lose originalUserId/isImpersonating claims.
 * When the short token expires, consume the target refresh token once, use the
 * replacement access token only to call logout, and revoke the replacement token.
 */
async function terminateExpiredImpersonationSession() {
  try {
    const { data } = await apiRaw.post<{ accessToken: string }>(
      "/api/auth/refresh-token"
    );
    await apiRaw.post("/api/auth/logout", undefined, {
      headers: { Authorization: `Bearer ${data.accessToken}` },
    });
  } catch {
    // The token may already be expired/revoked by another tab. Local cleanup and
    // a fresh login are still the safe outcome.
  }
}

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    const url = original?.url ?? "";

    if (error.response?.status === 401 && impersonatingSession) {
      processQueue(error, null);
      await terminateExpiredImpersonationSession();
      clearBrowserAuthState();
      if (typeof window !== "undefined") window.location.href = "/login";
      return Promise.reject(error);
    }

    if (
      error.response?.status === 401 &&
      !original._retry &&
      !url.includes("/api/auth/login") &&
      !url.includes("/api/auth/refresh-token") &&
      !url.includes("/api/portal/auth/")
    ) {
      original._retry = true;

      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          original.headers.Authorization = `Bearer ${token}`;
          return api(original);
        });
      }

      isRefreshing = true;
      try {
        const { data } = await apiRaw.post<{ accessToken: string }>(
          "/api/auth/refresh-token"
        );
        setAccessToken(data.accessToken);
        processQueue(null, data.accessToken);
        original.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(original);
      } catch {
        processQueue(error, null);
        clearBrowserAuthState();
        if (typeof window !== "undefined") window.location.href = "/login";
        return Promise.reject(error);
      } finally {
        isRefreshing = false;
      }
    }

    notifyClinicQueueActionFailure(error);
    return Promise.reject(error);
  }
);

export default api;

export async function upload<T = unknown>(url: string, formData: FormData): Promise<T> {
  const res = await api.post<T>(url, formData, {
    headers: { "Content-Type": undefined },
  });
  return res.data;
}

export async function downloadBlob(url: string, params?: Record<string, string | number | undefined>): Promise<Blob> {
  const res = await api.get<Blob>(url, {
    responseType: "blob",
    params,
  });
  return res.data;
}
