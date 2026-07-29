
import { useEffect, useState, useCallback, useRef } from "react";
import { MessageCircle, Send, Loader2, AlertTriangle, ExternalLink, Eye } from "lucide-react";
import { useConversation, useInternalPatientConversation, useMarkAsRead, useSendMessage } from "@/hooks/useMessaging";
import { useAuthStore } from "@/stores/authStore";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";
import { useRouter } from "@/lib/nextNavCompat";
import type { Message } from "@/types/messaging";

interface MessagesTabProps {
  patientId: string;
}

export function MessagesTab({ patientId }: MessagesTabProps) {
  const router = useRouter();
  const currentUserId = useAuthStore((s) => s.user?.id);
  const [newMessage, setNewMessage] = useState("");
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [initializationError, setInitializationError] = useState("");
  const [retryNonce, setRetryNonce] = useState(0);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, []);

  // محاولة جلب المحادثة الداخلية الموجودة
  const { data: conversation, isLoading: loading, error: fetchError } = useConversation(conversationId);

  // إنشاء محادثة داخلية إذا لم تكن موجودة
  const createConversation = useInternalPatientConversation();
  const sendMessage = useSendMessage(conversationId ?? "");
  const markAsRead = useMarkAsRead(conversationId ?? "");

  // إنشاء/جلب المحادثة عند التحميل. فشل هذا الطلب ليس مرادفاً لعدم وجود
  // محادثة؛ يجب أن يظهر كعطل قابل لإعادة المحاولة، وإلا يبدو التبويب فارغاً
  // ويسمح بمحاولة إرسال إلى conversationId فارغ.
  useEffect(() => {
    if (!conversationId) {
      setInitializationError("");
      createConversation.mutate(patientId, {
        onSuccess: (data) => {
          setInitializationError("");
          setConversationId(data.id);
        },
        onError: (error: unknown) => {
          setInitializationError(
            extractErrorMessage(error, "تعذر فتح محادثة المريض — تحقق من الاتصال وحاول مجدداً")
          );
        },
      });
    }
  }, [patientId, retryNonce]); // eslint-disable-line react-hooks/exhaustive-deps

  const retryConversationLoad = () => {
    setInitializationError("");
    setConversationId(null);
    setRetryNonce((value) => value + 1);
  };

  // تمرير لأسفل عند وصول رسائل جديدة
  useEffect(() => {
    scrollToBottom();
  }, [conversation?.messages?.length, scrollToBottom]);

  // تحديد الرسائل كمقروءة عند فتح المحادثة
  useEffect(() => {
    if (conversationId && conversation) {
      markAsRead.mutate();
    }
  }, [conversationId, conversation?.messages?.length]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    const content = newMessage.trim();
    if (!conversation || !content || sendMessage.isPending) return;
    if (content.length > 2000) {
      toast.error("الرسالة طويلة جداً — الحد الأقصى 2000 حرف");
      return;
    }

    sendMessage.mutate(
      { content },
      {
        onSuccess: () => {
          setNewMessage("");
          toast.success("تم إرسال الرسالة");
        },
        onError: (err: unknown) => {
          const status = (err as { response?: { status?: number } })?.response?.status;
          if (status === 403) {
            toast.error("ليس لديك صلاحية إرسال رسائل في هذه المحادثة");
          } else {
            toast.error("فشل إرسال الرسالة — تحقق من الاتصال وحاول مجدداً");
          }
        },
      }
    );
  };

  if (loading || createConversation.isPending) {
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

  const loadErrorMessage = initializationError ||
    (fetchError ? extractErrorMessage(fetchError, "فشل تحميل محادثة المريض") : "");

  if (loadErrorMessage && !conversation) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 p-6 text-center">
        <AlertTriangle className="w-10 h-10 mx-auto mb-3 text-red-400" />
        <p className="text-sm font-semibold text-red-700">{loadErrorMessage}</p>
        <p className="text-xs text-red-500 mt-1">
          لم نعتبر فشل الطلب «لا توجد محادثة»، ولم يتم إرسال أي رسالة دون معرّف صحيح.
        </p>
        <button
          type="button"
          onClick={retryConversationLoad}
          className="mt-4 px-4 py-2 text-sm font-semibold rounded-lg bg-white border border-red-200 text-red-700 hover:bg-red-100 transition"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  const messages = conversation?.messages ?? [];
  return (
    <div className="flex flex-col h-full">
      {conversation && (
        <div className="flex items-center justify-between pb-3 mb-3 border-b border-gray-100">
          <div className="flex items-center gap-2">
            <MessageCircle className="w-4 h-4 text-gray-500" />
            <span className="text-sm font-medium text-gray-700">
              محادثة حول المريض
            </span>
            {conversation.conversationType === "PatientFacing" ? (
              <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-orange-100 text-orange-600 font-semibold flex items-center gap-1">
                <Eye className="w-2.5 h-2.5" />
                مرئية للمريض
              </span>
            ) : (
              <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-500 font-semibold">
                لا تظهر للمريض
              </span>
            )}
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

      {loadErrorMessage && conversation && (
        <div className="mb-3 p-2 bg-amber-50 border border-amber-200 rounded-lg text-amber-700 text-xs flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
          {loadErrorMessage}
          <button
            type="button"
            onClick={retryConversationLoad}
            className="text-amber-600 hover:text-amber-800 underline ms-auto"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      <div className="flex-1 max-h-96 overflow-y-auto space-y-2 mb-3">
        {messages.length === 0 ? (
          <EmptyState
            icon={MessageCircle}
            title="لا توجد رسائل"
            description="ابدأ محادثة داخلية حول المريض"
          />
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

      {conversation?.conversationType === "PatientFacing" && (
        <div
          className="mb-3 p-3 rounded-lg flex items-start gap-2"
          style={{ background: "#f5922e10", border: "1px solid #f5922e30" }}
        >
          <Eye className="w-4 h-4 flex-shrink-0 mt-0.5" style={{ color: "#f5922e" }} />
          <div className="flex-1">
            <p className="text-xs font-bold" style={{ color: "#f5922e" }}>
              هذه المحادثة مرئية للمريض في البوابة
            </p>
            <p className="text-[11px] mt-0.5" style={{ color: "#c2410c" }}>
              الرسائل المُرسلة هنا ستظهر للمريض عند تسجيل دخوله في البوابة. لا تكتب معلومات سرية أو ملاحظات داخلية.
            </p>
          </div>
        </div>
      )}

      {conversation && (
        <form onSubmit={handleSend} className="flex gap-2 pt-3 border-t border-gray-100">
          <input
            type="text"
            value={newMessage}
            onChange={(e) => setNewMessage(e.target.value)}
            placeholder="اكتب رسالة..."
            maxLength={2000}
            className="flex-1 text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue focus:ring-1 focus:ring-clinic-blue"
            disabled={sendMessage.isPending}
          />
          <button
            type="submit"
            disabled={sendMessage.isPending || !newMessage.trim()}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            {sendMessage.isPending ? (
              <Loader2 className="w-3.5 h-3.5 animate-spin" />
            ) : (
              <Send className="w-3.5 h-3.5" />
            )}
            إرسال
          </button>
        </form>
      )}
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
          {!isMine && (
            <p className="text-xs font-semibold text-clinic-blue mb-0.5">
              {message.senderName}
            </p>
          )}
          <p className="whitespace-pre-wrap break-words leading-relaxed">
            {message.content}
          </p>
        </div>

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
