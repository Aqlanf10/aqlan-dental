export type PatientProfile = {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string | null;
  primaryDoctorName?: string | null;
};

export type PatientAuthResponse = {
  accessToken: string;
  refreshToken: string;
  profile: PatientProfile;
  mustChangePassword: boolean;
};

export type PatientDashboard = {
  profile: PatientProfile;
  nextAppointment?: PatientAppointment | null;
  totalAppointments: number;
  upcomingAppointments: number;
  completedTreatments: number;
  finance: {
    totalAmount: number;
    totalPaid: number;
    totalOutstanding: number;
  };
  clinicInfo: {
    clinicName: string;
    phone?: string | null;
    address?: string | null;
  };
};

export type PatientAppointment = {
  id: string;
  appointmentDate: string;
  startTime: string;
  appointmentType: string;
  doctorName: string;
  status: string;
  canCancel: boolean;
};
