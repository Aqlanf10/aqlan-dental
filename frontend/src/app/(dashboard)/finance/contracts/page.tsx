"use client";
import { useEffect, useState, useMemo } from "react";
import Link from "next/link";
import { Plus, FileText, Search } from "lucide-react";
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
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  useEffect(() => {
    const params = new URLSearchParams({ pageSize: "100" });
    if (statusFilter) params.set("status", statusFilter);
    api.get<{ items: Contract[]; totalCount: number }>("/api/contracts?" + params)
      .then((r) => setContracts(r.data.items))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [statusFilter]);

  const filtered = useMemo(() => {
    if (!search.trim()) return contracts;
    const term = search.trim().toLowerCase();
    return contracts.filter(
      (c) =>
        c.patientName.toLowerCase().includes(term) ||
        c.patientNumber?.toLowerCase().includes(term)
    );
  }, [contracts, search]);

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

      {/* Search + Status filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="relative min-w-56 flex-1">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث باسم المريض أو رقمه..."
            className="w-full h-9 pe-9 ps-3 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          />
        </div>
        {["", "active", "completed", "cancelled"].map((s) => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium whitespace-nowrap",
              statusFilter === s
                ? "bg-clinic-teal text-white border-clinic-teal"
                : "border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            {s === "" ? "الكل" : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-xl" />)}
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">{search ? "لا توجد نتائج مطابقة" : "لا توجد عقود"}</p>
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
                {filtered.map((c) => (
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
          {filtered.length > 0 && (
            <div className="px-4 py-2 border-t border-gray-100 bg-gray-50 text-xs text-gray-500">
              {filtered.length} عقد
            </div>
          )}
        </div>
      )}
    </div>
  );
}
