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
  Eye,
  EyeOff,
  Shield,
  ChevronLeft,
  ChevronRight,
  Stethoscope,
  Building2,
  ShieldCheck,
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
  ConversationType,

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
  { value: "all", label: "الكُل" },
  { value: "unread", label: "غير مقروءة" },
  { value: "PatientFacing", label: "مرئية للمريض" },
  { value: "StaffToPatient", label: "داخلية" },
  { value: "StaffToStaff", label: "موظفين" },
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
  Doctor: "طبيب",
  Nurse: "ممرض",
};

// ─── شارة نوع المحادثة ────────────────────────────────────────────────────────
function ConversationTypeBadge({ type }: { type: ConversationType }) {
  switch (type) {
    case "PatientFacing":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 leading-none">
          <Eye className="w-2.5 h-2.5" />
          مرئية للمريض
        </span>
      );
    case "StaffToPatient":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-200 leading-none">
          <EyeOff className="w-2.5 h-2.5" />
          داخلية
        </span>
      );
    case "StaffToStaff":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200 leading-none">
          <Shield className="w-2.5 h-2.5" />
          طاقم
        </span>
      );
    default:
      return null;
  }
}

// ─── شارة نوع المستلم ──────────────────────────────────────────────────────────
function RecipientTypeBadge({ recipientType }: { recipientType?: string | null }) {
  if (!recipientType) return null;
  switch (recipientType) {
    case "TreatingDoctor":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200 leading-none">
          <Stethoscope className="w-2.5 h-2.5" />
          موجهة إلى الطبيب
        </span>
      );
    case "Reception":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-amber-50 text-amber-600 border border-amber-200 leading-none">
          <Building2 className="w-2.5 h-2.5" />
          موجهة إلى الاستقبال
        </span>
      );
    case "Admin":
      return (
        <span className="flex-shrink-0 inline-flex items-center gap-1 text-[9px] font-bold px-1.5 py-0.5 rounded-full bg-purple-50 text-purple-600 border border-purple-200 leading-none">
          <ShieldCheck className="w-2.5 h-2.5" />
          موجهة إلى الإدارة
        </span>
      );
    default:
      return null;
  }
}

// ─── مساعد تفاصيل الخطأ ──────────────────────────────────────────────────────
function getErrorDetail(error: unknown): { title: string; description: string } {
  const err = error as { response?: { status?: number } } | null;
  const status = err?.response?.status;
  if (status === 403) {
    return {
      title: "غير مصرّح بالوصول",
      description: "ليس لديك صلاحية للوصول إلى هذا المحتوى",
    };
  }
  if (status === 404) {
    return {
      title: "المحتوى غير موجود",
      description: "المحادثة المطلوبة غير موجودة أو تم حذفها",
    };
  }
  return {
    title: "خطأ",
    description: "حدث خطأ أثناء تحميل المحادثات",
  };
}

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
  const [convPage, setConvPage] = useState(1);

  const { data: convData, isLoading: convLoading, error: convError } = useConversations(
    convPage,
    searchQuery || undefined,
    activeFilter
  );
  const { data: conversation } = useConversation(selectedConvId);
  const sendMessage = useSendMessage(selectedConvId ?? "");
  const markAsRead = useMarkAsRead(selectedConvId ?? "");
  const { data: unreadData } = useUnreadCount();
  const patientConv = usePatientConversation();

  const conversations = convData?.data ?? [];
  const totalPages = convData?.totalPages ?? 1;

  // Reset page on filter/search change
  useEffect(() => {
    setConvPage(1);
  }, [activeFilter, searchQuery]);

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

  const errorDetail = convError ? getErrorDetail(convError) : null;

  return (
    <div className="h-[calc(100vh-4rem)] flex flex-col" dir="rtl">
      {/* Top accent gradient bar */}
      <div className="h-1 bg-gradient-to-l from-[#3d7ab5] via-[#0d9488] to-[#0d2137] rounded-t-2xl flex-shrink-0" />

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

      {/* Main messaging area wrapper */}
      <div className="flex-1 flex gap-0 rounded-2xl shadow-sm border border-slate-200/60 bg-white overflow-hidden">
        {/* ─── قائمة المحادثات ──────────────────────────────────────────── */}
        <div
          className={cn(
            "w-full md:w-80 lg:w-96 flex-shrink-0 bg-white flex flex-col overflow-hidden border-l border-slate-200/60",
            isMobileDetail ? "hidden md:flex" : "flex"
          )}
        >
          {/* Header */}
          <div className="p-4 border-b border-gray-100">
            {/* User info */}
            {user && (
              <div className="flex items-center gap-3 mb-4 pb-3 border-b border-gray-100">
                <div
                  className="w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
                  style={{ backgroundColor: user.doctorColor ?? "#0d9488" }}
                >
                  {user.doctorInitials ?? user.username?.charAt(0).toUpperCase() ?? "U"}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-bold text-gray-900 truncate">
                    {user.doctorName ?? user.username}
                  </p>
                  <p className="text-[11px] text-gray-400">
                    {ROLE_LABELS[user.role] ?? user.role}
                  </p>
                </div>
              </div>
            )}

            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <MessageCircle className="w-5 h-5 text-[#0d9488]" />
                <h2 className="text-lg font-bold text-gray-900">الرسائل</h2>
                {unreadData && unreadData.totalUnread > 0 && (
                  <span className="bg-red-500 text-white text-xs font-bold rounded-full px-2 py-0.5">
                    {unreadData.totalUnread > 99 ? "99+" : unreadData.totalUnread}
                  </span>
                )}
              </div>
              <button
                onClick={() => setShowNewChat(true)}
                className="w-9 h-9 rounded-lg bg-[#0d9488] text-white flex items-center justify-center hover:opacity-90 transition"
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
                className="w-full pr-10 pl-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#0d9488] bg-gray-50"
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
                      ? "bg-[#0d9488] text-white"
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
                <Loader2 className="w-6 h-6 animate-spin text-[#0d9488]" />
                <p className="text-sm text-gray-400">جارٍ تحميل المحادثات...</p>
              </div>
            ) : convError && errorDetail ? (
              <div className="text-center py-12 px-4">
                <AlertTriangle className="w-12 h-12 text-amber-400 mx-auto mb-3" />
                <p className="text-gray-700 text-sm font-semibold">{errorDetail.title}</p>
                <p className="text-gray-400 text-xs mt-1">{errorDetail.description}</p>
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

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="p-3 border-t border-gray-100 flex items-center justify-center gap-2">
              <button
                onClick={() => setConvPage((p) => Math.max(1, p - 1))}
                disabled={convPage <= 1}
                className={cn(
                  "w-8 h-8 rounded-lg flex items-center justify-center transition",
                  convPage <= 1
                    ? "text-gray-300 cursor-not-allowed"
                    : "text-gray-500 hover:bg-gray-100"
                )}
              >
                <ChevronRight className="w-4 h-4" />
              </button>
              <span className="text-xs text-gray-500">
                {convPage} / {totalPages}
              </span>
              <button
                onClick={() => setConvPage((p) => Math.min(totalPages, p + 1))}
                disabled={convPage >= totalPages}
                className={cn(
                  "w-8 h-8 rounded-lg flex items-center justify-center transition",
                  convPage >= totalPages
                    ? "text-gray-300 cursor-not-allowed"
                    : "text-gray-500 hover:bg-gray-100"
                )}
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>
          )}
        </div>

        {/* ─── منطقة الرسائل ──────────────────────────────────────────────── */}
        <div
          className={cn(
            "flex-1 bg-white flex flex-col overflow-hidden",
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
              <div className="text-center max-w-xs mx-auto">
                <div className="w-20 h-20 rounded-full bg-gray-50 flex items-center justify-center mx-auto mb-5">
                  <MessageCircle className="w-10 h-10 text-gray-300" />
                </div>
                <p className="text-gray-500 text-lg font-semibold mb-2">اختر محادثة للبدء</p>
                <p className="text-gray-400 text-sm leading-relaxed">
                  اختر محادثة من القائمة أو أنشئ محادثة جديدة بالضغط على زر +
                </p>
              </div>
            </div>
          )}
        </div>
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
  const isPatientConv =
    conv.conversationType === "StaffToPatient" || conv.conversationType === "PatientFacing";
  const hasUnread = conv.unreadCount > 0;

  return (
    <button
      onClick={onClick}
      className={cn(
        "w-full flex items-center gap-3 px-4 py-3 text-right transition-colors border-b border-gray-50",
        isSelected
          ? "bg-[#0d9488]/5 border-r-4 border-r-[#0d9488]"
          : "hover:bg-gray-50"
      )}
    >
      {/* Avatar */}
      <div className="relative flex-shrink-0">
        {isPatientConv ? (
          <div className={cn(
            "w-11 h-11 rounded-full flex items-center justify-center",
            conv.conversationType === "PatientFacing" ? "bg-emerald-50" : "bg-amber-50"
          )}>
            <User className={cn("w-5 h-5", conv.conversationType === "PatientFacing" ? "text-emerald-600" : "text-amber-600")} />
          </div>
        ) : conv.isGroup ? (
          <div className="w-11 h-11 rounded-full bg-blue-50 flex items-center justify-center">
            <Users className="w-5 h-5 text-blue-500" />
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
            {/* Conversation type badge */}
            <ConversationTypeBadge type={conv.conversationType} />
            {/* Recipient type badge (for patient-facing conversations) */}
            {conv.conversationType === "PatientFacing" && (
              <RecipientTypeBadge recipientType={conv.recipientType} />
            )}
          </div>
          <div className="flex items-center gap-1.5 flex-shrink-0">
            {conv.lastMessageAt && (
              <span className="text-[10px] text-gray-400">
                {formatTime(conv.lastMessageAt)}
              </span>
            )}
            {hasUnread && (
              <span className="bg-[#0d9488] text-white text-[10px] font-bold rounded-full min-w-[18px] h-[18px] flex items-center justify-center px-1 flex-shrink-0">
                {conv.unreadCount > 9 ? "9+" : conv.unreadCount}
              </span>
            )}
          </div>
        </div>
        {/* Patient number + name row */}
        {isPatientConv && (conv.patientNumber || conv.patientName) && (
          <p className="text-[10px] text-amber-600 font-medium mt-0.5 flex items-center gap-1">
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
  const isPatientConv =
    conversation.conversationType === "StaffToPatient" ||
    conversation.conversationType === "PatientFacing";
  const isPatientFacing = conversation.conversationType === "PatientFacing";
  const isStaffToPatient = conversation.conversationType === "StaffToPatient";

  const title = isPatientConv
    ? conversation.patientName
      ? `${isPatientFacing ? "مراسلة" : "ملف"} المريض: ${conversation.patientName}`
      : conversation.title
    : conversation.isGroup
      ? conversation.title
      : otherParticipants[0]?.displayName ?? conversation.title;

  // Build a map of senderId -> role for role labels
  const participantRoleMap = new Map<string, string>();
  for (const p of conversation.participants) {
    if (p.role) participantRoleMap.set(p.userId, p.role);
  }

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
          <div className={cn(
            "w-10 h-10 rounded-full flex items-center justify-center",
            isPatientFacing ? "bg-emerald-50" : "bg-amber-50"
          )}>
            <User className={cn("w-5 h-5", isPatientFacing ? "text-emerald-600" : "text-amber-600")} />
          </div>
        ) : conversation.isGroup ? (
          <div className="w-10 h-10 rounded-full bg-blue-50 flex items-center justify-center">
            <Users className="w-5 h-5 text-blue-500" />
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
              <span className="text-xs text-amber-600 font-medium">
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
          {isPatientConv && (
            <div className="flex items-center gap-1.5">
              <ConversationTypeBadge type={conversation.conversationType} />
              {isPatientFacing && (
                <RecipientTypeBadge recipientType={conversation.recipientType} />
              )}
            </div>
          )}
        </div>

        {/* Open patient file button */}
        {isPatientConv && conversation.patientId && (
          <button
            onClick={() => onOpenPatient(conversation.patientId!)}
            className="w-9 h-9 rounded-lg bg-[#0d2137]/5 hover:bg-[#0d2137]/10 flex items-center justify-center text-[#0d2137] transition"
            title="فتح ملف المريض"
          >
            <ExternalLink className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Chat header type indicator banners */}
      {isPatientFacing && (
        <div className="px-4 py-2 bg-emerald-50 border-b border-emerald-200 flex items-center gap-2">
          <Eye className="w-4 h-4 text-emerald-600 flex-shrink-0" />
          <p className="text-xs text-emerald-700 font-medium">
            محادثة مع المريض — هذه المحادثة مرئية للمريض
          </p>
          {conversation.recipientType && (
            <RecipientTypeBadge recipientType={conversation.recipientType} />
          )}
        </div>
      )}
      {isStaffToPatient && (
        <div className="px-4 py-2 bg-amber-50 border-b border-amber-200 flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0" />
          <p className="text-xs text-amber-700 font-medium">
            ⚠️ هذه المحادثة داخلية ولا تظهر للمريض
          </p>
        </div>
      )}

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
            participantRoleMap={participantRoleMap}
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
          <Reply className="w-4 h-4 text-[#0d9488] flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-[#0d9488]">
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
            className="flex-1 resize-none border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-[#0d9488] max-h-32"
            style={{ minHeight: "40px" }}
          />
          <button
            onClick={handleSend}
            disabled={!input.trim() || sending || input.length > 2000}
            className={cn(
              "w-10 h-10 rounded-lg flex items-center justify-center transition flex-shrink-0",
              input.trim() && !sending && input.length <= 2000
                ? "bg-[#0d9488] text-white hover:opacity-90"
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
  participantRoleMap,
}: {
  message: Message;
  isMine: boolean;
  onReply: () => void;
  participantRoleMap: Map<string, string>;
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

  const senderRole = participantRoleMap.get(message.senderId);
  const senderRoleLabel = senderRole ? ROLE_LABELS[senderRole] : undefined;

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
              "text-xs px-3 py-1.5 rounded-lg mb-1 border-r-2 border-[#0d9488] bg-gray-100",
              isMine ? "text-left" : "text-right"
            )}
          >
            <span className="font-semibold text-[#0d9488]">
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
              ? "bg-[#0d9488] text-white rounded-br-md"
              : "bg-white border border-gray-200 text-gray-800 rounded-bl-md"
          )}
        >
          {/* Show sender name + role in group conversations */}
          {!isMine && (
            <p className="text-xs font-semibold text-[#0d9488] mb-0.5 flex items-center gap-1.5">
              {message.senderName}
              {senderRoleLabel && (
                <span className="text-[9px] px-1 py-0.5 rounded font-medium bg-gray-100 text-gray-500">
                  {senderRoleLabel}
                </span>
              )}
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
                message.isReadByMe ? "text-[#0d9488]" : "text-gray-300"
              )}
            />
          )}
        </div>

        {/* Reply button (on hover) */}
        <button
          onClick={onReply}
          className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-400 hover:text-[#0d9488] text-xs mt-0.5 flex items-center gap-1"
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
              className="w-full pr-10 pl-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#0d9488]"
            />
          </div>

          {/* Group title (if multiple selected) */}
          {selected.size > 1 && (
            <input
              type="text"
              placeholder="اسم المجموعة (اختياري)"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#0d9488]"
            />
          )}

          {/* Users list */}
          {loading ? (
            <div className="flex flex-col items-center justify-center py-8 gap-2">
              <Loader2 className="w-6 h-6 animate-spin text-[#0d9488]" />
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
                      ? "bg-[#0d9488]/10 border border-[#0d9488]/30"
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
                    <div className="w-5 h-5 bg-[#0d9488] rounded-full flex items-center justify-center">
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
            className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#0d9488] resize-none"
          />
          <button
            onClick={handleCreate}
            disabled={selected.size === 0 || createConv.isPending}
            className={cn(
              "w-full py-2.5 rounded-lg font-semibold text-sm transition",
              selected.size > 0 && !createConv.isPending
                ? "bg-[#0d9488] text-white hover:opacity-90"
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
