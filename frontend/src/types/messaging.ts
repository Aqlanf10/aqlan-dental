export interface Conversation {
  id: string;
  title?: string;
  type: "Direct" | "Group";
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  participants: ConversationParticipant[];
  lastMessage?: LastMessage;
  unreadCount: number;
}

export interface ConversationParticipant {
  userId: string;
  username: string;
  fullName?: string;
  role: string;
  lastReadAt?: string;
}

export interface LastMessage {
  id: string;
  senderId: string;
  senderName: string;
  content: string;
  type: string;
  createdAt: string;
}

export interface Message {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  senderRole: string;
  content: string;
  type: "Text" | "Inquiry" | "System";
  createdAt: string;
}

export interface DoctorForMessaging {
  userId: string;
  username: string;
  fullName?: string;
  role: string;
  specialty?: string;
}

export interface CreateConversationRequest {
  recipientId?: string;
  participantIds?: string[];
  title?: string;
  initialMessage?: string;
  type?: string;
}

export interface SendMessageRequest {
  content: string;
  type: string;
}
