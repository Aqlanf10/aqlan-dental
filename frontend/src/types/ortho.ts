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

// ─── Model Analysis ──────────────────────────────────────────────────────────
export interface ModelAnalysisDto {
  id: string;
  orthoCaseId: string;
  analysisDate?: string;
  boltonOverall?: number;
  boltonAnterior?: number;
  upperSum12?: number;
  lowerSum12?: number;
  upperArchLength?: number;
  lowerArchLength?: number;
  upperAld?: number;
  lowerAld?: number;
  pontIndex?: number;
  notes?: string;
}

export interface BoltonResult {
  overallRatio: number;
  normalOverall: number;
  overallDiscrepancy: number;
  anteriorRatio?: number;
  normalAnterior: number;
  anteriorDiscrepancy?: number;
  interpretationAr: string;
}

export interface BoltonCalculationRequest {
  upperWidths: number[];
  lowerWidths: number[];
  upperAnteriorWidths: number[];
  lowerAnteriorWidths: number[];
}

// ─── Treatment Plan A/B ─────────────────────────────────────────────────────
export interface TreatmentPlanDto {
  id: string;
  orthoCaseId: string;
  planLabel: string;
  isSelected: boolean;
  applianceType?: string;
  bracketSystem?: string;
  initialWire?: string;
  extractionPlan?: string;
  anchoragePlan?: string;
  useTads?: boolean;
  useElastics?: boolean;
  expectedDurationMonths?: number;
  retentionPlan?: string;
  treatmentGoals?: string;
  risksLimitations?: string;
  isApproved?: boolean;
  approvedByName?: string;
  approvedAt?: string;
}

export interface TreatmentPlanListDto {
  plans: TreatmentPlanDto[];
  selectedPlanId?: string;
}

// ─── Clinical Photo ─────────────────────────────────────────────────────────
export interface ClinicalPhoto {
  id: string;
  patientId: string;
  orthoCaseId?: string;
  category: string;
  photoType: string;
  fileUrl: string;
  stage?: string;
  photoDate?: string;
  notes?: string;
}
