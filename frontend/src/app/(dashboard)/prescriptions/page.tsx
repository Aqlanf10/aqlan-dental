"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Plus, FileText, Pill } from "lucide-react";
import type { PrescriptionListItem } from "@/types/prescription";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";

export default function PrescriptionsPage() {
  const [prescriptions, setPrescriptions] = useState<PrescriptionListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const pageSize = 20;

  useEffect(() => {
    setLoading(true);
    api.get<{ data: PrescriptionListItem[]; total: number }>(`/api/prescriptions?page=${page}&pageSize=${pageSize}`)
      .then((r) => { setPrescriptions(r.data.data); setTotal(r.data.total); })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [page]);

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">الوصفات الطبية</h1>
          <p className="text-sm text-gray-500 mt-0.5">إدارة وطباعة وصفات المرضى</p>
        </div>
        <Link
          href="/prescriptions/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          وصفة جديدة
        </Link>
      </div>

      {/* List */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : prescriptions.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <Pill className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد وصفات بعد</p>
          <Link href="/prescriptions/new" className="mt-2 inline-block text-xs text-clinic-teal hover:underline">
            + إنشاء أول وصفة
          </Link>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="text-start px-4 py-3 font-semibold text-gray-600">المريض</th>
                <th className="text-start px-4 py-3 font-semibold text-gray-600">التشخيص</th>
                <th className="text-start px-4 py-3 font-semibold text-gray-600">الطبيب</th>
                <th className="text-start px-4 py-3 font-semibold text-gray-600">الأدوية</th>
                <th className="text-start px-4 py-3 font-semibold text-gray-600">التاريخ</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {prescriptions.map((rx) => (
                <tr key={rx.id} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <Link href={`/patients/${rx.patientId}`} className="font-semibold text-gray-900 hover:text-clinic-teal transition">
                      {rx.patientName}
                    </Link>
                    <div className="text-xs text-gray-400 font-mono">{rx.patientNumber}</div>
                  </td>
                  <td className="px-4 py-3 text-gray-700">{rx.diagnosis ?? "—"}</td>
                  <td className="px-4 py-3 text-gray-600">{rx.doctorName ?? "—"}</td>
                  <td className="px-4 py-3">
                    <span className="inline-flex items-center gap-1 text-xs bg-teal-50 text-teal-700 px-2 py-0.5 rounded-full font-medium">
                      <Pill className="w-3 h-3" />
                      {rx.drugCount} {rx.drugCount === 1 ? "دواء" : "أدوية"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500 text-xs">{formatArabicDate(rx.createdAt)}</td>
                  <td className="px-4 py-3">
                    <Link
                      href={`/prescriptions/${rx.id}`}
                      className="flex items-center gap-1 text-xs text-clinic-teal hover:underline"
                    >
                      <FileText className="w-3.5 h-3.5" />
                      عرض / طباعة
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50 text-sm">
              <span className="text-gray-500">
                {total} وصفة · صفحة {page} من {totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-3 py-1 rounded border border-gray-200 disabled:opacity-40 hover:bg-gray-100 transition text-gray-600"
                >
                  السابق
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  className="px-3 py-1 rounded border border-gray-200 disabled:opacity-40 hover:bg-gray-100 transition text-gray-600"
                >
                  التالي
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
