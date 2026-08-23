import type {
  ConversationDetail,
  ConversationListItem,
  ConversationListResponse,
  ConversationMessage,
  ConversationParticipant,
  MessageAttachment,
  MessagingUnreadCount
} from "./types";

type UnknownRecord = Record<string, unknown>;

function record(value: unknown): UnknownRecord | null {
  return value !== null && typeof value === "object" ? value as UnknownRecord : null;
}

function property(source: UnknownRecord, camel: string, pascal: string): unknown {
  return source[camel] ?? source[pascal];
}

function text(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function optionalText(value: unknown): string | null {
  const valueText = text(value);
  return valueText || null;
}

function finiteNumber(value: unknown, fallback = 0): number {
  const normalized = typeof value === "number" ? value : Number(value);
  return Number.isFinite(normalized) ? normalized : fallback;
}

function boolean(value: unknown, fallback = false): boolean {
  return typeof value === "boolean" ? value : fallback;
}

function normalizeParticipant(value: unknown): ConversationParticipant | null {
  const source = record(value);
  if (!source) return null;
  const userId = text(property(source, "userId", "UserId"));
  if (!userId) return null;
  return {
    userId,
    username: text(property(source, "username", "Username")) || "مستخدم",
    displayName: optionalText(property(source, "displayName", "DisplayName")),
    role: optionalText(property(source, "role", "Role")),
    avatarInitials: optionalText(property(source, "avatarInitials", "AvatarInitials")),
    color: optionalText(property(source, "color", "Color")),
    isAdmin: boolean(property(source, "isAdmin", "IsAdmin"))
  };
}

function normalizeParticipants(value: unknown): ConversationParticipant[] {
  if (!Array.isArray(value)) return [];
  return value.map(normalizeParticipant).filter((item): item is ConversationParticipant => item !== null);
}

function normalizeAttachment(value: unknown): MessageAttachment | null {
  const source = record(value);
  if (!source) return null;
  const fileUrl = text(property(source, "fileUrl", "FileUrl"));
  if (!fileUrl) return null;
  const id = text(property(source, "id", "Id")) || fileUrl;
  return {
    id,
    messageId: text(property(source, "messageId", "MessageId")),
    fileUrl,
    fileName: text(property(source, "fileName", "FileName")) || "مرفق",
    fileSize: Math.max(0, finiteNumber(property(source, "fileSize", "FileSize"))),
    mimeType: text(property(source, "mimeType", "MimeType")) || "application/octet-stream"
  };
}

export function normalizeConversationMessage(value: unknown): ConversationMessage | null {
  const source = record(value);
  if (!source) return null;
  const id = text(property(source, "id", "Id"));
  if (!id) return null;
  const rawAttachments = property(source, "attachments", "Attachments");
  const attachments = Array.isArray(rawAttachments)
    ? rawAttachments.map(normalizeAttachment).filter((item): item is MessageAttachment => item !== null)
    : [];
  return {
    id,
    conversationId: text(property(source, "conversationId", "ConversationId")),
    senderId: text(property(source, "senderId", "SenderId")),
    senderName: text(property(source, "senderName", "SenderName")) || "مستخدم",
    senderInitials: optionalText(property(source, "senderInitials", "SenderInitials")),
    senderColor: optionalText(property(source, "senderColor", "SenderColor")),
    content: text(property(source, "content", "Content")),
    attachmentUrl: optionalText(property(source, "attachmentUrl", "AttachmentUrl")),
    attachmentName: optionalText(property(source, "attachmentName", "AttachmentName")),
    attachmentType: optionalText(property(source, "attachmentType", "AttachmentType")),
    attachments,
    replyToId: optionalText(property(source, "replyToId", "ReplyToId")),
    replyToContent: optionalText(property(source, "replyToContent", "ReplyToContent")),
    replyToSenderName: optionalText(property(source, "replyToSenderName", "ReplyToSenderName")),
    isSystemMessage: boolean(property(source, "isSystemMessage", "IsSystemMessage")),
    isEdited: boolean(property(source, "isEdited", "IsEdited")),
    editedAt: optionalText(property(source, "editedAt", "EditedAt")),
    isReadByMe: boolean(property(source, "isReadByMe", "IsReadByMe")),
    readCount: Math.max(0, finiteNumber(property(source, "readCount", "ReadCount"))),
    createdAt: text(property(source, "createdAt", "CreatedAt"))
  };
}

export function normalizeConversationMessages(value: unknown): ConversationMessage[] {
  if (!Array.isArray(value)) return [];
  return value.map(normalizeConversationMessage).filter((item): item is ConversationMessage => item !== null);
}

export function normalizeConversationListItem(value: unknown): ConversationListItem | null {
  const source = record(value);
  if (!source) return null;
  const id = text(property(source, "id", "Id"));
  if (!id) return null;
  return {
    id,
    title: text(property(source, "title", "Title")) || "محادثة",
    isGroup: boolean(property(source, "isGroup", "IsGroup")),
    conversationType: text(property(source, "conversationType", "ConversationType")) || "Internal",
    patientId: optionalText(property(source, "patientId", "PatientId")),
    patientName: optionalText(property(source, "patientName", "PatientName")),
    patientNumber: optionalText(property(source, "patientNumber", "PatientNumber")),
    lastMessageAt: optionalText(property(source, "lastMessageAt", "LastMessageAt")),
    lastMessagePreview: optionalText(property(source, "lastMessagePreview", "LastMessagePreview")),
    unreadCount: Math.max(0, finiteNumber(property(source, "unreadCount", "UnreadCount"))),
    otherParticipant: normalizeParticipant(property(source, "otherParticipant", "OtherParticipant")),
    participants: normalizeParticipants(property(source, "participants", "Participants")),
    recipientType: optionalText(property(source, "recipientType", "RecipientType")),
    recipientUserId: optionalText(property(source, "recipientUserId", "RecipientUserId"))
  };
}

export function normalizeConversationList(value: unknown): ConversationListResponse {
  const source = record(value) ?? {};
  const rawData = property(source, "data", "Data");
  const data = Array.isArray(rawData)
    ? rawData.map(normalizeConversationListItem).filter((item): item is ConversationListItem => item !== null)
    : [];
  const totalCount = Math.max(0, finiteNumber(property(source, "totalCount", "TotalCount"), data.length));
  const page = Math.max(1, finiteNumber(property(source, "page", "Page"), 1));
  const pageSize = Math.max(1, finiteNumber(property(source, "pageSize", "PageSize"), Math.max(data.length, 1)));
  return {
    data,
    totalCount,
    page,
    pageSize,
    totalPages: Math.max(0, finiteNumber(property(source, "totalPages", "TotalPages"), Math.ceil(totalCount / pageSize))),
    hasNextPage: boolean(property(source, "hasNextPage", "HasNextPage"), page * pageSize < totalCount),
    hasPreviousPage: boolean(property(source, "hasPreviousPage", "HasPreviousPage"), page > 1)
  };
}

export function normalizeUnreadCount(value: unknown): MessagingUnreadCount {
  const source = record(value) ?? {};
  return {
    totalUnread: Math.max(0, finiteNumber(property(source, "totalUnread", "TotalUnread"))),
    unreadConversations: Math.max(0, finiteNumber(property(source, "unreadConversations", "UnreadConversations")))
  };
}

export function normalizeConversationDetail(value: unknown): ConversationDetail | null {
  const source = record(value);
  if (!source) return null;
  const id = text(property(source, "id", "Id"));
  if (!id) return null;
  return {
    id,
    title: text(property(source, "title", "Title")) || "محادثة",
    isGroup: boolean(property(source, "isGroup", "IsGroup")),
    conversationType: text(property(source, "conversationType", "ConversationType")) || "Internal",
    patientId: optionalText(property(source, "patientId", "PatientId")),
    patientName: optionalText(property(source, "patientName", "PatientName")),
    patientNumber: optionalText(property(source, "patientNumber", "PatientNumber")),
    patientPhone: optionalText(property(source, "patientPhone", "PatientPhone")),
    participants: normalizeParticipants(property(source, "participants", "Participants")),
    messages: normalizeConversationMessages(property(source, "messages", "Messages")),
    createdAt: text(property(source, "createdAt", "CreatedAt")),
    recipientType: optionalText(property(source, "recipientType", "RecipientType")),
    recipientUserId: optionalText(property(source, "recipientUserId", "RecipientUserId"))
  };
}

export function normalizePollMessages(value: unknown): ConversationMessage[] {
  const source = record(value);
  return source ? normalizeConversationMessages(property(source, "messages", "Messages")) : [];
}
