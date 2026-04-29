import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/* ═══════════════════════════════════════════════════════════════
   Aqlan Design System Utilities
   ═══════════════════════════════════════════════════════════════ */

/**
 * Returns the badge CSS class for a given color key.
 * Maps to the `.badge-{color}` utility classes in globals.css.
 */
export function badgeColorClass(color: BadgeColor): string {
  const map: Record<BadgeColor, string> = {
    blue: "badge-blue",
    orange: "badge-orange",
    green: "badge-green",
    red: "badge-red",
    purple: "badge-purple",
    yellow: "badge-yellow",
    sky: "badge-sky",
    gray: "badge-gray",
  };
  return map[color];
}

export type BadgeColor = "blue" | "orange" | "green" | "red" | "purple" | "yellow" | "sky" | "gray";

/**
 * Returns a hex color with an alpha suffix for tinted backgrounds.
 * Example: hexToTint("#3d7ab5", 0.1) → "#3d7ab51a"
 */
export function hexToTint(hex: string, alpha: number): string {
  const a = Math.round(alpha * 255)
    .toString(16)
    .padStart(2, "0");
  return `${hex}${a}`;
}

/**
 * Returns a severity badge color based on a numeric value and thresholds.
 */
export function severityBadge(
  value: number,
  { low, mid }: { low: number; mid: number },
): BadgeColor {
  if (value <= low) return "green";
  if (value <= mid) return "yellow";
  return "red";
}

/**
 * Returns a status dot CSS class for the given color.
 */
export function dotColorClass(color: "green" | "yellow" | "red" | "blue" | "gray" | "purple"): string {
  return `dot-${color}`;
}

/**
 * Maps appointment status to badge color.
 */
export function appointmentStatusColor(status: string): BadgeColor {
  const map: Record<string, BadgeColor> = {
    Scheduled: "blue",
    Confirmed: "sky",
    Arrived: "purple",
    InProgress: "orange",
    Completed: "green",
    Cancelled: "red",
    NoShow: "yellow",
  };
  return map[status] ?? "gray";
}

/**
 * Maps payment status to badge color.
 */
export function paymentStatusColor(status: string): BadgeColor {
  const map: Record<string, BadgeColor> = {
    Paid: "green",
    Partial: "orange",
    Pending: "yellow",
    Overdue: "red",
    Cancelled: "gray",
  };
  return map[status] ?? "gray";
}

/* ═══════════════════════════════════════════════════════════════
   Formatters
   ═══════════════════════════════════════════════════════════════ */

export function formatYemeniRiyal(amount: number): string {
  return new Intl.NumberFormat("ar-YE", {
    style: "currency",
    currency: "YER",
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(amount);
}

export function formatArabicDate(dateStr: string): string {
  return new Intl.DateTimeFormat("ar-YE", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(new Date(dateStr));
}

export function formatTime(timeStr: string): string {
  const [h, m] = timeStr.split(":");
  const hour = parseInt(h);
  const period = hour >= 12 ? "م" : "ص";
  const h12 = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
  return `${h12}:${m} ${period}`;
}

export const APPOINTMENT_STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  InProgress: "جارٍ",
  Completed: "مكتمل",
  Cancelled: "ملغى",
  NoShow: "لم يحضر",
};

export const GENDER_LABELS: Record<string, string> = {
  Male: "ذكر",
  Female: "أنثى",
};
