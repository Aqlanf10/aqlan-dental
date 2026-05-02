"use client";
import { useState, useRef, useEffect, useCallback } from "react";
import {
  MessageCircle, Send, Plus, ArrowRight, Loader2,
  X, AlertTriangle, CheckCheck, User,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import {
  usePortalConversations,
  usePortalConversation,
  usePortalSendMessage,
  usePortalMarkAsRead,
  usePortalStartConversation,
  type PortalConversationListItem,
  type PortalConversationDetail,
  type PortalMessage,
} from "@/hooks/usePortalMessaging";

// ─── Helpers ─────────────────────────���────────────────────────────────────────

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

// ─── Main Page ────────────────────────────────���──────────────────────────���────

export default function PortalMessagesPage() {
  const { profile } = usePatientAuthStore();
  const [selectedConvId, setSelectedConvId] = useState<string | null>(null);
  const [showMobileChat, setShowMobileChat] = useState(false);
  const [showStartDialog, setShowStartDialog] = useState(false);
  const [patientUserId] = useState<string | null>(() => getPatientUserId());

  const { data: conversations = [], isLoading, isError, error } = usePortalConversations();
  const { data: conversation } = usePortalConversation(selectedConvId);
  const markAsRead = usePortalMarkAsRead(selectedConvId);
  const sendMessage = usePortalSendMessage(selectedConvId ?? "");
  const startConversation = usePortalStartConversation();

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
    (content: string) => {
      if (!selectedConvId) return;
      sendMessage.mutate(content);
    },
    [selectedConvId, sendMessage]
  );

  const handleStartConversation = useCallback(
    async (initialMessage?: string) => {
      try {
        const conv = await startConversation.mutateAsync(initialMessage);
        setSelectedConvId(conv.id);
        setShowStartDialog(false);
      } catch {
        // Conversation might already exist — try opening existing one
        setShowStartDialog(false);
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
          {conversations.filter((c) => c.unreadCount > 0).length > 0 && (
            <span className="bg-red-500 text-white text-xs font-bold rounded-full px-2 py-0.5 leading-none">
              {conversations.reduce((sum, c) => sum + c.unreadCount, 0)}
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
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50/80">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                المحادثات
              </p>
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
                conversations.map((conv) => (
                  <ConversationItem
                    key={conv.id}
                    conv={conv}
                    isSelected={conv.id === selectedConvId}
                    onClick={() => handleSelectConv(conv.id)}
                  />
                ))
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
      {showStartDialog && (
        <StartConversationDialog
          onClose={() => setShowStartDialog(false)}
          onStart={handleStartConversation}
          loading={startConversation.isPending}
        />
      )}
    </div>
  );
}

// ─── Conversation List States ────────────────────��────────────────────────────

function ConvListEmpty({ onNew }: { onNew: () => void }) {
  return (
    <div className="text-center py-14 px-4">
      <div className="w-14 h-14 rounded-full bg-teal-50 flex items-center justify-center mx-auto mb-3">
        <MessageCircle className="w-7 h-7 text-teal-400" />
      </div>
      <p className="text-sm font-semibold text-gray-700 mb-1">لا توجد محادثات</p>
      <p className="text-xs text-gray-400 mb-4">تواصل مع المركز للاستفسار أو الحجز</p>
      <button
        onClick={onNew}
        className="inline-flex items-center gap-1.5 px-4 py-2 rounded-full bg-teal-500 text-white text-sm font-semibold hover:bg-teal-600 transition"
      >
        <Plus className="w-3.5 h-3.5" />
        بدء محادثة
      </button>
    </div>
  );
}

function ConvListError({ error }: { error: unknown }) {
  const msg = (error as { message?: string })?.message;
  return (
    <div className="m-3 p-3 bg-amber-50 border border-amber-200 rounded-xl flex items-start gap-2">
      <AlertTriangle className="w-4 h-4 text-amber-500 mt-0.5 flex-shrink-0" />
      <p className="text-xs text-amber-700">{msg ?? "فشل تحميل المحادثات"}</p>
    </div>
  );
}

// ─── Conversation Item ───────────────────────────────��────────────────────────

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
  const displayName = conv.title || staffParticipant?.displayName || "المركز";
  const initial = getInitials(displayName);
  const avatarColor = staffParticipant?.color ?? "#0d9488";
  const avatarInitials = staffParticipant?.avatarInitials ?? initial;
  const hasUnread = conv.unreadCount > 0;

  return (
    <button
      onClick={onClick}
      className={cn(
        "w-full flex items-center gap-3 px-4 py-3.5 text-right transition-colors border-b border-gray-50",
        isSelected
          ? "bg-teal-50 border-r-4 border-r-teal-500"
          : "hover:bg-gray-50 active:bg-gray-100"
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

// ─── Chat Area ─────────────────────────��───────────────────────────���──────────

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
  onSend: (content: string) => void;
  sending: boolean;
  sendError?: string;
}) {
  const [input, setInput] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [conversation.messages.length]);

  const handleSend = () => {
    const trimmed = input.trim();
    if (!trimmed || sending || trimmed.length > 2000) return;
    onSend(trimmed);
    setInput("");
    inputRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  // Build display name for header
  const staffParticipants = conversation.participants.filter(
    (p) => p.userId !== patientUserId
  );
  const headerName =
    conversation.title ||
    (staffParticipants.length === 1
      ? staffParticipants[0].displayName ?? "المركز"
      : `${staffParticipants[0]?.displayName ?? "المركز"} و${staffParticipants.length - 1} آخرين`);

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
          <p className="text-xs text-teal-600">مركز عقلان للأسنان</p>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-2 bg-gray-50/30">
        {conversation.messages.length === 0 ? (
          <div className="text-center py-12">
            <MessageCircle className="w-10 h-10 text-gray-200 mx-auto mb-2" />
            <p className="text-sm text-gray-400">لا توجد رسائل بعد</p>
            <p className="text-xs text-gray-300 mt-1">ابدأ المحادثة بكتابة رسالة</p>
          </div>
        ) : (
          conversation.messages.map((msg) => (
            <MessageBubble
              key={msg.id}
              message={msg}
              isMine={patientUserId ? msg.senderId === patientUserId : false}
            />
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Send error */}
      {sendError && (
        <div className="px-4 py-2 bg-red-50 border-t border-red-200 text-red-700 text-xs flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
          {sendError}
        </div>
      )}

      {/* Input area */}
      <div className="px-4 py-3 border-t border-gray-100 bg-white">
        <div className="flex items-center gap-2">
          <input
            ref={inputRef}
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="اكتب رسالتك..."
            maxLength={2000}
            disabled={sending}
            className="flex-1 px-4 py-2.5 rounded-full border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-gray-50 disabled:opacity-50 transition"
          />
          <button
            onClick={handleSend}
            disabled={!input.trim() || sending || input.length > 2000}
            className={cn(
              "w-10 h-10 rounded-full flex items-center justify-center transition flex-shrink-0",
              input.trim() && !sending && input.length <= 2000
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
        </div>
        {input.length > 1800 && (
          <p className="text-[10px] text-amber-500 mt-1 text-left" dir="ltr">
            {input.length}/2000
          </p>
        )}
      </div>
    </>
  );
}

// ─── Empty Chat Placeholder ────────────────────────────────��──────────────────

function EmptyChatPlaceholder({ onNew }: { onNew: () => void }) {
  return (
    <div className="flex-1 flex items-center justify-center">
      <div className="text-center px-6">
        <div className="w-16 h-16 rounded-full bg-teal-50 flex items-center justify-center mx-auto mb-4">
          <MessageCircle className="w-8 h-8 text-teal-400" />
        </div>
        <p className="text-gray-500 text-base font-semibold mb-1">اختر محادثة للبدء</p>
        <p className="text-gray-400 text-sm mb-4">أو تواصل مع المركز مباشرة</p>
        <button
          onClick={onNew}
          className="inline-flex items-center gap-1.5 px-4 py-2 rounded-full bg-teal-500 text-white text-sm font-semibold hover:bg-teal-600 transition"
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
}: {
  message: PortalMessage;
  isMine: boolean;
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

      <div className={cn("max-w-[75%] min-w-0 flex flex-col", isMine ? "items-end" : "items-start")}>
        {/* Sender name (staff only) */}
        {!isMine && (
          <p className="text-[10px] font-semibold text-teal-700 mb-0.5 px-1">
            {message.senderName}
          </p>
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
      </div>
    </div>
  );
}

// ─── Start Conversation Dialog ─────────────────────��──────────────────────────

function StartConversationDialog({
  onClose,
  onStart,
  loading,
}: {
  onClose: () => void;
  onStart: (initialMessage?: string) => void;
  loading: boolean;
}) {
  const [message, setMessage] = useState("");

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-end sm:items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl" dir="rtl">
        {/* Header */}
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-teal-100 flex items-center justify-center">
              <MessageCircle className="w-4 h-4 text-teal-600" />
            </div>
            <h3 className="font-bold text-gray-900">تواصل مع المركز</h3>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400 transition"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-3">
          <p className="text-sm text-gray-500">
            اكتب رسالتك وسيتواصل معك الطاقم في أقرب وقت ممكن.
          </p>
          <textarea
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            placeholder="مثال: أريد حجز موعد... / عندي استفسار عن..."
            rows={4}
            maxLength={2000}
            autoFocus
            className="w-full px-4 py-3 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 resize-none transition"
          />
          {message.length > 1800 && (
            <p className="text-[10px] text-amber-500 text-left" dir="ltr">
              {message.length}/2000
            </p>
          )}
        </div>

        {/* Footer */}
        <div className="px-5 pb-5">
          <button
            onClick={() => onStart(message.trim() || undefined)}
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
      </div>
    </div>
  );
}
