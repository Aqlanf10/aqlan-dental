"use client";

import { Sidebar } from "@/components/layout/Sidebar";
import { Topbar } from "@/components/layout/Topbar";
import { ImpersonationBanner } from "@/components/layout/ImpersonationBanner";
import { Toaster } from "@/components/ui/toaster";
import { useAuthStore } from "@/stores/authStore";
import {
  clearStaffBrowserSession,
  IMPERSONATION_SESSION_MARKER,
  terminateImpersonatedRefreshSession,
} from "@/lib/api";
import { useRouter, usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { useSignalRMessaging } from "@/hooks/useSignalRMessaging";
import { isRouteAllowed } from "@/lib/routePermissions";
import { resolveSessionBoot } from "@/lib/sessionBoot";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { fetchMe, user } = useAuthStore();
  const router = useRouter();
  const pathname = usePathname();
  const [isReady, setIsReady] = useState(false);

  useSignalRMessaging();

  useEffect(() => {
    // W04: access tokens are no longer persisted. A non-secret marker is retained
    // only to detect a page reload during impersonation. In that case revoke the
    // target refresh session before it can be refreshed into an ordinary target
    // login, clear local state, and require fresh administrator authentication.
    const validateSession = async () => {
      if (localStorage.getItem(IMPERSONATION_SESSION_MARKER) === "1") {
        await terminateImpersonatedRefreshSession();
        clearStaffBrowserSession();
        useAuthStore.setState({
          user: null,
          isAuthenticated: false,
          originalUser: null,
          isImpersonating: false,
          isLoading: false,
        });
        router.replace("/login");
        return; // the finally below opens the gate
      }

      // CORE-F-014: this used to be a bare `await fetchMe()` followed by
      // `setIsReady(true)`. If the call never settled — no axios timeout is configured, and
      // the 401 refresh interceptor parks concurrent requests in a queue that only drains
      // when the refresh itself answers — the gate below never opened and the user sat on
      // "جارٍ تحميل النظام..." indefinitely, with no error and no way out. A hard reload is
      // the common trigger, because access tokens are in-memory since W04 and a reload
      // therefore always depends on the silent refresh.
      const outcome = await resolveSessionBoot({
        fetchMe,
        isAuthenticated: () => useAuthStore.getState().isAuthenticated,
      });

      if (outcome.status === "redirect-to-login") {
        // Deliberately not clearing the session here: a stalled network is not proof that
        // the session is invalid, and wiping it would log out a user whose connection merely
        // hiccuped. If the session is in fact fine, /login sends them back.
        router.replace("/login");
      }
    };

    // Whatever happens above, the gate opens. Leaving it shut is the actual defect.
    void validateSession().finally(() => setIsReady(true));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const userRole = user?.role ?? null;
  useEffect(() => {
    if (isReady && userRole && !isRouteAllowed(pathname, userRole)) {
      router.replace("/daily-operations");
    }
  }, [isReady, pathname, userRole, router]);

  if (!isReady) {
    return (
      <div className="flex h-screen items-center justify-center" style={{ background: "#eef3f9" }}>
        <div className="text-center">
          <div className="w-12 h-12 clinic-gradient rounded-2xl flex items-center justify-center mx-auto mb-4 animate-pulse">
            <svg className="w-6 h-6 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
            </svg>
          </div>
          <p className="text-gray-500 text-sm">جارٍ تحميل النظام...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen overflow-hidden" style={{ background: "#eef3f9", direction: "rtl" }}>
      <ImpersonationBanner />
      <Sidebar />
      {/* CORE-REQ-006: logical margin so the content clears the sidebar on whichever side it is. */}
      <div className="flex-1 flex flex-col overflow-hidden lg:ms-64">
        <Topbar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
      <Toaster />
    </div>
  );
}
