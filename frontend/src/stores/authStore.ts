"use client";
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { UserDto, LoginRequest } from "@/types/auth";
import api from "@/lib/api";

/** Helper: set/clear auth cookie for middleware */
function setAuthCookie(authenticated: boolean, token?: string | null) {
  if (typeof document === "undefined") return;
  // NAV-CEPH-FIX (Part 2): the access-token cookie must carry the Secure flag on HTTPS
  // origins. Without it, browsers silently drop the cookie when the production frontend
  // (Vercel HTTPS) talks to the backend /uploads/* endpoint (proxied same-origin via the
  // Next.js rewrite) → image requests go out unauthenticated → 401 → ceph X-rays and
  // clinical photos fail to load. Computed once per call (login/logout/rehydrate).
  const isHttps = typeof window !== "undefined" && window.location.protocol === "https:";
  const secureFlag = isHttps ? "; Secure" : "";
  // SEC-03: aqlan_auth_status is the sentinel cookie for Next.js middleware routing (existing).
  document.cookie = `aqlan_auth_status=${
    authenticated ? "authenticated" : ""
  }; path=/; max-age=${authenticated ? 60 * 60 * 24 * 7 : 0}; SameSite=Strict${secureFlag}`;

  // SEC-03: aqlan_access_token carries the real JWT so the backend /uploads/* auth middleware
  // (Program.cs) can validate it for <img src="/uploads/..."> requests. Browsers send cookies
  // automatically on same-origin requests, so images render without manual headers. Cleared on
  // logout. Note: this is NOT HttpOnly because the frontend still reads access_token from
  // localStorage for axios — the cookie is a SECONDARY copy for image requests only.
  // NAV-CEPH-FIX (Part 2): Secure flag added on HTTPS so the cookie survives cross-origin-via-
  // rewrite same-origin requests in production.
  if (authenticated && token) {
    document.cookie = `aqlan_access_token=${token}; path=/; max-age=${60 * 60 * 24 * 7}; SameSite=Strict${secureFlag}`;
  } else {
    document.cookie = `aqlan_access_token=; path=/; max-age=0; SameSite=Strict${secureFlag}`;
  }
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
  startImpersonation: (accessToken: string, user: UserDto) => void;
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
          localStorage.setItem("access_token", data.accessToken);
          setAuthCookie(true, data.accessToken);

          // Fetch user permissions after login
          try {
            const { data: permData } = await api.get<{ role: string; permissions: string[] }>("/api/auth/me/permissions");
            data.user.permissions = permData.permissions;
          } catch {
            // Permissions fetch failed — continue without permissions
          }

          set({ user: data.user, isAuthenticated: true, originalUser: null, isImpersonating: false });
          // Return true if user must change password so caller can redirect
          return !!(data.mustChangePassword || data.user.mustChangePassword);
        } catch (error: unknown) {
          // Re-throw so login page can show appropriate error message
          // (network errors vs auth errors need different messages)
          throw error;
        } finally {
          set({ isLoading: false });
        }
      },

      logout: async () => {
        try {
          await api.post("/api/auth/logout");
        } catch {
          // ignore logout API errors
        }
        localStorage.removeItem("access_token");
        setAuthCookie(false);
        set({ user: null, isAuthenticated: false, originalUser: null, isImpersonating: false });
      },

      fetchMe: async () => {
        try {
          const { data } = await api.get<UserDto>("/api/auth/me");
          setAuthCookie(true, localStorage.getItem("access_token"));

          // Fetch user permissions
          try {
            const { data: permData } = await api.get<{ role: string; permissions: string[] }>("/api/auth/me/permissions");
            data.permissions = permData.permissions;
          } catch {
            // Permissions fetch failed — user will have limited UI but can still function
          }

          set({ user: data, isAuthenticated: true });
        } catch {
          localStorage.removeItem("access_token");
          setAuthCookie(false);
          set({ user: null, isAuthenticated: false, isImpersonating: false });
        }
      },

      startImpersonation: (accessToken: string, user: UserDto) => {
        const currentUser = get().user;
        if (!currentUser) return;
        // Store the original user and token for restoration
        localStorage.setItem("aqlan_original_token", localStorage.getItem("access_token") ?? "");
        localStorage.setItem("access_token", accessToken);
        // SEC-03: sync the access-token cookie so /uploads/* requests authenticate as the
        // impersonated user.
        setAuthCookie(true, accessToken);
        set({
          originalUser: currentUser,
          isImpersonating: true,
          user,
          isAuthenticated: true,
        });
      },

      stopImpersonation: async () => {
        const originalToken = localStorage.getItem("aqlan_original_token");
        if (originalToken) {
          localStorage.setItem("access_token", originalToken);
          localStorage.removeItem("aqlan_original_token");
        }
        // SEC-03: sync the access-token cookie back to the original user.
        setAuthCookie(true, originalToken);
        const { originalUser } = get();
        if (originalUser) {
          set({
            user: originalUser,
            originalUser: null,
            isImpersonating: false,
          });
        }
        // Also try to call the backend to invalidate the impersonation session
        try {
          await api.post("/api/auth/stop-impersonation");
        } catch {
          // Ignore errors - we've already restored the original token locally
        }
        // Refresh user data from backend to ensure consistency
        await get().fetchMe();
      },
    }),
    {
      name: "aqlan-auth",
      partialize: (s) => ({
        user: s.user,
        isAuthenticated: s.isAuthenticated,
        originalUser: s.originalUser,
        isImpersonating: s.isImpersonating,
      }),
      onRehydrateStorage: () => (state) => {
        // Sync cookie on rehydration
        if (state?.isAuthenticated) {
          setAuthCookie(true, localStorage.getItem("access_token"));
        }
      },
    }
  )
);
