import { clearTokens, readTokens, writeTokens } from "@/auth/tokenStore";
import type { PatientAuthResponse } from "@/lib/types";

const PORTAL_PREFIX = "/api/portal";
const MOBILE_REFRESH_HEADER = "X-Aqlan-Portal-Refresh-Token";

function baseUrl(): string {
  const configured = process.env.EXPO_PUBLIC_API_URL?.trim().replace(/\/+$/, "");
  if (!configured) {
    if (__DEV__) return "http://127.0.0.1:5000";
    throw new Error("رابط خدمة بوابة المرضى غير مضبوط.");
  }
  if (!__DEV__ && !configured.startsWith("https://")) {
    throw new Error("بوابة المرضى تتطلب اتصال HTTPS.");
  }
  return configured;
}

/**
 * MOBILE-REQ-011: resolve every patient request inside the portal namespace
 * after URL normalization. A textual prefix check alone is not sufficient:
 * "/../auth/me" would otherwise normalize from /api/portal to /api/auth.
 */
export function portalUrl(path: string): string {
  if (!path.startsWith("/") || path.startsWith("//") || path.startsWith("/api/")) {
    throw new Error("مسار بوابة المرضى غير صالح.");
  }

  const origin = new URL(`${baseUrl()}/`);
  const resolved = new URL(`${PORTAL_PREFIX}${path}`, origin);

  if (
    resolved.origin !== origin.origin ||
    (resolved.pathname !== PORTAL_PREFIX &&
      !resolved.pathname.startsWith(`${PORTAL_PREFIX}/`))
  ) {
    throw new Error("مسار بوابة المرضى غير صالح.");
  }

  return resolved.toString();
}

async function payload(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function errorMessage(value: unknown, fallback: string): string {
  if (value && typeof value === "object" && "message" in value) {
    const message = (value as { message?: unknown }).message;
    if (typeof message === "string") return message;
  }
  return fallback;
}

let refreshInFlight: Promise<boolean> | null = null;

async function refresh(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight;
  refreshInFlight = (async () => {
    const tokens = await readTokens();
    if (!tokens) return false;
    try {
      const response = await fetch(portalUrl("/mobile/auth/refresh-token"), {
        method: "POST",
        headers: {
          Accept: "application/json",
          Authorization: `Bearer ${tokens.accessToken}`,
          [MOBILE_REFRESH_HEADER]: tokens.refreshToken
        }
      });
      if (!response.ok) {
        await clearTokens();
        return false;
      }
      const next = (await response.json()) as PatientAuthResponse;
      await writeTokens({ accessToken: next.accessToken, refreshToken: next.refreshToken });
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

export async function portalRequest<T>(
  path: string,
  init: RequestInit = {},
  retry = true
): Promise<T> {
  const requestUrl = portalUrl(path);
  const tokens = await readTokens();
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  if (tokens?.accessToken) headers.set("Authorization", `Bearer ${tokens.accessToken}`);

  const response = await fetch(requestUrl, { ...init, headers });
  if (response.status === 401 && retry && tokens?.refreshToken && (await refresh())) {
    return portalRequest<T>(path, init, false);
  }

  const body = await payload(response);
  if (!response.ok) throw new Error(errorMessage(body, `تعذر تنفيذ الطلب (${response.status})`));
  return body as T;
}

export async function patientLogin(username: string, password: string): Promise<PatientAuthResponse> {
  const response = await fetch(portalUrl("/mobile/auth/login"), {
    method: "POST",
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    body: JSON.stringify({ username, password })
  });
  const body = await payload(response);
  if (!response.ok) throw new Error(errorMessage(body, "تعذر تسجيل الدخول"));
  const session = body as PatientAuthResponse;
  if (!session.accessToken || !session.refreshToken || !session.profile) {
    throw new Error("استجابة تسجيل الدخول غير مكتملة.");
  }
  await writeTokens({ accessToken: session.accessToken, refreshToken: session.refreshToken });
  return session;
}

export async function patientLogout(): Promise<void> {
  const tokens = await readTokens();
  try {
    if (tokens) {
      await portalRequest("/mobile/auth/logout", {
        method: "POST",
        headers: { [MOBILE_REFRESH_HEADER]: tokens.refreshToken }
      });
    }
  } finally {
    await clearTokens();
  }
}
