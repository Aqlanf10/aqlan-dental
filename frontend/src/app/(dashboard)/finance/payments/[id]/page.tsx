"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight, Printer } from "lucide-react";
import type { Payment } from "@/types/finance";
import api from "@/lib/api";
import { ReceiptView } from "@/components/finance/ReceiptView";

export default function PaymentReceiptPage() {
  const { id } = useParams<{ id: string }>();
  const [payment, setPayment] = useState<Payment | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<Payment>(`/api/payments/${id}`)
      .then((r) => setPayment(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="max-w-md mx-auto space-y-4 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-20 bg-gray-100 rounded-xl" />
        ))}
      </div>
    );
  }

  if (!payment) {
    return <div className="text-center py-20 text-gray-400 text-sm">الدفعة غير موجودة</div>;
  }

  return (
    <div className="space-y-5 max-w-xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/finance" className="hover:text-clinic-teal transition">المالية</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">سند قبض #{payment.receiptNumber}</span>
      </div>

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/finance" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-gray-900">سند قبض</h1>
        </div>
        <button
          onClick={() => window.print()}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Printer className="w-4 h-4" />
          طباعة
        </button>
      </div>

      {/* Receipt */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-6 print:border-0 print:shadow-none print:p-0">
        <ReceiptView payment={payment} />
      </div>
    </div>
  );
}
