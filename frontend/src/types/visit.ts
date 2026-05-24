/**
 * Shared visit types — extracted from inline definitions.
 * Aligned with backend: GET /api/visits
 */

export interface VisitDto {
  id: string;
  patientId: string;
  appointmentId?: string;
  visitDate: string;
  visitType?: string;
  specialty?: string;
  doctorId?: string;
  doctorName?: string;
  chiefComplaint?: string;
  clinicalNotes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  instructions?: string;
  nextVisitPlan?: string;
  cost?: number;
  nextVisitDate?: string;
  serviceId?: string;
  checkoutStatus?: string;
  readyForCheckoutAt?: string;
  amountDueReference?: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  appointment?: {
    appointmentDate: string;
    appointmentTime?: string;
    appointmentType?: string;
    appointmentStatus?: string;
    doctorName?: string;
  };
}

export interface CreateVisitRequest {
  patientId: string;
  appointmentId?: string;
  visitDate?: string;
  visitType?: string;
  specialty?: string;
  doctorId?: string;
  serviceId?: string;
  chiefComplaint?: string;
  clinicalNotes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  instructions?: string;
  nextVisitPlan?: string;
  cost?: number;
  nextVisitDate?: string;
}

export type UpdateVisitRequest = Partial<CreateVisitRequest>;

export const VISIT_TYPE_OPTIONS = [
  { value: "Consultation", label: "استشارة" },
  { value: "FollowUp", label: "متابعة" },
  { value: "Emergency", label: "طارئ" },
  { value: "Treatment", label: "علاج" },
  { value: "Review", label: "مراجعة" },
] as const;
