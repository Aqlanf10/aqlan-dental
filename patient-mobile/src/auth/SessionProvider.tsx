import { clearTokens, readTokens, writeTokens } from "@/auth/tokenStore";
import { patientLogin, patientLogout, portalRequest } from "@/lib/api";
import type { PatientAuthResponse, PatientDashboard, PatientProfile } from "@/lib/types";
import React, { createContext, type PropsWithChildren, useCallback, useContext, useEffect, useMemo, useState } from "react";

type PatientSession = {
  loading: boolean;
  profile: PatientProfile | null;
  mustChangePassword: boolean;
  signIn: (username: string, password: string) => Promise<PatientAuthResponse>;
  signOut: () => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
};

const Context = createContext<PatientSession | null>(null);

export function PatientSessionProvider({ children }: PropsWithChildren) {
  const [loading, setLoading] = useState(true);
  const [profile, setProfile] = useState<PatientProfile | null>(null);
  const [mustChangePassword, setMustChangePassword] = useState(false);

  const restore = useCallback(async () => {
    try {
      if (!(await readTokens())) return;
      const dashboard = await portalRequest<PatientDashboard>("/dashboard");
      setProfile(dashboard.profile);
    } catch {
      await clearTokens();
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void restore(); }, [restore]);

  const signIn = useCallback(async (username: string, password: string) => {
    const session = await patientLogin(username, password);
    setProfile(session.profile);
    setMustChangePassword(session.mustChangePassword);
    return session;
  }, []);

  const signOut = useCallback(async () => {
    await patientLogout();
    setProfile(null);
    setMustChangePassword(false);
  }, []);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    const next = await portalRequest<PatientAuthResponse>("/mobile/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword, newPassword })
    });
    await writeTokens({ accessToken: next.accessToken, refreshToken: next.refreshToken });
    setProfile(next.profile);
    setMustChangePassword(next.mustChangePassword);
  }, []);

  const value = useMemo(() => ({ loading, profile, mustChangePassword, signIn, signOut, changePassword }), [loading, profile, mustChangePassword, signIn, signOut, changePassword]);
  return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function usePatientSession(): PatientSession {
  const value = useContext(Context);
  if (!value) throw new Error("usePatientSession must be inside PatientSessionProvider");
  return value;
}
