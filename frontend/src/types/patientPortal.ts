// Auth
export interface PatientLoginRequest {
  username: string;
  password: string;
}

export interface PatientForgotPasswordRequest {
  phoneNumber: string;
}

export interface PatientResetPasswordRequest {
  phoneNumber: string;
  code: string;
  newPassword: string;
}

export interface PatientAuthResponse {
  accessToken: string;
  profile: PatientPortalProfile;
  mustChangePassword: boolean;
}

// Profile — extended with more fields
export interface PatientPortalProfile {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
  whatsapp?: string;
  gender?: string;
  age?: number;
  dateOfBirth?: string;
  address?: string;
  primaryDoctorName?: string;
  accountStatus: string;
  lastLogin?: string;
  username?: string;
}

export interface PatientProfileUpdate {
  phone?: string;
  whatsapp?: string;
  address?: string;
}

// Credentials — for staff to view patient portal account info (no password)
export interface PatientPortalCredentials {
  username: string;
  accountActive: boolean;
  mustChangePassword: boolean;
  lastLogin?: string;
  hasPortalAccount: boolean;
}

export interface PatientPasswordResetResponse {
  temporaryPassword: string;
  username: string;
  message: string;
}

// Appointments
export interface PatientAppointment {
  id: string;
  appointmentDate: string;
  startTime: string;
  endTime: string;
  appointmentType: string;
  doctorName: string;
  status: string;
  notes?: string;
  canCancel: boolean;
}

export interface PatientAppointmentRequest {
  appointmentDate: string;
  startTime: string;
  appointmentType: string;
  doctorId?: string;
  notes?: string;
}

// Treatments
export interface PatientTreatment {
  id: string;
  treatmentType: string;
  toothNumber?: string;
  materialUsed?: string;
  doctorName?: string;
  visitDate?: string;
  specialty?: string;
  createdAt: string;
  notes?: string;
}

// Visits — safe patient-facing view
export interface PatientVisit {
  id: string;
  visitDate: string;
  visitType?: string;
  specialty?: string;
  doctorName?: string;
  chiefComplaint?: string;
  treatmentDone?: string;
  instructions?: string;
  nextVisitPlan?: string;
  nextVisitDate?: string;
  treatmentCount: number;
}

// Prescriptions — with proper drug details
export interface PatientPrescription {
  id: string;
  diagnosis?: string;
  drugs: PrescriptionDrug[];
  instructions?: string;
  doctorName: string;
  createdAt: string;
}

export interface PrescriptionDrug {
  name: string;
  dosage?: string;
  frequency?: string;
  duration?: string;
  notes?: string;
}

// Payments
export interface PatientPayment {
  id: string;
  amount: number;
  paymentMethod: string;
  serviceDescription?: string;
  receiptNumber?: string;
  createdAt: string;
}

// Contracts
export interface PatientContract {
  id: string;
  specialty?: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  status: string;
  startDate?: string;
}

export interface PatientFinancialSummary {
  totalAmount: number;
  totalPaid: number;
  totalOutstanding: number;
  activeContracts: number;
  contracts: PatientContract[];
  recentPayments: PatientPayment[];
}

// Doctor (for appointment request — portal-accessible)
export interface PatientDoctor {
  id: string;
  name: string;
  specialty?: string;
}

// Clinic info
export interface PatientClinicInfo {
  clinicName: string;
  phone?: string;
  whatsapp?: string;
  address?: string;
  workingHours?: string;
}

// Dashboard — enriched
export interface PatientPortalDashboard {
  profile: PatientPortalProfile;
  nextAppointment: PatientAppointment | null;
  totalAppointments: number;
  upcomingAppointments: number;
  completedTreatments: number;
  finance: PatientFinancialSummary;
  latestPrescription: PatientPrescription | null;
  clinicInfo: PatientClinicInfo;
}

// Legacy alias for backward compatibility
export type PortalDoctor = PatientDoctor;
