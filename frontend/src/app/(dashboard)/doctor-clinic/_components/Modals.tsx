/**
 * Modal components for Doctor Clinic Workspace.
 * StartVisit, Examination, PricedProcedures, TreatmentPlan,
 * OrthoFollowUp, Prescription, FollowUpSuggest, HandoffConfirm.
 */

"use client";

import { useState, useMemo } from "react";
import {
  X, Play, ClipboardCheck, ListChecks, FileText, GitBranch,
  Pill, CalendarClock, Send, Loader2, AlertCircle, Check,
  Plus, Minus, Trash2, ChevronLeft, Stethoscope,
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
   Examination & Diagnosis Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function ExaminationModal({
  open, onClose, patient, diagnosis, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  diagnosis: string;
  onSave: (data: { diagnosis: string; treatmentDone: string }) => void;
}) {
  const [localDiagnosis, setLocalDiagnosis] = useState(diagnosis);
  const [treatmentDone, setTreatmentDone] = useState("");

  const handleSave = () => {
    onSave({ diagnosis: localDiagnosis, treatmentDone });
    setLocalDiagnosis("");
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
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>التشخيص *</label>
          <textarea value={localDiagnosis} onChange={e => setLocalDiagnosis(e.target.value)}
            rows={3} placeholder="مثال: تسوس سطحي في الضرس الثاني العلوي الأيسر" className={inputCls()} />
        </div>
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
  const [customAmount, setCustomAmount] = useState("");

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

  const finalAmount = customAmount ? parseFloat(customAmount) || 0 : totalAmount;

  const handleSave = () => {
    const names = Array.from(selectedServices.values()).map(s =>
      s.quantity > 1 ? `${s.arabicName} ×${s.quantity}` : s.arabicName
    );
    const treatmentText = names.join(" + ") || currentTreatmentDone;
    const firstServiceId = selectedServices.keys().next().value ?? "";

    onSave({
      treatmentDone: treatmentText,
      totalAmount: finalAmount,
      suggestedServiceId: firstServiceId,
    });
    setSelectedServices(new Map());
    setSearchService("");
    setCustomAmount("");
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

          {/* Total + Custom amount */}
          <div className="mt-3 p-3 rounded-xl" style={{ background: "#9333ea08", border: "1px solid #9333ea20" }}>
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-bold" style={{ color: NAVY }}>الإجمالي المحسوب</span>
              <span className="text-sm font-extrabold" style={{ color: "#9333ea" }}>{fmtRial(totalAmount)}</span>
            </div>
            <div>
              <label className="text-[10px] font-semibold block mb-1" style={{ color: "#64748b" }}>
                تعديل المبلغ يدوياً (اختياري)
              </label>
              <input type="number" value={customAmount} onChange={e => setCustomAmount(e.target.value)}
                placeholder={String(totalAmount)} className={inputCls()} dir="ltr" min={0} step={0.01} />
            </div>
            {customAmount && parseFloat(customAmount) !== totalAmount && (
              <div className="text-[10px] font-medium mt-1 flex items-center gap-1" style={{ color: ORANGE }}>
                <AlertCircle className="w-3 h-3" />
                المبلغ النهائي: {fmtRial(finalAmount)}
              </div>
            )}
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
        <button onClick={handleSave} disabled={selectedServices.size === 0 && !customAmount}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#9333ea", opacity: selectedServices.size === 0 && !customAmount ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          تأكيد الإجراءات ({fmtRial(finalAmount)})
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Treatment Plan Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function TreatmentPlanModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: { plan: string }) => void;
}) {
  const [plan, setPlan] = useState("");

  const handleSave = () => {
    onSave({ plan });
    setPlan("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="خطة العلاج" icon={FileText} iconColor="#2563eb">
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#eff6ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خطة العلاج المقترحة</label>
          <textarea value={plan} onChange={e => setPlan(e.target.value)}
            rows={5} placeholder="وصف خطة العلاج التفصيلية..." className={inputCls()} />
        </div>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!plan.trim()}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#2563eb", opacity: !plan.trim() ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ خطة العلاج
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Orthodontic Follow-up Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function OrthoFollowUpModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: { notes: string; nextVisitPlan: string }) => void;
}) {
  const [notes, setNotes] = useState("");
  const [nextPlan, setNextPlan] = useState("");

  const handleSave = () => {
    onSave({ notes, nextVisitPlan: nextPlan });
    setNotes(""); setNextPlan("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="متابعة التقويم" icon={GitBranch} iconColor={ORANGE}>
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#fff7ed" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات الجلسة</label>
          <textarea value={notes} onChange={e => setNotes(e.target.value)}
            rows={3} placeholder="تسجيل تفاصيل تعديل التقويم..." className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خطة الزيارة القادمة</label>
          <input value={nextPlan} onChange={e => setNextPlan(e.target.value)}
            placeholder="مثال: تعديل سلك، تغيير مطاط" className={inputCls()} />
        </div>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!notes.trim()}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: !notes.trim() ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ المتابعة
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Prescription & Instructions Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function PrescriptionModal({
  open, onClose, patient, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  onSave: (data: { instructions: string }) => void;
}) {
  const [instructions, setInstructions] = useState("");

  const handleSave = () => {
    onSave({ instructions });
    setInstructions("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="الوصفة والتعليمات" icon={Pill} iconColor="#dc2626">
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#fef2f2" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>التعليمات للمريض</label>
          <textarea value={instructions} onChange={e => setInstructions(e.target.value)}
            rows={4} placeholder="مثال: عدم أكل الأطعمة الصلبة لمدة 24 ساعة، مضاد حيوي مرتين يومياً لمدة 5 أيام..." className={inputCls()} />
        </div>
        <div className="p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#f0f5fb" }}>
          <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
          <span className="text-[11px] font-medium" style={{ color: NAVY }}>
            لإنشاء وصفة طبية رسمية، استخدم قسم الوصفات من القائمة الجانبية
          </span>
        </div>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSave} disabled={!instructions.trim()}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#dc2626", opacity: !instructions.trim() ? 0.5 : 1 }}>
          <Check className="w-4 h-4" />
          حفظ التعليمات
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Follow-up Appointment Suggest Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function FollowUpSuggestModal({
  open, onClose, patient, services, onSave,
}: {
  open: boolean; onClose: () => void;
  patient: DoctorPatientItem | null;
  services: ServiceWithPrice[];
  onSave: (data: { followUpDate: string; serviceId: string }) => void;
}) {
  const [followUpDate, setFollowUpDate] = useState("");
  const [serviceId, setServiceId] = useState("");

  const handleSave = () => {
    onSave({ followUpDate, serviceId });
    setFollowUpDate(""); setServiceId("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="موعد متابعة" icon={CalendarClock} iconColor="#7c3aed">
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f5f3ff" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient?.patientName}</div>
      </div>
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>تاريخ المتابعة المقترح *</label>
          <input type="date" value={followUpDate} onChange={e => setFollowUpDate(e.target.value)} className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>خدمة المتابعة</label>
          <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
            <option value="">اختر الخدمة</option>
            {services.map(s => (
              <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${fmtRial(s.defaultPrice)}` : ""}</option>
            ))}
          </select>
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
   Handoff to Reception Confirm Modal — THE KEY HANDOFF FLOW
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

  return (
    <ModalShell open={open} onClose={onClose} title="إنهاء وإرسال للاستقبال" icon={Send} iconColor={ORANGE} wide>
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#fff7ed" }}>
        <div className="font-bold text-sm" style={{ color: NAVY }}>{patient.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الخدمة: {patient.serviceName ?? "—"} — الغرفة: {patient.roomName ?? "—"}
        </div>
      </div>

      {/* Clinical summary */}
      {hasNotes ? (
        <div className="mb-4 space-y-2">
          <div className="text-xs font-bold mb-2" style={{ color: NAVY }}>ملخص سيرسل للاستقبال:</div>
          <div className="grid grid-cols-2 gap-2">
            {clinicalNotes.diagnosis && (
              <div className="p-2.5 rounded-lg" style={{ background: "#f0f5fb" }}>
                <div className="text-[10px] font-bold" style={{ color: BLUE }}>التشخيص</div>
                <div className="text-xs font-medium" style={{ color: NAVY }}>{clinicalNotes.diagnosis}</div>
              </div>
            )}
            {clinicalNotes.treatmentDone && (
              <div className="p-2.5 rounded-lg" style={{ background: "#faf5ff" }}>
                <div className="text-[10px] font-bold" style={{ color: "#9333ea" }}>العلاج المنفذ</div>
                <div className="text-xs font-medium" style={{ color: NAVY }}>{clinicalNotes.treatmentDone}</div>
              </div>
            )}
            {clinicalNotes.amountDue > 0 && (
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
      ) : (
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
