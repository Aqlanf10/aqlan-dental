"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { GitBranch, User, Stethoscope, Scissors, Loader2, Inbox } from "lucide-react";
import type { OrthoSurgicalCaseListItem } from "@/types/orthoSurgical";
import { ORTHO_SURGICAL_STATUS_COLORS } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

export default function OrthoSurgicalListPage() {
  const [items, setItems] = useState<OrthoSurgicalCaseListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [pendingOnly, setPendingOnly] = useState(false);

  const fetchList = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, string> = { pageSize: "100" };
      if (pendingOnly) params.pendingSurgeonReview = "true";
      const res = await api.get<{ data: OrthoSurgicalCaseListItem[] }>("/api/ortho-surgical-cases", { params });
      setItems(res.data?.data ?? []);
    } catch {
      toast.error("فشل تحميل الحالات");
    } finally {
      setLoading(false);
    }
  }, [pendingOnly]);

  useEffect(() => { fetchList(); }, [fetchList]);

  return (
    <div className="space-y-5 max-w-4xl">
      <div className="flex items-center gap-3">
        <GitBranch className="w-6 h-6 text-[#3d7ab5]" />
        <h1 className="text-2xl font-extrabold text-gray-900">التخطيط التقويمي الجراحي</h1>
      </div>
      <p className="text-sm text-gray-500">
        الحالات المشتركة بين أخصائي التقويم وأخصائي جراحة الفكين — ملف واحد لسير العمل والاعتماد.
      </p>

      <div className="flex items-center gap-3">
        <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
          <input type="checkbox" checked={pendingOnly} onChange={(e) => setPendingOnly(e.target.checked)}
            className="w-4 h-4 rounded border-gray-300 text-[#3d7ab5] focus:ring-[#3d7ab5]" />
          بانتظار مراجعة الجراح فقط
        </label>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-24 text-gray-400">
          <Loader2 className="w-6 h-6 animate-spin" />
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-gray-400">
          <Inbox className="w-12 h-12 mb-3 opacity-30" />
          <p className="text-sm">لا توجد حالات تقويمية جراحية{pendingOnly ? " بانتظار المراجعة" : ""}</p>
          <p className="text-xs mt-2">تُنشأ الحالة من ملف المريض ← العمليات والعلاجات ← التقويم الجراحي.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((c) => (
            <Link key={c.id} href={`/ortho-surgical/${c.id}`}
              className="block bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-4 hover:border-[#3d7ab5] transition">
              <div className="flex items-start justify-between gap-3">
                <div className="space-y-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-mono text-xs font-semibold text-[#3d7ab5]">{c.caseNumber}</span>
                    <span className="text-gray-300">·</span>
                    <span className="flex items-center gap-1 text-sm font-semibold text-gray-800">
                      <User className="w-3.5 h-3.5 text-gray-400" /> {c.patientName}
                    </span>
                    <span className="text-xs text-gray-400 font-mono">{c.patientNumber}</span>
                  </div>
                  <div className="flex items-center gap-3 text-xs text-gray-500 flex-wrap">
                    <span className="flex items-center gap-1"><Stethoscope className="w-3 h-3" /> {c.orthodontistName ?? "—"}</span>
                    <span className="flex items-center gap-1"><Scissors className="w-3 h-3" /> {c.surgeonName ?? "لم يُحدَّد"}</span>
                    <span>المسؤول: {c.responsibleParty}</span>
                  </div>
                </div>
                <div className="flex flex-col items-end gap-1">
                  <span className={cn("text-xs px-2.5 py-1 rounded-full font-medium", ORTHO_SURGICAL_STATUS_COLORS[c.status])}>
                    {c.statusLabel}
                  </span>
                  <span className="text-[11px] text-gray-400">{formatArabicDate(c.createdAt)}</span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
