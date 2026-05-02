// Auth
export interface PatientPasswordLoginRequest {
  username: string;
  password: string;
}

export interface PatientCredentialsRequest {
  phoneNumber: string;
}

export interface PatientAuthResponse {
  accessToken: string;
  profile: PatientPortalProfile;
  mustChangePassword: boolean;
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

// Portal Account Info (for staff dashboard)
export interface PatientPortalAccountInfo {
  patientId: string;
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

// Messaging (portal)
export interface PortalConversationList {
  id: string;
  title: string;
  isGroup: boolean;
  lastMessageAt?: string;
  lastMessagePreview?: string;
  unreadCount: number;
  otherParticipant?: PortalConversationParticipant;
  participants: PortalConversationParticipant[];
}

export interface PortalConversationParticipant {
  userId: string;
  username: string;
  displayName?: string;
  role?: string;
  avatarInitials?: string;
  color?: string;
  isAdmin: boolean;
}

export interface PortalMessage {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  senderInitials?: string;
  senderColor?: string;
  content: string;
  attachmentUrl?: string;
  attachmentName?: string;
  attachmentType?: string;
  replyToId?: string;
  replyToContent?: string;
  replyToSenderName?: string;
  isSystemMessage: boolean;
  isReadByMe: boolean;
  readCount: number;
  createdAt: string;
}

export interface PortalConversationDetail {
  id: string;
  title: string;
  isGroup: boolean;
  participants: PortalConversationParticipant[];
  messages: PortalMessage[];
  createdAt: string;
}

export interface PortalUnreadCount {
  totalUnread: number;
  unreadConversations: number;
}
