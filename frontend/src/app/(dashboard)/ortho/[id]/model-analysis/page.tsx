"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  ArrowRight,
  BadgeCheck,
  Calculator,
  CheckCircle2,
  ClipboardList,
  FilePlus2,
  Loader2,
  Plus,
  Ruler,
  Save,
  ScanLine,
  Trash2,
  TriangleAlert,
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type {
  DentalModelAnalysisInput,
  DentalModelAnalysisRecord,
  DentalModelAnalysisResult,
  HuckabaToothInput,
  MixedDentitionPrediction,
} from "@/types/modelAnalysis";

const UPPER_TEETH = ["16", "15", "14", "13", "12", "11", "21", "22", "23", "24", "25", "26"];
const LOWER_TEETH = ["36", "35", "34", "33", "32", "31", "41", "42", "43", "44", "45", "46"];
const MOYERS_PERCENTILES = [5, 15, 25, 35, 50, 65, 75, 85, 95];

type SectionKey = "teeth" | "pont-howe" | "mixed" | "huckaba" | "results";

const SECTIONS: { key: SectionKey; label: string; icon: typeof Ruler }[] = [
  { key: "teeth", label: "الأسنان والمسافة", icon: Ruler },
  { key: "pont-howe", label: "Pont وAshley Howe", icon: ClipboardList },
  { key: "mixed", label: "الأسنان المختلطة", icon: Calculator },
  { key: "huckaba", label: "Huckaba الشعاعي", icon: ScanLine },
  { key: "results", label: "النتائج السريرية", icon: CheckCircle2 },
];

const createEmptyInputs = (): DentalModelAnalysisInput => ({
  toothWidths: Object.fromEntries([...UPPER_TEETH, ...LOWER_TEETH].map(code => [code, null])),
  upperAvailableSpace: null,
  lowerAvailableSpace: null,
  upperInterpremolarWidth: null,
  upperIntermolarWidth: null,
  howePremolarDiameter: null,
  howePremolarBasalArchWidth: null,
  howeBasalArchLength: null,
  mixedUpperAvailablePerSide: null,
  mixedLowerAvailablePerSide: null,
  moyersPercentile: 75,
  huckabaTeeth: [],
});

const parseMeasurement = (value: string): number | null => {
  if (value.trim() === "") return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

const formatMeasurement = (value: number | null | undefined, suffix = " مم") =>
  value === null || value === undefined ? "-" : `${value.toFixed(2)}${suffix}`;

const getBackendMessage = (error: unknown, fallback: string) =>
  (error as { response?: { data?: { message?: string } } }).response?.data?.message ?? fallback;

export default function ModelAnalysisPage() {
  const { id: orthoCaseId } = useParams<{ id: string }>();
  const [section, setSection] = useState<SectionKey>("teeth");
  const [inputs, setInputs] = useState<DentalModelAnalysisInput>(createEmptyInputs);
  const [results, setResults] = useState<DentalModelAnalysisResult | null>(null);
  const [recordId, setRecordId] = useState<string | null>(null);
  const [approvedAt, setApprovedAt] = useState<string | null>(null);
  const [analysisDate, setAnalysisDate] = useState(new Date().toISOString().slice(0, 10));
  const [dentitionStage, setDentitionStage] = useState<"Permanent" | "Mixed">("Permanent");
  const [notes, setNotes] = useState("");
  const [caseInfo, setCaseInfo] = useState<{ caseNumber?: string; patientName?: string } | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyAction, setBusyAction] = useState<"calculate" | "save" | "approve" | null>(null);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  useEffect(() => {
    let active = true;
    Promise.allSettled([
      api.get(`/api/ortho-cases/${orthoCaseId}`),
      api.get<DentalModelAnalysisRecord | null>(`/api/ortho-cases/${orthoCaseId}/model-analyses/latest`),
    ])
      .then(([caseResult, analysisResult]) => {
        if (!active) return;
        if (caseResult.status === "fulfilled") {
          setCaseInfo(caseResult.value.data as { caseNumber?: string; patientName?: string });
        }
        if (analysisResult.status === "fulfilled" && analysisResult.value.data) {
          loadRecord(analysisResult.value.data);
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [orthoCaseId]);

  const enteredToothCount = useMemo(
    () => Object.values(inputs.toothWidths).filter(value => value !== null && value > 0).length,
    [inputs.toothWidths],
  );

  const isApproved = Boolean(approvedAt);

  function loadRecord(record: DentalModelAnalysisRecord) {
    setRecordId(record.id);
    setInputs({ ...createEmptyInputs(), ...record.inputs });
    setResults(record.results);
    setAnalysisDate(record.analysisDate);
    setDentitionStage(record.dentitionStage);
    setNotes(record.notes ?? "");
    setApprovedAt(record.approvedAt ?? null);
  }

  function markChanged(nextInputs: DentalModelAnalysisInput) {
    setInputs(nextInputs);
    setResults(null);
    setMessage(null);
  }

  function setNumericField(
    field: Exclude<keyof DentalModelAnalysisInput, "toothWidths" | "huckabaTeeth">,
    value: string,
  ) {
    markChanged({ ...inputs, [field]: parseMeasurement(value) });
  }

  function setToothWidth(code: string, value: string) {
    markChanged({
      ...inputs,
      toothWidths: { ...inputs.toothWidths, [code]: parseMeasurement(value) },
    });
  }

  function updateHuckabaRows(rows: HuckabaToothInput[]) {
    markChanged({ ...inputs, huckabaTeeth: rows });
  }

  async function calculate() {
    setBusyAction("calculate");
    setMessage(null);
    try {
      const response = await api.post<DentalModelAnalysisResult>(
        `/api/ortho-cases/${orthoCaseId}/model-analyses/preview`,
        inputs,
      );
      setResults(response.data);
      setSection("results");
    } catch (error) {
      setMessage({ type: "error", text: getBackendMessage(error, "تعذر حساب التحاليل. راجع القياسات المدخلة.") });
    } finally {
      setBusyAction(null);
    }
  }

  async function save() {
    if (isApproved) {
      setMessage({ type: "error", text: "التحليل معتمد. أنشئ نسخة جديدة قبل إدخال قياسات محدثة." });
      return;
    }

    setBusyAction("save");
    setMessage(null);
    try {
      const payload = { analysisDate, dentitionStage, inputs, notes: notes || null };
      const response = recordId
        ? await api.put<DentalModelAnalysisRecord>(
            `/api/ortho-cases/${orthoCaseId}/model-analyses/${recordId}`,
            payload,
          )
        : await api.post<DentalModelAnalysisRecord>(
            `/api/ortho-cases/${orthoCaseId}/model-analyses`,
            payload,
          );
      loadRecord(response.data);
      setMessage({ type: "success", text: "تم حفظ التحليل وربطه بحالة التقويم." });
      setSection("results");
    } catch (error) {
      setMessage({ type: "error", text: getBackendMessage(error, "تعذر حفظ تحليل النماذج.") });
    } finally {
      setBusyAction(null);
    }
  }

  async function approve() {
    if (!recordId || !results) {
      setMessage({ type: "error", text: "احفظ التحليل المحسوب قبل اعتماده." });
      return;
    }
    setBusyAction("approve");
    setMessage(null);
    try {
      const response = await api.patch<DentalModelAnalysisRecord>(
        `/api/ortho-cases/${orthoCaseId}/model-analyses/${recordId}/approve`,
      );
      loadRecord(response.data);
      setMessage({ type: "success", text: "تم اعتماد التحليل وتثبيت القياسات." });
    } catch (error) {
      setMessage({ type: "error", text: getBackendMessage(error, "تعذر اعتماد التحليل.") });
    } finally {
      setBusyAction(null);
    }
  }

  function startNewVersion() {
    setRecordId(null);
    setApprovedAt(null);
    setAnalysisDate(new Date().toISOString().slice(0, 10));
    setMessage({ type: "success", text: "بدأت نسخة جديدة. القياسات السابقة ما زالت محفوظة في السجل." });
    setSection("teeth");
  }

  if (loading) {
    return <div className="flex h-72 items-center justify-center"><Loader2 className="h-8 w-8 animate-spin text-clinic-blue" /></div>;
  }

  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f4f7fb]">
      <header className="border-b border-gray-200 bg-white px-4 py-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <Link href={`/ortho/${orthoCaseId}`} className="rounded border border-gray-200 p-2 text-gray-500 hover:bg-gray-50" title="العودة إلى حالة التقويم">
              <ArrowRight className="h-4 w-4" />
            </Link>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-base font-extrabold text-gray-900">تحاليل النماذج وقياسات الأسنان</h1>
                {isApproved && (
                  <span className="inline-flex items-center gap-1 rounded border border-emerald-200 bg-emerald-50 px-2 py-1 text-[10px] font-bold text-emerald-700">
                    <BadgeCheck className="h-3.5 w-3.5" /> معتمد
                  </span>
                )}
              </div>
              <p className="truncate text-xs text-gray-500">
                {caseInfo?.patientName ?? "حالة التقويم"}
                {caseInfo?.caseNumber ? ` | ${caseInfo.caseNumber}` : ""}
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <input type="date" value={analysisDate} disabled={isApproved} onChange={event => setAnalysisDate(event.target.value)} className="h-9 rounded border border-gray-200 bg-white px-2 text-xs disabled:bg-gray-50" />
            <div className="flex h-9 rounded border border-gray-200 bg-gray-50 p-0.5">
              {([["Permanent", "دائمة"], ["Mixed", "مختلطة"]] as const).map(([value, label]) => (
                <button key={value} type="button" disabled={isApproved} onClick={() => setDentitionStage(value)} className={cn("rounded px-3 text-xs font-medium disabled:opacity-60", dentitionStage === value ? "bg-white text-clinic-blue shadow-sm" : "text-gray-500")}>
                  {label}
                </button>
              ))}
            </div>
            <ActionButton icon={Calculator} label="احسب" loading={busyAction === "calculate"} disabled={isApproved} onClick={calculate} variant="secondary" />
            <ActionButton icon={Save} label={recordId ? "حفظ" : "حفظ جديد"} loading={busyAction === "save"} disabled={isApproved} onClick={save} />
            {recordId && !isApproved && (
              <ActionButton icon={BadgeCheck} label="اعتماد" loading={busyAction === "approve"} onClick={approve} variant="approve" />
            )}
            {isApproved && <ActionButton icon={FilePlus2} label="نسخة جديدة" onClick={startNewVersion} variant="secondary" />}
          </div>
        </div>
      </header>

      {message && (
        <div className={cn("mx-4 mt-3 rounded border px-3 py-2 text-xs", message.type === "success" ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-red-200 bg-red-50 text-red-700")}>
          {message.text}
        </div>
      )}

      <div className="grid gap-0 p-4 lg:grid-cols-[220px_minmax(0,1fr)]">
        <nav className="flex gap-1 overflow-x-auto border border-gray-200 bg-white p-2 lg:flex-col">
          <div className="hidden border-b border-gray-100 px-2 pb-3 lg:block">
            <p className="text-xs font-bold text-gray-800">اكتمال قياسات الأسنان</p>
            <p className="mt-1 text-[11px] text-gray-500">{enteredToothCount} من 24 سنًا</p>
            <div className="mt-2 h-1.5 overflow-hidden rounded bg-gray-100">
              <div className="h-full bg-clinic-blue" style={{ width: `${enteredToothCount / 24 * 100}%` }} />
            </div>
          </div>
          {SECTIONS.map(item => {
            const Icon = item.icon;
            return (
              <button key={item.key} type="button" onClick={() => setSection(item.key)} className={cn("flex flex-shrink-0 items-center gap-2 rounded px-3 py-2 text-start text-xs font-medium", section === item.key ? "bg-blue-50 text-clinic-blue" : "text-gray-600 hover:bg-gray-50")}>
                <Icon className="h-4 w-4" /><span>{item.label}</span>
              </button>
            );
          })}
        </nav>

        <main className="min-w-0 border-x border-b border-gray-200 bg-white p-4 lg:border-s-0 lg:border-t">
          {section === "teeth" && (
            <div className="space-y-6">
              <SectionTitle title="القياسات الميزيوديستالية" description="أدخل أكبر عرض لكل سن بالملليمتر وفق ترقيم FDI. تُستخدم القيم في Bolton وتحليل المسافة وPont وHowe." />
              <ToothRow title="الفك العلوي" codes={UPPER_TEETH} values={inputs.toothWidths} disabled={isApproved} onChange={setToothWidth} />
              <ToothRow title="الفك السفلي" codes={LOWER_TEETH} values={inputs.toothWidths} disabled={isApproved} onChange={setToothWidth} />
              <div className="grid gap-4 border-t border-gray-100 pt-5 md:grid-cols-2">
                <MeasurementField label="المسافة المتاحة في القوس العلوي" hint="Arch perimeter الفعلي على مسار نقاط التماس" value={inputs.upperAvailableSpace} disabled={isApproved} onChange={value => setNumericField("upperAvailableSpace", value)} />
                <MeasurementField label="المسافة المتاحة في القوس السفلي" hint="Arch perimeter الفعلي على مسار نقاط التماس" value={inputs.lowerAvailableSpace} disabled={isApproved} onChange={value => setNumericField("lowerAvailableSpace", value)} />
              </div>
              <ReadinessLine ready={enteredToothCount === 24} text="يتطلب تحليل Bolton الكامل قياسات 12 سنًا في كل فك من الرحى الأولى إلى الرحى الأولى." />
            </div>
          )}

          {section === "pont-howe" && (
            <div className="space-y-5">
              <SectionTitle title="Pont وAshley Howe" description="مؤشرات تخطيطية مساعدة تُفسر مع الفحص السريري والسيفالومتري، ولا تُستخدم منفردة لاتخاذ قرار الخلع أو التوسيع." />
              <div className="grid gap-4 md:grid-cols-2">
                <MeasurementField label="العرض بين الضواحك العلوية" value={inputs.upperInterpremolarWidth} disabled={isApproved} onChange={value => setNumericField("upperInterpremolarWidth", value)} />
                <MeasurementField label="العرض بين الأرحاء العلوية" value={inputs.upperIntermolarWidth} disabled={isApproved} onChange={value => setNumericField("upperIntermolarWidth", value)} />
                <MeasurementField label="Premolar Diameter (PMD)" value={inputs.howePremolarDiameter} disabled={isApproved} onChange={value => setNumericField("howePremolarDiameter", value)} />
                <MeasurementField label="Premolar Basal Arch Width (PMBAW)" value={inputs.howePremolarBasalArchWidth} disabled={isApproved} onChange={value => setNumericField("howePremolarBasalArchWidth", value)} />
                <MeasurementField label="Basal Arch Length" value={inputs.howeBasalArchLength} disabled={isApproved} onChange={value => setNumericField("howeBasalArchLength", value)} />
              </div>
              {results && <ResultsDashboard results={results} only={["pont", "howe"]} />}
            </div>
          )}

          {section === "mixed" && (
            <div className="space-y-5">
              <SectionTitle title="تحليل الأسنان المختلطة" description="Moyers يعتمد جدول الاحتمالات، بينما Tanaka-Johnston يحسب التنبؤ مباشرة من مجموع القواطع السفلية." />
              <div className="grid gap-4 md:grid-cols-3">
                <label className="space-y-1.5">
                  <span className="text-xs font-bold text-gray-700">نسبة Moyers</span>
                  <select value={inputs.moyersPercentile} disabled={isApproved} onChange={event => markChanged({ ...inputs, moyersPercentile: Number(event.target.value) })} className="h-10 w-full rounded border border-gray-200 bg-white px-3 text-sm disabled:bg-gray-50">
                    {MOYERS_PERCENTILES.map(value => <option key={value} value={value}>{value}%</option>)}
                  </select>
                </label>
                <MeasurementField label="المسافة العلوية المتاحة لكل جهة" value={inputs.mixedUpperAvailablePerSide} disabled={isApproved} onChange={value => setNumericField("mixedUpperAvailablePerSide", value)} />
                <MeasurementField label="المسافة السفلية المتاحة لكل جهة" value={inputs.mixedLowerAvailablePerSide} disabled={isApproved} onChange={value => setNumericField("mixedLowerAvailablePerSide", value)} />
              </div>
              <ReadinessLine ready={["32", "31", "41", "42"].every(code => Boolean(inputs.toothWidths[code]))} text="أدخل عروض القواطع السفلية الأربع لتفعيل Moyers وTanaka-Johnston." />
              {results && <ResultsDashboard results={results} only={["mixed"]} />}
            </div>
          )}

          {section === "huckaba" && (
            <HuckabaEditor rows={inputs.huckabaTeeth} results={results} disabled={isApproved} onChange={updateHuckabaRows} />
          )}

          {section === "results" && (
            <div className="space-y-5">
              <SectionTitle title="ملخص التحليل السريري" description="نتائج محسوبة من القياسات الحالية. راجعها سريريًا ثم احفظ واعتمد النسخة النهائية." />
              {results ? <ResultsDashboard results={results} /> : (
                <div className="border border-dashed border-gray-300 px-4 py-14 text-center">
                  <Calculator className="mx-auto mb-3 h-8 w-8 text-gray-300" />
                  <p className="text-sm font-bold text-gray-600">لم تُحسب النتائج بعد</p>
                  <p className="mt-1 text-xs text-gray-400">أدخل القياسات ثم اضغط «احسب»</p>
                </div>
              )}
              <label className="block space-y-1.5 border-t border-gray-100 pt-4">
                <span className="text-xs font-bold text-gray-700">ملاحظات الطبيب</span>
                <textarea value={notes} disabled={isApproved} onChange={event => setNotes(event.target.value)} rows={4} className="w-full resize-y rounded border border-gray-200 px-3 py-2 text-sm disabled:bg-gray-50" placeholder="الملاحظات السريرية وقرار العلاج المرتبط بنتائج التحليل..." />
              </label>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}

function ActionButton({ icon: Icon, label, loading = false, disabled = false, onClick, variant = "primary" }: { icon: typeof Save; label: string; loading?: boolean; disabled?: boolean; onClick: () => void; variant?: "primary" | "secondary" | "approve" }) {
  return (
    <button type="button" onClick={onClick} disabled={disabled || loading} className={cn("flex h-9 items-center gap-2 rounded px-3 text-xs font-bold disabled:opacity-50", variant === "primary" && "bg-clinic-blue text-white", variant === "secondary" && "border border-blue-200 bg-blue-50 text-clinic-blue", variant === "approve" && "bg-emerald-600 text-white")}>
      {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Icon className="h-4 w-4" />}{label}
    </button>
  );
}

function SectionTitle({ title, description }: { title: string; description: string }) {
  return <div><h2 className="text-base font-extrabold text-gray-900">{title}</h2><p className="mt-1 max-w-3xl text-xs leading-6 text-gray-500">{description}</p></div>;
}

function ToothRow({ title, codes, values, disabled, onChange }: { title: string; codes: string[]; values: Record<string, number | null>; disabled: boolean; onChange: (code: string, value: string) => void }) {
  return (
    <section>
      <div className="mb-2 flex items-center justify-between"><h3 className="text-xs font-bold text-gray-700">{title}</h3><span className="text-[10px] text-gray-400">العرض بالملليمتر</span></div>
      <div className="grid grid-cols-6 gap-2 xl:grid-cols-12">
        {codes.map(code => (
          <label key={code} className="min-w-0">
            <span className="mb-1 block text-center font-mono text-[10px] font-bold text-gray-500">{code}</span>
            <input type="number" min={0} max={20} step={0.01} value={values[code] ?? ""} disabled={disabled} onChange={event => onChange(code, event.target.value)} className="h-10 w-full rounded border border-gray-200 px-1 text-center text-xs focus:border-blue-400 focus:outline-none disabled:bg-gray-50" dir="ltr" />
          </label>
        ))}
      </div>
    </section>
  );
}

function MeasurementField({ label, hint, value, disabled, onChange }: { label: string; hint?: string; value: number | null; disabled: boolean; onChange: (value: string) => void }) {
  return (
    <label className="space-y-1.5">
      <span className="block text-xs font-bold text-gray-700">{label}</span>
      <div className="relative">
        <input type="number" min={0} max={200} step={0.01} value={value ?? ""} disabled={disabled} onChange={event => onChange(event.target.value)} className="h-10 w-full rounded border border-gray-200 px-3 ps-11 text-sm focus:border-blue-400 focus:outline-none disabled:bg-gray-50" dir="ltr" />
        <span className="absolute start-3 top-1/2 -translate-y-1/2 text-[10px] text-gray-400">mm</span>
      </div>
      {hint ? <span className="block text-[10px] leading-5 text-gray-400">{hint}</span> : null}
    </label>
  );
}

function ReadinessLine({ ready, text }: { ready: boolean; text: string }) {
  return (
    <div className={cn("flex items-start gap-2 rounded border px-3 py-2 text-xs", ready ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-amber-200 bg-amber-50 text-amber-800")}>
      {ready ? <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0" /> : <TriangleAlert className="mt-0.5 h-4 w-4 flex-shrink-0" />}<span>{text}</span>
    </div>
  );
}

function HuckabaEditor({ rows, results, disabled, onChange }: { rows: HuckabaToothInput[]; results: DentalModelAnalysisResult | null; disabled: boolean; onChange: (rows: HuckabaToothInput[]) => void }) {
  const addRow = () => onChange([...rows, { toothCode: "", radiographicUneruptedWidth: 0, actualReferenceWidth: 0, radiographicReferenceWidth: 0 }]);
  const updateRow = (index: number, patch: Partial<HuckabaToothInput>) => onChange(rows.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <SectionTitle title="Huckaba Radiographic Analysis" description="يصحح تكبير الصورة بمقارنة سن مرجعي ظاهر على النموذج والصورة الشعاعية." />
        <button type="button" onClick={addRow} disabled={disabled} className="flex items-center gap-2 rounded border border-blue-200 bg-blue-50 px-3 py-2 text-xs font-bold text-clinic-blue disabled:opacity-50"><Plus className="h-4 w-4" />إضافة سن</button>
      </div>
      <div className="overflow-x-auto border border-gray-200">
        <table className="w-full min-w-[760px] text-xs">
          <thead className="bg-gray-50 text-gray-600"><tr><th className="px-3 py-2 text-start">السن غير البازغ</th><th className="px-3 py-2 text-start">عرضه في الأشعة</th><th className="px-3 py-2 text-start">العرض الحقيقي للمرجع</th><th className="px-3 py-2 text-start">عرض المرجع في الأشعة</th><th className="px-3 py-2 text-start">التنبؤ المصحح</th><th className="w-12" /></tr></thead>
          <tbody>
            {rows.length === 0 ? <tr><td colSpan={6} className="px-4 py-10 text-center text-gray-400">أضف سنًا غير بازغ لبدء التصحيح الشعاعي</td></tr> : null}
            {rows.map((row, index) => {
              const prediction = results?.huckaba.find(item => item.toothCode === row.toothCode);
              return (
                <tr key={`${index}-${row.toothCode}`} className="border-t border-gray-100">
                  <td className="p-2"><input value={row.toothCode} disabled={disabled} onChange={event => updateRow(index, { toothCode: event.target.value })} placeholder="مثال: 13" className="h-9 w-full rounded border border-gray-200 px-2 disabled:bg-gray-50" /></td>
                  {([["radiographicUneruptedWidth", row.radiographicUneruptedWidth], ["actualReferenceWidth", row.actualReferenceWidth], ["radiographicReferenceWidth", row.radiographicReferenceWidth]] as const).map(([field, value]) => (
                    <td key={field} className="p-2"><input type="number" min={0} max={30} step={0.01} value={value || ""} disabled={disabled} onChange={event => updateRow(index, { [field]: parseMeasurement(event.target.value) ?? 0 })} className="h-9 w-full rounded border border-gray-200 px-2 disabled:bg-gray-50" dir="ltr" /></td>
                  ))}
                  <td className="px-3 py-2 font-bold text-clinic-blue">{formatMeasurement(prediction?.predictedActualWidth)}</td>
                  <td className="p-2"><button type="button" disabled={disabled} onClick={() => onChange(rows.filter((_, rowIndex) => rowIndex !== index))} className="rounded p-2 text-red-500 hover:bg-red-50 disabled:opacity-40" title="حذف الصف"><Trash2 className="h-4 w-4" /></button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function ResultsDashboard({ results, only }: { results: DentalModelAnalysisResult; only?: ("bolton" | "arches" | "pont" | "howe" | "mixed")[] }) {
  const visible = (key: "bolton" | "arches" | "pont" | "howe" | "mixed") => !only || only.includes(key);
  return (
    <div className="space-y-4">
      {visible("bolton") && results.bolton ? <ResultSection title="Bolton"><ResultMetric label="النسبة الكلية" value={`${results.bolton.overallRatio.toFixed(2)}%`} /><ResultMetric label="النسبة الأمامية" value={`${results.bolton.anteriorRatio.toFixed(2)}%`} /><ResultMetric label="التفاوت الكلي" value={formatMeasurement(results.bolton.overallDiscrepancy)} hint={results.bolton.overallInterpretation} /><ResultMetric label="التفاوت الأمامي" value={formatMeasurement(results.bolton.anteriorDiscrepancy)} hint={results.bolton.anteriorInterpretation} /></ResultSection> : null}
      {visible("arches") && (results.upperArch || results.lowerArch) ? <ResultSection title="Arch Perimeter / Space">{results.upperArch ? <ResultMetric label="الفك العلوي" value={formatMeasurement(results.upperArch.discrepancy)} hint={results.upperArch.interpretation} /> : null}{results.lowerArch ? <ResultMetric label="الفك السفلي" value={formatMeasurement(results.lowerArch.discrepancy)} hint={results.lowerArch.interpretation} /> : null}</ResultSection> : null}
      {visible("pont") && results.pont ? <ResultSection title="Pont Index"><ResultMetric label="العرض المتوقع بين الضواحك" value={formatMeasurement(results.pont.predictedInterpremolarWidth)} /><ResultMetric label="العرض المتوقع بين الأرحاء" value={formatMeasurement(results.pont.predictedIntermolarWidth)} /><ResultMetric label="فرق الضواحك" value={formatMeasurement(results.pont.premolarDifference)} /><ResultMetric label="فرق الأرحاء" value={formatMeasurement(results.pont.molarDifference)} /></ResultSection> : null}
      {visible("howe") && results.howe ? <ResultSection title="Ashley Howe"><ResultMetric label="PMD / TTM" value={`${results.howe.premolarDiameterPercent.toFixed(2)}%`} /><ResultMetric label="PMBAW / TTM" value={`${results.howe.premolarBasalArchWidthPercent.toFixed(2)}%`} hint={results.howe.interpretation} /></ResultSection> : null}
      {visible("mixed") && (results.moyers || results.tanakaJohnston) ? <div className="grid gap-4 xl:grid-cols-2">{results.moyers ? <MixedResult title={`Moyers | ${results.moyers.percentile}%`} prediction={results.moyers.prediction} /> : null}{results.tanakaJohnston ? <MixedResult title="Tanaka-Johnston" prediction={results.tanakaJohnston.prediction} /> : null}</div> : null}
      {results.warnings.length > 0 ? <div className="border-s-4 border-amber-400 bg-amber-50 px-3 py-2"><p className="mb-1 text-xs font-bold text-amber-900">ملاحظات التفسير السريري</p>{results.warnings.map(warning => <p key={warning} className="text-[11px] leading-5 text-amber-800">• {warning}</p>)}</div> : null}
    </div>
  );
}

function ResultSection({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="border border-gray-200"><h3 className="border-b border-gray-100 bg-gray-50 px-3 py-2 text-xs font-extrabold text-gray-800">{title}</h3><div className="grid gap-px bg-gray-100 md:grid-cols-2">{children}</div></section>;
}

function ResultMetric({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return <div className="bg-white px-3 py-3"><p className="text-[10px] text-gray-500">{label}</p><p className="mt-1 text-sm font-extrabold text-gray-900" dir="ltr">{value}</p>{hint ? <p className="mt-1 text-[10px] leading-5 text-gray-500">{hint}</p> : null}</div>;
}

function MixedResult({ title, prediction }: { title: string; prediction: MixedDentitionPrediction }) {
  return <section className="border border-gray-200"><h3 className="border-b border-gray-100 bg-gray-50 px-3 py-2 text-xs font-extrabold text-gray-800">{title}</h3><div className="grid grid-cols-2 gap-px bg-gray-100"><ResultMetric label="المتوقع علويًا / جهة" value={formatMeasurement(prediction.predictedUpperPerSide)} /><ResultMetric label="المتوقع سفليًا / جهة" value={formatMeasurement(prediction.predictedLowerPerSide)} /><ResultMetric label="فرق المسافة العلوية" value={formatMeasurement(prediction.upperSpaceDiscrepancyPerSide)} /><ResultMetric label="فرق المسافة السفلية" value={formatMeasurement(prediction.lowerSpaceDiscrepancyPerSide)} /></div></section>;
}
