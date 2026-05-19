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
  login: (credentials: LoginRequest) => Promise<boolean>;
  logout: () => Promise<void>;
  fetchMe: () => Promise<void>;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      isAuthenticated: false,
      isLoading: false,

      login: async (credentials) => {
        set({ isLoading: true });
        try {
          const { data } = await api.post<{ accessToken: string; user: UserDto; mustChangePassword?: boolean }>(
            "/api/auth/login",
            credentials
          );
          localStorage.setItem("access_token", data.accessToken);
          setAuthCookie(true);
          set({ user: data.user, isAuthenticated: true });
          // Return true if user must change password so caller can redirect
          return !!(data.mustChangePassword || data.user.mustChangePassword);
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
        set({ user: null, isAuthenticated: false });
      },

      fetchMe: async () => {
        try {
          const { data } = await api.get<UserDto>("/api/auth/me");
          setAuthCookie(true);
          set({ user: data, isAuthenticated: true });
        } catch {
          localStorage.removeItem("access_token");
          setAuthCookie(false);
          set({ user: null, isAuthenticated: false });
        }
      },
    }),
    {
      name: "aqlan-auth",
      partialize: (s) => ({ user: s.user, isAuthenticated: s.isAuthenticated }),
      onRehydrateStorage: () => (state) => {
        // Sync cookie on rehydration
        if (state?.isAuthenticated) {
          setAuthCookie(true);
        }
      },
    }
  )
);
