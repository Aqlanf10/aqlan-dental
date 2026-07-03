"use client";

import { Ruler, Activity, Layers, Scissors, ArrowLeftRight, Gauge } from "lucide-react";
import type {
  ArchPerimeterResult,
  AshleyHoweResult,
  LinderHarthResult,
  PeckPeckResult,
  KorkhausResult,
  NanceMixedResult,
  DentalModelAnalysisResultExtended,
} from "@/types/modelAnalysis";

const NAVY = "#1a3a5c";
const BLUE = "#3d7ab5";

function Card({ icon: Icon, title, children }: {
  icon: React.ElementType;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-xl border border-gray-100 bg-white p-4 shadow-sm">
      <div className="flex items-center gap-2 mb-3">
        <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-clinic-blue/10">
          <Icon className="w-3.5 h-3.5" style={{ color: BLUE }} />
        </div>
        <h4 className="text-sm font-bold" style={{ color: NAVY }}>{title}</h4>
      </div>
      {children}
    </div>
  );
}

function Row({ label, value, unit }: { label: string; value: string | number | null; unit?: string }) {
  const display = value === null || value === undefined || value === "" ? "—" : `${value}${unit ?? ""}`;
  return (
    <div className="flex items-center justify-between gap-2 py-1 text-xs">
      <span className="text-gray-500">{label}</span>
      <span className="font-mono font-semibold text-gray-900" dir="ltr">{display}</span>
    </div>
  );
}

function Diagnosis({ text, tone = "neutral" }: { text: string; tone?: "neutral" | "warning" | "danger" | "success" }) {
  const colors = {
    neutral: "bg-gray-50 text-gray-700 border-gray-200",
    warning: "bg-amber-50 text-amber-800 border-amber-200",
    danger: "bg-red-50 text-red-800 border-red-200",
    success: "bg-green-50 text-green-800 border-green-200",
  };
  return (
    <div className={`mt-2 rounded-lg border p-2 text-[11px] leading-relaxed ${colors[tone]}`}>
      {text}
    </div>
  );
}

function getTone(discrepancy: number | null): "neutral" | "warning" | "danger" | "success" {
  if (discrepancy === null) return "neutral";
  if (discrepancy < -5) return "danger";
  if (discrepancy < 0) return "warning";
  if (discrepancy === 0) return "success";
  return "neutral";
}

/** QA-599: Displays the 7 new analyses ported from the Android app. */
export function ExtendedAnalysisResults({ result }: { result: DentalModelAnalysisResultExtended | null }) {
  if (!result) {
    return (
      <div className="rounded-xl border border-dashed border-gray-200 py-8 text-center text-sm text-gray-400">
        لا توجد نتائج تحاليل موسّعة بعد. أدخل القياسات في الأداة الكاملة لعرض النتائج.
      </div>
    );
  }

  const hasAny = result.archPerimeter || result.careys || result.ashleyHowe ||
    result.linderHarth || result.peckPeck || result.korkhaus || result.nanceMixed;

  if (!hasAny) {
    return (
      <div className="rounded-xl border border-dashed border-gray-200 py-8 text-center text-sm text-gray-400">
        لم تُدخل أي قياسات للتحاليل الموسّعة (Arch Perimeter, Carey&apos;s, Ashley Howe, Linder Harth, Peck &amp; Peck, Korkhaus, Nance).
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2 pb-2 border-b border-gray-100">
        <Layers className="w-4 h-4" style={{ color: BLUE }} />
        <h3 className="text-sm font-bold" style={{ color: NAVY }}>التحاليل الموسّعة (QA-599)</h3>
        <span className="text-[10px] text-gray-400">— منقولة من تطبيق تحاليل النماذج</span>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {result.archPerimeter && <ArchPerimeterCard result={result.archPerimeter} title="Arch Perimeter" />}
        {result.careys && <ArchPerimeterCard result={result.careys} title="Carey&apos;s Analysis" />}
        {result.ashleyHowe && <AshleyHoweCard result={result.ashleyHowe} />}
        {result.linderHarth && <LinderHarthCard result={result.linderHarth} />}
        {result.peckPeck && <PeckPeckCard result={result.peckPeck} />}
        {result.korkhaus && <KorkhausCard result={result.korkhaus} />}
        {result.nanceMixed && <NanceMixedCard result={result.nanceMixed} />}
      </div>
    </div>
  );
}

function ArchPerimeterCard({ result, title }: { result: ArchPerimeterResult; title: string }) {
  return (
    <Card icon={Ruler} title={title}>
      <div className="space-y-0.5">
        <Row label="المساحة المتاحة" value={result.spaceAvailable} unit=" مم" />
        <Row label="المساحة المطلوبة" value={result.spaceRequired} unit=" مم" />
        <Row label="التفاوت" value={result.discrepancy} unit=" مم" />
      </div>
      <Diagnosis text={result.diagnosis} tone={getTone(result.discrepancy)} />
      <Diagnosis text={result.comment} tone="neutral" />
    </Card>
  );
}

function AshleyHoweCard({ result }: { result: AshleyHoweResult }) {
  return (
    <Card icon={Gauge} title="Ashley Howe Analysis">
      <div className="space-y-0.5">
        <Row label="نسبة قاعدة الفك" value={result.basalArchPercent} unit="%" />
      </div>
      <Diagnosis text={result.interpretation} tone={result.basalArchPercent < 44 ? "warning" : "success"} />
      <Diagnosis text={result.expansionPossibility} tone="neutral" />
    </Card>
  );
}

function LinderHarthCard({ result }: { result: LinderHarthResult }) {
  return (
    <Card icon={ArrowLeftRight} title="Linder Harth Analysis">
      <div className="space-y-0.5">
        <Row label="مجموع القواطع (SI)" value={result.incisorSum} unit=" مم" />
        <Row label="الضواحك المتوقع (CPV)" value={result.predictedInterpremolarWidth} unit=" مم" />
        <Row label="الأرحاء المتوقع (CMV)" value={result.predictedIntermolarWidth} unit=" مم" />
        <Row label="الضواحك المقاس" value={result.measuredInterpremolarWidth} unit=" مم" />
        <Row label="الأرحاء المقاس" value={result.measuredIntermolarWidth} unit=" مم" />
        <Row label="فرق الضواحك" value={result.premolarDifference} unit=" مم" />
        <Row label="فرق الأرحاء" value={result.molarDifference} unit=" مم" />
      </div>
      <Diagnosis text={result.premolarDiagnosis} tone={getTone(result.premolarDifference)} />
      <Diagnosis text={result.molarDiagnosis} tone={getTone(result.molarDifference)} />
    </Card>
  );
}

function PeckPeckCard({ result }: { result: PeckPeckResult }) {
  return (
    <Card icon={Scissors} title="Peck & Peck Index">
      <div className="space-y-2">
        {result.teeth.map((tooth, i) => (
          <div key={i} className="rounded-lg border border-gray-100 p-2">
            <div className="flex items-center justify-between mb-1">
              <span className="text-[11px] font-semibold text-gray-700">{tooth.toothName}</span>
              <span className="font-mono text-xs font-bold" style={{ color: NAVY }} dir="ltr">
                {tooth.index}%
              </span>
            </div>
            <div className="flex gap-3 text-[10px] text-gray-400">
              <span>MD: {tooth.mdWidth}</span>
              <span>FL: {tooth.flWidth}</span>
            </div>
            <Diagnosis text={tooth.diagnosis} tone={tooth.diagnosis.includes("عريض") ? "warning" : "success"} />
          </div>
        ))}
      </div>
    </Card>
  );
}

function KorkhausCard({ result }: { result: KorkhausResult }) {
  return (
    <Card icon={Activity} title="Korkhaus Analysis">
      <div className="space-y-0.5">
        <Row label="مجموع القواطع (SI)" value={result.incisorSum} unit=" مم" />
        <Row label="طول القوس المتوقع" value={result.predictedArchLength} unit=" مم" />
        <Row label="طول القوس المقاس" value={result.measuredArchLength} unit=" مم" />
        <Row label="الفرق" value={result.difference} unit=" مم" />
      </div>
      <Diagnosis text={result.diagnosis} tone={getTone(result.difference)} />
    </Card>
  );
}

function NanceMixedCard({ result }: { result: NanceMixedResult }) {
  return (
    <Card icon={Layers} title="Nance Mixed Dentition">
      <div className="space-y-0.5">
        <div className="text-[11px] font-semibold text-gray-600 pt-1">الفك العلوي</div>
        <Row label="المساحة المتاحة" value={result.maxAvailable} unit=" مم" />
        <Row label="المساحة المطلوبة" value={result.maxRequired} unit=" مم" />
        <Row label="التفاوت" value={result.maxDiscrepancy} unit=" مم" />
      </div>
      {result.maxDiagnosis && <Diagnosis text={result.maxDiagnosis} tone={getTone(result.maxDiscrepancy)} />}
      <div className="space-y-0.5 mt-2">
        <div className="text-[11px] font-semibold text-gray-600 pt-1">الفك السفلي</div>
        <Row label="المساحة المتاحة" value={result.mandAvailable} unit=" مم" />
        <Row label="المساحة المطلوبة" value={result.mandRequired} unit=" مم" />
        <Row label="التفاوت" value={result.mandDiscrepancy} unit=" مم" />
      </div>
      {result.mandDiagnosis && <Diagnosis text={result.mandDiagnosis} tone={getTone(result.mandDiscrepancy)} />}
    </Card>
  );
}
