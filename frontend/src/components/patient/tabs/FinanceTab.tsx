"use client";

import { useEffect, useState } from "react";
import { Wallet, CreditCard, FileSignature } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface ContractDto {
  id: string;
  totalAmount: number;
  paidAmount: number;
  status: string;
}

interface PaymentDto {
  id: string;
  amount: number;
  date: string;
  method?: string;
}

interface FinanceTabProps {
  patientId: string;
  totalPaid: number;
  totalOutstanding: number;
}

export function FinanceTab({ patientId, totalPaid, totalOutstanding }: FinanceTabProps) {
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<ContractDto[]>(`/api/contracts?patientId=${patientId}`).then((r) => r.data).catch(() => []),
      api.get<PaymentDto[]>(`/api/payments?patientId=${patientId}`).then((r) => r.data).catch(() => []),
    ]).then(([c, p]) => {
      setContracts(c);
      setPayments(p);
      setLoading(false);
    });
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        <div className="h-24 bg-[#f1f5f9] rounded-lg" />
        <div className="h-14 bg-[#f1f5f9] rounded-lg" />
        <div className="h-14 bg-[#f1f5f9] rounded-lg" />
      </div>
    );
  }

  const activeContracts = contracts.filter((c) => c.status === "active" || c.status === "Active").length;
  const totalContractValue = contracts.reduce((sum, c) => sum + (c.totalAmount ?? 0), 0);

  return (
    <div className="space-y-6" dir="rtl">
      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="rounded-lg px-4 py-3 bg-clinic-blue-50">
          <p className="text-xs text-[#64748b]">إجمالي المدفوع</p>
          <p className="text-lg font-bold text-clinic-blue">{totalPaid.toLocaleString()}</p>
        </div>
        <div className="rounded-lg px-4 py-3 bg-orange-50">
          <p className="text-xs text-[#64748b]">إجمالي المتبقي</p>
          <p className="text-lg font-bold text-orange-600">{totalOutstanding.toLocaleString()}</p>
        </div>
        <div className="rounded-lg px-4 py-3 bg-[#3d7ab518]">
          <p className="text-xs text-[#64748b]">العقود النشطة</p>
          <p className="text-lg font-bold text-[#3d7ab5]">{activeContracts}</p>
        </div>
        <div className="rounded-lg px-4 py-3 bg-purple-50">
          <p className="text-xs text-[#64748b]">قيمة العقود</p>
          <p className="text-lg font-bold text-purple-600">{totalContractValue.toLocaleString()}</p>
        </div>
      </div>

      {/* Recent Payments */}
      <div>
        <h3 className="text-sm font-semibold text-[#0d2137] mb-3 flex items-center gap-2">
          <CreditCard className="w-4 h-4 text-clinic-blue" />
          المدفوعات الأخيرة
        </h3>
        {payments.length === 0 ? (
          <p className="text-sm text-[#94a3b8]">لا توجد مدفوعات مسجّلة</p>
        ) : (
          <div className="space-y-2">
            {payments.slice(0, 10).map((p) => (
              <div key={p.id} className="flex items-center justify-between p-3 bg-[#f7fafd] rounded-lg border border-[#e8f0f9]">
                <div className="flex items-center gap-2">
                  <CreditCard className="w-4 h-4 text-[#94a3b8]" />
                  <span className="text-sm text-[#0d2137]">{p.date}</span>
                  {p.method && <span className="text-xs text-[#94a3b8]">({p.method})</span>}
                </div>
                <span className="text-sm font-semibold text-clinic-blue">{p.amount.toLocaleString()}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Contracts Summary */}
      <div>
        <h3 className="text-sm font-semibold text-[#0d2137] mb-3 flex items-center gap-2">
          <FileSignature className="w-4 h-4 text-clinic-blue" />
          ملخص العقود
        </h3>
        {contracts.length === 0 ? (
          <p className="text-sm text-[#94a3b8]">لا توجد عقود مسجّلة</p>
        ) : (
          <div className="space-y-2">
            {contracts.slice(0, 10).map((c) => (
              <div key={c.id} className="flex items-center justify-between p-3 bg-[#f7fafd] rounded-lg border border-[#e8f0f9]">
                <div className="flex items-center gap-2">
                  <Wallet className="w-4 h-4 text-[#94a3b8]" />
                  <span className={cn(
                    "text-xs px-1.5 py-0.5 rounded-full font-medium",
                    c.status === "active" || c.status === "Active" ? "bg-green-50 text-green-700" : "bg-[#f1f5f9] text-[#64748b]"
                  )}>
                    {c.status === "active" || c.status === "Active" ? "نشط" : c.status}
                  </span>
                </div>
                <div className="text-left">
                  <span className="text-xs text-[#64748b]">{c.paidAmount.toLocaleString()} / {c.totalAmount.toLocaleString()}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
