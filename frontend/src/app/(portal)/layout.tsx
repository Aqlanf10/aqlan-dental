"use client";
import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import { Home, Calendar, Stethoscope, Pill, CreditCard, UserCircle, MessageCircle } from "lucide-react";
import { cn } from "@/lib/utils";

const PUBLIC_PATHS = ["/portal/login"];

export default function PortalLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { isAuthenticated, profile } = usePatientAuthStore();

  useEffect(() => {
    if (!isAuthenticated && !PUBLIC_PATHS.includes(pathname)) {
      router.replace("/portal/login");
    }
  }, [isAuthenticated, pathname, router]);

  if (!isAuthenticated && !PUBLIC_PATHS.includes(pathname)) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50" style={{ direction: "rtl" }}>
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-clinic-blue border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-gray-500">جارٍ التحويل...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50" style={{ direction: "rtl" }}>
      {children}
      {isAuthenticated && !PUBLIC_PATHS.includes(pathname) && profile && (
        <PortalNavBar pathname={pathname} />
      )}
    </div>
  );
}

function PortalNavBar({ pathname }: { pathname: string }) {
  const router = useRouter();

  const items = [
    { path: "/portal", icon: Home, label: "الرئيسية" },
    { path: "/portal/appointments", icon: Calendar, label: "المواعيد" },
    { path: "/portal/messages", icon: MessageCircle, label: "الرسائل" },
    { path: "/portal/treatments", icon: Stethoscope, label: "العلاجات" },
    { path: "/portal/prescriptions", icon: Pill, label: "الوصفات" },
    { path: "/portal/finance", icon: CreditCard, label: "المالية" },
    { path: "/portal/profile", icon: UserCircle, label: "بياناتي" },
  ];

  return (
    <nav className="fixed bottom-0 inset-x-0 bg-white border-t border-gray-200 z-50 safe-area-bottom">
      <div className="flex items-center justify-around max-w-lg mx-auto">
        {items.map((item) => {
          const isActive = pathname === item.path;
          const Icon = item.icon;
          return (
            <button
              key={item.path}
              onClick={() => router.push(item.path)}
              className={cn(
                "flex flex-col items-center py-2 px-2 text-[10px] transition",
                isActive ? "text-clinic-blue" : "text-gray-400"
              )}
            >
              <Icon className={cn("w-5 h-5 mb-0.5", isActive ? "stroke-[2.5px]" : "stroke-[1.5px]")} />
              <span className="font-medium">{item.label}</span>
            </button>
          );
        })}
      </div>
    </nav>
  );
}
