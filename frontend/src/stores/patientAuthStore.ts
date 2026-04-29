"use client";
import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { PatientPortalProfile } from "@/types/patientPortal";

function setPortalCookie(authenticated: boolean) {
  if (typeof document === "undefined") return;
  document.cookie = `aqlan_portal_auth=${
    authenticated ? "authenticated" : ""
  }; path=/portal; max-age=${authenticated ? 60 * 60 * 24 * 7 : 0}; SameSite=Strict`;
}

interface PatientAuthState {
  profile: PatientPortalProfile | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  phoneNumber: string;
  setAuth: (profile: PatientPortalProfile, token: string) => void;
  logout: () => void;
  setPhoneNumber: (phone: string) => void;
}

export const usePatientAuthStore = create<PatientAuthState>()(
  persist(
    (set) => ({
      profile: null,
      isAuthenticated: false,
      isLoading: false,
      phoneNumber: "",

      setAuth: (profile, token) => {
        localStorage.setItem("portal_token", token);
        setPortalCookie(true);
        set({ profile, isAuthenticated: true });
      },

      logout: () => {
        localStorage.removeItem("portal_token");
        setPortalCookie(false);
        set({ profile: null, isAuthenticated: false });
      },

      setPhoneNumber: (phone) => set({ phoneNumber: phone }),
    }),
    {
      name: "aqlan-patient-auth",
      partialize: (s) => ({ profile: s.profile, isAuthenticated: s.isAuthenticated }),
      onRehydrateStorage: () => (state) => {
        if (state?.isAuthenticated) {
          setPortalCookie(true);
        }
      },
    }
  )
);
