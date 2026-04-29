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
  active: "bg-[#22c55e18] text-[#22c55e]",
  completed: "bg-[#3d7ab518] text-accent-blue",
  cancelled: "bg-[#94a3b818] text-[#94a3b8]",
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
          <h1 className="text-2xl font-extrabold text-[#0d2137]">العقود</h1>
          <p className="text-sm text-[#64748b] mt-0.5">عقود المرضى وجداول الأقساط</p>
        </div>
        <Link href="/finance/contracts/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
        >
          <Plus className="w-4 h-4" />
          عقد جديد
        </Link>
      </div>

      {/* Search + Status filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="relative min-w-56 flex-1">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-[#94a3b8]" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث باسم المريض أو رقمه..."
            className="w-full h-9 pe-9 ps-3 text-sm rounded-lg border-[1.5px] border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-accent-blue"
          />
        </div>
        {["", "active", "completed", "cancelled"].map((s) => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium whitespace-nowrap",
              statusFilter === s
                ? "bg-accent-blue text-white border-accent-blue"
                : "border-[#e8f0f9] text-[#64748b] hover:bg-[#f7fafd]"
            )}
          >
            {s === "" ? "الكل" : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-16 bg-[#eef3f9] rounded-xl" />)}
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-20 text-[#94a3b8]">
          <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">{search ? "لا توجد نتائج مطابقة" : "لا توجد عقود"}</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
                <tr>
                  {["المريض", "التخصص", "إجمالي العقد", "المدفوع", "المتبقي", "الأقساط", "بدأ", "الحالة"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-bold text-[#64748b] whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {filtered.map((c) => (
                  <tr key={c.id} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-3">
                      <Link href={`/finance/contracts/${c.id}`} className="font-medium text-[#0d2137] hover:text-accent-blue transition">
                        {c.patientName}
                      </Link>
                      <div className="text-xs text-[#94a3b8] font-mono">{c.patientNumber}</div>
                    </td>
                    <td className="px-4 py-3 text-[#64748b]">{c.specialty ?? "—"}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-[#0d2137]">{formatYemeniRiyal(c.totalAmount)}</td>
                    <td className="px-4 py-3 font-mono text-[#22c55e]">{formatYemeniRiyal(c.paidAmount)}</td>
                    <td className="px-4 py-3 font-mono text-[#ef4444]">{formatYemeniRiyal(c.remainingAmount)}</td>
                    <td className="px-4 py-3 text-[#64748b]">{c.installmentsCount}</td>
                    <td className="px-4 py-3 text-[#64748b] text-xs">
                      {c.startDate ? formatArabicDate(c.startDate) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn("text-xs px-[10px] py-[2px] rounded-full font-medium",
                        STATUS_COLORS[c.status] ?? "bg-[#94a3b818] text-[#94a3b8]"
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
            <div className="px-4 py-2 border-t border-[#f1f5f9] bg-[#f7fafd] text-xs text-[#64748b]">
              {filtered.length} عقد
            </div>
          )}
        </div>
      )}
    </div>
  );
}
