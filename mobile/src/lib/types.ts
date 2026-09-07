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

export type DoctorSummary = {
  id: string;
  name: string;
  specialty?: string | null;
  color?: string | null;
  branchId?: string | null;
  branchName?: string | null;
  isActive: boolean;
  defaultClinicRoomId?: string | null;
  defaultRoomName?: string | null;
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

export type PatientMutationInput = {
  firstName: string;
  middleName?: string | null;
  lastName: string;
  dateOfBirth?: string | null;
  gender?: string | null;
  phone?: string | null;
  email?: string | null;
  whatsApp?: string | null;
  address?: string | null;
  occupation?: string | null;
  referralSource?: string | null;
  primaryDoctorId?: string | null;
  medicalHistory?: MedicalHistory | null;
  dentalHistory?: DentalHistory | null;
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

export type AppointmentMutationInput = {
  patientId: string;
  doctorId: string;
  appointmentDate: string;
  startTime: string;
  durationMinutes: number;
  appointmentType: string;
  specialty?: string | null;
  notes?: string | null;
  serviceId?: string | null;
  clinicRoomId?: string | null;
  orthoCaseId?: string | null;
  companionName?: string | null;
  companionPhone?: string | null;
  companionRelationship?: string | null;
  appointmentColor?: string | null;
  packageId?: string | null;
};

export type VisitAppointmentSummary = {
  appointmentDate: string;
  appointmentTime: string;
  appointmentType?: string | null;
  appointmentStatus?: string | null;
  doctorName?: string | null;
};

export type ClinicalVisit = {
  id: string;
  patientId: string;
  appointmentId?: string | null;
  visitDate: string;
  visitType?: string | null;
  specialty?: string | null;
  doctorId?: string | null;
  doctorName?: string | null;
  chiefComplaint?: string | null;
  clinicalNotes?: string | null;
  treatmentDone?: string | null;
  diagnosis?: string | null;
  instructions?: string | null;
  nextVisitPlan?: string | null;
  cost?: number | null;
  nextVisitDate?: string | null;
  serviceId?: string | null;
  checkoutStatus?: string | null;
  amountDueReference?: number | null;
  proposedProcedure?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  appointment?: VisitAppointmentSummary | null;
};

export type VisitListResponse = {
  data: ClinicalVisit[];
  total: number;
  page: number;
  pageSize: number;
};

export type VisitMutationInput = {
  patientId?: string;
  appointmentId?: string | null;
  visitDate?: string | null;
  visitType?: string | null;
  specialty?: string | null;
  doctorId?: string | null;
  serviceId?: string | null;
  chiefComplaint?: string | null;
  clinicalNotes?: string | null;
  treatmentDone?: string | null;
  diagnosis?: string | null;
  instructions?: string | null;
  nextVisitPlan?: string | null;
  nextVisitDate?: string | null;
};

export type ConversationParticipant = {
  userId: string;
  username: string;
  displayName?: string | null;
  role?: string | null;
  avatarInitials?: string | null;
  color?: string | null;
  isAdmin: boolean;
};

export type MessageAttachment = {
  id: string;
  messageId: string;
  fileUrl: string;
  fileName: string;
  fileSize: number;
  mimeType: string;
};

export type ConversationMessage = {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  senderInitials?: string | null;
  senderColor?: string | null;
  content: string;
  attachmentUrl?: string | null;
  attachmentName?: string | null;
  attachmentType?: string | null;
  attachments: MessageAttachment[];
  replyToId?: string | null;
  replyToContent?: string | null;
  replyToSenderName?: string | null;
  isSystemMessage: boolean;
  isEdited: boolean;
  editedAt?: string | null;
  isReadByMe: boolean;
  readCount: number;
  createdAt: string;
};

export type ConversationListItem = {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: string;
  patientId?: string | null;
  patientName?: string | null;
  patientNumber?: string | null;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  unreadCount: number;
  otherParticipant?: ConversationParticipant | null;
  participants: ConversationParticipant[];
  recipientType?: string | null;
  recipientUserId?: string | null;
};

export type ConversationListResponse = {
  data: ConversationListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
};

export type ConversationDetail = {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: string;
  patientId?: string | null;
  patientName?: string | null;
  patientNumber?: string | null;
  patientPhone?: string | null;
  participants: ConversationParticipant[];
  messages: ConversationMessage[];
  createdAt: string;
  recipientType?: string | null;
  recipientUserId?: string | null;
};

export type MessagingUnreadCount = {
  totalUnread: number;
  unreadConversations: number;
};

export type NotificationItem = {
  id: string;
  type: string;
  title: string;
  body: string;
  isRead: boolean;
  relatedEntity?: string | null;
  relatedId?: string | null;
  createdAt: string;
};

export type NotificationsResponse = {
  data: NotificationItem[];
  total: number;
  unreadCount: number;
  page: number;
  pageSize: number;
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
