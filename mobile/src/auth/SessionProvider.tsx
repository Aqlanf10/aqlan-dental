import { apiRequest, mobileLogin, mobileLogout } from "@/lib/api";
import { readTokens, replaceAccessToken } from "@/auth/tokenStore";
import type { StaffUser, UserPermissions } from "@/lib/types";
import { normalizePermissions, normalizeStaffUser } from "@/lib/session";
import React, {
  createContext,
  type PropsWithChildren,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState
} from "react";

type SessionContextValue = {
  isLoading: boolean;
  user: StaffUser | null;
  permissions: string[];
  signIn: (username: string, password: string) => Promise<StaffUser>;
  signOut: () => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  reload: () => Promise<void>;
  can: (permission: string) => boolean;
};

const SessionContext = createContext<SessionContextValue | null>(null);

export function SessionProvider({ children }: PropsWithChildren) {
  const [isLoading, setIsLoading] = useState(true);
  const [user, setUser] = useState<StaffUser | null>(null);
  const [permissions, setPermissions] = useState<string[]>([]);

  const loadSession = useCallback(async () => {
    setIsLoading(true);
    try {
      const tokens = await readTokens();
      if (!tokens) {
        setUser(null);
        setPermissions([]);
        return;
      }

      const [meResult, permissionResult] = await Promise.allSettled([
        apiRequest<unknown>("/api/auth/me"),
        apiRequest<unknown>("/api/auth/me/permissions")
      ]);
      if (meResult.status === "rejected") throw meResult.reason;
      const me = normalizeStaffUser(meResult.value);
      if (!me) throw new Error("استجابة الحساب غير مكتملة.");
      setUser(me);
      setPermissions(permissionResult.status === "fulfilled" ? normalizePermissions(permissionResult.value).permissions : []);
    } catch {
      setUser(null);
      setPermissions([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSession();
  }, [loadSession]);

  const signIn = useCallback(async (username: string, password: string) => {
    const response = await mobileLogin(username, password);
    setUser(response.user);

    try {
      const permissionResponse = await apiRequest<unknown>("/api/auth/me/permissions");
      setPermissions(normalizePermissions(permissionResponse).permissions);
    } catch {
      setPermissions([]);
    }

    return response.user;
  }, []);

  const signOut = useCallback(async () => {
    try {
      await mobileLogout();
    } finally {
      setUser(null);
      setPermissions([]);
    }
  }, []);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    const response = await apiRequest<{ accessToken: string }>("/api/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword })
    });

    if (!response.accessToken) throw new Error("لم يُرجع الخادم جلسة محدثة.");
    await replaceAccessToken(response.accessToken);

    const me = normalizeStaffUser(await apiRequest<unknown>("/api/auth/me"));
    if (!me) throw new Error("استجابة الحساب بعد تغيير كلمة المرور غير مكتملة.");
    setUser(me);
  }, []);

  const can = useCallback(
    (permission: string) =>
      user?.role?.toLowerCase() === "admin" || permissions.includes(permission),
    [permissions, user?.role]
  );

  const value = useMemo<SessionContextValue>(
    () => ({
      isLoading,
      user,
      permissions,
      signIn,
      signOut,
      changePassword,
      reload: loadSession,
      can
    }),
    [can, changePassword, isLoading, loadSession, permissions, signIn, signOut, user]
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (!value) throw new Error("useSession must be used inside SessionProvider");
  return value;
}
