import type { StaffUser } from "@/lib/types";

export type TodayJourneyItem = {
  appointmentId: string | null;
  appointmentDate?: string | null;
  arrivedAt?: string | null;
  queueAddedAt?: string | null;
  visitStartedAt?: string | null;
  patientId: string;
  patientName: string;
  patientPhone?: string | null;
  patientNumber?: string | null;
  appointmentTime?: string | null;
  appointmentType?: string | null;
  appointmentStatus: string;
  doctorId?: string | null;
  doctorName: string;
  serviceId?: string | null;
  serviceName?: string | null;
  roomName?: string | null;
  roomId?: string | null;
  queueItemId?: string | null;
  queueStatus?: string | null;
  visitId?: string | null;
  visitStatus?: string | null;
  proposedProcedure?: string | null;
  consultationFeeRequired?: boolean;
  consultationFeePaid?: boolean;
  paymentBeforeEntryRequired?: boolean;
  financialEntryStatus?: string | null;
  financialEntryReason?: string | null;
  canEnterWithoutPayment?: boolean;
  managerOverrideAllowed?: boolean;
  hasDraftInvoice?: boolean;
  hasLabOrder?: boolean;
  labOrderStatus?: string | null;
  hasActiveOrthoCase?: boolean;
  orthoCaseId?: string | null;
  orthoCaseNumber?: string | null;
  orthoCurrentStage?: string | null;
  orthoLastVisitDate?: string | null;
  orthoNextAppointmentDate?: string | null;
  orthoContractRemaining?: number | null;
  orthoVisitWireUpper?: string | null;
  orthoVisitWireLower?: string | null;
  orthoVisitCurrentStage?: string | null;
  amountDueReference?: number | null;
  treatmentDone?: string | null;
  chiefComplaint?: string | null;
  checkoutStatus?: string | null;
  nextAction: string;
  hasMedicalAlerts?: boolean;
  visitCount?: number;
  inRoomSince?: string | null;
};

export const JOURNEY_ACTION_LABELS: Record<string, string> = {
  Intake: "تسجيل الوصول",
  SendToQueue: "إضافة للانتظار",
  CallPatient: "نداء المريض",
  EnterRoom: "دخول الغرفة",
  StartVisit: "بدء الزيارة",
  InProgress: "جاري العلاج",
  Handoff: "تسليم للاستقبال",
  Checkout: "الحساب والخروج",
  None: "لا يوجد إجراء"
};

export const JOURNEY_STATUS_LABELS: Record<string, string> = {
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
  ReadyForCheckout: "جاهز للحساب",
  CheckedOut: "تم الخروج",
  LeftWithoutCompletion: "خرج بدون إكمال",
  Incomplete: "غير مكتمل",
  Abandoned: "متروك"
};

export function journeyStatusLabel(value?: string | null): string {
  if (!value) return "—";
  return JOURNEY_STATUS_LABELS[value] ?? value;
}

export function journeyActionLabel(value?: string | null): string {
  if (!value) return "—";
  return JOURNEY_ACTION_LABELS[value] ?? value;
}

export function canReceptionJourney(user?: StaffUser | null): boolean {
  return Boolean(user && (user.role === "Admin" || user.role === "Reception"));
}

export function canClinicalJourney(user?: StaffUser | null): boolean {
  return Boolean(
    user &&
      ["Admin", "Orthodontist", "GeneralDentist", "OralSurgeon"].includes(user.role)
  );
}

export function canFinanceJourney(user?: StaffUser | null): boolean {
  return Boolean(user && ["Admin", "Reception", "Accountant"].includes(user.role));
}
