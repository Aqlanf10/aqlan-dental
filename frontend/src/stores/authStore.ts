"use client";
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { UserDto, LoginRequest } from "@/types/auth";
import api, {
  clearAccessToken,
  IMPERSONATION_SESSION_MARKER,
  setAccessToken,
  setImpersonatingSession,
} from "@/lib/api";

/** Helper: set/clear the non-secret routing sentinel used by middleware. */
function setAuthCookie(authenticated: boolean) {
  if (typeof document === "undefined") return;
  const isHttps = typeof window !== "undefined" && window.location.protocol === "https:";
  const secureFlag = isHttps ? "; Secure" : "";
  document.cookie = `aqlan_auth_status=${
    authenticated ? "authenticated" : ""
  }; path=/; max-age=${authenticated ? 60 * 60 * 24 * 7 : 0}; SameSite=Strict${secureFlag}`;
}

function clearLegacyTokenStorage() {
  if (typeof window === "undefined") return;
  // W04: remove values written by older releases. Staff access tokens now live
  // only in module memory; the refresh credential is an HttpOnly API cookie.
  localStorage.removeItem("access_token");
  localStorage.removeItem("aqlan_original_token");
}

function markImpersonationActive(active: boolean) {
  if (typeof window === "undefined") return;
  if (active) localStorage.setItem(IMPERSONATION_SESSION_MARKER, "1");
  else localStorage.removeItem(IMPERSONATION_SESSION_MARKER);
}

interface AuthState {
  user: UserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  originalUser: UserDto | null;
  isImpersonating: boolean;
  login: (credentials: LoginRequest) => Promise<boolean>;
  logout: () => Promise<void>;
  fetchMe: () => Promise<void>;
  startImpersonation: (accessToken: string, user: UserDto) => Promise<void>;
  stopImpersonation: () => Promise<void>;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      isAuthenticated: false,
      isLoading: false,
      originalUser: null,
      isImpersonating: false,

      login: async (credentials) => {
        set({ isLoading: true });
        try {
          const { data } = await api.post<{ accessToken: string; user: UserDto; mustChangePassword?: boolean }>(
            "/api/auth/login",
            credentials
          );
          clearLegacyTokenStorage();
          markImpersonationActive(false);
          setAccessToken(data.accessToken);
          setImpersonatingSession(false);
          setAuthCookie(true);

          try {
            const { data: permData } = await api.get<{ role: string; permissions: string[] }>("/api/auth/me/permissions");
            data.user.permissions = permData.permissions;
          } catch {
            // Permissions fetch failed — continue without permissions.
          }

          set({ user: data.user, isAuthenticated: true, originalUser: null, isImpersonating: false });
          return !!(data.mustChangePassword || data.user.mustChangePassword);
        } catch (error: unknown) {
          throw error;
        } finally {
          set({ isLoading: false });
        }
      },

      logout: async () => {
        try {
          await api.post("/api/auth/logout");
        } catch {
          // Local cleanup still runs.
        }
        clearAccessToken();
        setImpersonatingSession(false);
        clearLegacyTokenStorage();
        markImpersonationActive(false);
        setAuthCookie(false);
        set({ user: null, isAuthenticated: false, originalUser: null, isImpersonating: false });
      },

      fetchMe: async () => {
        try {
          const { data } = await api.get<UserDto>("/api/auth/me");
          setAuthCookie(true);

          try {
            const { data: permData } = await api.get<{ role: string; permissions: string[] }>("/api/auth/me/permissions");
            data.permissions = permData.permissions;
          } catch {
            // Permissions fetch failed — user will have limited UI but can still function.
          }

          set({ user: data, isAuthenticated: true });
        } catch {
          clearAccessToken();
          setImpersonatingSession(false);
          clearLegacyTokenStorage();
          markImpersonationActive(false);
          setAuthCookie(false);
          set({ user: null, isAuthenticated: false, originalUser: null, isImpersonating: false });
        }
      },

      startImpersonation: async (targetAccessToken: string, user: UserDto) => {
        const currentUser = get().user;
        if (!currentUser) return;

        clearLegacyTokenStorage();
        markImpersonationActive(true);
        setAccessToken(targetAccessToken);
        setImpersonatingSession(true);
        setAuthCookie(true);
        set({
          originalUser: currentUser,
          isImpersonating: true,
          user,
          isAuthenticated: true,
        });
      },

      stopImpersonation: async () => {
        if (!get().isImpersonating) return;
        set({ isLoading: true });

        try {
          // First revoke the target user's refresh cookie while retaining the
          // impersonated access token in memory. The next call still carries the
          // originalUserId claims and asks the backend to issue a brand-new admin
          // session, which is the only accepted restoration path.
          await api.post("/api/auth/logout");

          const { data } = await api.post<{
            accessToken: string;
            user: UserDto;
            isImpersonating: false;
          }>("/api/auth/stop-impersonation");

          setAccessToken(data.accessToken);
          setImpersonatingSession(false);
          clearLegacyTokenStorage();
          markImpersonationActive(false);
          setAuthCookie(true);

          try {
            const { data: permData } = await api.get<{ role: string; permissions: string[] }>("/api/auth/me/permissions");
            data.user.permissions = permData.permissions;
          } catch {
            // The restored administrator can reload permissions later.
          }

          set({
            user: data.user,
            originalUser: null,
            isImpersonating: false,
            isAuthenticated: true,
          });
        } catch (error) {
          // Never restore a cached administrator token after a failed stop. The
          // only safe fallback is a fresh login.
          clearAccessToken();
          setImpersonatingSession(false);
          clearLegacyTokenStorage();
          markImpersonationActive(false);
          setAuthCookie(false);
          set({
            user: null,
            originalUser: null,
            isImpersonating: false,
            isAuthenticated: false,
          });
          throw error;
        } finally {
          set({ isLoading: false });
        }
      },
    }),
    {
      name: "aqlan-auth",
      // Never persist impersonation recovery material. A page reload during an
      // impersonated session is detected through a non-secret marker and revoked
      // before any target session can be restored.
      partialize: (s) => ({
        user: s.isImpersonating ? null : s.user,
        isAuthenticated: s.isImpersonating ? false : s.isAuthenticated,
      }),
      onRehydrateStorage: () => (state) => {
        clearAccessToken();
        setImpersonatingSession(false);
        clearLegacyTokenStorage();
        if (state?.isAuthenticated) setAuthCookie(true);
      },
    }
  )
);