"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, Users, Calendar, GitBranch, Activity,
  Stethoscope, Scissors, ArrowLeftRight, Wallet,
  Route,
  BarChart2, Package, FlaskConical, Settings, LogOut,
  Pill, X, Menu, MessageCircle, MessageSquare, ClipboardList, Globe, Clock, FileText,
  UserRound, Building2, Monitor, UserCog,
  CreditCard, FileCheck, AlertTriangle, Truck, ShoppingCart, ChevronDown,
} from "lucide-react";
import Image from "next/image";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import { useRouter } from "next/navigation";
import { useState, useCallback, useEffect } from "react";
import { useUnreadCount } from "@/hooks/useMessaging";

/* ─── Brand colors ──────────────────────────────────────────────────────────── */
const BRAND_PRIMARY       = "#1a3a5c";
const BRAND_PRIMARY_LIGHT = "#244b73";
const BRAND_ORANGE        = "#f5922e";

/* ─── Types ─────────────────────────────────────────────────────────────────── */
type NavLeaf = {
  href: string;
  label: string;
  icon: React.ElementType;
  roles: string[];
};

type NavGroup = {
  kind: "group";
  label: string;
  icon: React.ElementType;
  roles: string[];
  children: NavLeaf[];
};

type NavItem = NavLeaf & { section?: string };
type NavEntry = (NavItem & { kind?: "leaf" }) | (NavGroup & { section?: string });

/* ─── Navigation definition ─────────────────────────────────────────────────── */
const NAV: NavEntry[] = [
  // ── رئيسي ────────────────────────────────────────────────────────────────
  { href: "/",               label: "لوحة التحكم",     icon: LayoutDashboard, roles: [],                                                             section: "رئيسي" },
  { href: "/patients",       label: "المرضى",           icon: Users,           roles: [] },
  { href: "/appointments",   label: "المواعيد",         icon: Calendar,        roles: [] },
  { href: "/patient-journey",label: "رحلة المرضى",      icon: Route,           roles: ["Admin","Reception","GeneralDentist","OralSurgeon","Orthodontist"] },

  // ── العيادة ───────────────────────────────────────────────────────────────
  { href: "/clinic-queue",   label: "طابور العيادة",   icon: ClipboardList,   roles: [],                                                             section: "العيادة" },
  { href: "/clinic-display", label: "شاشة العرض",      icon: Monitor,         roles: [] },
  { href: "/schedule",       label: "جداول الأطباء",   icon: Clock,           roles: ["Admin","Reception"] },

  // ── تخصصات ───────────────────────────────────────────────────────────────
  { href: "/ortho",          label: "التقويم",          icon: GitBranch,       roles: ["Admin","Orthodontist"],                                       section: "تخصصات" },
  { href: "/ceph",           label: "السيفالومتري",     icon: Activity,        roles: ["Admin","Orthodontist"] },
  { href: "/general",        label: "طب الأسنان العام", icon: Stethoscope,     roles: ["Admin","GeneralDentist"] },
  { href: "/surgery",        label: "الجراحة",          icon: Scissors,        roles: ["Admin","OralSurgeon"] },

  // ── التواصل ───────────────────────────────────────────────────────────────
  { href: "/referrals",      label: "الإحالات",         icon: ArrowLeftRight,  roles: [],                                                             section: "التواصل" },
  { href: "/booking-requests",label:"طلبات الحجز",      icon: Globe,           roles: ["Admin","Reception"] },
  { href: "/messages",       label: "الرسائل",          icon: MessageCircle,   roles: [] },
  { href: "/whatsapp",       label: "واتساب",           icon: MessageSquare,   roles: [] },

  // ── عمليات ───────────────────────────────────────────────────────────────
  {
    kind: "group", section: "عمليات",
    label: "المالية", icon: Wallet,
    roles: ["Admin","Reception","Accountant"],
    children: [
      { href: "/finance",           label: "ملخص المالية", icon: Wallet,         roles: ["Admin","Reception","Accountant"] },
      { href: "/finance/invoices",  label: "الفواتير",     icon: FileText,       roles: ["Admin","Reception","Accountant"] },
      { href: "/finance/payments",  label: "المدفوعات",    icon: CreditCard,     roles: ["Admin","Reception","Accountant"] },
      { href: "/finance/contracts", label: "العقود",       icon: FileCheck,      roles: ["Admin","Reception","Accountant"] },
      { href: "/finance/overdue",   label: "المتأخرات",    icon: AlertTriangle,  roles: ["Admin","Reception","Accountant"] },
    ],
  },
  {
    kind: "group",
    label: "المخزون", icon: Package,
    roles: ["Admin"],
    children: [
      { href: "/inventory",           label: "المخزون",      icon: Package,      roles: ["Admin"] },
      { href: "/inventory/suppliers", label: "الموردون",     icon: Truck,        roles: ["Admin"] },
      { href: "/inventory/purchases", label: "أوامر الشراء", icon: ShoppingCart, roles: ["Admin"] },
    ],
  },
  { href: "/prescriptions",  label: "الوصفات الطبية",  icon: Pill,            roles: ["Admin","GeneralDentist","OralSurgeon","Orthodontist"] },
  { href: "/lab",            label: "المختبر",          icon: FlaskConical,    roles: ["Admin","Orthodontist"] },

  // ── تقارير ───────────────────────────────────────────────────────────────
  { href: "/reports",        label: "التقارير",         icon: BarChart2,       roles: ["Admin","Accountant"],                                         section: "تقارير" },

  // ── الإدارة ───────────────────────────────────────────────────────────────
  { href: "/doctors",        label: "الأطباء",          icon: UserRound,       roles: ["Admin"],                                                      section: "الإدارة" },
  { href: "/employees",      label: "الموظفين",         icon: UserCog,         roles: ["Admin"] },
  { href: "/branches",       label: "الفروع",           icon: Building2,       roles: ["Admin"] },

  // ── النظام ───────────────────────────────────────────────────────────────
  { href: "/settings",       label: "الإعدادات",        icon: Settings,        roles: ["Admin"],                                                      section: "النظام" },
];

const ROLE_LABELS: Record<string, string> = {
  Admin:         "مدير النظام",
  Orthodontist:  "أخصائي تقويم",
  GeneralDentist:"طبيب أسنان",
  OralSurgeon:   "جراح وجه وفكين",
  Reception:     "استقبال",
  Accountant:    "محاسب",
  Assistant:     "مساعد",
  BranchManager: "مدير فرع",
};

/* ─── Section label ─────────────────────────────────────────────────────────── */
function SectionLabel({ label }: { label: string }) {
  return (
    <div
      className="px-[18px] pt-3.5 pb-1 text-[10px] font-bold uppercase tracking-wider"
      style={{ color: "rgba(255,255,255,0.35)" }}
    >
      {label}
    </div>
  );
}

/* ─── Leaf link ─────────────────────────────────────────────────────────────── */
function NavLink({
  href, label, icon: Icon, isCurrent, indent = false, unreadCount,
}: {
  href: string; label: string; icon: React.ElementType;
  isCurrent: boolean; indent?: boolean; unreadCount?: number;
}) {
  return (
    <Link
      href={href}
      className={cn(
        "flex items-center gap-2.5 py-2.5 text-sm font-medium transition-all relative",
        indent ? "pr-9 pl-[18px]" : "px-[18px]",
        isCurrent ? "text-white font-bold" : "hover:text-white",
      )}
      style={isCurrent ? {
        background: "rgba(245,146,46,0.18)",
        borderRight: `3px solid ${BRAND_ORANGE}`,
      } : {
        color: "rgba(255,255,255,0.65)",
        borderRight: "3px solid transparent",
      }}
      onMouseEnter={(e) => { if (!isCurrent) e.currentTarget.style.background = "rgba(255,255,255,0.05)"; }}
      onMouseLeave={(e) => { if (!isCurrent) e.currentTarget.style.background = "transparent"; }}
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
  );
}

/* ─── Collapsible group ─────────────────────────────────────────────────────── */
function NavGroupItem({
  group, userRole, pathname,
}: {
  group: NavGroup; userRole: string; pathname: string;
}) {
  const visibleChildren = group.children.filter(
    (c) => c.roles.length === 0 || c.roles.includes(userRole),
  );
  const isChildActive = visibleChildren.some((c) => pathname.startsWith(c.href));
  const [open, setOpen] = useState(isChildActive);

  if (visibleChildren.length === 0) return null;

  return (
    <div>
      {/* Group header button */}
      <button
        onClick={() => setOpen((o) => !o)}
        className="w-full flex items-center gap-2.5 px-[18px] py-2.5 text-sm font-medium transition-all"
        style={{
          color: isChildActive ? "#fff" : "rgba(255,255,255,0.65)",
          borderRight: "3px solid transparent",
        }}
        onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(255,255,255,0.05)"; }}
        onMouseLeave={(e) => { e.currentTarget.style.background = "transparent"; }}
      >
        <group.icon
          className="w-[18px] h-[18px] flex-shrink-0"
          style={{ color: isChildActive ? BRAND_ORANGE : "rgba(255,255,255,0.65)" }}
        />
        <span className="flex-1 text-right">{group.label}</span>
        <ChevronDown
          className="w-3.5 h-3.5 transition-transform duration-200 flex-shrink-0"
          style={{
            color: "rgba(255,255,255,0.4)",
            transform: open ? "rotate(180deg)" : "rotate(0deg)",
          }}
        />
      </button>

      {/* Children */}
      {open && (
        <div style={{ background: "rgba(0,0,0,0.15)" }}>
          {visibleChildren.map((child) => (
            <NavLink
              key={child.href}
              href={child.href}
              label={child.label}
              icon={child.icon}
              isCurrent={child.href === "/" ? pathname === "/" : pathname.startsWith(child.href)}
              indent
            />
          ))}
        </div>
      )}
    </div>
  );
}

/* ─── Sidebar ───────────────────────────────────────────────────────────────── */
export function Sidebar() {
  const pathname  = usePathname();
  const router    = useRouter();
  const { user, logout } = useAuthStore();
  const [mobileOpen, setMobileOpen] = useState(false);
  const { data: unreadData } = useUnreadCount();

  const userRole = user?.role ?? "";

  const handleLogout = useCallback(async () => {
    await logout();
    router.push("/login");
  }, [logout, router]);

  useEffect(() => { setMobileOpen(false); }, [pathname]);

  useEffect(() => {
    document.body.style.overflow = mobileOpen ? "hidden" : "";
    return () => { document.body.style.overflow = ""; };
  }, [mobileOpen]);

  return (
    <>
      {/* Mobile hamburger */}
      <button
        onClick={() => setMobileOpen(true)}
        className="lg:hidden fixed top-3.5 right-3 z-50 w-10 h-10 rounded-lg border flex items-center justify-center text-white hover:opacity-90"
        style={{ backgroundColor: BRAND_PRIMARY, borderColor: BRAND_PRIMARY_LIGHT }}
        aria-label="فتح القائمة"
      >
        <Menu className="w-5 h-5" />
      </button>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div
          className="lg:hidden fixed inset-0 bg-black/50 z-40 backdrop-blur-sm"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside
        className={cn(
          "w-64 flex flex-col h-full fixed top-0 right-0 z-40 transition-transform duration-300",
          "lg:translate-x-0",
          mobileOpen ? "translate-x-0" : "translate-x-full lg:translate-x-0",
        )}
        style={{ backgroundColor: BRAND_PRIMARY }}
      >
        {/* Logo */}
        <div className="border-b min-h-[72px] flex items-center px-4 py-4" style={{ borderColor: "rgba(255,255,255,0.08)" }}>
          <div className="flex items-center gap-2.5 flex-1">
            <div className="w-[38px] h-[38px] rounded-lg flex items-center justify-center flex-shrink-0 overflow-hidden" style={{ background: "#fff", padding: 2 }}>
              <Image src="/logo.png" alt="Aqlan Dental Pro" width={34} height={34} className="w-[34px] h-[34px] object-contain" />
            </div>
            <div className="min-w-0">
              <p className="font-extrabold text-white text-sm leading-tight">Aqlan Dental Pro</p>
              <p className="text-[11px] mt-0.5 truncate" style={{ color: "rgba(255,255,255,0.55)" }}>مركز د. عقلان الكامل</p>
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
          {NAV.map((entry, idx) => {
            // Group visibility check
            if (entry.kind === "group") {
              const visible = entry.roles.length === 0 || entry.roles.includes(userRole);
              if (!visible) return null;
              return (
                <div key={`group-${idx}`}>
                  {entry.section && <SectionLabel label={entry.section} />}
                  <NavGroupItem group={entry} userRole={userRole} pathname={pathname} />
                </div>
              );
            }

            // Leaf item
            const leaf = entry as NavItem;
            const visible = leaf.roles.length === 0 || leaf.roles.includes(userRole);
            if (!visible) return null;

            const isCurrent = leaf.href === "/" ? pathname === "/" : pathname.startsWith(leaf.href);
            const unreadCount = leaf.href === "/messages" ? unreadData?.totalUnread : undefined;

            return (
              <div key={leaf.href}>
                {leaf.section && <SectionLabel label={leaf.section} />}
                <NavLink
                  href={leaf.href}
                  label={leaf.label}
                  icon={leaf.icon}
                  isCurrent={isCurrent}
                  unreadCount={unreadCount}
                />
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
            <p className="text-[13px] font-bold text-white truncate">{user?.doctorName ?? user?.username}</p>
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
