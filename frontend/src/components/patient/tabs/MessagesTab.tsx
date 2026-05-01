"use client";

import { useEffect, useState } from "react";
import { MessageCircle, Send } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

interface ConversationDto {
  id: string;
  patientId: string;
  patientName?: string;
  lastMessage?: string;
  lastMessageAt?: string;
  unreadCount?: number;
}

interface MessageDto {
  id: string;
  content: string;
  senderType: string;
  createdAt: string;
}

interface MessagesTabProps {
  patientId: string;
}

export function MessagesTab({ patientId }: MessagesTabProps) {
  const [conversations, setConversations] = useState<ConversationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedConv, setSelectedConv] = useState<string | null>(null);
  const [messages, setMessages] = useState<MessageDto[]>([]);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [newMessage, setNewMessage] = useState("");
  const [sending, setSending] = useState(false);

  useEffect(() => {
    api.get<ConversationDto[]>(`/api/messages/conversations?patientId=${patientId}`)
      .then((r) => {
        setConversations(r.data);
        if (r.data.length > 0) {
          setSelectedConv(r.data[0].id);
        }
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    if (!selectedConv) return;
    setLoadingMessages(true);
    api.get<MessageDto[]>(`/api/messages/patient/${patientId}`)
      .then((r) => setMessages(r.data))
      .catch(() => {})
      .finally(() => setLoadingMessages(false));
  }, [patientId, selectedConv]);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newMessage.trim()) return;
    setSending(true);
    try {
      // Create conversation if none exists
      let convId = selectedConv;
      if (!convId) {
        const res = await api.post<{ id: string }>("/api/messages/conversations", {
          patientId,
        });
        convId = res.data.id;
        setSelectedConv(convId);
        setConversations([{ id: convId, patientId }]);
      }
      // Send message - append to messages list optimistically
      setMessages((prev) => [...prev, {
        id: `temp-${Date.now()}`,
        content: newMessage,
        senderType: "Doctor",
        createdAt: new Date().toISOString(),
      }]);
      setNewMessage("");
      toast.success("تم إرسال الرسالة");
    } catch {
      toast.error("فشل إرسال الرسالة");
    } finally {
      setSending(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-14 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  if (conversations.length === 0) {
    return (
      <div className="text-center py-12" dir="rtl">
        <MessageCircle className="w-10 h-10 mx-auto mb-2 text-gray-300" />
        <p className="text-sm text-gray-400 mb-3">لا توجد محادثات مع هذا المريض</p>
        <form onSubmit={handleSend} className="max-w-md mx-auto flex gap-2">
          <input
            type="text"
            value={newMessage}
            onChange={(e) => setNewMessage(e.target.value)}
            placeholder="اكتب رسالة..."
            className="flex-1 text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
          />
          <button type="submit" disabled={sending || !newMessage.trim()} className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-50">
            <Send className="w-3.5 h-3.5" />
            إرسال
          </button>
        </form>
      </div>
    );
  }

  return (
    <div className="space-y-4" dir="rtl">
      {/* Messages list */}
      <div className="max-h-80 overflow-y-auto space-y-2">
        {loadingMessages ? (
          <div className="space-y-2 animate-pulse">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-12 bg-gray-100 rounded-lg" />
            ))}
          </div>
        ) : messages.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">لا توجد رسائل</p>
        ) : (
          messages.map((msg) => (
            <div key={msg.id} className={cn(
              "p-3 rounded-lg max-w-[80%]",
              msg.senderType === "Doctor"
                ? "bg-clinic-teal/10 mr-auto"
                : "bg-gray-50 ml-auto"
            )}>
              <p className="text-sm text-gray-800">{msg.content}</p>
              <p className="text-xs text-gray-400 mt-1">{formatArabicDate(msg.createdAt)}</p>
            </div>
          ))
        )}
      </div>

      {/* Send form */}
      <form onSubmit={handleSend} className="flex gap-2">
        <input
          type="text"
          value={newMessage}
          onChange={(e) => setNewMessage(e.target.value)}
          placeholder="اكتب رسالة..."
          className="flex-1 text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
        />
        <button type="submit" disabled={sending || !newMessage.trim()} className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-50">
          <Send className="w-3.5 h-3.5" />
          إرسال
        </button>
      </form>
    </div>
  );
}
