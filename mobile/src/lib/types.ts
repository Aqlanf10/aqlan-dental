export type StaffUser = {
  id: string;
  username: string;
  role: string;
  branchId?: string | null;
  doctorName?: string | null;
  doctorId?: string | null;
  doctorColor?: string | null;
  doctorInitials?: string | null;
  mustChangePassword: boolean;
  email?: string | null;
  isActive: boolean;
  deletedAt?: string | null;
};

export type UserPermissions = {
  role: string;
  permissions: string[];
};

export type MobileLoginResponse = {
  accessToken: string;
  refreshToken: string;
  user: StaffUser;
};

export type MobileRefreshResponse = {
  accessToken: string;
  refreshToken: string;
};

export type PaginatedResponse<T> = {
  data: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages?: number;
};

export type PatientListItem = {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string | null;
  email?: string | null;
  gender?: string | null;
  age?: number | null;
  primaryDoctorName?: string | null;
  branchName?: string | null;
  createdAt: string;
  isActive: boolean;
  lastVisitDate?: string | null;
};

export type MedicalHistory = {
  chronicDiseases?: string | null;
  currentMedications?: string | null;
  drugAllergies?: string | null;
  bleedingDisorders?: boolean;
  isPregnant?: string | null;
  tmjProblems?: boolean;
  previousSurgeries?: string | null;
  notes?: string | null;
};

export type DentalHistory = {
  chiefComplaint?: string | null;
  previousTreatments?: string | null;
  mouthBreathing?: boolean;
  bruxism?: boolean;
  thumbSucking?: boolean;
  tongueThrusing?: boolean;
  notes?: string | null;
};

export type PatientProfile = {
  id: string;
  patientNumber: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  dateOfBirth?: string | null;
  gender?: string | null;
  age?: number | null;
  phone?: string | null;
  email?: string | null;
  whatsApp?: string | null;
  address?: string | null;
  occupation?: string | null;
  referralSource?: string | null;
  primaryDoctorId?: string | null;
  primaryDoctorName?: string | null;
  branchId?: string | null;
  branchName?: string | null;
  createdAt: string;
  isActive: boolean;
  medicalHistory?: MedicalHistory | null;
  dentalHistory?: DentalHistory | null;
  isLimitedView?: boolean;
};

export type Appointment = {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId: string;
  doctorName: string;
  doctorColor?: string | null;
  appointmentDate: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  appointmentType: string;
  specialty?: string | null;
  status: string;
  notes?: string | null;
  roomName?: string | null;
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
  packageName?: string | null;
};

export type DashboardStats = {
  appointmentsToday: number;
  newPatientsToday: number;
  totalPatients: number;
  activeOrthoCases: number;
  pendingLabOrders: number;
  overdueContractsCount: number;
  totalRevenueMTD: number;
  queueWaitingCount: number;
  pendingBookingRequestsCount: number;
  todayArrivedCount: number;
};

export type DashboardAlerts = {
  overdueLabOrdersCount: number;
  maxLabDaysOverdue: number;
  todayNoShowCount: number;
  longWaitingCount: number;
  unconfirmedTomorrowCount: number;
  recallCandidatesCount: number;
};
