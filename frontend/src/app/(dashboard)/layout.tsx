"use client";

import { Sidebar } from "@/components/layout/Sidebar";
import { Topbar } from "@/components/layout/Topbar";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import { useEffect, useState, useCallback } from "react";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { isAuthenticated, fetchMe, user } = useAuthStore();
  const router = useRouter();
  const [isReady, setIsReady] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed((prev) => !prev);
  }, []);

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

  if (!isReady) {
    return (
      <div className="flex h-screen items-center justify-center bg-[#eef3f9]">
        <div className="text-center">
          <div className="w-12 h-12 bg-[#3d7ab5] rounded-2xl flex items-center justify-center mx-auto mb-4 animate-pulse">
            <svg className="w-6 h-6 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
            </svg>
          </div>
          <p className="text-[#0d2137]/60 text-sm">جارٍ تحميل النظام...</p>
        </div>
      </div>
    );
  }

  // Sidebar width offset: expanded = 258px, collapsed = 64px
  const sidebarOffsetClass = sidebarCollapsed ? "lg:mr-16" : "lg:mr-[258px]";

  return (
    <div className="flex h-screen overflow-hidden bg-[#eef3f9]">
      {/* Sidebar — fixed, right side in RTL */}
      <Sidebar collapsed={sidebarCollapsed} onToggleCollapse={toggleSidebar} />

      {/* Main content — offset by sidebar width on desktop */}
      <div className={`flex-1 flex flex-col overflow-hidden transition-[margin] duration-[250ms] ease-in-out ${sidebarOffsetClass}`}>
        <Topbar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
