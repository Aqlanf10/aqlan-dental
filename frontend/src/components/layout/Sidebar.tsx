"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import {
  Home, Users, Calendar, Smile, BarChart3,
  Sparkles, ClipboardList, Cross, Phone,
  UserCheck, Banknote, Pill, ArrowRightLeft,
  FlaskConical, Package, BarChart2, Settings,
  ChevronRight, LogOut, X, Menu, MessageCircle,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import { useState, useCallback, useEffect } from "react";

/* ─── Types ──────────────────────────────────────────────────────────────────── */
type NavItem = {
  href: string;
  label: string;
  icon: React.ElementType;
  roles: string[];
  badge?: number;
};

type NavSection = {
  title: string;
  items: NavItem[];
};

/* ─── Navigation structure matching the design spec ──────────────────────────── */
const NAV_SECTIONS: NavSection[] = [
  {
    title: "رئيسي",
    items: [
      { href: "/", label: "لوحة التحكم", icon: Home, roles: [] },
      { href: "/patients", label: "المرضى", icon: Users, roles: [] },
      { href: "/appointments", label: "المواعيد", icon: Calendar, roles: [] },
    ],
  },
  {
    title: "تخصصات",
    items: [
      { href: "/ortho", label: "التقويم", icon: Smile, roles: ["Admin", "Orthodontist"] },
      { href: "/ceph", label: "السيفالومتري المتقدم", icon: BarChart3, roles: ["Admin", "Orthodontist"] },
      { href: "/vto", label: "VTO — محاكاة العلاج", icon: Sparkles, roles: ["Admin", "Orthodontist"] },
      { href: "/general", label: "طب الأسنان العام", icon: ClipboardList, roles: ["Admin", "GeneralDentist"] },
      { href: "/surgery", label: "جراحة الوجه والفكين", icon: Cross, roles: ["Admin", "OralSurgeon"] },
    ],
  },
  {
    title: "التواصل",
    items: [
      { href: "/messaging", label: "الرسائل", icon: MessageCircle, roles: [] },
      { href: "/sms", label: "تذكيرات SMS", icon: Phone, roles: [] },
      { href: "/recall", label: "نظام الاستدعاء", icon: UserCheck, roles: [] },
    ],
  },
  {
    title: "عمليات",
    items: [
      { href: "/finance", label: "المالية", icon: Banknote, roles: ["Admin", "Reception", "Accountant"] },
      { href: "/prescriptions", label: "الوصفات الطبية", icon: Pill, roles: ["Admin", "GeneralDentist", "OralSurgeon", "Orthodontist"] },
      { href: "/referrals", label: "الإحالات", icon: ArrowRightLeft, roles: [] },
      { href: "/lab", label: "طلبات المختبر", icon: FlaskConical, roles: ["Admin", "Orthodontist"] },
      { href: "/inventory", label: "المخزون", icon: Package, roles: ["Admin"] },
    ],
  },
  {
    title: "تقارير",
    items: [
      { href: "/reports", label: "التقارير والإحصائيات", icon: BarChart2, roles: ["Admin", "Accountant"] },
    ],
  },
  {
    title: "النظام",
    items: [
      { href: "/settings", label: "الإعدادات", icon: Settings, roles: ["Admin"] },
    ],
  },
];

/* Additional nav items that exist but aren't in the spec's main nav — keep them accessible */
const EXTRA_ITEMS: NavItem[] = [
  { href: "/finance/expenses", label: "المصروفات", icon: Banknote, roles: ["Admin", "Accountant"] },
  { href: "/audit", label: "سجل التدقيق", icon: ClipboardList, roles: ["Admin"] },
  { href: "/profile", label: "حسابي", icon: Users, roles: [] },
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

/* ─── Props ──────────────────────────────────────────────────────────────────── */
interface SidebarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
}

/* ─── Sidebar Component ──────────────────────────────────────────────────────── */
export function Sidebar({ collapsed, onToggleCollapse }: SidebarProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuthStore();
  const [mobileOpen, setMobileOpen] = useState(false);

  const userRole = user?.role ?? "";

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

  // Filter items by role
  const filterByRole = (items: NavItem[]) =>
    items.filter((item) => item.roles.length === 0 || item.roles.includes(userRole));

  // Determine active state — check against all known routes for best match,
  // then see if the candidate href is an ancestor of that match
  const isActive = (href: string) => {
    if (href === "/") return pathname === "/";
    const allItems = NAV_SECTIONS.flatMap((s) => s.items).concat(EXTRA_ITEMS);
    const bestMatch = allItems
      .filter((item) => item.href !== "/" && pathname.startsWith(item.href))
      .sort((a, b) => b.href.length - a.href.length)[0];
    if (!bestMatch) return false;
    // Exact match or the candidate is an ancestor of the best match
    return href === bestMatch.href || bestMatch.href.startsWith(href + "/");
  };

  const sidebarWidth = collapsed ? "w-16" : "w-[258px]";

  const sidebarContent = (
    <div
      className={cn(
        "h-full flex flex-col bg-[#0d2137] transition-[width] duration-[250ms] ease-in-out overflow-hidden",
        sidebarWidth
      )}
    >
      {/* ── Logo Area ─────────────────────────────────────────────── */}
      <div className="min-h-[72px] flex items-center gap-3 px-[18px] py-4 border-b border-white/[0.08]">
        <div className="w-[38px] h-[38px] rounded-lg bg-white p-[2px] flex-shrink-0 flex items-center justify-center overflow-hidden">
          <Image
            src="/logo.png"
            alt="Aqlan Dental Pro"
            width={34}
            height={34}
            className="rounded-md object-contain"
          />
        </div>
        {!collapsed && (
          <div className="min-w-0">
            <p className="text-[14px] font-extrabold text-white leading-tight truncate">
              Aqlan Dental Pro
            </p>
            <p className="text-[11px] text-white/45 leading-tight truncate">
              مركز د. عقلان الكامل
            </p>
          </div>
        )}
      </div>

      {/* ── Collapse button (absolute positioned on left edge) ───── */}
      <button
        onClick={onToggleCollapse}
        className="absolute top-[22px] left-0 w-6 h-6 bg-[#1a3a5c] border border-white/[0.15] rounded-r-md flex items-center justify-center text-white/60 hover:text-white hover:bg-[#234b6e] transition-colors z-10 hidden lg:flex"
        aria-label={collapsed ? "توسيع القائمة" : "تصغير القائمة"}
      >
        <ChevronRight
          className={cn(
            "w-3.5 h-3.5 transition-transform duration-250",
            collapsed ? "rotate-180" : ""
          )}
        />
      </button>

      {/* ── Navigation ────────────────────────────────────────────── */}
      <nav className="flex-1 overflow-y-auto py-1 custom-scrollbar-dark">
        {NAV_SECTIONS.map((section) => {
          const visibleItems = filterByRole(section.items);
          if (visibleItems.length === 0) return null;

          return (
            <div key={section.title}>
              {/* Section label */}
              {!collapsed && (
                <div className="px-[18px] pt-[14px] pb-1">
                  <span className="text-[10px] font-bold text-white/30 uppercase tracking-[1px]">
                    {section.title}
                  </span>
                </div>
              )}
              {collapsed && <div className="h-3" />}

              {/* Section items */}
              {visibleItems.map(({ href, label, icon: Icon, badge }) => {
                const active = isActive(href);
                return (
                  <Link
                    key={href}
                    href={href}
                    className={cn(
                      "flex items-center gap-[10px] px-[18px] py-[10px] text-[13px] transition-colors relative group",
                      active
                        ? "bg-[#3d7ab5]/35 text-white font-bold border-r-[3px] border-[#3d7ab5]"
                        : "text-white/60 font-medium hover:bg-white/[0.05] border-r-[3px] border-transparent"
                    )}
                    title={collapsed ? label : undefined}
                  >
                    <Icon className="w-[18px] h-[18px] flex-shrink-0" />
                    {!collapsed && (
                      <>
                        <span className="truncate">{label}</span>
                        {badge !== undefined && badge > 0 && (
                          <span className="ms-auto bg-[#ef4444] text-white rounded-full px-[6px] py-[1px] text-[10px] font-extrabold leading-none">
                            {badge}
                          </span>
                        )}
                      </>
                    )}
                    {/* Collapsed: show badge as dot */}
                    {collapsed && badge !== undefined && badge > 0 && (
                      <span className="absolute top-2 right-2 w-2 h-2 bg-[#ef4444] rounded-full" />
                    )}
                  </Link>
                );
              })}
            </div>
          );
        })}
      </nav>

      {/* ── User Area (bottom) ────────────────────────────────────── */}
      <div className="px-4 py-3 border-t border-white/[0.08]">
        <div className="flex items-center gap-3">
          <div
            className="w-[34px] h-[34px] rounded-full flex items-center justify-center text-white text-[13px] font-bold flex-shrink-0"
            style={{ backgroundColor: user?.doctorColor ?? "#3d7ab5" }}
          >
            {user?.doctorInitials ?? user?.username?.charAt(0).toUpperCase() ?? "م"}
          </div>
          {!collapsed && (
            <div className="min-w-0 flex-1">
              <p className="text-[13px] font-bold text-white truncate">
                {user?.doctorName ?? user?.username}
              </p>
              <p className="text-[11px] text-white/40 truncate">
                {ROLE_LABELS[user?.role ?? ""] ?? user?.role}
              </p>
            </div>
          )}
        </div>
        {!collapsed && (
          <button
            onClick={handleLogout}
            className="w-full mt-2 flex items-center gap-2 px-3 py-2 text-[12px] text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
          >
            <LogOut className="w-3.5 h-3.5" />
            <span>تسجيل الخروج</span>
          </button>
        )}
      </div>
    </div>
  );

  return (
    <>
      {/* ── Mobile hamburger button ──────────────────────────────────── */}
      <button
        onClick={() => setMobileOpen(true)}
        className="lg:hidden fixed top-3.5 right-3 z-50 w-10 h-10 rounded-lg bg-white border border-gray-200 shadow-sm flex items-center justify-center text-gray-600 hover:bg-gray-50"
        aria-label="فتح القائمة"
      >
        <Menu className="w-5 h-5" />
      </button>

      {/* ── Mobile overlay ───────────────────────────────────────────── */}
      {mobileOpen && (
        <div
          className="lg:hidden fixed inset-0 bg-black/40 z-40"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* ── Desktop Sidebar ──────────────────────────────────────────── */}
      <aside className="hidden lg:block fixed top-0 right-0 h-full z-30">
        {sidebarContent}
      </aside>

      {/* ── Mobile Sidebar ───────────────────────────────────────────── */}
      <aside
        className={cn(
          "lg:hidden fixed top-0 right-0 h-full z-50 transition-transform duration-300",
          mobileOpen ? "translate-x-0" : "translate-x-full"
        )}
      >
        {/* Close button for mobile */}
        <button
          onClick={() => setMobileOpen(false)}
          className="absolute top-5 left-3 z-10 w-7 h-7 rounded-lg bg-white/10 hover:bg-white/20 flex items-center justify-center text-white/60"
          aria-label="إغلاق القائمة"
        >
          <X className="w-4 h-4" />
        </button>
        {sidebarContent}
      </aside>
    </>
  );
}
