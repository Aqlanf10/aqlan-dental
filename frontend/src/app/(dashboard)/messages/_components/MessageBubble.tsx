"use client";
import {
  Reply,
  X,
  CheckCheck,
  Pencil,
  Paperclip,
  FileText,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type { Message } from "@/types/messaging";
import { ROLE_LABELS, formatFullTime, toFullUploadUrl } from "./shared";

// ─── فقاعة الرسالة ──────────────────────────────────────────────────────────

export function MessageBubble({
  message,
  isMine,
  onReply,
  onEdit,
  onDelete,
  participantRoleMap,
}: {
  message: Message;
  isMine: boolean;
  onReply: () => void;
  onEdit: () => void;
  onDelete: () => void;
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
              "text-xs px-3 py-1.5 rounded-lg mb-1 border-r-2 border-[#3d7ab5] bg-gray-100",
              isMine ? "text-end" : "text-start"
            )}
          >
            <span className="font-semibold text-[#3d7ab5]">
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
              ? "bg-[#3d7ab5] text-white rounded-br-md"
              : "bg-white border border-gray-200 text-gray-800 rounded-bl-md"
          )}
        >
          {/* Show sender name + role in group conversations */}
          {!isMine && (
            <p className="text-xs font-semibold text-[#3d7ab5] mb-0.5 flex items-center gap-1.5">
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
          {message.isEdited && (
            <p className={cn("text-[9px] mt-0.5", isMine ? "text-white/60" : "text-gray-400")}>
              (معدّل)
            </p>
          )}

          {/* Attachment(s) — prefer attachments[] if present, fall back to single attachment */}
          {(() => {
            const allAttachments: Array<{ url: string; fileName?: string; fileType?: string }> =
              message.attachments && message.attachments.length > 0
                ? message.attachments.map((a) => ({ url: a.url, fileName: a.fileName, fileType: a.fileType }))
                : message.attachmentUrl
                  ? [{ url: message.attachmentUrl, fileName: message.attachmentName, fileType: message.attachmentType }]
                  : [];
            if (allAttachments.length === 0) return null;
            return (
              <div className="mt-2 space-y-1.5">
                {allAttachments.map((att, idx) =>
                  att.fileType?.startsWith("audio/") ? (
                    <div key={idx} className={cn(isMine ? "text-white/90" : "")}>
                      {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
                      <audio
                        controls
                        src={toFullUploadUrl(att.url)}
                        className="max-w-[220px] h-9 rounded"
                        style={{ filter: isMine ? "invert(1) brightness(0.9)" : "none" }}
                      />
                    </div>
                  ) : att.fileType?.startsWith("image/") ? (
                    <a
                      key={idx}
                      href={toFullUploadUrl(att.url)}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="block"
                    >
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img
                        src={toFullUploadUrl(att.url)}
                        alt={att.fileName ?? "صورة مرفقة"}
                        className="max-w-[200px] rounded-lg cursor-pointer"
                      />
                    </a>
                  ) : att.fileType === "application/pdf" ? (
                    <div
                      key={idx}
                      className={cn(
                        "p-2.5 rounded-lg flex items-center gap-2.5",
                        isMine ? "bg-white/15" : "bg-gray-50 border border-gray-200"
                      )}
                    >
                      <FileText className={cn("w-5 h-5 flex-shrink-0", isMine ? "text-white/80" : "text-red-500")} />
                      <span className="text-xs truncate flex-1">{att.fileName ?? "مرفق PDF"}</span>
                      <a
                        href={toFullUploadUrl(att.url)}
                        target="_blank"
                        rel="noopener noreferrer"
                        className={cn(
                          "text-xs font-semibold underline",
                          isMine ? "text-white/90 hover:text-white" : "text-[#3d7ab5] hover:text-[#3d7ab5]/80"
                        )}
                      >
                        فتح
                      </a>
                    </div>
                  ) : (
                    <a
                      key={idx}
                      href={toFullUploadUrl(att.url)}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={cn(
                        "p-2 rounded-lg flex items-center gap-2",
                        isMine ? "bg-white/10" : "bg-gray-50"
                      )}
                    >
                      <Paperclip className="w-4 h-4 flex-shrink-0" />
                      <span className="text-xs truncate underline">
                        {att.fileName ?? "مرفق"}
                      </span>
                    </a>
                  )
                )}
              </div>
            );
          })()}
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
                message.isReadByMe ? "text-[#3d7ab5]" : "text-gray-300"
              )}
            />
          )}
        </div>

        {/* Action buttons (on hover) */}
        <div className={cn(
          "opacity-0 group-hover:opacity-100 transition-opacity flex items-center gap-2 mt-0.5",
          isMine ? "flex-row-reverse" : "flex-row"
        )}>
          <button
            onClick={onReply}
            className="text-gray-400 hover:text-[#3d7ab5] text-xs flex items-center gap-1"
          >
            <Reply className="w-3 h-3" />
            رد
          </button>
          {isMine && !message.isSystemMessage && message.content !== "تم حذف هذه الرسالة" && (
            <>
              <button
                onClick={onEdit}
                className="text-gray-400 hover:text-amber-500 text-xs flex items-center gap-1"
              >
                <Pencil className="w-3 h-3" />
                تعديل
              </button>
              <button
                onClick={onDelete}
                className="text-gray-400 hover:text-red-500 text-xs flex items-center gap-1"
              >
                <X className="w-3 h-3" />
                حذف
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
