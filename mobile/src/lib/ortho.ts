export type TreatmentStage = {
  id: string;
  stageName: string;
  stageOrder: number;
  startedAt?: string | null;
  completedAt?: string | null;
  targetDurationMonths?: number | null;
  status: string;
  notes?: string | null;
};

export type OrthoVisit = {
  id: string;
  visitNumber: number;
  visitDate: string;
  visitType?: string | null;
  currentStage?: string | null;
  wireUpper?: string | null;
  wireLower?: string | null;
  elasticsType?: string | null;
  currentOverjet?: number | null;
  currentOverbite?: number | null;
  clinicalNotes?: string | null;
  patientInstructions?: string | null;
  nextAppointmentDate?: string | null;
  nextAppointmentType?: string | null;
  doctorName?: string | null;
};

export type OrthoCase = {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId?: string | null;
  doctorName?: string | null;
  doctorColor?: string | null;
  applianceType?: string | null;
  startDate?: string | null;
  expectedDurationMonths?: number | null;
  currentStage?: string | null;
  stagePercentage: number;
  status: string;
  totalFee?: number | null;
  extractionDecisionValue?: string | null;
  retentionPlan?: string | null;
  stages?: TreatmentStage[];
  recentVisits?: OrthoVisit[];
};

export type CreateOrthoVisitInput = {
  visitDate: string;
  visitType?: string | null;
  currentStage?: string | null;
  wireUpper?: string | null;
  wireLower?: string | null;
  elasticsType?: string | null;
  currentOverjet?: number | null;
  currentOverbite?: number | null;
  clinicalNotes?: string | null;
  patientInstructions?: string | null;
  nextAppointmentDate?: string | null;
  nextAppointmentType?: string | null;
  doctorId?: string | null;
};

export const ORTHO_STATUS_LABELS: Record<string, string> = {
  Active: "نشطة",
  active: "نشطة",
  Completed: "مكتملة",
  completed: "مكتملة",
  OnHold: "موقوفة مؤقتاً",
  on_hold: "موقوفة مؤقتاً",
  Cancelled: "ملغاة",
  cancelled: "ملغاة"
};

export const STAGE_STATUS_LABELS: Record<string, string> = {
  active: "نشطة",
  pending: "قادمة",
  completed: "مكتملة"
};

export function canUseOrthodontics(role?: string | null): boolean {
  return role === "Admin" || role === "Orthodontist";
}
