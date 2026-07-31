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
        setIsReady(true);
        return;
      }

      await fetchMe();
      if (!useAuthStore.getState().isAuthenticated) {
        router.push("/login");
      }
      setIsReady(true);
    };

    void validateSession();
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
      <div className="flex-1 flex flex-col overflow-hidden lg:mr-64">
        <Topbar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
      <Toaster />
    </div>
  );
}
