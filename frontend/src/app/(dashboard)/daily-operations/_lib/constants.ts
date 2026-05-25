/**
 * Daily Operations — Constants, types, and helpers.
 * Reuses existing journey types where possible.
 */

import type { DailyJourneySummary } from "@/types/journey";

// ─── Brand ───────────────────────────────────────────────────────────────────
export const NAVY = "#1a3a5c";
export const BLUE = "#3d7ab5";
export const ORANGE = "#f5922e";

// ─── Journey Item (from GET /api/patient-journey/today) ──────────────────────
export interface TodayJourneyItem {
  appointmentId: string;
  patientId: string;
  patientName: string;
  patientPhone?: string;
  appointmentTime: string;
  appointmentStatus: string;
  doctorId: string;
  doctorName: string;
  serviceId?: string;
  serviceName?: string;
  roomName?: string;
  queueItemId?: string;
  queueStatus?: string;
  visitId?: string;
  visitStatus?: string;
  consultationFeeRequired?: boolean;
  consultationFeePaid?: boolean;
  checkoutStatus?: string;
  nextAction: string;
}

// ─── Summary stats for cards ─────────────────────────────────────────────────
export interface DayStats {
  totalAppointments: number;
  arrived: number;
  waiting: number;
  inClinic: number;
  completed: number;
  noShow: number;
  todayPayments: number;
  overdueAmount: number;
}

// ─── Doctor option ───────────────────────────────────────────────────────────
export interface DoctorOption {
  id: string;
  name: string;
  specialty?: string;
}

// ─── Branch option ───────────────────────────────────────────────────────────
export interface BranchOption {
  id: string;
  name: string;
}

// ─── Room option ─────────────────────────────────────────────────────────────
export interface RoomOption {
  id: string;
  arabicName: string;
}

// ─── Service option ──────────────────────────────────────────────────────────
export interface ServiceOption {
  id: string;
  arabicName: string;
  defaultPrice?: number;
  requiresConsultationFee?: boolean;
}

// ─── Status Labels ───────────────────────────────────────────────────────────
export const APPT_STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغى",
  NoShow: "لم يحضر",
};

export const QUEUE_STATUS_LABELS: Record<string, string> = {
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغى",
};

export const ACTION_LABELS: Record<string, string> = {
  Intake: "تسجيل الوصول",
  SendToQueue: "إضافة للطابور",
  CallPatient: "نداء المريض",
  EnterRoom: "دخول الغرفة",
  StartVisit: "بدء الزيارة",
  InProgress: "جاري العلاج",
  Checkout: "الحساب والخروج",
  Handoff: "تسليم للاستقبال",
  None: "لا يوجد إجراء",
};

// ─── Status Colors ───────────────────────────────────────────────────────────
export const STATUS_COLORS: Record<string, { bg: string; text: string; border: string }> = {
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

// ─── Payment methods ─────────────────────────────────────────────────────────
export const PAYMENT_METHODS = [
  { value: "Cash",          label: "نقدي" },
  { value: "Card",          label: "بطاقة" },
  { value: "BankTransfer",  label: "تحويل بنكي" },
  { value: "MobileWallet",  label: "محفظة إلكترونية" },
];

// ─── Appointment types ───────────────────────────────────────────────────────
export const APPOINTMENT_TYPES = [
  { value: "Consultation",    label: "استشارة" },
  { value: "FollowUp",        label: "متابعة" },
  { value: "Treatment",       label: "علاج" },
  { value: "Emergency",       label: "طوارئ" },
  { value: "OrthoAdjustment", label: "تعديل تقويم" },
  { value: "Surgery",         label: "جراحة" },
];

// ─── Tab definitions ─────────────────────────────────────────────────────────
export type TabKey = "appointments" | "queue" | "inClinic" | "completed" | "payments" | "overdue";

export interface TabDef {
  key: TabKey;
  label: string;
  icon: string;
  color: string;
}

export const TABS: TabDef[] = [
  { key: "appointments", label: "مواعيد اليوم",  icon: "Calendar",       color: BLUE },
  { key: "queue",        label: "قائمة الانتظار", icon: "Clock",          color: ORANGE },
  { key: "inClinic",     label: "داخل العيادة",  icon: "Stethoscope",    color: "#9333ea" },
  { key: "completed",    label: "مكتمل اليوم",   icon: "CheckCircle",    color: "#16a34a" },
  { key: "payments",     label: "المدفوعات السريعة", icon: "CreditCard",  color: "#22c55e" },
  { key: "overdue",      label: "المتأخرات",     icon: "AlertTriangle",  color: "#ef4444" },
];

// ─── WhatsApp Templates ──────────────────────────────────────────────────────
export interface WhatsAppTemplate {
  key: string;
  label: string;
  build: (vars: { patientName: string; clinicName: string; aptDate: string; aptTime: string; doctorName: string; remaining?: number }) => string;
}

export const WHATSAPP_TEMPLATES: WhatsAppTemplate[] = [
  {
    key: "reminder",
    label: "تذكير بالموعد",
    build: ({ clinicName, aptDate, aptTime, doctorName }) =>
      `تذكير من ${clinicName}: لديكم موعد يوم ${aptDate} الساعة ${aptTime} مع ${doctorName}. نرجو الحضور في الموعد المحدد.`,
  },
  {
    key: "confirm",
    label: "تأكيد الحضور",
    build: ({ clinicName, patientName, aptDate, aptTime }) =>
      `عزيزي/عزيزتي ${patientName}، تم تأكيد حضوركم لموعد يوم ${aptDate} الساعة ${aptTime} في ${clinicName}. شكراً لكم.`,
  },
  {
    key: "waiting",
    label: "رسالة تأخير",
    build: ({ clinicName, patientName }) =>
      `عزيزي/عزيزتي ${patientName}، نعتذر عن التأخير. سيتم نداؤكم قريباً في ${clinicName}. شكراً لصبركم.`,
  },
  {
    key: "nextAppointment",
    label: "تأكيد الموعد القادم",
    build: ({ clinicName, patientName, aptDate, aptTime, doctorName }) =>
      `عزيزي/عزيزتي ${patientName}، تم حجز موعدكم القادم في ${clinicName} يوم ${aptDate} الساعة ${aptTime} مع ${doctorName}.`,
  },
  {
    key: "paymentReminder",
    label: "تذكير بدفع مستحق",
    build: ({ clinicName, patientName, remaining }) =>
      `عزيزي/عزيزتي ${patientName}، يوجد لديكم مستحق مبلغ ${remaining ?? 0} ر.ي في ${clinicName}. نرجى التكرم بسداد المستحق في أقرب وقت.`,
  },
];

// ─── Helpers ─────────────────────────────────────────────────────────────────
export function fmtRial(n: number | undefined | null): string {
  if (n == null) return "—";
  return n.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + " ر.ي";
}

export function fmtDate(d: string | Date): string {
  if (!d) return "—";
  const dt = typeof d === "string" ? new Date(d) : d;
  return dt.toLocaleDateString("ar-YE", { weekday: "long", year: "numeric", month: "long", day: "numeric" });
}

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

export function getTodayStr(): string {
  return new Date().toISOString().split("T")[0];
}

export function normalizePhone(phone: string | undefined): string {
  if (!phone) return "";
  let clean = phone.replace(/[^0-9+]/g, "");
  if (clean.startsWith("0")) {
    clean = "967" + clean.substring(1);
  } else if (!clean.startsWith("+") && !clean.startsWith("967") && clean.length <= 9) {
    clean = "967" + clean;
  }
  return clean;
}

export function inputCls(hasError = false): string {
  const base =
    "w-full rounded-lg border px-3 py-2 text-sm outline-none transition-colors";
  const normal = "border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20";
  const error = "border-red-300 bg-red-50 focus:border-red-400 focus:ring-1 focus:ring-red-200";
  return `${base} ${hasError ? error : normal}`;
}

// ─── Compute DayStats from journey items ─────────────────────────────────────
export function computeDayStats(items: TodayJourneyItem[]): DayStats {
  return {
    totalAppointments: items.length,
    arrived: items.filter(i => i.appointmentStatus === "Arrived").length,
    waiting: items.filter(i => i.queueStatus === "Waiting" || i.queueStatus === "Called").length,
    inClinic: items.filter(i => ["InRoom", "InProgress"].includes(i.appointmentStatus)).length,
    completed: items.filter(i => i.appointmentStatus === "Completed").length,
    noShow: items.filter(i => i.appointmentStatus === "NoShow").length,
    todayPayments: 0, // Will be filled from API if available
    overdueAmount: 0,
  };
}

// ─── Filter items by tab ─────────────────────────────────────────────────────
export function filterByTab(items: TodayJourneyItem[], tab: TabKey): TodayJourneyItem[] {
  switch (tab) {
    case "appointments":
      return items;
    case "queue":
      return items.filter(i =>
        i.queueStatus === "Waiting" || i.queueStatus === "Called" ||
        (i.appointmentStatus === "Waiting" && !i.queueStatus)
      );
    case "inClinic":
      return items.filter(i =>
        i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress" ||
        i.queueStatus === "InRoom" || i.queueStatus === "InProgress"
      );
    case "completed":
      return items.filter(i => i.appointmentStatus === "Completed");
    case "payments":
      return items.filter(i =>
        i.checkoutStatus === "ReadyForCheckout" ||
        i.nextAction === "Checkout" ||
        i.appointmentStatus === "Completed"
      );
    case "overdue":
      // Patients with NoShow or overdue — will be enhanced with finance data
      return items.filter(i =>
        i.appointmentStatus === "NoShow" || i.appointmentStatus === "Cancelled"
      );
    default:
      return items;
  }
}

// ─── Role helpers ────────────────────────────────────────────────────────────
export function isDoctorRole(role: string): boolean {
  return ["Orthodontist", "GeneralDentist", "OralSurgeon"].includes(role);
}

export function isReceptionRole(role: string): boolean {
  return role === "Reception";
}

export function isAccountantRole(role: string): boolean {
  return role === "Accountant";
}
