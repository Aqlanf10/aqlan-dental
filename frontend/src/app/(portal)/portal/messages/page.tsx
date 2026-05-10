"use client";
import { useState, useRef, useEffect, useCallback } from "react";
import {
  MessageCircle, Send, Plus, ArrowRight, Loader2,
  X, AlertTriangle, CheckCheck, User,
  Stethoscope, Building2, ShieldCheck, ChevronLeft,
  Paperclip, Image as ImageIcon, FileText, Reply, Mic,
} from "lucide-react";
import { VoiceRecorder } from "@/components/messages/VoiceRecorder";
import { cn } from "@/lib/utils";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import {
  usePortalConversations,
  usePortalConversation,
  usePortalSendMessage,
  usePortalMarkAsRead,
  usePortalStartConversation,
  usePortalUnreadCount,
  usePortalRecipients,
  type PortalConversationListItem,
  type PortalConversationDetail,
  type PortalMessage,
  type PortalRecipient,
  type RecipientType,
  type StartConversationPayload,
  type PortalSendMessageRequest,
} from "@/hooks/usePortalMessaging";
import portalApi from "@/lib/portalApi";

// ─── Helpers ──────────────────────────────────────────────────────────────────

function getPatientUserId(): string | null {
  if (typeof window === "undefined") return null;
  try {
    const token = localStorage.getItem("portal_token");
    if (!token) return null;
    const payload = JSON.parse(atob(token.split(".")[1]));
    return payload.userId ?? null;
  } catch {
    return null;
  }
}

function formatTime(dateStr: string) {
  const d = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const mins = Math.floor(diffMs / 60000);
  const hours = Math.floor(mins / 60);
  const days = Math.floor(hours / 24);

  if (mins < 1) return "الآن";
  if (mins < 60) return `منذ ${mins} د`;
  if (hours < 24) return d.toLocaleTimeString("ar-SA", { hour: "2-digit", minute: "2-digit" });
  if (days < 7) return `منذ ${days} ي`;
  return d.toLocaleDateString("ar-SA", { month: "short", day: "numeric" });
}

function formatFullTime(dateStr: string) {
  return new Date(dateStr).toLocaleTimeString("ar-SA", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getInitials(name: string) {
  return name.trim().charAt(0).toUpperCase();
}

function getErrorMessage(error: unknown): { title: string; detail: string } {
  const err = error as { message?: string; status?: number; response?: { status?: number } } | null;
  const status = err?.status ?? err?.response?.status;
  const message = err?.message;

  if (status === 403) {
    return { title: "غير مصرّح بالوصول", detail: "ليس لديك صلاحية لعرض هذا المحتوى" };
  }
  if (status === 404) {
    return { title: "المحتوى غير موجود", detail: "البيانات المطلوبة غير متوفرة" };
  }
  return { title: "حدث خطأ", detail: message ?? "فشل تحميل البيانات" };
}

const RECIPIENT_TYPE_LABELS: Record<string, string> = {
  TreatingDoctor: "الطبيب المسؤول",
  Reception: "الاستقبال",
  Admin: "الإدارة / الدعم",
};

function getRecipientBadgeLabel(type: string | null | undefined): string | null {
  if (!type) return null;
  return RECIPIENT_TYPE_LABELS[type] ?? null;
}

// ─── Recipient option config ──────────────────────────────────────────────────

// ─── Attachment constants ─────────────────────────────────────────────────────
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
const ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".pdf", ".webm", ".ogg", ".mp4", ".m4a", ".mp3", ".wav"];
const ALLOWED_MIME_TYPES = ["image/jpeg", "image/png", "application/pdf", "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav"];

/** Convert a full upload URL to a relative /uploads/ path for the backend */
function toRelativeUploadUrl(url: string): string {
  try {
    const u = new URL(url);
    return u.pathname; // e.g. /uploads/guid.ext
  } catch {
    // Already a relative path
    return url;
  }
}

/** Convert a relative /uploads/ path to a full URL for display (img src, links) */
function toFullUploadUrl(url: string): string {
  if (!url) return "";
  if (url.startsWith("http://") || url.startsWith("https://")) return url;
  const base = process.env.NEXT_PUBLIC_API_URL ?? "";
  return `${base}${url}`;
}

const RECIPIENT_OPTIONS: {
  type: RecipientType;
  label: string;
  description: string;
  icon: typeof Stethoscope;
  color: string;
  bgColor: string;
  borderColor: string;
}[] = [
  {
    type: "TreatingDoctor",
    label: "الطبيب المسؤول",
    description: "تواصل مع طبيبك المعالج مباشرة",
    icon: Stethoscope,
    color: "text-emerald-600",
    bgColor: "bg-emerald-50",
    borderColor: "border-emerald-200 hover:border-emerald-400",
  },
  {
    type: "Reception",
    label: "الاستقبال",
    description: "حجز مواعيد، استفسارات عامة",
    icon: Building2,
    color: "text-amber-600",
    bgColor: "bg-amber-50",
    borderColor: "border-amber-200 hover:border-amber-400",
  },
  {
    type: "Admin",
    label: "الإدارة / الدعم",
    description: "شكاوى، اقتراحات، مشاكل تقنية",
    icon: ShieldCheck,
    color: "text-purple-600",
    bgColor: "bg-purple-50",
    borderColor: "border-purple-200 hover:border-purple-400",
  },
];

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PortalMessagesPage() {
  const { profile } = usePatientAuthStore();
  const [selectedConvId, setSelectedConvId] = useState<string | null>(null);
  const [showMobileChat, setShowMobileChat] = useState(false);
  const [showStartDialog, setShowStartDialog] = useState(false);
  const [patientUserId] = useState<string | null>(() => getPatientUserId());

  const { data: conversations = [], isLoading, isError, error } = usePortalConversations();
  const { data: conversation } = usePortalConversation(selectedConvId);
  const { data: unreadData } = usePortalUnreadCount();
  const markAsRead = usePortalMarkAsRead(selectedConvId);
  const sendMessage = usePortalSendMessage(selectedConvId ?? "");
  const startConversation = usePortalStartConversation();

  const totalUnread = unreadData?.totalUnread ?? conversations.reduce((sum, c) => sum + c.unreadCount, 0);

  // Mark as read when opening a conversation
  useEffect(() => {
    if (selectedConvId) {
      markAsRead.mutate();
      setShowMobileChat(true);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedConvId]);

  const handleSelectConv = useCallback((id: string) => {
    setSelectedConvId(id);
  }, []);

  const handleBack = useCallback(() => {
    setShowMobileChat(false);
    setSelectedConvId(null);
  }, []);

  const handleSend = useCallback(
    (req: PortalSendMessageRequest) => {
      if (!selectedConvId) return;
      sendMessage.mutate(req);
    },
    [selectedConvId, sendMessage]
  );

  const handleStartConversation = useCallback(
    async (payload: StartConversationPayload) => {
      try {
        const conv = await startConversation.mutateAsync(payload);
        setSelectedConvId(conv.id);
        setShowStartDialog(false);
      } catch (err: unknown) {
        setShowStartDialog(false);
        // Show friendly Arabic error if backend returned a message
        const axiosErr = err as { response?: { data?: { message?: string } } };
        const msg = axiosErr?.response?.data?.message;
        if (msg) {
          alert(msg);
        }
      }
    },
    [startConversation]
  );

  return (
    <div className="px-3 pb-24 pt-4" dir="rtl">
      {/* Page Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <MessageCircle className="w-5 h-5 text-teal-600" />
          <h1 className="text-lg font-bold text-gray-900">الرسائل</h1>
          {totalUnread > 0 && (
            <span className="bg-red-500 text-white text-xs font-bold rounded-full px-2 py-0.5 leading-none">
              {totalUnread > 99 ? "99+" : totalUnread}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {profile && (
            <span className="text-xs text-gray-400 max-w-[120px] truncate">
              {profile.fullName}
            </span>
          )}
          <button
            onClick={() => setShowStartDialog(true)}
            className="w-9 h-9 rounded-full bg-teal-500 text-white flex items-center justify-center hover:bg-teal-600 active:bg-teal-700 transition shadow-sm"
            title="محادثة جديدة"
          >
            <Plus className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Main chat layout */}
      <div
        className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden"
        style={{ height: "calc(100vh - 168px)" }}
      >
        <div className="flex h-full">
          {/* ── Conversation List ── */}
          <div
            className={cn(
              "w-full md:w-80 border-l border-gray-100 flex flex-col flex-shrink-0",
              showMobileChat ? "hidden md:flex" : "flex"
            )}
          >
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50/80 flex items-center justify-between">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                المحادثات
              </p>
              {totalUnread > 0 && (
                <span className="text-[10px] font-bold text-teal-600 bg-teal-50 rounded-full px-2 py-0.5">
                  {totalUnread} غير مقروءة
                </span>
              )}
            </div>

            <div className="flex-1 overflow-y-auto">
              {isLoading ? (
                <div className="flex flex-col items-center justify-center py-16 gap-3">
                  <Loader2 className="w-6 h-6 animate-spin text-teal-500" />
                  <p className="text-sm text-gray-400">جارٍ التحميل...</p>
                </div>
              ) : isError ? (
                <ConvListError error={error} />
              ) : conversations.length === 0 ? (
                <ConvListEmpty onNew={() => setShowStartDialog(true)} />
              ) : (
                <div className="p-2 space-y-1">
                  {conversations.map((conv) => (
                    <ConversationItem
                      key={conv.id}
                      conv={conv}
                      isSelected={conv.id === selectedConvId}
                      onClick={() => handleSelectConv(conv.id)}
                    />
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* ── Chat Area ── */}
          <div
            className={cn(
              "flex-1 flex flex-col min-w-0",
              !showMobileChat ? "hidden md:flex" : "flex"
            )}
          >
            {selectedConvId && conversation ? (
              <ChatArea
                conversation={conversation}
                patientUserId={patientUserId}
                onBack={handleBack}
                onSend={handleSend}
                sending={sendMessage.isPending}
                sendError={sendMessage.isError ? "فشل إرسال الرسالة" : undefined}
              />
            ) : (
              <EmptyChatPlaceholder onNew={() => setShowStartDialog(true)} />
            )}
          </div>
        </div>
      </div>

      {/* ── Start Conversation Dialog ── */}
      <StartConversationDialog
        open={showStartDialog}
        onClose={() => setShowStartDialog(false)}
        onStart={handleStartConversation}
        loading={startConversation.isPending}
      />
    </div>
  );
}

// ─── Conversation List States ─────────────────────────────────────────────────

function ConvListEmpty({ onNew }: { onNew: () => void }) {
  return (
    <div className="text-center py-14 px-4">
      <div className="w-16 h-16 rounded-2xl bg-teal-50 flex items-center justify-center mx-auto mb-4">
        <MessageCircle className="w-8 h-8 text-teal-400" />
      </div>
      <p className="text-sm font-semibold text-gray-700 mb-1">لا توجد محادثات</p>
      <p className="text-xs text-gray-400 mb-5">تواصل مع المركز للاستفسار أو الحجز</p>
      <button
        onClick={onNew}
        className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full bg-teal-500 text-white text-sm font-semibold hover:bg-teal-600 active:bg-teal-700 transition shadow-sm"
      >
        <Plus className="w-3.5 h-3.5" />
        بدء محادثة
      </button>
    </div>
  );
}

function ConvListError({ error }: { error: unknown }) {
  const { title, detail } = getErrorMessage(error);
  return (
    <div className="m-3 p-4 bg-amber-50 border border-amber-200 rounded-xl">
      <div className="flex items-start gap-2.5">
        <AlertTriangle className="w-4 h-4 text-amber-500 mt-0.5 flex-shrink-0" />
        <div>
          <p className="text-xs font-semibold text-amber-800">{title}</p>
          <p className="text-xs text-amber-600 mt-0.5">{detail}</p>
        </div>
      </div>
    </div>
  );
}

// ─── Conversation Item ────────────────────────────────────────────────────────

function ConversationItem({
  conv,
  isSelected,
  onClick,
}: {
  conv: PortalConversationListItem;
  isSelected: boolean;
  onClick: () => void;
}) {
  const staffParticipant = conv.otherParticipant ?? conv.participants[0];
  const recipientLabel = getRecipientBadgeLabel(conv.recipientType);
  const displayName = recipientLabel
    ? conv.recipientType === "TreatingDoctor" && staffParticipant?.displayName
      ? `${staffParticipant.displayName} — ${recipientLabel}`
      : recipientLabel
    : (conv.title || staffParticipant?.displayName || "المركز");
  const initial = getInitials(displayName);
  const avatarColor = staffParticipant?.color ?? "#0d9488";
  const avatarInitials = staffParticipant?.avatarInitials ?? initial;
  const hasUnread = conv.unreadCount > 0;

  return (
    <button
      onClick={onClick}
      className={cn(
        "w-full flex items-center gap-3 px-4 py-3.5 text-right transition-all duration-150 rounded-xl",
        isSelected
          ? "bg-teal-50 border border-teal-200"
          : "hover:bg-gray-50 active:scale-[0.99] border border-transparent"
      )}
    >
      {/* Avatar */}
      <div
        className="w-11 h-11 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
        style={{ backgroundColor: avatarColor }}
      >
        {avatarInitials}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between gap-2">
          <span
            className={cn(
              "text-sm truncate",
              hasUnread ? "font-bold text-gray-900" : "font-semibold text-gray-800"
            )}
          >
            {displayName}
          </span>
          <div className="flex items-center gap-1.5 flex-shrink-0">
            {conv.lastMessageAt && (
              <span className="text-[10px] text-gray-400">
                {formatTime(conv.lastMessageAt)}
              </span>
            )}
            {hasUnread && (
              <span className="w-5 h-5 bg-teal-500 text-white text-[10px] font-bold rounded-full flex items-center justify-center">
                {conv.unreadCount > 9 ? "9+" : conv.unreadCount}
              </span>
            )}
          </div>
        </div>
        {conv.lastMessagePreview && (
          <p
            className={cn(
              "text-xs mt-0.5 truncate",
              hasUnread ? "text-gray-700 font-medium" : "text-gray-500"
            )}
          >
            {conv.lastMessagePreview}
          </p>
        )}
      </div>
    </button>
  );
}

// ─── Chat Area ────────────────────────────────────────────────────────────────

function ChatArea({
  conversation,
  patientUserId,
  onBack,
  onSend,
  sending,
  sendError,
}: {
  conversation: PortalConversationDetail;
  patientUserId: string | null;
  onBack: () => void;
  onSend: (req: PortalSendMessageRequest) => void;
  sending: boolean;
  sendError?: string;
}) {
  const [input, setInput] = useState("");
  const [replyTo, setReplyTo] = useState<PortalMessage | null>(null);
  const [attachmentPreview, setAttachmentPreview] = useState<{
    url: string; name: string; type: string;
  } | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [conversation.messages.length]);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Reset file input so re-selecting the same file works
    e.target.value = "";
    setUploadError(null);

    // Validate extension
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setUploadError("نوع الملف غير مدعوم. الأنواع المسموحة: JPG، PNG، PDF، صوتيات");
      return;
    }

    // Validate MIME type
    if (!ALLOWED_MIME_TYPES.includes(file.type)) {
      setUploadError("نوع الملف غير مدعوم. الأنواع المسموحة: JPG، PNG، PDF، صوتيات");
      return;
    }

    // Validate size
    if (file.size > MAX_FILE_SIZE) {
      setUploadError("حجم الملف كبير جداً. الحد الأقصى 10 ميجابايت.");
      return;
    }

    // Upload
    setIsUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const { data } = await portalApi.post<{
        url: string; fileName: string; originalName: string; size: number; contentType: string;
      }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setAttachmentPreview({
        url: data.url,
        name: data.originalName,
        type: data.contentType,
      });
    } catch {
      setUploadError("فشل رفع الملف. حاول مرة أخرى.");
    } finally {
      setIsUploading(false);
    }
  };

  const removeAttachment = () => {
    setAttachmentPreview(null);
    setUploadError(null);
  };

  const handleVoiceRecorded = async (blob: Blob, mimeType: string) => {
    setIsUploading(true);
    setUploadError(null);
    try {
      const ext = mimeType === "audio/ogg" ? ".ogg" : mimeType === "audio/mp4" ? ".mp4" : ".webm";
      const formData = new FormData();
      formData.append("file", blob, `voice${ext}`);
      const { data } = await portalApi.post<{
        url: string; fileName: string; originalName: string; contentType: string;
      }>("/api/uploads", formData, { headers: { "Content-Type": "multipart/form-data" } });
      setAttachmentPreview({ url: data.url, name: data.originalName, type: data.contentType });
    } catch {
      setUploadError("فشل رفع الرسالة الصوتية");
    } finally {
      setIsUploading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const trimmed = input.trim();
    if ((!trimmed && !attachmentPreview) || sending || trimmed.length > 2000 || isUploading) return;

    const req: PortalSendMessageRequest = { content: trimmed };
    if (replyTo) req.replyToId = replyTo.id;
    if (attachmentPreview) {
      req.attachmentUrl = toRelativeUploadUrl(attachmentPreview.url);
      req.attachmentName = attachmentPreview.name;
      req.attachmentType = attachmentPreview.type;
    }
    onSend(req);
    setInput("");
    setReplyTo(null);
    setAttachmentPreview(null);
    setUploadError(null);
    inputRef.current?.focus();
  };

  // Build display name for header
  const staffParticipants = conversation.participants.filter(
    (p) => p.userId !== patientUserId
  );
  const recipientLabel = getRecipientBadgeLabel(conversation.recipientType);
  const headerName =
    recipientLabel
      ? conversation.recipientType === "TreatingDoctor" && staffParticipants[0]?.displayName
        ? `${staffParticipants[0].displayName} — ${recipientLabel}`
        : recipientLabel
      : (conversation.title ||
        (staffParticipants.length === 1
          ? staffParticipants[0].displayName ?? "المركز"
          : `${staffParticipants[0]?.displayName ?? "المركز"} و${staffParticipants.length - 1} آخرين`));

  const avatarColor = staffParticipants[0]?.color ?? "#0d9488";
  const avatarInitials =
    staffParticipants[0]?.avatarInitials ?? getInitials(staffParticipants[0]?.displayName ?? "م");

  return (
    <>
      {/* Header */}
      <div className="px-4 py-3 border-b border-gray-100 bg-white flex items-center gap-3">
        <button
          onClick={onBack}
          className="md:hidden w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-500 transition"
        >
          <ArrowRight className="w-4 h-4" />
        </button>
        <div
          className="w-9 h-9 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
          style={{ backgroundColor: avatarColor }}
        >
          {avatarInitials}
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-gray-900 truncate">{headerName}</p>
          <div className="flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
            <p className="text-xs text-green-600">متصل الآن</p>
          </div>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-4 py-4 bg-gray-50/30">
        <div className="max-w-2xl mx-auto space-y-2">
          {conversation.messages.length === 0 ? (
            <div className="text-center py-16">
              <div className="w-14 h-14 rounded-2xl bg-gray-100 flex items-center justify-center mx-auto mb-3">
                <MessageCircle className="w-7 h-7 text-gray-300" />
              </div>
              <p className="text-sm font-medium text-gray-500">لا توجد رسائل بعد</p>
              <p className="text-xs text-gray-400 mt-1">ابدأ المحادثة بكتابة رسالة</p>
            </div>
          ) : (
            conversation.messages.map((msg) => (
              <MessageBubble
                key={msg.id}
                message={msg}
                isMine={patientUserId ? msg.senderId === patientUserId : false}
                onReply={() => { setReplyTo(msg); inputRef.current?.focus(); }}
              />
            ))
          )}
          <div ref={messagesEndRef} />
        </div>
      </div>

      {/* Send error */}
      {sendError && (
        <div className="px-4 py-2 bg-red-50 border-t border-red-200 flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 text-red-500 flex-shrink-0" />
          <div>
            <p className="text-xs font-semibold text-red-700">فشل الإرسال</p>
            <p className="text-[10px] text-red-600">{sendError}</p>
          </div>
        </div>
      )}

      {/* Upload error */}
      {uploadError && (
        <div className="px-4 py-2 bg-amber-50 border-t border-amber-200 flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 text-amber-500 flex-shrink-0" />
          <p className="text-xs text-amber-700">{uploadError}</p>
          <button onClick={() => setUploadError(null)} className="ms-auto">
            <X className="w-3 h-3 text-amber-500" />
          </button>
        </div>
      )}

      {/* Reply preview */}
      {replyTo && (
        <div className="px-4 py-2 bg-teal-50 border-t border-teal-200 flex items-center gap-2">
          <Reply className="w-3.5 h-3.5 text-teal-600 flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-teal-700">{replyTo.senderName}</p>
            <p className="text-xs text-teal-600 truncate">{replyTo.content}</p>
          </div>
          <button onClick={() => setReplyTo(null)} className="text-teal-400 hover:text-teal-600">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Input area */}
      <div className="px-4 py-3 border-t border-gray-100 bg-white">
        {/* Attachment preview chip */}
        {attachmentPreview && (
          <div className="flex items-center gap-2 mb-2 px-3 py-2 bg-gray-50 rounded-lg">
            {attachmentPreview.type.startsWith("image/") ? (
              <ImageIcon className="w-4 h-4 text-teal-600 flex-shrink-0" />
            ) : attachmentPreview.type.startsWith("audio/") ? (
              <Mic className="w-4 h-4 text-purple-500 flex-shrink-0" />
            ) : (
              <FileText className="w-4 h-4 text-red-500 flex-shrink-0" />
            )}
            <span className="text-xs text-gray-700 truncate flex-1">{attachmentPreview.name}</span>
            <button
              type="button"
              onClick={removeAttachment}
              className="w-5 h-5 rounded-full bg-gray-200 hover:bg-gray-300 flex items-center justify-center transition flex-shrink-0"
            >
              <X className="w-3 h-3 text-gray-500" />
            </button>
          </div>
        )}

        {/* Uploading indicator */}
        {isUploading && (
          <div className="flex items-center gap-2 mb-2 px-3 py-2 bg-teal-50 rounded-lg">
            <Loader2 className="w-4 h-4 text-teal-600 animate-spin" />
            <span className="text-xs text-teal-700">جارٍ رفع الملف...</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="flex items-center gap-2">
          {/* Hidden file input */}
          <input
            ref={fileInputRef}
            type="file"
            accept=".jpg,.jpeg,.png,.pdf,.webm,.ogg,.mp4,.m4a,.mp3,.wav"
            className="hidden"
            onChange={handleFileSelect}
          />
          <input
            ref={inputRef}
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="اكتب رسالتك..."
            maxLength={2000}
            disabled={sending || isUploading}
            className="flex-1 px-4 py-2.5 rounded-full border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-gray-50 disabled:opacity-50 transition"
          />
          <VoiceRecorder
            onRecorded={handleVoiceRecorded}
            disabled={sending || isUploading || !!attachmentPreview}
          />
          {/* Paperclip button */}
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={sending || isUploading || !!attachmentPreview}
            className={cn(
              "w-10 h-10 rounded-full flex items-center justify-center transition flex-shrink-0",
              sending || isUploading || attachmentPreview
                ? "bg-gray-100 text-gray-400 cursor-not-allowed"
                : "bg-gray-100 text-gray-500 hover:bg-gray-200 hover:text-gray-700"
            )}
            title="إرفاق ملف"
          >
            <Paperclip className="w-4 h-4" />
          </button>
          <button
            type="submit"
            disabled={(!input.trim() && !attachmentPreview) || sending || isUploading || input.length > 2000}
            className={cn(
              "w-10 h-10 rounded-full flex items-center justify-center transition flex-shrink-0",
              (input.trim() || attachmentPreview) && !sending && !isUploading && input.length <= 2000
                ? "bg-teal-500 text-white hover:bg-teal-600 active:bg-teal-700 shadow-sm"
                : "bg-gray-100 text-gray-400 cursor-not-allowed"
            )}
          >
            {sending ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Send className="w-4 h-4" />
            )}
          </button>
        </form>
        {input.length > 1800 && (
          <p className={cn(
            "text-[10px] mt-1 text-left",
            input.length > 2000 ? "text-red-500" : "text-amber-500"
          )} dir="ltr">
            {input.length}/2000
          </p>
        )}
      </div>
    </>
  );
}

// ─── Empty Chat Placeholder ───────────────────────────────────────────────────

function EmptyChatPlaceholder({ onNew }: { onNew: () => void }) {
  return (
    <div className="flex-1 flex items-center justify-center">
      <div className="text-center px-6">
        <div className="w-20 h-20 rounded-2xl bg-teal-50 flex items-center justify-center mx-auto mb-5">
          <MessageCircle className="w-10 h-10 text-teal-300" />
        </div>
        <p className="text-gray-600 text-base font-semibold mb-1">اختر محادثة للبدء</p>
        <p className="text-gray-400 text-sm mb-5">أو تواصل مع المركز مباشرة</p>
        <button
          onClick={onNew}
          className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-full bg-teal-500 text-white text-sm font-semibold hover:bg-teal-600 active:bg-teal-700 transition shadow-sm"
        >
          <Plus className="w-3.5 h-3.5" />
          محادثة جديدة
        </button>
      </div>
    </div>
  );
}

// ─── Message Bubble ───────────────────────────────────────────────────────────

function MessageBubble({
  message,
  isMine,
  onReply,
}: {
  message: PortalMessage;
  isMine: boolean;
  onReply?: () => void;
}) {
  if (message.isSystemMessage) {
    return (
      <div className="text-center py-1">
        <span className="text-[11px] text-gray-400 bg-gray-100 rounded-full px-3 py-1 inline-block">
          {message.content}
        </span>
      </div>
    );
  }

  const avatarColor = message.senderColor ?? "#0d9488";
  const avatarInitials = message.senderInitials ?? getInitials(message.senderName);

  return (
    <div className={cn("flex gap-2", isMine ? "flex-row-reverse" : "flex-row")}>
      {/* Staff avatar (only when not mine) */}
      {!isMine && (
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0 mt-auto"
          style={{ backgroundColor: avatarColor }}
        >
          {avatarInitials}
        </div>
      )}

      {/* Patient avatar placeholder (keeps spacing) */}
      {isMine && (
        <div className="w-8 h-8 rounded-full bg-teal-100 flex items-center justify-center flex-shrink-0 mt-auto">
          <User className="w-4 h-4 text-teal-600" />
        </div>
      )}

      <div className={cn("max-w-[75%] min-w-0 flex flex-col group", isMine ? "items-end" : "items-start")}>
        {/* Sender name (staff only) */}
        {!isMine && (
          <p className="text-[10px] font-semibold text-teal-700 mb-0.5 px-1">
            {message.senderName}
          </p>
        )}

        {/* Reply context */}
        {message.replyToContent && (
          <div className="text-xs px-3 py-1.5 rounded-lg mb-1 border-r-2 border-teal-400 bg-teal-50/70 max-w-full">
            <span className="font-semibold text-teal-600">{message.replyToSenderName}</span>
            <p className="text-gray-500 truncate">{message.replyToContent}</p>
          </div>
        )}

        {/* Bubble */}
        <div
          className={cn(
            "px-3.5 py-2.5 text-sm leading-relaxed break-words",
            isMine
              ? "bg-teal-500 text-white rounded-2xl rounded-br-sm"
              : "bg-white border border-gray-200 text-gray-800 rounded-2xl rounded-bl-sm shadow-xs"
          )}
        >
          {message.content}

          {/* Attachment rendering */}
          {message.attachmentUrl && (
            message.attachmentType?.startsWith("audio/") ? (
              <audio
                controls
                src={toFullUploadUrl(message.attachmentUrl)}
                className="mt-2 max-w-[220px] h-9 rounded"
                style={{ filter: isMine ? "invert(1) brightness(0.9)" : "none" }}
              />
            ) : message.attachmentType?.startsWith("image/") ? (
              <a
                href={toFullUploadUrl(message.attachmentUrl)}
                target="_blank"
                rel="noopener noreferrer"
                className="block mt-2"
              >
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={toFullUploadUrl(message.attachmentUrl)}
                  alt={message.attachmentName ?? "صورة مرفقة"}
                  className="max-w-[200px] rounded-lg cursor-pointer"
                />
              </a>
            ) : message.attachmentType === "application/pdf" ? (
              <div className={cn(
                "mt-2 p-2.5 rounded-lg flex items-center gap-2.5",
                isMine ? "bg-white/15" : "bg-gray-50 border border-gray-200"
              )}>
                <FileText className={cn("w-5 h-5 flex-shrink-0", isMine ? "text-white/80" : "text-red-500")} />
                <span className="text-xs truncate flex-1">{message.attachmentName ?? "مرفق PDF"}</span>
                <a
                  href={toFullUploadUrl(message.attachmentUrl)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={cn(
                    "text-xs font-semibold underline",
                    isMine ? "text-white/90 hover:text-white" : "text-teal-600 hover:text-teal-800"
                  )}
                >
                  فتح
                </a>
              </div>
            ) : (
              <div className={cn(
                "mt-2 p-2 rounded-lg flex items-center gap-2",
                isMine ? "bg-white/10" : "bg-gray-50"
              )}>
                <Paperclip className="w-4 h-4 flex-shrink-0" />
                <span className="text-xs truncate">{message.attachmentName ?? "مرفق"}</span>
              </div>
            )
          )}
        </div>

        {/* Timestamp + read status */}
        <div
          className={cn(
            "flex items-center gap-1 mt-0.5 px-1",
            isMine ? "flex-row-reverse" : "flex-row"
          )}
        >
          <span className="text-[10px] text-gray-400">
            {formatFullTime(message.createdAt)}
          </span>
          {isMine && (
            <CheckCheck
              className={cn(
                "w-3.5 h-3.5",
                message.isReadByMe ? "text-teal-500" : "text-gray-300"
              )}
            />
          )}
        </div>
        {/* Reply button on hover */}
        {onReply && !message.isSystemMessage && (
          <button
            onClick={onReply}
            className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-400 hover:text-teal-600 text-[10px] mt-0.5 flex items-center gap-0.5"
          >
            <Reply className="w-3 h-3" />
            رد
          </button>
        )}
      </div>
    </div>
  );
}

// ─── Start Conversation Dialog (Two-Step) ─────────────────────────────────────

function StartConversationDialog({
  open,
  onClose,
  onStart,
  loading,
}: {
  open: boolean;
  onClose: () => void;
  onStart: (payload: StartConversationPayload) => void;
  loading: boolean;
}) {
  const [step, setStep] = useState<1 | 2>(1);
  const [selectedType, setSelectedType] = useState<RecipientType | null>(null);
  const [selectedRecipient, setSelectedRecipient] = useState<PortalRecipient | null>(null);
  const [message, setMessage] = useState("");
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const { data: recipients = [], isLoading: recipientsLoading } = usePortalRecipients();

  // Reset state when dialog opens
  useEffect(() => {
    if (open) {
      setStep(1);
      setSelectedType(null);
      setSelectedRecipient(null);
      setMessage("");
    }
  }, [open]);

  // Focus textarea when moving to step 2
  useEffect(() => {
    if (step === 2) {
      const timer = setTimeout(() => textareaRef.current?.focus(), 100);
      return () => clearTimeout(timer);
    }
  }, [step]);

  if (!open) return null;

  const handleSelectType = (type: RecipientType) => {
    setSelectedType(type);
    // Find the matching recipient from the API data
    const matched = recipients.find((r) => r.type === type) ?? null;
    setSelectedRecipient(matched);
    setStep(2);
  };

  const handleBackToStep1 = () => {
    setStep(1);
    setSelectedType(null);
    setSelectedRecipient(null);
  };

  const handleSend = () => {
    const trimmed = message.trim();
    onStart({
      initialMessage: trimmed || undefined,
      recipientType: selectedType ?? undefined,
      recipientUserId: selectedRecipient?.userId ?? undefined,
    });
  };

  // Check if TreatingDoctor is available (has a userId)
  const treatingDoctorRecipient = recipients.find((r) => r.type === "TreatingDoctor");
  const isTreatingDoctorAvailable = !!treatingDoctorRecipient?.userId;

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-end sm:items-center justify-center p-4 animate-in fade-in duration-200">
      <div
        className="bg-white rounded-2xl w-full max-w-md shadow-2xl animate-in fade-in zoom-in-95 duration-200"
        dir="rtl"
      >
        {/* Header */}
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            {step === 2 && (
              <button
                onClick={handleBackToStep1}
                className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400 transition"
              >
                <ChevronLeft className="w-4 h-4 rotate-180" />
              </button>
            )}
            <div className="w-8 h-8 rounded-full bg-teal-100 flex items-center justify-center">
              <MessageCircle className="w-4 h-4 text-teal-600" />
            </div>
            <h3 className="font-bold text-gray-900">
              {step === 1 ? "تواصل مع المركز" : "اكتب رسالتك"}
            </h3>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400 transition"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Step 1: Recipient Selection */}
        {step === 1 && (
          <div className="p-5 space-y-3">
            <p className="text-sm text-gray-500 mb-4">
              اختر الجهة التي تريد التواصل معها
            </p>

            {recipientsLoading ? (
              <div className="flex flex-col items-center justify-center py-8 gap-3">
                <Loader2 className="w-6 h-6 animate-spin text-teal-500" />
                <p className="text-sm text-gray-400">جارٍ تحميل جهات الاتصال...</p>
              </div>
            ) : (
              <div className="space-y-2.5">
                {RECIPIENT_OPTIONS.map((opt) => {
                  const Icon = opt.icon;
                  const isDisabled = opt.type === "TreatingDoctor" && !isTreatingDoctorAvailable;

                  return (
                    <button
                      key={opt.type}
                      onClick={() => !isDisabled && handleSelectType(opt.type)}
                      disabled={isDisabled}
                      className={cn(
                        "w-full flex items-center gap-4 px-4 py-3.5 rounded-xl border-2 text-right transition-all duration-150",
                        isDisabled
                          ? "opacity-40 cursor-not-allowed border-gray-100 bg-gray-50"
                          : cn(opt.borderColor, opt.bgColor, "active:scale-[0.99]")
                      )}
                    >
                      <div
                        className={cn(
                          "w-11 h-11 rounded-full flex items-center justify-center flex-shrink-0",
                          opt.bgColor
                        )}
                      >
                        <Icon className={cn("w-5 h-5", opt.color)} />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className={cn("text-sm font-bold", isDisabled ? "text-gray-400" : opt.color)}>
                          {opt.label}
                        </p>
                        <p className="text-xs text-gray-500 mt-0.5">
                          {isDisabled
                            ? "لم يتم تحديد الطبيب المسؤول بعد، يمكنك التواصل مع الاستقبال."
                            : opt.description}
                        </p>
                      </div>
                      {isDisabled && (
                        <div className="flex-shrink-0">
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium bg-gray-100 text-gray-400">
                            غير متاح
                          </span>
                        </div>
                      )}
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {/* Step 2: Message Input */}
        {step === 2 && selectedType && (
          <div className="p-5 space-y-3">
            {/* Selected recipient chip */}
            <div className="flex items-center gap-2 mb-2">
              {(() => {
                const opt = RECIPIENT_OPTIONS.find((o) => o.type === selectedType);
                if (!opt) return null;
                const Icon = opt.icon;
                return (
                  <span
                    className={cn(
                      "inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold",
                      opt.bgColor,
                      opt.color,
                      "border",
                      opt.borderColor
                    )}
                  >
                    <Icon className="w-3.5 h-3.5" />
                    {opt.label}
                  </span>
                );
              })()}
              {selectedRecipient?.displayName && selectedType === "TreatingDoctor" && (
                <span className="text-xs text-gray-500">
                  — {selectedRecipient.displayName}
                </span>
              )}
            </div>

            <textarea
              ref={textareaRef}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder={
                selectedType === "TreatingDoctor"
                  ? "اكتب رسالتك للطبيب المسؤول..."
                  : selectedType === "Reception"
                    ? "اكتب استفسارك أو طلب حجز موعد..."
                    : "اكتب رسالتك للإدارة..."
              }
              rows={4}
              maxLength={2000}
              className="w-full px-4 py-3 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 resize-none transition"
            />
            <p className={cn(
              "text-[10px] text-left",
              message.length > 1800 ? "text-amber-500" : "text-gray-400"
            )} dir="ltr">
              {message.length}/2000
            </p>
          </div>
        )}

        {/* Footer */}
        {step === 2 && (
          <div className="px-5 pb-5">
            <button
              onClick={handleSend}
              disabled={loading}
              className="w-full py-3 rounded-xl font-semibold text-sm bg-teal-500 text-white hover:bg-teal-600 active:bg-teal-700 disabled:opacity-50 flex items-center justify-center gap-2 transition"
            >
              {loading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  جارٍ الإرسال...
                </>
              ) : (
                <>
                  <Send className="w-4 h-4" />
                  إرسال
                </>
              )}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
