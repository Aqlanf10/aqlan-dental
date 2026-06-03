/**
 * Daily Operations — Constants, types, and helpers.
 * Reuses existing journey types where possible.
 */

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
  patientNumber?: string;
  appointmentTime: string;
  appointmentType?: string;
  appointmentStatus: string;
  doctorId: string;
  doctorName: string;
  serviceId?: string;
  serviceName?: string;
  roomName?: string;
  roomId?: string;
  queueItemId?: string;
  queueStatus?: string;
  visitId?: string;
  visitStatus?: string;
  proposedProcedure?: string;
  consultationFeeRequired?: boolean;
  consultationFeePaid?: boolean;
  paymentBeforeEntryRequired?: boolean;
  financialEntryStatus?: string;
  financialEntryReason?: string | null;
  canEnterWithoutPayment?: boolean;
  managerOverrideAllowed?: boolean;
  hasDraftInvoice?: boolean;
  hasLabOrder?: boolean;
  labOrderStatus?: string;
  amountDueReference?: number;
  treatmentDone?: string;
  chiefComplaint?: string;
  checkoutStatus?: string;
  nextAction: string;
  hasMedicalAlerts?: boolean;
  visitCount?: number;
  inRoomSince?: string; // ISO timestamp when patient entered room
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
  appointmentId: string;
  previousStatus: string;
  queueItemId?: string;
  patientName: string;
  timestamp: number;
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
  SendToQueue: "إضافة للانتظار",
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
  { value: "cash",          label: "نقدي" },
  { value: "bank_transfer", label: "تحويل بنكي" },
  { value: "karimey",       label: "حاسب الكريمي" },
  { value: "jawaly",        label: "ام فلوس / جوالي" },
  { value: "transfer",      label: "حوالة" },
  { value: "card",          label: "بطاقة" },
  { value: "check",         label: "شيك" },
  { value: "other",         label: "أخرى" },
];

// ─── Appointment types ───────────────────────────────────────────────────────
export const APPOINTMENT_TYPES = [
  { value: "Consultation",    label: "استشارة" },
  { value: "FollowUp",        label: "متابعة" },
  { value: "Treatment",       label: "علاج" },
  { value: "Emergency",       label: "حالة إسعافية" },
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

export function normalizePhone(phone: string | undefined): string {
  if (!phone) return "";
  let clean = phone.replace(/[^0-9+]/g, "");
  if (clean.startsWith("0")) {
    clean = "967" + clean.substring(1);
  } else if (!clean.startsWith("+") && !clean.startsWith("967") && clean.length <= 9) {
    clean = "967" + clean;
  }
  // Remove leading + if present (wa.me URLs don't use +)
  if (clean.startsWith("+")) clean = clean.substring(1);
  return clean;
}

export function inputCls(hasError = false): string {
  const base =
    "w-full rounded-lg border px-3 py-2 text-sm outline-none transition-colors";
  const normal = "border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20";
  const error = "border-red-300 bg-red-50 focus:border-red-400 focus:ring-1 focus:ring-red-200";
  return `${base} ${hasError ? error : normal}`;
}

// ─── Compute DayStats from journey items + finance summary ────────────────────
export function computeDayStats(items: TodayJourneyItem[], financeSummary?: FinanceSummaryData | null): DayStats {
  const totalAppointments = items.length;
  const noShow = items.filter(i => i.appointmentStatus === "NoShow").length;
  const now = new Date();
  const overdueAppointments = items.filter(i => {
    if (i.appointmentStatus !== "Scheduled" && i.appointmentStatus !== "Confirmed") return false;
    // Parse appointment time — format can be "HH:mm" or ISO
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
    // Overdue if appointment time is more than 15 minutes in the past
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
    let entry = map.get(item.doctorId);
    if (!entry) {
      entry = { doctorId: item.doctorId, doctorName: item.doctorName, totalPatients: 0, completed: 0, inClinic: 0, waiting: 0 };
      map.set(item.doctorId, entry);
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
  // Priority: first patient in queue waiting to be called
  const waitingToCall = items.find(i =>
    i.queueStatus === "Waiting" && i.nextAction === "CallPatient"
  );
  if (waitingToCall) return waitingToCall;
  // Then: patient called and waiting to enter room
  const waitingToEnter = items.find(i =>
    i.queueStatus === "Called" && i.nextAction === "EnterRoom"
  );
  if (waitingToEnter) return waitingToEnter;
  // Then: scheduled patient who needs intake
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

// ─── Check if appointment is overdue (past time + 15 min, still Scheduled/Confirmed) ──
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
