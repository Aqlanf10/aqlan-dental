export type PatientDocument = {
  id: string;
  patientId: string;
  documentType?: string | null;
  title?: string | null;
  fileUrl?: string | null;
  fileName?: string | null;
  fileSize?: number | null;
  mimeType?: string | null;
  notes?: string | null;
  signed: boolean;
  signedAt?: string | null;
  orthoCaseId?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
};

export type PatientDocumentList = {
  data: PatientDocument[];
  total: number;
  page: number;
  pageSize: number;
};

export type DrugItem = {
  name: string;
  dose: string;
  frequency: string;
  duration: string;
  notes?: string | null;
};

export type PrescriptionListItem = {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string | null;
  diagnosis?: string | null;
  drugCount: number;
  notes?: string | null;
  createdAt: string;
};

export type PrescriptionDetail = {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string | null;
  diagnosis?: string | null;
  drugs: DrugItem[];
  notes?: string | null;
  createdAt: string;
};

export type PrescriptionList = {
  data: PrescriptionListItem[];
  total: number;
  page: number;
  pageSize: number;
};

export type InternalReferral = {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  fromDoctorName: string;
  fromDoctorColor?: string | null;
  toDoctorName: string;
  toDoctorColor?: string | null;
  reason?: string | null;
  priority?: string | null;
  notes?: string | null;
  status: string;
  createdAt: string;
  acceptedAt?: string | null;
};

export type ReferralList = {
  data: InternalReferral[];
  total: number;
  page: number;
  pageSize: number;
};

export const REFERRAL_STATUS_LABELS: Record<string, string> = {
  pending: "معلّقة",
  accepted: "مقبولة",
  completed: "مكتملة"
};

export const REFERRAL_PRIORITY_OPTIONS = [
  { value: "urgent", label: "عاجل" },
  { value: "normal", label: "عادي" },
  { value: "low", label: "منخفض" }
] as const;

export const REFERRAL_PRIORITY_LABELS: Record<string, string> = Object.fromEntries(
  REFERRAL_PRIORITY_OPTIONS.map((item) => [item.value, item.label])
);

export const DOCUMENT_TYPE_OPTIONS = [
  { value: "consent", label: "موافقة علاج" },
  { value: "medical-report", label: "تقرير طبي" },
  { value: "lab-report", label: "تقرير معمل" },
  { value: "insurance", label: "تأمين" },
  { value: "external-report", label: "تقرير خارجي" },
  { value: "other", label: "أخرى" }
] as const;

export const DOCUMENT_TYPE_LABELS: Record<string, string> = Object.fromEntries(
  DOCUMENT_TYPE_OPTIONS.map((item) => [item.value, item.label])
);
