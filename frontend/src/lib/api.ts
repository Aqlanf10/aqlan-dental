import { createApiClient } from "@/lib/apiClient";

// Sprint 13: base URL + common headers + withCredentials are now sourced from the
// shared `apiClient.ts` factory. The staff auth interceptors (JWT in localStorage
// `access_token`, refresh via `/api/auth/refresh-token`, redirect → `/login`)
// remain owned by this file — see apiClient.ts for why the two clients are NOT merged.
export const api = createApiClient();

// Raw axios instance without interceptors — used for refresh-token to avoid deadlock (F1 FIX).
// `createApiClient()` returns a fresh instance with no interceptors attached, which is exactly
// what we need here: if the refresh call itself returns 401, using `api` would queue it forever.
const apiRaw = createApiClient();

// Inject access token on every request
api.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("access_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// F1 FIX: Improved 401 handling with request queuing during refresh
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

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    const url = original?.url ?? "";

    // Skip refresh logic for auth endpoints — they handle 401 themselves
    if (
      error.response?.status === 401 &&
      !original._retry &&
      !url.includes("/api/auth/login") &&
      !url.includes("/api/auth/refresh-token") &&
      !url.includes("/api/portal/auth/")
    ) {
      original._retry = true;

      if (isRefreshing) {
        // F1 FIX: Queue the request instead of immediately redirecting
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          original.headers.Authorization = `Bearer ${token}`;
          return api(original);
        });
      }

      isRefreshing = true;
      try {
        // F1 FIX: Use raw axios (no interceptors) for refresh-token to prevent deadlock
        // If the refresh call itself returns 401, using `api` would queue it and hang forever
        const { data } = await apiRaw.post<{ accessToken: string }>(
          "/api/auth/refresh-token"
        );
        localStorage.setItem("access_token", data.accessToken);
        processQueue(null, data.accessToken);
        original.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(original);
      } catch {
        processQueue(error, null);
        if (typeof window !== "undefined") {
          localStorage.removeItem("access_token");
          document.cookie = "aqlan_auth_status=; path=/; max-age=0";
          window.location.href = "/login";
        }
        return Promise.reject(error);
      } finally {
        isRefreshing = false;
      }
    }
    return Promise.reject(error);
  }
);

export default api;

// ─── FE-05 / FE-16: Helpers for upload (multipart) and download (blob) ─────────
// These replace the direct fetch() calls that bypassed the axios refresh-token interceptor,
// causing silent failures on CSV export and image upload when the access token expired.

/**
 * Upload a file via multipart/form-data. Uses the axios `api` instance so the request
 * interceptor injects the JWT and the response interceptor handles 401 refresh.
 * The Content-Type header is NOT set — axios sets it automatically with the correct
 * multipart boundary when given a FormData body.
 */
export async function upload<T = unknown>(url: string, formData: FormData): Promise<T> {
  const res = await api.post<T>(url, formData, {
    headers: { "Content-Type": undefined }, // let axios set multipart boundary
  });
  return res.data;
}

/**
 * Download a binary blob (e.g., CSV export, PDF receipt). Uses the axios `api` instance
 * so the JWT is injected and 401 refresh works. Returns a Blob that the caller can
 * turn into a download URL or open in a new tab.
 */
export async function downloadBlob(url: string, params?: Record<string, string | number | undefined>): Promise<Blob> {
  const res = await api.get<Blob>(url, {
    responseType: "blob",
    params,
  });
  return res.data;
}
