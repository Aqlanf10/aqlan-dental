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
  teethInvolved?: string;
  status: string;
  createdAt: string;
}

export interface CreateSurgeryCaseRequest {
  patientId: string;
  surgeryType: string;
  teethInvolved?: string;
  doctorId?: string;
  notes?: string;
}

export interface PreopReport {
  surgeryDate?: string;
  surgeryLocation?: string;
  anesthesiaType?: string;
  consentSigned?: boolean;
  doctorId?: string;
  doctorName?: string;
}

export interface OperativeReport {
  id?: string;
  surgeryDate?: string;
  surgeryTime?: string;
  durationMinutes?: number;
  anesthesiaUsed?: string;
  technique?: string;
  description?: string;
  outcome?: string;
  complications?: string;
  suturesCount?: number;
  specimenSent?: boolean;
  isApproved?: boolean;
  approvedByName?: string;
  approvedAt?: string;
}

export const OPERATIVE_TECHNIQUES = [
  { value: "extraction", label: "قلع" },
  { value: "impaction", label: "ضرس منغرس" },
  { value: "cyst", label: "كيسة" },
  { value: "implant", label: "زراعة" },
  { value: "fracture", label: "كسر" },
  { value: "bone_graft", label: "ترقيع عظمي" },
  { value: "other", label: "أخرى" },
] as const;

export const OPERATIVE_OUTCOMES = [
  { value: "successful", label: "ناجحة" },
  { value: "complicated", label: "معقدة" },
  { value: "partial", label: "جزئية" },
] as const;

export interface PostopRecord {
  instructions?: string;
  medications?: string;
  followUpDate?: string;
  dietRestrictions?: string;
  activityRestrictions?: string;
}

export interface HospitalReferral {
  id: string;
  surgeryCaseId: string;
  hospitalName: string;
  reason: string;
  referralDate: string;
  notes?: string;
  status: "pending" | "accepted" | "completed";
  createdAt: string;
}

export interface CreateHospitalReferralRequest {
  surgeryCaseId: string;
  hospitalName: string;
  reason: string;
  referralDate: string;
  notes?: string;
}

export interface SurgeryCaseFilters {
  page?: number;
  pageSize?: number;
  doctorId?: string;
  status?: string;
  search?: string;
  patientId?: string;
}
