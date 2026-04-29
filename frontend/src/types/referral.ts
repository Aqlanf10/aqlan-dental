export interface Referral {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  fromDoctorId?: string;
  fromDoctorName?: string;
  toDoctorId?: string;
  toDoctorName?: string;
  toSpecialty?: string;
  reason: string;
  notes?: string;
  status: "pending" | "accepted" | "completed" | "cancelled";
  referralDate: string;
  createdAt: string;
}

export interface CreateReferralRequest {
  patientId: string;
  fromDoctorId?: string;
  toDoctorId?: string;
  toSpecialty?: string;
  reason: string;
  notes?: string;
  referralDate: string;
}

export interface ReferralFilters {
  page?: number;
  pageSize?: number;
  status?: string;
  patientId?: string;
  doctorId?: string;
  search?: string;
}
