import { createContext, type PropsWithChildren, useCallback, useContext, useEffect, useMemo, useState } from 'react';

import { ApiError } from '@/api/client';
import { authApi } from '@/api/authApi';
import type { Permissions, Session, StaffUser } from '@/types/auth';
import { clearSession, loadStoredSession, saveSession } from './sessionStorage';

type AuthContextValue = {
  initializing: boolean;
  busy: boolean;
  user: StaffUser | null;
  permissions: Permissions | null;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<Session | null>(null);
  const [permissions, setPermissions] = useState<Permissions | null>(null);
  const [initializing, setInitializing] = useState(true);
  const [busy, setBusy] = useState(false);

  const establishSession = useCallback(async (candidate: Session) => {
    let active = candidate;
    let user: StaffUser;
    let nextPermissions: Permissions;
    try {
      [user, nextPermissions] = await Promise.all([
        authApi.me(active.accessToken),
        authApi.permissions(active.accessToken),
      ]);
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error;
      const refreshed = await authApi.refresh(active.refreshToken);
      active = { ...active, ...refreshed };
      [user, nextPermissions] = await Promise.all([
        authApi.me(active.accessToken),
        authApi.permissions(active.accessToken),
      ]);
    }

    active = { ...active, user };
    await saveSession(active);
    setSession(active);
    setPermissions(nextPermissions);
  }, []);

  useEffect(() => {
    let active = true;
    loadStoredSession().then(async (stored) => {
      if (!stored || !active) return;
      try {
        await establishSession(stored);
      } catch {
        await clearSession();
      }
    }).finally(() => {
      if (active) setInitializing(false);
    });
    return () => { active = false; };
  }, [establishSession]);

  const signIn = useCallback(async (username: string, password: string) => {
    setBusy(true);
    try {
      const next = await authApi.login(username.trim(), password);
      const nextPermissions = await authApi.permissions(next.accessToken);
      await saveSession(next);
      setSession(next);
      setPermissions(nextPermissions);
    } finally {
      setBusy(false);
    }
  }, []);

  const signOut = useCallback(async () => {
    const current = session;
    setBusy(true);
    setSession(null);
    setPermissions(null);
    await clearSession();
    try {
      if (current) await authApi.logout(current.accessToken, current.refreshToken);
    } catch {
      // Local logout is authoritative; remote token expiry/revocation remains server-managed.
    } finally {
      setBusy(false);
    }
  }, [session]);

  const value = useMemo<AuthContextValue>(() => ({
    initializing,
    busy,
    user: session?.user ?? null,
    permissions,
    signIn,
    signOut,
  }), [busy, initializing, permissions, session?.user, signIn, signOut]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside AuthProvider');
  return value;
}
