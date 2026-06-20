/**
 * Shared Journey Types — Single Source of Truth.
 *
 * Re-exports canonical types from @/types/journey.ts
 * and defines unified item types used by both /patient-journey
 * and /daily-operations pages.
 *
 * TodayJourneyItem is the superset type — used by daily-operations.
 * Patient-journey page imports TodayJourneyItem and uses only the
 * fields it needs (no separate JourneyItem type needed).
 */

// ─── Re-export all types from SSOT ────────────────────────────────────────
export type {
  DailyJourneySummary,
  DailyJourneyPatient,
  DailyJourneyAppointment,
  DailyJourneyQueue,
  DailyJourneyVisit,
  DailyJourneyFinance,
  DailyJourneyContract,
  DailyJourneyOrtho,
  MedicalAlert,
  DailyJourneyRecentVisit,
  TimelineEvent,
  CheckoutStatus,
} from "@/types/journey";

// ─── TodayJourneyItem — canonical journey item type ───────────────────────
/**
 * The unified journey item type returned by GET /api/patient-journey/today.
 * This is the superset — daily-operations uses all fields,
 * patient-journey uses a subset.
 *
 * Previously, patient-journey defined a smaller JourneyItem type,
 * but since the API returns TodayJourneyItem, we use the superset
 * everywhere for consistency.
 */
export interface TodayJourneyItem {
  appointmentId: string | null; // null for walk-in patients without an appointment
  patientId: string;
  patientName: string;
  patientPhone?: string;
  patientNumber?: string;
  appointmentTime?: string;
  appointmentType?: string;
  appointmentStatus: string;
  doctorId?: string;
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
  hasActiveOrthoCase?: boolean;
  orthoCaseId?: string;
  orthoCaseNumber?: string;
  orthoCurrentStage?: string;
  orthoLastVisitDate?: string;
  orthoNextAppointmentDate?: string;
  orthoContractRemaining?: number;
  // CLIN-05: ortho bridge fields mirrored from the linked Visit
  // (set by OrthoService.AddVisitAsync). Null for non-ortho visits.
  orthoVisitWireUpper?: string | null;
  orthoVisitWireLower?: string | null;
  orthoVisitCurrentStage?: string | null;
  amountDueReference?: number;
  treatmentDone?: string;
  chiefComplaint?: string;
  checkoutStatus?: string;
  nextAction: string;
  hasMedicalAlerts?: boolean;
  visitCount?: number;
  inRoomSince?: string; // ISO timestamp when patient entered room
}

/**
 * @deprecated Use TodayJourneyItem instead.
 * Kept for backward compatibility during migration.
 */
export type JourneyItem = TodayJourneyItem;

// ─── Option Types ─────────────────────────────────────────────────────────

export interface ServiceOption {
  id: string;
  arabicName: string;
  code?: string;
  defaultPrice?: number;
  requiresConsultationFee?: boolean;
}

export interface RoomOption {
  id: string;
  arabicName: string;
  code?: string;
  roomType?: string;
}

export interface DoctorOption {
  id: string;
  name: string;
  specialty?: string;
}

export interface BranchOption {
  id: string;
  name: string;
}
