"use client";
import { useState, useEffect, useCallback } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import {
  MessageCircle,
  Search,
  Plus,
  X,
  AlertTriangle,
  Loader2,
  BarChart3,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuthStore } from "@/stores/authStore";
import {
  useConversations,
  useConversation,
  usePatientConversation,
  useSendMessage,
  useMarkAsRead,
  useUnreadCount,
  useMessagingStats,
} from "@/hooks/useMessaging";
import type {
  ConversationFilter,
  SendMessageRequest,
} from "@/types/messaging";
import {
  FILTER_OPTIONS,
  ROLE_LABELS,
  getErrorDetail,
} from "./_components/shared";
import { ConversationItem } from "./_components/ConversationItem";
import { ChatArea } from "./_components/ChatArea";
import { NewChatDialog } from "./_components/NewChatDialog";

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
  const [showStats, setShowStats] = useState(false);
  const [convPage, setConvPage] = useState(1);

  const { data: stats } = useMessagingStats();
  const { data: convData, isLoading: convLoading, error: convError } = useConversations(
    convPage,
    searchQuery || undefined,
    activeFilter
  );
  const { data: conversation, isLoading: convDetailLoading, isError: convDetailError, refetch: refetchConv } = useConversation(selectedConvId);
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
    <div className="h-[calc(100vh-4rem)] flex flex-col">
      {/* Top accent gradient bar */}
      <div className="h-1 bg-gradient-to-l from-[#3d7ab5] via-[#3d7ab5] to-[#0d2137] rounded-t-2xl flex-shrink-0" />

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
            "w-full md:w-80 lg:w-96 flex-shrink-0 bg-white flex flex-col overflow-hidden border-e border-slate-200/60",
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
                  style={{ backgroundColor: user.doctorColor ?? "#3d7ab5" }}
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
                <MessageCircle className="w-5 h-5 text-[#3d7ab5]" />
                <h2 className="text-lg font-bold text-gray-900">الرسائل</h2>
                {unreadData && unreadData.totalUnread > 0 && (
                  <span className="bg-red-500 text-white text-xs font-bold rounded-full px-2 py-0.5">
                    {unreadData.totalUnread > 99 ? "99+" : unreadData.totalUnread}
                  </span>
                )}
              </div>
              <div className="flex items-center gap-1.5">
                <button
                  onClick={() => setShowStats((v) => !v)}
                  className={cn(
                    "w-9 h-9 rounded-lg flex items-center justify-center transition",
                    showStats ? "bg-[#3d7ab5] text-white" : "bg-gray-100 text-gray-500 hover:bg-gray-200"
                  )}
                  title="إحصائيات المراسلة"
                >
                  <BarChart3 className="w-4 h-4" />
                </button>
                <button
                  onClick={() => setShowNewChat(true)}
                  className="w-9 h-9 rounded-lg bg-[#3d7ab5] text-white flex items-center justify-center hover:opacity-90 transition"
                  title="محادثة جديدة"
                >
                  <Plus className="w-5 h-5" />
                </button>
              </div>
            </div>

            {/* Stats panel */}
            {showStats && stats && (
              <div className="mb-3 p-3 bg-gray-50 rounded-xl border border-gray-200 space-y-2">
                <p className="text-xs font-semibold text-gray-600 mb-2">إحصائيات المراسلة</p>
                <div className="grid grid-cols-2 gap-2">
                  <div className="bg-white rounded-lg p-2 text-center border border-gray-100">
                    <p className="text-lg font-bold text-[#3d7ab5]">{stats.messagesToday}</p>
                    <p className="text-[10px] text-gray-500">رسائل اليوم</p>
                  </div>
                  <div className="bg-white rounded-lg p-2 text-center border border-gray-100">
                    <p className="text-lg font-bold text-blue-600">{stats.messagesThisWeek}</p>
                    <p className="text-[10px] text-gray-500">رسائل هذا الأسبوع</p>
                  </div>
                  <div className="bg-white rounded-lg p-2 text-center border border-gray-100">
                    <p className="text-lg font-bold text-gray-700">{stats.totalConversations}</p>
                    <p className="text-[10px] text-gray-500">إجمالي المحادثات</p>
                  </div>
                  <div className="bg-white rounded-lg p-2 text-center border border-gray-100">
                    <p className="text-lg font-bold text-emerald-600">{stats.activeConversations}</p>
                    <p className="text-[10px] text-gray-500">محادثات نشطة</p>
                  </div>
                </div>
                <div className="flex gap-2 text-[10px] text-gray-500 justify-between px-1">
                  <span>طاقم ↔ طاقم: <b>{stats.staffToStaffConversations}</b></span>
                  <span>طاقم ↔ مريض: <b>{stats.staffToPatientConversations}</b></span>
                  <span>بوابة: <b>{stats.patientFacingConversations}</b></span>
                </div>
              </div>
            )}

            {/* Search */}
            <div className="relative mb-3">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <input
                type="text"
                placeholder="بحث بالاسم، رقم المريض، أو نص الرسالة..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full ps-10 pe-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] bg-gray-50"
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
                      ? "bg-[#3d7ab5] text-white"
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
                <Loader2 className="w-6 h-6 animate-spin text-[#3d7ab5]" />
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
          {selectedConvId && convDetailLoading ? (
            /* Loading state — shown while fetching the conversation */
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                <Loader2 className="w-8 h-8 animate-spin text-[#3d7ab5] mx-auto mb-3" />
                <p className="text-sm text-gray-400">جارٍ تحميل المحادثة...</p>
              </div>
            </div>
          ) : selectedConvId && convDetailError ? (
            /* Error state — API failed (e.g. 500, network error) */
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center max-w-xs mx-auto">
                <div className="w-16 h-16 rounded-full bg-red-50 flex items-center justify-center mx-auto mb-4">
                  <AlertTriangle className="w-8 h-8 text-red-400" />
                </div>
                <p className="text-gray-700 font-semibold mb-1">تعذّر تحميل المحادثة</p>
                <p className="text-gray-400 text-sm mb-4">
                  حدث خطأ أثناء جلب الرسائل. قد يكون الخادم يُعاد تشغيله، حاول مجدداً.
                </p>
                <button
                  onClick={() => refetchConv()}
                  className="px-4 py-2 bg-[#3d7ab5] text-white rounded-lg text-sm font-semibold hover:opacity-90 transition flex items-center gap-2 mx-auto"
                >
                  <RefreshCw className="w-4 h-4" />
                  إعادة المحاولة
                </button>
              </div>
            </div>
          ) : selectedConvId && conversation ? (
            <ChatArea
              conversation={conversation}
              currentUserId={user?.id ?? ""}
              onBack={handleBack}
              onSend={(req: SendMessageRequest) => sendMessage.mutate(req)}
              sending={sendMessage.isPending}
              onOpenPatient={(patientId) => router.push(`/patients/${patientId}`)}
              sendError={
                sendMessage.isError
                  ? (sendMessage.error as { response?: { data?: { message?: string } } })?.response?.data?.message
                    ?? "فشل إرسال الرسالة — تحقق من الاتصال وحاول مجدداً"
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
