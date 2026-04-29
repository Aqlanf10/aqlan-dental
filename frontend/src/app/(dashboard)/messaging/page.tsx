"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import {
  Send,
  Search,
  Plus,
  MessageCircle,
  X,
  Users,
  User,
  ChevronLeft,
  Hash,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import {
  useConversations,
  useMessages,
  useDoctorsForMessaging,
  useCreateConversation,
  useSendMessage,
  useMarkAsRead,
  useMessageUnreadCount,
} from "@/hooks/useMessaging";
import type { Conversation, Message } from "@/types/messaging";

// ── Helper: format time ────────────────────────────────────────────────────
function formatTime(dateStr: string) {
  const d = new Date(dateStr);
  const now = new Date();
  const diff = now.getTime() - d.getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "الآن";
  if (mins < 60) return `منذ ${mins} دقيقة`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `منذ ${hrs} ساعة`;
  const days = Math.floor(hrs / 24);
  if (days < 7) return `منذ ${days} يوم`;
  return d.toLocaleDateString("ar-YE");
}

function formatMessageTime(dateStr: string) {
  const d = new Date(dateStr);
  return d.toLocaleTimeString("ar-SA", { hour: "2-digit", minute: "2-digit" });
}

// ── Role label map ─────────────────────────────────────────────────────────
const roleLabels: Record<string, string> = {
  Admin: "مدير",
  Orthodontist: "تقويم أسنان",
  GeneralDentist: "طبيب أسنان عام",
  OralSurgeon: "جراح فكين",
  Reception: "استقبال",
  Accountant: "محاسب",
  Assistant: "مساعد",
  BranchManager: "مدير فرع",
};

const roleColors: Record<string, string> = {
  Admin: "bg-purple-100 text-purple-700",
  Orthodontist: "bg-blue-100 text-blue-700",
  GeneralDentist: "bg-green-100 text-green-700",
  OralSurgeon: "bg-red-100 text-red-700",
  Reception: "bg-amber-100 text-amber-700",
  Accountant: "bg-sky-100 text-sky-700",
  Assistant: "bg-gray-100 text-gray-700",
  BranchManager: "bg-indigo-100 text-indigo-700",
};

// ══════════════════════════════════════════════════════════════════════════
// Main Component
// ══════════════════════════════════════════════════════════════════════════
export default function MessagingPage() {
  const [selectedConvId, setSelectedConvId] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState("");
  const [showNewConvModal, setShowNewConvModal] = useState(false);
  const [isMobileDetail, setIsMobileDetail] = useState(false);

  const { data: conversations, isLoading: convLoading } = useConversations();
  const { data: unreadCount } = useMessageUnreadCount();

  // Filter conversations by search
  const filteredConvs = (conversations ?? []).filter(
    (c) =>
      !searchTerm ||
      c.title?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      c.participants.some(
        (p) =>
          p.fullName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
          p.username?.toLowerCase().includes(searchTerm.toLowerCase())
      ) ||
      c.lastMessage?.content.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const selectedConv = conversations?.find((c) => c.id === selectedConvId);

  const handleSelectConv = useCallback(
    (id: string) => {
      setSelectedConvId(id);
      setIsMobileDetail(true);
    },
    []
  );

  return (
    <div className="space-y-4" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-extrabold text-[#0d2137]">الرسائل</h2>
          <p className="text-sm text-gray-500 mt-1">
            التواصل الداخلي بين الكادر والأطباء
            {(unreadCount ?? 0) > 0 && (
              <span className="ms-2 inline-flex items-center px-2 py-0.5 rounded-full text-xs font-bold bg-[#3d7ab5] text-white">
                {unreadCount} غير مقروء
              </span>
            )}
          </p>
        </div>
        <button
          onClick={() => setShowNewConvModal(true)}
          className="flex items-center gap-2 px-5 py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors shadow-sm"
        >
          <Plus className="w-4 h-4" />
          محادثة جديدة
        </button>
      </div>

      {/* Chat Layout */}
      <div className="flex gap-4 h-[calc(100vh-180px)] min-h-[500px]">
        {/* Conversations List */}
        <div
          className={`${
            isMobileDetail ? "hidden lg:flex" : "flex"
          } flex-col w-full lg:w-80 flex-shrink-0 rounded-xl border overflow-hidden`}
          style={{
            backgroundColor: "#ffffff",
            borderColor: "#e8f0f9",
            boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
          }}
        >
          {/* Search */}
          <div className="p-3 border-b" style={{ borderColor: "#e8f0f9" }}>
            <div className="relative">
              <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 right-3 text-gray-400" />
              <input
                type="text"
                placeholder="بحث في المحادثات..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full h-9 pr-9 pl-3 text-sm rounded-lg border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 focus:border-[#3d7ab5]"
              />
            </div>
          </div>

          {/* List */}
          <div className="flex-1 overflow-y-auto">
            {convLoading ? (
              <div className="p-4 space-y-3">
                {[1, 2, 3, 4, 5].map((i) => (
                  <div key={i} className="animate-pulse h-16 bg-gray-100 rounded-lg" />
                ))}
              </div>
            ) : filteredConvs.length === 0 ? (
              <div className="text-center py-16 px-4">
                <MessageCircle className="w-12 h-12 mx-auto text-gray-200 mb-3" />
                <p className="text-gray-400 text-sm font-bold">لا توجد محادثات</p>
                <p className="text-gray-300 text-xs mt-1">ابدأ محادثة جديدة مع زميلك</p>
              </div>
            ) : (
              filteredConvs.map((conv) => (
                <ConversationItem
                  key={conv.id}
                  conversation={conv}
                  isSelected={conv.id === selectedConvId}
                  onClick={() => handleSelectConv(conv.id)}
                />
              ))
            )}
          </div>
        </div>

        {/* Chat Area */}
        <div
          className={`${
            isMobileDetail ? "flex" : "hidden lg:flex"
          } flex-col flex-1 rounded-xl border overflow-hidden`}
          style={{
            backgroundColor: "#ffffff",
            borderColor: "#e8f0f9",
            boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
          }}
        >
          {selectedConvId ? (
            <ChatArea
              conversation={selectedConv!}
              onBack={() => setIsMobileDetail(false)}
            />
          ) : (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <MessageCircle className="w-16 h-16 mx-auto text-gray-200 mb-4" />
                <p className="text-gray-400 text-lg font-bold">اختر محادثة للبدء</p>
                <p className="text-gray-300 text-sm mt-1">
                  أو أنشئ محادثة جديدة مع زميلك
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* New Conversation Modal */}
      {showNewConvModal && (
        <NewConversationModal onClose={() => setShowNewConvModal(false)} />
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Conversation List Item
// ══════════════════════════════════════════════════════════════════════════
function ConversationItem({
  conversation,
  isSelected,
  onClick,
}: {
  conversation: Conversation;
  isSelected: boolean;
  onClick: () => void;
}) {
  const otherParticipant = conversation.participants.find(
    (p) => p.userId !== conversation.createdBy
  ) ?? conversation.participants[0];

  const displayName =
    conversation.type === "Group"
      ? conversation.title ?? "محادثة جماعية"
      : otherParticipant?.fullName ?? otherParticipant?.username ?? "مستخدم";

  const displayRole =
    conversation.type === "Direct"
      ? otherParticipant?.role
      : undefined;

  return (
    <button
      onClick={onClick}
      className={`w-full text-start p-3 border-b transition-all hover:bg-[#f0f5fb] ${
        isSelected ? "bg-[#f0f5fb] border-r-4 border-r-[#3d7ab5]" : "border-r-4 border-r-transparent"
      }`}
      style={{ borderColor: "#e8f0f9" }}
    >
      <div className="flex items-start gap-3">
        {/* Avatar */}
        <div
          className={`w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 ${
            conversation.type === "Group"
              ? "bg-[#f5922e]/10 text-[#f5922e]"
              : "bg-[#3d7ab5]/10 text-[#3d7ab5]"
          }`}
        >
          {conversation.type === "Group" ? (
            <Users className="w-5 h-5" />
          ) : (
            <User className="w-5 h-5" />
          )}
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-0.5">
            <span className="text-sm font-bold text-[#0d2137] truncate">
              {displayName}
            </span>
            {displayRole && (
              <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${roleColors[displayRole] ?? "bg-gray-100 text-gray-600"}`}>
                {roleLabels[displayRole] ?? displayRole}
              </span>
            )}
            {conversation.unreadCount > 0 && (
              <span className="ms-auto inline-flex items-center justify-center w-5 h-5 rounded-full text-[10px] font-bold bg-[#3d7ab5] text-white">
                {conversation.unreadCount}
              </span>
            )}
          </div>
          {conversation.lastMessage && (
            <p className="text-xs text-gray-500 truncate">
              {conversation.lastMessage.content}
            </p>
          )}
          <p className="text-[10px] text-gray-400 mt-0.5">
            {conversation.lastMessage
              ? formatTime(conversation.lastMessage.createdAt)
              : formatTime(conversation.createdAt)}
          </p>
        </div>
      </div>
    </button>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Chat Area
// ══════════════════════════════════════════════════════════════════════════
function ChatArea({
  conversation,
  onBack,
}: {
  conversation: Conversation;
  onBack: () => void;
}) {
  const [newMessage, setNewMessage] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const { data: messages, isLoading: msgLoading } = useMessages(conversation.id);
  const sendMutation = useSendMessage();
  const markReadMutation = useMarkAsRead();
  const hasMarkedRead = useRef(false);

  // Mark as read when conversation is opened
  useEffect(() => {
    if (conversation.unreadCount > 0 && !hasMarkedRead.current) {
      markReadMutation.mutate(conversation.id);
      hasMarkedRead.current = true;
    }
    return () => {
      hasMarkedRead.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversation.id, conversation.unreadCount]);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const handleSend = () => {
    if (!newMessage.trim() || sendMutation.isPending) return;
    sendMutation.mutate(
      {
        conversationId: conversation.id,
        req: { content: newMessage.trim(), type: "Text" },
      },
      {
        onSuccess: () => setNewMessage(""),
      }
    );
  };

  const otherParticipant = conversation.participants.find(
    (p) => p.userId !== conversation.createdBy
  ) ?? conversation.participants[0];

  const headerName =
    conversation.type === "Group"
      ? conversation.title ?? "محادثة جماعية"
      : otherParticipant?.fullName ?? otherParticipant?.username ?? "مستخدم";

  return (
    <>
      {/* Chat Header */}
      <div
        className="flex items-center gap-3 p-4 border-b"
        style={{ borderColor: "#e8f0f9" }}
      >
        <button
          onClick={onBack}
          className="lg:hidden p-1.5 rounded-lg hover:bg-gray-100 transition-colors"
        >
          <ChevronLeft className="w-5 h-5 text-gray-500 rotate-180" />
        </button>

        <div
          className={`w-9 h-9 rounded-full flex items-center justify-center ${
            conversation.type === "Group"
              ? "bg-[#f5922e]/10 text-[#f5922e]"
              : "bg-[#3d7ab5]/10 text-[#3d7ab5]"
          }`}
        >
          {conversation.type === "Group" ? (
            <Users className="w-4 h-4" />
          ) : (
            <User className="w-4 h-4" />
          )}
        </div>

        <div className="flex-1">
          <h3 className="text-sm font-bold text-[#0d2137]">{headerName}</h3>
          {conversation.type === "Group" && (
            <p className="text-[11px] text-gray-400">
              {conversation.participants.map((p) => p.fullName ?? p.username).join("، ")}
            </p>
          )}
        </div>

        {conversation.type === "Group" && (
          <span className="badge-orange text-[10px]">
            <Hash className="w-3 h-3 inline me-1" />
            {conversation.participants.length} مشارك
          </span>
        )}
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3 bg-[#f7fafd]">
        {msgLoading ? (
          <div className="space-y-3">
            {[1, 2, 3, 4, 5].map((i) => (
              <div
                key={i}
                className={`animate-pulse h-10 rounded-xl max-w-[60%] ${
                  i % 2 === 0 ? "ms-auto bg-[#3d7ab5]/10" : "bg-gray-100"
                }`}
              />
            ))}
          </div>
        ) : messages && messages.length > 0 ? (
          messages.map((msg) => {
            return <MessageBubble key={msg.id} message={msg} />;
          })
        ) : (
          <div className="text-center py-10">
            <MessageCircle className="w-10 h-10 mx-auto text-gray-200 mb-2" />
            <p className="text-gray-400 text-sm">ابدأ المحادثة!</p>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Input Area */}
      <div className="p-3 border-t" style={{ borderColor: "#e8f0f9" }}>
        <div className="flex items-center gap-2">
          <input
            type="text"
            placeholder="اكتب رسالتك..."
            value={newMessage}
            onChange={(e) => setNewMessage(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                handleSend();
              }
            }}
            className="flex-1 h-10 px-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 focus:border-[#3d7ab5]"
          />
          <button
            onClick={handleSend}
            disabled={!newMessage.trim() || sendMutation.isPending}
            className="w-10 h-10 flex items-center justify-center rounded-xl bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Send className="w-4 h-4" />
          </button>
        </div>
      </div>
    </>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Message Bubble
// ══════════════════════════════════════════════════════════════════════════
function MessageBubble({ message }: { message: Message }) {
  const user = useAuthStore((s) => s.user);
  const isMine = message.senderId === user?.id;

  return (
    <div className={`flex ${isMine ? "justify-start" : "justify-end"}`}>
      <div
        className={`max-w-[75%] rounded-xl px-4 py-2.5 ${
          isMine
            ? "bg-[#3d7ab5] text-white rounded-bl-none"
            : "bg-white text-[#0d2137] border border-[#e8f0f9] rounded-br-none"
        }`}
        style={
          !isMine
            ? {
                boxShadow: "0 1px 2px rgba(13,33,55,0.06)",
              }
            : {}
        }
      >
        {!isMine && (
          <div className="flex items-center gap-1.5 mb-1">
            <span className="text-[11px] font-bold text-[#3d7ab5]">
              {message.senderName}
            </span>
            <span className={`text-[9px] font-bold px-1.5 py-0.5 rounded-full ${roleColors[message.senderRole] ?? "bg-gray-100 text-gray-600"}`}>
              {roleLabels[message.senderRole] ?? message.senderRole}
            </span>
          </div>
        )}
        {message.type === "Inquiry" && (
          <div className={`text-[10px] font-bold mb-1 ${isMine ? "text-blue-200" : "text-[#f5922e]"}`}>
            ❓ استفسار
          </div>
        )}
        <p className="text-sm leading-relaxed whitespace-pre-wrap">{message.content}</p>
        <p
          className={`text-[10px] mt-1 ${
            isMine ? "text-blue-200" : "text-gray-400"
          }`}
        >
          {formatMessageTime(message.createdAt)}
        </p>
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// New Conversation Modal
// ══════════════════════════════════════════════════════════════════════════
function NewConversationModal({ onClose }: { onClose: () => void }) {
  const [mode, setMode] = useState<"direct" | "group">("direct");
  const [selectedDoctor, setSelectedDoctor] = useState<string>("");
  const [groupTitle, setGroupTitle] = useState("");
  const [groupParticipants, setGroupParticipants] = useState<string[]>([]);
  const [initialMessage, setInitialMessage] = useState("");
  const [search, setSearch] = useState("");

  const { data: doctors, isLoading } = useDoctorsForMessaging();
  const createMutation = useCreateConversation();

  const filteredDoctors = (doctors ?? []).filter(
    (d) =>
      !search ||
      d.fullName?.toLowerCase().includes(search.toLowerCase()) ||
      d.username?.toLowerCase().includes(search.toLowerCase()) ||
      d.specialty?.toLowerCase().includes(search.toLowerCase())
  );

  const handleCreate = () => {
    if (mode === "direct" && !selectedDoctor) return;
    if (mode === "group" && groupParticipants.length === 0) return;

    createMutation.mutate(
      mode === "direct"
        ? {
            recipientId: selectedDoctor,
            initialMessage: initialMessage || undefined,
            type: "Direct",
          }
        : {
            participantIds: groupParticipants,
            title: groupTitle,
            initialMessage: initialMessage || undefined,
            type: "Group",
          },
      {
        onSuccess: () => onClose(),
      }
    );
  };

  const toggleParticipant = (userId: string) => {
    setGroupParticipants((prev) =>
      prev.includes(userId)
        ? prev.filter((id) => id !== userId)
        : [...prev, userId]
    );
  };

  return (
    <div
      className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-2xl shadow-2xl max-w-md w-full max-h-[90vh] overflow-hidden flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b" style={{ borderColor: "#e8f0f9" }}>
          <h3 className="text-lg font-extrabold text-[#0d2137]">
            محادثة جديدة
          </h3>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-gray-100 transition-colors"
          >
            <X className="w-5 h-5 text-gray-400" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-5 space-y-4">
          {/* Mode Toggle */}
          <div className="flex gap-2">
            <button
              onClick={() => setMode("direct")}
              className={`flex-1 py-2.5 rounded-xl text-sm font-bold transition-colors ${
                mode === "direct"
                  ? "bg-[#3d7ab5] text-white"
                  : "bg-[#f0f5fb] text-gray-600 hover:bg-[#dce8f5]"
              }`}
            >
              <User className="w-4 h-4 inline me-1.5" />
              رسالة مباشرة
            </button>
            <button
              onClick={() => setMode("group")}
              className={`flex-1 py-2.5 rounded-xl text-sm font-bold transition-colors ${
                mode === "group"
                  ? "bg-[#f5922e] text-white"
                  : "bg-[#f0f5fb] text-gray-600 hover:bg-[#dce8f5]"
              }`}
            >
              <Users className="w-4 h-4 inline me-1.5" />
              محادثة جماعية
            </button>
          </div>

          {/* Group Title */}
          {mode === "group" && (
            <input
              type="text"
              placeholder="عنوان المحادثة الجماعية"
              value={groupTitle}
              onChange={(e) => setGroupTitle(e.target.value)}
              className="w-full h-10 px-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30"
            />
          )}

          {/* Search Doctors */}
          <div className="relative">
            <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 right-3 text-gray-400" />
            <input
              type="text"
              placeholder="بحث عن طبيب أو موظف..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full h-10 pr-9 pl-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30"
            />
          </div>

          {/* Doctors List */}
          {isLoading ? (
            <div className="space-y-2">
              {[1, 2, 3].map((i) => (
                <div key={i} className="animate-pulse h-12 bg-gray-100 rounded-xl" />
              ))}
            </div>
          ) : (
            <div className="space-y-1.5 max-h-48 overflow-y-auto">
              {filteredDoctors.map((doc) => {
                const isSelected =
                  mode === "direct"
                    ? selectedDoctor === doc.userId
                    : groupParticipants.includes(doc.userId);

                return (
                  <button
                    key={doc.userId}
                    onClick={() => {
                      if (mode === "direct") {
                        setSelectedDoctor(doc.userId);
                      } else {
                        toggleParticipant(doc.userId);
                      }
                    }}
                    className={`w-full flex items-center gap-3 p-2.5 rounded-xl transition-colors ${
                      isSelected
                        ? "bg-[#3d7ab5]/10 border border-[#3d7ab5]/30"
                        : "hover:bg-[#f0f5fb] border border-transparent"
                    }`}
                  >
                    <div className="w-8 h-8 rounded-full bg-[#3d7ab5]/10 flex items-center justify-center flex-shrink-0">
                      <User className="w-4 h-4 text-[#3d7ab5]" />
                    </div>
                    <div className="flex-1 text-start min-w-0">
                      <p className="text-sm font-bold text-[#0d2137] truncate">
                        {doc.fullName ?? doc.username}
                      </p>
                      <div className="flex items-center gap-1.5">
                        <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${roleColors[doc.role] ?? "bg-gray-100 text-gray-600"}`}>
                          {roleLabels[doc.role] ?? doc.role}
                        </span>
                        {doc.specialty && (
                          <span className="text-[10px] text-gray-400">
                            {doc.specialty}
                          </span>
                        )}
                      </div>
                    </div>
                    {isSelected && (
                      <div className="w-5 h-5 rounded-full bg-[#3d7ab5] flex items-center justify-center">
                        <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M5 13l4 4L19 7" />
                        </svg>
                      </div>
                    )}
                  </button>
                );
              })}
              {filteredDoctors.length === 0 && (
                <p className="text-center text-gray-400 text-sm py-4">
                  لا توجد نتائج
                </p>
              )}
            </div>
          )}

          {/* Initial Message */}
          <textarea
            placeholder="اكتب رسالة أولية (اختياري)..."
            rows={3}
            value={initialMessage}
            onChange={(e) => setInitialMessage(e.target.value)}
            className="w-full px-4 py-3 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 resize-none"
          />
        </div>

        {/* Footer */}
        <div className="flex gap-2 p-5 border-t" style={{ borderColor: "#e8f0f9" }}>
          <button
            onClick={onClose}
            className="flex-1 py-2.5 bg-gray-100 text-gray-600 rounded-xl text-sm font-bold hover:bg-gray-200 transition-colors"
          >
            إلغاء
          </button>
          <button
            onClick={handleCreate}
            disabled={
              createMutation.isPending ||
              (mode === "direct" && !selectedDoctor) ||
              (mode === "group" && groupParticipants.length === 0)
            }
            className="flex-1 py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {createMutation.isPending ? "جاري الإنشاء..." : "إنشاء محادثة"}
          </button>
        </div>
      </div>
    </div>
  );
}
