export interface PatientListItem {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
  gender?: string;
  age?: number;
  primaryDoctorName?: string;
  branchName?: string;
  createdAt: string;
  isActive: boolean;
  lastVisitDate?: string;
}

export interface MedicalHistory {
  chronicDiseases?: string;
  currentMedications?: string;
  drugAllergies?: string;
  bleedingDisorders: boolean;
  isPregnant?: string;
  tmjProblems: boolean;
  previousSurgeries?: string;
  notes?: string;
}

export interface DentalHistory {
  chiefComplaint?: string;
  previousTreatments?: string;
  mouthBreathing: boolean;
  bruxism: boolean;
  thumbSucking: boolean;
  tongueThrusing: boolean;
  notes?: string;
}

export interface PatientProfile {
  id: string;
  patientNumber: string;
  firstName: string;
  middleName?: string;
  lastName: string;
  dateOfBirth?: string;
  gender?: string;
  age?: number;
  phone?: string;
  whatsApp?: string;
  address?: string;
  occupation?: string;
  referralSource?: string;
  primaryDoctorId?: string;
  primaryDoctorName?: string;
  branchId?: string;
  branchName?: string;
  createdAt: string;
  isActive: boolean;
  medicalHistory?: MedicalHistory;
  dentalHistory?: DentalHistory;
  portalUsername?: string;
  portalTemporaryPassword?: string;  // Only set on creation, never persisted
  /** Present only when the backend returns a clinical-only view for doctor roles. */
  isLimitedView?: boolean;
}


export interface CreatePatientRequest {
  firstName: string;
  middleName?: string;
  lastName: string;
  dateOfBirth?: string;
  gender?: string;
  phone?: string;
  whatsApp?: string;
  address?: string;
  occupation?: string;
  referralSource?: string;
  primaryDoctorId?: string;
  medicalHistory?: Partial<MedicalHistory>;
  dentalHistory?: Partial<DentalHistory>;
}
