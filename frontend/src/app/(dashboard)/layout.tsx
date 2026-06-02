"use client";

import { Sidebar } from "@/components/layout/Sidebar";
import { Topbar } from "@/components/layout/Topbar";
import { ImpersonationBanner } from "@/components/layout/ImpersonationBanner";
import { Toaster } from "@/components/ui/toaster";
import { useAuthStore } from "@/stores/authStore";
import { useRouter, usePathname } from "next/navigation";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useSignalRMessaging } from "@/hooks/useSignalRMessaging";
import { isRouteAllowed } from "@/lib/routePermissions";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { isAuthenticated, fetchMe, user } = useAuthStore();
  const router = useRouter();
  const pathname = usePathname();
  const [isReady, setIsReady] = useState(false);

  // SignalR: اتصال فوري للمراسلة والإشعارات
  useSignalRMessaging();

  useEffect(() => {
    // If we have a persisted auth state but no user data, try to fetch it
    if (isAuthenticated && !user) {
      fetchMe().finally(() => setIsReady(true));
    } else if (!isAuthenticated) {
      // Check if there's a token and try to validate it
      const token = localStorage.getItem("access_token");
      if (token) {
        fetchMe().catch(() => {
          router.push("/login");
        }).finally(() => setIsReady(true));
      } else {
        router.push("/login");
      }
    } else {
      setIsReady(true);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
    // Intentionally excluded: this effect runs once on mount to validate the auth token.
    // Adding fetchMe/user/isAuthenticated would cause an infinite re-fetch loop since
    // fetchMe updates the user state, which would re-trigger this effect.

  // ─── Route Permission Guard ─────────────────────────────────────────
  const userRole = user?.role ?? null;
  if (isReady && !isRouteAllowed(pathname, userRole)) {
    return (
      <div className="flex items-center justify-center min-h-screen" dir="rtl">
        <div className="text-center">
          <div className="w-16 h-16 bg-red-50 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-red-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            </svg>
          </div>
          <h2 className="text-xl font-semibold">ليس لديك صلاحية للوصول إلى هذه الصفحة</h2>
          <p className="text-gray-500 mt-2">تواصل مع الإدارة إذا كنت تعتقد أن هذا خطأ</p>
          <Link href="/" className="mt-4 inline-block text-blue-600 hover:underline">العودة للرئيسية</Link>
        </div>
      </div>
    );
  }

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
      {/* Impersonation Banner — fixed at top */}
      <ImpersonationBanner />

      {/* Sidebar — fixed, right side in RTL */}
      <Sidebar />

      {/* Main content — offset by sidebar width on desktop */}
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
