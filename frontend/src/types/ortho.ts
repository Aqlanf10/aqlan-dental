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

  // ── المرحلة 3 — الفحص السريري المُهيكل (كلها اختيارية / إضافية) ──

  // الإطباق: تقسيم يمين/يسار + قياسات إضافية
  /** ClassI | ClassII | ClassIII */
  molarRelationRight?: string;
  molarRelationLeft?: string;
  canineRelationRight?: string;
  canineRelationLeft?: string;
  /** ClassI | ClassIIDiv1 | ClassIIDiv2 | ClassIII */
  incisorRelation?: string;
  overbitePercent?: number;
  deepBite?: boolean;
  /** Anterior | PosteriorUnilateralRight | PosteriorUnilateralLeft | PosteriorBilateral */
  crossbiteType?: string;
  scissorBite?: boolean;
  /** mm موقعة: + = انحراف يمين */
  midlineUpperShiftMm?: number;
  midlineLowerShiftMm?: number;
  upperCrowdingMm?: number;
  lowerCrowdingMm?: number;
  lowerSpacingMm?: number;
  /** Normal | Deep | Reverse */
  curveOfSpee?: string;
  /** Ovoid | Tapered | Square */
  archFormUpper?: string;
  archFormLower?: string;
  boltonDiscrepancyNote?: string;

  // إضافات خارج الفم
  /** Competent | PotentiallyCompetent | Incompetent */
  lipCompetenceGrade?: string;
  /** Normal | Acute | Obtuse */
  nasolabialAngle?: string;
  /** Normal | Prominent | Retruded */
  chinPosition?: string;
  functionalShift?: string;
  gummySmile?: boolean;

  // العادات الفموية كحقول مُهيكلة (نص habits القديم محفوظ)
  thumbSucking?: boolean;
  mouthBreathing?: boolean;
  tongueThrust?: boolean;
  lipBiting?: boolean;
  nailBiting?: boolean;
  bruxism?: boolean;

  // صحة الفم
  /** Good | Fair | Poor */
  oralHygiene?: string;
  gingivalCondition?: string;
  periodontalConcerns?: string;
  /** أرقام FDI مفصولة بفواصل، مثال: "11,21" */
  missingTeethFdi?: string;
  retainedDeciduousFdi?: string;
  impactedTeethFdi?: string;
  supernumeraryNote?: string;
  ectopicEruptionNote?: string;
  frenumNote?: string;
  tongueNote?: string;
  cariesNote?: string;
}

/** تسميات أصناف Angle (علاقة الأرحاء/الأنياب يمين ويسار) */
export const ANGLE_CLASS_LABELS: Record<string, string> = {
  ClassI: "صنف أول",
  ClassII: "صنف ثانٍ",
  ClassIII: "صنف ثالث",
};

export const INCISOR_RELATION_LABELS: Record<string, string> = {
  ClassI: "صنف أول",
  ClassIIDiv1: "صنف ثانٍ قسم 1",
  ClassIIDiv2: "صنف ثانٍ قسم 2",
  ClassIII: "صنف ثالث",
};

export const CROSSBITE_TYPE_LABELS: Record<string, string> = {
  Anterior: "أمامية",
  PosteriorUnilateralRight: "خلفية أحادية يمين",
  PosteriorUnilateralLeft: "خلفية أحادية يسار",
  PosteriorBilateral: "خلفية ثنائية",
};

export const CURVE_OF_SPEE_LABELS: Record<string, string> = {
  Normal: "طبيعي",
  Deep: "عميق",
  Reverse: "معكوس",
};

export const ARCH_FORM_LABELS: Record<string, string> = {
  Ovoid: "بيضاوي",
  Tapered: "مدبب",
  Square: "مربع",
};

export const LIP_COMPETENCE_LABELS: Record<string, string> = {
  Competent: "منطبقة",
  PotentiallyCompetent: "منطبقة بجهد",
  Incompetent: "غير منطبقة",
};

export const ORAL_HYGIENE_LABELS: Record<string, string> = {
  Good: "جيدة",
  Fair: "متوسطة",
  Poor: "ضعيفة",
};

export const NASOLABIAL_LABELS: Record<string, string> = {
  Normal: "طبيعية",
  Acute: "حادة",
  Obtuse: "منفرجة",
};

export const CHIN_POSITION_LABELS: Record<string, string> = {
  Normal: "طبيعي",
  Prominent: "بارز",
  Retruded: "متراجع",
};

/** العادات الفموية المُهيكلة (المفتاح في ClinicalExam → التسمية العربية) */
export const HABIT_LABELS: Record<string, string> = {
  thumbSucking: "مص الإبهام",
  mouthBreathing: "تنفس فموي",
  tongueThrust: "دفع اللسان",
  lipBiting: "عض الشفة",
  nailBiting: "قضم الأظافر",
  bruxism: "صرير الأسنان",
};

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
  cephSourceAnalysisId?: string;
  cephSyncedAt?: string;
  photoAnalysisSummary?: string;
  profileSourceAnalysisId?: string;
  frontalSourceAnalysisId?: string;
  photoAnalysisSyncedAt?: string;
  latestCephAnalysisId?: string;
  latestCephAnalysisDate?: string;
  isCephSyncOutdated?: boolean;
  latestProfileAnalysisId?: string;
  latestProfileAnalysisDate?: string;
  latestFrontalAnalysisId?: string;
  latestFrontalAnalysisDate?: string;
  isPhotoAnalysisSyncOutdated?: boolean;
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
  /** فئة الصورة القياسية: Extraoral | Intraoral | Radiograph | Document */
  category?: string | null;
  /** النوع الفرعي القياسي (FrontalRest, Profile, UpperOcclusal, OPG, ...) */
  subtype?: string | null;
  /** مرحلة العلاج: Initial (قبل) | Progress (أثناء) | Final (بعد) */
  treatmentPhase?: string | null;
  /** مُدرجة في تقرير الحالة */
  isSelectedForReport?: boolean;
  preparationStatus?: OrthoImagePreparationStatus;
  isPreparedForReport?: boolean;
  preparedImageUrl?: string | null;
}

export type OrthoImagePreparationStatus =
  | "OriginalUploaded"
  | "PreparedForReport"
  | "SelectedForPresentation"
  | "ApprovedForPresentation";

export interface OrthoImagePreparation {
  photoId: string;
  originalPhotoUrl: string;
  preparedImageUrl?: string | null;
  cropX: number;
  cropY: number;
  cropWidth: number;
  cropHeight: number;
  zoom: number;
  rotationDegrees: number;
  brightness: number;
  contrast: number;
  flipHorizontal: boolean;
  flipVertical: boolean;
  aspectRatio: string;
  preset?: string | null;
  status: OrthoImagePreparationStatus;
  preparedAt?: string | null;
  approvedAt?: string | null;
}

export interface SaveOrthoImagePreparationRequest {
  cropX: number;
  cropY: number;
  cropWidth: number;
  cropHeight: number;
  zoom: number;
  rotationDegrees: number;
  brightness: number;
  contrast: number;
  flipHorizontal: boolean;
  flipVertical: boolean;
  aspectRatio: string;
  preset?: string | null;
  status: Exclude<OrthoImagePreparationStatus, "OriginalUploaded">;
}

export interface PhotoAnalysisListItem {
  id: string;
  orthoCaseId: string;
  viewType: "profile" | "frontal";
  imageFileUrl: string;
  analysisDate: string;
}

export interface UpdateOrthoPhotoRequest {
  category?: string;
  subtype?: string;
  treatmentPhase?: string;
  isSelectedForReport?: boolean;
  caption?: string;
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
  photoAnalysesCount: number;
  latestCephAnalysisId?: string;
  latestCephAnalysisDate?: string;
  cephDiagnosisSyncedAt?: string;
  isCephDiagnosisOutdated?: boolean;
  latestProfileAnalysisId?: string;
  latestProfileAnalysisDate?: string;
  latestFrontalAnalysisId?: string;
  latestFrontalAnalysisDate?: string;
  photoAnalysisSyncedAt?: string;
  isPhotoAnalysisSyncOutdated?: boolean;
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

export const ORTHO_PHOTO_CATEGORY_LABELS: Record<string, string> = {
  Extraoral: "خارج الفم",
  Intraoral: "داخل الفم",
  Radiograph: "أشعة",
  Document: "مستند",
};

/** الأنواع الفرعية القياسية لكل فئة (القيمة المخزنة → التسمية العربية) */
export const ORTHO_PHOTO_SUBTYPES: Record<string, { value: string; label: string }[]> = {
  Extraoral: [
    { value: "FrontalRest", label: "أمامية راحة" },
    { value: "FrontalSmile", label: "أمامية ابتسامة" },
    { value: "Profile", label: "جانبية" },
  ],
  Intraoral: [
    { value: "Frontal", label: "أمامية" },
    { value: "Right", label: "شدق أيمن" },
    { value: "Left", label: "شدق أيسر" },
    { value: "UpperOcclusal", label: "إطباقية علوية" },
    { value: "LowerOcclusal", label: "إطباقية سفلية" },
  ],
  Radiograph: [
    { value: "OPG", label: "بانوراما" },
    { value: "LateralCeph", label: "سيفالومتري جانبي" },
    { value: "PACeph", label: "سيفالومتري خلفي أمامي" },
    { value: "CBCT", label: "CBCT" },
  ],
  Document: [],
};

export const TREATMENT_PHASE_LABELS: Record<string, string> = {
  Initial: "قبل",
  Progress: "أثناء",
  Final: "بعد",
};

/** تسمية النوع الفرعي بالعربية بغض النظر عن الفئة */
export function orthoSubtypeLabel(subtype?: string | null): string | undefined {
  if (!subtype) return undefined;
  for (const options of Object.values(ORTHO_PHOTO_SUBTYPES)) {
    const found = options.find((o) => o.value === subtype);
    if (found) return found.label;
  }
  return subtype;
}

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
