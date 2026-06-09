/**
 * Shared types for Patient Daily Journey Hub.
 * Aligned with backend: GET /api/patient-journey/{patientId}/daily-summary
 */

// ─── Daily Summary Response ─────────────────────────────────────────────

export interface DailyJourneySummary {
  patient: DailyJourneyPatient;
  todayAppointment: DailyJourneyAppointment | null;
  queueStatus: DailyJourneyQueue | null;
  todayVisit: DailyJourneyVisit | null;
  financeSummary: DailyJourneyFinance | null;
  unpaidInvoicesCount: number;
  activeContract: DailyJourneyContract | null;
  activeOrthoCase: DailyJourneyOrtho | null;
  medicalAlerts: MedicalAlert[];
  recentVisits: DailyJourneyRecentVisit[];
  timeline: TimelineEvent[];
  journeyStep: string;
  nextAction: string;
}

export interface DailyJourneyPatient {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
  email?: string;
  gender?: string;
  age?: number;
  branchId?: string;
  primaryDoctorId?: string;
}

export interface DailyJourneyAppointment {
  id: string;
  appointmentDate: string;
  startTime: string;
  endTime?: string;
  appointmentType?: string;
  status: string;
  doctorId?: string;
  doctorName: string;
  serviceId?: string;
  roomName?: string;
  specialty?: string;
  arrivedAt?: string;
  calledAt?: string;
  inRoomAt?: string;
  notes?: string;
}

export interface DailyJourneyQueue {
  id: string;
  status: string;
  roomName?: string;
  calledAt?: string;
  inRoomAt?: string;
  startedAt?: string;
  doctorId?: string;
  serviceId?: string;
}

export interface DailyJourneyVisit {
  id: string;
  visitType?: string;
  specialty?: string;
  doctorId?: string;
  chiefComplaint?: string;
  clinicalNotes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  instructions?: string;
  nextVisitPlan?: string;
  cost?: number;
  nextVisitDate?: string;
  checkoutStatus?: CheckoutStatus;
  readyForCheckoutAt?: string;
  amountDueReference?: number;
  appointmentId?: string;
}

/**
 * Finance summary returned by the journey API.
 * Full access (Admin/Accountant): all fields present.
 * Limited access (Reception): only checkout-relevant fields —
 *   outstandingBalance, overdueAmount, latestPayment, financialStatus.
 *   totalTreatmentCost, totalPaid, activeContractsCount, totalPaymentsCount will be undefined.
 */
export interface DailyJourneyFinance {
  totalTreatmentCost?: number;
  totalPaid?: number;
  outstandingBalance: number;
  overdueAmount: number;
  latestPayment: { id: string; amount: number; paymentDate: string; paymentMethod?: string; receiptNumber?: string } | null;
  financialStatus: "no_plan" | "paid_full" | "has_balance" | "overdue";
  activeContractsCount?: number;
  totalPaymentsCount?: number;
}

export interface DailyJourneyContract {
  id: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  installmentAmount?: number;
  installmentsCount: number;
  specialty?: string;
  startDate?: string;
  status: string;
}

/**
 * Ortho case summary from backend.
 * Matches actual OrthoCase entity fields:
 *   ApplianceType (not CaseType), ExpectedDurationMonths (not EstimatedEndDate),
 *   CurrentStage (not Notes), plus TotalFee and StagePercentage.
 */
export interface DailyJourneyOrtho {
  id: string;
  caseNumber?: string;
  status: string;
  applianceType?: string;            // was caseType — OrthoCase has ApplianceType
  startDate?: string;
  expectedDurationMonths?: number;   // was estimatedEndDate — compute on frontend if needed
  currentStage?: string;             // was notes — OrthoCase has CurrentStage
  doctorId?: string;
  totalFee?: number;
  stagePercentage?: number;
}

export interface MedicalAlert {
  type: string;
  label: string;
  value: string;
  severity: "danger" | "warning" | "info";
}

export interface DailyJourneyRecentVisit {
  id: string;
  visitDate: string;
  visitType?: string;
  chiefComplaint?: string;
  treatmentDone?: string;
  diagnosis?: string;
  doctorId?: string;
  cost?: number;
}

export interface TimelineEvent {
  date: string;
  type: string;
  title: string;
  sub: string;
  status?: string;
}

// ─── Journey Step Constants ──────────────────────────────────────────────

export const JOURNEY_STEPS = [
  { key: "Scheduled", label: "مجدول", icon: "Calendar" },
  { key: "Confirmed", label: "مؤكد", icon: "CheckCircle" },
  { key: "Arrived", label: "وصل", icon: "UserCheck" },
  { key: "Waiting", label: "الانتظار", icon: "Clock" },
  { key: "Called", label: "النداء", icon: "Speakerphone" },
  { key: "InRoom", label: "الغرفة", icon: "DoorOpen" },
  { key: "InProgress", label: "الطبيب", icon: "Stethoscope" },
  { key: "Completed", label: "مكتمل", icon: "Check" },
] as const;

export const STEP_ORDER = [
  "Scheduled", "Confirmed", "Arrived", "Waiting", "Called", "InRoom", "InProgress", "Completed"
] as const;

export function getStepIndex(status: string): number {
  const idx = STEP_ORDER.indexOf(status as typeof STEP_ORDER[number]);
  return idx >= 0 ? idx : -1;
}

// ─── Status Arabic Labels (Single Source of Truth) ──────────────────────
// Sprint 1 FIX: Unified Arabic labels — all other files should import from here.
// "ملغي" (not "ملغى") to match backend ClinicQueueStatusTransitions.

export const APPOINTMENT_STATUS_ARABIC: Record<string, string> = {
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
};

export const QUEUE_STATUS_ARABIC: Record<string, string> = {
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر",
};

export const CHECKOUT_STATUS_ARABIC: Record<string, string> = {
  ReadyForCheckout: "جاهز للحساب",
  CheckedOut: "تم الخروج",
  LeftWithoutCompletion: "خرج بدون إكمال",
  CancelledAfterArrival: "إلغاء بعد الوصول",
  Incomplete: "غير مكتمل",
  Abandoned: "متروك",
};

export const NEXT_ACTION_ARABIC: Record<string, string> = {
  Intake: "تسجيل الوصول",
  SendToQueue: "إضافة للانتظار",
  CallPatient: "نداء المريض",
  EnterRoom: "دخول الغرفة",
  StartVisit: "بدء الزيارة",
  InProgress: "جاري العلاج",
  Handoff: "تسليم للاستقبال",
  Checkout: "الحساب والخروج",
  None: "لا يوجد إجراء",
};

// ─── Checkout Status Type ───────────────────────────────────────────────
// Sprint 1 FIX: Type-safe checkout status values instead of raw strings.
// Matches backend Visit.CheckoutStatus (magic strings until enum migration).

export type CheckoutStatus =
  | "ReadyForCheckout"
  | "CheckedOut"
  | "LeftWithoutCompletion"
  | "CancelledAfterArrival"
  | "Incomplete"
  | "Abandoned";

/** Type guard for CheckoutStatus values */
export function isCheckoutStatus(value: string): value is CheckoutStatus {
  return [
    "ReadyForCheckout",
    "CheckedOut",
    "LeftWithoutCompletion",
    "CancelledAfterArrival",
    "Incomplete",
    "Abandoned",
  ].includes(value);
}

/** Terminal checkout statuses — no further transitions allowed */
export const TERMINAL_CHECKOUT_STATUSES: readonly CheckoutStatus[] = [
  "CheckedOut",
  "LeftWithoutCompletion",
  "CancelledAfterArrival",
  "Incomplete",
  "Abandoned",
] as const;

export function isTerminalCheckoutStatus(status: string): boolean {
  return (TERMINAL_CHECKOUT_STATUSES as readonly string[]).includes(status);
}
