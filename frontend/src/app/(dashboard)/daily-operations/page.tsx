"use client";

import { useEffect, useState, useMemo } from "react";
import Link from "next/link";
import {
  Calendar, ClipboardList, Monitor, Route, CreditCard, FileText,
  Globe, Settings, Plus, ArrowLeft, ListOrdered,
  CalendarClock, UserCheck,
} from "lucide-react";
import type { DashboardStats } from "@/types/dashboard";
import { useAuthStore } from "@/stores/authStore";
import api from "@/lib/api";

/* ─── Brand constants ───────────────────────────────────────────────────────── */
const NAVY = "#1a3a5c";

/* ─── Card styles ───────────────────────────────────────────────────────────── */
const sectionCardStyle: React.CSSProperties = {
  background: "#fff",
  borderRadius: 12,
  padding: 20,
  boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
  border: "1px solid #e8f0f9",
};

/* ─── Role-based visibility ─────────────────────────────────────────────────── */
/**
 * Mirrors the Sidebar's role-filtering logic.
 * Empty array = visible to ALL roles.
 * Non-empty array = only those roles can see the link.
 */
interface QuickLink {
  label: string;
  description: string;
  href: string;
  icon: React.ElementType;
  color: string;
  roles: string[];
  countKey?: keyof Pick<DashboardStats, "appointmentsToday" | "queueWaitingCount" | "pendingBookingRequestsCount" | "todayArrivedCount">;
}

interface Section {
  title: string;
  links: QuickLink[];
}

const SECTIONS: Section[] = [
  {
    title: "الحجز والمواعيد",
    links: [
      {
        label: "طلبات الحجز",
        description: "إدارة طلبات الحجز الواردة من الموقع",
        href: "/booking-requests",
        icon: Globe,
        color: "#3d7ab5",
        roles: ["Admin", "Reception"],
        countKey: "pendingBookingRequestsCount",
      },
      {
        label: "المواعيد",
        description: "عرض وإدارة مواعيد اليوم واليوم التالي",
        href: "/appointments",
        icon: Calendar,
        color: "#f5922e",
        roles: [], // all roles
        countKey: "appointmentsToday",
      },
      {
        label: "موعد جديد",
        description: "إنشاء موعد جديد لمريض",
        href: "/appointments/new",
        icon: CalendarClock,
        color: "#a855f7",
        roles: ["Admin", "Reception", "Orthodontist", "GeneralDentist", "OralSurgeon"],
      },
    ],
  },
  {
    title: "الطابور والعرض",
    links: [
      {
        label: "طابور العيادة",
        description: "إدارة طابور المرضى والنداء",
        href: "/clinic-queue",
        icon: ClipboardList,
        color: "#f5922e",
        roles: ["Admin", "Reception"],
        countKey: "queueWaitingCount",
      },
      {
        label: "شاشة النداء",
        description: "شاشة عرض الطابور للمرضى في الصالة",
        href: "/clinic-display",
        icon: Monitor,
        color: "#3d7ab5",
        roles: [], // all roles
      },
      {
        label: "رحلة المرضى",
        description: "تتبع المرضى داخل العيادة خطوة بخطوة",
        href: "/patient-journey",
        icon: Route,
        color: "#22c55e",
        roles: ["Admin", "Reception", "GeneralDentist", "OralSurgeon", "Orthodontist"],
        countKey: "todayArrivedCount",
      },
    ],
  },
  {
    title: "المالية اليومية",
    links: [
      {
        label: "المدفوعات",
        description: "تسجيل ومتابعة المدفوعات اليومية",
        href: "/finance/payments",
        icon: CreditCard,
        color: "#22c55e",
        roles: ["Admin", "Reception", "Accountant"],
      },
      {
        label: "الفواتير",
        description: "إنشاء وعرض الفواتير",
        href: "/finance/invoices",
        icon: FileText,
        color: "#3d7ab5",
        roles: ["Admin", "Reception", "Accountant"],
      },
    ],
  },
  {
    title: "الإعداد السريع",
    links: [
      {
        label: "الغرف والكراسي",
        description: "إدارة غرف العيادة وتوزيع الكراسي",
        href: "/settings/rooms",
        icon: Settings,
        color: "#64748b",
        roles: ["Admin"],
      },
      {
        label: "مريض جديد",
        description: "تسجيل مريض جديد في النظام",
        href: "/patients/new",
        icon: Plus,
        color: "#3d7ab5",
        roles: ["Admin", "Reception"],
      },
    ],
  },
];

/* ─── Summary strip stat — which roles can see each stat ───────────────────── */
const SUMMARY_STATS: {
  icon: React.ElementType;
  label: string;
  color: string;
  countKey: keyof Pick<DashboardStats, "appointmentsToday" | "queueWaitingCount" | "pendingBookingRequestsCount" | "todayArrivedCount">;
  roles: string[];
}[] = [
  {
    icon: Globe,
    label: "طلبات حجز معلقة",
    color: "#3d7ab5",
    countKey: "pendingBookingRequestsCount",
    roles: ["Admin", "Reception"],
  },
  {
    icon: Calendar,
    label: "مواعيد اليوم",
    color: "#f5922e",
    countKey: "appointmentsToday",
    roles: [], // all roles
  },
  {
    icon: ListOrdered,
    label: "عدد المنتظرين",
    color: "#ef4444",
    countKey: "queueWaitingCount",
    roles: ["Admin", "Reception"],
  },
  {
    icon: UserCheck,
    label: "عدد الواصلين",
    color: "#22c55e",
    countKey: "todayArrivedCount",
    roles: ["Admin", "Reception", "GeneralDentist", "OralSurgeon", "Orthodontist"],
  },
];

/**
 * Same visibility logic as Sidebar:
 * empty roles array = visible to ALL; otherwise must be included.
 */
function isVisible(roles: string[], userRole: string): boolean {
  return roles.length === 0 || roles.includes(userRole);
}

/* ─── Safe count helper ─────────────────────────────────────────────────────── */
/** Returns the number only when explicitly provided by backend; "—" otherwise. */
function safeCount(value: number | null | undefined, loading: boolean): number | string {
  if (loading) return "—";
  if (value == null) return "—";
  return value;
}

/* ─── Page Component ────────────────────────────────────────────────────────── */
export default function DailyOperationsPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const { user } = useAuthStore();
  const userRole = user?.role ?? "";

  useEffect(() => {
    api
      .get<DashboardStats>("/api/dashboard/stats")
      .then((r) => setStats(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  /** Filter sections and their links based on user role */
  const visibleSections = useMemo<Section[]>(() => {
    return SECTIONS
      .map((section) => ({
        ...section,
        links: section.links.filter((link) => isVisible(link.roles, userRole)),
      }))
      .filter((section) => section.links.length > 0);
  }, [userRole]);

  /** Filter summary stats based on user role */
  const visibleStats = useMemo(() => {
    return SUMMARY_STATS.filter((s) => isVisible(s.roles, userRole));
  }, [userRole]);

  return (
    <div className="space-y-5 page-content">
      {/* ── Header ──────────────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: NAVY }}>
            التشغيل اليومي
          </h1>
          <p className="text-sm mt-1" style={{ color: "#64748b" }}>
            إدارة طلبات الحجز والمواعيد والطابور والزيارات والدفع اليومي من مكان واحد
          </p>
        </div>
        <Link
          href="/"
          className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-sm font-semibold transition"
          style={{ background: NAVY + "0d", color: NAVY, border: `1px solid ${NAVY}20` }}
          onMouseEnter={(e) => (e.currentTarget.style.background = NAVY + "1a")}
          onMouseLeave={(e) => (e.currentTarget.style.background = NAVY + "0d")}
        >
          <ArrowLeft className="w-4 h-4" />
          لوحة التحكم
        </Link>
      </div>

      {/* ── Summary strip ───────────────────────────────────────────────────── */}
      {visibleStats.length > 0 && (
        <div className={`grid gap-3 grid-cols-2 sm:grid-cols-${Math.min(visibleStats.length, 4)}`}>
          {visibleStats.map((stat) => (
            <MiniStat
              key={stat.countKey}
              icon={stat.icon}
              label={stat.label}
              value={safeCount(stats?.[stat.countKey] ?? null, loading)}
              color={stat.color}
            />
          ))}
        </div>
      )}

      {/* ── Section cards with quick links ──────────────────────────────────── */}
      {visibleSections.map((section) => (
        <div key={section.title} style={sectionCardStyle}>
          <h2 className="font-extrabold text-[15px] mb-4" style={{ color: NAVY }}>
            {section.title}
          </h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {section.links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="group flex items-start gap-3 p-3.5 rounded-xl transition"
                style={{
                  background: link.color + "08",
                  border: `1.5px solid ${link.color}18`,
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.background = link.color + "14";
                  e.currentTarget.style.borderColor = link.color + "30";
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.background = link.color + "08";
                  e.currentTarget.style.borderColor = link.color + "18";
                }}
              >
                <div
                  className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                  style={{ background: link.color + "18" }}
                >
                  <link.icon className="w-5 h-5" style={{ color: link.color }} />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-bold" style={{ color: link.color }}>
                      {link.label}
                    </span>
                    {link.countKey && (
                      <CountBadge
                        loading={loading}
                        value={stats?.[link.countKey] ?? null}
                      />
                    )}
                  </div>
                  <p className="text-xs mt-0.5" style={{ color: "#94a3b8" }}>
                    {link.description}
                  </p>
                </div>
              </Link>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

/* ─── Mini Stat Card ────────────────────────────────────────────────────────── */
function MiniStat({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: number | string;
  color: string;
}) {
  return (
    <div
      className="rounded-xl p-3.5 flex items-center gap-3"
      style={{ background: color + "0a", border: `1.5px solid ${color}20` }}
    >
      <div
        className="w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0"
        style={{ background: color + "18" }}
      >
        <Icon className="w-4.5 h-4.5" style={{ color }} />
      </div>
      <div>
        <div className="text-lg font-extrabold leading-tight" style={{ color }}>
          {value}
        </div>
        <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>
          {label}
        </div>
      </div>
    </div>
  );
}

/* ─── Count Badge ───────────────────────────────────────────────────────────── */
function CountBadge({
  loading,
  value,
}: {
  loading: boolean;
  value: number | null | undefined;
}) {
  if (loading) {
    return (
      <span
        className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full"
        style={{ background: "#e2e8f0", color: "#94a3b8" }}
      >
        ...
      </span>
    );
  }

  if (value == null) {
    // Graceful unavailable state — do not fake numbers
    return (
      <span
        className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full"
        style={{ background: "#f1f5f9", color: "#94a3b8" }}
      >
        —
      </span>
    );
  }

  if (value === 0) {
    return null; // Don't show badge when count is zero
  }

  return (
    <span
      className="text-[10px] font-extrabold px-1.5 py-0.5 rounded-full"
      style={{ background: "#ef444418", color: "#ef4444" }}
    >
      {value}
    </span>
  );
}
