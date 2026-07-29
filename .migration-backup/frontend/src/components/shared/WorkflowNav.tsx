"use client";

import Link from "next/link";
import { useAuthStore } from "@/stores/authStore";
import { hasPermission } from "@/hooks/usePermissions";
import { financeV3CollectionsUrl, financeV3InvoicesUrl } from "@/lib/financeRoutes";
import type { UserDto } from "@/types/auth";
import {
  ArrowRight,
  Globe,
  Calendar,
  CalendarClock,
  ClipboardList,
  Monitor,
  Route,
  CreditCard,
  FileText,
  User,
} from "lucide-react";

/* ─── Types ────────────────────────────────────────────────────────────────── */
export interface WorkflowLink {
  label: string;
  href: string;
  icon: React.ElementType;
  permission?: string;
  variant?: "default" | "primary" | "accent";
}

export interface WorkflowNavProps {
  /** Links to display, filtered by permission automatically */
  links: WorkflowLink[];
  /** Current page identifier for styling active link */
  currentPage?: string;
}

/* ─── Constants ────────────────────────────────────────────────────────────── */
const VARIANT_STYLES: Record<string, { bg: string; color: string; border: string; hoverBg: string }> = {
  default: { bg: "#f1f5f9", color: "#475569", border: "#e2e8f0", hoverBg: "#e2e8f0" },
  primary: { bg: "#3d7ab514", color: "#3d7ab5", border: "#3d7ab530", hoverBg: "#3d7ab520" },
  accent:  { bg: "#f5922e14", color: "#f5922e", border: "#f5922e30", hoverBg: "#f5922e20" },
};

/* ─── Reusable page-specific link presets ──────────────────────────────────── */
export const WORKFLOW_LINKS = {
  /** الرجوع إلى التشغيل اليومي */
  backToDailyOps: (permission = "daily_operations.view"): WorkflowLink => ({
    label: "التشغيل اليومي",
    href: "/daily-operations",
    icon: ArrowRight,
    permission,
    variant: "default",
  }),

  /** مراجعة طلبات الحجز */
  bookingRequests: (permission = "booking_requests.view"): WorkflowLink => ({
    label: "طلبات الحجز",
    href: "/booking-requests",
    icon: Globe,
    permission,
    variant: "default",
  }),

  /** المواعيد */
  appointments: (permission = "appointments.view"): WorkflowLink => ({
    label: "المواعيد",
    href: "/appointments",
    icon: Calendar,
    permission,
    variant: "default",
  }),

  /** مواعيد اليوم (same route, could add ?filter=today later) */
  todayAppointments: (permission = "appointments.view"): WorkflowLink => ({
    label: "مواعيد اليوم",
    href: "/appointments",
    icon: Calendar,
    permission,
    variant: "primary",
  }),

  /** حجز موعد جديد */
  newAppointment: (permission = "appointments.create"): WorkflowLink => ({
    label: "موعد جديد",
    href: "/appointments/new",
    icon: CalendarClock,
    permission,
    variant: "primary",
  }),

  /** قائمة الانتظار */
  // NAV-CEPH-FIX (audit §7 checklist): /clinic-queue is now a redirect stub → point directly
  // to the canonical workspace to avoid an extra redirect hop.
  clinicQueue: (permission = "clinic_queue.view"): WorkflowLink => ({
    label: "الانتظار",
    href: "/daily-operations?tab=queue",
    icon: ClipboardList,
    permission,
    variant: "default",
  }),

  /** شاشة النداء */
  clinicDisplay: (permission = "clinic_display.view"): WorkflowLink => ({
    label: "شاشة النداء",
    href: "/clinic-display",
    icon: Monitor,
    permission,
    variant: "default",
  }),

  /** رحلة المرضى */
  // NAV-CEPH-FIX (audit §7 checklist): /patient-journey index is now a redirect stub → point
  // directly to the canonical workspace to avoid an extra redirect hop.
  patientJourney: (permission = "patient_journey.view"): WorkflowLink => ({
    label: "رحلة المرضى",
    href: "/daily-operations",
    icon: Route,
    permission,
    variant: "default",
  }),

  /** المدفوعات / جاهز للدفع */
  payments: (permission = "finance.view"): WorkflowLink => ({
    label: "المدفوعات",
    href: financeV3CollectionsUrl(),
    icon: CreditCard,
    permission,
    variant: "default",
  }),

  /** الفواتير */
  invoices: (permission = "invoices.view"): WorkflowLink => ({
    label: "الفواتير",
    href: financeV3InvoicesUrl(),
    icon: FileText,
    permission,
    variant: "default",
  }),

  /** جاهز للدفع */
  // NAV-CEPH-FIX (audit §7 checklist): /patient-journey index is now a redirect stub → point
  // directly to the canonical workspace (checkout happens inside /daily-operations).
  checkout: (permission = "checkout.view"): WorkflowLink => ({
    label: "جاهز للدفع",
    href: "/daily-operations",
    icon: CreditCard,
    permission,
    variant: "accent",
  }),

  /** فتح ملف المريض (requires patientId context) */
  patientFile: (patientId: string, permission = "patients.view"): WorkflowLink => ({
    label: "ملف المريض",
    href: `/patients/${patientId}`,
    icon: User,
    permission,
    variant: "accent",
  }),
} as const;

/* ─── Component ────────────────────────────────────────────────────────────── */
export function WorkflowNav({ links, currentPage }: WorkflowNavProps) {
  const { user } = useAuthStore();

  const visibleLinks = links.filter((link) => {
    // If no permission required, show the link
    if (!link.permission) return true;
    return hasPermission(user as UserDto | null, link.permission);
  });

  if (visibleLinks.length === 0) return null;

  return (
    <div
      className="flex items-center gap-2 flex-wrap"
      role="navigation"
      aria-label="تنقل سير العمل"
    >
      {visibleLinks.map((link) => {
        const Icon = link.icon;
        const isCurrentPage = currentPage && link.href === currentPage;
        const variant = link.variant ?? "default";
        const style = VARIANT_STYLES[variant] ?? VARIANT_STYLES.default;

        return (
          <Link
            key={link.href + link.label}
            href={link.href}
            className="group flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all no-underline"
            style={{
              background: isCurrentPage ? style.hoverBg : style.bg,
              color: isCurrentPage ? style.color : style.color,
              border: `1px solid ${isCurrentPage ? style.border : style.border}`,
              opacity: isCurrentPage ? 1 : 0.85,
            }}
            onMouseEnter={(e) => {
              if (!isCurrentPage) {
                e.currentTarget.style.background = style.hoverBg;
                e.currentTarget.style.opacity = "1";
              }
            }}
            onMouseLeave={(e) => {
              if (!isCurrentPage) {
                e.currentTarget.style.background = style.bg;
                e.currentTarget.style.opacity = "0.85";
              }
            }}
            aria-current={isCurrentPage ? "page" : undefined}
          >
            <Icon className="w-3.5 h-3.5 flex-shrink-0" />
            {link.label}
          </Link>
        );
      })}
    </div>
  );
}

export default WorkflowNav;
