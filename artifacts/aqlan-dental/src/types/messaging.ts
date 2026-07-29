export type ConversationType = "StaffToStaff" | "StaffToPatient" | "PatientFacing";

export type ConversationFilter = "all" | "unread" | "StaffToStaff" | "StaffToPatient" | "PatientFacing";

export type RecipientType = "TreatingDoctor" | "Reception" | "Admin";

export interface MessageAttachment {
  id: string;
  fileName: string;
  fileType: string;
  fileSize: number;
  url: string;
}

export interface ConversationParticipant {
  userId: string;
  username: string;
  displayName?: string;
  role?: string;
  avatarInitials?: string;
  color?: string;
  isAdmin: boolean;
}

export interface ConversationListItem {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: ConversationType;
  patientId?: string;
  patientName?: string;
  patientNumber?: string;
  lastMessageAt: string | null;
  lastMessagePreview: string | null;
  unreadCount: number;
  otherParticipant?: ConversationParticipant;
  participants: ConversationParticipant[];
  recipientType?: string | null;
  recipientUserId?: string | null;
}

export interface Message {
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
  attachments?: MessageAttachment[];
  replyToId?: string;
  replyToContent?: string;
  replyToSenderName?: string;
  isSystemMessage: boolean;
  isEdited: boolean;
  editedAt?: string;
  isReadByMe: boolean;
  readCount: number;
  createdAt: string;
}

export interface MessagingStats {
  totalConversations: number;
  activeConversations: number;
  messagesToday: number;
  messagesThisWeek: number;
  staffToStaffConversations: number;
  staffToPatientConversations: number;
  patientFacingConversations: number;
}

export interface ConversationDetail {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: ConversationType;
  patientId?: string;
  patientName?: string;
  patientNumber?: string;
  patientPhone?: string;
  participants: ConversationParticipant[];
  messages: Message[];
  createdAt: string;
  recipientType?: string | null;
  recipientUserId?: string | null;
}

export interface CreateConversationRequest {
  title?: string;
  isGroup?: boolean;
  participantIds: string[];
  initialMessage?: string;
  conversationType?: string;
  patientId?: string;
}

export interface SendMessageRequest {
  content: string;
  attachmentUrl?: string;
  attachmentName?: string;
  attachmentType?: string;
  replyToId?: string;
}

export interface UnreadCount {
  totalUnread: number;
  unreadConversations: number;
}
