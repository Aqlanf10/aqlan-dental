"use client";
import { useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import { usePortalUnreadCount } from "@/hooks/usePortalMessaging";
import { Home, Calendar, Stethoscope, Pill, CreditCard, UserCircle, MessageCircle } from "lucide-react";
import { cn } from "@/lib/utils";

const PUBLIC_PATHS = ["/portal/login"];
const CHANGE_PASSWORD_PATH = "/portal/change-password";

export default function PortalLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { isAuthenticated, mustChangePassword, _hasRehydrated } = usePatientAuthStore();

  useEffect(() => {
    // Do NOT make any redirect decisions until Zustand persist has
    // rehydrated from localStorage. Without this guard the layout
    // sees the default state (isAuthenticated=false) on first render
    // and immediately redirects to /portal/login, even though the
    // persisted state says the user IS authenticated — causing an
    // infinite redirect loop between login and change-password.
    if (!_hasRehydrated) return;

    if (!isAuthenticated && !PUBLIC_PATHS.includes(pathname)) {
      router.replace("/portal/login");
    }
    // If mustChangePassword, force redirect to change-password page
    if (isAuthenticated && mustChangePassword && pathname !== CHANGE_PASSWORD_PATH) {
      router.replace(CHANGE_PASSWORD_PATH);
    }
  }, [_hasRehydrated, isAuthenticated, mustChangePassword, pathname, router]);

  // While Zustand persist is rehydrating, show a loading spinner
  // instead of making premature redirect decisions.
  if (!_hasRehydrated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50" style={{ direction: "rtl" }}>
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-clinic-blue border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-sm text-gray-500">جارٍ التحميل...</p>
        </div>
      </div>
    );
  }

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

  // When mustChangePassword is true, only show the change-password page
  const isLockedToChangePassword = isAuthenticated && mustChangePassword;

  return (
    <div className="min-h-screen bg-gray-50" style={{ direction: "rtl" }}>
      {children}
      {isAuthenticated && !PUBLIC_PATHS.includes(pathname) && !isLockedToChangePassword && (
        <PortalNavBar pathname={pathname} />
      )}
    </div>
  );
}

function PortalNavBar({ pathname }: { pathname: string }) {
  const router = useRouter();
  const { data: unreadData } = usePortalUnreadCount();
  const totalUnread = unreadData?.totalUnread ?? 0;

  const items = [
    { path: "/portal",              icon: Home,          label: "الرئيسية" },
    { path: "/portal/appointments", icon: Calendar,      label: "المواعيد" },
    { path: "/portal/messages",     icon: MessageCircle, label: "الرسائل",  badge: totalUnread },
    { path: "/portal/treatments",   icon: Stethoscope,   label: "العلاجات" },
    { path: "/portal/prescriptions",icon: Pill,          label: "الوصفات" },
    { path: "/portal/finance",      icon: CreditCard,    label: "المالية" },
    { path: "/portal/profile",      icon: UserCircle,    label: "بياناتي" },
  ];

  return (
    <nav className="fixed bottom-0 inset-x-0 bg-white border-t border-gray-200 z-50 safe-area-bottom">
      <div className="flex items-center justify-around max-w-lg mx-auto">
        {items.map((item) => {
          const isActive = pathname === item.path;
          const Icon = item.icon;
          const hasBadge = (item.badge ?? 0) > 0;
          return (
            <button
              key={item.path}
              onClick={() => router.push(item.path)}
              className={cn(
                "flex flex-col items-center py-2 px-2 text-[10px] transition relative",
                isActive ? "text-clinic-blue" : "text-gray-400"
              )}
            >
              <div className="relative">
                <Icon className={cn("w-5 h-5 mb-0.5", isActive ? "stroke-[2.5px]" : "stroke-[1.5px]")} />
                {hasBadge && (
                  <span className="absolute -top-1 -left-1 min-w-[14px] h-3.5 bg-red-500 text-white text-[8px] font-bold rounded-full flex items-center justify-center px-0.5 leading-none">
                    {(item.badge ?? 0) > 9 ? "9+" : item.badge}
                  </span>
                )}
              </div>
              <span className="font-medium">{item.label}</span>
            </button>
          );
        })}
      </div>
    </nav>
  );
}
