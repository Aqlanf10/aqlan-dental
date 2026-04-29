"use client";
import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { usePatientAuthStore } from "@/stores/patientAuthStore";

const PUBLIC_PATHS = ["/portal/login", "/portal/portal/login"];

export default function PortalLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { isAuthenticated, profile } = usePatientAuthStore();

  useEffect(() => {
    if (!isAuthenticated && !PUBLIC_PATHS.includes(pathname)) {
      router.replace("/portal/portal/login");
    }
  }, [isAuthenticated, pathname, router]);

  if (!isAuthenticated && !PUBLIC_PATHS.includes(pathname)) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50" style={{ direction: "rtl" }}>
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-teal-500 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-gray-500">جارٍ التحويل...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50" style={{ direction: "rtl" }}>
      {children}
      {/* Bottom Navigation - only for authenticated pages */}
      {isAuthenticated && !PUBLIC_PATHS.includes(pathname) && profile && (
        <PortalNavBar pathname={pathname} />
      )}
    </div>
  );
}

function PortalNavBar({ pathname }: { pathname: string }) {
  const router = useRouter();

  const items = [
    { path: "/portal", icon: "🏠", label: "الرئيسية" },
    { path: "/portal/appointments", icon: "📅", label: "المواعيد" },
    { path: "/portal/treatments", icon: "🦷", label: "العلاجات" },
    { path: "/portal/finance", icon: "💰", label: "المالية" },
  ];

  return (
    <nav className="fixed bottom-0 inset-x-0 bg-white border-t border-gray-200 z-50 safe-area-bottom">
      <div className="flex items-center justify-around max-w-md mx-auto">
        {items.map((item) => {
          const isActive = pathname === item.path;
          return (
            <button
              key={item.path}
              onClick={() => router.push(item.path)}
              className={`flex flex-col items-center py-2 px-3 text-xs transition ${
                isActive ? "text-teal-700" : "text-gray-400"
              }`}
            >
              <span className="text-lg mb-0.5">{item.icon}</span>
              <span className="font-medium">{item.label}</span>
            </button>
          );
        })}
      </div>
    </nav>
  );
}
