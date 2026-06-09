/**
 * Daily Operations — Page-specific constants, types, and helpers.
 *
 * Shared constants and types are imported from:
 *   @/components/shared/journey/constants  (labels, colors, helpers)
 *   @/components/shared/journey/types       (TodayJourneyItem, etc.)
 *
 * This file contains ONLY page-specific items (tabs, WhatsApp templates,
 * room occupancy, doctor workload, etc.) not used by /patient-journey.
 */

// ─── Import shared types for local use + re-export ──────────────────────
import type { TodayJourneyItem, RoomOption } from "@/components/shared/journey/types";
export type { TodayJourneyItem, ServiceOption, RoomOption, DoctorOption, BranchOption } from "@/components/shared/journey/types";

// ─── Import shared constants for local use + re-export ────────────────────
import {
  APPOINTMENT_STATUS_ARABIC as _APPT_STATUS_ARABIC,
} from "@/components/shared/journey/constants";

export {
  APPOINTMENT_STATUS_ARABIC,
  QUEUE_STATUS_ARABIC,
  CHECKOUT_STATUS_ARABIC,
  NEXT_ACTION_ARABIC,
  NEXT_ACTION_ARABIC as ACTION_LABELS,
  STATUS_COLORS_TAILWIND,
  STATUS_COLORS_HEX,
  STATUS_COLORS_HEX as STATUS_COLORS,
  ACTION_COLORS,
  PAYMENT_METHODS,
  APPOINTMENT_TYPES,
  fmtRial,
  fmtDate,
  fmtTime,
  inputCls,
  normalizePhone,
  isDoctorRole,
  isReceptionRole,
  isAccountantRole,
  getStepIndex,
} from "@/components/shared/journey/constants";

// Legacy aliases for backward compat with existing daily-ops components
import {
  QUEUE_STATUS_ARABIC as _QUEUE_STATUS_ARABIC,
} from "@/components/shared/journey/constants";

/** @deprecated Use APPOINTMENT_STATUS_ARABIC from @/components/shared/journey/constants instead */
export const APPT_STATUS_LABELS = _APPT_STATUS_ARABIC;

/** @deprecated Use QUEUE_STATUS_ARABIC from @/components/shared/journey/constants instead */
export const QUEUE_STATUS_LABELS = _QUEUE_STATUS_ARABIC;

// ─── Brand ───────────────────────────────────────────────────────────────────
export const NAVY = "#1a3a5c";
export const BLUE = "#3d7ab5";
export const ORANGE = "#f5922e";

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
  noShowRate: number; // percentage
  overdueAppointments: number; // appointments past their time without arrival
}

// ─── Room occupancy info ──────────────────────────────────────────────────────
export interface RoomOccupancy {
  roomId: string;
  roomName: string;
  isOccupied: boolean;
  patientName?: string;
  doctorName?: string;
  since?: string;
}

// ─── Doctor workload ──────────────────────────────────────────────────────────
export interface DoctorWorkload {
  doctorId: string;
  doctorName: string;
  totalPatients: number;
  completed: number;
  inClinic: number;
  waiting: number;
}

// ─── Finance Summary from API ────────────────────────────────────────────────
export interface FinanceSummaryData {
  todayCollected: number;
  monthCollected: number;
  totalOutstanding: number;
  activeContracts: number;
  unpaidInvoicesCount: number;
  draftInvoicesCount: number;
  overdueAmount: number;
  pendingCommissionsAmount: number;
  recentPayments?: { id: string; amount: number; paymentDate: string; patientName?: string }[];
  recentInvoices?: { id: string; invoiceNumber: string; totalAmount: number; status: string }[];
}

// ─── Undo Action type ────────────────────────────────────────────────────────
export interface UndoAction {
  id: string;
  type: "Cancel" | "NoShow" | "CancelQueue";
  appointmentId: string | null;
  previousStatus: string;
  queueItemId?: string;
  patientName: string;
  timestamp: number;
}

// ─── Tab definitions ─────────────────────────────────────────────────────────
export type TabKey = "appointments" | "queue" | "inClinic" | "completed" | "payments" | "overdue";

export interface TabDef {
  key: TabKey;
  label: string;
  icon: string;
  color: string;
  shortcut: string;
}

export const TABS: TabDef[] = [
  { key: "appointments", label: "مواعيد اليوم",  icon: "Calendar",       color: BLUE,       shortcut: "1" },
  { key: "queue",        label: "قائمة الانتظار", icon: "Clock",          color: ORANGE,     shortcut: "2" },
  { key: "inClinic",     label: "داخل العيادة",  icon: "Stethoscope",    color: "#9333ea",  shortcut: "3" },
  { key: "completed",    label: "مكتمل اليوم",   icon: "CheckCircle",    color: "#16a34a",  shortcut: "4" },
  { key: "payments",     label: "المدفوعات السريعة", icon: "CreditCard",  color: "#22c55e",  shortcut: "5" },
  { key: "overdue",      label: "المتأخرات",     icon: "AlertTriangle",  color: "#ef4444",  shortcut: "6" },
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

// ─── Keyboard Shortcuts ──────────────────────────────────────────────────────
export const KEYBOARD_SHORTCUTS = [
  { keys: "Ctrl + R",     description: "تحديث البيانات" },
  { keys: "Ctrl + F",     description: "التركيز على البحث" },
  { keys: "1 – 6",        description: "تبديل التبويبات" },
  { keys: "Escape",        description: "إغلاق النافذة المفتوحة" },
  { keys: "Ctrl + N",     description: "مريض مشي جديد" },
];

// ─── Page-specific Helpers ─────────────────────────────────────────────────

export function fmtWaitMinutes(minutes: number | undefined | null): string {
  if (minutes == null || minutes <= 0) return "—";
  if (minutes < 60) return `~${minutes} دقيقة`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `~${hours} ساعة ${mins} دقيقة` : `~${hours} ساعة`;
}

export function getTodayStr(): string {
  return new Date().toISOString().split("T")[0];
}

// ─── Compute DayStats from journey items + finance summary ────────────────────
export function computeDayStats(items: TodayJourneyItem[], financeSummary?: FinanceSummaryData | null): DayStats {
  const totalAppointments = items.length;
  const noShow = items.filter(i => i.appointmentStatus === "NoShow").length;
  const now = new Date();
  const overdueAppointments = items.filter(i => {
    if (i.appointmentStatus !== "Scheduled" && i.appointmentStatus !== "Confirmed") return false;
    const timeStr = i.appointmentTime;
    if (!timeStr) return false;
    let aptDate: Date;
    if (timeStr.length === 5) {
      const [h, m] = timeStr.split(":").map(Number);
      aptDate = new Date();
      aptDate.setHours(h, m, 0, 0);
    } else {
      aptDate = new Date(timeStr);
    }
    return now.getTime() - aptDate.getTime() > 15 * 60 * 1000;
  }).length;

  return {
    totalAppointments,
    arrived: items.filter(i => i.appointmentStatus === "Arrived").length,
    waiting: items.filter(i => i.queueStatus === "Waiting" || i.queueStatus === "Called").length,
    inClinic: items.filter(i => ["InRoom", "InProgress"].includes(i.appointmentStatus)).length,
    completed: items.filter(i => i.appointmentStatus === "Completed").length,
    noShow,
    todayPayments: financeSummary?.todayCollected ?? 0,
    overdueAmount: financeSummary?.overdueAmount ?? 0,
    noShowRate: totalAppointments > 0 ? Math.round((noShow / totalAppointments) * 100) : 0,
    overdueAppointments,
  };
}

// ─── Compute room occupancy ──────────────────────────────────────────────────
export function computeRoomOccupancy(
  rooms: RoomOption[],
  items: TodayJourneyItem[]
): RoomOccupancy[] {
  return rooms.map(room => {
    const occupant = items.find(i =>
      i.roomId === room.id &&
      (i.appointmentStatus === "InRoom" || i.appointmentStatus === "InProgress" ||
       i.queueStatus === "InRoom" || i.queueStatus === "InProgress")
    );
    return {
      roomId: room.id,
      roomName: room.arabicName,
      isOccupied: !!occupant,
      patientName: occupant?.patientName,
      doctorName: occupant?.doctorName,
      since: occupant?.inRoomSince,
    };
  });
}

// ─── Compute doctor workload ─────────────────────────────────────────────────
export function computeDoctorWorkload(items: TodayJourneyItem[]): DoctorWorkload[] {
  const map = new Map<string, DoctorWorkload>();
  for (const item of items) {
    const docId = item.doctorId ?? "unknown";
    let entry = map.get(docId);
    if (!entry) {
      entry = { doctorId: docId, doctorName: item.doctorName, totalPatients: 0, completed: 0, inClinic: 0, waiting: 0 };
      map.set(docId, entry);
    }
    entry.totalPatients++;
    if (item.appointmentStatus === "Completed") entry.completed++;
    if (["InRoom", "InProgress"].includes(item.appointmentStatus)) entry.inClinic++;
    if (item.queueStatus === "Waiting" || item.queueStatus === "Called" || item.appointmentStatus === "Waiting") entry.waiting++;
  }
  return Array.from(map.values());
}

// ─── Get next patient to be called/entered ────────────────────────────────────
export function getNextPatient(items: TodayJourneyItem[]): TodayJourneyItem | null {
  const waitingToCall = items.find(i =>
    i.queueStatus === "Waiting" && i.nextAction === "CallPatient"
  );
  if (waitingToCall) return waitingToCall;
  const waitingToEnter = items.find(i =>
    i.queueStatus === "Called" && i.nextAction === "EnterRoom"
  );
  if (waitingToEnter) return waitingToEnter;
  const needsIntake = items.find(i =>
    (i.appointmentStatus === "Scheduled" || i.appointmentStatus === "Confirmed" || i.appointmentStatus === "Arrived") &&
    (i.nextAction === "Intake" || i.nextAction === "SendToQueue")
  );
  return needsIntake ?? null;
}

// ─── Format session duration ─────────────────────────────────────────────────
export function fmtSessionDuration(sinceIso?: string): string {
  if (!sinceIso) return "";
  const start = new Date(sinceIso).getTime();
  const diffMs = Date.now() - start;
  if (diffMs < 0) return "";
  const mins = Math.floor(diffMs / 60000);
  if (mins < 60) return `${mins} د`;
  const hrs = Math.floor(mins / 60);
  const remMins = mins % 60;
  return remMins > 0 ? `${hrs}س ${remMins}د` : `${hrs}س`;
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
        (i.appointmentStatus === "Completed" && i.checkoutStatus !== "CheckedOut" && i.amountDueReference != null && i.amountDueReference > 0)
      );
    case "overdue":
      return items.filter(i =>
        i.appointmentStatus === "NoShow" || i.appointmentStatus === "Cancelled"
      );
    default:
      return items;
  }
}

// ─── Check if appointment is overdue ──────────────────────────────────────────
export function isAppointmentOverdue(item: TodayJourneyItem): boolean {
  if (item.appointmentStatus !== "Scheduled" && item.appointmentStatus !== "Confirmed") return false;
  const timeStr = item.appointmentTime;
  if (!timeStr) return false;
  const now = new Date();
  let aptDate: Date;
  if (timeStr.length === 5) {
    const [h, m] = timeStr.split(":").map(Number);
    aptDate = new Date();
    aptDate.setHours(h, m, 0, 0);
  } else {
    aptDate = new Date(timeStr);
  }
  return now.getTime() - aptDate.getTime() > 15 * 60 * 1000;
}

// ─── Format overdue minutes ──────────────────────────────────────────────────
export function fmtOverdueMinutes(item: TodayJourneyItem): string {
  const timeStr = item.appointmentTime;
  if (!timeStr) return "";
  let aptDate: Date;
  if (timeStr.length === 5) {
    const [h, m] = timeStr.split(":").map(Number);
    aptDate = new Date();
    aptDate.setHours(h, m, 0, 0);
  } else {
    aptDate = new Date(timeStr);
  }
  const diffMs = Date.now() - aptDate.getTime();
  if (diffMs <= 15 * 60 * 1000) return "";
  const overdueMins = Math.floor((diffMs - 15 * 60 * 1000) / 60000);
  return overdueMins < 60 ? `متأخر ${overdueMins} د` : `متأخر ${Math.floor(overdueMins / 60)}س ${overdueMins % 60}د`;
}
