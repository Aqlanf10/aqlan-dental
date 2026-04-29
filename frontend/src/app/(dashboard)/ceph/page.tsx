"use client";
import { useEffect, useState, useMemo } from "react";
import Link from "next/link";
import { Activity, Plus, Brain, GitCompare } from "lucide-react";
import type { CephAnalysisList, AnalysisType } from "@/types/ceph";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { CephComparison } from "@/components/ceph/CephComparison";

// Badge: padding 2px 10px, rounded-full, bg {color}18, text {color}
const analysisTypeBadge: Record<string, { bg: string; color: string }> = {
  steiner:    { bg: "#3d7ab518", color: "#3d7ab5" },
  tweed:      { bg: "#22c55e18", color: "#22c55e" },
  mcnamara:   { bg: "#a855f718", color: "#a855f7" },
  ricketts:   { bg: "#f5922e18", color: "#f5922e" },
  downs:      { bg: "#ef444418", color: "#ef4444" },
  wits:       { bg: "#3d7ab518", color: "#3d7ab5" },
  jarabak:    { bg: "#f59e0b18", color: "#f59e0b" },
  softtissue: { bg: "#a855f718", color: "#a855f7" },
  full:       { bg: "#3d7ab518", color: "#3d7ab5" },
};

export default function CephPage() {
  const [analyses, setAnalyses] = useState<CephAnalysisList[]>([]);
  const [loading, setLoading] = useState(true);
  const [showComparison, setShowComparison] = useState(false);
  const [comparisonCaseId, setComparisonCaseId] = useState<string>("");

  useEffect(() => {
    api.get<CephAnalysisList[]>("/api/ceph")
      .then((r) => setAnalyses(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  // Group analyses by orthoCaseId to find cases with multiple analyses
  const casesWithMultiple = useMemo(() => {
    const caseMap = new Map<string, CephAnalysisList[]>();
    analyses.forEach((a) => {
      if (!caseMap.has(a.orthoCaseId)) caseMap.set(a.orthoCaseId, []);
      caseMap.get(a.orthoCaseId)!.push(a);
    });
    const filteredEntries: [string, CephAnalysisList[]][] = [];
    caseMap.forEach((list, key) => {
      if (list.length >= 2) filteredEntries.push([key, list]);
    });
    return new Map(filteredEntries);
  }, [analyses]);

  const canCompare = (orthoCaseId: string) => casesWithMultiple.has(orthoCaseId);

  const handleCompare = (orthoCaseId: string) => {
    setComparisonCaseId(orthoCaseId);
    setShowComparison(true);
  };

  // Stats
  const totalAnalyses = analyses.length;
  const aiAssistedCount = analyses.filter(a => a.aiAssisted).length;
  const measuredCount = analyses.filter(a => a.hasMeasurements).length;
  const completeLandmarks = analyses.filter(a => a.landmarkCount >= 20).length;

  return (
    <div className="space-y-5 max-w-5xl" dir="rtl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold" style={{ color: "#0d2137" }}>السيفالومتري</h1>
          <p className="text-sm mt-0.5" style={{ color: "#64748b" }}>تحليل الأشعة السيفالومترية وقياسات الهيكل العظمي</p>
        </div>
        <Link href="/ceph/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-xl text-white transition hover:opacity-90"
          style={{ backgroundColor: "#3d7ab5" }}
        >
          <Plus className="w-4 h-4" />
          تحليل جديد
        </Link>
      </div>

      {/* Mini stat cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          { label: "إجمالي التحاليل", value: totalAnalyses, color: "#3d7ab5" },
          { label: "بمساعدة AI", value: aiAssistedCount, color: "#a855f7" },
          { label: "قياسات محسوبة", value: measuredCount, color: "#22c55e" },
          { label: "نقاط مكتملة", value: completeLandmarks, color: "#f5922e" },
        ].map((s) => (
          <div
            key={s.label}
            className="bg-white rounded-xl border p-4 shadow-sm"
            style={{ borderColor: "#e8f0f9", boxShadow: "0 1px 3px rgba(13,33,55,0.06)" }}
          >
            <span className="text-xs font-medium block mb-1" style={{ color: "#64748b" }}>{s.label}</span>
            <p className="text-2xl font-bold" style={{ color: s.color }}>{s.value}</p>
          </div>
        ))}
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-16 rounded-xl" style={{ backgroundColor: "#f7fafd" }} />
          ))}
        </div>
      ) : analyses.length === 0 ? (
        <div className="text-center py-20" style={{ color: "#94a3b8" }}>
          <Activity className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد تحاليل سيفالومترية</p>
          <p className="text-xs mt-1" style={{ color: "#94a3b8" }}>ابدأ بإنشاء تحليل جديد من صفحة حالة التقويم</p>
        </div>
      ) : (
        <div
          className="bg-white rounded-xl border shadow-sm overflow-hidden"
          style={{ borderColor: "#e8f0f9", boxShadow: "0 1px 3px rgba(13,33,55,0.06)" }}
        >
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead style={{ backgroundColor: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
                <tr>
                  {["المريض", "نوع التحليل", "التاريخ", "النقاط", "القياسات", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold whitespace-nowrap" style={{ color: "#64748b" }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {analyses.map((a) => {
                  const badge = analysisTypeBadge[a.analysisType] ?? { bg: "#94a3b818", color: "#94a3b8" };
                  return (
                    <tr key={a.id} className="hover:opacity-95 transition">
                      <td className="px-4 py-3">
                        <div className="font-medium" style={{ color: "#0d2137" }}>{a.patientName}</div>
                        {a.caseNumber && <div className="text-xs font-mono" style={{ color: "#94a3b8" }}>{a.caseNumber}</div>}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className="text-xs py-0.5 rounded-full font-medium"
                          style={{ padding: "2px 10px", backgroundColor: badge.bg, color: badge.color }}
                        >
                          {ANALYSIS_TYPE_AR[a.analysisType as AnalysisType] ?? a.analysisType}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs" style={{ color: "#64748b" }}>{formatArabicDate(a.analysisDate)}</td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-1.5">
                          <span className={cn("text-xs font-mono font-semibold",
                            a.landmarkCount >= 20 ? { color: "#22c55e" } : a.landmarkCount > 0 ? { color: "#f59e0b" } : { color: "#94a3b8" }
                          )}>
                            {a.landmarkCount}/24
                          </span>
                          {a.aiAssisted && (
                            <span title="مساعدة الذكاء الاصطناعي">
                              <Brain className="w-3.5 h-3.5" style={{ color: "#a855f7" }} />
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        {a.hasMeasurements ? (
                          <span className="text-xs font-medium" style={{ color: "#22c55e" }}>✓ محسوبة</span>
                        ) : (
                          <span className="text-xs" style={{ color: "#94a3b8" }}>—</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <Link href={`/ceph/${a.id}`}
                            className="text-xs font-medium hover:underline"
                            style={{ color: "#3d7ab5" }}
                          >
                            فتح التحليل
                          </Link>
                          {canCompare(a.orthoCaseId) && (
                            <button
                              onClick={() => handleCompare(a.orthoCaseId)}
                              className="text-xs font-medium flex items-center gap-1 hover:opacity-80 transition"
                              style={{ color: "#3d7ab5" }}
                              title="مقارنة مع تحليل آخر من نفس الحالة"
                            >
                              <GitCompare className="w-3 h-3" />
                              مقارنة
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Comparison Dialog */}
      <CephComparison
        orthoCaseId={comparisonCaseId}
        open={showComparison}
        onClose={() => setShowComparison(false)}
      />
    </div>
  );
}
