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
  scheduled:   "bg-blue-50 text-blue-700",
  in_progress: "bg-yellow-50 text-yellow-700",
  completed:   "bg-green-50 text-green-700",
  cancelled:   "bg-gray-100 text-gray-500",
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
    api.get<SurgeryCase[]>(`/api/surgery-cases${qs}`)
      .then((r) => setCases(r.data))
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
          <h1 className="text-2xl font-extrabold text-gray-900">الجراحة</h1>
          <p className="text-sm text-gray-500 mt-0.5">الحالات الجراحية</p>
        </div>
        <Link href="/surgery/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          حالة جراحية
        </Link>
      </div>

      {/* Search + Filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="relative min-w-56">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3 text-gray-400" />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالاسم أو رقم الحالة..."
            className="w-full h-9 pe-9 ps-3 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          />
        </div>
        {["", "scheduled", "in_progress", "completed"].map((s) => (
          <button key={s} onClick={() => setFilter(s)}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium",
              filter === s
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
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-xl" />)}
        </div>
      ) : cases.length === 0 && !patientSuggestions.length ? (
        <div className="text-center py-20 text-gray-400">
          <Scissors className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد حالات جراحية</p>
        </div>
      ) : cases.length > 0 ? (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["رقم الحالة", "المريض", "نوع الجراحة", "الأسنان", "الطبيب", "التاريخ", "الحالة", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {cases.map((c) => (
                  <tr key={c.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-mono font-semibold text-clinic-teal text-xs">{c.caseNumber}</td>
                    <td className="px-4 py-3">
                      <Link href={`/patients/${c.patientId}`} className="font-medium text-gray-900 hover:text-clinic-teal">
                        {c.patientName}
                      </Link>
                      <div className="text-xs text-gray-400 font-mono">{c.patientNumber}</div>
                    </td>
                    <td className="px-4 py-3 text-gray-700">{c.surgeryType}</td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-600">{c.teethInvolved ?? "—"}</td>
                    <td className="px-4 py-3">
                      {c.doctorName ? (
                        <div className="flex items-center gap-1.5">
                          <div className="w-2 h-2 rounded-full" style={{ backgroundColor: c.doctorColor ?? "#0E7490" }} />
                          <span className="text-gray-700">{c.doctorName}</span>
                        </div>
                      ) : "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">{formatArabicDate(c.createdAt)}</td>
                    <td className="px-4 py-3">
                      <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium",
                        STATUS_COLORS[c.status] ?? "bg-gray-100 text-gray-600"
                      )}>
                        {STATUS_LABELS[c.status] ?? c.status}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      {c.status === "scheduled" && (
                        <button onClick={() => handleStatus(c.id, "in_progress")}
                          className="text-xs text-yellow-700 hover:underline font-medium"
                        >بدء</button>
                      )}
                      {c.status === "in_progress" && (
                        <button onClick={() => handleStatus(c.id, "completed")}
                          className="text-xs text-green-700 hover:underline font-medium"
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
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
            مرضى مسجّلون — بدون حالة جراحية
          </p>
          <div className="bg-white rounded-xl border border-dashed border-gray-300 divide-y divide-gray-100 shadow-sm">
            {patientSuggestions.map((p) => (
              <div key={p.id} className="flex items-center justify-between px-4 py-3">
                <div>
                  <span className="font-medium text-sm text-gray-900">{p.fullName}</span>
                  <span className="text-xs text-gray-400 font-mono ms-2">{p.patientNumber}</span>
                </div>
                <Link
                  href={`/surgery/new?patientId=${p.id}&patientName=${encodeURIComponent(p.fullName)}`}
                  className="flex items-center gap-1.5 text-xs font-medium text-clinic-teal hover:opacity-80 transition"
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
