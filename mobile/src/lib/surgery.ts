export const SURGERY_TYPES = [
  "قلع بسيط",
  "قلع جراحي",
  "قلع ضرس العقل",
  "زراعة أسنان",
  "كسر الفك",
  "خراج حاد",
  "كيس فكي",
  "جراحة اللثة",
  "جراحة الأعصاب",
  "ترقيع عظمي",
  "رفع الجيب الفكي",
  "أخرى"
] as const;

export const SURGERY_STATUS_LABELS: Record<string, string> = {
  scheduled: "مقررة",
  in_progress: "قيد التنفيذ",
  completed: "مكتملة",
  cancelled: "ملغاة",
  postponed: "مؤجلة"
};

export const REFERRAL_STATUS_LABELS: Record<string, string> = {
  pending: "قيد الانتظار",
  in_progress: "قيد التنفيذ",
  completed: "مكتملة",
  cancelled: "ملغاة"
};

export const PREOP_CHECKLIST_ITEMS = [
  { key: "blood_test", label: "فحص الدم" },
  { key: "panoramic_xray", label: "صورة بانوراما" },
  { key: "radiograph", label: "تصوير أشعة" },
  { key: "anesthesia_eval", label: "تقييم التخدير" },
  { key: "allergy_review", label: "مراجعة الحساسية" },
  { key: "fasting", label: "صيام" },
  { key: "stop_blood_thin", label: "إيقاف مميعات الدم" }
] as const;

export type SurgeryCase = {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId?: string | null;
  doctorName?: string | null;
  doctorColor?: string | null;
  surgeryType: string;
  teethInvolved?: string | null;
  status: string;
  createdAt: string;
};

export type SurgeryCaseListResponse = {
  data: SurgeryCase[];
  total: number;
  page: number;
  pageSize: number;
};

export type CreateSurgeryCaseInput = {
  patientId: string;
  doctorId?: string | null;
  surgeryType: string;
  teethInvolved?: string | null;
};

export type PreopReport = {
  id: string;
  surgeryDate?: string | null;
  surgeryLocation?: string | null;
  anesthesiaType?: string | null;
  checklist?: Record<string, boolean> | null;
  requiredTests?: string[] | null;
  consentSigned: boolean;
  doctorId?: string | null;
  doctorName?: string | null;
};

export type UpsertPreopInput = {
  surgeryDate?: string | null;
  surgeryLocation?: string | null;
  anesthesiaType?: string | null;
  consentSigned: boolean;
  doctorId?: string | null;
  checklist?: Record<string, boolean> | null;
  requiredTests?: string[] | null;
};

export type OperativeReport = {
  id: string;
  surgeryDateTime?: string | null;
  durationMinutes?: number | null;
  anesthesiaUsed?: string | null;
  technique?: string | null;
  detailedDescription?: string | null;
  outcome?: string | null;
  complications?: string | null;
  suturesCount?: number | null;
  specimenSent: boolean;
  doctorId?: string | null;
  doctorName?: string | null;
  approvedAt?: string | null;
};

export type UpsertOperativeInput = {
  surgeryDateTime?: string | null;
  durationMinutes?: number | null;
  anesthesiaUsed?: string | null;
  technique?: string | null;
  detailedDescription?: string | null;
  outcome?: string | null;
  complications?: string | null;
  suturesCount?: number | null;
  specimenSent: boolean;
  doctorId?: string | null;
};

export type SurgeryPrescriptionItem = {
  medicine: string;
  dosage: string;
  frequency: string;
  duration: string;
};

export type SurgeryFollowupItem = {
  date: string;
  notes?: string | null;
};

export type PostopRecord = {
  id: string;
  instructions?: string | null;
  prescription?: SurgeryPrescriptionItem[] | null;
  followupSchedule?: SurgeryFollowupItem[] | null;
};

export type UpsertPostopInput = {
  instructions?: string | null;
  prescription?: SurgeryPrescriptionItem[] | null;
  followupSchedule?: SurgeryFollowupItem[] | null;
};

export type HospitalReferral = {
  id: string;
  hospitalName?: string | null;
  reason?: string | null;
  referralDate?: string | null;
  status: string;
  notes?: string | null;
  createdAt?: string | null;
};

export function canUseSurgery(role?: string | null): boolean {
  return role === "Admin" || role === "OralSurgeon";
}

export function normalizeSurgeryStatus(value?: string | null): string {
  if (!value) return "";
  const normalized = value
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/[\s-]+/g, "_")
    .toLowerCase();
  return normalized;
}

export function allowedSurgeryTransitions(value?: string | null): string[] {
  switch (normalizeSurgeryStatus(value)) {
    case "scheduled":
      return ["in_progress", "postponed", "cancelled"];
    case "in_progress":
      return ["completed", "cancelled"];
    case "postponed":
      return ["scheduled", "cancelled"];
    default:
      return [];
  }
}
