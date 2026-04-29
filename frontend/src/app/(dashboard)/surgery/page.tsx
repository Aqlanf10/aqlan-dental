"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { Scissors, Plus, Search, UserPlus } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface SurgeryCase {
  id: string;
  caseNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string;
  doctorColor?: string;
  surgeryType: string;
  teethInvolved?: string;
  status: string;
  createdAt: string;
}

const STATUS_LABELS: Record<string, string> = {
  scheduled: "مجدولة", in_progress: "جارية", completed: "مكتملة", cancelled: "ملغاة",
};
const STATUS_COLORS: Record<string, string> = {
  scheduled:   "bg-[#3d7ab518] text-accent-blue",
  in_progress: "bg-[#f59e0b18] text-[#f59e0b]",
  completed:   "bg-[#22c55e18] text-[#22c55e]",
  cancelled:   "bg-[#94a3b818] text-[#94a3b8]",
};

export default function SurgeryPage() {
  const [cases, setCases] = useState<SurgeryCase[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("");
  const [search, setSearch] = useState("");
  const [patientSuggestions, setPatientSuggestions] = useState<PatientListItem[]>([]);

  const load = useCallback(() => {
    setLoading(true);
    const params = new URLSearchParams();
    if (filter) params.set("status", filter);
    if (search) params.set("search", search);
    const qs = params.toString() ? `?${params}` : "";
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases${qs}`)
      .then((r) => setCases(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [filter, search]);

  useEffect(() => {
    const timer = setTimeout(load, 300);
    return () => clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    if (search.length < 2) { setPatientSuggestions([]); return; }
    const timer = setTimeout(() => {
      api
        .get<PaginatedResponse<PatientListItem>>(`/api/patients?search=${encodeURIComponent(search)}&pageSize=6`)
        .then((r) => setPatientSuggestions(r.data.data))
        .catch(() => {});
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  const handleStatus = async (id: string, status: string) => {
    try {
      await api.put(`/api/surgery-cases/${id}/status`, { status });
      load();
    } catch {}
  };

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-[#0d2137]">الجراحة</h1>
          <p className="text-sm text-[#64748b] mt-0.5">الحالات الجراحية</p>
        </div>
        <Link href="/surgery/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
        >
          <Plus className="w-4 h-4" />
          حالة جراحية
        </Link>
      </div>

      {/* Search + Filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="relative min-w-56">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-[#94a3b8]" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالاسم أو رقم الحالة..."
            className="w-full h-9 pe-9 ps-3 text-sm rounded-lg border-[1.5px] border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-accent-blue"
          />
        </div>
        {["", "scheduled", "in_progress", "completed"].map((s) => (
          <button key={s} onClick={() => setFilter(s)}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium",
              filter === s
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
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-16 bg-[#eef3f9] rounded-xl" />)}
        </div>
      ) : cases.length === 0 && !patientSuggestions.length ? (
        <div className="text-center py-20 text-[#94a3b8]">
          <Scissors className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد حالات جراحية</p>
        </div>
      ) : cases.length > 0 ? (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
                <tr>
                  {["رقم الحالة", "المريض", "نوع الجراحة", "الأسنان", "الطبيب", "التاريخ", "الحالة", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-bold text-[#64748b] whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {cases.map((c) => (
                  <tr key={c.id} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-3 font-mono font-semibold text-accent-blue text-xs">
                      <Link href={`/surgery/${c.id}`} className="hover:underline">{c.caseNumber}</Link>
                    </td>
                    <td className="px-4 py-3">
                      <Link href={`/patients/${c.patientId}`} className="font-medium text-[#0d2137] hover:text-accent-blue">
                        {c.patientName}
                      </Link>
                      <div className="text-xs text-[#94a3b8] font-mono">{c.patientNumber}</div>
                    </td>
                    <td className="px-4 py-3 text-[#64748b]">{c.surgeryType}</td>
                    <td className="px-4 py-3 font-mono text-xs text-[#64748b]">{c.teethInvolved ?? "—"}</td>
                    <td className="px-4 py-3">
                      {c.doctorName ? (
                        <div className="flex items-center gap-1.5">
                          <div className="w-2 h-2 rounded-full" style={{ backgroundColor: c.doctorColor ?? "#3d7ab5" }} />
                          <span className="text-[#64748b]">{c.doctorName}</span>
                        </div>
                      ) : "—"}
                    </td>
                    <td className="px-4 py-3 text-[#64748b] text-xs">{formatArabicDate(c.createdAt)}</td>
                    <td className="px-4 py-3">
                      <span className={cn("text-xs px-[10px] py-[2px] rounded-full font-medium",
                        STATUS_COLORS[c.status] ?? "bg-[#94a3b818] text-[#94a3b8]"
                      )}>
                        {STATUS_LABELS[c.status] ?? c.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 flex items-center gap-2">
                      <Link href={`/surgery/${c.id}`} className="text-xs text-accent-blue hover:underline font-medium">عرض</Link>
                      {c.status === "scheduled" && (
                        <button onClick={() => handleStatus(c.id, "in_progress")}
                          className="text-xs text-[#f59e0b] hover:underline font-medium"
                        >بدء</button>
                      )}
                      {c.status === "in_progress" && (
                        <button onClick={() => handleStatus(c.id, "completed")}
                          className="text-xs text-[#22c55e] hover:underline font-medium"
                        >إكمال</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {/* Patient suggestions — patients without a surgery case */}
      {search.length >= 2 && patientSuggestions.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-[#64748b] uppercase tracking-wide mb-2">
            مرضى مسجّلون — بدون حالة جراحية
          </p>
          <div className="bg-white rounded-xl border border-dashed border-[#dce8f5] divide-y divide-[#f1f5f9] shadow-card">
            {patientSuggestions.map((p) => (
              <div key={p.id} className="flex items-center justify-between px-4 py-3">
                <div>
                  <span className="font-medium text-sm text-[#0d2137]">{p.fullName}</span>
                  <span className="text-xs text-[#94a3b8] font-mono ms-2">{p.patientNumber}</span>
                </div>
                <Link
                  href={`/surgery/new?patientId=${p.id}&patientName=${encodeURIComponent(p.fullName)}`}
                  className="flex items-center gap-1.5 text-xs font-medium text-accent-blue hover:opacity-80 transition"
                >
                  <UserPlus className="w-3.5 h-3.5" />
                  إنشاء حالة جراحية
                </Link>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
