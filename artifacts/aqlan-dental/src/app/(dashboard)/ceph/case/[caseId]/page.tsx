
import { useEffect, useMemo, useState } from "react";
import Link from "@/lib/nextLinkCompat";
import { useParams } from "@/lib/nextNavCompat";
import { ArrowRight, BadgeCheck, Clock3, FileSearch, GitCompareArrows, Loader2, Microscope } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import type { CephAnalysisList, CephClinicalTag } from "@/types/ceph";
import { CasePresentationPanel } from "@/components/ortho/CasePresentationPanel";

export default function CephCaseReviewPage() {
  const { caseId } = useParams<{ caseId: string }>();
  const [analyses, setAnalyses] = useState<CephAnalysisList[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    api.get<CephAnalysisList[]>(`/api/ceph?orthoCaseId=${caseId}`)
      .then(response => { if (active) setAnalyses(response.data); })
      .catch(() => { if (active) setError(true); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [caseId]);

  const tags = useMemo(() => {
    const byKey = new Map<string, CephClinicalTag>();
    analyses.forEach(analysis => (analysis.clinicalTags ?? []).forEach(tag => byKey.set(tag.key, tag)));
    return [...byKey.values()];
  }, [analyses]);
  const patientName = analyses[0]?.patientName ?? "حالة التقويم";
  const caseNumber = analyses[0]?.caseNumber;
  const approvedCount = analyses.filter(item => item.isApproved).length;

  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f4f7fb]">
      <header className="border-b border-gray-200 bg-white px-4 py-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3"><Link href="/ceph" className="rounded border border-gray-200 p-2 text-gray-500 hover:bg-gray-50" title="العودة إلى السيفالومتري"><ArrowRight className="h-4 w-4" /></Link><div><h1 className="text-base font-extrabold text-gray-900">مراجعة حالة السيفالو</h1><p className="text-xs text-gray-500">{patientName}{caseNumber ? ` | ${caseNumber}` : ""}</p></div></div>
          <div className="flex flex-wrap gap-2"><Link href={`/ceph/timelapse/${caseId}`} className="inline-flex items-center gap-2 rounded border border-blue-200 bg-blue-50 px-3 py-2 text-xs font-bold text-clinic-blue"><Clock3 className="h-4 w-4" />Timelapse</Link><Link href={`/ortho/${caseId}/model-analysis`} className="inline-flex items-center gap-2 rounded border border-gray-200 bg-white px-3 py-2 text-xs font-bold text-gray-700"><Microscope className="h-4 w-4" />Occlusogram</Link><Link href={`/ortho/${caseId}`} className="inline-flex items-center gap-2 rounded bg-clinic-blue px-3 py-2 text-xs font-bold text-white"><FileSearch className="h-4 w-4" />حالة التقويم</Link></div>
        </div>
      </header>

      <div className="space-y-3 p-4">
        <section className="grid border border-gray-200 bg-white sm:grid-cols-3">
          <ReviewMetric label="سجلات السيفالو" value={loading ? "..." : String(analyses.length)} />
          <ReviewMetric label="تحاليل معتمدة" value={loading ? "..." : String(approvedCount)} bordered />
          <ReviewMetric label="وسوم معتمدة" value={loading ? "..." : String(tags.length)} bordered />
        </section>

        {tags.length > 0 && <section className="flex flex-wrap items-center gap-2 border border-gray-200 bg-white p-3"><span className="text-xs font-bold text-gray-600">الوسوم السريرية:</span>{tags.map(tag => <span key={tag.key} className="rounded border border-emerald-200 bg-emerald-50 px-2 py-1 text-[10px] font-bold text-emerald-700">{tag.label}</span>)}</section>}

        <div className="grid gap-3 xl:grid-cols-[360px_minmax(0,1fr)]">
          <section className="border border-gray-200 bg-white">
            <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2"><h2 className="text-xs font-extrabold text-gray-800">السجلات المرتبة</h2>{analyses.length >= 2 && <Link href={`/ceph/compare?records=${analyses.slice(0, 6).map(item => item.id).join(",")}`} className="inline-flex items-center gap-1 text-[10px] font-bold text-clinic-blue"><GitCompareArrows className="h-3.5 w-3.5" />تراكب</Link>}</div>
            {loading ? <div className="grid h-40 place-items-center"><Loader2 className="h-6 w-6 animate-spin text-clinic-blue" /></div> : error ? <p className="p-6 text-center text-xs text-red-600">تعذر تحميل سجلات السيفالو.</p> : (
              <div className="divide-y divide-gray-100">{analyses.map(analysis => <Link key={analysis.id} href={`/ceph/${analysis.id}`} className="block px-3 py-3 hover:bg-gray-50"><div className="flex items-center justify-between gap-2"><span className="text-xs font-bold text-gray-800">{ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}</span>{analysis.isApproved && <span className="inline-flex items-center gap-1 text-[10px] font-bold text-emerald-700"><BadgeCheck className="h-3.5 w-3.5" />معتمد</span>}</div><p className="mt-1 text-[10px] text-gray-500">{formatArabicDate(analysis.analysisDate)}</p><div className="mt-2 flex flex-wrap gap-1">{(analysis.clinicalTags ?? []).map(tag => <span key={tag.key} className="rounded bg-gray-100 px-1.5 py-0.5 text-[9px] text-gray-600">{tag.label}</span>)}</div></Link>)}</div>
            )}
          </section>

          <section className="min-w-0 border border-gray-200 bg-white p-4"><CasePresentationPanel caseId={caseId} /></section>
        </div>
      </div>
    </div>
  );
}

function ReviewMetric({ label, value, bordered = false }: { label: string; value: string; bordered?: boolean }) {
  return <div className={cn("px-4 py-3", bordered && "border-t border-gray-200 sm:border-s sm:border-t-0")}><p className="text-[10px] font-medium text-gray-500">{label}</p><p className="mt-1 text-xl font-extrabold text-gray-900">{value}</p></div>;
}
