export type OrthoSurgicalStatus =
  | "DraftByOrthodontist"
  | "RecordsIncomplete"
  | "CephReady"
  | "VtoDraft"
  | "SentToSurgeon"
  | "SurgeonReviewPending"
  | "SurgeonRequestedChanges"
  | "JointPlanApproved"
  | "ReadyForSurgery"
  | "SurgeryScheduled"
  | "SurgeryDone"
  | "PostOpOrthodontics"
  | "Completed"
  | "NotSurgicalCandidate"
  | "Cancelled";

export type OrthoSurgicalCaseListItem = {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  orthoCaseId: string;
  cephAnalysisId?: string | null;
  surgeryCaseId?: string | null;
  orthodontistName?: string | null;
  surgeonName?: string | null;
  status: OrthoSurgicalStatus;
  statusLabel: string;
  responsibleParty: string;
  orthodontistApprovedAt?: string | null;
  surgeonApprovedAt?: string | null;
  createdAt: string;
};

export type SurgeonReview = {
  decision: string;
  proposedProcedure?: string | null;
  requiredRecords?: string | null;
  risks?: string | null;
  notes?: string | null;
  reviewedAt?: string | null;
};

export type JointPlan = {
  procedureType?: string | null;
  timing?: string | null;
  orthodonticObjectives?: string | null;
  surgicalObjectives?: string | null;
  preSurgicalRequirements?: string | null;
  postSurgicalPlan?: string | null;
  risks?: string | null;
  patientExplanation?: string | null;
  lockedAt?: string | null;
};

export type OrthoSurgicalCaseDetail = OrthoSurgicalCaseListItem & {
  orthodontistId?: string | null;
  surgeonId?: string | null;
  allowedTransitions: OrthoSurgicalStatus[];
  diagnosisSummary?: string | null;
  surgeonReview?: SurgeonReview | null;
  jointPlan?: JointPlan | null;
};

export type OrthoSurgicalReadiness = {
  orthoSurgicalCaseId: string;
  recordsReady: boolean;
  cephReady: boolean;
  diagnosisReady: boolean;
  surgeonReviewReady: boolean;
  missing: string[];
  checklist?: Record<string, boolean> | null;
  diagnosis?: {
    skeletalClassification?: string | null;
    dentalClassification?: string | null;
    facialPattern?: string | null;
    summary?: string | null;
    approvedAt?: string | null;
  } | null;
  ceph?: { id: string; isApproved: boolean; analysisDate: string } | null;
};

export type OrthoSurgicalComment = {
  id: string;
  authorUserId?: string | null;
  authorRole?: string | null;
  body: string;
  createdAt: string;
};

export type OrthoSurgicalVto = {
  id: string;
  orthoSurgicalCaseId: string;
  cephAnalysisId?: string | null;
  maxillaMoveMm?: number | null;
  mandibleMoveMm?: number | null;
  chinMoveMm?: number | null;
  rotationDegree?: number | null;
  predictedSNA?: number | null;
  predictedSNB?: number | null;
  predictedANB?: number | null;
  predictedWits?: number | null;
  predictedOverjet?: number | null;
  notes?: string | null;
  createdBy?: string | null;
  isApprovedByOrthodontist: boolean;
  approvedAt?: string | null;
  createdAt: string;
  disclaimer: string;
};

export type OrthoSurgicalVtoList = {
  data: OrthoSurgicalVto[];
  disclaimer: string;
};

export type SurgeryExecutionSummary = {
  linked: boolean;
  id?: string;
  caseNumber?: string;
  surgeryType?: string;
  status?: string;
  doctorName?: string | null;
  preop?: { surgeryDate?: string | null; consentSigned: boolean } | null;
  operative?: { surgeryDateTime?: string | null; outcome?: string | null; approvedAt?: string | null } | null;
  postop?: { hasInstructions: boolean } | null;
};

export const ORTHO_SURGICAL_STATUS_LABELS: Record<OrthoSurgicalStatus, string> = {
  DraftByOrthodontist: "مسودة لدى التقويم",
  RecordsIncomplete: "السجلات ناقصة",
  CephReady: "السيفالو جاهز",
  VtoDraft: "مسودة المحاكاة VTO",
  SentToSurgeon: "أُرسلت للجراح",
  SurgeonReviewPending: "قيد مراجعة الجراح",
  SurgeonRequestedChanges: "الجراح طلب تعديلًا",
  JointPlanApproved: "الخطة المشتركة معتمدة",
  ReadyForSurgery: "جاهزة للجراحة",
  SurgeryScheduled: "الجراحة مجدولة",
  SurgeryDone: "تمت الجراحة",
  PostOpOrthodontics: "تقويم ما بعد الجراحة",
  Completed: "مكتملة",
  NotSurgicalCandidate: "غير مرشّحة للجراحة",
  Cancelled: "ملغاة"
};

export const SURGEON_REVIEW_DECISIONS = [
  { value: "Approved", label: "موافق" },
  { value: "RequestChanges", label: "يحتاج تعديلًا" },
  { value: "NotCandidate", label: "غير مرشح للجراحة" },
  { value: "NeedsImaging", label: "يحتاج صورًا إضافية" }
] as const;

export const VTO_DISCLAIMER_AR = "هذه محاكاة تخطيطية تقريبية ولا تُعد قرارًا جراحيًا نهائيًا.";

export function orthoSurgicalStatusLabel(status?: string | null): string {
  if (!status) return "—";
  return ORTHO_SURGICAL_STATUS_LABELS[status as OrthoSurgicalStatus] ?? status;
}

export function canViewOrthognathic(can: (permission: string) => boolean): boolean {
  return can("ortho_surgical.view");
}
