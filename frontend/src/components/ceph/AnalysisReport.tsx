"use client";
import { useState } from "react";
import { Printer, CheckCircle, AlertTriangle, XCircle, ArrowUp, ArrowDown } from "lucide-react";
import type { CephMeasurement, CephDiagnosis, MeasurementGroup } from "@/types/ceph";
import { cn } from "@/lib/utils";

interface Props {
  measurements: CephMeasurement[];
  diagnosis: CephDiagnosis | null | undefined;
  onDiagnosisChange?: (d: Partial<CephDiagnosis>) => void;
  patientName: string;
  analysisDate: string;
  calibrated: boolean;
  defaultGroup?: MeasurementGroup;
}

const SCIENTIST_TABS: { key: MeasurementGroup; label: string; labelFull: string }[] = [
  { key: 'steiner',    label: 'ستاينر',        labelFull: 'تحليل ستاينر' },
  { key: 'tweed',      label: 'تويد',          labelFull: 'تحليل تويد' },
  { key: 'mcnamara',   label: 'ماكنامارا',     labelFull: 'تحليل ماكنامارا' },
  { key: 'ricketts',   label: 'ريكتس',        labelFull: 'تحليل ريكتس' },
  { key: 'downs',      label: 'داونز',         labelFull: 'تحليل داونز' },
  { key: 'wits',       label: 'وتس',           labelFull: 'تحليل وتس' },
  { key: 'jarabak',    label: 'جاراباك',       labelFull: 'تحليل جاراباك' },
  { key: 'softtissue', label: 'الأنسجة الرخوة', labelFull: 'تحليل الأنسجة الرخوة' },
];

// Badge: padding 2px 10px, rounded-full, bg {color}18, text {color}
const SEVERITY_CFG = {
  normal: { icon: CheckCircle,   color: "#22c55e",  bg: "#22c55e18",  label: "طبيعي" },
  mild:   { icon: AlertTriangle, color: "#f59e0b",  bg: "#f59e0b18",  label: "خفيف" },
  severe: { icon: XCircle,       color: "#ef4444",  bg: "#ef444418",  label: "واضح" },
} as const;

const SKELETAL_CLASS_AR: Record<string, string> = {
  'Class I':   'الدرجة الأولى — علاقة هيكلية طبيعية',
  'Class II':  'الدرجة الثانية — تقدم في الفك العلوي أو تراجع في الفك السفلي',
  'Class III': 'الدرجة الثالثة — تراجع في الفك العلوي أو تقدم في الفك السفلي',
};

const VERTICAL_AR: Record<string, string> = {
  'Normal': 'طبيعي', 'Normodivergent': 'طبيعي',
  'Hyperdivergent': 'متشعب (وجه طويل)', 'Hypodivergent': 'مضغوط (وجه قصير)',
};

export function AnalysisReport({
  measurements, diagnosis, onDiagnosisChange,
  patientName, analysisDate, calibrated,
  defaultGroup = 'steiner',
}: Props) {
  const [activeGroup, setActiveGroup] = useState<MeasurementGroup>(defaultGroup);
  const [finalNotes, setFinalNotes]   = useState(diagnosis?.finalDiagnosis ?? "");

  const grouped = measurements.filter(m => m.analysisGroup === activeGroup);
  const hasMeas = measurements.length > 0;
  const totalAbnormal = measurements.filter(m => m.severity !== 'normal' && m.value !== null).length;
  const groupAbnormal = grouped.filter(m => m.severity !== 'normal' && m.value !== null).length;

  const availableTabs = SCIENTIST_TABS.filter(t =>
    !hasMeas || measurements.some(m => m.analysisGroup === t.key)
  );

  return (
    <div className="flex flex-col h-full" dir="rtl">
      {/* Print header */}
      <div className="hidden print:block text-center border-b-2 pb-3 mb-4">
        <h2 className="text-lg font-bold">مركز د. عقلان الكامل لطب وتقويم الأسنان</h2>
        <p className="text-sm">تقرير التحليل السيفالومتري</p>
        <div className="flex justify-between text-xs mt-2">
          <span>المريض: {patientName}</span>
          <span>التاريخ: {analysisDate}</span>
        </div>
      </div>

      {/* Summary banner */}
      {hasMeas && (
        <div
          className="flex-shrink-0 rounded-xl p-2.5 mb-2 border text-xs"
          style={{
            backgroundColor: totalAbnormal === 0 ? "#22c55e10" : totalAbnormal <= 3 ? "#f59e0b10" : "#ef444410",
            borderColor: totalAbnormal === 0 ? "#22c55e30" : totalAbnormal <= 3 ? "#f59e0b30" : "#ef444430",
            color: totalAbnormal === 0 ? "#22c55e" : totalAbnormal <= 3 ? "#f59e0b" : "#ef4444",
          }}
        >
          <div className="flex items-center justify-between">
            <span className="font-bold text-[11px]">
              {totalAbnormal === 0 ? "جميع القياسات ضمن الحدود الطبيعية" :
               `${totalAbnormal} قياس خارج النطاق الطبيعي`}
            </span>
            <button onClick={() => window.print()}
              className="print:hidden flex items-center gap-1 text-[9px] px-1.5 py-0.5 rounded border border-current hover:opacity-70 transition">
              <Printer className="w-2.5 h-2.5" />طباعة
            </button>
          </div>
          {diagnosis?.skeletalClass && (
            <p className="mt-0.5 text-[10px]">{SKELETAL_CLASS_AR[diagnosis.skeletalClass] ?? diagnosis.skeletalClass}</p>
          )}
          {diagnosis?.verticalPattern && (
            <p className="text-[10px]">النمط الرأسي: {VERTICAL_AR[diagnosis.verticalPattern] ?? diagnosis.verticalPattern}</p>
          )}
        </div>
      )}

      {/* Calibration note */}
      {!calibrated && (
        <div className="flex-shrink-0 rounded-xl p-2 text-[10px] mb-2"
          style={{ backgroundColor: "#3d7ab510", border: "1px solid #3d7ab530", color: "#3d7ab5" }}>
          القياسات الخطية (mm) تتطلب معايرة — أدخل معامل التحجيم أسفل الصورة
        </div>
      )}

      {/* Scientist selector tabs */}
      {hasMeas && (
        <div className="flex-shrink-0 flex gap-1 mb-2 flex-wrap">
          {availableTabs.map(t => {
            const cnt = measurements.filter(m => m.analysisGroup === t.key && m.severity !== 'normal' && m.value !== null).length;
            const isActive = activeGroup === t.key;
            return (
              <button key={t.key} onClick={() => setActiveGroup(t.key)}
                className="flex items-center gap-1 px-2.5 py-1 rounded-xl text-[11px] border transition"
                style={isActive
                  ? { backgroundColor: "#3d7ab5", color: "#ffffff", borderColor: "#3d7ab5", fontWeight: 700 }
                  : { backgroundColor: "#ffffff", color: "#64748b", borderColor: "#e8f0f9" }
                }
              >
                {t.label}
                {cnt > 0 && (
                  <span
                    className="text-[9px] rounded-full min-w-[14px] text-center font-bold leading-tight"
                    style={{
                      padding: "0px 4px",
                      backgroundColor: isActive ? "#ffffff30" : "#ef444418",
                      color: isActive ? "#ffffff" : "#ef4444",
                    }}
                  >{cnt}</span>
                )}
              </button>
            );
          })}
        </div>
      )}

      {/* Content scroll area */}
      <div className="flex-1 overflow-y-auto min-h-0">
        {!hasMeas && !diagnosis && (
          <div className="text-center py-10" style={{ color: "#94a3b8" }}>
            <p className="text-xs font-medium">لا توجد قياسات محسوبة</p>
            <p className="text-[10px] mt-1">ضع النقاط ثم اضغط &quot;احسب القياسات&quot;</p>
          </div>
        )}

        {hasMeas && (
          <div className="space-y-3">
            <div className="flex items-center gap-2">
              <span className="text-[11px] font-bold" style={{ color: "#0d2137" }}>
                {SCIENTIST_TABS.find(t => t.key === activeGroup)?.labelFull}
              </span>
              {groupAbnormal > 0 && (
                <span
                  className="text-[9px] rounded-full font-bold"
                  style={{ padding: "2px 10px", backgroundColor: "#ef444418", color: "#ef4444" }}
                >
                  {groupAbnormal} انحراف
                </span>
              )}
            </div>

            {grouped.length === 0 ? (
              <p className="text-[11px] text-center py-6" style={{ color: "#94a3b8" }}>
                لا تتوفر قياسات {SCIENTIST_TABS.find(t => t.key === activeGroup)?.labelFull}
              </p>
            ) : (
              <div className="space-y-1.5">
                {grouped.map(m => {
                  const cfg = SEVERITY_CFG[m.severity];
                  const Icon = cfg.icon;
                  const above = m.direction === 'above';
                  const below = m.direction === 'below';

                  return (
                    <div key={m.name}
                      className="rounded-xl border p-2.5"
                      style={{
                        backgroundColor: m.severity === 'severe' ? "#ef444408" : m.severity === 'mild' ? "#f59e0b08" : "#ffffff",
                        borderColor: m.severity === 'severe' ? "#ef444430" : m.severity === 'mild' ? "#f59e0b30" : "#e8f0f9",
                      }}
                    >
                      {/* Top row */}
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <span className="text-[11px] font-bold" style={{ color: "#0d2137" }}>{m.nameAr}</span>
                            <span
                              className="text-[9px] rounded-full font-medium flex items-center gap-0.5"
                              style={{ padding: "2px 10px", backgroundColor: cfg.bg, color: cfg.color }}
                            >
                              <Icon className="w-2.5 h-2.5" />{cfg.label}
                            </span>
                          </div>
                          {m.severity !== 'normal' && m.interpretationAr && (
                            <p className="text-[10px] mt-0.5 leading-tight" style={{ color: "#64748b" }}>{m.interpretationAr}</p>
                          )}
                        </div>

                        {/* Value */}
                        <div className="flex-shrink-0 text-end">
                          <div className="text-sm font-bold font-mono leading-none" style={{ color: "#0d2137" }}>
                            {m.value !== null
                              ? `${m.value}${m.unit}`
                              : <span style={{ color: "#94a3b8" }}>—</span>}
                          </div>
                          <div className="text-[9px] font-mono mt-0.5" style={{ color: "#94a3b8" }}>
                            {m.normal}{m.unit} <span style={{ color: "#dce8f5" }}>±{m.stdDev}</span>
                          </div>
                        </div>
                      </div>

                      {/* Deviation bar */}
                      {m.deviation !== null && m.value !== null && (
                        <div className="mt-2 flex items-center gap-2">
                          {/* Progress bar: track bg #f1f5f9, fill gradient #3d7ab5 to #2d5e8e */}
                          <div className="flex-1 h-1.5 rounded-full overflow-visible relative" style={{ backgroundColor: "#f1f5f9" }}>
                            <div className="absolute inset-y-0 left-[20%] right-[20%] rounded-full" style={{ backgroundColor: "#22c55e18" }} />
                            <div
                              className="absolute w-2.5 h-2.5 rounded-full -top-[2px] border-2 border-white shadow-sm"
                              style={{
                                backgroundColor: m.severity === 'severe' ? '#ef4444' : m.severity === 'mild' ? '#f59e0b' : '#22c55e',
                                left: `calc(${Math.max(2, Math.min(94, 50 + (m.deviation / (m.stdDev * 4)) * 50))}% - 5px)`,
                              }}
                            />
                          </div>
                          <div className="text-[10px] font-mono flex items-center gap-0.5 flex-shrink-0"
                            style={{ color: above ? '#f5922e' : below ? '#3d7ab5' : '#94a3b8' }}
                          >
                            {above && <ArrowUp className="w-2.5 h-2.5" />}
                            {below && <ArrowDown className="w-2.5 h-2.5" />}
                            {m.deviation > 0 ? '+' : ''}{m.deviation}{m.unit}
                          </div>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {/* Diagnosis section */}
        {diagnosis && (
          <div className={cn("pt-3 space-y-2.5", hasMeas && "mt-4")} style={{ borderTop: "1px solid #e8f0f9" }}>
            <h3 className="font-bold text-[11px]" style={{ color: "#0d2137" }}>التشخيص السريري</h3>

            {diagnosis.aiRecommendation && (
              <div className="rounded-xl p-2.5" style={{ backgroundColor: "#f0f5fb", border: "1px solid #3d7ab520" }}>
                <p className="text-[9px] font-bold uppercase tracking-wide mb-1" style={{ color: "#3d7ab5" }}>توصية النظام</p>
                <p className="text-[10px] leading-relaxed whitespace-pre-line" style={{ color: "#0d2137" }}>
                  {diagnosis.aiRecommendation}
                </p>
              </div>
            )}

            {diagnosis.incisorInclination && (
              <div className="text-[10px] rounded-xl p-2" style={{ backgroundColor: "#f7fafd", color: "#0d2137" }}>
                <span className="font-semibold">ميلان القواطع: </span>
                {diagnosis.incisorInclination}
              </div>
            )}

            {onDiagnosisChange && (
              <div className="space-y-2">
                <label className="block text-[10px] font-semibold" style={{ color: "#64748b" }}>
                  ملاحظات الطبيب والتشخيص النهائي
                </label>
                <textarea
                  value={finalNotes}
                  onChange={(e) => setFinalNotes(e.target.value)}
                  onBlur={() => onDiagnosisChange({ finalDiagnosis: finalNotes })}
                  rows={4}
                  className="w-full text-[11px] px-2.5 py-2 rounded-xl border focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] resize-none"
                  style={{ borderColor: "#dce8f5" }}
                  placeholder="أكتب تشخيصك النهائي وخطة العلاج..."
                />
                <label className="flex items-center gap-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={diagnosis.doctorApproved}
                    onChange={(e) => onDiagnosisChange({ doctorApproved: e.target.checked })}
                    className="w-3.5 h-3.5 accent-[#3d7ab5]"
                  />
                  <span className="text-[10px]" style={{ color: "#64748b" }}>موافقة الطبيب على التشخيص</span>
                </label>
              </div>
            )}

            {diagnosis.finalDiagnosis && !onDiagnosisChange && (
              <div className="rounded-xl p-2.5 text-[10px] leading-relaxed" style={{ backgroundColor: "#f7fafd", color: "#0d2137" }}>
                {diagnosis.finalDiagnosis}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
