"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ArrowRight, GitCompareArrows, Loader2, Printer } from "lucide-react";
import api from "@/lib/api";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { formatArabicDate } from "@/lib/utils";
import { PhotoLongitudinalImageCompare } from "@/components/ceph/PhotoLongitudinalImageCompare";
import { compareStoredPhotoMeasurements, parseStoredPhotoMeasurements, selectChronologicalPhotoPair } from "@/lib/photoAnalysisComparison";
import type { SavedPhotoAnalysis } from "@/hooks/usePhotoAnalysisCase";

interface PhotoAnalysisDetail extends SavedPhotoAnalysis {
  measurementsJson?: string | null;
}

function CompareInner() {
  const searchParams = useSearchParams();
  const orthoCaseId = searchParams.get("orthoCaseId") ?? "";
  const viewType = searchParams.get("viewType") === "frontal" ? "frontal" : "profile";
  const requestedBaseId = searchParams.get("baseId") ?? "";
  const requestedTargetId = searchParams.get("targetId") ?? "";
  const [records, setRecords] = useState<SavedPhotoAnalysis[]>([]);
  const [baseId, setBaseId] = useState(requestedBaseId);
  const [targetId, setTargetId] = useState(requestedTargetId);
  const [base, setBase] = useState<PhotoAnalysisDetail | null>(null);
  const [target, setTarget] = useState<PhotoAnalysisDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    api.get<SavedPhotoAnalysis[]>(`/api/photo-analysis?orthoCaseId=${encodeURIComponent(orthoCaseId)}`)
      .then(response => {
        if (!active) return;
        const filtered = response.data
          .filter(record => record.viewType === viewType)
          .sort((a, b) => a.analysisDate.localeCompare(b.analysisDate));
        setRecords(filtered);
        const pair = selectChronologicalPhotoPair(filtered, requestedBaseId, requestedTargetId);
        setBaseId(pair.baseId);
        setTargetId(pair.targetId);
      })
      .catch(() => { if (active) setError("تعذّر تحميل سجلات تحليل الصور لهذه الحالة."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [orthoCaseId, requestedBaseId, requestedTargetId, viewType]);

  useEffect(() => {
    let active = true;
    if (!baseId || !targetId || baseId === targetId) {
      setBase(null);
      setTarget(null);
      return () => { active = false; };
    }
    setLoading(true);
    setError("");
    Promise.all([
      api.get<PhotoAnalysisDetail>(`/api/photo-analysis/${encodeURIComponent(baseId)}`),
      api.get<PhotoAnalysisDetail>(`/api/photo-analysis/${encodeURIComponent(targetId)}`),
    ])
      .then(([before, after]) => {
        if (!active) return;
        setBase(before.data);
        setTarget(after.data);
      })
      .catch(() => { if (active) setError("تعذّر تحميل سجلي المقارنة."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [baseId, targetId]);

  const rows = useMemo(() => compareStoredPhotoMeasurements(
    parseStoredPhotoMeasurements(base?.measurementsJson),
    parseStoredPhotoMeasurements(target?.measurementsJson),
  ), [base?.measurementsJson, target?.measurementsJson]);

  const query = orthoCaseId ? `?orthoCaseId=${encodeURIComponent(orthoCaseId)}` : "";
  const compareHref = (type: "profile" | "frontal") => `/ceph/photo/compare?orthoCaseId=${encodeURIComponent(orthoCaseId)}&viewType=${type}`;

  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f4f7fb]">
      <header className="border-b border-gray-200 bg-white px-4 py-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <Link href={orthoCaseId ? `/ortho/${orthoCaseId}?tab=ceph` : "/ceph"} aria-label="العودة" className="border border-gray-200 p-2 text-gray-500 hover:bg-gray-50"><ArrowRight className="h-4 w-4" /></Link>
            <div><h1 className="flex items-center gap-2 text-base font-extrabold text-gray-900"><GitCompareArrows className="h-5 w-5 text-clinic-blue" />مقارنة تحليل الصور قبل وبعد</h1><p className="text-xs text-gray-500">سجلان محفوظان من الحالة نفسها وفرق القياسات الموثقة</p></div>
          </div>
          <button type="button" onClick={() => window.print()} className="no-print inline-flex items-center gap-2 border border-gray-200 bg-white px-3 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50"><Printer className="h-4 w-4" />طباعة المقارنة</button>
        </div>
      </header>

      <div className="space-y-3 p-4">
        <nav aria-label="أنواع تحليل الصور" className="no-print flex flex-wrap gap-2 border border-gray-200 bg-white p-2">
          <Link href={`/ceph/photo${query}`} className="border border-gray-200 px-3 py-2 text-xs font-bold text-gray-700">تحليل البروفايل</Link>
          <Link href={`/ceph/photo/frontal${query}`} className="border border-gray-200 px-3 py-2 text-xs font-bold text-gray-700">التحليل الأمامي</Link>
          <Link href={compareHref("profile")} aria-current={viewType === "profile" ? "page" : undefined} className={`border px-3 py-2 text-xs font-bold ${viewType === "profile" ? "border-blue-300 bg-blue-50 text-clinic-blue" : "border-gray-200 text-gray-700"}`}>مقارنة البروفايل</Link>
          <Link href={compareHref("frontal")} aria-current={viewType === "frontal" ? "page" : undefined} className={`border px-3 py-2 text-xs font-bold ${viewType === "frontal" ? "border-blue-300 bg-blue-50 text-clinic-blue" : "border-gray-200 text-gray-700"}`}>مقارنة أمامية</Link>
        </nav>

        {error && <div role="alert" className="border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div>}
        {records.length >= 2 && (
          <section className="no-print grid gap-3 border border-gray-200 bg-white p-3 md:grid-cols-2">
            <RecordSelect label="السجل السابق" value={baseId} records={records} isBlocked={record => record.analysisDate >= (records.find(item => item.id === targetId)?.analysisDate ?? "")} onChange={setBaseId} />
            <RecordSelect label="السجل اللاحق" value={targetId} records={records} isBlocked={record => record.analysisDate <= (records.find(item => item.id === baseId)?.analysisDate ?? "")} onChange={setTargetId} />
          </section>
        )}

        {loading ? <div className="grid h-72 place-items-center"><Loader2 className="h-8 w-8 animate-spin text-clinic-blue" /></div>
          : records.length < 2 ? <section className="grid min-h-72 place-items-center border border-amber-200 bg-amber-50 p-8 text-center"><div><h2 className="text-base font-extrabold text-amber-900">يلزم سجلان محفوظان للمقارنة</h2><p className="mt-2 text-sm text-amber-800">احفظ تحليلين من النوع نفسه وفي تاريخين مختلفين لعرض التغير الطولي.</p></div></section>
          : base && target ? <>
            <PhotoLongitudinalImageCompare baseImageUrl={resolveImageUrl(base.imageFileUrl) || base.imageFileUrl} targetImageUrl={resolveImageUrl(target.imageFileUrl) || target.imageFileUrl} baseDate={formatArabicDate(base.analysisDate)} targetDate={formatArabicDate(target.analysisDate)} />
            <section className="overflow-hidden border border-gray-200 bg-white">
              <h2 className="border-b border-gray-100 px-3 py-2 text-xs font-extrabold text-gray-800">فرق القياسات المحفوظة</h2>
              <div className="overflow-x-auto"><table className="w-full min-w-[620px] text-xs"><thead className="bg-gray-50 text-gray-500"><tr><th className="px-3 py-2 text-start">القياس</th><th className="px-3 py-2 text-end">قبل</th><th className="px-3 py-2 text-end">بعد</th><th className="px-3 py-2 text-end">الفرق</th></tr></thead><tbody>{rows.map(row => <tr key={row.key} className="border-t border-gray-100"><th className="px-3 py-2 text-start font-bold text-gray-800">{row.nameAr}</th><ValueCell value={row.baseValue} /><ValueCell value={row.targetValue} /><ValueCell value={row.delta} signed /></tr>)}{rows.length === 0 && <tr><td colSpan={4} className="px-3 py-10 text-center text-gray-400">لا توجد قياسات مشتركة محفوظة للمقارنة.</td></tr>}</tbody></table></div>
            </section>
            <p className="border border-blue-100 bg-blue-50 p-3 text-xs text-blue-800">تعرض المقارنة الصور والقياسات المحفوظة فقط. لا تنشئ إطارات وسيطة ولا تتنبأ بالنمو أو الاستجابة العلاجية.</p>
          </> : null}
      </div>
    </div>
  );
}

function RecordSelect({ label, value, records, isBlocked, onChange }: { label: string; value: string; records: SavedPhotoAnalysis[]; isBlocked: (record: SavedPhotoAnalysis) => boolean; onChange: (id: string) => void }) {
  return <label className="space-y-1"><span className="block text-[10px] font-bold text-gray-600">{label}</span><select aria-label={label} value={value} onChange={event => onChange(event.target.value)} className="h-10 w-full border border-gray-200 bg-white px-2 text-xs">{records.map(record => <option key={record.id} value={record.id} disabled={isBlocked(record)}>{formatArabicDate(record.analysisDate)}</option>)}</select></label>;
}

function ValueCell({ value, signed = false }: { value: number | null; signed?: boolean }) {
  const text = value === null ? "—" : signed && value > 0 ? `+${value}` : String(value);
  return <td className="px-3 py-2 text-end font-mono" dir="ltr">{text}</td>;
}

export default function PhotoAnalysisComparePage() {
  return <Suspense fallback={<div className="grid h-72 place-items-center"><Loader2 className="h-8 w-8 animate-spin text-clinic-blue" /></div>}><CompareInner /></Suspense>;
}
