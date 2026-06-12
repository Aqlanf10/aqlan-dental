/**
 * Panel components for Doctor Clinic Workspace.
 * FluentPanel, StartVisit, Examination, PricedProcedures, TreatmentPlan,
 * OrthoFollowUp, ImagesRadiographs, LabOrder, Prescription,
 * FollowUpSuggest, Handoff.
 */

"use client";

import { useState, useMemo } from "react";
import {
  Play, ListChecks,
  Send, Loader2, AlertCircle, Check,
  Plus, Minus, Trash2, Stethoscope,
  MessageSquare, Camera, Upload,
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import {
  NAVY, BLUE, ORANGE, inputCls, fmtRial,
} from "../../daily-operations/_lib/constants";
import type { DoctorPatientItem, ServiceWithPrice } from "../_lib/hooks";

/* ═══════════════════════════════════════════════════════════════════════════
   FluentPanel — simple wrapper for inline panel content
   ═══════════════════════════════════════════════════════════════════════════ */
export function FluentPanel({ children }: { children: React.ReactNode }) {
  return <div className="p-5">{children}</div>;
}

/* ═══════════════════════════════════════════════════════════════════════════
   Medical Alerts display helper
   ═══════════════════════════════════════════════════════════════════════════ */
function MedicalAlertsBadge({ alerts }: { alerts: { type: string; severity: string; label?: string }[] }) {
  if (!alerts || alerts.length === 0) return null;
  return (
    <div className="p-3 rounded-xl mb-3" style={{ background: "#fef2f2", border: "1px solid #fecaca" }}>
      <div className="flex items-center gap-1.5 mb-1.5">
        <AlertCircle className="w-4 h-4" style={{ color: "#dc2626" }} />
        <span className="text-xs font-bold" style={{ color: "#dc2626" }}>تنبيهات طبية</span>
      </div>
      <div className="flex gap-1.5 flex-wrap">
        {alerts.map((alert, i) => (
          <span key={i} className="text-[10px] font-bold px-2 py-0.5 rounded-full flex items-center gap-1"
            style={{ background: alert.severity === "danger" ? "#fef2f2" : "#fff7ed", color: alert.severity === "danger" ? "#dc2626" : "#d97706" }}>
            {alert.type === "allergy" ? "حساسية" : alert.type === "bleeding" ? "نزيف" : alert.type === "pregnancy" ? "حمل" : alert.label ?? "تنبيه"}
          </span>
        ))}
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Start Visit Panel
   ═══════════════════════════════════════════════════════════════════════════ */
export function StartVisitPanel({
  onClose, patient, isPending, onConfirm,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  isPending: boolean;
  onConfirm: () => void;
}) {
  if (!patient) return null;

  return (
    <div className="p-5 space-y-3">
      <div className="p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient.serviceName ?? "—"} — الغرفة: {patient.roomName ?? "—"}
        </div>
      </div>
      <div className="p-3 rounded-xl flex items-center gap-2" style={{ background: "#f0f5fb" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
        <span className="text-xs font-medium" style={{ color: NAVY }}>
          سيتم تغيير حالة المريض إلى &quot;جاري العلاج&quot; وإنشاء سجل زيارة جديد
        </span>
      </div>
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={onConfirm} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#16a34a", opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Play className="w-4 h-4" />}
          بدء الزيارة
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Examination & Diagnosis Panel — Enhanced with detailed fields
   ═══════════════════════════════════════════════════════════════════════════ */
export function ExaminationPanel({
  onClose, patient, diagnosis, medicalAlerts, onSave,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  diagnosis: string;
  medicalAlerts?: { type: string; severity: string; label?: string }[];
  onSave: (data: {
    chiefComplaint: string;
    extraoral: string;
    intraoral: string;
    diagnosis: string;
    clinicalNotes: string;
    treatmentDone: string;
  }) => void;
}) {
  const [chiefComplaint, setChiefComplaint] = useState("");
  const [extraoral, setExtraoral] = useState("");
  const [intraoral, setIntraoral] = useState("");
  const [localDiagnosis, setLocalDiagnosis] = useState(diagnosis);
  const [clinicalNotes, setClinicalNotes] = useState("");
  const [treatmentDone, setTreatmentDone] = useState("");

  const handleSave = () => {
    onSave({
      chiefComplaint,
      extraoral,
      intraoral,
      diagnosis: localDiagnosis,
      clinicalNotes,
      treatmentDone,
    });
    setChiefComplaint("");
    setExtraoral("");
    setIntraoral("");
    setLocalDiagnosis("");
    setClinicalNotes("");
    setTreatmentDone("");
  };

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient?.serviceName ?? "—"} — الغرفة: {patient?.roomName ?? "—"}
        </div>
      </div>

      {/* Medical Alerts read-only */}
      <MedicalAlertsBadge alerts={medicalAlerts ?? []} />

      {/* Chief Complaint */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الشكوى الرئيسية</label>
        <input value={chiefComplaint} onChange={e => setChiefComplaint(e.target.value)}
          placeholder="مثال: ألم في الضرس السفلي الأيمن" className={inputCls()} />
      </div>

      {/* Extraoral Examination */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الفحص خارج الفم</label>
        <textarea value={extraoral} onChange={e => setExtraoral(e.target.value)}
          rows={2} placeholder="مثال: تورم خفيف في المنطقة الوجنية اليمنى، لا يوجد إزاحة..." className={inputCls()} />
      </div>

      {/* Intraoral Examination */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الفحص داخل الفم</label>
        <textarea value={intraoral} onChange={e => setIntraoral(e.target.value)}
          rows={3} placeholder="مثال: تسوس سطحي في الضرس الثاني العلوي الأيسر، التهاب لثة خفيف..." className={inputCls()} />
      </div>

      {/* Diagnosis */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>التشخيص *</label>
        <textarea value={localDiagnosis} onChange={e => setLocalDiagnosis(e.target.value)}
          rows={3} placeholder="مثال: تسوس سطحي في الضرس الثاني العلوي الأيسر" className={inputCls()} />
      </div>

      {/* Clinical Notes */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات سريرية</label>
        <textarea value={clinicalNotes} onChange={e => setClinicalNotes(e.target.value)}
          rows={2} placeholder="ملاحظات إضافية حول الفحص أو حالة المريض..." className={inputCls()} />
      </div>

      {/* Treatment Done Summary */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملخص الإجراء المنفذ</label>
        <textarea value={treatmentDone} onChange={e => setTreatmentDone(e.target.value)}
          rows={2} placeholder="مثال: حشو ضرس، تنظيف جير" className={inputCls()} />
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!localDiagnosis.trim()}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: BLUE, opacity: !localDiagnosis.trim() ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ الفحص
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Priced Procedures Panel — THE KEY FEATURE
   Loads real services from catalog, multi-select, auto-total
   ═══════════════════════════════════════════════════════════════════════════ */
const UPPER_FDI_TEETH = ["18", "17", "16", "15", "14", "13", "12", "11", "21", "22", "23", "24", "25", "26", "27", "28"];
const LOWER_FDI_TEETH = ["48", "47", "46", "45", "44", "43", "42", "41", "31", "32", "33", "34", "35", "36", "37", "38"];

const DEFAULT_TOOTH_CONDITIONS = [
  { value: "healthy", label: "سليم" },
  { value: "decay", label: "تسوس" },
  { value: "filled", label: "محشو" },
  { value: "rct", label: "معالج عصب" },
  { value: "crown", label: "تاج" },
  { value: "extracted", label: "مخلوع" },
  { value: "missing", label: "مفقود" },
  { value: "implant", label: "زرعة" },
  { value: "deep_caries", label: "تسوس عميق" },
  { value: "pulpitis", label: "التهاب لب" },
  { value: "necrotic_pulp", label: "لب متموت" },
  { value: "periapical_lesion", label: "آفة حول الذروة" },
  { value: "fractured", label: "كسر سن" },
  { value: "mobility", label: "حركة" },
  { value: "periodontal_pocket", label: "جيب لثوي" },
  { value: "defective_restoration", label: "حشوة غير صالحة" },
];

const DEFAULT_GENERAL_PROCEDURES = [
  "فحص وتشخيص",
  "تنظيف وتلميع",
  "إزالة جير",
  "حشو كومبوزت",
  "حشو أملغم",
  "حشو مؤقت",
  "علاج عصب جلسة أولى",
  "استكمال علاج عصب",
  "حشو عصب",
  "خلع بسيط",
  "خلع جراحي",
  "ترميم كسر",
  "تاج مؤقت",
  "تاج نهائي",
  "جسر",
  "تلبيسة زركون",
  "تبييض",
  "فلورايد",
  "سد شقوق",
  "استشارة",
];

const DEFAULT_MATERIALS = [
  "Composite",
  "Glass Ionomer",
  "Amalgam",
  "Temporary Filling",
  "Zirconia",
  "E.max",
  "Metal Ceramic",
  "Gutta Percha",
  "MTA",
  "Calcium Hydroxide",
];

const DEFAULT_ANESTHESIA = [
  "بدون تخدير",
  "تخدير موضعي",
  "تخدير ارتشاحي",
  "تخدير عصب سفلي",
  "تخدير رباطي",
];

const DEFAULT_INSTRUCTIONS = [
  "تجنب الأكل على الجهة المعالجة لمدة ساعتين",
  "تجنب الأطعمة القاسية لمدة 24 ساعة",
  "مراجعة عند حدوث ألم مستمر أو تورم",
  "المحافظة على التفريش والخيط",
  "الالتزام بالدواء حسب الوصفة",
];

function addUniqueOption(options: string[], value: string) {
  const trimmed = value.trim();
  if (!trimmed || options.some(option => option.toLowerCase() === trimmed.toLowerCase())) return options;
  return [...options, trimmed];
}

function addUniqueConditionOption(options: typeof DEFAULT_TOOTH_CONDITIONS, label: string) {
  const trimmed = label.trim();
  if (!trimmed) return options;
  const value = trimmed.toLowerCase().replace(/\s+/g, "_");
  if (options.some(option => option.value.toLowerCase() === value || option.label.toLowerCase() === trimmed.toLowerCase())) return options;
  return [...options, { value, label: trimmed }];
}

function toggleValue(values: string[], value: string) {
  return values.includes(value) ? values.filter(v => v !== value) : [...values, value];
}

export interface GeneralDentistryPanelData {
  toothNumber: string;
  condition: string;
  surfacesAffected: string;
  diagnosis: string;
  procedureType: string;
  procedureDetails: string;
  materialUsed: string;
  anesthesiaType: string;
  instructions: string;
  nextVisitPlan: string;
  serviceId: string;
  cost: number;
  treatmentSummary: string;
  chartNotes: string;
}

export function GeneralDentistryPanel({
  onClose, patient, services, onSave, isPending = false,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  services: ServiceWithPrice[];
  isPending?: boolean;
  onSave: (data: GeneralDentistryPanelData) => Promise<void> | void;
}) {
  const [toothNumber, setToothNumber] = useState("");
  const [condition, setCondition] = useState("decay");
  const [surfaces, setSurfaces] = useState<string[]>([]);
  const [diagnosis, setDiagnosis] = useState("");
  const [procedureType, setProcedureType] = useState("حشو كومبوزت");
  const [procedureDetails, setProcedureDetails] = useState("");
  const [materialUsed, setMaterialUsed] = useState("Composite");
  const [anesthesiaType, setAnesthesiaType] = useState("تخدير موضعي");
  const [instructions, setInstructions] = useState("");
  const [nextVisitPlan, setNextVisitPlan] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [cost, setCost] = useState(0);

  const [conditionOptions, setConditionOptions] = useState(DEFAULT_TOOTH_CONDITIONS);
  const [procedureOptions, setProcedureOptions] = useState(DEFAULT_GENERAL_PROCEDURES);
  const [materialOptions, setMaterialOptions] = useState(DEFAULT_MATERIALS);
  const [anesthesiaOptions, setAnesthesiaOptions] = useState(DEFAULT_ANESTHESIA);
  const [customOption, setCustomOption] = useState("");
  const [customOptionType, setCustomOptionType] = useState<"condition" | "procedure" | "material" | "anesthesia">("procedure");

  const serviceMatches = useMemo(() => {
    return services.filter(service => {
      const name = service.arabicName.toLowerCase();
      return /حشو|عصب|خلع|تنظيف|جير|تاج|تلبيس|فحص|استشارة|ترميم|تبييض|فلورايد/.test(name);
    });
  }, [services]);

  const selectedService = serviceId ? services.find(service => service.id === serviceId) : undefined;
  const conditionLabel = conditionOptions.find(option => option.value === condition)?.label ?? condition;
  const canSave = !!toothNumber && !!condition && diagnosis.trim().length > 0 && procedureType.trim().length > 0;

  const handleServiceChange = (value: string) => {
    setServiceId(value);
    const service = services.find(s => s.id === value);
    if (!service) return;
    if (service.defaultPrice != null) setCost(service.defaultPrice);
    if (!procedureType.trim()) setProcedureType(service.arabicName);
  };

  const handleAddCustomOption = () => {
    const trimmed = customOption.trim();
    if (!trimmed) return;
    if (customOptionType === "condition") {
      const value = trimmed.toLowerCase().replace(/\s+/g, "_");
      setConditionOptions(prev => addUniqueConditionOption(prev, trimmed));
      setCondition(value);
    } else if (customOptionType === "procedure") {
      setProcedureOptions(prev => addUniqueOption(prev, trimmed));
      setProcedureType(trimmed);
    } else if (customOptionType === "material") {
      setMaterialOptions(prev => addUniqueOption(prev, trimmed));
      setMaterialUsed(trimmed);
    } else {
      setAnesthesiaOptions(prev => addUniqueOption(prev, trimmed));
      setAnesthesiaType(trimmed);
    }
    setCustomOption("");
  };

  const handleInstructionClick = (instruction: string) => {
    setInstructions(prev => prev.trim() ? `${prev.trim()}\n${instruction}` : instruction);
  };

  const handleSave = async () => {
    if (!canSave) return;
    const surfacesAffected = surfaces.join("");
    const surfacesText = surfaces.join(", ");
    const selectedServiceText = selectedService ? `الخدمة المالية المقترحة: ${selectedService.arabicName}` : "";
    const treatmentSummary = [
      "طب الأسنان العام",
      `السن ${toothNumber}`,
      `الحالة: ${conditionLabel}`,
      surfacesText ? `الأسطح: ${surfacesText}` : "",
      `التشخيص: ${diagnosis.trim()}`,
      `الإجراء: ${procedureType.trim()}`,
      procedureDetails.trim() ? `التفاصيل: ${procedureDetails.trim()}` : "",
      materialUsed.trim() ? `المادة: ${materialUsed.trim()}` : "",
      anesthesiaType.trim() ? `التخدير: ${anesthesiaType.trim()}` : "",
      selectedServiceText,
    ].filter(Boolean).join(" | ");

    const chartNotes = [
      diagnosis.trim() ? `التشخيص: ${diagnosis.trim()}` : "",
      procedureDetails.trim() ? `تفاصيل الإجراء: ${procedureDetails.trim()}` : "",
      materialUsed.trim() ? `المادة المستخدمة: ${materialUsed.trim()}` : "",
      anesthesiaType.trim() ? `التخدير: ${anesthesiaType.trim()}` : "",
      instructions.trim() ? `تعليمات: ${instructions.trim()}` : "",
      nextVisitPlan.trim() ? `الخطة القادمة: ${nextVisitPlan.trim()}` : "",
    ].filter(Boolean).join("\n");

    await onSave({
      toothNumber,
      condition,
      surfacesAffected,
      diagnosis: diagnosis.trim(),
      procedureType: procedureType.trim(),
      procedureDetails: procedureDetails.trim(),
      materialUsed: materialUsed.trim(),
      anesthesiaType: anesthesiaType.trim(),
      instructions: instructions.trim(),
      nextVisitPlan: nextVisitPlan.trim(),
      serviceId,
      cost: Number.isFinite(cost) ? cost : 0,
      treatmentSummary,
      chartNotes,
    });
  };

  return (
    <div className="p-5 space-y-4">
      <div className="p-3 rounded-xl" style={{ background: "#f0fdfa" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          توثيق السن والتشخيص والإجراء وربطه بملف المريض والزيارة الحالية
        </div>
      </div>

      <div>
        <div className="text-xs font-bold mb-2" style={{ color: NAVY }}>اختيار السن بنظام FDI</div>
        <div className="space-y-2 rounded-xl border border-[#e5e7eb] p-3">
          {[UPPER_FDI_TEETH, LOWER_FDI_TEETH].map((row, rowIndex) => (
            <div key={rowIndex} className="grid grid-cols-8 gap-1.5">
              {row.map(tooth => (
                <button
                  key={tooth}
                  type="button"
                  onClick={() => setToothNumber(tooth)}
                  className="h-8 rounded-lg text-xs font-extrabold border transition"
                  style={{
                    background: toothNumber === tooth ? BLUE : "#fff",
                    color: toothNumber === tooth ? "#fff" : NAVY,
                    borderColor: toothNumber === tooth ? BLUE : "#dbe3ef",
                  }}
                >
                  {tooth}
                </button>
              ))}
            </div>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>حالة السن *</label>
          <select value={condition} onChange={e => setCondition(e.target.value)} className={inputCls()}>
            {conditionOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الإجراء *</label>
          <select value={procedureType} onChange={e => setProcedureType(e.target.value)} className={inputCls()}>
            {procedureOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </div>
      </div>

      <div>
        <div className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الأسطح المصابة</div>
        <div className="flex flex-wrap gap-1.5">
          {["M", "O", "D", "B", "F"].map(surface => (
            <button
              key={surface}
              type="button"
              onClick={() => setSurfaces(prev => toggleValue(prev, surface))}
              className="px-3 py-1.5 rounded-full text-xs font-bold border"
              style={{
                background: surfaces.includes(surface) ? "#eff6ff" : "#fff",
                borderColor: surfaces.includes(surface) ? BLUE : "#dbe3ef",
                color: surfaces.includes(surface) ? BLUE : "#475569",
              }}
            >
              {surface}
            </button>
          ))}
        </div>
      </div>

      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>التشخيص السريري *</label>
        <textarea value={diagnosis} onChange={e => setDiagnosis(e.target.value)}
          rows={2} placeholder="مثال: تسوس عميق مع حساسية حرارية في السن 36" className={inputCls()} />
      </div>

      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تفاصيل الإجراء المنفذ</label>
        <textarea value={procedureDetails} onChange={e => setProcedureDetails(e.target.value)}
          rows={3} placeholder="مثال: إزالة التسوس، تبطين، حشو كومبوزت طبقي مع ضبط الإطباق" className={inputCls()} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المادة المستخدمة</label>
          <select value={materialUsed} onChange={e => setMaterialUsed(e.target.value)} className={inputCls()}>
            {materialOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>التخدير</label>
          <select value={anesthesiaType} onChange={e => setAnesthesiaType(e.target.value)} className={inputCls()}>
            {anesthesiaOptions.map(option => <option key={option} value={option}>{option}</option>)}
          </select>
        </div>
      </div>

      <div className="rounded-xl border border-[#e5e7eb] p-3 space-y-2">
        <div className="text-xs font-bold" style={{ color: NAVY }}>إضافة خيار مخصص للقوائم</div>
        <div className="grid grid-cols-1 md:grid-cols-[150px_1fr_auto] gap-2">
          <select value={customOptionType} onChange={e => setCustomOptionType(e.target.value as typeof customOptionType)} className={inputCls()}>
            <option value="procedure">إجراء</option>
            <option value="condition">حالة سن</option>
            <option value="material">مادة</option>
            <option value="anesthesia">تخدير</option>
          </select>
          <input value={customOption} onChange={e => setCustomOption(e.target.value)}
            placeholder="اكتب خياراً جديداً لاستخدامه الآن" className={inputCls()} />
          <button type="button" onClick={handleAddCustomOption} disabled={!customOption.trim()}
            className="px-4 py-2 rounded-xl text-xs font-bold text-white disabled:opacity-50"
            style={{ background: BLUE }}>
            إضافة
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الخدمة المالية المقترحة</label>
          <select value={serviceId} onChange={e => handleServiceChange(e.target.value)} className={inputCls()}>
            <option value="">بدون ربط خدمة مالية</option>
            {serviceMatches.map(service => (
              <option key={service.id} value={service.id}>
                {service.arabicName}{service.defaultPrice ? ` — ${fmtRial(service.defaultPrice)}` : ""}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المبلغ المرجعي للتحصيل</label>
          <input type="number" min={0} value={cost} onChange={e => setCost(Number(e.target.value))}
            className={inputCls()} />
        </div>
      </div>

      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تعليمات للمريض</label>
        <div className="flex flex-wrap gap-1.5 mb-2">
          {DEFAULT_INSTRUCTIONS.map(instruction => (
            <button key={instruction} type="button" onClick={() => handleInstructionClick(instruction)}
              className="px-2.5 py-1 rounded-full text-[11px] font-semibold border border-[#dbe3ef] bg-white text-[#475569]">
              {instruction}
            </button>
          ))}
        </div>
        <textarea value={instructions} onChange={e => setInstructions(e.target.value)}
          rows={3} placeholder="تعليمات مخصصة للمريض بعد الإجراء..." className={inputCls()} />
      </div>

      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الخطة القادمة</label>
        <textarea value={nextVisitPlan} onChange={e => setNextVisitPlan(e.target.value)}
          rows={2} placeholder="مثال: مراجعة بعد أسبوع، استكمال علاج عصب، تركيب تاج..." className={inputCls()} />
      </div>

      <div className="p-2.5 rounded-lg flex items-start gap-2" style={{ background: "#fff7ed" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" style={{ color: ORANGE }} />
        <span className="text-[11px] font-medium" style={{ color: "#92400e" }}>
          الحفظ هنا يوثق طب الأسنان العام في مخطط الأسنان والعلاجات العامة ويضيف ملخصاً للزيارة. التحصيل الفعلي يبقى عند الاستقبال.
        </span>
      </div>

      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!canSave || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#0f766e", opacity: !canSave || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
          حفظ وتوثيق طب الأسنان العام
        </button>
      </div>
    </div>
  );
}

export function PricedProceduresPanel({
  onClose, patient, services, currentTreatmentDone, onSave,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  services: ServiceWithPrice[];
  currentAmountDue?: number;
  currentTreatmentDone: string;
  onSave: (data: { treatmentDone: string; totalAmount: number; suggestedServiceId: string; additionalServicesText: string }) => void;
}) {
  const [searchService, setSearchService] = useState("");
  const [selectedServices, setSelectedServices] = useState<Map<string, ServiceWithPrice & { quantity: number }>>(new Map());
  const [isFreeVisit, setIsFreeVisit] = useState(false);

  // Filter services by search
  const filteredServices = useMemo(() => {
    if (!searchService.trim()) return services;
    const q = searchService.trim().toLowerCase();
    return services.filter(s => s.arabicName.toLowerCase().includes(q));
  }, [services, searchService]);

  // Add a service
  const addService = (service: ServiceWithPrice) => {
    setSelectedServices(prev => {
      const next = new Map(prev);
      const existing = next.get(service.id);
      if (existing) {
        next.set(service.id, { ...existing, quantity: existing.quantity + 1 });
      } else {
        next.set(service.id, { ...service, quantity: 1 });
      }
      return next;
    });
  };

  // Remove a service
  const removeService = (serviceId: string) => {
    setSelectedServices(prev => {
      const next = new Map(prev);
      next.delete(serviceId);
      return next;
    });
  };

  // Change quantity
  const changeQuantity = (serviceId: string, delta: number) => {
    setSelectedServices(prev => {
      const next = new Map(prev);
      const existing = next.get(serviceId);
      if (existing) {
        const newQty = existing.quantity + delta;
        if (newQty <= 0) {
          next.delete(serviceId);
        } else {
          next.set(serviceId, { ...existing, quantity: newQty });
        }
      }
      return next;
    });
  };

  // Calculate total
  const totalAmount = useMemo(() => {
    let sum = 0;
    selectedServices.forEach(s => {
      sum += (s.defaultPrice ?? 0) * s.quantity;
    });
    return sum;
  }, [selectedServices]);

  const finalAmount = isFreeVisit ? 0 : totalAmount;

  const handleSave = () => {
    if (!isFreeVisit && selectedServices.size === 0) return;
    const selected = Array.from(selectedServices.values());
    const names = selected.map(s =>
      s.quantity > 1 ? `${s.arabicName} ×${s.quantity}` : s.arabicName
    );
    const treatmentText = names.join(" + ") || currentTreatmentDone || "زيارة بدون رسوم";
    const firstServiceId = selectedServices.keys().next().value ?? "";
    // F6: Additional services beyond the first are listed as readable text
    const additionalNames = selected.length > 1
      ? selected.slice(1).map(s => s.quantity > 1 ? `${s.arabicName} ×${s.quantity}` : s.arabicName).join("، ")
      : "";

    onSave({
      treatmentDone: treatmentText,
      totalAmount: finalAmount,
      suggestedServiceId: firstServiceId,
      additionalServicesText: additionalNames,
    });
    setSelectedServices(new Map());
    setSearchService("");
    setIsFreeVisit(false);
  };

  return (
    <div className="p-5 space-y-3">
      {/* Patient info */}
      <div className="p-3 rounded-xl" style={{ background: "#faf5ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة الأصلية: {patient?.serviceName ?? "—"}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Left: Service catalog */}
        <div>
          <div className="text-xs font-bold mb-2" style={{ color: NAVY }}>كتالوج الخدمات</div>
          <input value={searchService} onChange={e => setSearchService(e.target.value)}
            placeholder="بحث عن خدمة..." className={inputCls() + " mb-2"} />

          <div className="max-h-[300px] overflow-y-auto space-y-1 rounded-lg border p-2" style={{ borderColor: "#e5e7eb" }}>
            {filteredServices.length === 0 && (
              <div className="text-xs text-center py-4" style={{ color: "#94a3b8" }}>لا توجد خدمات</div>
            )}
            {filteredServices.map(service => {
              const isSelected = selectedServices.has(service.id);
              return (
                <button key={service.id}
                  onClick={() => isSelected ? removeService(service.id) : addService(service)}
                  className="w-full flex items-center gap-2 px-3 py-2.5 rounded-lg text-right transition"
                  style={{
                    background: isSelected ? "#9333ea08" : "transparent",
                    border: isSelected ? "1.5px solid #9333ea40" : "1.5px solid transparent",
                  }}>
                  <div className="flex-1">
                    <div className="text-xs font-bold" style={{ color: NAVY }}>{service.arabicName}</div>
                    {service.defaultPrice != null && (
                      <div className="text-[10px] font-bold" style={{ color: ORANGE }}>{fmtRial(service.defaultPrice)}</div>
                    )}
                  </div>
                  {isSelected ? (
                    <Minus className="w-4 h-4" style={{ color: "#ef4444" }} />
                  ) : (
                    <Plus className="w-4 h-4" style={{ color: "#9333ea" }} />
                  )}
                </button>
              );
            })}
          </div>
        </div>

        {/* Right: Selected services + total */}
        <div>
          <div className="text-xs font-bold mb-2" style={{ color: NAVY }}>الإجراءات المختارة</div>
          <div className="min-h-[200px] max-h-[260px] overflow-y-auto space-y-1.5 rounded-lg border p-2"
            style={{ borderColor: "#e5e7eb", background: "#fafafa" }}>
            {selectedServices.size === 0 && (
              <div className="text-xs text-center py-8" style={{ color: "#94a3b8" }}>
                اختر خدمات من الكتالوج
              </div>
            )}
            {Array.from(selectedServices.values()).map(service => (
              <div key={service.id}
                className="flex items-center gap-2 px-3 py-2 rounded-lg bg-white border"
                style={{ borderColor: "#9333ea20" }}>
                <div className="flex-1">
                  <div className="text-xs font-bold" style={{ color: NAVY }}>{service.arabicName}</div>
                  <div className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>
                    {fmtRial(service.defaultPrice ?? 0)} × {service.quantity} = {fmtRial((service.defaultPrice ?? 0) * service.quantity)}
                  </div>
                </div>
                <div className="flex items-center gap-1">
                  <button onClick={() => changeQuantity(service.id, -1)}
                    className="w-6 h-6 rounded flex items-center justify-center" style={{ background: "#fef2f2" }}>
                    <Minus className="w-3 h-3" style={{ color: "#ef4444" }} />
                  </button>
                  <span className="text-xs font-bold w-5 text-center" style={{ color: NAVY }}>{service.quantity}</span>
                  <button onClick={() => changeQuantity(service.id, 1)}
                    className="w-6 h-6 rounded flex items-center justify-center" style={{ background: "#f0fdf4" }}>
                    <Plus className="w-3 h-3" style={{ color: "#16a34a" }} />
                  </button>
                  <button onClick={() => removeService(service.id)}
                    className="w-6 h-6 rounded flex items-center justify-center mr-1" style={{ background: "#fef2f2" }}>
                    <Trash2 className="w-3 h-3" style={{ color: "#ef4444" }} />
                  </button>
                </div>
              </div>
            ))}
          </div>

          {/* Total + Free visit option */}
          <div className="mt-3 p-3 rounded-xl" style={{ background: "#9333ea08", border: "1px solid #9333ea20" }}>
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-bold" style={{ color: NAVY }}>{isFreeVisit ? "زيارة بدون رسوم" : "الإجمالي المحسوب"}</span>
              <span className="text-sm font-extrabold" style={{ color: isFreeVisit ? "#6b7280" : "#9333ea" }}>
                {isFreeVisit ? fmtRial(0) : fmtRial(totalAmount)}
              </span>
            </div>
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input type="checkbox" checked={isFreeVisit} onChange={e => setIsFreeVisit(e.target.checked)}
                className="w-4 h-4 rounded accent-[#9333ea]" />
              <span className="text-[11px] font-medium" style={{ color: "#64748b" }}>زيارة بدون رسوم</span>
            </label>
          </div>
        </div>
      </div>

      {/* Important note */}
      <div className="p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#fff7ed" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
        <span className="text-[11px] font-medium" style={{ color: "#92400e" }}>
          لن يتم إنشاء دفعة مالية من هنا — يتم تسجيل المبلغ فقط كمرجع للاستقبال
        </span>
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!isFreeVisit && selectedServices.size === 0}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#9333ea", opacity: !isFreeVisit && selectedServices.size === 0 ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          {isFreeVisit ? "تأكيد زيارة بدون رسوم" : `تأكيد الإجراءات (${fmtRial(finalAmount)})`}
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Treatment Plan Panel — Enhanced
   ═══════════════════════════════════════════════════════════════════════════ */
export function TreatmentPlanPanel({
  onClose, patient, onSave,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: {
    currentTreatmentPlan: string;
    nextVisitPlan: string;
    treatmentObjectives: string;
    notesForNextAppointment: string;
    plan: string;
  }) => void;
}) {
  const [currentTreatmentPlan, setCurrentTreatmentPlan] = useState("");
  const [nextVisitPlan, setNextVisitPlan] = useState("");
  const [treatmentObjectives, setTreatmentObjectives] = useState("");
  const [notesForNextAppointment, setNotesForNextAppointment] = useState("");

  const handleSave = () => {
    // Combine all fields into the legacy `plan` field for backwards compat
    const planParts = [
      currentTreatmentPlan ? `خطة العلاج الحالية: ${currentTreatmentPlan}` : "",
      nextVisitPlan ? `خطة الزيارة القادمة: ${nextVisitPlan}` : "",
      treatmentObjectives ? `أهداف العلاج: ${treatmentObjectives}` : "",
      notesForNextAppointment ? `ملاحظات للموعد القادم: ${notesForNextAppointment}` : "",
    ].filter(Boolean).join("\n");

    onSave({
      currentTreatmentPlan,
      nextVisitPlan,
      treatmentObjectives,
      notesForNextAppointment,
      plan: planParts,
    });
    setCurrentTreatmentPlan("");
    setNextVisitPlan("");
    setTreatmentObjectives("");
    setNotesForNextAppointment("");
  };

  const hasAnyField = currentTreatmentPlan.trim() || nextVisitPlan.trim() ||
    treatmentObjectives.trim() || notesForNextAppointment.trim();

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#eff6ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>

      {/* Current Treatment Plan */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خطة العلاج الحالية</label>
        <textarea value={currentTreatmentPlan} onChange={e => setCurrentTreatmentPlan(e.target.value)}
          rows={4} placeholder="وصف خطة العلاج التفصيلية المتبعة حالياً..." className={inputCls()} />
      </div>

      {/* Treatment Objectives */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>أهداف العلاج</label>
        <textarea value={treatmentObjectives} onChange={e => setTreatmentObjectives(e.target.value)}
          rows={3} placeholder="مثال: إزالة الألم، الحفاظ على السن، تحسين الإطباق..." className={inputCls()} />
      </div>

      {/* Next Visit Plan */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خطة الزيارة القادمة</label>
        <textarea value={nextVisitPlan} onChange={e => setNextVisitPlan(e.target.value)}
          rows={2} placeholder="ما المخطط للزيارة القادمة..." className={inputCls()} />
      </div>

      {/* Notes for Next Appointment */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات للموعد القادم</label>
        <textarea value={notesForNextAppointment} onChange={e => setNotesForNextAppointment(e.target.value)}
          rows={2} placeholder="أي ملاحظات يجب مراعاتها في الموعد القادم..." className={inputCls()} />
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!hasAnyField}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#2563eb", opacity: !hasAnyField ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ خطة العلاج
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Orthodontic Follow-up Panel — Enhanced with detailed ortho fields
   ═══════════════════════════════════════════════════════════════════════════ */
export function OrthoFollowUpPanel({
  onClose, patient, onSave, activeOrthoCase,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  /** Phase 1 ortho integration: active case (from daily summary) used to prefill the current phase. */
  activeOrthoCase?: { caseNumber?: string; currentStage?: string } | null;
  onSave: (data: {
    currentPhase: string;
    upperArchwire: string;
    lowerArchwire: string;
    elastics: string;
    bracketIssues: string;
    oralHygiene: string;
    sessionNotes: string;
    nextOrthodonticPlan: string;
    notes: string;
    nextVisitPlan: string;
  }) => void;
}) {
  // Prefill from the active ortho case's current stage when available (field starts empty otherwise)
  const [currentPhase, setCurrentPhase] = useState(activeOrthoCase?.currentStage ?? "");
  const [upperArchwire, setUpperArchwire] = useState("");
  const [lowerArchwire, setLowerArchwire] = useState("");
  const [elastics, setElastics] = useState("");
  const [bracketIssues, setBracketIssues] = useState("");
  const [oralHygiene, setOralHygiene] = useState("");
  const [sessionNotes, setSessionNotes] = useState("");
  const [nextOrthodonticPlan, setNextOrthodonticPlan] = useState("");

  const handleSave = () => {
    // Build legacy fields for backwards compat
    const notesParts = [
      currentPhase ? `المرحلة: ${currentPhase}` : "",
      upperArchwire ? `سلك الفك العلوي: ${upperArchwire}` : "",
      lowerArchwire ? `سلك الفك السفلي: ${lowerArchwire}` : "",
      elastics ? `المطاط: ${elastics}` : "",
      bracketIssues ? `مشاكل الأقواس: ${bracketIssues}` : "",
      oralHygiene ? `نظافة الفم: ${oralHygiene}` : "",
      sessionNotes ? `ملاحظات الجلسة: ${sessionNotes}` : "",
    ].filter(Boolean).join("\n");

    onSave({
      currentPhase,
      upperArchwire,
      lowerArchwire,
      elastics,
      bracketIssues,
      oralHygiene,
      sessionNotes,
      nextOrthodonticPlan,
      notes: notesParts,
      nextVisitPlan: nextOrthodonticPlan,
    });

    setCurrentPhase("");
    setUpperArchwire("");
    setLowerArchwire("");
    setElastics("");
    setBracketIssues("");
    setOralHygiene("");
    setSessionNotes("");
    setNextOrthodonticPlan("");
  };

  const hasAnyField = currentPhase.trim() || upperArchwire.trim() || lowerArchwire.trim() ||
    elastics.trim() || bracketIssues.trim() || sessionNotes.trim() || nextOrthodonticPlan.trim();

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#fff7ed" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>

      {/* Current Phase */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المرحلة الحالية</label>
        <input value={currentPhase} onChange={e => setCurrentPhase(e.target.value)}
          placeholder="مثال: المرحلة الأولى، مرحلة الإغلاق..." className={inputCls()} />
      </div>

      {/* Archwire details — side by side */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>سلك الفك العلوي</label>
          <input value={upperArchwire} onChange={e => setUpperArchwire(e.target.value)}
            placeholder="مثال: NiTi 0.016" className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>سلك الفك السفلي</label>
          <input value={lowerArchwire} onChange={e => setLowerArchwire(e.target.value)}
            placeholder="مثال: SS 0.019×0.025" className={inputCls()} />
        </div>
      </div>

      {/* Elastics & Bracket Issues — side by side */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المطاط</label>
          <input value={elastics} onChange={e => setElastics(e.target.value)}
            placeholder="مثال: Class II، 6oz" className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>مشاكل الأقواس</label>
          <input value={bracketIssues} onChange={e => setBracketIssues(e.target.value)}
            placeholder="مثال: لا توجد، قوس مفكوك 3-4" className={inputCls()} />
        </div>
      </div>

      {/* Oral Hygiene */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نظافة الفم</label>
        <select value={oralHygiene} onChange={e => setOralHygiene(e.target.value)} className={inputCls()}>
          <option value="">اختر التقييم</option>
          <option value="Good">جيدة</option>
          <option value="Fair">مقبولة</option>
          <option value="Poor">ضعيفة</option>
        </select>
      </div>

      {/* Session Notes */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات الجلسة</label>
        <textarea value={sessionNotes} onChange={e => setSessionNotes(e.target.value)}
          rows={3} placeholder="تسجيل تفاصيل تعديل التقويم..." className={inputCls()} />
      </div>

      {/* Next Orthodontic Plan */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خطة الزيارة التقويمية القادمة</label>
        <input value={nextOrthodonticPlan} onChange={e => setNextOrthodonticPlan(e.target.value)}
          placeholder="مثال: تعديل سلك، تغيير مطاط" className={inputCls()} />
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!hasAnyField}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: !hasAnyField ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ المتابعة
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Images & Radiographs Panel
   ═══════════════════════════════════════════════════════════════════════════ */
export function ImagesRadiographsPanel({
  onClose, patient, onSave,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: () => void;
}) {
  const [recordType, setRecordType] = useState<"Radiograph" | "ClinicalPhoto">("Radiograph");
  const [xrayType, setXrayType] = useState("Panoramic");
  const [photoType, setPhotoType] = useState("Clinical");
  const [notes, setNotes] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const canSave = !!patient?.patientId && !!file && !isSaving;

  const handleSave = async () => {
    if (!patient?.patientId) {
      toast.error("اختر مريضاً أولاً");
      return;
    }
    if (!file) {
      toast.error("اختر ملف الصورة أو الأشعة");
      return;
    }

    setIsSaving(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const uploadRes = await api.post<{
        url: string;
        originalName: string;
        size: number;
        contentType: string;
      }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });

      const uploaded = uploadRes.data;
      if (recordType === "Radiograph") {
        await api.post("/api/radiographs", {
          patientId: patient.patientId,
          fileUrl: uploaded.url,
          fileName: uploaded.originalName,
          fileSize: uploaded.size,
          mimeType: uploaded.contentType,
          xrayType,
          doctorId: patient.doctorId || undefined,
          notes: notes.trim() || null,
        });
      } else {
        await api.post("/api/clinical-photos", {
          patientId: patient.patientId,
          fileUrl: uploaded.url,
          category: "clinical",
          photoType,
          stage: patient.visitStatus || patient.appointmentStatus || null,
          notes: notes.trim() || null,
        });
      }

      toast.success(recordType === "Radiograph" ? "تم رفع الأشعة وربطها بالمريض" : "تم رفع الصورة وربطها بالمريض");
      onSave();
    } catch {
      toast.error("فشل رفع الملف أو حفظه في أرشيف المريض");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#f8fafc" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient?.serviceName ?? "—"} — الغرفة: {patient?.roomName ?? "—"}
        </div>
      </div>

      <div className="border-2 border-dashed rounded-2xl p-5 space-y-4"
        style={{ borderColor: "#e2e8f0", background: "#fafafa" }}>
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 rounded-2xl flex items-center justify-center" style={{ background: "#f1f5f9" }}>
            <Camera className="w-6 h-6" style={{ color: "#64748b" }} />
          </div>
          <div>
            <div className="text-sm font-bold mb-1" style={{ color: NAVY }}>رفع صورة أو أشعة</div>
            <div className="text-xs font-medium" style={{ color: "#64748b" }}>
              سيتم حفظ الملف مباشرة في أرشيف المريض
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نوع السجل</label>
            <select value={recordType} onChange={e => setRecordType(e.target.value as "Radiograph" | "ClinicalPhoto")} className={inputCls()}>
              <option value="Radiograph">أشعة</option>
              <option value="ClinicalPhoto">صورة سريرية</option>
            </select>
          </div>
          {recordType === "Radiograph" ? (
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نوع الأشعة</label>
              <select value={xrayType} onChange={e => setXrayType(e.target.value)} className={inputCls()}>
                <option value="Panoramic">بانوراما</option>
                <option value="Periapical">حول ذروية</option>
                <option value="Bitewing">بايت وينغ</option>
                <option value="CBCT">CBCT</option>
                <option value="Cephalometric">سيفالومتريك</option>
                <option value="Other">أخرى</option>
              </select>
            </div>
          ) : (
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نوع الصورة</label>
              <select value={photoType} onChange={e => setPhotoType(e.target.value)} className={inputCls()}>
                <option value="Clinical">سريرية</option>
                <option value="Intraoral">داخل الفم</option>
                <option value="Extraoral">خارج الفم</option>
                <option value="Progress">متابعة</option>
                <option value="Other">أخرى</option>
              </select>
            </div>
          )}
        </div>

        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الملف</label>
          <input
            type="file"
            accept=".jpg,.jpeg,.png,.webp,.pdf"
            onChange={e => setFile(e.target.files?.[0] ?? null)}
            disabled={isSaving}
            className="w-full text-xs file:ml-2 file:py-2 file:px-3 file:rounded-lg file:border-0 file:text-xs file:font-bold file:text-white file:cursor-pointer disabled:opacity-50"
            style={{ color: "#334155" }}
          />
          {file && (
            <p className="text-[10px] mt-1" style={{ color: "#16a34a" }}>
              تم اختيار: {file.name}
            </p>
          )}
        </div>

        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
          <textarea
            value={notes}
            onChange={e => setNotes(e.target.value)}
            rows={3}
            placeholder="مثال: قبل العلاج، بعد التنظيف، متابعة الجذر..."
            className={inputCls()}
          />
        </div>

        <div className="p-3 rounded-xl flex items-center gap-2" style={{ background: "#eff6ff", border: "1px solid #dbeafe" }}>
          <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
          <div className="text-xs font-medium" style={{ color: "#64748b" }}>
            الملفات المسموحة: JPG، PNG، WEBP، PDF. الحد الأقصى 10 ميجابايت.
          </div>
        </div>
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إغلاق</button>
        <button onClick={handleSave} disabled={!canSave}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: BLUE, opacity: !canSave ? 0.55 : 1 }}>
          {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
          {isSaving ? "جارٍ الرفع..." : "رفع وحفظ"}
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Lab Order Panel — Placeholder with TODO
   TODO: Connect to lab order API when backend is ready
   ═══════════════════════════════════════════════════════════════════════════ */
export interface LabOrderPanelData {
  labWorkType: string;
  shade: string;
  deliveryDate: string;
  labInstructions: string;
  referralDepartment: string;
}

export function LabOrderPanel({
  onClose, patient, onSave, isPending = false,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: LabOrderPanelData) => Promise<void> | void;
  isPending?: boolean;
}) {
  const [labWorkType, setLabWorkType] = useState("");
  const [shade, setShade] = useState("");
  const [deliveryDate, setDeliveryDate] = useState("");
  const [labInstructions, setLabInstructions] = useState("");
  const [referralDepartment, setReferralDepartment] = useState("");

  const handleSave = async () => {
    await onSave({
      labWorkType,
      shade,
      deliveryDate,
      labInstructions,
      referralDepartment,
    });
    setLabWorkType("");
    setShade("");
    setDeliveryDate("");
    setLabInstructions("");
    setReferralDepartment("");
  };

  const hasAnyField = labWorkType.trim() || labInstructions.trim() || referralDepartment;

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#ecfeff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient?.serviceName ?? "—"} — الغرفة: {patient?.roomName ?? "—"}
        </div>
      </div>

      {/* Lab Work Type */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نوع العمل المخبري</label>
        <input value={labWorkType} onChange={e => setLabWorkType(e.target.value)}
          placeholder="مثال: تاج خزفي، جسر، طقم..." className={inputCls()} />
      </div>

      {/* Shade & Delivery Date — side by side */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>اللون / الدرجة</label>
          <input value={shade} onChange={e => setShade(e.target.value)}
            placeholder="مثال: A2، B1" className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تاريخ التسليم</label>
          <input type="date" value={deliveryDate} onChange={e => setDeliveryDate(e.target.value)}
            className={inputCls()} />
        </div>
      </div>

      {/* Referral Department */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>قسم الإحالة الداخلية</label>
        <select value={referralDepartment} onChange={e => setReferralDepartment(e.target.value)} className={inputCls()}>
          <option value="">اختر القسم</option>
          <option value="surgery">جراحة الفم</option>
          <option value="endodontics">علاج الجذور</option>
          <option value="prosthodontics">التعويضات السنية</option>
          <option value="orthodontics">تقويم الأسنان</option>
        </select>
      </div>

      {/* Lab Instructions */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تعليمات المختبر</label>
        <textarea value={labInstructions} onChange={e => setLabInstructions(e.target.value)}
          rows={3} placeholder="تعليمات تفصيلية للمختبر..." className={inputCls()} />
      </div>

      {/* TODO: Connect to lab order API when backend is ready */}

      {/* Info note */}
      <div className="p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#ecfeff" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#0891b2" }} />
        <span className="text-[11px] font-medium" style={{ color: "#155e75" }}>
          سيتم حفظ طلب المختبر كملاحظة سريرية — سيتم ربطه بنظام المختبر لاحقاً
        </span>
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!hasAnyField}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#0891b2", opacity: !hasAnyField || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
          حفظ طلب المختبر
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Prescription & Instructions Panel — Enhanced
   ═══════════════════════════════════════════════════════════════════════════ */
export interface PrescriptionPanelData {
  prescriptionText: string;
  instructions: string;
}

export function PrescriptionPanel({
  onClose, patient, onSave, isPending = false,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: PrescriptionPanelData) => Promise<void> | void;
  isPending?: boolean;
}) {
  const [prescriptionText, setPrescriptionText] = useState("");
  const [instructions, setInstructions] = useState("");

  const handleSave = async () => {
    await onSave({ prescriptionText, instructions });
    setPrescriptionText("");
    setInstructions("");
  };

  // WhatsApp instruction template preview (read-only)
  const whatsappPreview = instructions.trim()
    ? `التعليمات بعد العلاج:\n${instructions}${prescriptionText.trim() ? `\nالوصفة:\n${prescriptionText}` : ""}`
    : "";

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#fef2f2" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>

      {/* Prescription Text */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نص الوصفة الطبية</label>
        <textarea value={prescriptionText} onChange={e => setPrescriptionText(e.target.value)}
          rows={4} placeholder="مثال: أموكسيسيلين 500 مجم — مرتين يومياً لمدة 5 أيام&#10;إيبوبروفين 400 مجم — ثلاث مرات يومياً عند الحاجة..." className={inputCls()} />
      </div>

      {/* Post-treatment Instructions */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تعليمات ما بعد العلاج</label>
        <textarea value={instructions} onChange={e => setInstructions(e.target.value)}
          rows={3} placeholder="مثال: عدم أكل الأطعمة الصلبة لمدة 24 ساعة، تجنب المضمضة القوية..." className={inputCls()} />
      </div>

      {/* Official prescription note */}
      <div className="p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#f0f5fb" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
        <span className="text-[11px] font-medium" style={{ color: NAVY }}>
          لإنشاء وصفة طبية رسمية، استخدم قسم الوصفات من القائمة الجانبية
        </span>
      </div>

      {/* WhatsApp instruction template preview */}
      {whatsappPreview && (
        <div className="p-3 rounded-xl" style={{ background: "#f0fdf4", border: "1px solid #bbf7d0" }}>
          <div className="flex items-center gap-1.5 mb-2">
            <MessageSquare className="w-3.5 h-3.5" style={{ color: "#16a34a" }} />
            <span className="text-[10px] font-bold" style={{ color: "#16a34a" }}>معاينة رسالة واتساب</span>
          </div>
          <div className="p-2.5 rounded-lg text-xs font-medium whitespace-pre-wrap" style={{ background: "#dcfce7", color: "#15803d" }}>
            {whatsappPreview}
          </div>
          <div className="mt-1.5 text-[10px] font-medium" style={{ color: "#64748b" }}>
            لإرسال رسالة واتساب، استخدم قسم الرسائل
          </div>
        </div>
      )}

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={(!prescriptionText.trim() && !instructions.trim()) || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#dc2626", opacity: (!prescriptionText.trim() && !instructions.trim()) || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
          حفظ الوصفة والتعليمات
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Follow-up Appointment Suggest Panel — Enhanced
   ═══════════════════════════════════════════════════════════════════════════ */
export interface FollowUpPanelData {
  followUpDate: string;
  followUpTime: string;
  serviceId: string;
  reason: string;
}

export function FollowUpSuggestPanel({
  onClose, patient, services, onSave, isPending = false,
}: {
  onClose: () => void;
  patient: DoctorPatientItem | null;
  services?: ServiceWithPrice[];
  onSave: (data: FollowUpPanelData) => Promise<void> | void;
  isPending?: boolean;
}) {
  const [followUpDate, setFollowUpDate] = useState("");
  const [followUpTime, setFollowUpTime] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [reason, setReason] = useState("");

  const handleSave = async () => {
    await onSave({ followUpDate, followUpTime, serviceId, reason });
    setFollowUpDate("");
    setFollowUpTime("");
    setServiceId("");
    setReason("");
  };

  return (
    <div className="p-5 space-y-3">
      {/* Patient info header */}
      <div className="p-3 rounded-xl" style={{ background: "#f5f3ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>

      {/* Date & Time — side by side */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تاريخ المتابعة المقترح *</label>
          <input type="date" value={followUpDate} onChange={e => setFollowUpDate(e.target.value)} className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الوقت المقترح</label>
          <input type="time" value={followUpTime} onChange={e => setFollowUpTime(e.target.value)} className={inputCls()} />
        </div>
      </div>

      {/* Service */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خدمة المتابعة</label>
        <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
          <option value="">اختر الخدمة</option>
          {services?.map(s => (
            <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${fmtRial(s.defaultPrice)}` : ""}</option>
          ))}
        </select>
      </div>

      {/* Reason for follow-up */}
      <div>
        <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>سبب المتابعة</label>
        <textarea value={reason} onChange={e => setReason(e.target.value)}
          rows={3} placeholder="مثال: متابعة بعد حشو العصب، إزالة الغرز..." className={inputCls()} />
      </div>

      {/* Note: just save suggestion, don't create appointment */}
      <div className="p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#f5f3ff" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#7c3aed" }} />
        <span className="text-[11px] font-medium" style={{ color: "#5b21b6" }}>
          سيتم إنشاء موعد متابعة فعلي في جدول المواعيد
        </span>
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!followUpDate || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#7c3aed", opacity: !followUpDate || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
          إنشاء موعد المتابعة
        </button>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Handoff to Reception Confirm Panel — Enhanced validation
   ═══════════════════════════════════════════════════════════════════════════ */
export function HandoffPanel({
  patient, clinicalNotes, isPending, onClose, onConfirm,
}: {
  patient: DoctorPatientItem;
  clinicalNotes: {
    diagnosis: string;
    treatmentDone: string;
    instructions: string;
    nextVisitPlan: string;
    amountDue: number;
    suggestedServiceId: string;
    followUpDate: string;
  };
  selectedServicesTotal?: number;
  isPending: boolean;
  onClose: () => void;
  onConfirm: () => void;
}) {
  const hasNotes = clinicalNotes.diagnosis || clinicalNotes.treatmentDone || clinicalNotes.amountDue > 0;
  const hasNoNotesAtAll = !clinicalNotes.diagnosis && !clinicalNotes.treatmentDone &&
    !clinicalNotes.instructions && !clinicalNotes.nextVisitPlan &&
    clinicalNotes.amountDue <= 0;

  // Count procedures from treatmentDone string
  const procedureCount = clinicalNotes.treatmentDone
    ? clinicalNotes.treatmentDone.split("+").filter(Boolean).length
    : 0;

  return (
    <div className="p-5 space-y-3">
      {/* Patient info */}
      <div className="p-3 rounded-xl" style={{ background: "#fff7ed" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient.serviceName ?? "—"} — الغرفة: {patient.roomName ?? "—"}
        </div>
      </div>

      {/* Strong warning when no clinical notes at all */}
      {hasNoNotesAtAll && (
        <div className="p-3 rounded-xl flex items-start gap-2" style={{ background: "#fef2f2", border: "1px solid #fecaca" }}>
          <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5" style={{ color: "#dc2626" }} />
          <div>
            <div className="text-xs font-bold mb-0.5" style={{ color: "#dc2626" }}>تحذير: لا توجد ملاحظات سريرية</div>
            <span className="text-[11px] font-medium" style={{ color: "#991b1b" }}>
              لم يتم تسجيل أي تشخيص أو إجراءات أو تعليمات. يُنصح بشدة بتسجيل الملاحظات السريرية قبل تسليم المريض للاستقبال.
              يمكنك المتابعة لكن هذا قد يؤثر على جودة الرعاية.
            </span>
          </div>
        </div>
      )}

      {/* Clinical summary */}
      {hasNotes && (
        <div className="space-y-2">
          <div className="text-xs font-bold mb-2" style={{ color: NAVY }}>ملخص سيرسل للاستقبال:</div>

          {/* Procedures count and total — prominent */}
          {procedureCount > 0 && (
            <div className="p-3 rounded-xl flex items-center justify-between" style={{ background: "#faf5ff", border: "1.5px solid #9333ea30" }}>
              <div className="flex items-center gap-2">
                <ListChecks className="w-4 h-4" style={{ color: "#9333ea" }} />
                <span className="text-xs font-bold" style={{ color: "#9333ea" }}>الإجراءات المحددة</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-xs font-bold" style={{ color: NAVY }}>{procedureCount} إجراء</span>
                <span className="text-sm font-extrabold" style={{ color: "#9333ea" }}>{fmtRial(clinicalNotes.amountDue)}</span>
              </div>
            </div>
          )}

          <div className="grid grid-cols-2 gap-2">
            {clinicalNotes.diagnosis && (
              <div className="p-2.5 rounded-lg" style={{ background: "#f0f5fb" }}>
                <div className="text-[10px] font-bold" style={{ color: BLUE }}>التشخيص</div>
                <div className="text-xs font-medium" style={{ color: NAVY }}>{clinicalNotes.diagnosis}</div>
              </div>
            )}
            {clinicalNotes.treatmentDone && !procedureCount && (
              <div className="p-2.5 rounded-lg" style={{ background: "#faf5ff" }}>
                <div className="text-[10px] font-bold" style={{ color: "#9333ea" }}>العلاج المنفذ</div>
                <div className="text-xs font-medium" style={{ color: NAVY }}>{clinicalNotes.treatmentDone}</div>
              </div>
            )}
            {clinicalNotes.amountDue > 0 && !procedureCount && (
              <div className="p-2.5 rounded-lg" style={{ background: "#fff7ed" }}>
                <div className="text-[10px] font-bold" style={{ color: ORANGE }}>المبلغ المستحق</div>
                <div className="text-sm font-extrabold" style={{ color: NAVY }}>{fmtRial(clinicalNotes.amountDue)}</div>
              </div>
            )}
            {clinicalNotes.instructions && (
              <div className="p-2.5 rounded-lg" style={{ background: "#f0fdf4" }}>
                <div className="text-[10px] font-bold" style={{ color: "#16a34a" }}>التعليمات</div>
                <div className="text-xs font-medium" style={{ color: NAVY }}>{clinicalNotes.instructions}</div>
              </div>
            )}
            {clinicalNotes.followUpDate && (
              <div className="p-2.5 rounded-lg" style={{ background: "#f5f3ff" }}>
                <div className="text-[10px] font-bold" style={{ color: "#7c3aed" }}>موعد المتابعة</div>
                <div className="text-xs font-medium" style={{ color: NAVY }}>{clinicalNotes.followUpDate}</div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Mild warning when some notes but not complete */}
      {!hasNotes && !hasNoNotesAtAll && (
        <div className="p-3 rounded-lg flex items-center gap-2" style={{ background: "#fef3c7" }}>
          <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#d97706" }} />
          <span className="text-xs font-medium" style={{ color: "#92400e" }}>
            لم يتم تسجيل ملاحظات سريرية بعد — يفضل تسجيل التشخيص والإجراءات قبل الإرسال
          </span>
        </div>
      )}

      {/* Important note */}
      <div className="p-3 rounded-xl" style={{ background: "#f0fdf4", border: "1px solid #bbf7d0" }}>
        <div className="flex items-center gap-2 mb-1">
          <Stethoscope className="w-4 h-4" style={{ color: "#16a34a" }} />
          <span className="text-xs font-bold" style={{ color: "#16a34a" }}>تأكيد التسليم</span>
        </div>
        <p className="text-xs font-medium" style={{ color: NAVY }}>
          سيتم إرسال المريض للاستقبال مع جميع الملاحظات السريرية والمبلغ المستحق.
          الاستقبال سيتولى إنشاء الفاتورة وتحصيل الدفع وإتمام الخروج.
          <strong> لن يتم إنشاء أي دفعة مالية من جانبك.</strong>
        </p>
      </div>

      {/* Action buttons */}
      <div className="flex gap-2 pt-3">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>تراجع</button>
        <button onClick={onConfirm} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
          إرسال للاستقبال
        </button>
      </div>
    </div>
  );
}
