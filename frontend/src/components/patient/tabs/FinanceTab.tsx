"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Wallet, CreditCard, FileSignature, TrendingDown, ArrowLeft } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn } from "@/lib/utils";

interface ContractStatementDto {
  id: string;
  specialty?: string;
  totalAmount: number;
  discountAmount: number;
  paidAmount: number;
  remainingAmount: number;
  startDate?: string;
  status: string;
  installmentsCount: number;
  installmentAmount?: number;
}

interface PaymentDto {
  id: string;
  amount: number;
  paymentDate: string;
  paymentMethod?: string;
  serviceDescription?: string;
  receiptNumber?: string;
}

interface AccountStatementDto {
  patientId: string;
  patientName: string;
  patientNumber: string;
  totalContracted: number;
  totalDiscounts: number;
  totalPaid: number;
  totalRemaining: number;
  activeContracts: number;
  completedContracts: number;
  contracts: ContractStatementDto[];
  recentPayments: PaymentDto[];
}

interface FinanceTabProps {
  patientId: string;
}

const statusLabel: Record<string, string> = {
  active: "نشط",
  completed: "مكتمل",
  cancelled: "ملغى",
};

const statusClass: Record<string, string> = {
  active: "bg-green-50 text-green-700",
  completed: "bg-blue-50 text-blue-700",
  cancelled: "bg-red-50 text-red-600",
};

const methodLabel: Record<string, string> = {
  cash: "نقدي",
  bank_transfer: "تحويل",
  card: "بطاقة",
};

export function FinanceTab({ patientId }: FinanceTabProps) {
  const [statement, setStatement] = useState<AccountStatementDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .get<AccountStatementDto>(`/api/patients/${patientId}/account-statement`)
      .then((r) => {
        setStatement(r.data);
        setLoading(false);
      })
      .catch(() => {
        setError("تعذّر تحميل البيانات المالية");
        setLoading(false);
      });
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        <div className="h-20 bg-[#f1f5f9] rounded-lg" />
        <div className="h-14 bg-[#f1f5f9] rounded-lg" />
        <div className="h-14 bg-[#f1f5f9] rounded-lg" />
      </div>
    );
  }

  if (error || !statement) {
    return error ? (
      <p className="text-sm text-red-500 text-center py-4">{error}</p>
    ) : (
      <EmptyState icon={Wallet} title="لا توجد بيانات مالية" description="لم يتم تسجيل أي معاملات مالية لهذا المريض" />
    );
  }

  const { totalContracted, totalDiscounts, totalPaid, totalRemaining, activeContracts, completedContracts, contracts, recentPayments } = statement;

  return (
    <div className="space-y-6" dir="rtl">
      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="rounded-xl px-4 py-3 bg-[#eef4fb]">
          <p className="text-xs text-[#64748b]">إجمالي العقود</p>
          <p className="text-lg font-bold text-[#3d7ab5]">{totalContracted.toLocaleString()}</p>
        </div>
        <div className="rounded-xl px-4 py-3 bg-green-50">
          <p className="text-xs text-[#64748b]">إجمالي المدفوع</p>
          <p className="text-lg font-bold text-green-700">{totalPaid.toLocaleString()}</p>
        </div>
        <div className="rounded-xl px-4 py-3 bg-orange-50">
          <p className="text-xs text-[#64748b]">إجمالي المتبقي</p>
          <p className="text-lg font-bold text-orange-600">{totalRemaining.toLocaleString()}</p>
        </div>
        <div className="rounded-xl px-4 py-3 bg-purple-50">
          <p className="text-xs text-[#64748b]">الخصومات</p>
          <p className="text-lg font-bold text-purple-600">{totalDiscounts.toLocaleString()}</p>
        </div>
      </div>

      {/* Contract count badges */}
      <div className="flex gap-3 text-sm">
        <span className="flex items-center gap-1.5 px-3 py-1 bg-green-50 text-green-700 rounded-full font-medium">
          <span className="w-2 h-2 rounded-full bg-green-500 inline-block" />
          {activeContracts} عقد نشط
        </span>
        <span className="flex items-center gap-1.5 px-3 py-1 bg-blue-50 text-blue-700 rounded-full font-medium">
          <span className="w-2 h-2 rounded-full bg-blue-500 inline-block" />
          {completedContracts} عقد مكتمل
        </span>
      </div>

      {/* Contracts */}
      <div>
        <h3 className="text-sm font-semibold text-[#0d2137] mb-3 flex items-center gap-2">
          <FileSignature className="w-4 h-4 text-[#3d7ab5]" />
          العقود
        </h3>
        {contracts.length === 0 ? (
          <p className="text-sm text-[#94a3b8]">لا توجد عقود مسجّلة</p>
        ) : (
          <div className="space-y-2">
            {contracts.map((c) => {
              const effectiveTotal = c.totalAmount - c.discountAmount;
              const progress = effectiveTotal > 0 ? Math.min(100, (c.paidAmount / effectiveTotal) * 100) : 0;
              return (
                <Link
                  key={c.id}
                  href={`/finance/contracts/${c.id}`}
                  className="flex flex-col gap-2 p-3 bg-[#f7fafd] rounded-xl border border-[#e8f0f9] hover:border-[#3d7ab5]/30 transition-colors"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Wallet className="w-4 h-4 text-[#94a3b8]" />
                      <span className="text-sm font-medium text-[#0d2137]">{c.specialty ?? "عقد علاج"}</span>
                      {c.startDate && <span className="text-xs text-[#94a3b8]">{c.startDate}</span>}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", statusClass[c.status] ?? "bg-[#f1f5f9] text-[#64748b]")}>
                        {statusLabel[c.status] ?? c.status}
                      </span>
                      <ArrowLeft className="w-3.5 h-3.5 text-[#94a3b8]" />
                    </div>
                  </div>
                  <div className="flex items-center justify-between text-xs text-[#64748b]">
                    <span>مدفوع: {c.paidAmount.toLocaleString()}</span>
                    <span>متبقي: {c.remainingAmount.toLocaleString()}</span>
                    <span>الإجمالي: {effectiveTotal.toLocaleString()}</span>
                  </div>
                  {/* progress bar */}
                  <div className="h-1.5 bg-[#e2e8f0] rounded-full overflow-hidden">
                    <div
                      className={cn("h-full rounded-full transition-all", c.status === "cancelled" ? "bg-red-400" : "bg-[#3d7ab5]")}
                      style={{ width: `${progress}%` }}
                    />
                  </div>
                </Link>
              );
            })}
          </div>
        )}
      </div>

      {/* Recent Payments */}
      <div>
        <h3 className="text-sm font-semibold text-[#0d2137] mb-3 flex items-center gap-2">
          <CreditCard className="w-4 h-4 text-[#3d7ab5]" />
          المدفوعات الأخيرة
        </h3>
        {recentPayments.length === 0 ? (
          <p className="text-sm text-[#94a3b8]">لا توجد مدفوعات مسجّلة</p>
        ) : (
          <div className="space-y-2">
            {recentPayments.map((p) => (
              <div key={p.id} className="flex items-center justify-between p-3 bg-[#f7fafd] rounded-xl border border-[#e8f0f9]">
                <div className="flex items-center gap-2">
                  <TrendingDown className="w-4 h-4 text-green-500" />
                  <div>
                    <p className="text-sm text-[#0d2137]">{p.serviceDescription ?? "دفعة"}</p>
                    <p className="text-xs text-[#94a3b8]">{p.paymentDate} {p.paymentMethod ? `· ${methodLabel[p.paymentMethod] ?? p.paymentMethod}` : ""}</p>
                  </div>
                </div>
                <div className="text-left">
                  <p className="text-sm font-semibold text-[#3d7ab5]">{p.amount.toLocaleString()}</p>
                  {p.receiptNumber && <p className="text-xs text-[#94a3b8]">{p.receiptNumber}</p>}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
