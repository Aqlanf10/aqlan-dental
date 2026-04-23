"use client";
import { useState } from "react";
import { Printer, CheckCircle, AlertTriangle, XCircle, ChevronDown, ChevronUp } from "lucide-react";
import type { CephMeasurement, CephDiagnosis } from "@/types/ceph";
import type { MeasurementResult } from "@/lib/cephMath";
import { cn } from "@/lib/utils";

interface Props {
  measurements: (CephMeasurement | MeasurementResult)[];
  diagnosis: CephDiagnosis | null | undefined;
  onDiagnosisChange?: (d: Partial<CephDiagnosis>) => void;
  patientName: string;
  analysisDate: string;
  calibrated: boolean;
}

const STATUS_CONFIG = {
  normal: { icon: CheckCircle, color: 'text-green-500', bg: 'bg-green-50 text-green-700', label: 'طبيعي' },
  mild:   { icon: AlertTriangle, color: 'text-yellow-500', bg: 'bg-yellow-50 text-yellow-700', label: 'خفيف' },
  severe: { icon: XCircle, color: 'text-red-500', bg: 'bg-red-50 text-red-700', label: 'واضح' },
} as const;

const GROUP_LABELS = {
  steiner:  'تحليل ستاينر',
  tweed:    'تحليل تويد',
  mcnamara: 'تحليل ماكنامارا',
} as const;

const SKELETAL_CLASS_AR: Record<string, string> = {
  'Class I':   'الدرجة الأولى — علاقة هيكلية طبيعية',
  'Class II':  'الدرجة الثانية — تقدم في الفك العلوي أو تراجع في الفك السفلي',
  'Class III': 'الدرجة الثالثة — تراجع في الفك العلوي أو تقدم في الفك السفلي',
};

const VERTICAL_AR: Record<string, string> = {
  'Normal':          'طبيعي',
  'Hyperdivergent':  'متشعب (نمط عمودي — وجه طويل)',
  'Hypodivergent':   'مضغوط (نمط أفقي — وجه قصير)',
};

export function AnalysisReport({
  measurements, diagnosis, onDiagnosisChange,
  patientName, analysisDate, calibrated,
}: Props) {
  const [openGroups, setOpenGroups] = useState<Set<string>>(new Set(['steiner', 'tweed']));
  const [finalNotes, setFinalNotes] = useState(diagnosis?.finalDiagnosis ?? "");

  const groups = (Object.keys(GROUP_LABELS) as (keyof typeof GROUP_LABELS)[]);
  const byGroup = (group: string) => measurements.filter(m => m.analysisGroup === group);

  const toggleGroup = (g: string) => {
    setOpenGroups(prev => {
      const next = new Set(prev);
      next.has(g) ? next.delete(g) : next.add(g);
      return next;
    });
  };

  const handlePrint = () => {
    window.print();
  };

  if (!measurements.length) {
    return (
      <div className="text-center py-10 text-gray-400 text-sm">
        <p>لا توجد قياسات محسوبة</p>
        <p className="text-xs mt-1 text-gray-300">ضع النقاط على الصورة ثم اضغط "احسب القياسات"</p>
      </div>
    );
  }

  const abnormalCount = measurements.filter(m => m.status !== 'normal' && m.value !== null).length;

  return (
    <div className="space-y-4 print:space-y-3">
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
      <div className={cn(
        "rounded-xl p-4 border",
        abnormalCount === 0 ? "bg-green-50 border-green-200" :
        abnormalCount <= 3 ? "bg-yellow-50 border-yellow-200" :
        "bg-red-50 border-red-200"
      )}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-900">
              {abnormalCount === 0 ? "جميع القياسات ضمن الحدود الطبيعية" :
               `${abnormalCount} قياس خارج النطاق الطبيعي`}
            </p>
            {diagnosis?.skeletalClass && (
              <p className="text-xs text-gray-600 mt-1">{SKELETAL_CLASS_AR[diagnosis.skeletalClass] ?? diagnosis.skeletalClass}</p>
            )}
            {diagnosis?.verticalPattern && (
              <p className="text-xs text-gray-600">النمط الرأسي: {VERTICAL_AR[diagnosis.verticalPattern] ?? diagnosis.verticalPattern}</p>
            )}
          </div>
          <button
            onClick={handlePrint}
            className="print:hidden flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 hover:bg-white transition"
          >
            <Printer className="w-3.5 h-3.5" />
            طباعة
          </button>
        </div>
      </div>

      {/* Calibration note */}
      {!calibrated && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-2.5 text-xs text-blue-700">
          القياسات الخطية (mm) تتطلب معايرة الصورة — أدخل معامل التحجيم للحصول على قياسات بالملليمتر
        </div>
      )}

      {/* Measurement groups */}
      {groups.map(group => {
        const rows = byGroup(group);
        if (!rows.length) return null;
        const isOpen = openGroups.has(group);
        const groupAbnormal = rows.filter(r => r.status !== 'normal' && r.value !== null).length;

        return (
          <div key={group} className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden print:shadow-none print:border-gray-300">
            <button
              className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 border-b border-gray-100 hover:bg-gray-100 transition print:pointer-events-none"
              onClick={() => toggleGroup(group)}
            >
              <div className="flex items-center gap-2">
                <span className="font-bold text-sm text-gray-800">{GROUP_LABELS[group]}</span>
                {groupAbnormal > 0 && (
                  <span className="text-xs bg-red-100 text-red-600 px-1.5 py-0.5 rounded-full font-medium">
                    {groupAbnormal} انحراف
                  </span>
                )}
              </div>
              <span className="print:hidden">
                {isOpen ? <ChevronUp className="w-4 h-4 text-gray-400" /> : <ChevronDown className="w-4 h-4 text-gray-400" />}
              </span>
            </button>

            {(isOpen || typeof window === 'undefined') && (
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead className="bg-gray-50/50 border-b border-gray-100">
                    <tr>
                      {["القياس", "القيمة", "المعيار", "الانحراف", "التقييم"].map(h => (
                        <th key={h} className="text-start px-3 py-2 text-gray-500 font-semibold">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {rows.map(m => {
                      const cfg = STATUS_CONFIG[m.status];
                      const StatusIcon = cfg.icon;
                      return (
                        <tr key={m.name} className={cn(
                          "hover:bg-gray-50/50 transition",
                          m.status === 'severe' ? "bg-red-50/30" : m.status === 'mild' ? "bg-yellow-50/20" : ""
                        )}>
                          <td className="px-3 py-2.5">
                            <div className="font-semibold text-gray-800">{m.nameAr}</div>
                            {m.interpretationAr && m.status !== 'normal' && (
                              <div className="text-gray-400 text-[10px] leading-tight mt-0.5 max-w-[150px]">{m.interpretationAr}</div>
                            )}
                          </td>
                          <td className="px-3 py-2.5 font-mono font-bold text-gray-900">
                            {m.value !== null ? `${m.value}${m.unit}` : (
                              <span className="text-gray-300 font-normal">—</span>
                            )}
                          </td>
                          <td className="px-3 py-2.5 font-mono text-gray-500">
                            {m.normal}{m.unit} <span className="text-gray-300">±{m.stdDev}</span>
                          </td>
                          <td className="px-3 py-2.5 font-mono">
                            {m.deviation !== null ? (
                              <span className={m.deviation > 0 ? "text-orange-600" : m.deviation < 0 ? "text-blue-600" : "text-gray-400"}>
                                {m.deviation > 0 ? '+' : ''}{m.deviation}{m.unit}
                              </span>
                            ) : <span className="text-gray-300">—</span>}
                          </td>
                          <td className="px-3 py-2.5">
                            <div className="flex items-center gap-1">
                              <StatusIcon className={cn("w-3.5 h-3.5", cfg.color)} />
                              <span className={cn("text-[10px] px-1.5 py-0.5 rounded-full font-medium", cfg.bg)}>
                                {cfg.label}
                              </span>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        );
      })}

      {/* Diagnosis section */}
      {diagnosis && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3 print:shadow-none">
          <h3 className="font-bold text-sm text-gray-900">التشخيص السريري</h3>

          {diagnosis.aiRecommendation && (
            <div className="bg-purple-50 border border-purple-200 rounded-lg p-3">
              <p className="text-xs font-semibold text-purple-800 mb-1">توصية الذكاء الاصطناعي:</p>
              <p className="text-xs text-purple-700 leading-relaxed">{diagnosis.aiRecommendation}</p>
            </div>
          )}

          {onDiagnosisChange && (
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1.5">
                ملاحظات الطبيب والتشخيص النهائي
              </label>
              <textarea
                value={finalNotes}
                onChange={(e) => setFinalNotes(e.target.value)}
                onBlur={() => onDiagnosisChange({ finalDiagnosis: finalNotes })}
                rows={3}
                className="w-full text-xs px-3 py-2 rounded-lg border border-gray-300 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
                placeholder="أكتب تشخيصك النهائي وخطة العلاج..."
              />
              <div className="flex items-center gap-2 mt-2">
                <input
                  type="checkbox"
                  id="approved"
                  checked={diagnosis.doctorApproved}
                  onChange={(e) => onDiagnosisChange({ doctorApproved: e.target.checked })}
                  className="w-3.5 h-3.5 accent-clinic-teal"
                />
                <label htmlFor="approved" className="text-xs text-gray-600">موافقة الطبيب على التشخيص</label>
              </div>
            </div>
          )}

          {diagnosis.finalDiagnosis && (
            <div className="bg-gray-50 rounded-lg p-3 text-xs text-gray-700 leading-relaxed">
              {diagnosis.finalDiagnosis}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
