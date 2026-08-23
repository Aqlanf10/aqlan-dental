import { clearTokens, readTokens, writeTokens } from "@/auth/tokenStore";
import type { MobileLoginResponse, MobileRefreshResponse } from "@/lib/types";
import { normalizeMobileLoginResponse } from "@/lib/session";

const MOBILE_REFRESH_HEADER = "X-Aqlan-Refresh-Token";
const REQUEST_TIMEOUT_MS = 30_000;

export type ApiHealth = {
  status: string;
  timestamp?: string | null;
  version?: string | null;
  latencyMs: number;
  serverOrigin: string;
};

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly payload?: unknown
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function getBaseUrl(): string {
  const configured = process.env.EXPO_PUBLIC_API_URL?.trim().replace(/\/+$/, "");

  if (!configured) {
    if (__DEV__) return "http://127.0.0.1:5000";
    throw new Error("EXPO_PUBLIC_API_URL غير مضبوط لتطبيق الإنتاج.");
  }

  if (!__DEV__ && !configured.startsWith("https://")) {
    throw new Error("تطبيق الإنتاج يتطلب رابط API يعمل عبر HTTPS.");
  }

  return configured;
}

export function apiServerOrigin(): string {
  return getBaseUrl();
}

export async function checkApiHealth(): Promise<ApiHealth> {
  const serverOrigin = getBaseUrl();
  const startedAt = Date.now();
  const response = await fetchWithTimeout(`${serverOrigin}/health`, {
    headers: { Accept: "application/json" }
  });
  const payload = await parsePayload(response);

  if (!response.ok) {
    throw new ApiError(
      messageFromPayload(payload, `تعذر فحص الخادم (${response.status})`),
      response.status,
      payload
    );
  }

  const data = payload && typeof payload === "object"
    ? payload as { status?: unknown; timestamp?: unknown; version?: unknown }
    : {};

  return {
    status: typeof data.status === "string" ? data.status : "healthy",
    timestamp: typeof data.timestamp === "string" ? data.timestamp : null,
    version: typeof data.version === "string" ? data.version : null,
    latencyMs: Date.now() - startedAt,
    serverOrigin
  };
}

export function apiAssetUrl(path: string): string {
  const value = path.trim();
  if (/^https?:\/\//i.test(value)) return value;
  return `${getBaseUrl()}${value.startsWith("/") ? value : `/${value}`}`;
}

async function fetchWithTimeout(url: string, init: RequestInit = {}): Promise<Response> {
  const controller = new AbortController();
  const externalSignal = init.signal;
  let timedOut = false;
  const forwardAbort = () => controller.abort();

  if (externalSignal?.aborted) controller.abort();
  else externalSignal?.addEventListener?.("abort", forwardAbort, { once: true });

  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, REQUEST_TIMEOUT_MS);

  try {
    return await fetch(url, { ...init, signal: controller.signal });
  } catch (err) {
    if (timedOut) {
      throw new ApiError("انتهت مهلة الاتصال بالخادم. تحقق من الإنترنت ثم أعد المحاولة.", 408);
    }
    if (externalSignal?.aborted) throw err;
    if (err instanceof ApiError) throw err;
    throw new ApiError("تعذر الاتصال بالخادم. تحقق من اتصال الإنترنت ثم أعد المحاولة.", 0);
  } finally {
    clearTimeout(timer);
    externalSignal?.removeEventListener?.("abort", forwardAbort);
  }
}

async function parsePayload(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return null;

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function messageFromPayload(payload: unknown, fallback: string): string {
  if (
    payload &&
    typeof payload === "object" &&
    "message" in payload &&
    typeof (payload as { message?: unknown }).message === "string"
  ) {
    return (payload as { message: string }).message;
  }

  return fallback;
}

let refreshInFlight: Promise<boolean> | null = null;

async function refreshAccessToken(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    const tokens = await readTokens();
    if (!tokens?.refreshToken) return false;

    try {
      const response = await fetchWithTimeout(`${getBaseUrl()}/api/auth/mobile/refresh-token`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          [MOBILE_REFRESH_HEADER]: tokens.refreshToken
        }
      });

      if (!response.ok) {
        if (response.status === 401) await clearTokens();
        return false;
      }

      const payload = (await response.json()) as MobileRefreshResponse;
      if (!payload.accessToken || !payload.refreshToken) {
        await clearTokens();
        return false;
      }

      await writeTokens(payload);
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  retryOnUnauthorized = true
): Promise<T> {
  const tokens = await readTokens();
  const headers = new Headers(init.headers);

  headers.set("Accept", "application/json");
  const isMultipart = typeof FormData !== "undefined" && init.body instanceof FormData;
  if (init.body && !isMultipart && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (tokens?.accessToken) {
    headers.set("Authorization", `Bearer ${tokens.accessToken}`);
  }

  // Deliberately no automatic retry for mutations. A duplicated POST/PUT could create
  // a second payment, visit, prescription, lab order, or accounting entry. The only
  // automatic retry below is the existing token-refresh replay after an explicit 401.
  const response = await fetchWithTimeout(`${getBaseUrl()}${path}`, {
    ...init,
    headers
  });

  if (response.status === 401 && retryOnUnauthorized && tokens?.refreshToken) {
    const refreshed = await refreshAccessToken();
    if (refreshed) return apiRequest<T>(path, init, false);
  }

  const payload = await parsePayload(response);
  if (!response.ok) {
    throw new ApiError(
      messageFromPayload(payload, `تعذر تنفيذ الطلب (${response.status})`),
      response.status,
      payload
    );
  }

  return payload as T;
}

export async function mobileLogin(
  username: string,
  password: string
): Promise<MobileLoginResponse> {
  const response = await fetchWithTimeout(`${getBaseUrl()}/api/auth/mobile/login`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ username, password })
  });

  const payload = await parsePayload(response);
  if (!response.ok) {
    throw new ApiError(
      messageFromPayload(payload, "تعذر تسجيل الدخول"),
      response.status,
      payload
    );
  }

  const session = normalizeMobileLoginResponse(payload);
  if (!session) {
    throw new ApiError("استجابة تسجيل الدخول غير مكتملة", 500, payload);
  }

  await writeTokens({
    accessToken: session.accessToken,
    refreshToken: session.refreshToken
  });

  return session;
}

export async function mobileLogout(): Promise<void> {
  const tokens = await readTokens();

  try {
    if (tokens?.accessToken) {
      await apiRequest<void>("/api/auth/mobile/logout", {
        method: "POST",
        headers: tokens.refreshToken
          ? { [MOBILE_REFRESH_HEADER]: tokens.refreshToken }
          : undefined
      });
    }
  } finally {
    await clearTokens();
  }
}
