// Types for the shared Ortho-Surgical (orthognathic) planning workspace.
// Mirrors the Sprint A1 backend (OrthoSurgicalCasesController / OrthoSurgicalStatus).

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

export interface OrthoSurgicalCaseListItem {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  orthoCaseId: string;
  cephAnalysisId: string | null;
  surgeryCaseId: string | null;
  orthodontistName: string | null;
  surgeonName: string | null;
  status: OrthoSurgicalStatus;
  statusLabel: string;
  responsibleParty: string;
  orthodontistApprovedAt: string | null;
  surgeonApprovedAt: string | null;
  createdAt: string;
}

export interface SurgeonReviewDto {
  decision: string;
  proposedProcedure: string | null;
  requiredRecords: string | null;
  risks: string | null;
  notes: string | null;
  reviewedAt: string | null;
}

export interface JointPlanDto {
  procedureType: string | null;
  timing: string | null;
  orthodonticObjectives: string | null;
  surgicalObjectives: string | null;
  preSurgicalRequirements: string | null;
  postSurgicalPlan: string | null;
  risks: string | null;
  patientExplanation: string | null;
  lockedAt: string | null;
}

export interface OrthoSurgicalCaseDetail {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  orthoCaseId: string;
  cephAnalysisId: string | null;
  surgeryCaseId: string | null;
  orthodontistId: string | null;
  orthodontistName: string | null;
  surgeonId: string | null;
  surgeonName: string | null;
  status: OrthoSurgicalStatus;
  statusLabel: string;
  responsibleParty: string;
  allowedTransitions: OrthoSurgicalStatus[];
  diagnosisSummary: string | null;
  orthodontistApprovedAt: string | null;
  surgeonApprovedAt: string | null;
  surgeonReview: SurgeonReviewDto | null;
  jointPlan: JointPlanDto | null;
  createdAt: string;
}

export const ORTHO_SURGICAL_STATUS_LABELS: Record<OrthoSurgicalStatus, string> = {
  DraftByOrthodontist: "مسودة لدى التقويم",
  RecordsIncomplete: "السجلات ناقصة",
  CephReady: "السيفالو جاهز",
  VtoDraft: "مسودة المحاكاة (VTO)",
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
  Cancelled: "ملغاة",
};

export const ORTHO_SURGICAL_STATUS_COLORS: Record<OrthoSurgicalStatus, string> = {
  DraftByOrthodontist: "bg-slate-100 text-slate-700",
  RecordsIncomplete: "bg-amber-50 text-amber-700",
  CephReady: "bg-sky-50 text-sky-700",
  VtoDraft: "bg-indigo-50 text-indigo-700",
  SentToSurgeon: "bg-blue-50 text-blue-700",
  SurgeonReviewPending: "bg-blue-50 text-blue-700",
  SurgeonRequestedChanges: "bg-orange-50 text-orange-700",
  JointPlanApproved: "bg-teal-50 text-teal-700",
  ReadyForSurgery: "bg-emerald-50 text-emerald-700",
  SurgeryScheduled: "bg-emerald-50 text-emerald-700",
  SurgeryDone: "bg-green-50 text-green-700",
  PostOpOrthodontics: "bg-cyan-50 text-cyan-700",
  Completed: "bg-green-100 text-green-800",
  NotSurgicalCandidate: "bg-gray-100 text-gray-500",
  Cancelled: "bg-gray-100 text-gray-500",
};

// The 9 macro-stages shown in the vertical workflow timeline (Records → Completion).
export const ORTHO_SURGICAL_TIMELINE: { key: string; label: string; statuses: OrthoSurgicalStatus[] }[] = [
  { key: "records", label: "السجلات", statuses: ["DraftByOrthodontist", "RecordsIncomplete"] },
  { key: "ceph", label: "السيفالو", statuses: ["CephReady", "VtoDraft"] },
  { key: "surgeon", label: "مراجعة الجراح", statuses: ["SentToSurgeon", "SurgeonReviewPending", "SurgeonRequestedChanges"] },
  { key: "joint", label: "الخطة المشتركة", statuses: ["JointPlanApproved"] },
  { key: "ready", label: "جاهزة للجراحة", statuses: ["ReadyForSurgery"] },
  { key: "surgery", label: "الجراحة", statuses: ["SurgeryScheduled", "SurgeryDone"] },
  { key: "postop", label: "ما بعد الجراحة", statuses: ["PostOpOrthodontics"] },
  { key: "done", label: "مكتملة", statuses: ["Completed"] },
];

export const SURGEON_REVIEW_DECISIONS: { value: string; label: string }[] = [
  { value: "Approved", label: "موافق" },
  { value: "RequestChanges", label: "يحتاج تعديلًا" },
  { value: "NotCandidate", label: "غير مرشّح للجراحة" },
  { value: "NeedsImaging", label: "يحتاج صورًا إضافية" },
];
