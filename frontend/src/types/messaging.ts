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
  conversationType: "StaffToStaff" | "StaffToPatient";
  patientId?: string;
  patientName?: string;
  lastMessageAt: string | null;
  lastMessagePreview: string | null;
  unreadCount: number;
  otherParticipant?: ConversationParticipant;
  participants: ConversationParticipant[];
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
  replyToId?: string;
  replyToContent?: string;
  replyToSenderName?: string;
  isSystemMessage: boolean;
  isReadByMe: boolean;
  readCount: number;
  createdAt: string;
}

export interface ConversationDetail {
  id: string;
  title: string;
  isGroup: boolean;
  conversationType: "StaffToStaff" | "StaffToPatient";
  patientId?: string;
  patientName?: string;
  patientPhone?: string;
  participants: ConversationParticipant[];
  messages: Message[];
  createdAt: string;
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
