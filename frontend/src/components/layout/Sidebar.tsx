"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, Users, Calendar, GitBranch, Activity,
  Stethoscope, Scissors, ArrowLeftRight, Wallet,
  BarChart2, Package, FlaskConical, Settings, LogOut,
  Pill, X, Menu, MessageCircle, MessageSquare,
} from "lucide-react";
import Image from "next/image";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import { useState, useCallback, useEffect } from "react";
import { useUnreadCount } from "@/hooks/useMessaging";

/* ─── Role-based navigation permissions ────────────────────────────────────── */
type NavItem = {
  href: string;
  label: string;
  icon: React.ElementType;
  roles: string[]; // empty = all roles
};

const NAV_ITEMS: NavItem[] = [
  { href: "/",             label: "لوحة التحكم",       icon: LayoutDashboard, roles: [] },
  { href: "/patients",     label: "المرضى",             icon: Users,           roles: [] },
  { href: "/appointments", label: "المواعيد",           icon: Calendar,        roles: [] },
  { href: "/ortho",        label: "التقويم",            icon: GitBranch,       roles: ["Admin", "Orthodontist"] },
  { href: "/ceph",         label: "السيفالومتري",       icon: Activity,        roles: ["Admin", "Orthodontist"] },
  { href: "/general",      label: "طب الأسنان العام",   icon: Stethoscope,     roles: ["Admin", "GeneralDentist"] },
  { href: "/surgery",      label: "الجراحة",            icon: Scissors,        roles: ["Admin", "OralSurgeon"] },
  { href: "/referrals",    label: "الإحالات",           icon: ArrowLeftRight,  roles: [] },
  { href: "/messages",     label: "الرسائل",            icon: MessageCircle,   roles: [] },
  { href: "/whatsapp",    label: "واتساب",             icon: MessageSquare,   roles: [] },
  { href: "/finance",      label: "المالية",            icon: Wallet,          roles: ["Admin", "Reception", "Accountant"] },
  { href: "/prescriptions", label: "الوصفات الطبية",    icon: Pill,            roles: ["Admin", "GeneralDentist", "OralSurgeon", "Orthodontist"] },
  { href: "/reports",      label: "التقارير",           icon: BarChart2,       roles: ["Admin", "Accountant"] },
  { href: "/inventory",    label: "المخزون",            icon: Package,         roles: ["Admin"] },
  { href: "/lab",          label: "المختبر",            icon: FlaskConical,    roles: ["Admin", "Orthodontist"] },
  { href: "/settings",     label: "الإعدادات",          icon: Settings,        roles: ["Admin"] },
];

const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير النظام",
  Orthodontist: "أخصائي تقويم",
  GeneralDentist: "طبيب أسنان",
  OralSurgeon: "جراح وجه وفكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

/* ─── Sidebar Component ──────────────────────────────────────────────────────── */
export function Sidebar() {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuthStore();
  const [mobileOpen, setMobileOpen] = useState(false);
  const { data: unreadData } = useUnreadCount();

  const userRole = user?.role ?? "";

  // Filter nav items by role
  const visibleItems = NAV_ITEMS.filter(
    (item) => item.roles.length === 0 || item.roles.includes(userRole)
  );

  const handleLogout = useCallback(async () => {
    await logout();
    router.push("/login");
  }, [logout, router]);

  // Close mobile sidebar on route change
  useEffect(() => {
    setMobileOpen(false);
  }, [pathname]);

  // Prevent body scroll when mobile sidebar is open
  useEffect(() => {
    if (mobileOpen) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => {
      document.body.style.overflow = "";
    };
  }, [mobileOpen]);

  return (
    <>
      {/* ── Mobile hamburger button ──────────────────────────────────── */}
      <button
        onClick={() => setMobileOpen(true)}
        className="lg:hidden fixed top-3.5 right-3 z-50 w-10 h-10 rounded-lg bg-clinic-navy border border-clinic-navy-700 shadow-sm flex items-center justify-center text-white hover:bg-clinic-navy-700"
        aria-label="فتح القائمة"
      >
        <Menu className="w-5 h-5" />
      </button>

      {/* ── Mobile overlay ───────────────────────────────────────────── */}
      {mobileOpen && (
        <div
          className="lg:hidden fixed inset-0 bg-black/50 z-40 backdrop-blur-sm"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* ── Sidebar (Dark Navy) ─────────────────────────────────────── */}
      <aside
        className={cn(
          "w-64 bg-clinic-navy flex flex-col h-full fixed top-0 right-0 z-40 transition-transform duration-300",
          // On mobile: translate off-screen when closed, on-screen when open
          "lg:translate-x-0",
          mobileOpen ? "translate-x-0" : "translate-x-full lg:translate-x-0"
        )}
      >
        {/* Logo */}
        <div className="p-4 border-b border-white/10">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0 overflow-hidden bg-white/10">
                <Image
                  src="/logo.svg"
                  alt="Aqlan Dental Pro"
                  width={36}
                  height={36}
                  className="w-9 h-9"
                />
              </div>
              <div className="min-w-0">
                <p className="font-bold text-white text-sm leading-tight truncate">
                  مركز د. عقلان الكامل
                </p>
                <p className="text-xs text-white/40 truncate">Aqlan Dental Pro</p>
              </div>
            </div>
            {/* Close button for mobile */}
            <button
              onClick={() => setMobileOpen(false)}
              className="lg:hidden w-8 h-8 rounded-lg hover:bg-white/10 flex items-center justify-center text-white/50"
              aria-label="إغلاق القائمة"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto py-3 px-2 space-y-0.5">
          {visibleItems.map(({ href, label, icon: Icon }) => {
            const isCurrent = href === "/"
              ? pathname === "/"
              : pathname.startsWith(href);

            // Show unread badge on messages link
            const unreadCount = href === "/messages" ? unreadData?.totalUnread : undefined;

            return (
              <Link
                key={href}
                href={href}
                className={cn(
                  "flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all",
                  isCurrent
                    ? "bg-clinic-blue text-white shadow-md shadow-blue-500/25"
                    : "text-white/60 hover:bg-white/10 hover:text-white"
                )}
              >
                <Icon className="w-4 h-4 flex-shrink-0" />
                <span className="flex-1">{label}</span>
                {unreadCount && unreadCount > 0 && (
                  <span className={cn(
                    "text-xs font-bold rounded-full min-w-[20px] h-5 flex items-center justify-center px-1.5",
                    isCurrent ? "bg-white/20 text-white" : "bg-clinic-orange text-white"
                  )}>
                    {unreadCount > 99 ? "99+" : unreadCount}
                  </span>
                )}
              </Link>
            );
          })}
        </nav>

        {/* User footer */}
        <div className="p-3 border-t border-white/10">
          <div className="flex items-center gap-3 mb-2">
            <div
              className="w-9 h-9 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0 ring-2 ring-white/20"
              style={{ backgroundColor: user?.doctorColor ?? "#2563EB" }}
            >
              {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold text-white truncate">
                {user?.doctorName ?? user?.username}
              </p>
              <p className="text-xs text-white/40 truncate">
                {ROLE_LABELS[user?.role ?? ""] ?? user?.role}
              </p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full flex items-center gap-2 px-3 py-2 text-sm text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
          >
            <LogOut className="w-4 h-4" />
            <span>تسجيل الخروج</span>
          </button>
        </div>
      </aside>
    </>
  );
}
