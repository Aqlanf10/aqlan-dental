"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { MessageCircle, Send, Loader2, AlertTriangle, ExternalLink } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import type { ConversationDetail, Message } from "@/types/messaging";
import { useRouter } from "next/navigation";

interface MessagesTabProps {
  patientId: string;
}

export function MessagesTab({ patientId }: MessagesTabProps) {
  const router = useRouter();
  const currentUserId = useAuthStore((s) => s.user?.id);
  const [conversation, setConversation] = useState<ConversationDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [newMessage, setNewMessage] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const isFetchingRef = useRef(false);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  // Fetch internal patient conversation (StaffToPatient — not visible to patient)
  const fetchConversation = useCallback(async () => {
    // Prevent concurrent fetches
    if (isFetchingRef.current) return;
    isFetchingRef.current = true;
    try {
      const { data } = await api.get<ConversationDetail>(
        `/api/messages/patient/${patientId}`
      );
      setConversation(data);
      setError("");
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) {
        // No conversation exists yet — that's fine, show empty state
        setConversation(null);
      } else {
        // Other errors: show inline error
        setError("فشل تحميل المحادثة");
      }
    } finally {
      isFetchingRef.current = false;
    }
  }, [patientId]);

  // Initial load
  useEffect(() => {
    fetchConversation().finally(() => setLoading(false));
  }, [patientId]); // eslint-disable-line react-hooks/exhaustive-deps

  // Scroll to bottom when messages change
  useEffect(() => {
    scrollToBottom();
  }, [conversation?.messages?.length, scrollToBottom]);

  // Auto-refresh messages every 8 seconds (safe — won't duplicate because we replace)
  useEffect(() => {
    const interval = setInterval(() => {
      fetchConversation();
    }, 8000);
    return () => clearInterval(interval);
  }, [fetchConversation]);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    const content = newMessage.trim();
    if (!content || sending) return;
    if (content.length > 2000) {
      toast.error("الرسالة طويلة جداً — الحد الأقصى 2000 حرف");
      return;
    }

    setSending(true);
    setError("");
    try {
      // Step 1: Ensure an internal conversation exists (get-or-create via POST)
      let convId = conversation?.id;

      if (!convId) {
        const { data: convDetail } = await api.post<ConversationDetail>(
          `/api/messages/internal-patient/${patientId}`
        );
        convId = convDetail.id;
        setConversation(convDetail);
      }

      // Step 2: Send the message via the backend endpoint
      await api.post(`/api/messages/conversations/${convId}/messages`, {
        content,
      });

      // Step 3: Clear input immediately
      setNewMessage("");

      // Step 4: Refetch conversation from backend to get the real message
      // (with server-assigned id, timestamp, sender info, etc.)
      const { data: updated } = await api.get<ConversationDetail>(
        `/api/messages/patient/${patientId}`
      );
      setConversation(updated);

      // Step 5: Only now show success toast
      toast.success("تم إرسال الرسالة");
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 403) {
        toast.error("ليس لديك صلاحية إرسال رسائل في هذه المحادثة");
      } else if (status === 404) {
        toast.error("المحادثة غير موجودة — حاول تحديث الصفحة");
      } else if (status === 400) {
        // Backend validation error (empty content, too long, etc.)
        const msg = (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message;
        toast.error(msg ?? "محتوى الرسالة غير صالح");
      } else {
        toast.error("فشل إرسال الرسالة — تحقق من الاتصال وحاول مجدداً");
      }
    } finally {
      setSending(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        <div className="h-8 bg-gray-100 rounded-lg w-1/3" />
        {Array.from({ length: 3 }).map((_, i) => (
          <div
            key={i}
            className={cn(
              "h-14 bg-gray-100 rounded-lg",
              i % 2 === 0 ? "w-3/4" : "w-1/2 ms-auto"
            )}
          />
        ))}
        <div className="h-10 bg-gray-100 rounded-lg" />
      </div>
    );
  }

  const messages = conversation?.messages ?? [];

  return (
    <div className="flex flex-col h-full" dir="rtl">
      {/* Header with open in messages page link */}
      {conversation && (
        <div className="flex items-center justify-between pb-3 mb-3 border-b border-gray-100">
          <div className="flex items-center gap-2">
            <MessageCircle className="w-4 h-4 text-gray-500" />
            <span className="text-sm font-medium text-gray-700">
              محادثة داخلية حول المريض
            </span>
            <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-500 font-semibold">
              لا تظهر للمريض
            </span>
          </div>
          <button
            onClick={() => router.push(`/messages`)}
            className="text-xs text-clinic-blue hover:underline flex items-center gap-1"
          >
            <ExternalLink className="w-3 h-3" />
            فتح في صفحة الرسائل
          </button>
        </div>
      )}

      {/* Inline error */}
      {error && (
        <div className="mb-3 p-2 bg-amber-50 border border-amber-200 rounded-lg text-amber-700 text-xs flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
          {error}
          <button
            onClick={() => {
              setError("");
              fetchConversation();
            }}
            className="text-amber-600 hover:text-amber-800 underline ms-auto"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {/* Messages list */}
      <div className="flex-1 max-h-96 overflow-y-auto space-y-2 mb-3">
        {messages.length === 0 ? (
          <div className="text-center py-8">
            <MessageCircle className="w-10 h-10 mx-auto mb-2 text-gray-300" />
            <p className="text-sm text-gray-400 mb-3">
              لا توجد رسائل مع هذا المريض بعد
            </p>
            <p className="text-xs text-gray-300">
              اكتب رسالة لبدء نقاش داخلي حول هذا المريض
            </p>
          </div>
        ) : (
          messages.map((msg) => (
            <MessageBubble
              key={msg.id}
              message={msg}
              isMine={msg.senderId === currentUserId}
            />
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Send form */}
      <form onSubmit={handleSend} className="flex gap-2 pt-3 border-t border-gray-100">
        <input
          type="text"
          value={newMessage}
          onChange={(e) => setNewMessage(e.target.value)}
          placeholder="اكتب رسالة..."
          maxLength={2000}
          className="flex-1 text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue"
          disabled={sending}
        />
        <button
          type="submit"
          disabled={sending || !newMessage.trim()}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition"
        >
          {sending ? (
            <Loader2 className="w-3.5 h-3.5 animate-spin" />
          ) : (
            <Send className="w-3.5 h-3.5" />
          )}
          إرسال
        </button>
      </form>
    </div>
  );
}

// ─── فقاعة الرسالة ──────────────────────────────────────────────────────────
function MessageBubble({
  message,
  isMine,
}: {
  message: Message;
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

  return (
    <div
      className={cn(
        "flex gap-2",
        isMine ? "flex-row-reverse" : "flex-row"
      )}
    >
      {/* Avatar */}
      {!isMine && (
        <div
          className="w-7 h-7 rounded-full flex items-center justify-center text-white text-[10px] font-bold flex-shrink-0 mt-auto"
          style={{ backgroundColor: message.senderColor ?? "#6B7280" }}
        >
          {message.senderInitials ?? message.senderName.charAt(0)}
        </div>
      )}

      <div
        className={cn(
          "max-w-[80%] min-w-0",
          isMine ? "items-end" : "items-start"
        )}
      >
        <div
          className={cn(
            "px-3 py-2 rounded-2xl text-sm",
            isMine
              ? "bg-clinic-blue text-white rounded-bl-md"
              : "bg-white border border-gray-200 text-gray-800 rounded-br-md"
          )}
        >
          {/* Sender name */}
          {!isMine && (
            <p className="text-xs font-semibold text-clinic-blue mb-0.5">
              {message.senderName}
            </p>
          )}
          <p className="whitespace-pre-wrap break-words leading-relaxed">
            {message.content}
          </p>
        </div>

        {/* Time */}
        <div
          className={cn(
            "flex items-center gap-1 mt-0.5 px-1",
            isMine ? "flex-row-reverse" : "flex-row"
          )}
        >
          <span className="text-[10px] text-gray-400">
            {formatArabicDate(message.createdAt)}
          </span>
        </div>
      </div>
    </div>
  );
}
