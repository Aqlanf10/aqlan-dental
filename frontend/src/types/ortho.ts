export interface TreatmentStage {
  id: string;
  stageName: string;
  stageOrder: number;
  startedAt?: string;
  completedAt?: string;
  targetDurationMonths?: number;
  status: "pending" | "active" | "completed";
  notes?: string;
}

export interface OrthoVisit {
  id: string;
  visitNumber: number;
  visitDate: string;
  visitType?: string;
  currentStage?: string;
  wireUpper?: string;
  wireLower?: string;
  elasticsType?: string;
  currentOverjet?: number;
  currentOverbite?: number;
  clinicalNotes?: string;
  nextAppointmentDate?: string;
  nextAppointmentType?: string;
  doctorName?: string;
}

export interface OrthoCase {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId?: string;
  doctorName?: string;
  doctorColor?: string;
  applianceType?: string;
  startDate?: string;
  expectedDurationMonths?: number;
  currentStage?: string;
  stagePercentage: number;
  status: string;
  totalFee?: number;
  extractionDecisionValue?: string;
  retentionPlan?: string;
  stages?: TreatmentStage[];
  recentVisits?: OrthoVisit[];
}

export interface CreateOrthoCaseRequest {
  patientId: string;
  doctorId?: string;
  applianceType?: string;
  startDate?: string;
  expectedDurationMonths?: number;
  totalFee?: number;
  notes?: string;
}

export interface CreateOrthoVisitRequest {
  visitDate: string;
  visitType?: string;
  currentStage?: string;
  wireUpper?: string;
  wireLower?: string;
  elasticsType?: string;
  currentOverjet?: number;
  currentOverbite?: number;
  clinicalNotes?: string;
  patientInstructions?: string;
  nextAppointmentDate?: string;
  nextAppointmentType?: string;
  doctorId?: string;
}
