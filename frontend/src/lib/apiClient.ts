import axios, { type AxiosInstance } from "axios";

/**
 * Shared API client foundation for both the staff (`api.ts`) and patient portal
 * (`portalApi.ts`) axios instances. Sprint 13 (API client unification).
 *
 * Why this file exists
 * --------------------
 * Before Sprint 13, `api.ts` and `portalApi.ts` each independently duplicated:
 *   - the backend base URL (`process.env.NEXT_PUBLIC_API_URL ?? ""`),
 *   - the common JSON + Arabic `Accept-Language` headers,
 *   - the `withCredentials: true` flag (needed for HttpOnly refresh cookies),
 *   - and ad-hoc error handling scattered across hooks/components.
 *
 * The two clients CANNOT be merged into a single instance because they live in
 * different auth domains:
 *   - Staff:       `access_token` in localStorage, refresh via `/api/auth/refresh-token`
 *                  (cookie `aqlan_auth_status` for SSR auth detection).
 *   - Patient portal: `portal_token` in localStorage, refresh via `/api/portal/auth/refresh-token`
 *                  (HttpOnly `portal_refresh` cookie + MUST_CHANGE_PASSWORD 403 flow).
 *
 * Different token storage keys + different refresh endpoints + different redirect
 * targets (→ `/login` vs → `/portal/login`) means each client must own its auth
 * interceptors. This file factor outs everything that is genuinely shared so the
 * two clients cannot drift on base URL / headers / error-message extraction.
 *
 * What is shared here
 *   1. `API_BASE_URL`           — single source of truth for the backend URL.
 *   2. `DEFAULT_API_HEADERS`    — `Content-Type: application/json` + `Accept-Language: ar`
 *                                  (CLAUDE.md: every request must send Arabic).
 *   3. `createApiClient(opts)`  — factory that returns a fresh axios instance with the
 *                                  shared base URL, headers, and `withCredentials`.
 *                                  Caller attaches its own request/response interceptors.
 *   4. `extractApiError(err)`   — pulls the Arabic `message` field out of an axios
 *                                  error response (backend contract: every 4xx/5xx
 *                                  returns `{ message: string }`). Falls back to a
 *                                  generic Arabic message for network/timeout errors
 *                                  so the user never sees raw English axios text.
 *
 * What is NOT shared here
 *   - JWT access-token injection (different localStorage keys).
 *   - 401 refresh-token flow (different endpoints, cookie names, redirect targets).
 *   - 403 MUST_CHANGE_PASSWORD handling (portal only).
 *   - The request-queue-during-refresh logic (each client implements its own).
 */

const PRODUCTION_VERCEL_HOSTS = new Set([
  "aqlan-dental.vercel.app",
  "aqlan-dental-pro.vercel.app",
]);

/**
 * Preview deployments are intentionally absent from the backend CORS allowlist.
 * Route them through Next.js' same-origin /api rewrite instead of weakening CORS
 * for every generated *.vercel.app hostname.
 */
export function resolveApiBaseUrl(
  configuredBaseUrl: string,
  hostname?: string
): string {
  const isVercelPreview =
    hostname?.endsWith(".vercel.app") === true &&
    !PRODUCTION_VERCEL_HOSTS.has(hostname);
  return isVercelPreview ? "" : configuredBaseUrl;
}

export const API_BASE_URL = resolveApiBaseUrl(
  process.env.NEXT_PUBLIC_API_URL ?? "",
  typeof window === "undefined" ? undefined : window.location.hostname
);

export const DEFAULT_API_HEADERS = {
  "Content-Type": "application/json",
  "Accept-Language": "ar",
} as const;

export interface ApiClientOptions {
  /** Forward cookies (needed for HttpOnly refresh-token cookies). Defaults to true. */
  withCredentials?: boolean;
}

/**
 * Create a new axios instance pre-configured with the shared backend base URL,
 * common JSON/Arabic headers, and `withCredentials`. The caller owns all
 * interceptors — this factory deliberately does NOT attach auth/refresh logic
 * so it stays generic across staff + portal auth domains.
 *
 * Usage:
 *   const api = createApiClient();
 *   api.interceptors.request.use(injectStaffToken);
 *   api.interceptors.response.use(..., handleStaff401);
 */
export function createApiClient(options: ApiClientOptions = {}): AxiosInstance {
  return axios.create({
    baseURL: API_BASE_URL,
    withCredentials: options.withCredentials ?? true,
    headers: { ...DEFAULT_API_HEADERS },
  });
}

/**
 * Extract a user-facing Arabic error message from an axios error response.
 *
 * The backend (per CLAUDE.md) always returns `{ message: string }` on 4xx/5xx
 * with an Arabic `message`. This helper centralizes that extraction so hooks
 * and components stop hand-rolling `err?.response?.data?.message ?? "..."`.
 *
 * For network errors / timeouts (no `response`), it returns the provided
 * fallback (also Arabic by default) rather than the raw English axios message
 * — exposing English error text to clinic staff/patients is a known UX trap.
 */
export function extractApiError(
  err: unknown,
  fallback = "حدث خطأ غير متوقع، يرجى المحاولة مرة أخرى."
): string {
  // axios error shape — avoid importing AxiosError to keep this helper
  // usable from non-axios call sites (e.g. raw fetch wrappers) too.
  const axiosErr = err as {
    response?: { data?: { message?: unknown } };
    message?: string;
  } | undefined;

  const responseMessage = axiosErr?.response?.data?.message;
  if (typeof responseMessage === "string" && responseMessage.trim().length > 0) {
    return responseMessage;
  }

  // No structured backend payload — network error, timeout, CORS, etc.
  // Don't leak the raw axios English text to users.
  return fallback;
}
