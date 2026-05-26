/**
 * Modal components for Doctor Clinic Workspace.
 * StartVisit, Examination, PricedProcedures, TreatmentPlan,
 * OrthoFollowUp, ImagesRadiographs, LabOrder, Prescription,
 * FollowUpSuggest, HandoffConfirm.
 */

"use client";

import { useState, useMemo } from "react";
import {
  X, Play, ClipboardCheck, ListChecks, FileText, GitBranch,
  Pill, CalendarClock, Send, Loader2, AlertCircle, Check,
  Plus, Minus, Trash2, Stethoscope, Image, FlaskConical,
  MessageSquare, Camera, Upload,
} from "lucide-react";
import {
  NAVY, BLUE, ORANGE, inputCls, fmtRial,
} from "../../daily-operations/_lib/constants";
import type { DoctorPatientItem, ServiceWithPrice } from "../_lib/hooks";

/* ═══════════════════════════════════════════════════════════════════════════
   Shared modal shell
   ═══════════════════════════════════════════════════════════════════════════ */
function ModalShell({ open, onClose, title, icon: Icon, iconColor, children, wide }: {
  open: boolean; onClose: () => void; title: string;
  icon?: React.ElementType; iconColor?: string; children: React.ReactNode; wide?: boolean;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div
        className={`bg-white rounded-2xl shadow-xl ${wide ? "w-full max-w-2xl" : "w-full max-w-md"} max-h-[90vh] overflow-y-auto`}
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          {Icon && (
            <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: (iconColor ?? BLUE) + "15" }}>
              <Icon className="w-4.5 h-4.5" style={{ color: iconColor ?? BLUE }} />
            </div>
          )}
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: NAVY }}>{title}</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
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
   Start Visit Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function StartVisitModal({
  open, onClose, patient, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  isPending: boolean;
  onConfirm: () => void;
}) {
  if (!patient) return null;

  return (
    <ModalShell open={open} onClose={onClose} title="بدء الزيارة" icon={Play} iconColor="#16a34a">
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0fdf4" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient.serviceName ?? "—"} — الغرفة: {patient.roomName ?? "—"}
        </div>
      </div>
      <div className="p-3 rounded-xl mb-4 flex items-center gap-2" style={{ background: "#f0f5fb" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
        <span className="text-xs font-medium" style={{ color: NAVY }}>
          سيتم تغيير حالة المريض إلى &quot;جاري العلاج&quot; وإنشاء سجل زيارة جديد
        </span>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={onConfirm} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#16a34a", opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Play className="w-4 h-4" />}
          بدء الزيارة
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Examination & Diagnosis Modal — Enhanced with detailed fields
   ═══════════════════════════════════════════════════════════════════════════ */
export function ExaminationModal({
  open, onClose, patient, diagnosis, medicalAlerts, onSave,
}: {
  open: boolean; onClose: () => void;
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
    <ModalShell open={open} onClose={onClose} title="الفحص والتشخيص" icon={ClipboardCheck} iconColor={BLUE} wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient?.serviceName ?? "—"} — الغرفة: {patient?.roomName ?? "—"}
        </div>
      </div>

      {/* Medical Alerts read-only */}
      <MedicalAlertsBadge alerts={medicalAlerts ?? []} />

      <div className="space-y-3">
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
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!localDiagnosis.trim()}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: BLUE, opacity: !localDiagnosis.trim() ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ الفحص
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Priced Procedures Modal — THE KEY FEATURE
   Loads real services from catalog, multi-select, auto-total
   ═══════════════════════════════════════════════════════════════════════════ */
export function PricedProceduresModal({
  open, onClose, patient, services, currentAmountDue, currentTreatmentDone, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  services: ServiceWithPrice[];
  currentAmountDue: number;
  currentTreatmentDone: string;
  onSave: (data: { treatmentDone: string; totalAmount: number; suggestedServiceId: string }) => void;
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
    const names = Array.from(selectedServices.values()).map(s =>
      s.quantity > 1 ? `${s.arabicName} ×${s.quantity}` : s.arabicName
    );
    const treatmentText = names.join(" + ") || currentTreatmentDone || "زيارة بدون رسوم";
    const firstServiceId = selectedServices.keys().next().value ?? "";

    onSave({
      treatmentDone: treatmentText,
      totalAmount: finalAmount,
      suggestedServiceId: firstServiceId,
    });
    setSelectedServices(new Map());
    setSearchService("");
    setIsFreeVisit(false);
  };

  return (
    <ModalShell open={open} onClose={onClose} title="الإجراءات المسعّرة" icon={ListChecks} iconColor="#9333ea" wide>
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#faf5ff" }}>
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
      <div className="mt-3 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#fff7ed" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
        <span className="text-[11px] font-medium" style={{ color: "#92400e" }}>
          لن يتم إنشاء دفعة مالية من هنا — يتم تسجيل المبلغ فقط كمرجع للاستقبال
        </span>
      </div>

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!isFreeVisit && selectedServices.size === 0}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#9333ea", opacity: !isFreeVisit && selectedServices.size === 0 ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          {isFreeVisit ? "تأكيد زيارة بدون رسوم" : `تأكيد الإجراءات (${fmtRial(finalAmount)})`}
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Treatment Plan Modal — Enhanced
   ═══════════════════════════════════════════════════════════════════════════ */
export function TreatmentPlanModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
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
    <ModalShell open={open} onClose={onClose} title="خطة العلاج" icon={FileText} iconColor="#2563eb" wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#eff6ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
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
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!hasAnyField}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#2563eb", opacity: !hasAnyField ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ خطة العلاج
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Orthodontic Follow-up Modal — Enhanced with detailed ortho fields
   ═══════════════════════════════════════════════════════════════════════════ */
export function OrthoFollowUpModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
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
  const [currentPhase, setCurrentPhase] = useState("");
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
    <ModalShell open={open} onClose={onClose} title="متابعة التقويم" icon={GitBranch} iconColor={ORANGE} wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#fff7ed" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
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
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!hasAnyField}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: !hasAnyField ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ المتابعة
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Images & Radiographs Modal — Placeholder
   TODO: Connect to file upload API when ready
   ═══════════════════════════════════════════════════════════════════════════ */
export function ImagesRadiographsModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: () => void;
}) {
  return (
    <ModalShell open={open} onClose={onClose} title="الأشعة والصور" icon={Image} iconColor="#64748b" wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f8fafc" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient?.serviceName ?? "—"} — الغرفة: {patient?.roomName ?? "—"}
        </div>
      </div>

      {/* Placeholder area for clinical images/radiographs */}
      <div className="border-2 border-dashed rounded-2xl p-8 flex flex-col items-center justify-center gap-4"
        style={{ borderColor: "#e2e8f0", background: "#fafafa" }}>
        <div className="w-16 h-16 rounded-2xl flex items-center justify-center" style={{ background: "#f1f5f9" }}>
          <Camera className="w-8 h-8" style={{ color: "#94a3b8" }} />
        </div>
        <div className="text-center">
          <div className="text-sm font-bold mb-1" style={{ color: NAVY }}>منطقة الأشعة والصور</div>
          <div className="text-xs font-medium" style={{ color: "#64748b" }}>
            سيتم عرض الأشعة والصور السريرية هنا
          </div>
        </div>

        {/* Upload placeholder */}
        <div className="flex gap-2">
          <button disabled
            className="px-4 py-2 rounded-lg text-xs font-bold flex items-center gap-2 opacity-50 cursor-not-allowed"
            style={{ background: "#f1f5f9", color: "#64748b" }}>
            <Upload className="w-3.5 h-3.5" />
            رفع أشعة
          </button>
          <button disabled
            className="px-4 py-2 rounded-lg text-xs font-bold flex items-center gap-2 opacity-50 cursor-not-allowed"
            style={{ background: "#f1f5f9", color: "#64748b" }}>
            <Camera className="w-3.5 h-3.5" />
            رفع صورة سريرية
          </button>
        </div>
      </div>

      {/* Development message */}
      <div className="mt-4 p-3 rounded-xl flex items-center gap-2" style={{ background: "#fef3c7", border: "1px solid #fde68a" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#d97706" }} />
        <span className="text-xs font-medium" style={{ color: "#92400e" }}>
          هذه الميزة قيد التطوير — سيتم إضافة رفع الصور والأشعة قريباً
        </span>
      </div>

      {/* TODO: Connect to file upload API when ready */}

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إغلاق</button>
        <button onClick={onSave}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#64748b" }}>
          <Check className="w-4 h-4" />
          حفظ مرجع الأشعة
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Lab Order Modal — Placeholder with TODO
   TODO: Connect to lab order API when backend is ready
   ═══════════════════════════════════════════════════════════════════════════ */
export function LabOrderModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: {
    labWorkType: string;
    shade: string;
    deliveryDate: string;
    labInstructions: string;
    referralDepartment: string;
  }) => void;
}) {
  const [labWorkType, setLabWorkType] = useState("");
  const [shade, setShade] = useState("");
  const [deliveryDate, setDeliveryDate] = useState("");
  const [labInstructions, setLabInstructions] = useState("");
  const [referralDepartment, setReferralDepartment] = useState("");

  const handleSave = () => {
    onSave({
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
    <ModalShell open={open} onClose={onClose} title="طلب مختبر / إحالة" icon={FlaskConical} iconColor="#0891b2" wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#ecfeff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient?.serviceName ?? "—"} — الغرفة: {patient?.roomName ?? "—"}
        </div>
      </div>

      <div className="space-y-3">
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
      </div>

      {/* TODO: Connect to lab order API when backend is ready */}

      {/* Info note */}
      <div className="mt-3 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#ecfeff" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#0891b2" }} />
        <span className="text-[11px] font-medium" style={{ color: "#155e75" }}>
          سيتم حفظ طلب المختبر كملاحظة سريرية — سيتم ربطه بنظام المختبر لاحقاً
        </span>
      </div>

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!hasAnyField}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#0891b2", opacity: !hasAnyField ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ طلب المختبر
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Prescription & Instructions Modal — Enhanced
   ═══════════════════════════════════════════════════════════════════════════ */
export function PrescriptionModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: { prescriptionText: string; instructions: string }) => void;
}) {
  const [prescriptionText, setPrescriptionText] = useState("");
  const [instructions, setInstructions] = useState("");

  const handleSave = () => {
    onSave({ prescriptionText, instructions });
    setPrescriptionText("");
    setInstructions("");
  };

  // WhatsApp instruction template preview (read-only)
  const whatsappPreview = instructions.trim()
    ? `التعليمات بعد العلاج:\n${instructions}${prescriptionText.trim() ? `\nالوصفة:\n${prescriptionText}` : ""}`
    : "";

  return (
    <ModalShell open={open} onClose={onClose} title="الوصفة والتعليمات" icon={Pill} iconColor="#dc2626" wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#fef2f2" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
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
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!prescriptionText.trim() && !instructions.trim()}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#dc2626", opacity: !prescriptionText.trim() && !instructions.trim() ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ الوصفة والتعليمات
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Follow-up Appointment Suggest Modal — Enhanced
   ═══════════════════════════════════════════════════════════════════════════ */
export function FollowUpSuggestModal({
  open, onClose, patient, services, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  services: ServiceWithPrice[];
  onSave: (data: { followUpDate: string; followUpTime: string; serviceId: string; reason: string }) => void;
}) {
  const [followUpDate, setFollowUpDate] = useState("");
  const [followUpTime, setFollowUpTime] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [reason, setReason] = useState("");

  const handleSave = () => {
    onSave({ followUpDate, followUpTime, serviceId, reason });
    setFollowUpDate("");
    setFollowUpTime("");
    setServiceId("");
    setReason("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="موعد متابعة" icon={CalendarClock} iconColor="#7c3aed" wide>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f5f3ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
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
            {services.map(s => (
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
            سيتم حفظ الاقتراح فقط — لن يتم إنشاء موعد تلقائياً
          </span>
        </div>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!followUpDate}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#7c3aed", opacity: !followUpDate ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          اقتراح الموعد
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Handoff to Reception Confirm Modal — Enhanced validation
   ═══════════════════════════════════════════════════════════════════════════ */
export function HandoffConfirmModal({
  open, onClose, patient, clinicalNotes, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  clinicalNotes: {
    diagnosis: string;
    treatmentDone: string;
    instructions: string;
    nextVisitPlan: string;
    amountDue: number;
    suggestedServiceId: string;
    followUpDate: string;
  };
  isPending: boolean;
  onConfirm: () => void;
}) {
  if (!patient) return null;

  const hasNotes = clinicalNotes.diagnosis || clinicalNotes.treatmentDone || clinicalNotes.amountDue > 0;
  const hasNoNotesAtAll = !clinicalNotes.diagnosis && !clinicalNotes.treatmentDone &&
    !clinicalNotes.instructions && !clinicalNotes.nextVisitPlan &&
    clinicalNotes.amountDue <= 0;

  // Count procedures from treatmentDone string
  const procedureCount = clinicalNotes.treatmentDone
    ? clinicalNotes.treatmentDone.split("+").filter(Boolean).length
    : 0;

  return (
    <ModalShell open={open} onClose={onClose} title="إنهاء وإرسال للاستقبال" icon={Send} iconColor={ORANGE} wide>
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#fff7ed" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient.serviceName ?? "—"} — الغرفة: {patient.roomName ?? "—"}
        </div>
      </div>

      {/* Strong warning when no clinical notes at all */}
      {hasNoNotesAtAll && (
        <div className="mb-4 p-3 rounded-xl flex items-start gap-2" style={{ background: "#fef2f2", border: "1px solid #fecaca" }}>
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
        <div className="mb-4 space-y-2">
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
        <div className="mb-4 p-3 rounded-lg flex items-center gap-2" style={{ background: "#fef3c7" }}>
          <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#d97706" }} />
          <span className="text-xs font-medium" style={{ color: "#92400e" }}>
            لم يتم تسجيل ملاحظات سريرية بعد — يفضل تسجيل التشخيص والإجراءات قبل الإرسال
          </span>
        </div>
      )}

      {/* Important note */}
      <div className="p-3 rounded-xl mb-4" style={{ background: "#f0fdf4", border: "1px solid #bbf7d0" }}>
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

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>تراجع</button>
        <button onClick={onConfirm} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
          إرسال للاستقبال
        </button>
      </div>
    </ModalShell>
  );
}
