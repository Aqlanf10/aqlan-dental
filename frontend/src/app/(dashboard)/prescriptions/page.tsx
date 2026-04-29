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
          <h1 className="text-2xl font-extrabold text-[#0d2137]">الوصفات الطبية</h1>
          <p className="text-sm text-[#64748b] mt-0.5">إدارة وطباعة وصفات المرضى</p>
        </div>
        <Link
          href="/prescriptions/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
        >
          <Plus className="w-4 h-4" />
          وصفة جديدة
        </Link>
      </div>

      {/* List */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-16 bg-[#eef3f9] rounded-xl" />
          ))}
        </div>
      ) : prescriptions.length === 0 ? (
        <div className="text-center py-20 text-[#94a3b8]">
          <Pill className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد وصفات بعد</p>
          <Link href="/prescriptions/new" className="mt-2 inline-block text-xs text-accent-blue hover:underline">
            + إنشاء أول وصفة
          </Link>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
              <tr>
                <th className="text-start px-4 py-3 font-bold text-[#64748b]">المريض</th>
                <th className="text-start px-4 py-3 font-bold text-[#64748b]">التشخيص</th>
                <th className="text-start px-4 py-3 font-bold text-[#64748b]">الطبيب</th>
                <th className="text-start px-4 py-3 font-bold text-[#64748b]">الأدوية</th>
                <th className="text-start px-4 py-3 font-bold text-[#64748b]">التاريخ</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f1f5f9]">
              {prescriptions.map((rx) => (
                <tr key={rx.id} className="hover:bg-[#f7fafd] transition">
                  <td className="px-4 py-3">
                    <Link href={`/patients/${rx.patientId}`} className="font-semibold text-[#0d2137] hover:text-accent-blue transition">
                      {rx.patientName}
                    </Link>
                    <div className="text-xs text-[#94a3b8] font-mono">{rx.patientNumber}</div>
                  </td>
                  <td className="px-4 py-3 text-[#64748b]">{rx.diagnosis ?? "—"}</td>
                  <td className="px-4 py-3 text-[#64748b]">{rx.doctorName ?? "—"}</td>
                  <td className="px-4 py-3">
                    <span className="inline-flex items-center gap-1 text-xs bg-light-blue text-accent-blue px-[10px] py-[2px] rounded-full font-medium">
                      <Pill className="w-3 h-3" />
                      {rx.drugCount} {rx.drugCount === 1 ? "دواء" : "أدوية"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-[#64748b] text-xs">{formatArabicDate(rx.createdAt)}</td>
                  <td className="px-4 py-3">
                    <Link
                      href={`/prescriptions/${rx.id}`}
                      className="flex items-center gap-1 text-xs text-accent-blue hover:underline"
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
            <div className="flex items-center justify-between px-4 py-3 border-t border-[#f1f5f9] bg-[#f7fafd] text-sm">
              <span className="text-[#64748b]">
                {total} وصفة · صفحة {page} من {totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-3 py-1 rounded border border-[#e8f0f9] disabled:opacity-40 hover:bg-[#eef3f9] transition text-[#64748b]"
                >
                  السابق
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  className="px-3 py-1 rounded border border-[#e8f0f9] disabled:opacity-40 hover:bg-[#eef3f9] transition text-[#64748b]"
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
