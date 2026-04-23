"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Plus, FileText } from "lucide-react";
import type { Contract } from "@/types/finance";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";

const STATUS_LABELS: Record<string, string> = {
  active: "نشط", completed: "مكتمل", cancelled: "ملغى",
};
const STATUS_COLORS: Record<string, string> = {
  active: "bg-green-50 text-green-700",
  completed: "bg-blue-50 text-blue-700",
  cancelled: "bg-gray-100 text-gray-500",
};

export default function ContractsPage() {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<Contract[]>("/api/contracts")
      .then((r) => setContracts(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">العقود</h1>
          <p className="text-sm text-gray-500 mt-0.5">عقود المرضى وجداول الأقساط</p>
        </div>
        <Link href="/finance/contracts/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          عقد جديد
        </Link>
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-xl" />)}
        </div>
      ) : contracts.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد عقود</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["المريض", "التخصص", "إجمالي العقد", "المدفوع", "المتبقي", "الأقساط", "بدأ", "الحالة"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {contracts.map((c) => (
                  <tr key={c.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3">
                      <Link href={`/finance/contracts/${c.id}`} className="font-medium text-gray-900 hover:text-clinic-teal transition">
                        {c.patientName}
                      </Link>
                      <div className="text-xs text-gray-400 font-mono">{c.patientNumber}</div>
                    </td>
                    <td className="px-4 py-3 text-gray-700">{c.specialty ?? "—"}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-gray-900">{formatYemeniRiyal(c.totalAmount)}</td>
                    <td className="px-4 py-3 font-mono text-green-700">{formatYemeniRiyal(c.paidAmount)}</td>
                    <td className="px-4 py-3 font-mono text-red-600">{formatYemeniRiyal(c.remainingAmount)}</td>
                    <td className="px-4 py-3 text-gray-600">{c.installmentsCount}</td>
                    <td className="px-4 py-3 text-gray-600 text-xs">
                      {c.startDate ? formatArabicDate(c.startDate) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium",
                        STATUS_COLORS[c.status] ?? "bg-gray-100 text-gray-600"
                      )}>
                        {STATUS_LABELS[c.status] ?? c.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
