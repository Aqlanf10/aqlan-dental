// Auth
export interface PatientLoginRequest {
  phoneNumber: string;
}

export interface PatientVerifyRequest {
  phoneNumber: string;
  code: string;
}

export interface PatientAuthResponse {
  accessToken: string;
  profile: PatientPortalProfile;
}

// Profile
export interface PatientPortalProfile {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
  gender?: string;
  age?: number;
  primaryDoctorName?: string;
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
  createdAt: string;
  notes?: string;
}

// Prescriptions
export interface PatientPrescription {
  id: string;
  medicationName: string;
  dosage?: string;
  frequency?: string;
  duration?: string;
  instructions?: string;
  doctorName: string;
  createdAt: string;
}

// Payments
export interface PatientPayment {
  id: string;
  amount: number;
  paymentMethod: string;
  receiptNumber?: string;
  createdAt: string;
}

export interface PatientFinancialSummary {
  totalPaid: number;
  totalOutstanding: number;
  activeContracts: number;
  recentPayments: PatientPayment[];
}

// Dashboard
export interface PatientPortalDashboard {
  profile: PatientPortalProfile;
  nextAppointment: PatientAppointment | null;
  totalAppointments: number;
  completedTreatments: number;
  finance: PatientFinancialSummary;
}

// Doctor (for appointment request)
export interface PortalDoctor {
  id: string;
  name: string;
  specialty: string;
}
