"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRight, BarChart3, Loader2, RefreshCw, ShieldCheck } from "lucide-react";
import api from "@/lib/api";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import type { AnalysisType, CephCohortBucket, CephCohortResult } from "@/types/ceph";

const ANALYSIS_TYPES = Object.entries(ANALYSIS_TYPE_AR) as [AnalysisType, string][];

export default function CephCohortPage() {
  const [filters, setFilters] = useState({ from: "", to: "", analysisType: "all", tag: "" });
  const [requestFilters, setRequestFilters] = useState(filters);
  const [data, setData] = useState<CephCohortResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    setData(null);
    api.get<CephCohortResult>("/api/ceph/cohort", {
      params: {
        from: requestFilters.from || undefined,
        to: requestFilters.to || undefined,
        analysisType: requestFilters.analysisType === "all" ? undefined : requestFilters.analysisType,
        tag: requestFilters.tag || undefined,
      },
    })
      .then(response => { if (active) setData(response.data); })
      .catch((requestError) => {
        if (!active) return;
        setError(requestError?.response?.data?.message ?? "تعذر تحميل تحليل المجموعة.");
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [requestFilters]);

  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f4f7fb]">
      <header className="border-b border-gray-200 bg-white px-4 py-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3"><Link href="/ceph" className="rounded border border-gray-200 p-2 text-gray-500 hover:bg-gray-50" title="العودة إلى السيفالومتري"><ArrowRight className="h-4 w-4" /></Link><div><h1 className="text-base font-extrabold text-gray-900">تحليل مجموعات السيفالو</h1><p className="text-xs text-gray-500">توزيعات مجمعة من أحدث تحليل معتمد لكل مريض ضمن صلاحية المستخدم</p></div></div>
          <span className="inline-flex items-center gap-2 rounded border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-bold text-emerald-700"><ShieldCheck className="h-4 w-4" />حد الخصوصية: 5 مرضى</span>
        </div>
      </header>

      <div className="space-y-3 p-4">
        <form onSubmit={event => { event.preventDefault(); setRequestFilters({ ...filters }); }} className="grid gap-3 border border-gray-200 bg-white p-3 md:grid-cols-[1fr_1fr_1fr_1.4fr_auto] md:items-end">
          <FilterField label="من تاريخ"><input aria-label="من تاريخ" type="date" value={filters.from} onChange={event => setFilters(current => ({ ...current, from: event.target.value }))} className="h-10 w-full rounded border border-gray-200 px-2 text-xs" /></FilterField>
          <FilterField label="إلى تاريخ"><input aria-label="إلى تاريخ" type="date" value={filters.to} onChange={event => setFilters(current => ({ ...current, to: event.target.value }))} className="h-10 w-full rounded border border-gray-200 px-2 text-xs" /></FilterField>
          <FilterField label="نوع التحليل"><select aria-label="نوع التحليل" value={filters.analysisType} onChange={event => setFilters(current => ({ ...current, analysisType: event.target.value }))} className="h-10 w-full rounded border border-gray-200 bg-white px-2 text-xs"><option value="all">كل الأنواع</option>{ANALYSIS_TYPES.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></FilterField>
          <FilterField label="وسم سريري معتمد"><select aria-label="وسم سريري معتمد" value={filters.tag} onChange={event => setFilters(current => ({ ...current, tag: event.target.value }))} className="h-10 w-full rounded border border-gray-200 bg-white px-2 text-xs"><option value="">كل الوسوم</option>{(data?.availableTags ?? []).map(tag => <option key={tag.key} value={tag.key}>{tag.label}</option>)}</select></FilterField>
          <button type="submit" disabled={loading} className="inline-flex h-10 items-center justify-center gap-2 rounded bg-clinic-blue px-4 text-xs font-bold text-white disabled:opacity-50">{loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}تحديث</button>
        </form>

        {error && <div role="alert" className="border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>}
        {loading && !data ? <div className="grid h-72 place-items-center"><Loader2 className="h-8 w-8 animate-spin text-clinic-blue" /></div> : data?.suppressed ? (
          <section className="grid min-h-72 place-items-center border border-amber-200 bg-amber-50 p-8 text-center"><div><ShieldCheck className="mx-auto h-10 w-10 text-amber-600" /><h2 className="mt-3 text-base font-extrabold text-amber-900">النتيجة محجوبة لحماية الخصوصية</h2><p className="mt-2 text-sm text-amber-800">لا تُعرض أعداد أو توزيعات حتى يصل نطاق المرشحات إلى {data.minimumCohortSize} مرضى مؤهلين على الأقل.</p></div></section>
        ) : data ? (
          <>
            <section className="grid border border-gray-200 bg-white sm:grid-cols-3">
              <CohortMetric label="حجم المجموعة" value={String(data.cohortSize ?? 0)} />
              <CohortMetric label="سجلات لكل مريض" value="أحدث سجل فقط" bordered />
              <CohortMetric label="حالة السجلات" value="معتمدة طبيًا" bordered />
            </section>
            <div className="grid gap-3 xl:grid-cols-3">
              <DistributionPanel title="التصنيف الهيكلي" rows={data.skeletalDistribution} />
              <DistributionPanel title="النمط الرأسي" rows={data.verticalDistribution} />
              <DistributionPanel title="ميلان القواطع" rows={data.incisorDistribution} />
            </div>
            <section className="overflow-hidden border border-gray-200 bg-white">
              <h2 className="flex items-center gap-2 border-b border-gray-100 px-3 py-2 text-xs font-extrabold text-gray-800"><BarChart3 className="h-4 w-4 text-clinic-blue" />ملخص القياسات</h2>
              <div className="overflow-x-auto"><table className="w-full min-w-[720px] text-xs"><thead className="bg-gray-50 text-gray-500"><tr><th className="px-3 py-2 text-start">القياس</th><th className="px-3 py-2 text-end">العينة</th><th className="px-3 py-2 text-end">المتوسط</th><th className="px-3 py-2 text-end">الانحراف المعياري</th><th className="px-3 py-2 text-end">الأدنى</th><th className="px-3 py-2 text-end">الأعلى</th></tr></thead><tbody>{data.measurements.map(row => <tr key={`${row.measurementName}-${row.unit}`} className="border-t border-gray-100"><th className="px-3 py-2 text-start font-bold text-gray-800">{row.measurementName}</th><td className="px-3 py-2 text-end font-mono">{row.count}</td><td className="px-3 py-2 text-end font-mono">{row.mean} {row.unit}</td><td className="px-3 py-2 text-end font-mono">{row.standardDeviation}</td><td className="px-3 py-2 text-end font-mono">{row.minimum}</td><td className="px-3 py-2 text-end font-mono">{row.maximum}</td></tr>)}{data.measurements.length === 0 && <tr><td colSpan={6} className="px-3 py-10 text-center text-gray-400">لا توجد قياسات مشتركة في خمسة سجلات أو أكثر.</td></tr>}</tbody></table></div>
            </section>
          </>
        ) : null}
      </div>
    </div>
  );
}

function FilterField({ label, children }: { label: string; children: React.ReactNode }) {
  return <label className="space-y-1"><span className="block text-[10px] font-bold text-gray-600">{label}</span>{children}</label>;
}

function CohortMetric({ label, value, bordered = false }: { label: string; value: string; bordered?: boolean }) {
  return <div className={`px-4 py-3 ${bordered ? "border-t border-gray-200 sm:border-s sm:border-t-0" : ""}`}><p className="text-[10px] text-gray-500">{label}</p><p className="mt-1 text-lg font-extrabold text-gray-900">{value}</p></div>;
}

function DistributionPanel({ title, rows }: { title: string; rows: CephCohortBucket[] }) {
  return <section className="border border-gray-200 bg-white"><h2 className="border-b border-gray-100 px-3 py-2 text-xs font-extrabold text-gray-800">{title}</h2><div className="space-y-3 p-3">{rows.map(row => <div key={row.key}><div className="mb-1 flex items-center justify-between gap-2 text-xs"><span className="font-medium text-gray-700">{row.label}</span><span className="font-mono font-bold text-gray-900" dir="ltr">{row.count} | {row.percentage}%</span></div><div className="h-2 overflow-hidden rounded bg-gray-100"><div className="h-full bg-clinic-blue" style={{ width: `${row.percentage}%` }} /></div></div>)}{rows.length === 0 && <p className="py-6 text-center text-xs text-gray-400">لا توجد بيانات مصنفة.</p>}</div></section>;
}
