/**
 * Shared Journey Constants — Single Source of Truth.
 *
 * Re-exports canonical types/labels from @/types/journey.ts
 * and adds shared UI constants, color maps, and helper functions
 * used by both /patient-journey and /daily-operations pages.
 *
 * CONVENTION:
 *   - Arabic labels (APPOINTMENT_STATUS_ARABIC, NEXT_ACTION_ARABIC, etc.)
 *     live in @/types/journey.ts — import from there.
 *   - UI-specific color maps, payment methods, and helpers live here.
 *   - Page-specific constants (tabs, WhatsApp templates, etc.) stay in
 *     their respective page _lib/constants.ts files.
 */

// ─── Re-export canonical labels from SSOT ─────────────────────────────────
export {
  JOURNEY_STEPS,
  STEP_ORDER,
  getStepIndex,
  APPOINTMENT_STATUS_ARABIC,
  QUEUE_STATUS_ARABIC,
  CHECKOUT_STATUS_ARABIC,
  NEXT_ACTION_ARABIC,
  type CheckoutStatus,
  isCheckoutStatus,
  TERMINAL_CHECKOUT_STATUSES,
  isTerminalCheckoutStatus,
} from "@/types/journey";

// ─── Legacy aliases (backward compat) ───────────────────────────────────
import {
  APPOINTMENT_STATUS_ARABIC as _STATUS_LABELS,
  NEXT_ACTION_ARABIC as _ACTION_LABELS,
} from "@/types/journey";

/** @deprecated Use APPOINTMENT_STATUS_ARABIC from @/types/journey instead */
export const STATUS_LABELS = _STATUS_LABELS;

/** @deprecated Use NEXT_ACTION_ARABIC from @/types/journey instead */
export const ACTION_LABELS = _ACTION_LABELS;

// ─── Status Colors (Tailwind) ─────────────────────────────────────────────
/** Tailwind class-based status colors — used by /patient-journey page */
export const STATUS_COLORS_TAILWIND: Record<string, string> = {
  Scheduled:  "bg-blue-50 text-blue-700",
  Confirmed:  "bg-indigo-50 text-indigo-700",
  Arrived:    "bg-amber-50 text-amber-700",
  Waiting:    "bg-orange-50 text-orange-700",
  Called:     "bg-purple-50 text-purple-700",
  InRoom:     "bg-cyan-50 text-cyan-700",
  InProgress: "bg-emerald-50 text-emerald-700",
  Completed:  "bg-green-50 text-green-700",
  Cancelled:  "bg-gray-100 text-gray-500",
  NoShow:     "bg-red-50 text-red-700",
  Handoff:    "bg-teal-50 text-teal-700",
  Checkout:   "bg-lime-50 text-lime-700",
};

/** @deprecated Use STATUS_COLORS_TAILWIND or STATUS_COLORS_HEX instead */
export const STATUS_COLORS = STATUS_COLORS_TAILWIND;

// ─── Status Colors (Hex) ──────────────────────────────────────────────────
/** Hex-based status colors — used by /daily-operations page */
export const STATUS_COLORS_HEX: Record<string, { bg: string; text: string; border: string }> = {
  Scheduled:  { bg: "#f0f5fb", text: "#3d7ab5", border: "#dce8f5" },
  Confirmed:  { bg: "#eff6ff", text: "#2563eb", border: "#bfdbfe" },
  Arrived:    { bg: "#f0fdf4", text: "#16a34a", border: "#bbf7d0" },
  Waiting:    { bg: "#fff7ed", text: "#f5922e", border: "#fde8d0" },
  Called:     { bg: "#fef3c7", text: "#d97706", border: "#fde68a" },
  InRoom:     { bg: "#faf5ff", text: "#9333ea", border: "#e9d5ff" },
  InProgress: { bg: "#fef2f2", text: "#dc2626", border: "#fecaca" },
  Completed:  { bg: "#f0fdf4", text: "#16a34a", border: "#bbf7d0" },
  Cancelled:  { bg: "#f5f5f5", text: "#6b7280", border: "#e5e7eb" },
  NoShow:     { bg: "#fef2f2", text: "#ef4444", border: "#fecaca" },
};

// ─── Action Colors (Tailwind) ─────────────────────────────────────────────
export const ACTION_COLORS: Record<string, string> = {
  Intake:      "bg-amber-500 hover:bg-amber-600",
  SendToQueue: "bg-blue-500 hover:bg-blue-600",
  CallPatient: "bg-purple-500 hover:bg-purple-600",
  EnterRoom:   "bg-cyan-500 hover:bg-cyan-600",
  StartVisit:  "bg-emerald-500 hover:bg-emerald-600",
  InProgress:  "bg-gray-400",
  Handoff:     "bg-teal-500 hover:bg-teal-600",
  Checkout:    "bg-green-600 hover:bg-green-700",
  None:        "bg-gray-300",
};

// ─── Payment Methods (full list — superset) ───────────────────────────────
export const PAYMENT_METHODS = [
  { value: "cash",          label: "نقدي" },
  { value: "bank_transfer", label: "تحويل بنكي" },
  { value: "karimey",       label: "حاسب الكريمي" },
  { value: "jawaly",        label: "ام فلوس / جوالي" },
  { value: "transfer",      label: "حوالة" },
  { value: "card",          label: "بطاقة" },
  { value: "check",         label: "شيك" },
  { value: "other",         label: "أخرى" },
];

// ─── Appointment Types ─────────────────────────────────────────────────────
export const APPOINTMENT_TYPES = [
  { value: "Consultation",    label: "استشارة" },
  { value: "FollowUp",        label: "متابعة" },
  { value: "Treatment",       label: "علاج" },
  { value: "Emergency",       label: "حالة إسعافية" },
  { value: "OrthoAdjustment", label: "تعديل تقويم" },
  { value: "Surgery",         label: "جراحة" },
];

// ─── Severity Styles ──────────────────────────────────────────────────────
// FE-10: Added optional `icon` field so patient-journey/[patientId]/page.tsx can use the
// shared SEVERITY_STYLES instead of a local copy. Cards.tsx (which imports the same const)
// is unaffected — it only reads bg/border/text.
export const SEVERITY_STYLES: Record<string, { bg: string; border: string; text: string; icon: string }> = {
  danger:  { bg: "bg-[#fcebeb]", border: "border-[#f09595]/50", text: "text-[#a32d2d]", icon: "text-[#a32d2d]" },
  warning: { bg: "bg-[#faeeda]", border: "border-[#fac775]/50", text: "text-[#633806]", icon: "text-[#ba7517]" },
  info:    { bg: "bg-[#e6f1fb]", border: "border-[#85b7eb]/50", text: "text-[#185fa5]", icon: "text-[#185fa5]" },
};

// ─── Timeline Dot Colors ──────────────────────────────────────────────────
export const TIMELINE_DOT_COLORS: Record<string, string> = {
  appointment: "bg-[#3d7ab5]",
  visit:       "bg-[#3d7ab5]",
  payment:     "bg-[#fac775]",
  invoice:     "bg-[#185fa5]",
  document:    "bg-[#d3d1c7]",
  ortho:       "bg-[#3d7ab5]",
  message:     "bg-[#185fa5]",
  default:     "bg-[#d3d1c7]",
};

// ─── Helper: Format Currency ──────────────────────────────────────────────
export function fmtRial(n: number | undefined | null): string {
  if (n == null) return "—";
  return n.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + " ر.ي";
}

// ─── Helper: Format Date ──────────────────────────────────────────────────
export function fmtDate(d: string | Date): string {
  if (!d) return "—";
  const dt = typeof d === "string" ? new Date(d) : d;
  return dt.toLocaleDateString("ar-YE", { weekday: "long", year: "numeric", month: "long", day: "numeric" });
}

// ─── Helper: Format Time ──────────────────────────────────────────────────
export function fmtTime(t: string | undefined): string {
  if (!t) return "—";
  // t can be "HH:mm" or ISO
  if (t.length === 5) {
    const [h, m] = t.split(":");
    const hour = parseInt(h, 10);
    const ampm = hour >= 12 ? "م" : "ص";
    const h12 = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
    return `${h12}:${m} ${ampm}`;
  }
  const dt = new Date(t);
  return dt.toLocaleTimeString("ar-YE", { hour: "2-digit", minute: "2-digit" });
}

// ─── Helper: Get Initials ─────────────────────────────────────────────────
export function getInitials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("");
}

// ─── Helper: Input Classes ────────────────────────────────────────────────
export function inputCls(hasError = false): string {
  const base =
    "w-full rounded-lg border px-3 py-2 text-sm outline-none transition-colors";
  const normal = "border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20";
  const error = "border-red-300 bg-red-50 focus:border-red-400 focus:ring-1 focus:ring-red-200";
  return `${base} ${hasError ? error : normal}`;
}

// ─── Helper: Normalize Phone ──────────────────────────────────────────────
export function normalizePhone(phone: string | undefined): string {
  if (!phone) return "";
  let clean = phone.replace(/[^0-9+]/g, "");
  if (clean.startsWith("0")) {
    clean = "967" + clean.substring(1);
  } else if (!clean.startsWith("+") && !clean.startsWith("967") && clean.length <= 9) {
    clean = "967" + clean;
  }
  if (clean.startsWith("+")) clean = clean.substring(1);
  return clean;
}

// ─── Role Helpers ──────────────────────────────────────────────────────────
export function isDoctorRole(role: string): boolean {
  return ["Orthodontist", "GeneralDentist", "OralSurgeon"].includes(role);
}

export function isReceptionRole(role: string): boolean {
  return role === "Reception";
}

export function isAccountantRole(role: string): boolean {
  return role === "Accountant";
}
