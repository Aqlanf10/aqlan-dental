"use client";

import { Sidebar } from "@/components/layout/Sidebar";
import { Topbar } from "@/components/layout/Topbar";
import { Toaster } from "@/components/ui/toaster";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { isAuthenticated, fetchMe, user } = useAuthStore();
  const router = useRouter();
  const [isReady, setIsReady] = useState(false);

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
      <div className="flex h-screen items-center justify-center bg-gray-50">
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
    <div className="flex h-screen overflow-hidden bg-gray-50">
      {/* Sidebar — fixed, right side in RTL */}
      <Sidebar />

      {/* Main content — offset by sidebar width on desktop */}
      <div className="flex-1 flex flex-col overflow-hidden lg:mr-64">
        <Topbar />
        <main className="flex-1 overflow-y-auto p-4 lg:p-6">
          {children}
        </main>
      </div>
      <Toaster />
    </div>
  );
}
