"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { Plus, GitBranch, Search, UserPlus, Activity, CheckCircle2, Clock, TrendingUp } from "lucide-react";
import type { OrthoCase } from "@/types/ortho";
import type { PatientListItem } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import api from "@/lib/api";
import { cn, formatYemeniRiyal } from "@/lib/utils";

const STATUS_LABELS: Record<string, string> = {
  active: "نشطة",
  completed: "مكتملة",
  on_hold: "موقوفة",
  cancelled: "ملغاة",
  retention: "قيد المتابعة",
};

// Badge: padding 2px 10px, rounded-full, bg {color}18, text {color}
const STATUS_BADGE: Record<string, string> = {
  active:     "bg-[#3d7ab518] text-[#3d7ab5]",
  completed:  "bg-[#22c55e18] text-[#22c55e]",
  on_hold:    "bg-[#f59e0b18] text-[#f59e0b]",
  cancelled:  "bg-[#94a3b818] text-[#94a3b8]",
  retention:  "bg-[#a855f718] text-[#a855f7]",
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

  // Parallel patient search
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

  // Compute stats
  const totalCount = cases.length;
  const activeCount = cases.filter(c => c.status === "active").length;
  const completedCount = cases.filter(c => c.status === "completed").length;
  const retentionCount = cases.filter(c => c.status === "retention" || c.status === "on_hold").length;

  const STATS = [
    { label: "إجمالي الحالات", value: totalCount, icon: Activity, color: "#3d7ab5" },
    { label: "نشطة", value: activeCount, icon: TrendingUp, color: "#22c55e" },
    { label: "مكتملة", value: completedCount, icon: CheckCircle2, color: "#a855f7" },
    { label: "قيد المتابعة", value: retentionCount, icon: Clock, color: "#f5922e" },
  ];

  return (
    <div className="space-y-5 max-w-6xl" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: "#0d2137" }}>التقويم</h1>
          <p className="text-sm mt-0.5" style={{ color: "#64748b" }}>الحالات التقويمية</p>
        </div>
        <Link
          href="/ortho/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-xl text-white transition hover:opacity-90"
          style={{ backgroundColor: "#a855f7" }}
        >
          <Plus className="w-4 h-4" />
          حالة جديدة
        </Link>
      </div>

      {/* Mini stat cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {STATS.map((s) => {
          const Icon = s.icon;
          return (
            <div
              key={s.label}
              className="bg-white rounded-xl border p-4 shadow-sm"
              style={{ borderColor: "#e8f0f9", boxShadow: "0 1px 3px rgba(13,33,55,0.06)" }}
            >
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-medium" style={{ color: "#64748b" }}>{s.label}</span>
                <div
                  className="w-8 h-8 rounded-lg flex items-center justify-center"
                  style={{ backgroundColor: s.color + "14" }}
                >
                  <Icon className="w-4 h-4" style={{ color: s.color }} />
                </div>
              </div>
              <p className="text-2xl font-bold" style={{ color: "#0d2137" }}>{s.value}</p>
            </div>
          );
        })}
      </div>

      {/* Search + Filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="relative min-w-56">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 end-3" style={{ color: "#94a3b8" }} />
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="بحث بالاسم أو رقم الحالة..."
            className="w-full h-10 pe-9 ps-3 text-sm rounded-xl border bg-white focus:outline-none focus:ring-2"
            style={{ borderColor: "#dce8f5", color: "#0d2137" }}
          />
        </div>
        {["", "active", "completed", "on_hold", "retention"].map((s) => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            className={cn(
              "px-3 py-2 text-sm rounded-xl border transition font-medium",
              statusFilter === s
                ? "text-white border-transparent"
                : "border-transparent hover:opacity-80"
            )}
            style={statusFilter === s
              ? { backgroundColor: "#a855f7", borderColor: "#a855f7", color: "#fff" }
              : { backgroundColor: "#f7fafd", color: "#64748b", borderColor: "#e8f0f9" }
            }
          >
            {s === "" ? "الكل" : STATUS_LABELS[s] ?? s}
          </button>
        ))}
      </div>

      {/* Table */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-16 rounded-xl" style={{ backgroundColor: "#f7fafd" }} />
          ))}
        </div>
      ) : cases.length === 0 && !patientSuggestions.length ? (
        <div className="text-center py-20" style={{ color: "#94a3b8" }}>
          <GitBranch className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد حالات تقويمية</p>
        </div>
      ) : cases.length > 0 ? (
        <div
          className="bg-white rounded-xl border shadow-sm overflow-hidden"
          style={{ borderColor: "#e8f0f9", boxShadow: "0 1px 3px rgba(13,33,55,0.06)" }}
        >
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead style={{ backgroundColor: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
                <tr>
                  {["رقم الحالة", "المريض", "الجهاز", "الطبيب", "التقدم", "الرسوم", "الحالة", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold whitespace-nowrap" style={{ color: "#64748b" }}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {cases.map((c) => (
                  <tr key={c.id} className="hover:opacity-95 transition" style={{ backgroundColor: "transparent" }}>
                    <td className="px-4 py-3 font-mono font-semibold text-xs" style={{ color: "#a855f7" }}>
                      {c.caseNumber}
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium" style={{ color: "#0d2137" }}>{c.patientName}</div>
                      <div className="text-xs font-mono" style={{ color: "#94a3b8" }}>{c.patientNumber}</div>
                    </td>
                    <td className="px-4 py-3" style={{ color: "#0d2137" }}>{c.applianceType ?? "—"}</td>
                    <td className="px-4 py-3">
                      {c.doctorName ? (
                        <div className="flex items-center gap-2">
                          <div
                            className="w-2 h-2 rounded-full flex-shrink-0"
                            style={{ backgroundColor: c.doctorColor ?? "#3d7ab5" }}
                          />
                          <span style={{ color: "#0d2137" }}>{c.doctorName}</span>
                        </div>
                      ) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 min-w-[100px]">
                        {/* Progress bar: track bg #f1f5f9, fill gradient #3d7ab5 to #2d5e8e */}
                        <div className="flex-1 h-1.5 rounded-full overflow-hidden" style={{ backgroundColor: "#f1f5f9" }}>
                          <div
                            className="h-full rounded-full transition-all"
                            style={{
                              width: `${c.stagePercentage}%`,
                              background: "linear-gradient(to left, #3d7ab5, #2d5e8e)",
                            }}
                          />
                        </div>
                        <span className="text-xs flex-shrink-0" style={{ color: "#64748b" }}>{c.stagePercentage}%</span>
                      </div>
                      {c.currentStage && (
                        <div className="text-xs mt-0.5" style={{ color: "#94a3b8" }}>{c.currentStage}</div>
                      )}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs" style={{ color: "#0d2137" }}>
                      {c.totalFee ? formatYemeniRiyal(c.totalFee) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn("text-xs py-0.5 rounded-full font-medium", STATUS_BADGE[c.status] ?? "bg-[#94a3b818] text-[#94a3b8]")}
                        style={{ padding: "2px 10px" }}
                      >
                        {STATUS_LABELS[c.status] ?? c.status}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <Link
                        href={`/ortho/${c.id}`}
                        className="text-xs font-medium hover:underline"
                        style={{ color: "#3d7ab5" }}
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

      {/* Patient suggestions */}
      {search.length >= 2 && patientSuggestions.length > 0 && (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide mb-2" style={{ color: "#64748b" }}>
            مرضى مسجّلون — بدون حالة تقويمية
          </p>
          <div
            className="bg-white rounded-xl border divide-y shadow-sm"
            style={{ borderColor: "#dce8f5" }}
          >
            {patientSuggestions.map((p) => (
              <div key={p.id} className="flex items-center justify-between px-4 py-3">
                <div>
                  <span className="font-medium text-sm" style={{ color: "#0d2137" }}>{p.fullName}</span>
                  <span className="text-xs font-mono ms-2" style={{ color: "#94a3b8" }}>{p.patientNumber}</span>
                </div>
                <Link
                  href={`/ortho/new?patientId=${p.id}&patientName=${encodeURIComponent(p.fullName)}`}
                  className="flex items-center gap-1.5 text-xs font-medium hover:opacity-80 transition"
                  style={{ color: "#a855f7" }}
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
