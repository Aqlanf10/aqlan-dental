import { clearTokens, readTokens, writeTokens } from "@/auth/tokenStore";
import type { MobileLoginResponse, MobileRefreshResponse } from "@/lib/types";

const MOBILE_REFRESH_HEADER = "X-Aqlan-Refresh-Token";

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

export function apiAssetUrl(path: string): string {
  const value = path.trim();
  if (/^https?:\/\//i.test(value)) return value;
  return `${getBaseUrl()}${value.startsWith("/") ? value : `/${value}`}`;
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
      const response = await fetch(`${getBaseUrl()}/api/auth/mobile/refresh-token`, {
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
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (tokens?.accessToken) {
    headers.set("Authorization", `Bearer ${tokens.accessToken}`);
  }

  const response = await fetch(`${getBaseUrl()}${path}`, {
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
  const response = await fetch(`${getBaseUrl()}/api/auth/mobile/login`, {
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

  const session = payload as MobileLoginResponse;
  if (!session.accessToken || !session.refreshToken || !session.user) {
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
