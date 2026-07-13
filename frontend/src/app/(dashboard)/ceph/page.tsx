"use client";
import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { Activity, AlertCircle, Brain, CheckCircle2, FileText, GitCompareArrows, Layers3, Plus, Ruler, ShieldCheck, Smile, UserSquare2, X } from "lucide-react";
import type { CephAnalysisList } from "@/types/ceph";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { getCephWorkflowStage } from "@/lib/cephWorkflow";

function ReadinessCard({ label, value, hint, tone }: { label: string; value: string | number; hint: string; tone: "green" | "blue" | "amber" | "gray" }) {
  const cls = tone === "green" ? "border-emerald-100 bg-emerald-50 text-emerald-800" : tone === "blue" ? "border-blue-100 bg-blue-50 text-blue-800" : tone === "amber" ? "border-amber-100 bg-amber-50 text-amber-800" : "border-gray-100 bg-gray-50 text-gray-700";
  return <div className={cn("rounded-xl border p-3", cls)}><div className="text-[11px] font-medium opacity-75">{label}</div><div className="mt-1 text-xl font-extrabold tabular-nums">{value}</div><div className="mt-1 text-[11px] leading-5 opacity-80">{hint}</div></div>;
}

export default function CephPage() {
  const [analyses, setAnalyses] = useState<CephAnalysisList[]>([]);
  const [loading, setLoading] = useState(true);
  const [compareBase, setCompareBase] = useState<CephAnalysisList | null>(null);
  const [multiSelectMode, setMultiSelectMode] = useState(false);
  const [multiSelection, setMultiSelection] = useState<CephAnalysisList[]>([]);

  useEffect(() => {
    api.get<CephAnalysisList[]>("/api/ceph")
      .then((r) => setAnalyses(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const stats = useMemo(() => {
    const total = analyses.length;
    const traced = analyses.filter((item) => item.landmarkCount >= 24).length;
    const measured = analyses.filter((item) => item.hasMeasurements).length;
    const approved = analyses.filter((item) => getCephWorkflowStage(item).complete).length;
    const incomplete = total - approved;
    return { total, traced, measured, approved, incomplete };
  }, [analyses]);

  const toggleMultiSelection = (analysis: CephAnalysisList) => {
    setMultiSelection(current => {
      if (current.some(item => item.id === analysis.id)) {
        return current.filter(item => item.id !== analysis.id);
      }
      if (current.length >= 6 || (current[0] && current[0].orthoCaseId !== analysis.orthoCaseId)) {
        return current;
      }
      return [...current, analysis];
    });
  };

  const closeMultiSelect = () => {
    setMultiSelectMode(false);
    setMultiSelection([]);
  };

  return (
    <div className="space-y-5 max-w-6xl">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">السيفالومتري</h1>
          <p className="text-sm text-gray-500 mt-0.5">تحليل الأشعة السيفالومترية، المعالم، القياسات، التشخيص، المقارنة، VTO والتقارير</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => {
              setMultiSelectMode(current => !current);
              setMultiSelection([]);
              setCompareBase(null);
            }}
            className={cn(
              "flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium transition",
              multiSelectMode
                ? "border-blue-300 bg-blue-50 text-blue-800"
                : "border-gray-300 text-gray-700 hover:bg-gray-50",
            )}
          >
            <Layers3 className="h-4 w-4" />
            تراكب متعدد
          </button>
          <Link href="/ceph/quality" className="flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-blue-200 bg-blue-50 text-blue-800 hover:bg-blue-100 transition"><ShieldCheck className="w-4 h-4" />فحص الجودة</Link>
          <Link href="/ceph/photo" className="flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"><UserSquare2 className="w-4 h-4" />تحليل صورة بروفايل</Link>
          <Link href="/ceph/photo/frontal" className="flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"><Smile className="w-4 h-4" />تحليل صورة أمامية</Link>
          <Link href="/ceph/new" className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"><Plus className="w-4 h-4" />تحليل جديد</Link>
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <ReadinessCard label="إجمالي التحاليل" value={stats.total} hint="كل تحاليل السيفالو المحفوظة" tone="blue" />
        <ReadinessCard label="نقاط مكتملة" value={stats.traced} hint="24/24 نقطة موضوعة" tone="green" />
        <ReadinessCard label="قياسات محفوظة" value={stats.measured} hint="لا تعني اعتماد التقرير" tone="blue" />
        <ReadinessCard label="مكتملة ومعتمدة" value={stats.approved} hint="اجتازت اعتماد الطبيب" tone="green" />
        <ReadinessCard label="تحتاج متابعة" value={stats.incomplete} hint="تتبع أو قياسات أو اعتماد" tone={stats.incomplete ? "amber" : "green"} />
      </div>

      <div className="rounded-xl border border-blue-100 bg-blue-50 p-3 text-xs leading-6 text-blue-900">
        <div className="flex items-start gap-2"><CheckCircle2 className="mt-1 h-4 w-4 shrink-0 text-blue-700" /><div><b>مسار العمل 10/10:</b> ارفع الصورة → عاير المسطرة → ضع أو راجع 24 نقطة → احفظ واحسب → راجع التشخيص → صدّر PDF أو افتح VTO. لا تعتمد التقرير من بيانات غير محفوظة.</div></div>
      </div>

      {compareBase && (
        <div className="flex items-center gap-2 bg-blue-50 border border-blue-200 rounded-lg px-3 py-2 text-sm text-blue-800">
          <GitCompareArrows className="w-4 h-4 shrink-0" />
          <span>أساس المقارنة: <span className="font-semibold">{compareBase.patientName}</span> — {formatArabicDate(compareBase.analysisDate)}</span>
          <span className="text-xs text-blue-500">اختر «قارن مع هذا» على تحليل آخر لنفس الحالة</span>
          <button onClick={() => setCompareBase(null)} className="ms-auto p-1 rounded hover:bg-blue-100 transition text-blue-600" title="إلغاء المقارنة"><X className="w-3.5 h-3.5" /></button>
        </div>
      )}

      {multiSelectMode && (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-800">
          <Layers3 className="h-4 w-4 shrink-0" />
          <span>السجلات المختارة: <b>{multiSelection.length}</b> من 6</span>
          {multiSelection[0] && <span className="text-xs text-blue-600">{multiSelection[0].patientName}</span>}
          <div className="ms-auto flex items-center gap-2">
            {multiSelection.length >= 2 ? (
              <Link
                href={`/ceph/compare?records=${multiSelection.map(item => encodeURIComponent(item.id)).join(",")}`}
                className="rounded-md bg-clinic-blue px-3 py-1.5 text-xs font-bold text-white hover:opacity-90"
              >
                فتح التراكب
              </Link>
            ) : (
              <span className="text-xs text-blue-500">اختر سجلين على الأقل</span>
            )}
            <button
              type="button"
              onClick={closeMultiSelect}
              title="إلغاء التحديد"
              aria-label="إلغاء تحديد التراكب المتعدد"
              className="rounded p-1 text-blue-600 hover:bg-blue-100"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
      )}

      {loading ? (
        <div className="space-y-2 animate-pulse">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-xl" />)}</div>
      ) : analyses.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-gray-200 bg-white py-16 text-center text-gray-400">
          <Activity className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm font-medium text-gray-500">لا توجد تحاليل سيفالومترية</p>
          <p className="text-xs mt-1 text-gray-400">ابدأ من حالة التقويم أو أنشئ تحليلًا جديدًا، ثم ارفع الصورة وعاير المسطرة.</p>
          <div className="mt-4 flex justify-center gap-2"><Link href="/ceph/new" className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-4 py-2 text-xs font-bold text-white"><Plus className="h-4 w-4" />تحليل جديد</Link></div>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200"><tr>{[...(multiSelectMode ? ["اختيار"] : []), "المريض", "نوع التحليل", "التاريخ", "جاهزية النقاط", "القياسات", "مرحلة العمل", ""].map((h) => <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>)}</tr></thead>
              <tbody className="divide-y divide-gray-100">
                {analyses.map((a) => {
                  const isBase = compareBase?.id === a.id;
                  const isSameCase = !!compareBase && !isBase && compareBase.orthoCaseId === a.orthoCaseId;
                  const isMultiSelected = multiSelection.some(item => item.id === a.id);
                  const isMultiSameCase = !multiSelection[0] || multiSelection[0].orthoCaseId === a.orthoCaseId;
                  const landmarkReady = a.landmarkCount >= 24;
                  const workflow = getCephWorkflowStage(a);
                  return (
                    <tr key={a.id} className={cn("transition", isBase || isMultiSelected ? "bg-blue-50/60" : isSameCase ? "bg-emerald-50/40 hover:bg-emerald-50/70" : "hover:bg-gray-50", (compareBase && !isBase && !isSameCase) || (multiSelectMode && !isMultiSameCase) ? "opacity-50" : "")}>
                      {multiSelectMode && (
                        <td className="px-4 py-3">
                          <input
                            type="checkbox"
                            checked={isMultiSelected}
                            disabled={!isMultiSelected && (!isMultiSameCase || multiSelection.length >= 6)}
                            onChange={() => toggleMultiSelection(a)}
                            aria-label={`اختيار تحليل ${a.patientName} بتاريخ ${formatArabicDate(a.analysisDate)}`}
                            className="h-4 w-4 accent-clinic-blue"
                          />
                        </td>
                      )}
                      <td className="px-4 py-3"><div className="font-medium text-gray-900">{a.patientName}</div>{a.caseNumber && <div className="text-xs text-gray-400 font-mono">{a.caseNumber}</div>}</td>
                      <td className="px-4 py-3"><span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full font-medium">{ANALYSIS_TYPE_AR[a.analysisType] ?? a.analysisType}</span></td>
                      <td className="px-4 py-3 text-gray-500 text-xs">{formatArabicDate(a.analysisDate)}</td>
                      <td className="px-4 py-3"><div className="flex items-center gap-2"><span className={cn("inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold", landmarkReady ? "bg-green-50 text-green-700" : a.landmarkCount > 0 ? "bg-amber-50 text-amber-700" : "bg-gray-100 text-gray-500")}><Ruler className="h-3.5 w-3.5" />{a.landmarkCount >= 24 ? "مكتمل" : `${a.landmarkCount}/24`}</span>{a.aiAssisted && <span title="مساعدة الذكاء الاصطناعي"><Brain className="w-3.5 h-3.5 text-purple-500" /></span>}</div></td>
                      <td className="px-4 py-3">{a.hasMeasurements ? <span className="inline-flex items-center gap-1 text-xs text-green-600 font-medium"><FileText className="h-3.5 w-3.5" />محسوبة</span> : <span className="inline-flex items-center gap-1 text-xs text-amber-600"><AlertCircle className="h-3.5 w-3.5" />تحتاج حفظ وحساب</span>}</td>
                      <td className="px-4 py-3"><span className={cn("inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold", workflow.complete ? "bg-green-50 text-green-700" : workflow.key === "approval" ? "bg-blue-50 text-blue-700" : "bg-amber-50 text-amber-700")}>{workflow.complete ? <CheckCircle2 className="h-3.5 w-3.5" /> : <AlertCircle className="h-3.5 w-3.5" />}{workflow.label}</span></td>
                      <td className="px-4 py-3"><div className="flex items-center gap-3 whitespace-nowrap"><Link href={`/ceph/${a.id}`} className={cn("text-xs font-medium hover:underline", workflow.complete ? "text-clinic-blue" : "text-amber-700")}>{workflow.actionLabel}</Link><Link href={`/ceph/${a.id}/quality`} className="text-xs text-blue-700 hover:underline font-medium">فحص الجودة</Link>{!multiSelectMode && (isBase ? <span className="text-xs text-blue-600 font-medium">أساس المقارنة</span> : isSameCase ? <Link href={`/ceph/compare?baseId=${compareBase!.id}&targetId=${a.id}`} className="text-xs px-2 py-1 rounded-lg bg-emerald-600 text-white hover:opacity-90 transition font-medium">قارن مع هذا</Link> : !compareBase ? <button onClick={() => setCompareBase(a)} className="text-xs text-gray-500 hover:text-clinic-blue hover:underline transition font-medium">قارن</button> : null)}</div></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
