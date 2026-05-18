"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, Users, Calendar, GitBranch, Activity,
  Stethoscope, Scissors, ArrowLeftRight, Wallet,
  Route,
  BarChart2, Package, FlaskConical, Settings, LogOut,
  Pill, X, Menu, MessageCircle, MessageSquare, ClipboardList, Globe, Clock,
  UserRound, Building2, Monitor, UserCog,
} from "lucide-react";
import Image from "next/image";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import { useState, useCallback, useEffect } from "react";
import { useUnreadCount } from "@/hooks/useMessaging";

/* ─── Brand colors (Aqlan Dental Pro) ──────────────────────────────────────── */
const BRAND_PRIMARY = "#1a3a5c";  // dark sky — sidebar background
const BRAND_PRIMARY_LIGHT = "#244b73";  // for hover/border
const BRAND_ORANGE = "#f5922e";   // active link, CTA

/* ─── Role-based navigation permissions ────────────────────────────────────── */
type NavItem = {
  href: string;
  label: string;
  icon: React.ElementType;
  roles: string[];
  section?: string;
};

const NAV_ITEMS: NavItem[] = [
  // Section: رئيسي
  { href: "/",             label: "لوحة التحكم",       icon: LayoutDashboard, roles: [], section: "رئيسي" },
  { href: "/patients",     label: "المرضى",             icon: Users,           roles: [] },
  { href: "/appointments", label: "المواعيد",           icon: Calendar,        roles: [] },
  { href: "/patient-journey", label: "رحلة المرضى",     icon: Route,           roles: ["Admin", "Reception", "GeneralDentist", "OralSurgeon", "Orthodontist"] },
  { href: "/clinic-queue", label: "طابور العيادة",     icon: ClipboardList,   roles: [] },
  { href: "/clinic-display", label: "شاشة العرض",       icon: Monitor,         roles: [] },
  { href: "/doctors",      label: "الأطباء",           icon: UserRound,       roles: ["Admin"] },
  { href: "/employees",    label: "الموظفين",          icon: UserCog,         roles: ["Admin"] },
  { href: "/branches",     label: "الفروع",            icon: Building2,       roles: ["Admin"] },
  { href: "/schedule",     label: "جداول الأطباء",     icon: Clock,           roles: ["Admin", "Reception"] },
  // Section: تخصصات
  { href: "/ortho",        label: "التقويم",            icon: GitBranch,       roles: ["Admin", "Orthodontist"], section: "تخصصات" },
  { href: "/ceph",         label: "السيفالومتري",       icon: Activity,        roles: ["Admin", "Orthodontist"] },
  { href: "/general",      label: "طب الأسنان العام",   icon: Stethoscope,     roles: ["Admin", "GeneralDentist"] },
  { href: "/surgery",      label: "الجراحة",            icon: Scissors,        roles: ["Admin", "OralSurgeon"] },
  // Section: التواصل
  { href: "/referrals",    label: "الإحالات",           icon: ArrowLeftRight,  roles: [], section: "التواصل" },
  { href: "/booking-requests", label: "طلبات الحجز",   icon: Globe,           roles: ["Admin", "Reception"] },
  { href: "/messages",     label: "الرسائل",            icon: MessageCircle,   roles: [] },
  { href: "/whatsapp",    label: "واتساب",             icon: MessageSquare,   roles: [] },
  // Section: عمليات
  { href: "/finance",      label: "المالية",            icon: Wallet,          roles: ["Admin", "Reception", "Accountant"], section: "عمليات" },
  { href: "/prescriptions", label: "الوصفات الطبية",    icon: Pill,            roles: ["Admin", "GeneralDentist", "OralSurgeon", "Orthodontist"] },
  { href: "/lab",          label: "المختبر",            icon: FlaskConical,    roles: ["Admin", "Orthodontist"] },
  { href: "/inventory",    label: "المخزون",            icon: Package,         roles: ["Admin"] },
  // Section: تقارير
  { href: "/reports",      label: "التقارير",           icon: BarChart2,       roles: ["Admin", "Accountant"], section: "تقارير" },
  // Section: النظام
  { href: "/settings",     label: "الإعدادات",          icon: Settings,        roles: ["Admin"], section: "النظام" },
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

  const visibleItems = NAV_ITEMS.filter(
    (item) => item.roles.length === 0 || item.roles.includes(userRole)
  );

  const handleLogout = useCallback(async () => {
    await logout();
    router.push("/login");
  }, [logout, router]);

  useEffect(() => {
    setMobileOpen(false);
  }, [pathname]);

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
        className="lg:hidden fixed top-3.5 right-3 z-50 w-10 h-10 rounded-lg border flex items-center justify-center text-white hover:opacity-90"
        style={{ backgroundColor: BRAND_PRIMARY, borderColor: BRAND_PRIMARY_LIGHT }}
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

      {/* ── Sidebar — Brand Dark Sky ─────────────────────────────────── */}
      <aside
        className={cn(
          "w-64 flex flex-col h-full fixed top-0 right-0 z-40 transition-transform duration-300",
          "lg:translate-x-0",
          mobileOpen ? "translate-x-0" : "translate-x-full lg:translate-x-0"
        )}
        style={{ backgroundColor: BRAND_PRIMARY }}
      >
        {/* Logo */}
        <div className="border-b min-h-[72px] flex items-center px-4 py-4" style={{ borderColor: "rgba(255,255,255,0.08)" }}>
          <div className="flex items-center gap-2.5 flex-1">
            <div className="w-[38px] h-[38px] rounded-lg flex items-center justify-center flex-shrink-0 overflow-hidden" style={{ background: "#fff", padding: 2 }}>
              <Image
                src="/logo.png"
                alt="Aqlan Dental Pro"
                width={34}
                height={34}
                className="w-[34px] h-[34px] object-contain"
              />
            </div>
            <div className="min-w-0">
              <p className="font-extrabold text-white text-sm leading-tight">
                Aqlan Dental Pro
              </p>
              <p className="text-[11px] mt-0.5 truncate" style={{ color: "rgba(255,255,255,0.55)" }}>
                مركز د. عقلان الكامل
              </p>
            </div>
          </div>
          <button
            onClick={() => setMobileOpen(false)}
            className="lg:hidden w-8 h-8 rounded-lg flex items-center justify-center"
            style={{ color: "rgba(255,255,255,0.5)" }}
            onMouseEnter={(e) => (e.currentTarget.style.background = "rgba(255,255,255,0.1)")}
            onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
            aria-label="إغلاق القائمة"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 overflow-y-auto py-2">
          {visibleItems.map(({ href, label, icon: Icon, section }) => {
            const isCurrent = href === "/"
              ? pathname === "/"
              : pathname.startsWith(href);

            const unreadCount = href === "/messages" ? unreadData?.totalUnread : undefined;

            return (
              <div key={href}>
                {section && (
                  <div
                    className="px-[18px] pt-3.5 pb-1 text-[10px] font-bold uppercase tracking-wider"
                    style={{ color: "rgba(255,255,255,0.35)" }}
                  >
                    {section}
                  </div>
                )}
                <Link
                  href={href}
                  className={cn(
                    "flex items-center gap-2.5 px-[18px] py-2.5 text-sm font-medium transition-all relative",
                    isCurrent
                      ? "text-white font-bold"
                      : "hover:text-white"
                  )}
                  style={isCurrent ? {
                    background: "rgba(245,146,46,0.18)",
                    borderRight: `3px solid ${BRAND_ORANGE}`,
                  } : {
                    color: "rgba(255,255,255,0.65)",
                    borderRight: "3px solid transparent",
                  }}
                  onMouseEnter={(e) => {
                    if (!isCurrent) e.currentTarget.style.background = "rgba(255,255,255,0.05)";
                  }}
                  onMouseLeave={(e) => {
                    if (!isCurrent) e.currentTarget.style.background = "transparent";
                  }}
                >
                  <Icon
                    className="w-[18px] h-[18px] flex-shrink-0"
                    style={{ color: isCurrent ? BRAND_ORANGE : "rgba(255,255,255,0.65)" }}
                  />
                  <span className="flex-1">{label}</span>
                  {unreadCount && unreadCount > 0 && (
                    <span
                      className="text-[10px] font-extrabold rounded-full min-w-[20px] h-5 flex items-center justify-center px-1.5"
                      style={{ background: "#ef4444", color: "#fff" }}
                    >
                      {unreadCount > 99 ? "99+" : unreadCount}
                    </span>
                  )}
                </Link>
              </div>
            );
          })}
        </nav>

        {/* User footer */}
        <div className="px-4 py-3 border-t flex items-center gap-2.5" style={{ borderColor: "rgba(255,255,255,0.08)" }}>
          <div
            className="w-[34px] h-[34px] rounded-full flex items-center justify-center text-white text-[13px] font-bold flex-shrink-0"
            style={{ backgroundColor: BRAND_ORANGE }}
          >
            {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-[13px] font-bold text-white truncate">
              {user?.doctorName ?? user?.username}
            </p>
            <p className="text-[11px] truncate" style={{ color: "rgba(255,255,255,0.5)" }}>
              {ROLE_LABELS[user?.role ?? ""] ?? user?.role ?? "موظف"}
            </p>
          </div>
          <button
            onClick={handleLogout}
            className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
            style={{ color: "rgba(255,255,255,0.5)" }}
            onMouseEnter={(e) => (e.currentTarget.style.background = "rgba(239,68,68,0.15)")}
            onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
            title="تسجيل الخروج"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </aside>
    </>
  );
}
