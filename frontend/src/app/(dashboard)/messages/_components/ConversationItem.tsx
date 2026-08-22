"use client";
import { User, Users } from "lucide-react";
import { cn } from "@/lib/utils";
import type { ConversationListItem } from "@/types/messaging";
import { formatTime, ConversationTypeBadge, RecipientTypeBadge } from "./shared";

// ─── عنصر المحادثة في القائمة ───────────────────────────────────────────────

export function ConversationItem({
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
        "w-full flex items-center gap-3 px-4 py-3 text-start transition-colors border-b border-gray-50",
        isSelected
          ? "bg-[#3d7ab5]/5 border-r-4 border-r-[#3d7ab5]"
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
              <span className="bg-[#3d7ab5] text-white text-[10px] font-bold rounded-full min-w-[18px] h-[18px] flex items-center justify-center px-1 flex-shrink-0">
                {conv.unreadCount > 99 ? "99+" : conv.unreadCount}
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
