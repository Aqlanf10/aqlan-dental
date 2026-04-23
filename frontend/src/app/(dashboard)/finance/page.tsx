"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { TrendingUp, Wallet, AlertCircle, FileText, Plus } from "lucide-react";
import type { FinanceSummary } from "@/types/finance";
import api from "@/lib/api";
import { formatYemeniRiyal, formatArabicDate } from "@/lib/utils";

export default function FinancePage() {
  const [summary, setSummary] = useState<FinanceSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<FinanceSummary>("/api/finance/summary")
      .then((r) => setSummary(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const stats = summary
    ? [
        { label: "محصّل اليوم",       value: formatYemeniRiyal(summary.todayCollected),   icon: TrendingUp, color: "bg-teal-50 text-teal-600 border-teal-200" },
        { label: "محصّل هذا الشهر",   value: formatYemeniRiyal(summary.monthCollected),   icon: Wallet,     color: "bg-blue-50 text-blue-600 border-blue-200" },
        { label: "المبالغ المستحقة",  value: formatYemeniRiyal(summary.totalOutstanding), icon: AlertCircle,color: "bg-red-50 text-red-600 border-red-200" },
        { label: "العقود النشطة",      value: summary.activeContracts.toString(),          icon: FileText,   color: "bg-purple-50 text-purple-600 border-purple-200" },
      ]
    : [];

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">المالية</h1>
          <p className="text-sm text-gray-500 mt-0.5">الملخص المالي والعقود والدفعات</p>
        </div>
        <div className="flex items-center gap-2">
          <Link href="/finance/contracts/new"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-teal-50 transition"
          >
            <FileText className="w-4 h-4" />
            عقد جديد
          </Link>
          <Link href="/finance/payments"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            <Plus className="w-4 h-4" />
            دفعة جديدة
          </Link>
        </div>
      </div>

      {/* Stats */}
      {loading ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-24 bg-gray-100 rounded-xl" />)}
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {stats.map(({ label, value, icon: Icon, color }) => (
            <div key={label} className={`rounded-xl border p-4 ${color}`}>
              <Icon className="w-5 h-5 mb-2 opacity-80" />
              <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
              <p className="text-xs font-medium mt-1 opacity-70">{label}</p>
            </div>
          ))}
        </div>
      )}

      {/* Recent Payments */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر الدفعات</h2>
          <Link href="/finance/contracts" className="text-sm text-clinic-teal hover:underline">
            عرض العقود
          </Link>
        </div>

        {loading ? (
          <div className="p-5 space-y-3 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}
          </div>
        ) : !summary?.recentPayments.length ? (
          <div className="text-center py-12 text-gray-400 text-sm">لا توجد دفعات مسجلة</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["المريض", "المبلغ", "التاريخ", "الطريقة", "رقم السند"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {summary.recentPayments.map((p) => (
                  <tr key={p.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-medium text-gray-900">{p.patientName}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-green-700">{formatYemeniRiyal(p.amount)}</td>
                    <td className="px-4 py-3 text-gray-600">{formatArabicDate(p.paymentDate)}</td>
                    <td className="px-4 py-3 text-gray-600">
                      {p.paymentMethod === "cash" ? "نقداً" : p.paymentMethod === "bank_transfer" ? "تحويل بنكي" : p.paymentMethod ?? "—"}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-400">{p.receiptNumber ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
