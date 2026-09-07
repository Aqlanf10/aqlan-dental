import { secureStorage } from '@/storage/secureStorage';
import type { Session } from '@/types/auth';

const SESSION_KEY = 'aqlan.session.v2';

export async function loadStoredSession(): Promise<Session | null> {
  const raw = await secureStorage.get(SESSION_KEY);
  if (!raw) return null;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return null;
    const session = parsed as Partial<Session>;
    if (typeof session.accessToken !== 'string' || typeof session.refreshToken !== 'string' || !session.user) return null;
    return session as Session;
  } catch {
    await secureStorage.remove(SESSION_KEY);
    return null;
  }
}

export async function saveSession(session: Session) {
  await secureStorage.set(SESSION_KEY, JSON.stringify(session));
}

export async function clearSession() {
  await secureStorage.remove(SESSION_KEY);
}
