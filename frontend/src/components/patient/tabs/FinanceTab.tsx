"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Wallet, CreditCard, FileSignature, FileText, TrendingDown, ArrowLeft } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn } from "@/lib/utils";
import type { Invoice, AccountStatement } from "@/types/finance";
import { PAYMENT_METHODS as paymentMethodLabels, CONTRACT_STATUS as contractStatusConfig, INVOICE_STATUS as invoiceStatusConfig } from "@/types/finance";
import { financeV3ContractsUrl, financeV3InvoicesUrl } from "@/lib/financeRoutes";

interface FinanceTabProps {
  patientId: string;
  refreshKey?: number; // Sprint Patient-Finance-Ledger: triggers re-fetch when payment changes
}

export function FinanceTab({ patientId, refreshKey }: FinanceTabProps) {
  const [statement, setStatement] = useState<AccountStatement | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [invoicesError, setInvoicesError] = useState(false);

  useEffect(() => {
    api
      .get<AccountStatement>(`/api/patients/${patientId}/account-statement`)
      .then((r) => {
        setStatement(r.data);
        setLoading(false);
      })
      .catch(() => {
        setError("تعذّر تحميل البيانات المالية");
        setLoading(false);
      });

    setInvoicesError(false);
    api
      .get<Invoice[]>(`/api/patients/${patientId}/invoices`)
      .then((r) => setInvoices(r.data))
      .catch(() => { setInvoicesError(true); });
  }, [patientId, refreshKey]);

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
    <div className="space-y-6">
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
              const statusCfg = contractStatusConfig[c.status as keyof typeof contractStatusConfig];
              return (
                <Link
                  key={c.id}
                  href={financeV3ContractsUrl(patientId)}
                  className="flex flex-col gap-2 p-3 bg-[#f7fafd] rounded-xl border border-[#e8f0f9] hover:border-[#3d7ab5]/30 transition-colors"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Wallet className="w-4 h-4 text-[#94a3b8]" />
                      <span className="text-sm font-medium text-[#0d2137]">{c.specialty ?? "عقد علاج"}</span>
                      {c.startDate && <span className="text-xs text-[#94a3b8]">{c.startDate}</span>}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", statusCfg?.color ?? "bg-[#f1f5f9] text-[#64748b]")}>
                        {statusCfg?.label ?? c.status}
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
                    <p className="text-xs text-[#94a3b8]">{p.paymentDate} {p.paymentMethod ? `· ${paymentMethodLabels[p.paymentMethod as keyof typeof paymentMethodLabels] ?? p.paymentMethod}` : ""}</p>
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

      {/* Invoices */}
      <div>
        <h3 className="text-sm font-semibold text-[#0d2137] mb-3 flex items-center gap-2">
          <FileText className="w-4 h-4 text-[#3d7ab5]" />
          الفواتير
        </h3>
        {invoicesError ? (
          <div className="p-3 text-center">
            <p className="text-sm text-red-600 mb-2">فشل في تحميل الفواتير</p>
          </div>
        ) : invoices.length === 0 ? (
          <p className="text-sm text-[#94a3b8]">لا توجد فواتير مسجّلة</p>
        ) : (
          <div className="space-y-2">
            {invoices.map((inv) => {
              const invStatus = invoiceStatusConfig[inv.status as keyof typeof invoiceStatusConfig];
              return (
                <Link
                  key={inv.id}
                  href={financeV3InvoicesUrl(patientId)}
                  className="flex items-center justify-between p-3 bg-[#f7fafd] rounded-xl border border-[#e8f0f9] hover:border-[#3d7ab5]/30 transition-colors"
                >
                  <div className="flex items-center gap-2">
                    <FileText className="w-4 h-4 text-[#3d7ab5]" />
                    <div>
                      <p className="text-sm font-medium text-[#0d2137]">{inv.invoiceNumber}</p>
                      <p className="text-xs text-[#94a3b8]">{new Date(inv.createdAt).toLocaleDateString("ar-YE")}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        invStatus?.color ?? "bg-gray-100 text-gray-600"
                      )}
                    >
                      {invStatus?.label ?? inv.statusArabic ?? inv.status}
                    </span>
                    <div className="text-left">
                      <p className="text-sm font-semibold text-[#3d7ab5]">
                        {inv.totalAmount.toLocaleString()} ر.ي
                      </p>
                      {inv.paidAmount != null && inv.balance != null && (
                        <p className="text-[10px] text-[#94a3b8]">
                          مدفوع {inv.paidAmount.toLocaleString()} · متبقي {inv.balance.toLocaleString()}
                        </p>
                      )}
                    </div>
                    <ArrowLeft className="w-3.5 h-3.5 text-[#94a3b8]" />
                  </div>
                </Link>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
