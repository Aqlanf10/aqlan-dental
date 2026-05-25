"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { Plus, GitBranch, Search, UserPlus } from "lucide-react";
import type { OrthoCase } from "@/types/ortho";
import { ORTHO_STAGE_LABELS } from "@/types/ortho";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, formatYemeniRiyal } from "@/lib/utils";

const STATUS_LABELS: Record<string, string> = {
  active: "نشطة",
  completed: "مكتملة",
  on_hold: "موقوفة",
  cancelled: "ملغاة",
};

const STATUS_COLORS: Record<string, string> = {
  active: "bg-clinic-blue-50 text-clinic-blue",
  completed: "bg-green-50 text-green-700",
  on_hold: "bg-yellow-50 text-yellow-700",
  cancelled: "bg-gray-100 text-gray-500",
};

export default function OrthoPage() {
  const [cases, setCases] = useState<OrthoCase[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");
  const [patientSuggestions, setPatientSuggestions] = useState<PatientListItem[]>([]);

  const fetchCases = useCallback(async () => {
    setLoading(true);
    const params = new URLSearchParams({ pageSize: "50" });
    if (statusFilter) params.set("status", statusFilter);
    if (search)       params.set("search", search);
    api.get<OrthoCase[]>(`/api/ortho-cases?${params}`)
      .then((r) => setCases(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [statusFilter, search]);

  useEffect(() => {
    const timer = setTimeout(fetchCases, 300);
    return () => clearTimeout(timer);
  }, [fetchCases]);

  // Parallel patient search — so patients without ortho cases are discoverable
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

  return (
    <div className="space-y-5 max-w-6xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">التقويم</h1>
          <p className="text-sm text-gray-500 mt-0.5">الحالات التقويمية</p>
        </div>
        <Link
          href="/ortho/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          حالة جديدة
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
            className="w-full h-9 pe-9 ps-3 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue"
          />
        </div>
        {["", "active", "completed", "on_hold"].map((s) => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={cn(
              "px-3 py-1.5 text-sm rounded-lg border transition font-medium",
              statusFilter === s
                ? "bg-clinic-blue text-white border-clinic-blue"
                : "border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >
            {s === "" ? "الكل" : STATUS_LABELS[s]}
          </button>
        ))}
      </div>

      {/* Table */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : cases.length === 0 && !patientSuggestions.length ? (
        <div className="text-center py-20 text-gray-400">
          <GitBranch className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد حالات تقويمية</p>
        </div>
      ) : cases.length > 0 ? (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["رقم الحالة", "المريض", "الجهاز", "الطبيب", "التقدم", "الرسوم", "الحالة", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {cases.map((c) => (
                  <tr key={c.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-mono font-semibold text-clinic-blue text-xs">
                      {c.caseNumber}
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-900">{c.patientName}</div>
                      <div className="text-xs text-gray-400 font-mono">{c.patientNumber}</div>
                    </td>
                    <td className="px-4 py-3 text-gray-700">{c.applianceType ?? "—"}</td>
                    <td className="px-4 py-3">
                      {c.doctorName ? (
                        <div className="flex items-center gap-2">
                          <div
                            className="w-2 h-2 rounded-full flex-shrink-0"
                            style={{ backgroundColor: c.doctorColor ?? "#2563EB" }}
                          />
                          <span className="text-gray-700">{c.doctorName}</span>
                        </div>
                      ) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 min-w-[100px]">
                        <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                          <div
                            className="h-full bg-clinic-blue rounded-full transition-all"
                            style={{ width: `${c.stagePercentage}%` }}
                          />
                        </div>
                        <span className="text-xs text-gray-500 flex-shrink-0">{c.stagePercentage}%</span>
                      </div>
                      {c.currentStage && (
                        <div className="text-xs text-gray-400 mt-0.5">{ORTHO_STAGE_LABELS[c.currentStage] ?? c.currentStage}</div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-700 font-mono text-xs">
                      {c.totalFee ? formatYemeniRiyal(c.totalFee) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        STATUS_COLORS[c.status] ?? "bg-gray-100 text-gray-600"
                      )}>
                        {STATUS_LABELS[c.status] ?? c.status}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <Link
                        href={`/ortho/${c.id}`}
                        className="text-xs text-clinic-blue hover:underline font-medium"
                      >
                        عرض
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}

      {/* Patient suggestions — patients without an ortho case */}
      {search.length >= 2 && patientSuggestions.length > 0 && (
        <div>
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
            مرضى مسجّلون — بدون حالة تقويمية
          </p>
          <div className="bg-white rounded-xl border border-dashed border-gray-300 divide-y divide-gray-100 shadow-sm">
            {patientSuggestions.map((p) => (
              <div key={p.id} className="flex items-center justify-between px-4 py-3">
                <div>
                  <span className="font-medium text-sm text-gray-900">{p.fullName}</span>
                  <span className="text-xs text-gray-400 font-mono ms-2">{p.patientNumber}</span>
                </div>
                <Link
                  href={`/ortho/new?patientId=${p.id}&patientName=${encodeURIComponent(p.fullName)}`}
                  className="flex items-center gap-1.5 text-xs font-medium text-clinic-blue hover:opacity-80 transition"
                >
                  <UserPlus className="w-3.5 h-3.5" />
                  إنشاء حالة تقويمية
                </Link>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
