/**
 * FE-09: Single source of truth for appointment + queue status labels and colors.
 *
 * Previously, STATUS_COLORS and STATUS_LABELS were re-declared in 11+ files with
 * DIFFERENT values — the same status rendered with different colors on different
 * pages (e.g., Arrived was bg-amber-50 in one file, bg-yellow-100 in another,
 * bg-cyan-50 in a third). This file consolidates them into one canonical source.
 *
 * Color format: 3-part Tailwind classes (bg + text + border) for badge styling.
 * Files that only need 2 parts (bg + text) can still use this — the border class
 * is simply not applied if the element has no border.
 *
 * Usage:
 *   import { APPOINTMENT_STATUS_COLORS, APPOINTMENT_STATUS_LABELS } from "@/lib/statusStyles";
 *   <span className={cn("rounded-full px-2 py-0.5 text-xs", APPOINTMENT_STATUS_COLORS[status])}>
 *     {APPOINTMENT_STATUS_LABELS[status] ?? status}
 *   </span>
 */

// ─── Appointment Status Labels (Arabic) ───────────────────────────────────
export const APPOINTMENT_STATUS_LABELS: Record<string, string> = {
  Scheduled:  "مجدول",
  Confirmed:  "مؤكد",
  Arrived:    "وصل",
  Waiting:    "في الانتظار",
  Called:     "تم النداء",
  InRoom:     "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed:  "مكتمل",
  Cancelled:  "ملغي",
  NoShow:     "لم يحضر",
  Handoff:    "تسليم للاستقبال",
  Checkout:   "خروج",
  // Non-appointment statuses that appear in mixed timeline views
  signed:     "موقّع",
};

// ─── Appointment Status Colors (Tailwind, 3-part: bg + text + border) ─────
export const APPOINTMENT_STATUS_COLORS: Record<string, string> = {
  Scheduled:    "bg-blue-50 text-blue-700 border-blue-200",
  Confirmed:    "bg-indigo-50 text-indigo-700 border-indigo-200",
  Arrived:      "bg-amber-50 text-amber-700 border-amber-200",
  Waiting:      "bg-orange-50 text-orange-700 border-orange-200",
  Called:       "bg-purple-50 text-purple-700 border-purple-200",
  InRoom:       "bg-cyan-50 text-cyan-700 border-cyan-200",
  InProgress:   "bg-emerald-50 text-emerald-700 border-emerald-200",
  Completed:    "bg-green-50 text-green-700 border-green-200",
  Cancelled:    "bg-gray-100 text-gray-500 border-gray-200",
  NoShow:       "bg-red-50 text-red-600 border-red-200",
  Handoff:      "bg-teal-50 text-teal-700 border-teal-200",
  Checkout:     "bg-lime-50 text-lime-700 border-lime-200",
  // Non-appointment statuses
  signed:       "bg-green-100 text-green-700 border-green-200",
};

// ─── Convenience: combined label+color lookup ─────────────────────────────
export function getStatusBadge(status: string): { label: string; color: string } {
  return {
    label: APPOINTMENT_STATUS_LABELS[status] ?? status,
    color: APPOINTMENT_STATUS_COLORS[status] ?? "bg-gray-100 text-gray-500 border-gray-200",
  };
}

// ─── Clinic Queue Status (separate from appointment) ──────────────────────
export const CLINIC_QUEUE_STATUS_LABELS: Record<string, string> = {
  Waiting:    "في الانتظار",
  Called:     "تم النداء",
  InRoom:     "داخل الغرفة",
  InProgress: "قيد المعالجة",
  Completed:  "مكتمل",
  Cancelled:  "ملغي",
  NoShow:     "لم يحضر",
};

export const CLINIC_QUEUE_STATUS_COLORS: Record<string, string> = {
  Waiting:    "bg-orange-50 text-orange-700 border-orange-200",
  Called:     "bg-purple-50 text-purple-700 border-purple-200",
  InRoom:     "bg-cyan-50 text-cyan-700 border-cyan-200",
  InProgress: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Completed:  "bg-green-50 text-green-700 border-green-200",
  Cancelled:  "bg-gray-100 text-gray-500 border-gray-200",
  NoShow:     "bg-red-50 text-red-600 border-red-200",
};
