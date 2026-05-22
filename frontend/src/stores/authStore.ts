"use client";
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { UserDto, LoginRequest } from "@/types/auth";
import api from "@/lib/api";

/** Helper: set/clear auth cookie for middleware */
function setAuthCookie(authenticated: boolean) {
  if (typeof document === "undefined") return;
  document.cookie = `aqlan_auth_status=${
    authenticated ? "authenticated" : ""
  }; path=/; max-age=${authenticated ? 60 * 60 * 24 * 7 : 0}; SameSite=Strict`;
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
          setAuthCookie(true);
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
          setAuthCookie(true);
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
          setAuthCookie(true);
        }
      },
    }
  )
);
