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
  patientInstructions?: string;
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
  softTissueDiagnosis?: string;
  functionalDiagnosis?: string;
  etiology?: string;
  anb?: number;
  wits?: number;
  fma?: number;
  sna?: number;
  snb?: number;
  impa?: number;
  summary?: string;
  isApproved?: boolean;
  approvedByName?: string;
  approvedAt?: string;
}

export interface TreatmentPlan {
  id?: string;
  planVersion?: number;
  planLabel?: string;
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
  proExtraction?: Record<string, boolean> | null;
  conExtraction?: Record<string, boolean> | null;
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

export interface RecordsChecklist {
  id?: string | null;
  orthoCaseId?: string;
  extraoralFrontal: boolean;
  extraoralProfile: boolean;
  extraoralSmile: boolean;
  intraoralFrontal: boolean;
  intraoralRight: boolean;
  intraoralLeft: boolean;
  upperOcclusal: boolean;
  lowerOcclusal: boolean;
  opg: boolean;
  lateralCeph: boolean;
  cbct: boolean;
  studyModels: boolean;
  consent: boolean;
  contract: boolean;
}

export interface OrthoOverview {
  hasClinicalExam: boolean;
  problemsCount: number;
  hasDiagnosis: boolean;
  isDiagnosisApproved: boolean;
  hasTreatmentPlan: boolean;
  isTreatmentPlanApproved: boolean;
  treatmentPlansCount: number;
  completedStages: number;
  totalStages: number;
  visitsCount: number;
  photosCount: number;
  cephAnalysesCount: number;
  hasRetention: boolean;
  checklistCompleted: number;
  checklistTotal: number;
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

export const RECORDS_CHECKLIST_ITEMS: { key: keyof RecordsChecklist; label: string; group: string }[] = [
  { key: "extraoralFrontal", label: "صورة خارجية أمامية", group: "صور خارجية" },
  { key: "extraoralProfile", label: "صورة خارجية جانبية", group: "صور خارجية" },
  { key: "extraoralSmile", label: "صورة الابتسامة", group: "صور خارجية" },
  { key: "intraoralFrontal", label: "صورة داخلية أمامية", group: "صور داخلية" },
  { key: "intraoralRight", label: "صورة داخلية يمين", group: "صور داخلية" },
  { key: "intraoralLeft", label: "صورة داخلية يسار", group: "صور داخلية" },
  { key: "upperOcclusal", label: "صورة إطباقية علوية", group: "صور داخلية" },
  { key: "lowerOcclusal", label: "صورة إطباقية سفلية", group: "صور داخلية" },
  { key: "opg", label: "OPG — بانوراما", group: "أشعة" },
  { key: "lateralCeph", label: "Lateral Ceph — جانبية", group: "أشعة" },
  { key: "cbct", label: "CBCT — ثلاثي الأبعاد", group: "أشعة" },
  { key: "studyModels", label: "Study Models / قوالب", group: "سجلات أخرى" },
  { key: "consent", label: "نموذج موافقة", group: "مستندات" },
  { key: "contract", label: "عقد العلاج", group: "مستندات" },
];

export const EXTRACTION_FACTORS = [
  { key: "profileFavorable", label: "البروفايل يدعم الخلع" },
  { key: "crowdingFavorable", label: "الاحتقان يدعم الخلع" },
  { key: "boltonFavorable", label: "تحليل بولتون يدعم الخلع" },
  { key: "incisorProtrusionFavorable", label: "بروز القواطع يدعم الخلع" },
  { key: "lipStrainFavorable", label: "توتر الشفاه يدعم الخلع" },
  { key: "cephFavorable", label: "السيفالومتري تدعم الخلع" },
  { key: "spaceFavorable", label: "تحليل المسافات يدعم الخلع" },
  { key: "growthFavorable", label: "نمط النمو يدعم الخلع" },
] as const;
