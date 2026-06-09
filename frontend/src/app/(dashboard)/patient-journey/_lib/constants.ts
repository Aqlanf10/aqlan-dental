// ─── Journey Step Definitions ────────────────────────────────────────────────

export const JOURNEY_STEPS = [
  { key: "Scheduled", label: "مجدول" },
  { key: "Arrived", label: "وصل" },
  { key: "Waiting", label: "في الانتظار" },
  { key: "Called", label: "تم النداء" },
  { key: "InRoom", label: "داخل الغرفة" },
  { key: "InProgress", label: "قيد المعالجة" },
  { key: "Handoff", label: "تسليم" },
  { key: "Checkout", label: "إنهاء الحساب" },
] as const;

export const STEP_ORDER_MAP: Record<string, number> = {
  Scheduled: 0,
  Arrived: 1,
  Waiting: 2,
  Called: 3,
  InRoom: 4,
  InProgress: 5,
  Handoff: 6,
  Checkout: 7,
  Completed: 8,
};

export function getStepIndex(status: string): number {
  return STEP_ORDER_MAP[status] ?? -1;
}

export function getStepStatus(stepKey: string, currentStep: string): "done" | "current" | "pending" {
  const currentIdx = getStepIndex(currentStep);
  const stepIdx = getStepIndex(stepKey);
  if (currentIdx < 0 || stepIdx < 0) return "pending";
  if (stepIdx < currentIdx) return "done";
  if (stepIdx === currentIdx) return "current";
  return "pending";
}

// ─── Status Labels & Colors ─────────────────────────────────────────────────

export const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر",
  Handoff: "تسليم",
  Checkout: "إنهاء الحساب",
};

export const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-50 text-blue-700",
  Confirmed: "bg-indigo-50 text-indigo-700",
  Arrived: "bg-amber-50 text-amber-700",
  Waiting: "bg-orange-50 text-orange-700",
  Called: "bg-purple-50 text-purple-700",
  InRoom: "bg-cyan-50 text-cyan-700",
  InProgress: "bg-emerald-50 text-emerald-700",
  Completed: "bg-green-50 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-50 text-red-700",
  Handoff: "bg-teal-50 text-teal-700",
  Checkout: "bg-lime-50 text-lime-700",
};

export const ACTION_LABELS: Record<string, string> = {
  Intake: "تسجيل الوصول",
  SendToQueue: "إدخال للانتظار",
  CallPatient: "نداء المريض",
  EnterRoom: "إدخال الغرفة",
  StartVisit: "بدء الزيارة",
  InProgress: "عند الطبيب",
  Handoff: "تسليم للاستقبال",
  Checkout: "إنهاء الحساب",
  None: "—",
};

export const ACTION_COLORS: Record<string, string> = {
  Intake: "bg-amber-500 hover:bg-amber-600",
  SendToQueue: "bg-blue-500 hover:bg-blue-600",
  CallPatient: "bg-purple-500 hover:bg-purple-600",
  EnterRoom: "bg-cyan-500 hover:bg-cyan-600",
  StartVisit: "bg-emerald-500 hover:bg-emerald-600",
  InProgress: "bg-gray-400",
  Handoff: "bg-teal-500 hover:bg-teal-600",
  Checkout: "bg-green-600 hover:bg-green-700",
  None: "bg-gray-300",
};

export const PAYMENT_METHODS = [
  { value: "cash", label: "نقدي" },
  { value: "transfer", label: "تحويل" },
  { value: "card", label: "بطاقة" },
];

export const SEVERITY_STYLES: Record<string, { bg: string; border: string; text: string }> = {
  danger: { bg: "bg-[#fcebeb]", border: "border-[#f09595]/50", text: "text-[#a32d2d]" },
  warning: { bg: "bg-[#faeeda]", border: "border-[#fac775]/50", text: "text-[#633806]" },
  info: { bg: "bg-[#e6f1fb]", border: "border-[#85b7eb]/50", text: "text-[#185fa5]" },
};

export const TIMELINE_DOT_COLORS: Record<string, string> = {
  appointment: "bg-[#3d7ab5]",
  visit: "bg-[#3d7ab5]",
  payment: "bg-[#fac775]",
  invoice: "bg-[#185fa5]",
  document: "bg-[#d3d1c7]",
  ortho: "bg-[#3d7ab5]",
  message: "bg-[#185fa5]",
  default: "bg-[#d3d1c7]",
};

// ─── Types ──────────────────────────────────────────────────────────────────

export interface JourneyItem {
  appointmentId: string | null;
  patientId: string;
  patientName: string;
  patientPhone?: string;
  appointmentTime?: string;
  appointmentStatus: string;
  doctorId?: string;
  doctorName: string;
  serviceId?: string;
  serviceName?: string;
  roomName?: string;
  queueItemId?: string;
  queueStatus?: string;
  visitId?: string;
  visitStatus?: string;
  consultationFeeRequired: boolean;
  consultationFeePaid: boolean;
  checkoutStatus?: string;
  nextAction: string;
}

export interface ServiceOption {
  id: string;
  arabicName: string;
  code: string;
  defaultPrice: number;
  requiresConsultationFee: boolean;
}

export interface RoomOption {
  id: string;
  arabicName: string;
  code: string;
  roomType: string;
}

// ─── Helpers ────────────────────────────────────────────────────────────────

export const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] focus:border-[#3d7ab5]";

export function fmtRial(amount: number): string {
  return amount.toLocaleString("ar-SA") + " ر.ي";
}

export function fmtDate(dateStr: string): string {
  try {
    return new Intl.DateTimeFormat("ar-YE", {
      year: "numeric", month: "long", day: "numeric",
    }).format(new Date(dateStr));
  } catch {
    return dateStr;
  }
}

export function fmtTime(timeStr: string): string {
  const [h, m] = timeStr.split(":");
  const hour = parseInt(h);
  const period = hour >= 12 ? "م" : "ص";
  const h12 = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
  return `${h12}:${m} ${period}`;
}

export function getInitials(name: string): string {
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("");
}

// ─── Role Helpers ───────────────────────────────────────────────────────────

export function isDoctorRole(role: string): boolean {
  return role === "Orthodontist" || role === "GeneralDentist" || role === "OralSurgeon";
}

export function isAccountantRole(role: string): boolean {
  return role === "Accountant";
}

export function isReceptionRole(role: string): boolean {
  return role === "Reception";
}
