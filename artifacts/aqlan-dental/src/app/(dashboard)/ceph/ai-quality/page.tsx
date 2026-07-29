
import { useCallback, useEffect, useState } from "react";
import Link from "@/lib/nextLinkCompat";
import { AlertTriangle, ArrowRight, CheckCircle2, Gauge, Loader2, RefreshCw } from "lucide-react";
import api from "@/lib/api";
import type { CephAiAccuracySummary } from "@/types/ceph";

const formatMm = (value: number) => `${value.toFixed(2)} mm`;
const formatPercent = (value: number) => `${value.toFixed(1)}%`;

export default function CephAiQualityPage() {
  const [summary, setSummary] = useState<CephAiAccuracySummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await api.get<CephAiAccuracySummary>("/api/ceph/ai/accuracy");
      setSummary(response.data);
    } catch (requestError) {
      const message = (requestError as { response?: { data?: { message?: string } } })
        .response?.data?.message;
      setError(message ?? "تعذر تحميل مؤشرات دقة نقاط الذكاء الاصطناعي");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-extrabold text-gray-900">
            <Gauge className="h-6 w-6 text-emerald-700" />
            دقة تحديد نقاط AI داخل العيادة
          </h1>
          <p className="mt-1 text-sm text-gray-500">
            قياس الفرق بين اقتراح النموذج والموضع الذي راجعه طبيب التقويم، بالمليمتر وبعد معايرة الصورة.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => void load()}
            disabled={loading}
            title="تحديث المؤشرات"
            className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-50"
          >
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
          </button>
          <Link href="/ceph" className="inline-flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50">
            <ArrowRight className="h-4 w-4" />
            العودة للسيفالو
          </Link>
        </div>
      </header>

      {error && (
        <div role="alert" className="flex items-center gap-2 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {error}
        </div>
      )}

      {loading && !summary ? (
        <div className="flex min-h-48 items-center justify-center text-sm text-gray-500">
          <Loader2 className="me-2 h-5 w-5 animate-spin" />
          جارٍ حساب المؤشرات المحلية...
        </div>
      ) : summary ? (
        <>
          <section className={`rounded-md border px-4 py-3 ${summary.sufficientSample ? "border-emerald-200 bg-emerald-50 text-emerald-900" : "border-amber-200 bg-amber-50 text-amber-900"}`}>
            <div className="flex items-start gap-2">
              {summary.sufficientSample
                ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" />
                : <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />}
              <div>
                <h2 className="text-sm font-bold">
                  {summary.sufficientSample ? "عينة محلية قابلة للمتابعة" : "النتيجة أولية والعينة غير كافية"}
                </h2>
                <p className="mt-1 text-xs leading-6">{summary.notice}</p>
              </div>
            </div>
          </section>

          <section className="grid grid-cols-1 gap-px overflow-hidden rounded-md border border-gray-200 bg-gray-200 sm:grid-cols-4">
            <div className="bg-white p-4"><p className="text-xs text-gray-500">النقاط المصححة</p><p className="mt-1 text-2xl font-extrabold tabular-nums text-gray-900">{summary.reviewedPointCount}</p></div>
            <div className="bg-white p-4"><p className="text-xs text-gray-500">التحليلات المستقلة</p><p className="mt-1 text-2xl font-extrabold tabular-nums text-gray-900">{summary.reviewedAnalysisCount}</p></div>
            <div className="bg-white p-4"><p className="text-xs text-gray-500">الحد الأدنى</p><p className="mt-1 text-2xl font-extrabold tabular-nums text-gray-900">{summary.minimumReviewedPoints} / {summary.minimumReviewedAnalyses}</p></div>
            <div className="bg-white p-4"><p className="text-xs text-gray-500">نماذج مقاسة</p><p className="mt-1 text-2xl font-extrabold tabular-nums text-gray-900">{summary.models.length}</p></div>
          </section>

          <section className="overflow-hidden rounded-md border border-gray-200 bg-white">
            <div className="border-b border-gray-200 px-4 py-3">
              <h2 className="text-sm font-bold text-gray-900">النتائج حسب إصدار النموذج</h2>
              <p className="mt-1 text-xs text-gray-500">الأقل أفضل في متوسط ووسيط وP95. النسب الأعلى أفضل ضمن 1 و2 مم.</p>
            </div>
            {summary.models.length === 0 ? (
              <div className="px-4 py-14 text-center text-sm text-gray-500">
                لا توجد تصحيحات محفوظة بعد. راجع نقاط AI واحفظها لبدء القياس المحلي.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[760px] text-sm">
                  <thead className="bg-gray-50 text-xs text-gray-500">
                    <tr>
                      <th className="px-4 py-3 text-start font-semibold">النموذج</th>
                      <th className="px-3 py-3 text-start font-semibold">التحاليل</th>
                      <th className="px-3 py-3 text-start font-semibold">النقاط</th>
                      <th className="px-3 py-3 text-start font-semibold">المتوسط</th>
                      <th className="px-3 py-3 text-start font-semibold">الوسيط</th>
                      <th className="px-3 py-3 text-start font-semibold">P95</th>
                      <th className="px-3 py-3 text-start font-semibold">ضمن 1 مم</th>
                      <th className="px-3 py-3 text-start font-semibold">ضمن 2 مم</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {summary.models.map((model) => (
                      <tr key={model.modelId}>
                        <td className="px-4 py-3 font-mono text-xs font-semibold text-gray-800">
                          <div>{model.modelId}</div>
                          <span className={model.sufficientSample ? "text-emerald-700" : "text-amber-700"}>
                            {model.sufficientSample ? "عينة قابلة للمتابعة" : "عينة أولية"}
                          </span>
                        </td>
                        <td className="px-3 py-3 tabular-nums text-gray-600">{model.analysisCount}</td>
                        <td className="px-3 py-3 tabular-nums text-gray-600">{model.reviewedPointCount}</td>
                        <td className="px-3 py-3 font-mono text-gray-800">{formatMm(model.meanErrorMm)}</td>
                        <td className="px-3 py-3 font-mono text-gray-800">{formatMm(model.medianErrorMm)}</td>
                        <td className="px-3 py-3 font-mono text-gray-800">{formatMm(model.p95ErrorMm)}</td>
                        <td className="px-3 py-3 font-mono text-emerald-700">{formatPercent(model.withinOneMmPercent)}</td>
                        <td className="px-3 py-3 font-mono text-emerald-700">{formatPercent(model.withinTwoMmPercent)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <p className="text-xs leading-6 text-gray-500">
            هذه مؤشرات QA داخلية من تصحيحات الأطباء وليست مقارنة مباشرة مع WebCeph أو اعتمادًا تنظيميًا. إثبات التكافؤ يحتاج مجموعة صور مرجعية يراجعها اختصاصيون بنفس بروتوكول القياس لكلا النظامين.
          </p>
        </>
      ) : null}
    </div>
  );
}
