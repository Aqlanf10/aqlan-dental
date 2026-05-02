"use client";
import { useState, useRef, useEffect, useCallback } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import {
  MessageCircle,
  Search,
  Plus,
  Send,
  Paperclip,
  Reply,
  X,
  Users,
  ArrowLeft,
  CheckCheck,
  AlertTriangle,
  User,
  Phone,
  Loader2,
  ExternalLink,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import {
  useConversations,
  useConversation,
  useCreateConversation,
  usePatientConversation,
  useSendMessage,
  useMarkAsRead,
  useUnreadCount,
} from "@/hooks/useMessaging";
import type {
  ConversationListItem,
  ConversationDetail,
  Message,
  ConversationFilter,
} from "@/types/messaging";
import api from "@/lib/api";

// ─── مستخدمو النظام (للمحادثة الجديدة) ───────────────────────────────────────
interface SystemUser {
  id: string;
  username: string;
  role: string;
  doctorName?: string;
  doctorColor?: string;
  doctorInitials?: string;
}

async function fetchUsers(): Promise<SystemUser[]> {
  const { data } = await api.get("/api/users/contacts");
  return Array.isArray(data) ? data : [];
}

// ─── تنسيق الوقت ──────────────────────────────────────────────────────────────
function formatTime(dateStr: string) {
  const d = new Date(dateStr);
  const now = new Date();
  const diff = now.getTime() - d.getTime();
  const mins = Math.floor(diff / 60000);
  const hours = Math.floor(mins / 60);
  const days = Math.floor(hours / 24);

  if (mins < 1) return "الآن";
  if (mins < 60) return `منذ ${mins} د`;
  if (hours < 24)
    return d.toLocaleTimeString("ar-SA", { hour: "2-digit", minute: "2-digit" });
  if (days < 7) return `منذ ${days} ي`;
  return d.toLocaleDateString("ar-SA", { month: "short", day: "numeric" });
}

function formatFullTime(dateStr: string) {
  const d = new Date(dateStr);
  return d.toLocaleString("ar-SA", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// ─── مكونات الفلتر ────────────────────────────────────────────────────────────
const FILTER_OPTIONS: { value: ConversationFilter; label: string }[] = [
  { value: "all", label: "الكل" },
  { value: "unread", label: "غير مقروء" },
  { value: "StaffToStaff", label: "موظفين" },
  { value: "StaffToPatient", label: "مرضى" },
];

const ROLE_LABELS: Record<string, string> = {
  Admin: "مدير",
  Orthodontist: "تقويم",
  GeneralDentist: "أسنان عام",
  OralSurgeon: "جراح",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

// ─── المكون الرئيسي ──────────────────────────────────────────────────────────
export default function MessagesPage() {
  const { user } = useAuthStore();
  const searchParams = useSearchParams();
  const router = useRouter();
  const patientIdFromUrl = searchParams?.get("patientId");

  const [selectedConvId, setSelectedConvId] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [activeFilter, setActiveFilter] = useState<ConversationFilter>("all");
  const [showNewChat, setShowNewChat] = useState(false);
  const [isMobileDetail, setIsMobileDetail] = useState(false);
  const [patientConvError, setPatientConvError] = useState("");

  const { data: convData, isLoading: convLoading } = useConversations(
    1,
    searchQuery || undefined,
    activeFilter
  );
  const { data: conversation } = useConversation(selectedConvId);
  const sendMessage = useSendMessage(selectedConvId ?? "");
  const markAsRead = useMarkAsRead(selectedConvId ?? "");
  const { data: unreadData } = useUnreadCount();
  const patientConv = usePatientConversation();

  const conversations = convData?.data ?? [];

  // Auto-open patient conversation if navigated with ?patientId=
  useEffect(() => {
    if (patientIdFromUrl && !selectedConvId) {
      patientConv.mutate(patientIdFromUrl, {
        onSuccess: (conv) => {
          setSelectedConvId(conv.id);
          setIsMobileDetail(true);
        },
        onError: () => {
          setPatientConvError("فشل فتح محادثة المريض");
        },
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientIdFromUrl]);

  // Mark as read when selecting a conversation
  useEffect(() => {
    if (selectedConvId) {
      markAsRead.mutate();
      setIsMobileDetail(true);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedConvId]);

  const handleSelectConv = useCallback((id: string) => {
    setSelectedConvId(id);
    setPatientConvError("");
  }, []);

  const handleBack = useCallback(() => {
    setIsMobileDetail(false);
    setSelectedConvId(null);
  }, []);

  return (
    <div className="h-[calc(100vh-4rem)] flex gap-4" dir="rtl">
      {/* Patient conversation error toast */}
      {patientConvError && (
        <div className="fixed top-4 left-1/2 -translate-x-1/2 z-50 bg-amber-50 border border-amber-300 text-amber-800 rounded-lg px-4 py-2 text-sm shadow-lg flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 flex-shrink-0" />
          {patientConvError}
          <button
            onClick={() => setPatientConvError("")}
            className="text-amber-600 hover:text-amber-800 ms-2"
          >
            <X className="w-3 h-3" />
          </button>
        </div>
      )}

      {/* ─── قائمة المحادثات ──────────────────────────────────────────── */}
      <div
        className={cn(
          "w-full md:w-80 lg:w-96 flex-shrink-0 bg-white rounded-xl border border-gray-200 flex flex-col overflow-hidden",
          isMobileDetail ? "hidden md:flex" : "flex"
        )}
      >
        {/* Header */}
        <div className="p-4 border-b border-gray-100">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <MessageCircle className="w-5 h-5 text-clinic-blue" />
              <h2 className="text-lg font-bold text-gray-900">الرسائل</h2>
              {unreadData && unreadData.totalUnread > 0 && (
                <span className="bg-red-500 text-white text-xs font-bold rounded-full px-2 py-0.5">
                  {unreadData.totalUnread > 99 ? "99+" : unreadData.totalUnread}
                </span>
              )}
            </div>
            <button
              onClick={() => setShowNewChat(true)}
              className="w-9 h-9 rounded-lg bg-clinic-blue text-white flex items-center justify-center hover:opacity-90 transition"
              title="محادثة جديدة"
            >
              <Plus className="w-5 h-5" />
            </button>
          </div>

          {/* Search */}
          <div className="relative mb-3">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="بحث بالاسم، رقم المريض، أو نص الرسالة..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pr-10 pl-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue bg-gray-50"
            />
          </div>

          {/* Filters */}
          <div className="flex gap-1.5 flex-wrap">
            {FILTER_OPTIONS.map((opt) => (
              <button
                key={opt.value}
                onClick={() => setActiveFilter(opt.value)}
                className={cn(
                  "px-3 py-1 rounded-full text-xs font-medium transition-colors",
                  activeFilter === opt.value
                    ? "bg-clinic-blue text-white"
                    : "bg-gray-100 text-gray-600 hover:bg-gray-200"
                )}
              >
                {opt.label}
                {opt.value === "unread" && unreadData && unreadData.unreadConversations > 0 && (
                  <span className="ms-1 bg-red-500 text-white rounded-full px-1.5 py-0.5 text-[10px]">
                    {unreadData.unreadConversations}
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>

        {/* Conversations list */}
        <div className="flex-1 overflow-y-auto">
          {convLoading ? (
            <div className="flex flex-col items-center justify-center py-12 gap-3">
              <Loader2 className="w-6 h-6 animate-spin text-clinic-blue" />
              <p className="text-sm text-gray-400">جارٍ تحميل المحادثات...</p>
            </div>
          ) : conversations.length === 0 ? (
            <div className="text-center py-12 px-4">
              <MessageCircle className="w-12 h-12 text-gray-300 mx-auto mb-3" />
              <p className="text-gray-500 text-sm">
                {searchQuery || activeFilter !== "all"
                  ? "لا توجد نتائج مطابقة"
                  : "لا توجد محادثات بعد"}
              </p>
              <p className="text-gray-400 text-xs mt-1">
                {searchQuery || activeFilter !== "all"
                  ? "جرّب تغيير البحث أو الفلتر"
                  : "اضغط + لبدء محادثة جديدة"}
              </p>
            </div>
          ) : (
            conversations.map((conv) => (
              <ConversationItem
                key={conv.id}
                conv={conv}
                isSelected={conv.id === selectedConvId}
                currentUserId={user?.id ?? ""}
                onClick={() => handleSelectConv(conv.id)}
              />
            ))
          )}
        </div>
      </div>

      {/* ─── منطقة الرسائل ──────────────────────────────────────────────── */}
      <div
        className={cn(
          "flex-1 bg-white rounded-xl border border-gray-200 flex flex-col overflow-hidden",
          !isMobileDetail ? "hidden md:flex" : "flex"
        )}
      >
        {selectedConvId && conversation ? (
          <ChatArea
            conversation={conversation}
            currentUserId={user?.id ?? ""}
            onBack={handleBack}
            onSend={(content) => sendMessage.mutate({ content })}
            sending={sendMessage.isPending}
            onOpenPatient={(patientId) => router.push(`/patients/${patientId}`)}
            sendError={
              sendMessage.isError
                ? "فشل إرسال الرسالة — تحقق من الاتصال وحاول مجدداً"
                : undefined
            }
          />
        ) : (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <MessageCircle className="w-16 h-16 text-gray-200 mx-auto mb-4" />
              <p className="text-gray-400 text-lg">اختر محادثة للبدء</p>
              <p className="text-gray-300 text-sm mt-1">
                أو أنشئ محادثة جديدة بالضغط على +
              </p>
            </div>
          </div>
        )}
      </div>

      {/* ─── نافذة محادثة جديدة ────────────────────────────────────────── */}
      {showNewChat && (
        <NewChatDialog
          currentUserId={user?.id ?? ""}
          onClose={() => setShowNewChat(false)}
          onCreated={(id) => {
            setSelectedConvId(id);
            setShowNewChat(false);
          }}
        />
      )}
    </div>
  );
}

// ─── عنصر المحادثة في القائمة ───────────────────────────────────────────────
function ConversationItem({
  conv,
  isSelected,
  onClick,
}: {
  conv: ConversationListItem;
  isSelected: boolean;
  currentUserId: string;
  onClick: () => void;
}) {
  const other = conv.otherParticipant;
  const isPatientConv = conv.conversationType === "StaffToPatient";
  const hasUnread = conv.unreadCount > 0;

  return (
    <button
      onClick={onClick}
      className={cn(
        "w-full flex items-center gap-3 px-4 py-3 text-right transition-colors border-b border-gray-50",
        isSelected
          ? "bg-clinic-blue/5 border-r-4 border-r-clinic-blue"
          : "hover:bg-gray-50"
      )}
    >
      {/* Avatar */}
      <div className="relative flex-shrink-0">
        {isPatientConv ? (
          <div className="w-11 h-11 rounded-full bg-clinic-orange/15 flex items-center justify-center">
            <User className="w-5 h-5 text-clinic-orange" />
          </div>
        ) : conv.isGroup ? (
          <div className="w-11 h-11 rounded-full bg-gray-200 flex items-center justify-center">
            <Users className="w-5 h-5 text-gray-500" />
          </div>
        ) : (
          <div
            className="w-11 h-11 rounded-full flex items-center justify-center text-white text-sm font-bold"
            style={{ backgroundColor: other?.color ?? "#6B7280" }}
          >
            {other?.avatarInitials ?? other?.displayName?.charAt(1) ?? "?"}
          </div>
        )}
        {/* Unread dot on avatar */}
        {hasUnread && !isSelected && (
          <span className="absolute -top-0.5 -right-0.5 w-3 h-3 bg-red-500 border-2 border-white rounded-full" />
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between gap-1">
          <div className="flex items-center gap-1.5 min-w-0">
            <span
              className={cn(
                "text-sm truncate",
                hasUnread ? "font-bold text-gray-900" : "font-semibold text-gray-800"
              )}
            >
              {conv.title}
            </span>
            {/* Patient-facing conversation badge */}
            {isPatientConv && (
              <span className="flex-shrink-0 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-clinic-orange/15 text-clinic-orange leading-none">
                مريض
              </span>
            )}
          </div>
          <div className="flex items-center gap-1.5 flex-shrink-0">
            {conv.lastMessageAt && (
              <span className="text-[10px] text-gray-400">
                {formatTime(conv.lastMessageAt)}
              </span>
            )}
            {hasUnread && (
              <span className="bg-clinic-blue text-white text-[10px] font-bold rounded-full min-w-[18px] h-[18px] flex items-center justify-center px-1 flex-shrink-0">
                {conv.unreadCount > 9 ? "9+" : conv.unreadCount}
              </span>
            )}
          </div>
        </div>
        {/* Patient number + name row */}
        {isPatientConv && (conv.patientNumber || conv.patientName) && (
          <p className="text-[10px] text-clinic-orange font-medium mt-0.5 flex items-center gap-1">
            {conv.patientNumber && <span>#{conv.patientNumber}</span>}
            {conv.patientName && conv.patientNumber && <span className="text-gray-300">·</span>}
            {conv.patientName && <span>{conv.patientName}</span>}
          </p>
        )}
        <p
          className={cn(
            "text-xs mt-0.5 truncate",
            hasUnread ? "text-gray-700 font-medium" : "text-gray-500"
          )}
        >
          {conv.lastMessagePreview ?? "لا توجد رسائل"}
        </p>
      </div>
    </button>
  );
}

// ─── منطقة الدردشة ──────────────────────────────────────────────────────────
function ChatArea({
  conversation,
  currentUserId,
  onBack,
  onSend,
  sending,
  onOpenPatient,
  sendError,
}: {
  conversation: ConversationDetail;
  currentUserId: string;
  onBack: () => void;
  onSend: (content: string) => void;
  sending: boolean;
  onOpenPatient: (patientId: string) => void;
  sendError?: string;
}) {
  const [input, setInput] = useState("");
  const [replyTo, setReplyTo] = useState<Message | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [conversation.messages]);

  const handleSend = () => {
    const trimmed = input.trim();
    if (!trimmed || sending) return;
    if (trimmed.length > 2000) return; // frontend validation
    onSend(trimmed);
    setInput("");
    setReplyTo(null);
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const otherParticipants = conversation.participants.filter(
    (p) => p.userId !== currentUserId
  );
  const isPatientConv = conversation.conversationType === "StaffToPatient";

  const title = isPatientConv
    ? conversation.patientName
      ? `المريض: ${conversation.patientName}`
      : conversation.title
    : conversation.isGroup
      ? conversation.title
      : otherParticipants[0]?.displayName ?? conversation.title;

  return (
    <>
      {/* Chat header */}
      <div className="px-4 py-3 border-b border-gray-100 flex items-center gap-3">
        <button
          onClick={onBack}
          className="md:hidden w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-500"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>

        {isPatientConv ? (
          <div className="w-10 h-10 rounded-full bg-clinic-orange/15 flex items-center justify-center">
            <User className="w-5 h-5 text-clinic-orange" />
          </div>
        ) : conversation.isGroup ? (
          <div className="w-10 h-10 rounded-full bg-gray-200 flex items-center justify-center">
            <Users className="w-5 h-5 text-gray-500" />
          </div>
        ) : (
          <div
            className="w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-bold"
            style={{ backgroundColor: otherParticipants[0]?.color ?? "#6B7280" }}
          >
            {otherParticipants[0]?.avatarInitials ?? "?"}
          </div>
        )}

        <div className="flex-1 min-w-0">
          <h3 className="font-semibold text-gray-900 text-sm truncate">{title}</h3>
          {isPatientConv && conversation.patientNumber && (
            <div className="flex items-center gap-2 mt-0.5">
              <span className="text-xs text-clinic-orange font-medium">
                #{conversation.patientNumber}
              </span>
              {conversation.patientPhone && (
                <span className="text-xs text-gray-400 flex items-center gap-1">
                  <Phone className="w-3 h-3" />
                  {conversation.patientPhone}
                </span>
              )}
            </div>
          )}
          {!isPatientConv && !conversation.isGroup && (
            <p className="text-xs text-gray-400">
              {ROLE_LABELS[otherParticipants[0]?.role ?? ""] ?? otherParticipants[0]?.role ?? "متصل"}
            </p>
          )}
          {conversation.isGroup && !isPatientConv && (
            <p className="text-xs text-gray-400">
              {conversation.participants.length} مشارك
            </p>
          )}
        </div>

        {/* Open patient file button */}
        {isPatientConv && conversation.patientId && (
          <button
            onClick={() => onOpenPatient(conversation.patientId!)}
            className="w-9 h-9 rounded-lg bg-clinic-navy/5 hover:bg-clinic-navy/10 flex items-center justify-center text-clinic-navy transition"
            title="فتح ملف المريض"
          >
            <ExternalLink className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-3 bg-gray-50/50">
        {conversation.messages.length === 0 && (
          <div className="text-center py-8">
            <MessageCircle className="w-10 h-10 text-gray-300 mx-auto mb-2" />
            <p className="text-sm text-gray-400">لا توجد رسائل بعد</p>
            <p className="text-xs text-gray-300">ابدأ المحادثة بكتابة رسالة</p>
          </div>
        )}
        {conversation.messages.map((msg) => (
          <MessageBubble
            key={msg.id}
            message={msg}
            isMine={msg.senderId === currentUserId}
            onReply={() => setReplyTo(msg)}
          />
        ))}
        <div ref={messagesEndRef} />
      </div>

      {/* Send error */}
      {sendError && (
        <div className="px-4 py-2 bg-red-50 border-t border-red-200 text-red-700 text-xs flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
          {sendError}
        </div>
      )}

      {/* Reply preview */}
      {replyTo && (
        <div className="px-4 py-2 bg-gray-50 border-t border-gray-200 flex items-center gap-2">
          <Reply className="w-4 h-4 text-clinic-blue flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-clinic-blue">
              {replyTo.senderName}
            </p>
            <p className="text-xs text-gray-500 truncate">{replyTo.content}</p>
          </div>
          <button
            onClick={() => setReplyTo(null)}
            className="text-gray-400 hover:text-gray-600"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Input area */}
      <div className="px-4 py-3 border-t border-gray-100 bg-white">
        <div className="flex items-end gap-2">
          <textarea
            ref={inputRef}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="اكتب رسالتك..."
            rows={1}
            maxLength={2000}
            className="flex-1 resize-none border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue max-h-32"
            style={{ minHeight: "40px" }}
          />
          <button
            onClick={handleSend}
            disabled={!input.trim() || sending || input.length > 2000}
            className={cn(
              "w-10 h-10 rounded-lg flex items-center justify-center transition flex-shrink-0",
              input.trim() && !sending && input.length <= 2000
                ? "bg-clinic-blue text-white hover:opacity-90"
                : "bg-gray-100 text-gray-400 cursor-not-allowed"
            )}
          >
            {sending ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Send className="w-4 h-4" />
            )}
          </button>
        </div>
        {input.length > 1800 && (
          <p className="text-xs text-amber-500 mt-1 text-left" dir="ltr">
            {input.length}/2000
          </p>
        )}
      </div>
    </>
  );
}

// ─── فقاعة الرسالة ──────────────────────────────────────────────────────────
function MessageBubble({
  message,
  isMine,
  onReply,
}: {
  message: Message;
  isMine: boolean;
  onReply: () => void;
}) {
  if (message.isSystemMessage) {
    return (
      <div className="text-center">
        <span className="text-xs text-gray-400 bg-gray-100 rounded-full px-3 py-1">
          {message.content}
        </span>
      </div>
    );
  }

  return (
    <div
      className={cn(
        "flex gap-2 group",
        isMine ? "flex-row-reverse" : "flex-row"
      )}
    >
      {/* Avatar */}
      {!isMine && (
        <div
          className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0 mt-auto"
          style={{ backgroundColor: message.senderColor ?? "#6B7280" }}
        >
          {message.senderInitials ?? message.senderName.charAt(0)}
        </div>
      )}

      <div className={cn("max-w-[75%] min-w-0", isMine ? "items-end" : "items-start")}>
        {/* Reply reference */}
        {message.replyToContent && (
          <div
            className={cn(
              "text-xs px-3 py-1.5 rounded-lg mb-1 border-r-2 border-clinic-blue bg-gray-100",
              isMine ? "text-left" : "text-right"
            )}
          >
            <span className="font-semibold text-clinic-blue">
              {message.replyToSenderName}
            </span>
            <p className="text-gray-500 truncate">{message.replyToContent}</p>
          </div>
        )}

        {/* Message bubble */}
        <div
          className={cn(
            "px-3 py-2 rounded-2xl text-sm relative",
            isMine
              ? "bg-clinic-blue text-white rounded-br-md"
              : "bg-white border border-gray-200 text-gray-800 rounded-bl-md"
          )}
        >
          {/* Show sender name in group conversations */}
          {!isMine && (
            <p className="text-xs font-semibold text-clinic-blue mb-0.5">
              {message.senderName}
            </p>
          )}
          <p className="whitespace-pre-wrap break-words leading-relaxed">
            {message.content}
          </p>

          {/* Attachment */}
          {message.attachmentUrl && (
            <div
              className={cn(
                "mt-2 p-2 rounded-lg flex items-center gap-2",
                isMine ? "bg-white/10" : "bg-gray-50"
              )}
            >
              <Paperclip className="w-4 h-4 flex-shrink-0" />
              <span className="text-xs truncate">
                {message.attachmentName ?? "مرفق"}
              </span>
            </div>
          )}
        </div>

        {/* Time + read status */}
        <div
          className={cn(
            "flex items-center gap-1 mt-1 px-1",
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
                message.isReadByMe ? "text-clinic-blue" : "text-gray-300"
              )}
            />
          )}
        </div>

        {/* Reply button (on hover) */}
        <button
          onClick={onReply}
          className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-400 hover:text-clinic-blue text-xs mt-0.5 flex items-center gap-1"
        >
          <Reply className="w-3 h-3" />
          رد
        </button>
      </div>
    </div>
  );
}

// ─── نافذة محادثة جديدة ──────────────────────────────────────────────────────
function NewChatDialog({
  currentUserId,
  onClose,
  onCreated,
}: {
  currentUserId: string;
  onClose: () => void;
  onCreated: (id: string) => void;
}) {
  const [users, setUsers] = useState<SystemUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [title, setTitle] = useState("");
  const [initialMsg, setInitialMsg] = useState("");
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");
  const createConv = useCreateConversation();

  useEffect(() => {
    fetchUsers()
      .then((u) => setUsers(u.filter((x) => x.id !== currentUserId)))
      .finally(() => setLoading(false));
  }, [currentUserId]);

  const filtered = users.filter(
    (u) =>
      u.doctorName?.includes(search) ||
      u.username.includes(search) ||
      u.role.includes(search)
  );

  const toggleUser = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleCreate = async () => {
    if (selected.size === 0) return;
    const isGroup = selected.size > 1;
    try {
      setError("");
      const result = await createConv.mutateAsync({
        participantIds: Array.from(selected),
        isGroup,
        title: isGroup ? title : undefined,
        initialMessage: initialMsg || undefined,
      });
      onCreated(result.id);
    } catch {
      setError("فشل إنشاء المحادثة — تحقق من الصلاحيات وحاول مجدداً");
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-md max-h-[90vh] flex flex-col shadow-2xl">
        {/* Header */}
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 className="font-bold text-gray-900">محادثة جديدة</h3>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-5 space-y-4">
          {/* Search */}
          <div className="relative">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="بحث عن مستخدم..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pr-10 pl-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue"
            />
          </div>

          {/* Group title (if multiple selected) */}
          {selected.size > 1 && (
            <input
              type="text"
              placeholder="اسم المجموعة (اختياري)"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue"
            />
          )}

          {/* Users list */}
          {loading ? (
            <div className="flex flex-col items-center justify-center py-8 gap-2">
              <Loader2 className="w-6 h-6 animate-spin text-clinic-blue" />
              <p className="text-sm text-gray-400">جارٍ تحميل المستخدمين...</p>
            </div>
          ) : filtered.length === 0 ? (
            <div className="text-center py-8">
              <Users className="w-10 h-10 text-gray-300 mx-auto mb-2" />
              <p className="text-sm text-gray-400">لا يوجد مستخدمون مطابقون</p>
            </div>
          ) : (
            <div className="space-y-1">
              {filtered.map((u) => (
                <button
                  key={u.id}
                  onClick={() => toggleUser(u.id)}
                  className={cn(
                    "w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-right transition-colors",
                    selected.has(u.id)
                      ? "bg-clinic-blue/10 border border-clinic-blue/30"
                      : "hover:bg-gray-50 border border-transparent"
                  )}
                >
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
                    style={{ backgroundColor: u.doctorColor ?? "#6B7280" }}
                  >
                    {u.doctorInitials ?? u.username.charAt(0).toUpperCase()}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-900 truncate">
                      {u.doctorName ?? u.username}
                    </p>
                    <p className="text-xs text-gray-400">
                      {ROLE_LABELS[u.role] ?? u.role}
                    </p>
                  </div>
                  {selected.has(u.id) && (
                    <div className="w-5 h-5 bg-clinic-blue rounded-full flex items-center justify-center">
                      <svg
                        className="w-3 h-3 text-white"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          strokeWidth={3}
                          d="M5 13l4 4L19 7"
                        />
                      </svg>
                    </div>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Error */}
        {error && (
          <div className="px-5 py-2 bg-red-50 border-t border-red-200 text-red-700 text-xs">
            {error}
          </div>
        )}

        {/* Initial message + Create */}
        <div className="px-5 py-4 border-t border-gray-100 space-y-3">
          <textarea
            placeholder="رسالة أولية (اختياري)"
            value={initialMsg}
            onChange={(e) => setInitialMsg(e.target.value)}
            rows={2}
            maxLength={2000}
            className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none"
          />
          <button
            onClick={handleCreate}
            disabled={selected.size === 0 || createConv.isPending}
            className={cn(
              "w-full py-2.5 rounded-lg font-semibold text-sm transition",
              selected.size > 0 && !createConv.isPending
                ? "bg-clinic-blue text-white hover:opacity-90"
                : "bg-gray-100 text-gray-400 cursor-not-allowed"
            )}
          >
            {createConv.isPending
              ? "جارٍ الإنشاء..."
              : selected.size === 0
                ? "اختر مستخدم"
                : selected.size === 1
                  ? "بدء المحادثة"
                  : `إنشاء مجموعة (${selected.size} مشارك)`}
          </button>
        </div>
      </div>
    </div>
  );
}
