"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { FileSignature, AlertCircle, Plus, ChevronLeft } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { EmptyState } from "./EmptyState";
import { financeV3ContractsUrl } from "@/lib/financeRoutes";
import type { Contract } from "@/types/finance";

// ─── Constants ────────────────────────────────────────────────────────────────

const CONTRACT_STATUS: Record<string, { label: string; color: string; bg: string }> = {
  active:    { label: "نشط",   color: "text-green-700",  bg: "bg-green-50" },
  completed: { label: "مكتمل", color: "text-blue-700",   bg: "bg-blue-50" },
  cancelled: { label: "ملغى",  color: "text-[#64748b]",  bg: "bg-[#f1f5f9]" },
  overdue:   { label: "متأخر", color: "text-red-700",    bg: "bg-red-50" },
};

const SPECIALTY_LABELS: Record<string, string> = {
  General: "طب أسنان عام",
  Orthodontics: "تقويم أسنان",
  OralSurgery: "جراحة فم",
  Periodontics: "لثة",
  Endodontics: "علاج عصب",
  Prosthodontics: "تركيبات",
  orthodontics: "تقويم أسنان",
  general: "طب أسنان عام",
  surgery: "جراحة",
  other: "أخرى",
};

// ─── Component ────────────────────────────────────────────────────────────────

interface ContractsTabProps {
  patientId: string;
}

export function ContractsTab({ patientId }: ContractsTabProps) {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const fetchContracts = useCallback(() => {
    setLoading(true);
    setError("");
    api.get<Contract[]>(`/api/contracts?patientId=${patientId}`)
      .then((r) => setContracts(r.data))
      .catch(() => setError("فشل تحميل العقود"))
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchContracts();
  }, [fetchContracts]);

  const totalContractValue = contracts.reduce((s, c) => s + c.totalAmount, 0);
  const totalPaid = contracts.reduce((s, c) => s + c.paidAmount, 0);
  const totalRemaining = contracts.reduce((s, c) => s + c.remainingAmount, 0);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-20 bg-[#f1f5f9] rounded-xl" />
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-center py-12">
        <AlertCircle className="w-10 h-10 mx-auto mb-2 text-red-300" />
        <p className="text-sm text-red-500">{error}</p>
        <button onClick={fetchContracts} className="mt-2 text-xs text-[#3d7ab5] hover:underline">إعادة المحاولة</button>
      </div>
    );
  }

  if (contracts.length === 0) {
    return (
      <EmptyState
        icon={FileSignature}
        title="لا توجد عقود مسجّلة"
        description="لم يتم تسجيل أي عقود لهذا المريض"
        actionLabel="إنشاء عقد"
        actionHref={financeV3ContractsUrl(patientId)}
      />
    );
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-bold text-[#0d2137]">العقود</h3>
        <Link
          href={financeV3ContractsUrl(patientId)}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          عقد جديد
        </Link>
      </div>

      {/* Summary */}
      <div className="grid grid-cols-3 gap-3">
        <div className="rounded-xl px-3 py-2 bg-[#3d7ab518] flex items-center gap-2">
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">إجمالي العقود</p>
            <p className="text-sm font-bold text-[#3d7ab5]">{totalContractValue.toLocaleString()} ر.ي</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2 bg-green-50 flex items-center gap-2">
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">المدفوع</p>
            <p className="text-sm font-bold text-green-600">{totalPaid.toLocaleString()} ر.ي</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2 bg-amber-50 flex items-center gap-2">
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">المتبقي</p>
            <p className="text-sm font-bold text-amber-600">{totalRemaining.toLocaleString()} ر.ي</p>
          </div>
        </div>
      </div>

      {/* Contract List */}
      <div className="space-y-2">
        {contracts.map((c) => {
          const statusDef = CONTRACT_STATUS[c.status] ?? CONTRACT_STATUS.active;
          const paidPercent = c.totalAmount > 0 ? Math.min(100, Math.round((c.paidAmount / c.totalAmount) * 100)) : 0;
          return (
            <Link key={c.id} href={financeV3ContractsUrl(patientId)}
              className="block p-3.5 bg-white border border-[#e8f0f9] rounded-xl hover:border-[#d6e8fa] transition"
            >
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <FileSignature className="w-4 h-4 text-[#3d7ab5]" />
                  <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", statusDef.bg, statusDef.color)}>
                    {statusDef.label}
                  </span>
                  {c.specialty && (
                    <span className="text-xs text-[#94a3b8]">{SPECIALTY_LABELS[c.specialty] ?? c.specialty}</span>
                  )}
                </div>
                <ChevronLeft className="w-4 h-4 text-[#94a3b8]" />
              </div>

              {/* Amounts */}
              <div className="flex items-center justify-between mb-2">
                <div>
                  <span className="text-sm font-bold text-[#0d2137]">{c.totalAmount.toLocaleString()} ر.ي</span>
                  {c.discountAmount != null && c.discountAmount > 0 && (
                    <span className="text-xs text-green-600 mr-2">خصم: {c.discountAmount.toLocaleString()}</span>
                  )}
                </div>
                <div className="text-left">
                  <span className="text-xs text-[#64748b]">مدفوع: {c.paidAmount.toLocaleString()}</span>
                  {c.remainingAmount > 0 && (
                    <span className="text-xs text-amber-600 mr-2">متبقي: {c.remainingAmount.toLocaleString()}</span>
                  )}
                </div>
              </div>

              {/* Progress bar */}
              <div className="flex items-center gap-2">
                <div className="flex-1 h-2 bg-[#e8f0f9] rounded-full overflow-hidden">
                  <div
                    className={cn("h-full rounded-full transition-all", paidPercent >= 100 ? "bg-green-500" : "bg-[#3d7ab5]")}
                    style={{ width: `${paidPercent}%` }}
                  />
                </div>
                <span className="text-[10px] text-[#94a3b8] font-medium flex-shrink-0">{paidPercent}%</span>
              </div>

              {/* Installments info */}
              {c.installmentsCount > 1 && c.installmentAmount && (
                <div className="flex items-center gap-3 mt-2 text-[10px] text-[#94a3b8]">
                  <span>أقساط: {c.installmentsCount}</span>
                  <span>القسط: {c.installmentAmount.toLocaleString()} ر.ي</span>
                  {c.startDate && <span>يبدأ: {formatArabicDate(c.startDate)}</span>}
                </div>
              )}
            </Link>
          );
        })}
      </div>
    </div>
  );
}
