import {
  Paperclip,
  Reply,
  X,
  Users,
  ArrowLeft,
  AlertTriangle,
  User,
  Phone,
  Loader2,
  ExternalLink,
  Eye,
  FileText,
  Pencil,
  Check,
  Send,
  MessageCircle,
  Image as ImageIcon,
  Mic,
} from "lucide-react";
import { useState, useRef, useEffect } from "react";
import { cn } from "@/lib/utils";
import { useDeleteMessage, useEditMessage } from "@/hooks/useMessaging";
import { VoiceRecorder } from "@/components/messages/VoiceRecorder";
import type {
  ConversationDetail,
  Message,
  SendMessageRequest,
} from "@/types/messaging";
import api from "@/lib/api";
import {
  MAX_FILE_SIZE,
  ALLOWED_EXTENSIONS,
  ALLOWED_MIME_TYPES,
  toRelativeUploadUrl,
  ROLE_LABELS,
  ConversationTypeBadge,
  RecipientTypeBadge,
} from "./shared";
import { MessageBubble } from "./MessageBubble";

// ─── منطقة الدردشة ──────────────────────────────────────────────────────────

export function ChatArea({
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
  onSend: (req: SendMessageRequest) => void;
  sending: boolean;
  onOpenPatient: (patientId: string) => void;
  sendError?: string;
}) {
  const [input, setInput] = useState("");
  const [replyTo, setReplyTo] = useState<Message | null>(null);
  const [editingMessage, setEditingMessage] = useState<Message | null>(null);
  const [editContent, setEditContent] = useState("");
  const [attachmentPreview, setAttachmentPreview] = useState<{
    url: string; name: string; type: string;
  } | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const deleteMessage = useDeleteMessage(conversation.id);
  const editMessage = useEditMessage(conversation.id);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [conversation.messages]);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Reset file input so re-selecting the same file works
    e.target.value = "";
    setUploadError(null);

    // Validate extension
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) {
      setUploadError("نوع الملف غير مدعوم. الأنواع المسموحة: JPG، PNG، PDF، صوتيات");
      return;
    }

    // Validate MIME type
    if (!ALLOWED_MIME_TYPES.includes(file.type)) {
      setUploadError("نوع الملف غير مدعوم. الأنواع المسموحة: JPG، PNG، PDF، صوتيات");
      return;
    }

    // Validate size
    if (file.size > MAX_FILE_SIZE) {
      setUploadError("حجم الملف كبير جداً. الحد الأقصى 10 ميجابايت.");
      return;
    }

    // Upload
    setIsUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const { data } = await api.post<{
        url: string; fileName: string; originalName: string; size: number; contentType: string;
      }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setAttachmentPreview({
        url: data.url,
        name: data.originalName,
        type: data.contentType,
      });
    } catch {
      setUploadError("فشل رفع الملف. حاول مرة أخرى.");
    } finally {
      setIsUploading(false);
    }
  };

  const removeAttachment = () => {
    setAttachmentPreview(null);
    setUploadError(null);
  };

  const handleVoiceRecorded = async (blob: Blob, mimeType: string) => {
    setIsUploading(true);
    setUploadError(null);
    try {
      const ext = mimeType === "audio/ogg" ? ".ogg" : mimeType === "audio/mp4" ? ".mp4" : ".webm";
      const formData = new FormData();
      formData.append("file", blob, `voice${ext}`);
      const { data } = await api.post<{
        url: string; fileName: string; originalName: string; contentType: string;
      }>("/api/uploads", formData, { headers: { "Content-Type": "multipart/form-data" } });
      setAttachmentPreview({ url: data.url, name: data.originalName, type: data.contentType });
    } catch {
      setUploadError("فشل رفع الرسالة الصوتية");
    } finally {
      setIsUploading(false);
    }
  };

  const handleEditSubmit = () => {
    if (!editingMessage || !editContent.trim() || editMessage.isPending) return;
    editMessage.mutate(
      { messageId: editingMessage.id, content: editContent.trim() },
      {
        onSuccess: () => {
          setEditingMessage(null);
          setEditContent("");
        },
      }
    );
  };

  const handleSend = () => {
    const trimmed = input.trim();
    if ((!trimmed && !attachmentPreview) || sending || trimmed.length > 2000 || isUploading) return;

    const req: SendMessageRequest = { content: trimmed };
    if (replyTo) {
      req.replyToId = replyTo.id;
    }
    if (attachmentPreview) {
      req.attachmentUrl = toRelativeUploadUrl(attachmentPreview.url);
      req.attachmentName = attachmentPreview.name;
      req.attachmentType = attachmentPreview.type;
    }
    onSend(req);
    setInput("");
    setReplyTo(null);
    setAttachmentPreview(null);
    setUploadError(null);
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
            onReply={() => { setReplyTo(msg); setEditingMessage(null); inputRef.current?.focus(); }}
            onEdit={() => { setEditingMessage(msg); setEditContent(msg.content); setReplyTo(null); }}
            onDelete={() => deleteMessage.mutate(msg.id)}
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

      {/* Edit mode banner */}
      {editingMessage && (
        <div className="px-4 py-2 bg-amber-50 border-t border-amber-200 flex items-center gap-2">
          <Pencil className="w-4 h-4 text-amber-600 flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-amber-700">تعديل الرسالة</p>
            <p className="text-xs text-amber-600 truncate">{editingMessage.content}</p>
          </div>
          <button onClick={() => { setEditingMessage(null); setEditContent(""); }} className="text-amber-500 hover:text-amber-700">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Reply preview */}
      {replyTo && !editingMessage && (
        <div className="px-4 py-2 bg-gray-50 border-t border-gray-200 flex items-center gap-2">
          <Reply className="w-4 h-4 text-[#3d7ab5] flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-[#3d7ab5]">
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

      {/* Upload error */}
      {uploadError && (
        <div className="px-4 py-2 bg-amber-50 border-t border-amber-200 flex items-center gap-2">
          <AlertTriangle className="w-3.5 h-3.5 text-amber-500 flex-shrink-0" />
          <p className="text-xs text-amber-700">{uploadError}</p>
          <button onClick={() => setUploadError(null)} className="ms-auto">
            <X className="w-3 h-3 text-amber-500" />
          </button>
        </div>
      )}

      {/* Input area */}
      <div className="px-4 py-3 border-t border-gray-100 bg-white">
        {/* Attachment preview chip */}
        {attachmentPreview && (
          <div className="flex items-center gap-2 mb-2 px-3 py-2 bg-gray-50 rounded-lg">
            {attachmentPreview.type.startsWith("image/") ? (
              <ImageIcon className="w-4 h-4 text-[#3d7ab5] flex-shrink-0" />
            ) : attachmentPreview.type.startsWith("audio/") ? (
              <Mic className="w-4 h-4 text-purple-500 flex-shrink-0" />
            ) : (
              <FileText className="w-4 h-4 text-red-500 flex-shrink-0" />
            )}
            <span className="text-xs text-gray-700 truncate flex-1">{attachmentPreview.name}</span>
            <button
              type="button"
              onClick={removeAttachment}
              className="w-5 h-5 rounded-full bg-gray-200 hover:bg-gray-300 flex items-center justify-center transition flex-shrink-0"
            >
              <X className="w-3 h-3 text-gray-500" />
            </button>
          </div>
        )}

        {/* Uploading indicator */}
        {isUploading && (
          <div className="flex items-center gap-2 mb-2 px-3 py-2 bg-[#3d7ab5]/5 rounded-lg">
            <Loader2 className="w-4 h-4 text-[#3d7ab5] animate-spin" />
            <span className="text-xs text-[#3d7ab5]">جارٍ رفع الملف...</span>
          </div>
        )}

        {editingMessage ? (
          /* Edit mode input */
          <div className="flex items-end gap-2">
            <textarea
              value={editContent}
              onChange={(e) => setEditContent(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); handleEditSubmit(); } }}
              rows={1}
              maxLength={2000}
              className="flex-1 resize-none border border-amber-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 max-h-32 bg-amber-50/30"
              style={{ minHeight: "40px" }}
              autoFocus
            />
            <button
              onClick={handleEditSubmit}
              disabled={!editContent.trim() || editMessage.isPending}
              className={cn(
                "w-10 h-10 rounded-lg flex items-center justify-center transition flex-shrink-0",
                editContent.trim() && !editMessage.isPending
                  ? "bg-amber-500 text-white hover:bg-amber-600"
                  : "bg-gray-100 text-gray-400 cursor-not-allowed"
              )}
              title="حفظ التعديل"
            >
              {editMessage.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
            </button>
          </div>
        ) : (
          /* Normal send mode */
          <div className="flex items-end gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept=".jpg,.jpeg,.png,.pdf,.webm,.ogg,.mp4,.m4a,.mp3,.wav"
              className="hidden"
              onChange={handleFileSelect}
            />
            <textarea
              ref={inputRef}
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="اكتب رسالتك..."
              rows={1}
              maxLength={2000}
              disabled={sending || isUploading}
              className="flex-1 resize-none border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] max-h-32 disabled:opacity-50"
              style={{ minHeight: "40px" }}
            />
            <VoiceRecorder
              onRecorded={handleVoiceRecorded}
              disabled={sending || isUploading || !!attachmentPreview}
            />
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              disabled={sending || isUploading || !!attachmentPreview}
              className={cn(
                "w-10 h-10 rounded-lg flex items-center justify-center transition flex-shrink-0",
                sending || isUploading || attachmentPreview
                  ? "bg-gray-100 text-gray-400 cursor-not-allowed"
                  : "bg-gray-100 text-gray-500 hover:bg-gray-200 hover:text-gray-700"
              )}
              title="إرفاق ملف"
            >
              <Paperclip className="w-4 h-4" />
            </button>
            <button
              onClick={handleSend}
              disabled={(!input.trim() && !attachmentPreview) || sending || isUploading || input.length > 2000}
              className={cn(
                "w-10 h-10 rounded-lg flex items-center justify-center transition flex-shrink-0",
                (input.trim() || attachmentPreview) && !sending && !isUploading && input.length <= 2000
                  ? "bg-[#3d7ab5] text-white hover:opacity-90"
                  : "bg-gray-100 text-gray-400 cursor-not-allowed"
              )}
            >
              {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
            </button>
          </div>
        )}
        {input.length > 1800 && !editingMessage && (
          <p className="text-xs text-amber-500 mt-1 text-left" dir="ltr">
            {input.length}/2000
          </p>
        )}
      </div>
    </>
  );
}
