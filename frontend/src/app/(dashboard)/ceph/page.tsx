"use client";
import { useEffect, useState, useMemo } from "react";
import Link from "next/link";
import { Activity, Plus, Brain, GitCompare } from "lucide-react";
import type { CephAnalysisList, AnalysisType } from "@/types/ceph";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { CephComparison } from "@/components/ceph/CephComparison";

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

  // Check if an analysis belongs to a case with multiple analyses
  const canCompare = (orthoCaseId: string) => casesWithMultiple.has(orthoCaseId);

  const handleCompare = (orthoCaseId: string) => {
    setComparisonCaseId(orthoCaseId);
    setShowComparison(true);
  };

  // All analysis type options for display
  const analysisTypeColors: Record<string, string> = {
    steiner:    "bg-blue-50 text-blue-700",
    tweed:      "bg-emerald-50 text-emerald-700",
    mcnamara:   "bg-violet-50 text-violet-700",
    ricketts:   "bg-amber-50 text-amber-700",
    downs:      "bg-rose-50 text-rose-700",
    wits:       "bg-cyan-50 text-cyan-700",
    jarabak:    "bg-orange-50 text-orange-700",
    softtissue: "bg-pink-50 text-pink-700",
    full:       "bg-teal-50 text-teal-700",
  };

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">السيفالومتري</h1>
          <p className="text-sm text-gray-500 mt-0.5">تحليل الأشعة السيفالومترية وقياسات الهيكل العظمي</p>
        </div>
        <Link href="/ceph/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          تحليل جديد
        </Link>
      </div>

      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-xl" />)}
        </div>
      ) : analyses.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <Activity className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد تحاليل سيفالومترية</p>
          <p className="text-xs mt-1 text-gray-300">ابدأ بإنشاء تحليل جديد من صفحة حالة التقويم</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["المريض", "نوع التحليل", "التاريخ", "النقاط", "القياسات", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {analyses.map((a) => (
                  <tr key={a.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-900">{a.patientName}</div>
                      {a.caseNumber && <div className="text-xs text-gray-400 font-mono">{a.caseNumber}</div>}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        analysisTypeColors[a.analysisType] ?? "bg-gray-50 text-gray-700"
                      )}>
                        {ANALYSIS_TYPE_AR[a.analysisType as AnalysisType] ?? a.analysisType}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">{formatArabicDate(a.analysisDate)}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5">
                        <span className={cn("text-xs font-mono font-semibold",
                          a.landmarkCount >= 20 ? "text-green-600" : a.landmarkCount > 0 ? "text-yellow-600" : "text-gray-400"
                        )}>
                          {a.landmarkCount}/24
                        </span>
                        {a.aiAssisted && (
                          <span title="مساعدة الذكاء الاصطناعي">
                            <Brain className="w-3.5 h-3.5 text-purple-400" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      {a.hasMeasurements ? (
                        <span className="text-xs text-green-600 font-medium">✓ محسوبة</span>
                      ) : (
                        <span className="text-xs text-gray-400">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Link href={`/ceph/${a.id}`}
                          className="text-xs text-clinic-teal hover:underline font-medium"
                        >
                          فتح التحليل
                        </Link>
                        {canCompare(a.orthoCaseId) && (
                          <button
                            onClick={() => handleCompare(a.orthoCaseId)}
                            className="text-xs text-indigo-600 hover:text-indigo-800 font-medium flex items-center gap-1"
                            title="مقارنة مع تحليل آخر من نفس الحالة"
                          >
                            <GitCompare className="w-3 h-3" />
                            مقارنة
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
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
