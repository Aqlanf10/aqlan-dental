export type DailyPatient = {
  appointmentId: string | null;
  appointmentDate: string | null;
  arrivedAt: string | null;
  queueAddedAt: string | null;
  visitStartedAt: string | null;
  patientId: string;
  patientName: string;
  patientPhone: string | null;
  patientNumber: string | null;
  appointmentTime: string | null;
  appointmentType: string | null;
  appointmentStatus: string;
  doctorId: string | null;
  doctorName: string;
  serviceId: string | null;
  serviceName: string | null;
  roomName: string | null;
  roomId: string | null;
  queueItemId: string | null;
  queueStatus: string | null;
  visitId: string | null;
  visitStatus: string | null;
  checkoutStatus: string | null;
  amountDueReference: number | null;
  treatmentDone: string | null;
  proposedProcedure: string | null;
  hasDraftInvoice: boolean;
  consultationFeeRequired: boolean;
  consultationFeePaid: boolean;
  paymentBeforeEntryRequired: boolean;
  financialEntryStatus: string | null;
  financialEntryReason: string | null;
  canEnterWithoutPayment: boolean;
  managerOverrideAllowed: boolean;
  hasMedicalAlerts: boolean;
  visitCount: number | null;
  nextAction: string;
};

export type ClinicRoom = {
  id: string;
  arabicName: string;
};

export type DailyOperationAction =
  | 'intake'
  | 'send-to-queue'
  | 'call'
  | 'recall'
  | 'enter-room'
  | 'start-visit'
  | 'handoff'
  | 'create-draft-invoice';

export type DailyOperationInput = {
  roomId?: string;
  roomName?: string;
  notes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  proposedProcedure?: string;
  amountDue?: number;
};
