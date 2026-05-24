"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { AlertCircle, Phone, FileText } from "lucide-react";
import api from "@/lib/api";
import { formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import type { OverdueContract } from "@/types/finance";
import { SPECIALTY_OPTIONS } from "@/types/finance";

export default function OverduePage() {
  const [contracts, setContracts] = useState<OverdueContract[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<OverdueContract[]>("/api/finance/overdue")
      .then((r) => setContracts(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const totalOverdue = contracts.reduce((s, c) => s + c.overdueAmount, 0);

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
            <AlertCircle className="w-6 h-6 text-red-500" />
            الأقساط المتأخرة
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">العقود النشطة التي تأخر سدادها</p>
        </div>
        <Link href="/finance" className="text-sm text-clinic-blue hover:underline">
          العودة للمالية
        </Link>
      </div>

      {/* Summary banner */}
      {!loading && contracts.length > 0 && (
        <div className="bg-red-50 border border-red-200 rounded-xl p-4 flex items-center justify-between">
          <div>
            <p className="text-sm font-semibold text-red-700">
              {contracts.length} عقد متأخر
            </p>
            <p className="text-xs text-red-500 mt-0.5">إجمالي المتأخر:</p>
          </div>
          <p className="text-2xl font-extrabold text-red-700 font-mono">
            {formatYemeniRiyal(totalOverdue)}
          </p>
        </div>
      )}

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : contracts.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm font-medium">لا توجد أقساط متأخرة</p>
          <p className="text-xs mt-1">جميع العقود النشطة في حالة جيدة</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["المريض", "التخصص", "تاريخ البداية", "المدفوع", "المتأخر", "المتبقي", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {contracts.map((c) => (
                  <tr key={c.contractId} className="hover:bg-red-50 transition">
                    <td className="px-4 py-3">
                      <Link href={`/patients/${c.patientId}`} className="font-semibold text-gray-900 hover:text-clinic-blue transition block">
                        {c.patientName}
                      </Link>
                      <div className="flex items-center gap-2 mt-0.5">
                        <span className="text-xs font-mono text-gray-400">{c.patientNumber}</span>
                        {c.phone && (
                          <span className="flex items-center gap-0.5 text-xs text-gray-500 font-mono" dir="ltr">
                            <Phone className="w-2.5 h-2.5" />
                            {c.phone}
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600 text-xs">
                      {SPECIALTY_OPTIONS[c.specialty as keyof typeof SPECIALTY_OPTIONS] ?? c.specialty ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600 text-xs">
                      {c.startDate ? formatArabicDate(c.startDate) : "—"}
                      <div className="text-gray-400">{c.monthsElapsed} شهر</div>
                    </td>
                    <td className="px-4 py-3 font-mono text-green-700 font-semibold">
                      {formatYemeniRiyal(c.paidAmount)}
                    </td>
                    <td className="px-4 py-3">
                      <span className="font-mono font-bold text-red-600">
                        {formatYemeniRiyal(c.overdueAmount)}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-mono text-gray-700">
                      {formatYemeniRiyal(c.remainingAmount)}
                    </td>
                    <td className="px-4 py-3">
                      <Link
                        href={`/finance/contracts/${c.contractId}`}
                        className="flex items-center gap-1 text-xs text-clinic-blue hover:underline whitespace-nowrap"
                      >
                        <FileText className="w-3.5 h-3.5" />
                        العقد
                      </Link>
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
