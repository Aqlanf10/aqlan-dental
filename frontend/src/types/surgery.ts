// ── Surgery Module ──────────────────────────────────────────────────────────

// ── Entities ────────────────────────────────────────────────────────────────

export interface SurgeryCase {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId?: string;
  doctorName?: string;
  doctorColor?: string;
  surgeryType: string;
  teethInvolved?: string[];
  status: string;
  createdAt: string;
}

export interface PreopReport {
  id: string;
  surgeryDate?: string;
  surgeryLocation?: string;
  anesthesiaType?: string;
  checklist?: Record<string, boolean>;
  requiredTests?: string[];
  consentSigned: boolean;
  doctorId?: string;
  doctorName?: string;
}

export interface OperativeReport {
  id: string;
  surgeryDateTime?: string;
  durationMinutes?: number;
  anesthesiaUsed?: string;
  technique?: string;
  detailedDescription?: string;
  outcome?: string;
  complications?: string;
  suturesCount?: number;
  specimenSent: boolean;
  doctorId?: string;
  doctorName?: string;
  approvedAt?: string;
}

export interface PrescriptionItem {
  medicine: string;
  dosage: string;
  frequency: string;
  duration: string;
}

export interface FollowupItem {
  date: string;
  notes?: string;
}

export interface PostopRecord {
  id: string;
  instructions?: string;
  prescription?: PrescriptionItem[];
  followupSchedule?: FollowupItem[];
}

export interface HospitalReferral {
  id: string;
  hospitalName?: string;
  reason?: string;
  referralDate?: string;
  status: "pending" | "in_progress" | "completed" | "cancelled";
  notes?: string;
}

// ── Request Types ───────────────────────────────────────────────────────────

export interface CreateSurgeryCaseRequest {
  patientId: string;
  doctorId?: string;
  surgeryType: string;
  teethInvolved?: string[];
  scheduledDate?: string;
}

export interface UpdateSurgeryCaseRequest {
  doctorId?: string;
  surgeryType?: string;
  teethInvolved?: string[];
}

export interface UpsertPreopRequest {
  surgeryDate?: string;
  surgeryLocation?: string;
  anesthesiaType?: string;
  checklist?: Record<string, boolean>;
  requiredTests?: string[];
  consentSigned: boolean;
  doctorId?: string;
}

export interface UpsertOperativeReportRequest {
  surgeryDateTime?: string;
  durationMinutes?: number;
  anesthesiaUsed?: string;
  technique?: string;
  detailedDescription?: string;
  outcome?: string;
  complications?: string;
  suturesCount?: number;
  specimenSent: boolean;
  doctorId?: string;
}

export interface UpsertPostopRequest {
  instructions?: string;
  prescription?: PrescriptionItem[];
  followupSchedule?: FollowupItem[];
}

export interface CreateHospitalReferralRequest {
  hospitalName?: string;
  reason?: string;
  referralDate?: string;
  notes?: string;
}

export interface UpdateHospitalReferralRequest {
  hospitalName?: string;
  reason?: string;
  referralDate?: string;
  status?: "pending" | "in_progress" | "completed" | "cancelled";
  notes?: string;
}

// ── Constants ───────────────────────────────────────────────────────────────

export const SURGERY_STATUS_LABELS: Record<string, string> = {
  scheduled: "مجدولة",
  in_progress: "جارية",
  completed: "مكتملة",
  cancelled: "ملغاة",
  postponed: "مؤجلة",
};

export const SURGERY_STATUS_COLORS: Record<string, string> = {
  scheduled: "bg-blue-50 text-blue-700",
  in_progress: "bg-yellow-50 text-yellow-700",
  completed: "bg-green-50 text-green-700",
  cancelled: "bg-gray-100 text-gray-500",
  postponed: "bg-orange-50 text-orange-700",
};

export const REFERRAL_STATUS_LABELS: Record<string, string> = {
  pending: "قيد الانتظار",
  in_progress: "قيد التنفيذ",
  completed: "مكتملة",
  cancelled: "ملغاة",
};

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
  "أخرى",
] as const;
