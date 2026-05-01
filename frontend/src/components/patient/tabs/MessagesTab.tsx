"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { MessageCircle, Send, Loader2 } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import type { ConversationDetail } from "@/types/messaging";

interface MessagesTabProps {
  patientId: string;
}

export function MessagesTab({ patientId }: MessagesTabProps) {
  const currentUserId = useAuthStore((s) => s.user?.id);
  const [conversation, setConversation] = useState<ConversationDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [newMessage, setNewMessage] = useState("");
  const [sending, setSending] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  // Fetch or create patient conversation using the dedicated endpoint
  const fetchConversation = useCallback(async () => {
    try {
      const { data } = await api.get<ConversationDetail>(
        `/api/messages/patient/${patientId}`
      );
      setConversation(data);
    } catch (err: unknown) {
      // 404 means patient not found — show empty state without error toast
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status !== 404) {
        // Other errors: show toast only on first load
        if (!conversation) {
          toast.error("فشل تحميل المحادثة");
        }
      }
    }
  }, [patientId, conversation]);

  useEffect(() => {
    fetchConversation().finally(() => setLoading(false));
  }, [patientId]); // eslint-disable-line react-hooks/exhaustive-deps

  // Scroll to bottom when messages change
  useEffect(() => {
    scrollToBottom();
  }, [conversation?.messages?.length, scrollToBottom]);

  // Auto-refresh messages every 8 seconds
  useEffect(() => {
    const interval = setInterval(() => {
      fetchConversation();
    }, 8000);
    return () => clearInterval(interval);
  }, [fetchConversation]);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    const content = newMessage.trim();
    if (!content) return;

    setSending(true);
    try {
      // Step 1: Ensure a conversation exists (get-or-create)
      let convId = conversation?.id;

      if (!convId) {
        // Use the patient-linked get-or-create endpoint
        const { data: convDetail } = await api.get<ConversationDetail>(
          `/api/messages/patient/${patientId}`
        );
        convId = convDetail.id;
        setConversation(convDetail);
      }

      // Step 2: Send the message via the correct backend endpoint
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
      } else {
        toast.error("فشل إرسال الرسالة — تحقق من الاتصال وحاول مجدداً");
      }
    } finally {
      setSending(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-14 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  const messages = conversation?.messages ?? [];

  return (
    <div className="space-y-4" dir="rtl">
      {/* Messages list */}
      <div className="max-h-80 overflow-y-auto space-y-2">
        {messages.length === 0 ? (
          <div className="text-center py-8">
            <MessageCircle className="w-10 h-10 mx-auto mb-2 text-[#cbd5e1]" />
            <p className="text-sm text-[#94a3b8] mb-3">لا توجد رسائل مع هذا المريض بعد</p>
          </div>
        ) : (
          messages.map((msg) => (
            <div key={msg.id} className={cn(
              "p-3 rounded-lg max-w-[80%]",
              msg.isSystemMessage
                ? "bg-[#f7fafd] text-center mx-auto"
                : msg.senderId === currentUserId
                  ? "bg-clinic-blue/10 mr-auto"
                  : "bg-[#f7fafd] ml-auto"
            )}>
              {msg.isSystemMessage ? (
                <p className="text-xs text-[#64748b] italic">{msg.content}</p>
              ) : (
                <>
                  <p className="text-sm text-[#0d2137]">{msg.content}</p>
                  <div className="flex items-center gap-2 mt-1">
                    {msg.senderName && (
                      <span className="text-xs text-clinic-blue font-medium">{msg.senderName}</span>
                    )}
                    <span className="text-xs text-[#94a3b8]">{formatArabicDate(msg.createdAt)}</span>
                  </div>
                </>
              )}
            </div>
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Send form */}
      <form onSubmit={handleSend} className="flex gap-2">
        <input
          type="text"
          value={newMessage}
          onChange={(e) => setNewMessage(e.target.value)}
          placeholder="اكتب رسالة..."
          className="flex-1 text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue"
          disabled={sending}
        />
        <button
          type="submit"
          disabled={sending || !newMessage.trim()}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50 transition"
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
