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
  medicalHistory?: MedicalHistory;
  dentalHistory?: DentalHistory;
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
