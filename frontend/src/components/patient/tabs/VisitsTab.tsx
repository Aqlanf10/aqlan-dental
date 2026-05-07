"use client";

import { useEffect, useState, useCallback } from "react";
import { ClipboardList, Plus, Pencil, Trash2, X, Stethoscope, ChevronDown, ChevronUp, Calendar } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

// ─── Types ──────────────────────────────────────────────────────────────────────

interface VisitDto {
  id: string;
  patientId: string;
  appointmentId?: string;
  visitDate: string;
  visitType?: string;
  specialty?: string;
  doctorId?: string;
  doctorName?: string;
  chiefComplaint?: string;
  clinicalNotes?: string;
  treatmentDone?: string;
  diagnosis?: string;
  instructions?: string;
  nextVisitPlan?: string;
  cost?: number;
  nextVisitDate?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

interface VisitForm {
  visitDate: string;
  visitType: string;
  specialty: string;
  doctorId: string;
  chiefComplaint: string;
  clinicalNotes: string;
  treatmentDone: string;
  diagnosis: string;
  instructions: string;
  nextVisitPlan: string;
  cost: string;
  nextVisitDate: string;
}

const EMPTY_FORM: VisitForm = {
  visitDate: new Date().toISOString().split("T")[0],
  visitType: "",
  specialty: "",
  doctorId: "",
  chiefComplaint: "",
  clinicalNotes: "",
  treatmentDone: "",
  diagnosis: "",
  instructions: "",
  nextVisitPlan: "",
  cost: "",
  nextVisitDate: "",
};

const VISIT_TYPES: Record<string, string> = {
  Consultation: "استشارة",
  FollowUp: "متابعة",
  Emergency: "طوارئ",
  Treatment: "علاج",
  Review: "مراجعة",
};

const SPECIALTY_LABELS: Record<string, string> = {
  General: "طب أسنان عام",
  Orthodontics: "تقويم أسنان",
  OralSurgery: "جراحة فم",
  Periodontics: "لثة",
  Endodontics: "علاج عصب",
  Prosthodontics: "تركيبات",
};

// ─── Component ──────────────────────────────────────────────────────────────────

interface VisitsTabProps {
  patientId: string;
}

export function VisitsTab({ patientId }: VisitsTabProps) {
  const [visits, setVisits] = useState<VisitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showModal, setShowModal] = useState(false);
  const [editingVisit, setEditingVisit] = useState<VisitDto | null>(null);
  const [form, setForm] = useState<VisitForm>({ ...EMPTY_FORM });
  const [saving, setSaving] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  const fetchVisits = useCallback(() => {
    setLoading(true);
    setError("");
    api.get<{ data: VisitDto[] }>(`/api/visits?patientId=${patientId}`)
      .then((r) => setVisits(r.data.data ?? []))
      .catch(() => setError("فشل تحميل الزيارات"))
      .finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchVisits();
  }, [fetchVisits]);

  const openAddModal = () => {
    setEditingVisit(null);
    setForm({ ...EMPTY_FORM });
    setShowModal(true);
  };

  const openEditModal = (visit: VisitDto) => {
    setEditingVisit(visit);
    setForm({
      visitDate: visit.visitDate,
      visitType: visit.visitType ?? "",
      specialty: visit.specialty ?? "",
      doctorId: visit.doctorId ?? "",
      chiefComplaint: visit.chiefComplaint ?? "",
      clinicalNotes: visit.clinicalNotes ?? "",
      treatmentDone: visit.treatmentDone ?? "",
      diagnosis: visit.diagnosis ?? "",
      instructions: visit.instructions ?? "",
      nextVisitPlan: visit.nextVisitPlan ?? "",
      cost: visit.cost?.toString() ?? "",
      nextVisitDate: visit.nextVisitDate ?? "",
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!form.visitDate) {
      toast.error("تاريخ الزيارة مطلوب");
      return;
    }
    setSaving(true);
    try {
      const payload: Record<string, unknown> = {
        patientId,
        visitDate: form.visitDate,
        visitType: form.visitType || null,
        specialty: form.specialty || null,
        doctorId: form.doctorId || null,
        chiefComplaint: form.chiefComplaint || null,
        clinicalNotes: form.clinicalNotes || null,
        treatmentDone: form.treatmentDone || null,
        diagnosis: form.diagnosis || null,
        instructions: form.instructions || null,
        nextVisitPlan: form.nextVisitPlan || null,
        cost: form.cost ? parseFloat(form.cost) : null,
        nextVisitDate: form.nextVisitDate || null,
      };

      if (editingVisit) {
        await api.put(`/api/visits/${editingVisit.id}`, payload);
        toast.success("تم تحديث الزيارة بنجاح");
      } else {
        await api.post("/api/visits", payload);
        toast.success("تم إضافة الزيارة بنجاح");
      }
      setShowModal(false);
      fetchVisits();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ الزيارة");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await api.delete(`/api/visits/${id}`);
      toast.success("تم حذف الزيارة بنجاح");
      setDeleteConfirm(null);
      fetchVisits();
    } catch {
      toast.error("فشل حذف الزيارة");
    }
  };

  const toggleExpand = (id: string) => {
    setExpandedId(prev => prev === id ? null : id);
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-bold text-[#0d2137]">الزيارات</h3>
        <button
          onClick={openAddModal}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          إضافة زيارة
        </button>
      </div>

      {/* Loading */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-20 bg-[#f1f5f9] rounded-lg" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-8 text-red-500 text-sm">{error}</div>
      ) : visits.length === 0 ? (
        <div className="text-center py-12 text-[#94a3b8]">
          <ClipboardList className="w-10 h-10 mx-auto mb-2 opacity-30" />
          <p className="text-sm">لا توجد زيارات مسجلة</p>
        </div>
      ) : (
        <div className="space-y-2">
          {visits.map((visit) => {
            const isExpanded = expandedId === visit.id;
            return (
              <div
                key={visit.id}
                className="border border-[#e8f0f9] rounded-lg overflow-hidden bg-white"
              >
                {/* Header row */}
                <button
                  onClick={() => toggleExpand(visit.id)}
                  className="w-full flex items-center justify-between p-3 hover:bg-[#f8fafc] transition text-right"
                >
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-lg bg-[#3d7ab518] flex items-center justify-center flex-shrink-0">
                      <Stethoscope className="w-5 h-5 text-[#3d7ab5]" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-sm font-medium text-[#0d2137]">
                          {formatArabicDate(visit.visitDate)}
                        </span>
                        {visit.visitType && (
                          <span className="text-xs px-2 py-0.5 rounded-full bg-[#3d7ab518] text-[#3d7ab5] font-medium">
                            {VISIT_TYPES[visit.visitType] ?? visit.visitType}
                          </span>
                        )}
                        {visit.specialty && (
                          <span className="text-xs text-[#64748b]">
                            {SPECIALTY_LABELS[visit.specialty] ?? visit.specialty}
                          </span>
                        )}
                      </div>
                      <div className="flex items-center gap-3 mt-0.5">
                        {visit.doctorName && (
                          <span className="text-xs text-[#64748b]">{visit.doctorName}</span>
                        )}
                        {visit.chiefComplaint && (
                          <span className="text-xs text-[#94a3b8] truncate max-w-[200px]">{visit.chiefComplaint}</span>
                        )}
                        {visit.appointmentId && (
                          <span className="flex items-center gap-1 text-xs text-[#3d7ab5]">
                            <Calendar className="w-3 h-3" />
                            مرتبط بموعد
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {visit.cost != null && visit.cost > 0 && (
                      <span className="text-xs font-medium text-[#3d7ab5]">{visit.cost.toLocaleString()} ر.ي</span>
                    )}
                    {isExpanded ? <ChevronUp className="w-4 h-4 text-[#94a3b8]" /> : <ChevronDown className="w-4 h-4 text-[#94a3b8]" />}
                  </div>
                </button>

                {/* Expanded detail */}
                {isExpanded && (
                  <div className="px-3 pb-3 pt-0 space-y-2 border-t border-[#f1f5f9]">
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-2">
                      {visit.chiefComplaint && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">الشكوى الرئيسية</p>
                          <p className="text-sm text-[#0d2137]">{visit.chiefComplaint}</p>
                        </div>
                      )}
                      {visit.diagnosis && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">التشخيص</p>
                          <p className="text-sm text-[#0d2137]">{visit.diagnosis}</p>
                        </div>
                      )}
                      {visit.treatmentDone && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">العلاج المنجز</p>
                          <p className="text-sm text-[#0d2137]">{visit.treatmentDone}</p>
                        </div>
                      )}
                      {visit.clinicalNotes && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">ملاحظات سريرية</p>
                          <p className="text-sm text-[#0d2137]">{visit.clinicalNotes}</p>
                        </div>
                      )}
                      {visit.instructions && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">التعليمات</p>
                          <p className="text-sm text-[#0d2137]">{visit.instructions}</p>
                        </div>
                      )}
                      {visit.nextVisitPlan && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">خطة الزيارة القادمة</p>
                          <p className="text-sm text-[#0d2137]">{visit.nextVisitPlan}</p>
                        </div>
                      )}
                      {visit.nextVisitDate && (
                        <div>
                          <p className="text-xs text-[#94a3b8]">موعد الزيارة القادمة</p>
                          <p className="text-sm text-[#0d2137]">{formatArabicDate(visit.nextVisitDate)}</p>
                        </div>
                      )}
                    </div>
                    <div className="flex items-center gap-2 pt-1">
                      <button
                        onClick={(e) => { e.stopPropagation(); openEditModal(visit); }}
                        className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                      >
                        <Pencil className="w-3 h-3" />
                        تعديل
                      </button>
                      {deleteConfirm === visit.id ? (
                        <div className="flex items-center gap-1">
                          <button
                            onClick={(e) => { e.stopPropagation(); handleDelete(visit.id); }}
                            className="px-2.5 py-1 text-xs font-medium rounded-lg bg-red-500 text-white hover:bg-red-600 transition"
                          >
                            تأكيد الحذف
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setDeleteConfirm(null); }}
                            className="px-2.5 py-1 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition"
                          >
                            إلغاء
                          </button>
                        </div>
                      ) : (
                        <button
                          onClick={(e) => { e.stopPropagation(); setDeleteConfirm(visit.id); }}
                          className="flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-lg text-red-500 hover:bg-red-50 transition"
                        >
                          <Trash2 className="w-3 h-3" />
                          حذف
                        </button>
                      )}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* ─── Add/Edit Modal ──────────────────────────────────────────────────── */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowModal(false)}>
          <div
            className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="p-6 space-y-4" dir="rtl">
              {/* Modal header */}
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">
                  {editingVisit ? "تعديل الزيارة" : "إضافة زيارة"}
                </h3>
                <button onClick={() => setShowModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Form */}
              <div className="space-y-3">
                {/* Date and Visit Type */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">تاريخ الزيارة *</label>
                    <input
                      type="date"
                      value={form.visitDate}
                      onChange={(e) => setForm({ ...form, visitDate: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">نوع الزيارة</label>
                    <select
                      value={form.visitType}
                      onChange={(e) => setForm({ ...form, visitType: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {Object.entries(VISIT_TYPES).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Specialty and Next Visit Date */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التخصص</label>
                    <select
                      value={form.specialty}
                      onChange={(e) => setForm({ ...form, specialty: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {Object.entries(SPECIALTY_LABELS).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">موعد الزيارة القادمة</label>
                    <input
                      type="date"
                      value={form.nextVisitDate}
                      onChange={(e) => setForm({ ...form, nextVisitDate: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                </div>

                {/* Chief Complaint */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">الشكوى الرئيسية</label>
                  <input
                    type="text"
                    value={form.chiefComplaint}
                    onChange={(e) => setForm({ ...form, chiefComplaint: e.target.value })}
                    placeholder="مثل: ألم في الضرس"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Diagnosis */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">التشخيص</label>
                  <input
                    type="text"
                    value={form.diagnosis}
                    onChange={(e) => setForm({ ...form, diagnosis: e.target.value })}
                    placeholder="مثل: التهاب لب سني"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Treatment Done */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">العلاج المنجز</label>
                  <textarea
                    value={form.treatmentDone}
                    onChange={(e) => setForm({ ...form, treatmentDone: e.target.value })}
                    placeholder="وصف العلاج المنجز"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Clinical Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات سريرية</label>
                  <textarea
                    value={form.clinicalNotes}
                    onChange={(e) => setForm({ ...form, clinicalNotes: e.target.value })}
                    placeholder="ملاحظات إضافية"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Instructions */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">التعليمات</label>
                  <textarea
                    value={form.instructions}
                    onChange={(e) => setForm({ ...form, instructions: e.target.value })}
                    placeholder="تعليمات للمريض"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Next Visit Plan */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">خطة الزيارة القادمة</label>
                  <textarea
                    value={form.nextVisitPlan}
                    onChange={(e) => setForm({ ...form, nextVisitPlan: e.target.value })}
                    placeholder="الخطة العلاجية للزيارة القادمة"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>

                {/* Cost */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">التكلفة (ر.ي)</label>
                  <input
                    type="number"
                    value={form.cost}
                    onChange={(e) => setForm({ ...form, cost: e.target.value })}
                    placeholder="0"
                    min="0"
                    step="0.01"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
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
                  {saving ? "جاري الحفظ..." : editingVisit ? "تحديث" : "إضافة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
