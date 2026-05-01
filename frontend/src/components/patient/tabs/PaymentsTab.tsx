"use client";

import { useEffect, useState } from "react";
import { CreditCard } from "lucide-react";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";

interface PaymentDto {
  id: string;
  amount: number;
  date: string;
  method?: string;
  notes?: string;
  contractNumber?: string;
}

const METHOD_LABELS: Record<string, string> = {
  cash: "نقدي",
  card: "بطاقة",
  transfer: "تحويل بنكي",
  Cash: "نقدي",
  Card: "بطاقة",
  Transfer: "تحويل بنكي",
};

interface PaymentsTabProps {
  patientId: string;
}

export function PaymentsTab({ patientId }: PaymentsTabProps) {
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<PaymentDto[]>(`/api/payments?patientId=${patientId}`)
      .then((r) => setPayments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-14 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  if (payments.length === 0) {
    return (
      <div className="text-center py-12 text-gray-400" dir="rtl">
        <CreditCard className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا توجد مدفوعات مسجّلة</p>
      </div>
    );
  }

  const total = payments.reduce((sum, p) => sum + (p.amount ?? 0), 0);

  return (
    <div className="space-y-4" dir="rtl">
      <div className="rounded-lg px-4 py-3 bg-teal-50">
        <p className="text-xs text-gray-500">إجمالي المدفوعات</p>
        <p className="text-lg font-bold text-teal-600">{total.toLocaleString()}</p>
      </div>

      <div className="space-y-2">
        {payments.map((p) => (
          <div key={p.id} className="flex items-center justify-between p-3 bg-white border border-gray-100 rounded-lg hover:border-gray-200 transition">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-green-50 flex items-center justify-center flex-shrink-0">
                <CreditCard className="w-5 h-5 text-green-500" />
              </div>
              <div>
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-sm font-medium text-gray-900">{p.amount.toLocaleString()}</span>
                  {p.method && (
                    <span className="text-xs bg-gray-100 text-gray-600 px-1.5 py-0.5 rounded">
                      {METHOD_LABELS[p.method] ?? p.method}
                    </span>
                  )}
                </div>
                <p className="text-xs text-gray-400">{formatArabicDate(p.date)}</p>
                {p.contractNumber && <p className="text-xs text-gray-500">عقد: {p.contractNumber}</p>}
                {p.notes && <p className="text-xs text-gray-500 line-clamp-1">{p.notes}</p>}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
