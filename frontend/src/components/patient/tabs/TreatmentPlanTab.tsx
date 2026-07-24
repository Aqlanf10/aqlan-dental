"use client";

import { useEffect, useState, useCallback } from "react";
import {
  ClipboardList, Plus, Pencil, Trash2, X,
  ArrowUp, ArrowDown, CheckCircle2, Clock, AlertTriangle,
  Ban, Pause, Stethoscope, Save, Link2,
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";
import { useDoctors } from "@/hooks/useDoctors";
import { EmptyState } from "./EmptyState";

// ─── Types ────────────────────────────────────────────────────────────────

interface TreatmentStep {
  id: string;
  patientId: string;
  sequenceNumber: number;
  serviceId?: string;
  serviceName?: string;
  serviceNameSnapshot?: string;
  department?: string;
  departmentArabic?: string;
  toothNumber?: string;
  toothArea?: string;
  title: string;
  description?: string;
  priority: string;
  priorityArabic: string;
  status: string;
  statusArabic: string;
  responsibleDoctorId?: string;
  doctorName?: string;
  plannedDate?: string;
  completedDate?: string;
  estimatedCost?: number;
  relatedAppointmentId?: string;
  relatedVisitId?: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

interface ServiceOption {
  id: string;
  arabicName: string;
  defaultPrice: number;
}

interface StepForm {
  serviceId: string;
  title: string;
  description: string;
  department: string;
  toothNumber: string;
  toothArea: string;
  priority: string;
  status: string;
  responsibleDoctorId: string;
  plannedDate: string;
  estimatedCost: string;
  notes: string;
}

// ─── Constants ────────────────────────────────────────────────────────────

const STATUS_LABELS: Record<string, string> = {
  Planned: "مخطط",
  InProgress: "قيد التنفيذ",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  Deferred: "مؤجل",
};

const STATUS_COLORS: Record<string, string> = {
  Planned: "bg-blue-50 text-blue-700",
  InProgress: "bg-amber-50 text-amber-700",
  Completed: "bg-green-50 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  Deferred: "bg-orange-50 text-orange-700",
};

const STATUS_ICONS: Record<string, typeof Clock> = {
  Planned: Clock,
  InProgress: Stethoscope,
  Completed: CheckCircle2,
  Cancelled: Ban,
  Deferred: Pause,
};

const PRIORITY_LABELS: Record<string, string> = {
  Low: "منخفض",
  Normal: "عادي",
  High: "مرتفع",
  Urgent: "عاجل",
};

const PRIORITY_COLORS: Record<string, string> = {
  Low: "bg-slate-50 text-slate-600",
  Normal: "bg-blue-50 text-blue-600",
  High: "bg-amber-50 text-amber-700",
  Urgent: "bg-red-50 text-red-700",
};

const DEPARTMENT_LABELS: Record<string, string> = {
  General: "طب الأسنان العام",
  Orthodontics: "التقويم",
  OralSurgery: "جراحة الفم والوجه والفكين",
  Periodontics: "أمراض اللثة",
  Endodontics: "علاج العصب",
  Prosthodontics: "التركيبات",
};

const EMPTY_FORM: StepForm = {
  serviceId: "",
  title: "",
  description: "",
  department: "",
  toothNumber: "",
  toothArea: "",
  priority: "Normal",
  status: "Planned",
  responsibleDoctorId: "",
  plannedDate: "",
  estimatedCost: "",
  notes: "",
};

const inputCls =
  "w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white";

// ─── Component ────────────────────────────────────────────────────────────

interface TreatmentPlanTabProps {
  patientId: string;
}

export function TreatmentPlanTab({ patientId }: TreatmentPlanTabProps) {
  const [steps, setSteps] = useState<TreatmentStep[]>([]);
  const [services, setServices] = useState<ServiceOption[]>([]);
  // CORE-PAT-002: the catalog failing must not look like "the clinic has no
  // services" — an empty dropdown with no explanation blocked adding a step.
  const [servicesError, setServicesError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Modal
  const [showModal, setShowModal] = useState(false);
  const [editingStep, setEditingStep] = useState<TreatmentStep | null>(null);
  const [form, setForm] = useState<StepForm>({ ...EMPTY_FORM });
  const [saving, setSaving] = useState(false);

  // Status change dropdown
  const [statusDropdownId, setStatusDropdownId] = useState<string | null>(null);

  // Delete confirm
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  // Doctors
  const { data: doctors = [] } = useDoctors();

  // ─── Data Loading ─────────────────────────────────────────────────────

  const fetchSteps = useCallback(() => {
    setLoading(true);
    setError("");
    api
      .get<TreatmentStep[]>(`/api/patients/${patientId}/treatment-plan`)
      .then((r) => setSteps(r.data))
      .catch(() => setError("فشل تحميل خطة العلاج"))
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchSteps();
  }, [fetchSteps]);

  useEffect(() => {
    api
      .get<ServiceOption[]>("/api/settings/services/active")
      .then((r) => { setServices(r.data); setServicesError(null); })
      .catch((err) => setServicesError(extractErrorMessage(err, "تعذر تحميل كتالوج الخدمات")));
  }, []);

  // ─── Summary Cards ───────────────────────────────────────────────────

  const totalSteps = steps.length;
  const plannedCount = steps.filter((s) => s.status === "Planned").length;
  const inProgressCount = steps.filter((s) => s.status === "InProgress").length;
  const completedCount = steps.filter((s) => s.status === "Completed").length;
  const deferredCancelledCount = steps.filter((s) => s.status === "Deferred" || s.status === "Cancelled").length;

  const summaryCards = [
    { label: "إجمالي الخطوات", count: totalSteps, bg: "bg-[#3d7ab518]", color: "text-[#3d7ab5]" },
    { label: "المخطط", count: plannedCount, bg: "bg-blue-50", color: "text-blue-700" },
    { label: "قيد التنفيذ", count: inProgressCount, bg: "bg-amber-50", color: "text-amber-700" },
    { label: "المكتمل", count: completedCount, bg: "bg-green-50", color: "text-green-700" },
    { label: "المؤجل / الملغي", count: deferredCancelledCount, bg: "bg-gray-50", color: "text-gray-600" },
  ];

  // ─── Handlers ────────────────────────────────────────────────────────

  const openAddModal = () => {
    setEditingStep(null);
    setForm({ ...EMPTY_FORM });
    setShowModal(true);
  };

  const openEditModal = (step: TreatmentStep) => {
    setEditingStep(step);
    setForm({
      serviceId: step.serviceId ?? "",
      title: step.title,
      description: step.description ?? "",
      department: step.department ?? "",
      toothNumber: step.toothNumber ?? "",
      toothArea: step.toothArea ?? "",
      priority: step.priority,
      status: step.status,
      responsibleDoctorId: step.responsibleDoctorId ?? "",
      plannedDate: step.plannedDate ?? "",
      estimatedCost: step.estimatedCost?.toString() ?? "",
      notes: step.notes ?? "",
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!form.title.trim()) {
      toast.error("عنوان الخطوة مطلوب");
      return;
    }
    setSaving(true);
    try {
      const payload = {
        serviceId: form.serviceId || null,
        title: form.title.trim(),
        description: form.description || null,
        department: form.department || null,
        toothNumber: form.toothNumber || null,
        toothArea: form.toothArea || null,
        priority: form.priority,
        status: form.status,
        responsibleDoctorId: form.responsibleDoctorId || null,
        plannedDate: form.plannedDate || null,
        estimatedCost: form.estimatedCost ? parseFloat(form.estimatedCost) : null,
        notes: form.notes || null,
      };

      if (editingStep) {
        await api.put(`/api/patients/${patientId}/treatment-plan/${editingStep.id}`, payload);
        toast.success("تم تحديث خطوة العلاج");
      } else {
        await api.post(`/api/patients/${patientId}/treatment-plan`, payload);
        toast.success("تمت إضافة خطوة العلاج");
      }
      setShowModal(false);
      fetchSteps();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ خطوة العلاج");
    } finally {
      setSaving(false);
    }
  };

  const handleStatusChange = async (stepId: string, newStatus: string) => {
    try {
      await api.patch(`/api/patients/${patientId}/treatment-plan/${stepId}/status`, { status: newStatus });
      toast.success("تم تغيير الحالة");
      setStatusDropdownId(null);
      fetchSteps();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل تغيير الحالة");
    }
  };

  const handleDelete = async (stepId: string) => {
    try {
      await api.delete(`/api/patients/${patientId}/treatment-plan/${stepId}`);
      toast.success("تم حذف خطوة العلاج");
      setDeleteConfirm(null);
      fetchSteps();
    } catch {
      toast.error("فشل حذف خطوة العلاج");
    }
  };

  const handleMoveStep = async (step: TreatmentStep, direction: "up" | "down") => {
    const currentIdx = steps.findIndex((s) => s.id === step.id);
    if (direction === "up" && currentIdx === 0) return;
    if (direction === "down" && currentIdx === steps.length - 1) return;

    const swapIdx = direction === "up" ? currentIdx - 1 : currentIdx + 1;
    const swapStep = steps[swapIdx];

    try {
      await api.patch(`/api/patients/${patientId}/treatment-plan/reorder`, {
        items: [
          { stepId: step.id, sequenceNumber: swapStep.sequenceNumber },
          { stepId: swapStep.id, sequenceNumber: step.sequenceNumber },
        ],
      });
      fetchSteps();
    } catch {
      toast.error("فشل إعادة الترتيب");
    }
  };

  // ─── Service name auto-fill ──────────────────────────────────────────

  const handleServiceSelect = (serviceId: string) => {
    const svc = services.find((s) => s.id === serviceId);
    if (svc) {
      setForm((prev) => ({
        ...prev,
        serviceId,
        title: prev.title || svc.arabicName,
        estimatedCost: prev.estimatedCost || (svc.defaultPrice > 0 ? svc.defaultPrice.toString() : ""),
      }));
    } else {
      setForm((prev) => ({ ...prev, serviceId }));
    }
  };

  // ─── Render ──────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-bold text-[#0d2137]">خطة العلاج</h3>
        <button
          onClick={openAddModal}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          إضافة خطوة
        </button>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
        {summaryCards.map(({ label, count, bg, color }) => (
          <div key={label} className={cn("rounded-xl px-3 py-2", bg)}>
            <p className="text-xs text-[#94a3b8]">{label}</p>
            <p className={cn("text-lg font-bold", color)}>{count}</p>
          </div>
        ))}
      </div>

      {/* Loading */}
      {loading ? (
        <div className="space-y-3 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-20 bg-[#f1f5f9] rounded-xl" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-10">
          <AlertTriangle className="w-10 h-10 mx-auto mb-2 text-red-300" />
          <p className="text-sm text-red-500">{error}</p>
          <button onClick={fetchSteps} className="mt-2 text-xs text-[#3d7ab5] hover:underline">
            إعادة المحاولة
          </button>
        </div>
      ) : steps.length === 0 ? (
        <EmptyState
          icon={ClipboardList}
          title="لا توجد خطوات علاجية"
          description="لم يتم إنشاء خطة علاج لهذا المريض بعد"
        />
      ) : (
        <div className="space-y-2">
          {steps.map((step, idx) => {
            const StatusIcon = STATUS_ICONS[step.status] ?? Clock;
            return (
              <div
                key={step.id}
                className="border border-[#e8f0f9] rounded-xl bg-white shadow-sm overflow-hidden"
              >
                <div className="flex items-center gap-3 p-3">
                  {/* Reorder handles */}
                  <div className="flex flex-col gap-0.5 flex-shrink-0">
                    <button
                      onClick={() => handleMoveStep(step, "up")}
                      disabled={idx === 0}
                      className="p-0.5 rounded hover:bg-[#f1f5f9] disabled:opacity-30"
                    >
                      <ArrowUp className="w-3.5 h-3.5 text-[#94a3b8]" />
                    </button>
                    <button
                      onClick={() => handleMoveStep(step, "down")}
                      disabled={idx === steps.length - 1}
                      className="p-0.5 rounded hover:bg-[#f1f5f9] disabled:opacity-30"
                    >
                      <ArrowDown className="w-3.5 h-3.5 text-[#94a3b8]" />
                    </button>
                  </div>

                  {/* Sequence number */}
                  <div className="w-7 h-7 rounded-lg bg-[#3d7ab518] flex items-center justify-center flex-shrink-0">
                    <span className="text-xs font-bold text-[#3d7ab5]">{step.sequenceNumber}</span>
                  </div>

                  {/* Main content */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-semibold text-[#0d2137] truncate">{step.title}</span>
                      {step.serviceName && (
                        <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-[#3d7ab518] text-[#3d7ab5] font-medium truncate max-w-[140px]">
                          {step.serviceName}
                        </span>
                      )}
                      {step.departmentArabic && (
                        <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-purple-50 text-purple-700 font-medium">
                          {step.departmentArabic}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-3 mt-0.5">
                      {step.toothNumber && (
                        <span className="text-xs text-[#64748b]">سن {step.toothNumber}</span>
                      )}
                      {step.toothArea && (
                        <span className="text-xs text-[#94a3b8] truncate max-w-[120px]">{step.toothArea}</span>
                      )}
                      {step.doctorName && (
                        <span className="text-xs text-[#64748b]">د. {step.doctorName}</span>
                      )}
                      {step.plannedDate && (
                        <span className="text-xs text-[#94a3b8]">{step.plannedDate}</span>
                      )}
                      {step.estimatedCost != null && step.estimatedCost > 0 && (
                        <span className="text-xs font-semibold text-[#3d7ab5]">
                          {step.estimatedCost.toLocaleString()} ر.ي
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Priority badge */}
                  <span className={cn("text-[10px] px-2 py-0.5 rounded-full font-medium flex-shrink-0", PRIORITY_COLORS[step.priority] ?? "")}>
                    {PRIORITY_LABELS[step.priority] ?? step.priority}
                  </span>

                  {/* Status badge + dropdown */}
                  <div className="relative flex-shrink-0">
                    <button
                      onClick={() => setStatusDropdownId(statusDropdownId === step.id ? null : step.id)}
                      className={cn("text-[10px] px-2.5 py-1 rounded-full font-medium flex items-center gap-1", STATUS_COLORS[step.status] ?? "")}
                    >
                      <StatusIcon className="w-3 h-3" />
                      {step.statusArabic}
                    </button>
                    {statusDropdownId === step.id && (
                      <div className="absolute left-0 top-full mt-1 bg-white border border-[#e8f0f9] rounded-lg shadow-lg z-20 min-w-[140px] py-1">
                        {Object.entries(STATUS_LABELS).map(([k, v]) => (
                          <button
                            key={k}
                            onClick={() => handleStatusChange(step.id, k)}
                            className={cn(
                              "w-full text-right text-xs px-3 py-1.5 hover:bg-[#f1f5f9] transition",
                              k === step.status ? "font-semibold text-[#3d7ab5]" : "text-[#0d2137]"
                            )}
                          >
                            {v}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-1 flex-shrink-0">
                    {(step.relatedAppointmentId || step.relatedVisitId) && (
                      <span className="flex items-center gap-0.5 text-[10px] text-[#3d7ab5] font-medium">
                        <Link2 className="w-3 h-3" />
                      </span>
                    )}
                    <button
                      onClick={() => openEditModal(step)}
                      className="p-1.5 rounded-lg hover:bg-[#eef3f9] transition text-[#3d7ab5]"
                      title="تعديل"
                    >
                      <Pencil className="w-3.5 h-3.5" />
                    </button>
                    {deleteConfirm === step.id ? (
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleDelete(step.id)}
                          className="px-2 py-1 text-[10px] font-medium rounded bg-red-500 text-white hover:bg-red-600"
                        >
                          تأكيد
                        </button>
                        <button
                          onClick={() => setDeleteConfirm(null)}
                          className="px-2 py-1 text-[10px] font-medium rounded border border-gray-300 text-gray-600"
                        >
                          إلغاء
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setDeleteConfirm(step.id)}
                        className="p-1.5 rounded-lg hover:bg-red-50 transition text-red-400"
                        title="حذف"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>
                </div>

                {/* Expanded notes/description */}
                {(step.description || step.notes) && (
                  <div className="px-3 pb-2 pt-0 border-t border-[#f1f5f9]">
                    {step.description && (
                      <p className="text-xs text-[#64748b] mt-1.5">{step.description}</p>
                    )}
                    {step.notes && (
                      <p className="text-xs text-[#94a3b8] mt-1 italic">ملاحظات: {step.notes}</p>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* ─── Add/Edit Modal ────────────────────────────────────────────── */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowModal(false)}>
          <div className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <div className="p-6 space-y-4">
              {/* Header */}
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">
                  {editingStep ? "تعديل خطوة العلاج" : "إضافة خطوة علاجية"}
                </h3>
                <button onClick={() => setShowModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Form */}
              <div className="space-y-3">
                {/* Service */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">الخدمة من الكتالوج</label>
                  <select value={form.serviceId} onChange={(e) => handleServiceSelect(e.target.value)} className={inputCls}>
                    <option value="">— اختر خدمة —</option>
                    {services.map((s) => (
                      <option key={s.id} value={s.id}>{s.arabicName}</option>
                    ))}
                  </select>
                  {servicesError && (
                    <p role="alert" className="mt-1 text-[11px] font-bold text-amber-700">
                      {servicesError} — يمكنك إدخال العنوان يدويًا.
                    </p>
                  )}
                </div>

                {/* Title */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">العنوان *</label>
                  <input
                    type="text"
                    value={form.title}
                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                    placeholder="مثل: حشوة ضرس، تنظيف جير"
                    className={inputCls}
                  />
                </div>

                {/* Description */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">الوصف</label>
                  <textarea
                    value={form.description}
                    onChange={(e) => setForm({ ...form, description: e.target.value })}
                    placeholder="وصف تفصيلي للخطوة"
                    rows={2}
                    className={cn(inputCls, "resize-none")}
                  />
                </div>

                {/* Department + Priority */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">القسم / التخصص</label>
                    <select value={form.department} onChange={(e) => setForm({ ...form, department: e.target.value })} className={inputCls}>
                      <option value="">— اختر —</option>
                      {Object.entries(DEPARTMENT_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">الأولوية</label>
                    <select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })} className={inputCls}>
                      {Object.entries(PRIORITY_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Tooth + Area */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">رقم السن</label>
                    <input
                      type="text"
                      value={form.toothNumber}
                      onChange={(e) => setForm({ ...form, toothNumber: e.target.value })}
                      placeholder="مثل: 36"
                      className={inputCls}
                    />
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">المنطقة</label>
                    <input
                      type="text"
                      value={form.toothArea}
                      onChange={(e) => setForm({ ...form, toothArea: e.target.value })}
                      placeholder="مثل: الأسفل يسار"
                      className={inputCls}
                    />
                  </div>
                </div>

                {/* Doctor + Planned Date */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">الطبيب المسؤول</label>
                    <select value={form.responsibleDoctorId} onChange={(e) => setForm({ ...form, responsibleDoctorId: e.target.value })} className={inputCls}>
                      <option value="">— اختر —</option>
                      {doctors.filter((d) => d.isActive).map((d) => (
                        <option key={d.id} value={d.id}>{d.name}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التاريخ المخطط</label>
                    <input
                      type="date"
                      value={form.plannedDate}
                      onChange={(e) => setForm({ ...form, plannedDate: e.target.value })}
                      className={inputCls}
                      dir="ltr"
                    />
                  </div>
                </div>

                {/* Estimated Cost + Status */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التكلفة التقديرية (ر.ي)</label>
                    <input
                      type="number"
                      value={form.estimatedCost}
                      onChange={(e) => setForm({ ...form, estimatedCost: e.target.value })}
                      placeholder="0"
                      min="0"
                      step="0.01"
                      className={inputCls}
                      dir="ltr"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">الحالة</label>
                    <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })} className={inputCls}>
                      {Object.entries(STATUS_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Cost reference note */}
                <div className="bg-blue-50 border border-blue-200 rounded-lg px-3 py-1.5 text-[10px] text-blue-700">
                  التكلفة التقديرية للمرجعية فقط ولا تؤثر على الحسابات المالية
                </div>

                {/* Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
                  <textarea
                    value={form.notes}
                    onChange={(e) => setForm({ ...form, notes: e.target.value })}
                    placeholder="ملاحظات إضافية"
                    rows={2}
                    className={cn(inputCls, "resize-none")}
                  />
                </div>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-2">
                <button
                  onClick={() => setShowModal(false)}
                  className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className={cn(
                    "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                    saving ? "bg-[#3d7ab5]/60 cursor-not-allowed" : "bg-[#3d7ab5] hover:bg-[#2d5e8e]"
                  )}
                >
                  <Save className="w-4 h-4 inline-block ml-1" />
                  {saving ? "جاري الحفظ..." : editingStep ? "تحديث" : "إضافة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
