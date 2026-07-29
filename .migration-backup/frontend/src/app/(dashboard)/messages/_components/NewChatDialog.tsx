"use client";
import { useEffect, useState } from "react";
import {
  Search,
  X,
  Users,
  Loader2,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useCreateConversation } from "@/hooks/useMessaging";
import { type SystemUser, fetchUsers, ROLE_LABELS } from "./shared";

// ─── نافذة محادثة جديدة ──────────────────────────────────────────────────────

export function NewChatDialog({
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
              className="w-full pr-10 pl-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]"
            />
          </div>

          {/* Group title (if multiple selected) */}
          {selected.size > 1 && (
            <input
              type="text"
              placeholder="اسم المجموعة (اختياري)"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]"
            />
          )}

          {/* Users list */}
          {loading ? (
            <div className="flex flex-col items-center justify-center py-8 gap-2">
              <Loader2 className="w-6 h-6 animate-spin text-[#3d7ab5]" />
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
                      ? "bg-[#3d7ab5]/10 border border-[#3d7ab5]/30"
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
                    <div className="w-5 h-5 bg-[#3d7ab5] rounded-full flex items-center justify-center">
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
            className="w-full px-3 py-2 rounded-lg border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] resize-none"
          />
          <button
            onClick={handleCreate}
            disabled={selected.size === 0 || createConv.isPending}
            className={cn(
              "w-full py-2.5 rounded-lg font-semibold text-sm transition",
              selected.size > 0 && !createConv.isPending
                ? "bg-[#3d7ab5] text-white hover:opacity-90"
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
