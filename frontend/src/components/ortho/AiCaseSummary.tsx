"use client";
import { useState } from "react";
import { Sparkles, FileText, AlertTriangle, ChevronDown, ChevronUp } from "lucide-react";
import { useCaseSummary, useOrthoCaseDataForAi } from "@/hooks/useAiAdvisory";
import type { CaseSummary as CaseSummaryType } from "@/types/ai";

export function AiCaseSummary({ caseId }: { caseId: string }) {
  const [summary, setSummary] = useState<CaseSummaryType | null>(null);
  const [error, setError] = useState("");
  const { data: caseData, isLoading: dataLoading } = useOrthoCaseDataForAi(caseId);
  const summaryMutation = useCaseSummary();
  const [expanded, setExpanded] = useState(true);

  const handleGenerate = async () => {
    if (!caseData) return;
    setError("");
    summaryMutation.mutate(
      { caseData },
      {
        onSuccess: (data) => setSummary(data),
        onError: () => setError("فشل إنشاء ملخص الحالة. حاول مرة أخرى."),
      }
    );
  };

  return (
    <div className="bg-gradient-to-l from-purple-50 to-teal-50 rounded-xl border border-purple-200 overflow-hidden">
      <div
        className="flex items-center justify-between px-4 py-3 cursor-pointer"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <Sparkles className="w-4 h-4 text-purple-600" />
          <h3 className="text-sm font-bold text-purple-800">ملخص الحالة بالذكاء الاصطناعي</h3>
          <span className="text-[10px] px-1.5 py-0.5 bg-purple-100 text-purple-600 rounded-full font-medium">مقترح</span>
        </div>
        {expanded ? <ChevronUp className="w-4 h-4 text-purple-400" /> : <ChevronDown className="w-4 h-4 text-purple-400" />}
      </div>

      {expanded && (
        <div className="px-4 pb-4 space-y-3">
          {!summary && !summaryMutation.isPending && (
            <div className="text-center py-4">
              <button
                onClick={handleGenerate}
                disabled={dataLoading || !caseData}
                className="inline-flex items-center gap-2 px-5 py-2.5 text-sm font-medium rounded-lg bg-purple-600 text-white hover:bg-purple-700 disabled:opacity-50 transition"
              >
                <Sparkles className="w-4 h-4" />
                إنشاء ملخص بالذكاء الاصطناعي
              </button>
              {dataLoading && <p className="text-xs text-gray-400 mt-2">جاري تحميل بيانات الحالة...</p>}
            </div>
          )}

          {summaryMutation.isPending && (
            <div className="flex items-center justify-center gap-2 py-6">
              <div className="w-5 h-5 border-2 border-purple-300 border-t-purple-600 rounded-full animate-spin" />
              <span className="text-sm text-purple-600">جاري تحليل الحالة بالذكاء الاصطناعي...</span>
            </div>
          )}

          {error && (
            <div className="flex items-center gap-2 p-3 bg-red-50 rounded-lg text-sm text-red-600">
              <AlertTriangle className="w-4 h-4" />
              {error}
            </div>
          )}

          {summary && (
            <div className="space-y-3">
              <SummarySection icon={<FileText className="w-4 h-4" />} title="الملخص التنفيذي" content={summary.executiveSummary} />
              <SummarySection title="تحليل الهيكل العظمي" content={summary.skeletalAnalysis} />
              <SummarySection title="تحليل الأسنان" content={summary.dentalAnalysis} />
              <SummarySection title="تحليل الأنسجة الرخوة" content={summary.softTissueAnalysis} />
              <SummarySection title="تقدم العلاج" content={summary.treatmentProgress} />

              {summary.recommendations.length > 0 && (
                <div className="bg-white/60 rounded-lg p-3">
                  <h4 className="text-xs font-bold text-teal-700 mb-2">التوصيات</h4>
                  <ul className="space-y-1">
                    {summary.recommendations.map((r, i) => (
                      <li key={i} className="text-xs text-gray-700 flex items-start gap-1.5">
                        <span className="text-teal-500 mt-0.5">•</span>
                        {r}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {summary.riskFactors.length > 0 && (
                <div className="bg-amber-50/60 rounded-lg p-3">
                  <h4 className="text-xs font-bold text-amber-700 mb-2 flex items-center gap-1.5">
                    <AlertTriangle className="w-3.5 h-3.5" />
                    عوامل الخطر
                  </h4>
                  <ul className="space-y-1">
                    {summary.riskFactors.map((r, i) => (
                      <li key={i} className="text-xs text-amber-800 flex items-start gap-1.5">
                        <span className="mt-0.5">•</span>
                        {r}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              <button
                onClick={() => { setSummary(null); }}
                className="text-xs text-purple-600 hover:underline"
              >
                إنشاء ملخص جديد
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function SummarySection({ title, content, icon }: { title: string; content: string; icon?: React.ReactNode }) {
  return (
    <div className="bg-white/60 rounded-lg p-3">
      <h4 className="text-xs font-bold text-gray-700 mb-1 flex items-center gap-1.5">
        {icon}
        {title}
      </h4>
      <p className="text-xs text-gray-600 leading-relaxed">{content}</p>
    </div>
  );
}
