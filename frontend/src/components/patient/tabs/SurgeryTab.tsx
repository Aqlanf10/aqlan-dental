"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Scissors, Plus } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import type { SurgeryCase } from "@/types/surgery";
import { StatusBadge } from "@/components/surgery";

interface SurgeryTabProps {
  patientId: string;
}

export function SurgeryTab({ patientId }: SurgeryTabProps) {
  const [cases, setCases] = useState<SurgeryCase[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setFetchError(false);
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientId}`)
      .then((r) => setCases(r.data.data ?? []))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);

  /* ── Loading skeleton ──────────────────────────────────────────────────── */
  if (loading) {
    return (
      <div className="space-y-3" dir="rtl">
        {/* header skeleton */}
        <div className="flex items-center justify-between">
          <div className="h-5 w-40 bg-[#f1f5f9] rounded-md animate-pulse" />
          <div className="h-8 w-28 bg-[#f1f5f9] rounded-lg animate-pulse" />
        </div>
        {/* card skeletons */}
        {Array.from({ length: 4 }).map((_, i) => (
          <div
            key={i}
            className="h-[88px] bg-[#f1f5f9] rounded-xl animate-pulse"
          />
        ))}
      </div>
    );
  }

  /* ── Error state ────────────────────────────────────────────────────────── */
  if (fetchError) {
    return (
      <div className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }

  /* ── Empty state ───────────────────────────────────────────────────────── */
  if (cases.length === 0) {
    return (
      <EmptyState
        icon={Scissors}
        title="لا توجد حالات جراحية"
        description="لم يتم تسجيل أي حالات جراحية لهذا المريض"
        actionLabel="حالة جراحية جديدة"
        actionHref={`/surgery/new?patientId=${patientId}`}
        actionVariant="outline"
      />
    );
  }

  /* ── Cases list ────────────────────────────────────────────────────────── */
  return (
    <div dir="rtl">
      {/* ── Header ─────────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-[#0d2137]">
          الحالات الجراحية ({cases.length})
        </h3>
        <Link
          href={`/surgery/new?patientId=${patientId}`}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          حالة جديدة
        </Link>
      </div>

      {/* ── Cards ───────────────────────────────────────────────────────── */}
      <div className="space-y-2.5">
        {cases.map((c) => (
          <Link
            key={c.id}
            href={`/surgery/${c.id}`}
            className="block bg-white border border-[#e8f0f9] rounded-xl shadow-sm hover:shadow-md hover:border-red-200 hover:bg-red-50/20 transition"
          >
            <div className="flex items-start justify-between p-4 gap-3">
              {/* Left: icon + info */}
              <div className="flex items-start gap-3 min-w-0">
                <div className="w-10 h-10 rounded-lg bg-red-50 flex items-center justify-center flex-shrink-0 mt-0.5">
                  <Scissors className="w-5 h-5 text-red-500" />
                </div>
                <div className="min-w-0 space-y-1">
                  {/* Title row */}
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-semibold text-[#0d2137]">
                      {c.caseNumber}
                    </span>
                    <span className="text-xs text-[#64748b] font-medium">
                      {c.surgeryType}
                    </span>
                  </div>

                  {/* Doctor */}
                  {c.doctorName && (
                    <p className="text-xs text-[#94a3b8] truncate">
                      {c.doctorName}
                    </p>
                  )}

                  {/* Teeth */}
                  {c.teethInvolved && c.teethInvolved.length > 0 && (
                    <div className="flex items-center gap-1 flex-wrap">
                      <span className="text-[11px] text-[#94a3b8]">الأسنان:</span>
                      <div className="flex items-center gap-1 flex-wrap">
                        {c.teethInvolved.map((tooth) => (
                          <span
                            key={tooth}
                            className="inline-block text-[11px] font-medium text-[#3d7ab5] bg-[#3d7ab518] px-1.5 py-0.5 rounded"
                          >
                            {tooth}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Created date */}
                  {c.createdAt && (
                    <p className="text-[11px] text-[#cbd5e1]">
                      {new Date(c.createdAt).toLocaleDateString("ar-EG", {
                        year: "numeric",
                        month: "short",
                        day: "numeric",
                      })}
                    </p>
                  )}
                </div>
              </div>

              {/* Right: status badge */}
              <StatusBadge status={c.status} />
            </div>
          </Link>
        ))}
      </div>

      {/* ── Footer: link to full surgery module ─────────────────────────── */}
      <Link
        href="/surgery"
        className="block text-center text-xs font-medium text-[#3d7ab5] hover:underline mt-4 py-2"
      >
        عرض جميع الحالات الجراحية ←
      </Link>
    </div>
  );
}
