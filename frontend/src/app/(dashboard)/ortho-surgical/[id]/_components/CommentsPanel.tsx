"use client";

import { useEffect, useState, useCallback } from "react";
import { MessageSquare, Send, Loader2, User } from "lucide-react";
import type { OrthoSurgicalComment } from "@/types/orthoSurgical";
import { AUTHOR_ROLE_LABELS } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

interface CommentsPanelProps {
  orthoSurgicalCaseId: string;
}

/** Sprint A4 — the back-and-forth discussion thread between the orthodontist and the surgeon. */
export function CommentsPanel({ orthoSurgicalCaseId }: CommentsPanelProps) {
  const [comments, setComments] = useState<OrthoSurgicalComment[]>([]);
  const [loading, setLoading] = useState(true);
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);

  const fetchComments = useCallback(async () => {
    try {
      const res = await api.get<{ data: OrthoSurgicalComment[] }>(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/comments`
      );
      setComments(res.data?.data ?? []);
    } catch {
      /* silent — an empty thread is a reasonable fallback */
    } finally {
      setLoading(false);
    }
  }, [orthoSurgicalCaseId]);

  useEffect(() => { fetchComments(); }, [fetchComments]);

  const submit = async () => {
    const trimmed = body.trim();
    if (!trimmed) return;
    setSending(true);
    try {
      await api.post(`/api/ortho-surgical-cases/${orthoSurgicalCaseId}/comments`, { body: trimmed });
      setBody("");
      await fetchComments();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إرسال التعليق");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
        <MessageSquare className="w-4 h-4 text-[#3d7ab5]" /> المناقشة بين التقويم والجراحة
      </h2>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          <div className="h-12 bg-gray-100 rounded-lg" />
          <div className="h-12 bg-gray-100 rounded-lg" />
        </div>
      ) : comments.length === 0 ? (
        <p className="text-xs text-gray-400">لا توجد تعليقات بعد.</p>
      ) : (
        <div className="space-y-3 max-h-80 overflow-y-auto">
          {comments.map((c) => (
            <div key={c.id} className="bg-gray-50 rounded-lg border border-gray-200 p-3">
              <div className="flex items-center justify-between gap-2 mb-1">
                <span className="flex items-center gap-1.5 text-xs font-semibold text-gray-700">
                  <User className="w-3.5 h-3.5 text-gray-400" />
                  {c.authorRole ? (AUTHOR_ROLE_LABELS[c.authorRole] ?? c.authorRole) : "—"}
                </span>
                <span className="text-[11px] text-gray-400">{formatArabicDate(c.createdAt)}</span>
              </div>
              <p className="text-sm text-gray-700 whitespace-pre-wrap">{c.body}</p>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-start gap-2 pt-2 border-t border-[#e8f0f9]">
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          rows={2}
          maxLength={2000}
          placeholder="أضف تعليقًا لأخصائي التقويم/الجراحة..."
          className="flex-1 px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] transition resize-y"
        />
        <button
          onClick={submit}
          disabled={sending || !body.trim()}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition flex-shrink-0"
        >
          {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
        </button>
      </div>
    </div>
  );
}
