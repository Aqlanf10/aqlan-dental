"use client";
import { useEffect, useState, useRef, useCallback } from "react";
import { MessageCircle, Send, ArrowRight, Plus, Loader2 } from "lucide-react";
import { usePatientAuthStore } from "@/stores/patientAuthStore";
import portalApi from "@/lib/portalApi";
import { cn } from "@/lib/utils";

interface ConvList { id: string; title: string; isGroup: boolean; conversationType?: string; lastMessageAt?: string; lastMessagePreview?: string; unreadCount: number; }
interface ConvDetail { id: string; title: string; isGroup: boolean; participants: { userId: string; displayName?: string; role?: string }[]; messages: MsgItem[]; createdAt: string; }
interface MsgItem { id: string; senderId: string; senderName: string; content: string; isSystemMessage: boolean; createdAt: string; }

export default function PortalMessagesPage() {
  const { profile } = usePatientAuthStore();
  const [conversations, setConversations] = useState<ConvList[]>([]);
  const [selectedConv, setSelectedConv] = useState<ConvDetail | null>(null);
  const [messages, setMessages] = useState<MsgItem[]>([]);
  const [newMessage, setNewMessage] = useState("");
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [showList, setShowList] = useState(true);
  const [showStartDialog, setShowStartDialog] = useState(false);
  const [startMessage, setStartMessage] = useState("");
  const [starting, setStarting] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Get the patient's userId from the JWT (stored in localStorage)
  const getPatientUserId = useCallback(() => {
    try {
      const token = localStorage.getItem("portal_token");
      if (!token) return null;
      const payload = JSON.parse(atob(token.split(".")[1]));
      return payload.userId || null;
    } catch {
      return null;
    }
  }, []);

  const patientUserId = getPatientUserId();

  const fetchConversations = useCallback(() => {
    portalApi.get<{ Data: ConvList[] }>("/api/portal/messages/conversations")
      .then((r) => setConversations(r.data.Data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { fetchConversations(); }, [fetchConversations]);

  // Auto-refresh every 15s
  useEffect(() => {
    const interval = setInterval(fetchConversations, 15000);
    return () => clearInterval(interval);
  }, [fetchConversations]);

  const openConversation = async (convId: string) => {
    try {
      const { data } = await portalApi.get<ConvDetail>(`/api/portal/messages/conversations/${convId}`);
      setSelectedConv(data);
      setMessages(data.messages);
      setShowList(false);
      fetchConversations();
    } catch { /* ignore */ }
  };

  const handleSend = async () => {
    if (!newMessage.trim() || !selectedConv) return;
    setSending(true);
    try {
      const { data } = await portalApi.post<MsgItem>(`/api/portal/messages/conversations/${selectedConv.id}/messages`, { content: newMessage.trim() });
      setMessages((prev) => [...prev, data]);
      setNewMessage("");
      fetchConversations();
    } catch { /* ignore */ }
    finally { setSending(false); }
  };

  const handleStartConversation = async () => {
    setStarting(true);
    try {
      const payload: { initialMessage?: string } = {};
      if (startMessage.trim()) {
        payload.initialMessage = startMessage.trim();
      }
      const { data } = await portalApi.post<ConvDetail>("/api/portal/messages/conversations", payload);
      setSelectedConv(data);
      setMessages(data.messages);
      setStartMessage("");
      setShowStartDialog(false);
      setShowList(false);
      fetchConversations();
    } catch {
      // If conversation already exists, just refresh list
      fetchConversations();
    } finally {
      setStarting(false);
    }
  };

  // Poll for new messages in open conversation
  useEffect(() => {
    if (!selectedConv) return;
    const interval = setInterval(async () => {
      try {
        const { data } = await portalApi.get<ConvDetail>(`/api/portal/messages/conversations/${selectedConv.id}`);
        setSelectedConv(data);
        setMessages(data.messages);
      } catch { /* ignore */ }
    }, 4000);
    return () => clearInterval(interval);
  }, [selectedConv]);

  useEffect(() => { messagesEndRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages]);

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="w-8 h-8 border-4 border-teal-500 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className="px-4 pb-20" dir="rtl">
      <div className="flex items-center justify-between mb-4 pt-4">
        <h1 className="text-lg font-bold text-gray-900 flex items-center gap-2">
          <MessageCircle className="w-5 h-5 text-teal-600" />
          التواصل مع المركز
        </h1>
        <div className="flex items-center gap-2">
          {profile && <span className="text-xs text-gray-400">{profile.fullName}</span>}
          <button
            onClick={() => setShowStartDialog(true)}
            className="w-8 h-8 rounded-full bg-teal-500 text-white flex items-center justify-center hover:bg-teal-600 transition"
            title="محادثة جديدة"
          >
            <Plus className="w-4 h-4" />
          </button>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden" style={{ height: "calc(100vh - 160px)" }}>
        <div className="flex h-full">
          {/* Conversation List */}
          <div className={cn("w-full md:w-80 border-l border-gray-100 flex flex-col", !showList && "hidden md:flex", showList && "flex")}>
            <div className="p-3 border-b border-gray-100 bg-gray-50">
              <p className="text-xs font-semibold text-gray-500">المحادثات</p>
            </div>
            <div className="flex-1 overflow-y-auto">
              {conversations.length === 0 ? (
                <div className="text-center py-12 text-gray-400">
                  <MessageCircle className="w-8 h-8 mx-auto mb-2 opacity-30" />
                  <p className="text-sm">لا توجد محادثات</p>
                  <p className="text-xs mt-1">اضغط + للتواصل مع المركز</p>
                </div>
              ) : conversations.map((conv) => (
                <button key={conv.id} onClick={() => openConversation(conv.id)}
                  className={cn("w-full p-3 border-b border-gray-50 text-right hover:bg-teal-50/50 transition", selectedConv?.id === conv.id && "bg-teal-50")}>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2 min-w-0">
                      {conv.unreadCount > 0 && <span className="w-5 h-5 bg-teal-500 text-white text-[10px] rounded-full flex items-center justify-center flex-shrink-0">{conv.unreadCount}</span>}
                      <span className="text-sm font-semibold text-gray-900 truncate">{conv.title}</span>
                    </div>
                    {conv.lastMessageAt && <span className="text-[10px] text-gray-400 flex-shrink-0">{new Date(conv.lastMessageAt).toLocaleDateString("ar")}</span>}
                  </div>
                  {conv.lastMessagePreview && <p className="text-xs text-gray-500 mt-0.5 truncate">{conv.lastMessagePreview}</p>}
                </button>
              ))}
            </div>
          </div>

          {/* Chat Area */}
          <div className={cn("flex-1 flex flex-col", showList && "hidden md:flex")}>
            {selectedConv ? (
              <>
                <div className="p-3 border-b border-gray-100 bg-gray-50 flex items-center gap-3">
                  <button onClick={() => setShowList(true)} className="md:hidden p-1 text-gray-500 hover:text-gray-700"><ArrowRight className="w-4 h-4" /></button>
                  <div>
                    <p className="text-sm font-semibold text-gray-900">{selectedConv.title}</p>
                    <p className="text-xs text-gray-500">{selectedConv.participants.map((p) => p.displayName).join("، ")}</p>
                  </div>
                </div>
                <div className="flex-1 overflow-y-auto p-4 space-y-3">
                  {messages.map((msg) => {
                    const isMe = msg.senderId === patientUserId;
                    return (
                      <div key={msg.id} className={cn("flex", isMe ? "justify-start" : "justify-end")}>
                        <div className={cn("max-w-[80%] rounded-2xl px-4 py-2.5", isMe ? "bg-teal-500 text-white rounded-bl-sm" : "bg-gray-100 text-gray-900 rounded-br-sm")}>
                          {!isMe && <p className="text-[10px] font-semibold mb-0.5 text-teal-600">{msg.senderName}</p>}
                          <p className="text-sm leading-relaxed">{msg.content}</p>
                          <p className={cn("text-[10px] mt-1", isMe ? "text-white/60" : "text-gray-400")}>{new Date(msg.createdAt).toLocaleTimeString("ar", { hour: "2-digit", minute: "2-digit" })}</p>
                        </div>
                      </div>
                    );
                  })}
                  <div ref={messagesEndRef} />
                </div>
                <div className="p-3 border-t border-gray-100 bg-gray-50">
                  <div className="flex items-center gap-2">
                    <input type="text" value={newMessage} onChange={(e) => setNewMessage(e.target.value)}
                      onKeyDown={(e) => e.key === "Enter" && !e.shiftKey && handleSend()} placeholder="اكتب رسالة..."
                      className="flex-1 px-4 py-2.5 rounded-full border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-teal-500 bg-white" disabled={sending} />
                    <button onClick={handleSend} disabled={sending || !newMessage.trim()}
                      className="w-10 h-10 rounded-full flex items-center justify-center transition bg-teal-500 text-white hover:bg-teal-600 disabled:opacity-40"><Send className="w-4 h-4" /></button>
                  </div>
                </div>
              </>
            ) : (
              <div className="flex-1 flex items-center justify-center text-gray-400">
                <div className="text-center"><MessageCircle className="w-10 h-10 mx-auto mb-2 opacity-30" /><p className="text-sm">اختر محادثة للبدء</p></div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Start Conversation Dialog */}
      {showStartDialog && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl" dir="rtl">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h3 className="font-bold text-gray-900">تواصل مع المركز</h3>
              <button onClick={() => setShowStartDialog(false)} className="text-gray-400 hover:text-gray-600 text-xl">&times;</button>
            </div>
            <div className="p-5 space-y-4">
              <p className="text-sm text-gray-600">اكتب رسالتك وسيتواصل معك الطاقم الطبي في أقرب وقت.</p>
              <textarea
                value={startMessage}
                onChange={(e) => setStartMessage(e.target.value)}
                placeholder="مثال: أريد حجز موعد... / عندي استفسار عن..."
                rows={4}
                maxLength={2000}
                className="w-full px-4 py-3 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-teal-500 resize-none"
                autoFocus
              />
            </div>
            <div className="px-5 py-4 border-t border-gray-100">
              <button
                onClick={handleStartConversation}
                disabled={starting}
                className="w-full py-3 rounded-xl font-semibold text-sm transition bg-teal-500 text-white hover:bg-teal-600 disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {starting ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    جارٍ الإرسال...
                  </>
                ) : (
                  <>
                    <Send className="w-4 h-4" />
                    إرسال الرسالة
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
