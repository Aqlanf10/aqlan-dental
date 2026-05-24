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

export interface ClinicalExam {
  id?: string;
  examDate?: string;
  facialSymmetry?: string;
  profile?: string;
  lipsCompetence?: boolean;
  smileLine?: string;
  verticalProportion?: string;
  molarRelation?: string;
  canineRelation?: string;
  overjet?: number;
  overbite?: number;
  crossbite?: boolean;
  openBite?: boolean;
  upperCrowding?: string;
  lowerCrowding?: string;
  upperSpacing?: number;
  midlineUpper?: string;
  midlineLower?: string;
  coCrDiscrepancy?: boolean;
  tmjFindings?: string;
  habits?: string;
  notes?: string;
  doctorId?: string;
}

export interface ProblemListItem {
  id: string;
  category: string;
  description: string;
  severity?: string;
  sortOrder?: number;
}

export interface OrthoDiagnosis {
  id?: string | null;
  skeletalClassification?: string;
  dentalClassification?: string;
  facialPattern?: string;
  anb?: number;
  wits?: number;
  fma?: number;
  sna?: number;
  snb?: number;
  impa?: number;
  summary?: string;
}

export interface TreatmentPlan {
  id?: string;
  planVersion?: number;
  isApproved?: boolean;
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
  approvedByName?: string;
  approvedAt?: string;
}

export interface ExtractionDecision {
  id?: string;
  decision?: string;
  doctorNotes?: string;
  aiRecommendation?: string;
  decidedByName?: string;
  decidedAt?: string;
}

export interface RetentionRecord {
  id?: string;
  debondDate?: string;
  upperRetainer?: string;
  lowerRetainer?: string;
  instructions?: string;
  status?: string;
  visits?: RetentionVisit[];
}

export interface RetentionVisit {
  id: string;
  visitDate?: string;
  period?: string;
  toothStability?: string;
  retainerStatus?: string;
  notes?: string;
}

export interface OrthoPhoto {
  id: string;
  photoUrl: string;
  photoType: string;
  caption?: string;
  takenAt?: string;
  sortOrder?: number;
}

export interface OrthoOverview {
  hasClinicalExam: boolean;
  problemsCount: number;
  hasDiagnosis: boolean;
  hasTreatmentPlan: boolean;
  isTreatmentPlanApproved: boolean;
  completedStages: number;
  totalStages: number;
  visitsCount: number;
  photosCount: number;
  cephAnalysesCount: number;
  hasRetention: boolean;
  contractId?: string;
  contractTotal?: number;
  contractPaid?: number;
  contractRemaining?: number;
  latestVisitDate?: string;
  nextAppointmentDate?: string;
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

export const ORTHO_STAGE_LABELS: Record<string, string> = {
  Records: "السجلات",
  Diagnosis: "التشخيص",
  TreatmentPlan: "خطة العلاج",
  Bonding: "تركيب الجهاز",
  Alignment: "المحاذاة",
  Leveling: "التسوية",
  SpaceGaining: "فتح المسافات",
  SpaceClosure: "إغلاق المسافات",
  Finishing: "التشطيب",
  Debonding: "فك الجهاز",
  Retention: "الاحتفاظ",
};

export const ORTHO_STATUS_LABELS: Record<string, string> = {
  Active: "نشطة",
  active: "نشطة",
  OnHold: "متوقفة",
  on_hold: "متوقفة",
  Completed: "مكتملة",
  completed: "مكتملة",
  Cancelled: "ملغاة",
  cancelled: "ملغاة",
};
