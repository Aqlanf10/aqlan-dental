import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { PatientPortalProfile } from "@/types/patientPortal";
import portalApi from "@/lib/portalApi";

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
  mustChangePassword: boolean;
  /**
   * SEC-10: the refresh token is now delivered as an HttpOnly cookie set by the
   * backend (`portal_refresh`). The 4th `refreshToken` parameter is RETAINED
   * ONLY for backward-compat with existing call sites — it is IGNORED (no-op).
   * The frontend has no way to read or store the cookie (HttpOnly by design),
   * and the access token (short-lived) stays in localStorage.
   */
  setAuth: (profile: PatientPortalProfile, token: string, mustChangePassword?: boolean, refreshToken?: string) => void;
  logout: () => Promise<void>;
  setMustChangePassword: (val: boolean) => void;
}

export const usePatientAuthStore = create<PatientAuthState>()(
  persist(
    (set) => ({
      profile: null,
      isAuthenticated: false,
      isLoading: false,
      mustChangePassword: false,

      setAuth: (profile, token, mustChangePassword = false, _refreshToken) => {
        // SEC-10: only the short-lived access token is stored locally. The
        // refresh token lives in an HttpOnly cookie managed by the backend —
        // `_refreshToken` is intentionally ignored (kept for signature compat
        // so existing call sites can still pass it during the transition).
        void _refreshToken;
        localStorage.setItem("portal_token", token);
        setPortalCookie(true);
        set({ profile, isAuthenticated: true, mustChangePassword });
      },

      logout: async () => {
        // SEC-10: call backend logout so the HttpOnly cookie is deleted
        // (Set-Cookie: portal_refresh=; max-age=0) AND the persisted
        // refresh-token hash is cleared server-side. Best-effort — local
        // state is cleared regardless of network outcome.
        try {
          await portalApi.post("/api/portal/auth/logout");
        } catch {
          // ignore — local clear must still happen
        }
        localStorage.removeItem("portal_token");
        setPortalCookie(false);
        set({ profile: null, isAuthenticated: false, mustChangePassword: false });
      },

      setMustChangePassword: (val) => set({ mustChangePassword: val }),
    }),
    {
      name: "aqlan-patient-auth",
      partialize: (s) => ({ profile: s.profile, isAuthenticated: s.isAuthenticated, mustChangePassword: s.mustChangePassword }),
      onRehydrateStorage: () => (state) => {
        if (state?.isAuthenticated) {
          setPortalCookie(true);
        }
      },
    }
  )
);
