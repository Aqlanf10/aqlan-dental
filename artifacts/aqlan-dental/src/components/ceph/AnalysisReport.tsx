import { printScreen } from "@/lib/printUtils";
import { useState } from "react";
import { Printer, CheckCircle, AlertTriangle, XCircle, ArrowUp, ArrowDown, Loader2, Info } from "lucide-react";
import type { CephMeasurement, CephDiagnosis, CephAiDraftResponse, MeasurementGroup } from "@/types/ceph";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { useClinicBranding } from "@/hooks/useClinicBranding";

interface Props {
  measurements: CephMeasurement[];
  diagnosis: CephDiagnosis | null | undefined;
  onDiagnosisChange?: (d: Partial<CephDiagnosis>) => void;
  patientName: string;
  analysisDate: string;
  calibrated: boolean;
  defaultGroup?: MeasurementGroup;
  /** Enables the C-D AI draft assistant button (requires the diagnosis edit mode). */
  analysisId?: string;
}

/** Fallback only — the server response always carries the authoritative disclaimer. */
const AI_DRAFT_DISCLAIMER_FALLBACK =
  "هذه مسودة مولّدة بالذكاء الاصطناعي — تتطلب مراجعة واعتماد أخصائي التقويم قبل أي استخدام سريري.";

const SCIENTIST_TABS: { key: MeasurementGroup; label: string; labelFull: string }[] = [
  { key: 'steiner',  label: 'ستاينر',    labelFull: 'تحليل ستاينر' },
  { key: 'tweed',    label: 'تويد',      labelFull: 'تحليل تويد' },
  { key: 'mcnamara', label: 'ماكنامارا', labelFull: 'تحليل ماكنامارا' },
  { key: 'ricketts', label: 'ريكتس',    labelFull: 'تحليل ريكتس' },
  { key: 'downs',    label: 'داونز',     labelFull: 'تحليل داونز' },
  { key: 'jarabak',  label: 'جاراباك',   labelFull: 'تحليل جاراباك' },
  { key: 'wits',     label: 'وتس',       labelFull: 'تحليل وتس' },
  { key: 'pa',       label: 'PA أمامي',  labelFull: 'تحليل عدم التناظر والميل الأمامي' },
];

const SEVERITY_CFG = {
  normal: { icon: CheckCircle,   color: 'text-green-500',  bg: 'bg-green-50 text-green-700',  label: 'طبيعي' },
  mild:   { icon: AlertTriangle, color: 'text-amber-500',  bg: 'bg-amber-50 text-amber-700',  label: 'خفيف' },
  severe: { icon: XCircle,       color: 'text-red-500',    bg: 'bg-red-50 text-red-700',       label: 'واضح' },
  unclassified: { icon: Info, color: 'text-gray-500', bg: 'bg-gray-100 text-gray-700', label: 'وصفي' },
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
  defaultGroup = 'steiner', analysisId,
}: Props) {
  const [activeGroup, setActiveGroup] = useState<MeasurementGroup>(defaultGroup);
  const [finalNotes, setFinalNotes]   = useState(diagnosis?.finalDiagnosis ?? "");
  // MS-TASK-006: printed-report identity comes from settings, not hardcoded.
  const branding = useClinicBranding();

  // ── C-D AI draft assistant — draft only, NEVER auto-saved ────────────────
  const [aiDraft, setAiDraft]     = useState<CephAiDraftResponse | null>(null);
  const [aiLoading, setAiLoading] = useState(false);
  const [aiError, setAiError]     = useState<string | null>(null);

  const generateAiDraft = async () => {
    if (!analysisId || aiLoading) return;
    setAiLoading(true);
    setAiError(null);
    try {
      const { data } = await api.post<CephAiDraftResponse>(`/api/ceph/${analysisId}/ai/draft-diagnosis`);
      setAiDraft(data);
    } catch (err) {
      // The server always sends an honest Arabic reason (403 disabled/unconfigured,
      // 429 monthly limit, 502 upstream) in the `message` field.
      const resp = err as { response?: { data?: { message?: string } } };
      setAiError(resp.response?.data?.message ?? "تعذر توليد المسودة — حاول لاحقًا");
    } finally {
      setAiLoading(false);
    }
  };

  const grouped = measurements.filter(m => m.analysisGroup === activeGroup);
  const hasMeas = measurements.length > 0;
  const totalAbnormal = measurements.filter(m => m.severity !== 'normal' && m.severity !== 'unclassified' && m.value !== null).length;
  const groupAbnormal = grouped.filter(m => m.severity !== 'normal' && m.severity !== 'unclassified' && m.value !== null).length;

  const availableTabs = SCIENTIST_TABS.filter(t =>
    !hasMeas || measurements.some(m => m.analysisGroup === t.key)
  );

  return (
    <div className="flex flex-col h-full">
      {/* Print header */}
      <div className="hidden print:block text-center border-b-2 pb-3 mb-4">
        <h2 className="text-lg font-bold">{branding.clinicName}</h2>
        <p className="text-sm">تقرير التحليل السيفالومتري</p>
        <div className="flex justify-between text-xs mt-2">
          <span>المريض: {patientName}</span>
          <span>التاريخ: {analysisDate}</span>
        </div>
      </div>

      {/* Summary banner */}
      {hasMeas && (
        <div className={cn(
          "flex-shrink-0 rounded-xl p-2.5 mb-2 border text-xs",
          totalAbnormal === 0 ? "bg-green-50 border-green-200 text-green-800" :
          totalAbnormal <= 3  ? "bg-amber-50 border-amber-200 text-amber-800" :
                                "bg-red-50 border-red-200 text-red-800"
        )}>
          <div className="flex items-center justify-between">
            <span className="font-bold text-[11px]">
              {totalAbnormal === 0 ? "جميع القياسات ضمن الحدود الطبيعية" :
               `${totalAbnormal} قياس خارج النطاق الطبيعي`}
            </span>
            <button onClick={() => printScreen()}
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
        <div className="flex-shrink-0 bg-blue-50 border border-blue-200 rounded-lg p-2 text-[10px] text-blue-700 mb-2">
          القياسات الخطية (mm) تتطلب معايرة — أدخل معامل التحجيم أسفل الصورة
        </div>
      )}

      {/* Scientist selector tabs */}
      {hasMeas && (
        <div className="flex-shrink-0 flex gap-1 mb-2 flex-wrap">
          {availableTabs.map(t => {
            const cnt = measurements.filter(m => m.analysisGroup === t.key && m.severity !== 'normal' && m.severity !== 'unclassified' && m.value !== null).length;
            return (
              <button key={t.key} onClick={() => setActiveGroup(t.key)}
                className={cn(
                  "flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-semibold border transition",
                  activeGroup === t.key
                    ? "bg-clinic-blue text-white border-clinic-blue shadow-sm"
                    : "bg-white text-gray-500 border-gray-200 hover:border-clinic-blue hover:text-clinic-blue"
                )}
              >
                {t.label}
                {cnt > 0 && (
                  <span className={cn(
                    "text-[9px] rounded-full px-1 min-w-[14px] text-center font-bold leading-tight",
                    activeGroup === t.key ? "bg-white/25 text-white" : "bg-red-100 text-red-600"
                  )}>{cnt}</span>
                )}
              </button>
            );
          })}
        </div>
      )}

      {/* Content scroll area */}
      <div className="flex-1 overflow-y-auto min-h-0">
        {/* No measurements yet */}
        {!hasMeas && !diagnosis && (
          <div className="text-center py-10 text-gray-400">
            <p className="text-xs font-medium">لا توجد قياسات محسوبة</p>
            <p className="text-[10px] mt-1 text-gray-300">ضع النقاط ثم اضغط &quot;احسب القياسات&quot;</p>
          </div>
        )}

        {/* Measurement rows for selected group */}
        {hasMeas && (
          <div className="space-y-3">
            <div className="flex items-center gap-2">
              <span className="text-[11px] font-bold text-gray-700">
                {SCIENTIST_TABS.find(t => t.key === activeGroup)?.labelFull}
              </span>
              {groupAbnormal > 0 && (
                <span className="text-[9px] bg-red-100 text-red-600 px-1.5 py-0.5 rounded-full font-bold">
                  {groupAbnormal} انحراف
                </span>
              )}
            </div>

            {grouped.length === 0 ? (
              <p className="text-[11px] text-gray-300 text-center py-6">
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
                    <div key={m.name} className={cn(
                      "rounded-lg border p-2.5",
                      m.severity === 'severe' ? "bg-red-50/60 border-red-200" :
                      m.severity === 'mild'   ? "bg-amber-50/50 border-amber-100" :
                      "bg-white border-gray-100"
                    )}>
                      {/* Top row */}
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <span className="text-[11px] font-bold text-gray-800">{m.nameAr}</span>
                            <span className={cn("text-[9px] px-1.5 py-0.5 rounded-full font-medium flex items-center gap-0.5", cfg.bg)}>
                              <Icon className="w-2.5 h-2.5" />{cfg.label}
                            </span>
                          </div>
                          {m.severity !== 'normal' && m.interpretationAr && (
                            <p className="text-[10px] text-gray-500 mt-0.5 leading-tight">{m.interpretationAr}</p>
                          )}
                        </div>

                        {/* Value */}
                        <div className="flex-shrink-0 text-end">
                          <div className="text-sm font-bold font-mono text-gray-900 leading-none">
                            {m.value !== null
                              ? `${m.value}${m.unit}`
                              : <span className="text-gray-300 text-xs font-normal">—</span>}
                          </div>
                          <div className="text-[9px] text-gray-400 font-mono mt-0.5">
                            {m.normal === null ? "وصفي بلا معيار عام" : <>{m.normal}{m.unit} <span className="text-gray-300">±{m.stdDev}</span></>}
                          </div>
                        </div>
                      </div>

                      {/* Deviation bar */}
                      {m.deviation !== null && m.value !== null && m.stdDev !== null && m.stdDev > 0 && (
                        <div className="mt-2 flex items-center gap-2">
                          <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-visible relative">
                            <div className="absolute inset-y-0 left-[20%] right-[20%] bg-green-100 rounded-full" />
                            <div
                              className={cn(
                                "absolute w-2.5 h-2.5 rounded-full -top-[2px] border-2 border-white shadow-sm",
                                m.severity === 'severe' ? 'bg-red-500' :
                                m.severity === 'mild'   ? 'bg-amber-500' : 'bg-green-500'
                              )}
                              style={{
                                left: `calc(${Math.max(2, Math.min(94, 50 + (m.deviation / (m.stdDev * 4)) * 50))}% - 5px)`
                              }}
                            />
                          </div>
                          <div className={cn(
                            "text-[10px] font-mono flex items-center gap-0.5 flex-shrink-0",
                            above ? 'text-orange-600' : below ? 'text-blue-600' : 'text-gray-400'
                          )}>
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

        {/* Diagnosis section — always shown when diagnosis exists */}
        {diagnosis && (
          <div className={cn("pt-3 border-t border-gray-100 space-y-2.5", hasMeas && "mt-4")}>
            <h3 className="font-bold text-[11px] text-gray-900">التشخيص السريري</h3>

            {diagnosis.aiRecommendation && (
              <div className="bg-purple-50 border border-purple-200 rounded-lg p-2.5">
                <p className="text-[9px] font-bold text-purple-800 mb-1 uppercase tracking-wide">توصية النظام</p>
                <p className="text-[10px] text-purple-700 leading-relaxed whitespace-pre-line">
                  {diagnosis.aiRecommendation}
                </p>
              </div>
            )}

            {diagnosis.incisorInclination && (
              <div className="text-[10px] text-gray-600 bg-gray-50 rounded-lg p-2">
                <span className="font-semibold">ميلان القواطع: </span>
                {diagnosis.incisorInclination}
              </div>
            )}

            {onDiagnosisChange && (
              <div className="space-y-2">
                {/* C-D AI draft assistant — the draft is NEVER saved automatically */}
                {analysisId && (
                  <div className="print:hidden space-y-1.5">
                    <button
                      type="button"
                      onClick={generateAiDraft}
                      disabled={aiLoading}
                      className="flex items-center gap-1.5 text-[10px] font-semibold px-2.5 py-1.5 rounded-lg border border-amber-300 bg-amber-50 text-amber-800 hover:bg-amber-100 disabled:opacity-60 transition"
                    >
                      {aiLoading && <Loader2 className="w-3 h-3 animate-spin" />}
                      {aiLoading ? "جارٍ توليد المسودة..." : "مسودة تشخيص بالذكاء الاصطناعي"}
                    </button>

                    {aiError && (
                      <p className="text-[10px] text-red-600 bg-red-50 border border-red-100 rounded-lg px-2.5 py-1.5">
                        {aiError}
                      </p>
                    )}

                    {aiDraft && (
                      <div className="rounded-lg border border-amber-300 bg-amber-50 p-2.5 space-y-2">
                        <p className="text-[9px] font-bold text-amber-800 flex items-start gap-1">
                          <AlertTriangle className="w-3 h-3 mt-px flex-shrink-0" />
                          {aiDraft.disclaimer || AI_DRAFT_DISCLAIMER_FALLBACK}
                        </p>
                        <p className="text-[10px] text-gray-800 leading-relaxed whitespace-pre-line">
                          {aiDraft.draft}
                        </p>
                        <div className="flex items-center justify-between gap-2">
                          <span className="text-[8px] text-amber-700 font-mono" dir="ltr">{aiDraft.modelId}</span>
                          <button
                            type="button"
                            onClick={() => setFinalNotes(aiDraft.draft)}
                            className="text-[9px] font-semibold px-2 py-1 rounded border border-amber-400 bg-white text-amber-800 hover:bg-amber-100 transition"
                            title="ينسخ المسودة إلى حقل التشخيص فقط — لا يتم الحفظ إلا بعد مراجعتك"
                          >
                            نسخ إلى حقل التشخيص (بدون حفظ)
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                <label className="block text-[10px] font-semibold text-gray-600">
                  ملاحظات الطبيب والتشخيص النهائي
                </label>
                <textarea
                  value={finalNotes}
                  onChange={(e) => setFinalNotes(e.target.value)}
                  onBlur={() => onDiagnosisChange({ finalDiagnosis: finalNotes })}
                  rows={4}
                  className="w-full text-[11px] px-2.5 py-2 rounded-lg border border-gray-300 focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none"
                  placeholder="أكتب تشخيصك النهائي وخطة العلاج..."
                />
                <label className="flex items-center gap-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={diagnosis.doctorApproved}
                    onChange={(e) => onDiagnosisChange({ doctorApproved: e.target.checked })}
                    className="w-3.5 h-3.5 accent-clinic-blue"
                  />
                  <span className="text-[10px] text-gray-600">موافقة الطبيب على التشخيص</span>
                </label>
              </div>
            )}

            {diagnosis.finalDiagnosis && !onDiagnosisChange && (
              <div className="bg-gray-50 rounded-lg p-2.5 text-[10px] text-gray-700 leading-relaxed">
                {diagnosis.finalDiagnosis}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
