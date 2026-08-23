import { createContext, type PropsWithChildren, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';

import { ApiError, requestJson, type RequestOptions } from '@/api/client';
import { authApi } from '@/api/authApi';
import type { Permissions, Session, StaffUser } from '@/types/auth';
import { clearSession, loadStoredSession, saveSession } from './sessionStorage';

type AuthContextValue = {
  initializing: boolean;
  busy: boolean;
  user: StaffUser | null;
  permissions: Permissions | null;
  hasPermission: (permission: string) => boolean;
  request: (path: string, options?: RequestOptions) => Promise<unknown>;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<Session | null>(null);
  const sessionRef = useRef<Session | null>(null);
  const refreshPromiseRef = useRef<Promise<Session> | null>(null);
  const [permissions, setPermissions] = useState<Permissions | null>(null);
  const [initializing, setInitializing] = useState(true);
  const [busy, setBusy] = useState(false);

  const storeActiveSession = useCallback(async (next: Session) => {
    sessionRef.current = next;
    setSession(next);
    await saveSession(next);
  }, []);

  const discardSession = useCallback(async () => {
    sessionRef.current = null;
    setSession(null);
    setPermissions(null);
    await clearSession();
  }, []);

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
    await storeActiveSession(active);
    setPermissions(nextPermissions);
  }, [storeActiveSession]);

  useEffect(() => {
    let active = true;
    loadStoredSession().then(async (stored) => {
      if (!stored || !active) return;
      try {
        await establishSession(stored);
      } catch {
        await discardSession();
      }
    }).finally(() => {
      if (active) setInitializing(false);
    });
    return () => { active = false; };
  }, [discardSession, establishSession]);

  const signIn = useCallback(async (username: string, password: string) => {
    setBusy(true);
    try {
      const next = await authApi.login(username.trim(), password);
      const nextPermissions = await authApi.permissions(next.accessToken);
      await storeActiveSession(next);
      setPermissions(nextPermissions);
    } finally {
      setBusy(false);
    }
  }, [storeActiveSession]);

  const refreshSession = useCallback(async () => {
    const current = sessionRef.current;
    if (!current) throw new ApiError('Session expired', 401, 'server');
    if (refreshPromiseRef.current) return refreshPromiseRef.current;

    const refreshPromise = (async () => {
      const refreshed = await authApi.refresh(current.refreshToken);
      const next = { ...current, ...refreshed };
      await storeActiveSession(next);
      return next;
    })();
    refreshPromiseRef.current = refreshPromise;
    try {
      return await refreshPromise;
    } finally {
      refreshPromiseRef.current = null;
    }
  }, [storeActiveSession]);

  const request = useCallback(async (path: string, options: RequestOptions = {}) => {
    const current = sessionRef.current;
    if (!current) throw new ApiError('Session expired', 401, 'server');

    try {
      return await requestJson(path, { ...options, accessToken: current.accessToken });
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error;
      const method = (options.method ?? 'GET').toUpperCase();
      if (method !== 'GET') {
        await discardSession();
        throw new ApiError('Session expired', 401, 'server');
      }
      try {
        const refreshed = await refreshSession();
        return await requestJson(path, { ...options, accessToken: refreshed.accessToken });
      } catch (refreshError) {
        if (refreshError instanceof ApiError && refreshError.status === 401) {
          await discardSession();
          throw new ApiError('Session expired', 401, 'server');
        }
        throw refreshError;
      }
    }
  }, [discardSession, refreshSession]);

  const signOut = useCallback(async () => {
    const current = session;
    setBusy(true);
    await discardSession();
    try {
      if (current) await authApi.logout(current.accessToken, current.refreshToken);
    } catch {
      // Local logout is authoritative; remote token expiry/revocation remains server-managed.
    } finally {
      setBusy(false);
    }
  }, [discardSession, session]);

  const hasPermission = useCallback((permission: string) => (
    session?.user.role === 'Admin' || permissions?.permissions.includes(permission) === true
  ), [permissions?.permissions, session?.user.role]);

  const value = useMemo<AuthContextValue>(() => ({
    initializing,
    busy,
    user: session?.user ?? null,
    permissions,
    hasPermission,
    request,
    signIn,
    signOut,
  }), [busy, hasPermission, initializing, permissions, request, session?.user, signIn, signOut]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside AuthProvider');
  return value;
}
