"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import {
  Wallet, CreditCard, FileSignature, AlertTriangle,
  CheckCircle2, Clock, Plus, ChevronLeft, BadgeDollarSign,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import type { PatientFinanceSummary, Payment, Contract } from "@/types/finance";

// ─── Constants ────────────────────────────────────────────────────────────────

const FINANCIAL_STATUS: Record<string, { label: string; color: string; bg: string; icon: typeof Wallet }> = {
  paid_full:  { label: "مدفوع بالكامل",  color: "text-green-700",  bg: "bg-green-50 border-green-200",  icon: CheckCircle2 },
  has_balance:{ label: "عليه متبقي",     color: "text-amber-700",  bg: "bg-amber-50 border-amber-200",  icon: Clock },
  overdue:    { label: "متأخر",          color: "text-red-700",    bg: "bg-red-50 border-red-200",      icon: AlertTriangle },
  no_plan:    { label: "لا توجد خطة مالية", color: "text-[#94a3b8]", bg: "bg-[#f7fafd] border-[#e8f0f9]", icon: Wallet },
};

const CONTRACT_STATUS: Record<string, { label: string; color: string; bg: string }> = {
  active:    { label: "نشط",   color: "text-green-700",  bg: "bg-green-50" },
  completed: { label: "مكتمل", color: "text-blue-700",   bg: "bg-blue-50" },
  cancelled: { label: "ملغى",  color: "text-[#64748b]",  bg: "bg-[#f1f5f9]" },
  overdue:   { label: "متأخر", color: "text-red-700",    bg: "bg-red-50" },
};

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  cash: "نقد", Cash: "نقد",
  card: "بطاقة", Card: "بطاقة",
  transfer: "تحويل", Transfer: "تحويل",
  bank_transfer: "تحويل بنكي", BankTransfer: "تحويل بنكي",
  other: "أخرى", Other: "أخرى",
};

// ─── Component ────────────────────────────────────────────────────────────────

interface FinanceTabProps {
  patientId: string;
  totalPaid: number;
  totalOutstanding: number;
  onPaymentChanged?: () => void;
}

export function FinanceTab({ patientId, totalPaid, totalOutstanding }: FinanceTabProps) {
  const [financeSummary, setFinanceSummary] = useState<PatientFinanceSummary | null>(null);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [recentPayments, setRecentPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const fetchData = useCallback(() => {
    setLoading(true);
    setError("");
    Promise.all([
      api.get<PatientFinanceSummary>(`/api/patients/${patientId}/finance-summary`).then((r) => r.data).catch(() => null),
      api.get<Contract[]>(`/api/contracts?patientId=${patientId}`).then((r) => r.data).catch(() => []),
      api.get<Payment[]>(`/api/payments?patientId=${patientId}`).then((r) => r.data).catch(() => []),
    ]).then(([summary, c, p]) => {
      setFinanceSummary(summary);
      setContracts(c);
      setRecentPayments(p);
    }).catch(() => {
      setError("فشل تحميل البيانات المالية");
    }).finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Use API summary if available, else fall back to props
  const summary = financeSummary ?? {
    totalTreatmentCost: 0,
    totalPaid,
    outstandingBalance: totalOutstanding,
    overdueAmount: 0,
    latestPayment: null,
    financialStatus: (totalOutstanding > 0 ? "has_balance" : totalPaid > 0 ? "paid_full" : "no_plan") as PatientFinanceSummary["financialStatus"],
    activeContractsCount: 0,
    totalPaymentsCount: 0,
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse" dir="rtl">
        <div className="h-24 bg-[#f1f5f9] rounded-xl" />
        <div className="grid grid-cols-2 gap-3">
          <div className="h-28 bg-[#f1f5f9] rounded-xl" />
          <div className="h-28 bg-[#f1f5f9] rounded-xl" />
        </div>
        <div className="h-48 bg-[#f1f5f9] rounded-xl" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-center py-12" dir="rtl">
        <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
        <p className="text-sm text-red-500">{error}</p>
        <button onClick={fetchData} className="mt-2 text-xs text-[#3d7ab5] hover:underline">إعادة المحاولة</button>
      </div>
    );
  }

  const statusInfo = FINANCIAL_STATUS[summary.financialStatus] ?? FINANCIAL_STATUS.no_plan;
  const StatusIcon = statusInfo.icon;

  return (
    <div className="space-y-5" dir="rtl">

      {/* ════════════════════ Financial Status Badge ════════════════════ */}
      <div className={cn("rounded-xl p-4 border-2 flex items-center justify-between", statusInfo.bg)}>
        <div className="flex items-center gap-3">
          <StatusIcon className={cn("w-6 h-6", statusInfo.color)} />
          <div>
            <p className="text-sm font-bold text-[#0d2137]">الحالة المالية</p>
            <p className={cn("text-lg font-extrabold", statusInfo.color)}>{statusInfo.label}</p>
          </div>
        </div>
        <div className="text-left">
          <p className="text-xs text-[#64748b]">المتبقي</p>
          <p className={cn("text-xl font-extrabold", summary.outstandingBalance > 0 ? "text-amber-600" : "text-green-600")}>
            {summary.outstandingBalance.toLocaleString()} ر.ي
          </p>
        </div>
      </div>

      {/* ════════════════════ Summary Cards ════════════════════ */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-[#3d7ab518]">
              <BadgeDollarSign className="w-3.5 h-3.5 text-[#3d7ab5]" />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">تكلفة العلاج</span>
          </div>
          <p className="text-sm font-bold text-[#0d2137]">{summary.totalTreatmentCost.toLocaleString()} ر.ي</p>
        </div>

        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-green-50">
              <CheckCircle2 className="w-3.5 h-3.5 text-green-600" />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">المدفوع</span>
          </div>
          <p className="text-sm font-bold text-green-600">{summary.totalPaid.toLocaleString()} ر.ي</p>
        </div>

        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-amber-50">
              <Clock className="w-3.5 h-3.5 text-amber-600" />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">المتبقي</span>
          </div>
          <p className="text-sm font-bold text-amber-600">{summary.outstandingBalance.toLocaleString()} ر.ي</p>
        </div>

        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className={cn("w-7 h-7 rounded-lg flex items-center justify-center", summary.overdueAmount > 0 ? "bg-red-50" : "bg-[#f7fafd]")}>
              <AlertTriangle className={cn("w-3.5 h-3.5", summary.overdueAmount > 0 ? "text-red-600" : "text-[#94a3b8]")} />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">متأخر</span>
          </div>
          <p className={cn("text-sm font-bold", summary.overdueAmount > 0 ? "text-red-600" : "text-[#94a3b8]")}>
            {summary.overdueAmount > 0 ? `${summary.overdueAmount.toLocaleString()} ر.ي` : "لا يوجد"}
          </p>
        </div>
      </div>

      {/* ════════════════════ Latest Payment ════════════════════ */}
      {summary.latestPayment && (
        <div className="rounded-xl border border-[#e8f0f9] bg-white overflow-hidden">
          <div className="px-4 py-3 flex items-center justify-between" style={{ background: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
            <div className="flex items-center gap-2">
              <CreditCard className="w-4 h-4 text-green-600" />
              <h3 className="text-sm font-bold text-[#0d2137]">آخر دفعة</h3>
            </div>
            <Link href={`/finance/payments/${summary.latestPayment.id}`} className="text-xs font-semibold text-[#3d7ab5] flex items-center gap-1 hover:underline">
              إيصال
              <ChevronLeft className="w-3 h-3" />
            </Link>
          </div>
          <div className="p-4 flex items-center justify-between">
            <div>
              <p className="text-sm font-bold text-green-600">{summary.latestPayment.amount.toLocaleString()} ر.ي</p>
              <p className="text-xs text-[#64748b]">{formatArabicDate(summary.latestPayment.paymentDate)}</p>
              {summary.latestPayment.paymentMethod && (
                <span className="text-xs bg-[#f1f5f9] text-[#64748b] px-1.5 py-0.5 rounded">
                  {PAYMENT_METHOD_LABELS[summary.latestPayment.paymentMethod] ?? summary.latestPayment.paymentMethod}
                </span>
              )}
            </div>
            {summary.latestPayment.receiptNumber && (
              <span className="text-xs font-mono text-[#94a3b8]">{summary.latestPayment.receiptNumber}</span>
            )}
          </div>
        </div>
      )}

      {/* ════════════════════ Active Contracts ════════════════════ */}
      <div className="rounded-xl border border-[#e8f0f9] bg-white overflow-hidden">
        <div className="px-4 py-3 flex items-center justify-between" style={{ background: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
          <div className="flex items-center gap-2">
            <FileSignature className="w-4 h-4 text-[#3d7ab5]" />
            <h3 className="text-sm font-bold text-[#0d2137]">العقود ({contracts.length})</h3>
          </div>
          <Link href={`/finance/contracts/new?patientId=${patientId}`} className="text-xs font-semibold text-[#3d7ab5] flex items-center gap-1 hover:underline">
            عقد جديد
            <Plus className="w-3 h-3" />
          </Link>
        </div>
        <div className="p-4">
          {contracts.length === 0 ? (
            <div className="text-center py-6">
              <FileSignature className="w-8 h-8 text-[#dce8f5] mx-auto mb-2" />
              <p className="text-sm text-[#94a3b8]">لا توجد عقود مسجّلة</p>
            </div>
          ) : (
            <div className="space-y-2">
              {contracts.slice(0, 5).map((c) => {
                const statusDef = CONTRACT_STATUS[c.status] ?? CONTRACT_STATUS.active;
                const paidPercent = c.totalAmount > 0 ? Math.min(100, Math.round((c.paidAmount / c.totalAmount) * 100)) : 0;
                return (
                  <Link key={c.id} href={`/finance/contracts/${c.id}`}
                    className="flex items-center justify-between p-3 bg-[#f7fafd] rounded-lg hover:bg-[#eef3f9] border border-transparent transition"
                  >
                    <div className="flex items-center gap-2.5">
                      <FileSignature className="w-4 h-4 text-[#3d7ab5] flex-shrink-0" />
                      <div>
                        <div className="flex items-center gap-2">
                          <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", statusDef.bg, statusDef.color)}>
                            {statusDef.label}
                          </span>
                          {c.specialty && <span className="text-xs text-[#94a3b8]">{c.specialty}</span>}
                        </div>
                        <div className="flex items-center gap-2 mt-1">
                          <div className="w-20 h-1.5 bg-[#dce8f5] rounded-full overflow-hidden">
                            <div className="h-full bg-[#3d7ab5] rounded-full" style={{ width: `${paidPercent}%` }} />
                          </div>
                          <span className="text-[10px] text-[#94a3b8]">{paidPercent}%</span>
                        </div>
                      </div>
                    </div>
                    <div className="text-left flex-shrink-0">
                      <p className="text-xs text-[#0d2137] font-semibold">{c.totalAmount.toLocaleString()} ر.ي</p>
                      <p className="text-[10px] text-[#94a3b8]">مدفوع: {c.paidAmount.toLocaleString()}</p>
                    </div>
                  </Link>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* ════════════════════ Recent Payments ════════════════════ */}
      <div className="rounded-xl border border-[#e8f0f9] bg-white overflow-hidden">
        <div className="px-4 py-3 flex items-center justify-between" style={{ background: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
          <div className="flex items-center gap-2">
            <CreditCard className="w-4 h-4 text-green-600" />
            <h3 className="text-sm font-bold text-[#0d2137]">المدفوعات الأخيرة ({recentPayments.length})</h3>
          </div>
        </div>
        <div className="p-4">
          {recentPayments.length === 0 ? (
            <div className="text-center py-6">
              <CreditCard className="w-8 h-8 text-[#dce8f5] mx-auto mb-2" />
              <p className="text-sm text-[#94a3b8]">لا توجد مدفوعات مسجّلة</p>
            </div>
          ) : (
            <div className="space-y-2">
              {recentPayments.slice(0, 8).map((p) => (
                <div key={p.id} className="flex items-center justify-between p-2.5 bg-[#f7fafd] rounded-lg border border-[#e8f0f9]">
                  <div className="flex items-center gap-2.5">
                    <div className="w-8 h-8 rounded-lg bg-green-50 flex items-center justify-center flex-shrink-0">
                      <CreditCard className="w-4 h-4 text-green-600" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-[#0d2137]">{p.amount.toLocaleString()} ر.ي</span>
                        {p.paymentMethod && (
                          <span className="text-[10px] bg-[#f1f5f9] text-[#64748b] px-1.5 py-0.5 rounded">
                            {PAYMENT_METHOD_LABELS[p.paymentMethod] ?? p.paymentMethod}
                          </span>
                        )}
                      </div>
                      <p className="text-[10px] text-[#94a3b8]">{formatArabicDate(p.paymentDate)}</p>
                    </div>
                  </div>
                  {p.receiptNumber && (
                    <Link href={`/finance/payments/${p.id}`} className="text-[10px] text-[#3d7ab5] hover:underline">إيصال</Link>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
