import { printScreen } from "@/lib/printUtils";

import { Suspense, useMemo, useState } from "react";
import Link from "@/lib/nextLinkCompat";
import { useSearchParams } from "@/lib/nextNavCompat";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight, AlertTriangle, Target, RotateCcw, Printer, ShieldCheck,
  Save, Loader2, Plus, History,
} from "lucide-react";
import api from "@/lib/api";
import { applyVtoMovements, approxOverjetMm } from "@/lib/cephMath";
import { CephVtoCanvas } from "@/components/ceph/CephVtoCanvas";
import type { CephAnalysis, CephVtoScenario } from "@/types/ceph";

type Pt = { x: number; y: number };

function toMap(analysis: CephAnalysis): Record<string, Pt> {
  const m: Record<string, Pt> = {};
  for (const l of analysis.landmarks) m[l.key] = { x: l.x, y: l.y };
  return m;
}

function MoveInput({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <div>
      <div className="flex items-center justify-between mb-1">
        <label className="text-xs font-medium text-gray-600">{label}</label>
        <span className="text-xs font-mono text-clinic-blue" dir="ltr">{value > 0 ? `+${value}` : value} مم</span>
      </div>
      <input
        type="range" min={-8} max={8} step={0.5} value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="w-full accent-clinic-blue"
      />
      <div className="flex justify-between text-[9px] text-gray-400 mt-0.5" dir="ltr">
        <span>−8 (للخلف)</span><span>+8 (للأمام)</span>
      </div>
    </div>
  );
}

function VtoPageInner() {
  const params = useSearchParams();
  const analysisId = params.get("analysisId") ?? "";

  const [u1, setU1] = useState(0);
  const [l1, setL1] = useState(0);
  const [scenarioName, setScenarioName] = useState("");
  const [scenarioNotes, setScenarioNotes] = useState("");
  const [scenarioGroupId, setScenarioGroupId] = useState<string | null>(null);
  const [scenarioSaving, setScenarioSaving] = useState(false);
  const [scenarioError, setScenarioError] = useState<string | null>(null);

  const { data, isLoading, error } = useQuery({
    queryKey: ["ceph-analysis-vto", analysisId],
    enabled: Boolean(analysisId),
    retry: false,
    queryFn: async () => (await api.get<CephAnalysis>(`/api/ceph/${encodeURIComponent(analysisId)}`)).data,
  });
  const {
    data: scenarios = [],
    isLoading: scenariosLoading,
    refetch: refetchScenarios,
  } = useQuery({
    queryKey: ["ceph-vto-scenarios", analysisId],
    enabled: Boolean(analysisId),
    retry: false,
    queryFn: async () => (
      await api.get<{ data: CephVtoScenario[] }>(`/api/ceph/${encodeURIComponent(analysisId)}/vto-scenarios`)
    ).data.data,
  });

  const pixelsPerMm = data?.pixelsPerMm ?? null;
  const calibrated = pixelsPerMm !== null && pixelsPerMm > 0;

  const original = useMemo(() => (data ? toMap(data) : {}), [data]);
  const predicted = useMemo(
    () => (calibrated ? applyVtoMovements(original, { u1, l1 }, pixelsPerMm!) : original),
    [original, u1, l1, calibrated, pixelsPerMm],
  );

  const overjetBefore = calibrated ? approxOverjetMm(original, pixelsPerMm!) : null;
  const overjetAfter = calibrated ? approxOverjetMm(predicted, pixelsPerMm!) : null;

  // Bodily movement shifts the crown tip AND the apex together, so all four
  // incisor landmarks must exist — otherwise the plan would move tips without
  // their apex/axis and misrepresent the planned tooth movement.
  const hasIncisors = Boolean(
    original["U1T"] && original["U1A"] && original["L1T"] && original["L1A"],
  );

  const resetScenario = () => {
    setScenarioGroupId(null);
    setScenarioName("");
    setScenarioNotes("");
    setU1(0);
    setL1(0);
    setScenarioError(null);
  };

  const loadScenario = (scenario: CephVtoScenario) => {
    setScenarioGroupId(scenario.scenarioGroupId);
    setScenarioName(scenario.name);
    setScenarioNotes(scenario.notes ?? "");
    setU1(scenario.upperIncisorMoveMm);
    setL1(scenario.lowerIncisorMoveMm);
    setScenarioError(null);
  };

  const saveScenario = async () => {
    if (!data?.isApproved || !calibrated || !hasIncisors || !scenarioName.trim() || scenarioSaving) return;
    setScenarioSaving(true);
    setScenarioError(null);
    try {
      const response = await api.post<CephVtoScenario>(
        `/api/ceph/${encodeURIComponent(analysisId)}/vto-scenarios`,
        {
          scenarioGroupId,
          name: scenarioName.trim(),
          upperIncisorMoveMm: u1,
          lowerIncisorMoveMm: l1,
          notes: scenarioNotes.trim() || null,
        },
      );
      setScenarioGroupId(response.data.scenarioGroupId);
      await refetchScenarios();
    } catch (saveError) {
      setScenarioError(
        (saveError as { response?: { data?: { message?: string } } }).response?.data?.message
        ?? "تعذر حفظ سيناريو VTO",
      );
    } finally {
      setScenarioSaving(false);
    }
  };

  if (!analysisId) {
    return (
      <div className="space-y-5 max-w-5xl">
        <Breadcrumb />
        <div className="text-center py-20 text-gray-400">
          <AlertTriangle className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لم يُحدّد تحليل لإنشاء هدف العلاج</p>
          <Link href="/ceph" className="inline-block mt-4 text-sm text-clinic-blue hover:underline font-medium">
            العودة إلى قائمة التحاليل
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5 max-w-5xl">
      <Breadcrumb />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Link href={`/ceph/${analysisId}`}
            className="no-print p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div>
            <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">
              <Target className="w-6 h-6 text-clinic-blue" />
              هدف العلاج البصري (VTO)
            </h1>
            {data && <p className="text-sm text-gray-500 mt-0.5">{data.patientName}</p>}
          </div>
        </div>
        <div className="no-print flex flex-wrap items-center gap-2">
          <Link
            href={`/ceph/${analysisId}/quality`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-blue-200 bg-blue-50 text-blue-800 hover:bg-blue-100 transition"
          >
            <ShieldCheck className="w-3.5 h-3.5" />فحص الجودة
          </Link>
          <button onClick={() => printScreen()}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition">
            <Printer className="w-3.5 h-3.5" />طباعة
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="h-80 bg-gray-100 rounded-xl animate-pulse" />
      ) : error || !data ? (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-4 text-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          {(error as { response?: { data?: { message?: string } } } | null)?.response?.data?.message ?? "تعذر تحميل التحليل"}
        </div>
      ) : (
        <>
          <div className="no-print bg-blue-50 border border-blue-200 rounded-lg p-2.5 text-[11px] leading-5 text-blue-800">
            قبل استخدام VTO كمرجع تعليمي أو طباعته، افتح فحص الجودة وتأكد من الصورة والمعايرة والنقاط والقياسات.
            <Link href={`/ceph/${analysisId}/quality`} className="ms-1 font-bold underline">فتح فحص الجودة</Link>
          </div>

          {/* Honest disclaimer */}
          <div className="bg-amber-50 border border-amber-200 rounded-lg p-2.5 text-[11px] text-amber-800">
            يعرض هذا التتبّع الحركات التي يخطّط لها الأخصائي يدويًا فقط (إزاحة جسمية للقواطع)، وليس تنبؤًا آليًا
            بالنتيجة النهائية أو باستجابة الأنسجة الرخوة.
          </div>

          {!calibrated && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-2.5 text-[11px] text-blue-700">
              تخطيط الحركات بالملّيمترات يتطلب معايرة الصورة أولًا — افتح التحليل وعايِر المسطرة، ثم عُد إلى هنا.
            </div>
          )}

          {!hasIncisors && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-2.5 text-[11px] text-blue-700">
              يتطلب VTO وجود معالم القواطع الأربعة (طرف وذروة العلوي U1T/U1A والسفلي L1T/L1A) في التحليل.
            </div>
          )}

          {!data.isApproved && (
            <div className="bg-amber-50 border border-amber-200 rounded-lg p-2.5 text-[11px] text-amber-800">
              اعتماد التحليل مطلوب قبل حفظ سيناريو VTO. تبقى الحركات الحالية معاينة غير محفوظة.
            </div>
          )}

          <div className="grid lg:grid-cols-3 gap-4">
            {/* Tracing */}
            <div className="lg:col-span-2 bg-white rounded-xl border border-gray-200 shadow-sm p-4">
              <CephVtoCanvas original={original} predicted={predicted} />
            </div>

            {/* Controls */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-bold text-gray-700">الحركات المخطّطة</h3>
                <div className="no-print flex items-center gap-2">
                  <button
                    type="button"
                    onClick={resetScenario}
                    className="rounded p-1 text-gray-500 hover:bg-gray-100 hover:text-clinic-blue"
                    title="سيناريو جديد"
                    aria-label="سيناريو جديد"
                  >
                    <Plus className="w-3.5 h-3.5" />
                  </button>
                  <button
                    type="button"
                    onClick={() => { setU1(0); setL1(0); }}
                    className="rounded p-1 text-gray-500 hover:bg-gray-100 hover:text-clinic-blue"
                    title="تصفير الحركات"
                    aria-label="تصفير الحركات"
                  >
                    <RotateCcw className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>

              <fieldset disabled={!calibrated || !hasIncisors} className="space-y-4 disabled:opacity-50">
                <MoveInput label="القاطع العلوي (U1) — أفقي" value={u1} onChange={setU1} />
                <MoveInput label="القاطع السفلي (L1) — أفقي" value={l1} onChange={setL1} />
              </fieldset>

              {/* Overjet readout */}
              <div className="border-t border-gray-100 pt-3">
                <p className="text-xs font-semibold text-gray-600 mb-2">الـ overjet التقريبي</p>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-gray-500">الحالي</span>
                  <span className="font-mono text-gray-700" dir="ltr">
                    {overjetBefore === null ? "—" : `${overjetBefore} مم`}
                  </span>
                </div>
                <div className="flex items-center justify-between text-sm mt-1">
                  <span className="text-clinic-blue font-medium">بعد التخطيط</span>
                  <span className="font-mono font-bold text-clinic-blue" dir="ltr">
                    {overjetAfter === null ? "—" : `${overjetAfter} مم`}
                  </span>
                </div>
                <p className="text-[9px] text-gray-400 mt-2 leading-relaxed">
                  قيمة هندسية تقريبية (الفجوة الأفقية بين حافتَي القاطعين) — ليست قياسًا سريريًا معتمدًا للـ overjet.
                </p>
              </div>

              <div className="no-print space-y-2 border-t border-gray-100 pt-3">
                <input
                  type="text"
                  value={scenarioName}
                  maxLength={100}
                  onChange={event => setScenarioName(event.target.value)}
                  placeholder="اسم السيناريو"
                  className="w-full rounded-md border border-gray-200 px-2.5 py-2 text-xs focus:border-clinic-blue focus:outline-none"
                />
                <textarea
                  value={scenarioNotes}
                  maxLength={2000}
                  rows={3}
                  onChange={event => setScenarioNotes(event.target.value)}
                  placeholder="ملاحظات الطبيب"
                  className="w-full resize-none rounded-md border border-gray-200 px-2.5 py-2 text-xs focus:border-clinic-blue focus:outline-none"
                />
                {scenarioError && <p className="text-[10px] text-red-600">{scenarioError}</p>}
                <button
                  type="button"
                  onClick={saveScenario}
                  disabled={!data.isApproved || !calibrated || !hasIncisors || !scenarioName.trim() || scenarioSaving}
                  className="flex w-full items-center justify-center gap-1.5 rounded-md bg-clinic-blue px-3 py-2 text-xs font-bold text-white hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  {scenarioSaving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Save className="h-3.5 w-3.5" />}
                  {scenarioGroupId ? "حفظ إصدار جديد" : "حفظ السيناريو"}
                </button>
              </div>
            </div>
          </div>

          <section className="no-print border-t border-gray-200 pt-4">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="flex items-center gap-2 text-sm font-bold text-gray-800">
                <History className="h-4 w-4 text-clinic-blue" />
                السيناريوهات المحفوظة
              </h2>
              <span className="text-[10px] text-gray-400">{scenarios.length} إصدار</span>
            </div>
            {scenariosLoading ? (
              <div className="flex justify-center py-8"><Loader2 className="h-5 w-5 animate-spin text-clinic-blue" /></div>
            ) : scenarios.length === 0 ? (
              <p className="py-6 text-center text-xs text-gray-400">لا توجد سيناريوهات محفوظة.</p>
            ) : (
              <div className="grid gap-2 md:grid-cols-2 lg:grid-cols-3">
                {scenarios.map(scenario => (
                  <button
                    key={scenario.id}
                    type="button"
                    onClick={() => loadScenario(scenario)}
                    className={`rounded-md border p-3 text-start transition ${
                      scenarioGroupId === scenario.scenarioGroupId &&
                      u1 === scenario.upperIncisorMoveMm && l1 === scenario.lowerIncisorMoveMm
                        ? "border-clinic-blue bg-blue-50"
                        : "border-gray-200 bg-white hover:border-blue-200"
                    }`}
                  >
                    <span className="flex items-center justify-between gap-2">
                      <span className="truncate text-xs font-bold text-gray-800">{scenario.name}</span>
                      <span className="font-mono text-[9px] text-gray-400">v{scenario.versionNumber}</span>
                    </span>
                    <span className="mt-2 grid grid-cols-2 gap-2 text-[10px] text-gray-500" dir="ltr">
                      <span>U1 {scenario.upperIncisorMoveMm > 0 ? "+" : ""}{scenario.upperIncisorMoveMm} mm</span>
                      <span>L1 {scenario.lowerIncisorMoveMm > 0 ? "+" : ""}{scenario.lowerIncisorMoveMm} mm</span>
                    </span>
                    <span className="mt-1 block text-[10px] text-clinic-blue" dir="rtl">
                      Overjet: {scenario.overjetBeforeMm ?? "—"} ← {scenario.overjetAfterMm ?? "—"} مم
                    </span>
                    {scenario.notes && <span className="mt-1 block truncate text-[9px] text-gray-400">{scenario.notes}</span>}
                  </button>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function Breadcrumb() {
  return (
    <div className="no-print flex items-center gap-2 text-sm text-gray-500">
      <Link href="/ceph" className="hover:text-clinic-blue transition">السيفالومتري</Link>
      <span>/</span>
      <span className="text-gray-900 font-medium">هدف العلاج البصري</span>
    </div>
  );
}

export default function CephVtoPage() {
  return (
    <Suspense>
      <VtoPageInner />
    </Suspense>
  );
}
