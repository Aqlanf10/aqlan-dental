"use client";
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { UserDto, LoginRequest } from "@/types/auth";
import api from "@/lib/api";

interface AuthState {
  user: UserDto | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
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
          const { data } = await api.post<{ accessToken: string; user: UserDto }>(
            "/api/auth/login",
            credentials
          );
          sessionStorage.setItem("access_token", data.accessToken);
          set({ user: data.user, isAuthenticated: true });
        } finally {
          set({ isLoading: false });
        }
      },

      logout: async () => {
        try {
          await api.post("/api/auth/logout");
        } catch {}
        sessionStorage.removeItem("access_token");
        set({ user: null, isAuthenticated: false });
      },

      fetchMe: async () => {
        try {
          const { data } = await api.get<UserDto>("/api/auth/me");
          set({ user: data, isAuthenticated: true });
        } catch {
          set({ user: null, isAuthenticated: false });
        }
      },
    }),
    {
      name: "aqlan-auth",
      partialize: (s) => ({ user: s.user, isAuthenticated: s.isAuthenticated }),
    }
  )
);
