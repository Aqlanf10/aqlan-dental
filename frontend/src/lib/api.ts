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

// Sprint 13: base URL + common headers + withCredentials are sourced from the
// shared `apiClient.ts` factory. Staff authentication remains owned here.
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

// Improved 401 handling with request queuing during refresh.
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null = null) => {
  failedQueue.forEach((promise) => {
    if (error) {
      promise.reject(error);
    } else {
      promise.resolve(token);
    }
  });
  failedQueue = [];
};

function clearBrowserAuthState() {
  clearAccessToken();
  setImpersonatingSession(false);
  if (typeof window !== "undefined") {
    // Remove legacy values written by older releases as well as the routing
    // sentinel. This is deliberately defensive during the migration window.
    localStorage.removeItem("access_token");
    localStorage.removeItem("aqlan_original_token");
    document.cookie = "aqlan_auth_status=; path=/; max-age=0";
  }
}

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    const url = original?.url ?? "";

    // An impersonated session is intentionally access-token-only. Both the
    // original administrator refresh token and the target refresh token are
    // revoked when impersonation starts. If the short session expires, require a
    // fresh login instead of refreshing into an untracked non-impersonated target
    // session that can no longer be safely returned to the administrator.
    if (error.response?.status === 401 && impersonatingSession) {
      processQueue(error, null);
      clearBrowserAuthState();
      if (typeof window !== "undefined") window.location.href = "/login";
      return Promise.reject(error);
    }

    // Skip refresh logic for auth endpoints — they handle 401 themselves.
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

    // SEQ-36: mutating clinic-queue failures must never be swallowed silently.
    // The notifier ignores reads, cancellations, and authentication failures.
    notifyClinicQueueActionFailure(error);
    return Promise.reject(error);
  }
);

export default api;

// ─── FE-05 / FE-16: Helpers for upload (multipart) and download (blob) ─────────

/** Upload a file via multipart/form-data through the authenticated client. */
export async function upload<T = unknown>(url: string, formData: FormData): Promise<T> {
  const res = await api.post<T>(url, formData, {
    headers: { "Content-Type": undefined },
  });
  return res.data;
}

/** Download a binary blob through the authenticated client. */
export async function downloadBlob(url: string, params?: Record<string, string | number | undefined>): Promise<Blob> {
  const res = await api.get<Blob>(url, {
    responseType: "blob",
    params,
  });
  return res.data;
}
