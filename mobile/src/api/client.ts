import { API_URL } from './config';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number | null,
    readonly kind: 'network' | 'timeout' | 'server' | 'invalid-response',
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export type RequestOptions = RequestInit & {
  accessToken?: string;
  refreshToken?: string;
  timeoutMs?: number;
};

export async function requestJson(path: string, options: RequestOptions = {}): Promise<unknown> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), options.timeoutMs ?? 15_000);
  const headers = new Headers(options.headers);
  headers.set('Accept', 'application/json');
  if (options.body) headers.set('Content-Type', 'application/json');
  if (options.accessToken) headers.set('Authorization', `Bearer ${options.accessToken}`);
  if (options.refreshToken) headers.set('X-Aqlan-Refresh-Token', options.refreshToken);

  try {
    const { accessToken: _accessToken, refreshToken: _refreshToken, timeoutMs: _timeoutMs, ...fetchOptions } = options;
    const response = await fetch(`${API_URL}${path}`, { ...fetchOptions, headers, signal: controller.signal });
    const text = await response.text();
    let payload: unknown = null;
    if (text) {
      try {
        payload = JSON.parse(text);
      } catch {
        throw new ApiError('Invalid server response', response.status, 'invalid-response');
      }
    }

    if (!response.ok) {
      const serverMessage = readString(payload, 'message');
      throw new ApiError(serverMessage ?? `Request failed (${response.status})`, response.status, 'server');
    }
    return payload;
  } catch (error) {
    if (error instanceof ApiError) throw error;
    if (error instanceof Error && error.name === 'AbortError') {
      throw new ApiError('Request timed out', null, 'timeout');
    }
    throw new ApiError('Network request failed', null, 'network');
  } finally {
    clearTimeout(timeout);
  }
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function readString(value: unknown, key: string): string | null {
  if (!isRecord(value)) return null;
  return typeof value[key] === 'string' ? value[key] : null;
}

export function readBoolean(value: unknown, key: string, fallback = false): boolean {
  if (!isRecord(value)) return fallback;
  return typeof value[key] === 'boolean' ? value[key] : fallback;
}

export function readNumber(value: unknown, key: string): number | null {
  if (!isRecord(value)) return null;
  return typeof value[key] === 'number' && Number.isFinite(value[key]) ? value[key] : null;
}
