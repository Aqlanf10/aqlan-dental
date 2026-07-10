"use client";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import type { OrthoVisit, CreateOrthoVisitRequest } from "@/types/ortho";
import api from "@/lib/api";
import { extractErrorMessage as extractApiError } from "@/lib/errors";
import { formatArabicDate, localDateString } from "@/lib/utils";
import {
  CalendarClock,
  CalendarPlus,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Pencil,
  Plus,
  Trash2,
  X,
} from "lucide-react";

interface Props {
  caseId: string;
  visits: OrthoVisit[];
  onVisitAdded?: (visit: OrthoVisit) => void;
}

const schema = z.object({
  visitDate:            z.string().min(1, "التاريخ مطلوب"),
  visitType:            z.string().optional(),
  currentStage:         z.string().optional(),
  wireUpper:            z.string().optional(),
  wireLower:            z.string().optional(),
  elasticsType:         z.string().optional(),
  currentOverjet:       z.string().optional(),
  currentOverbite:      z.string().optional(),
  clinicalNotes:        z.string().optional(),
  patientInstructions:  z.string().optional(),
  nextAppointmentDate:  z.string().optional(),
  nextAppointmentType:  z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

const VISIT_TYPE_LABELS: Record<string, string> = {
  activation: "تنشيط",
  review: "متابعة",
  bonding: "تركيب",
  debonding: "فك",
  retention: "احتفاظ",
};

/**
 * Sprint 3 — Carry-forward defaults for the "add visit" form. The orthodontist usually
 * continues with the same archwires and the same treatment stage until explicitly changed,
 * so pre-filling these from the most-recent visit saves ~3 clicks per visit. Date is always
 * pre-filled with today (localDateString — Yemen UTC+3, no ISO UTC drift per CLAUDE.md).
 *
 * Returns a FormData-shaped object suitable for useForm.reset() / defaultValues.
 */
function buildCarryForwardDefaults(visits: OrthoVisit[]): FormData {
  const latest = visits
    .slice()
    .sort((a, b) => (a.visitDate < b.visitDate ? 1 : a.visitDate > b.visitDate ? -1 : 0))[0];
  return {
    visitDate: localDateString(),
    visitType: latest?.visitType ?? "",
    currentStage: latest?.currentStage ?? "",
    wireUpper: latest?.wireUpper ?? "",
    wireLower: latest?.wireLower ?? "",
    elasticsType: latest?.elasticsType ?? "",
    currentOverjet: latest?.currentOverjet?.toString() ?? "",
    currentOverbite: latest?.currentOverbite?.toString() ?? "",
    clinicalNotes: "",
    patientInstructions: "",
    nextAppointmentDate: "",
    nextAppointmentType: "",
  };
}

/** Build a FormData object pre-filled from an existing visit (edit mode). */
function buildEditDefaults(visit: OrthoVisit): FormData {
  return {
    visitDate: visit.visitDate,
    visitType: visit.visitType ?? "",
    currentStage: visit.currentStage ?? "",
    wireUpper: visit.wireUpper ?? "",
    wireLower: visit.wireLower ?? "",
    elasticsType: visit.elasticsType ?? "",
    currentOverjet: visit.currentOverjet?.toString() ?? "",
    currentOverbite: visit.currentOverbite?.toString() ?? "",
    clinicalNotes: visit.clinicalNotes ?? "",
    patientInstructions: visit.patientInstructions ?? "",
    nextAppointmentDate: visit.nextAppointmentDate ?? "",
    nextAppointmentType: visit.nextAppointmentType ?? "",
  };
}

function toRequest(data: FormData): CreateOrthoVisitRequest {
  return {
    visitDate: data.visitDate,
    visitType: data.visitType,
    currentStage: data.currentStage,
    wireUpper: data.wireUpper,
    wireLower: data.wireLower,
    elasticsType: data.elasticsType,
    currentOverjet: data.currentOverjet ? parseFloat(data.currentOverjet) : undefined,
    currentOverbite: data.currentOverbite ? parseFloat(data.currentOverbite) : undefined,
    clinicalNotes: data.clinicalNotes,
    patientInstructions: data.patientInstructions,
    nextAppointmentDate: data.nextAppointmentDate,
    nextAppointmentType: data.nextAppointmentType,
  };
}

export function OrthoVisitTimeline({ caseId, visits: initialVisits, onVisitAdded }: Props) {
  const [visits, setVisits] = useState(initialVisits);
  const [showForm, setShowForm] = useState(false);
  const [editingVisitId, setEditingVisitId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [schedulingId, setSchedulingId] = useState<string | null>(null);
  const [scheduledVisits, setScheduledVisits] = useState<Set<string>>(new Set());
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const { register, handleSubmit, reset } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: buildCarryForwardDefaults(initialVisits),
  });

  // The latest visit (most-recent VisitDate) — shown as a compact summary card at the top
  // of the timeline so the orthodontist sees the current appliance state without expanding.
  const latestVisit = useMemo(() => {
    if (visits.length === 0) return null;
    return visits
      .slice()
      .sort((a, b) => (a.visitDate < b.visitDate ? 1 : a.visitDate > b.visitDate ? -1 : 0))[0];
  }, [visits]);

  const openCreateForm = () => {
    setEditingVisitId(null);
    setFormError(null);
    reset(buildCarryForwardDefaults(visits));
    setShowForm(true);
  };

  const openEditForm = (visit: OrthoVisit) => {
    setEditingVisitId(visit.id);
    setFormError(null);
    reset(buildEditDefaults(visit));
    setShowForm(true);
  };

  const closeForm = () => {
    setShowForm(false);
    setEditingVisitId(null);
    setFormError(null);
  };

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setFormError(null);
    try {
      const req = toRequest(data);
      if (editingVisitId) {
        const { data: updated } = await api.put<OrthoVisit>(
          `/api/ortho-cases/${caseId}/visits/${editingVisitId}`,
          req
        );
        setVisits((prev) =>
          prev.map((v) => (v.id === editingVisitId ? { ...v, ...updated } : v))
        );
      } else {
        const { data: newVisit } = await api.post<OrthoVisit>(
          `/api/ortho-cases/${caseId}/visits`,
          req
        );
        setVisits([newVisit, ...visits]);
        onVisitAdded?.(newVisit);
      }
      reset(buildCarryForwardDefaults(visits));
      setShowForm(false);
      setEditingVisitId(null);
    } catch (error: unknown) {
      const message = extractApiError(error, editingVisitId ? "تعذر تحديث الزيارة" : "تعذر تسجيل الزيارة");
      setFormError(message);
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = (visit: OrthoVisit) => {
    setDeletingId(visit.id);
  };

  const cancelDelete = () => setDeletingId(null);

  const performDelete = async (visitId: string) => {
    try {
      await api.delete(`/api/ortho-cases/${caseId}/visits/${visitId}`);
      setVisits((prev) => prev.filter((v) => v.id !== visitId));
      if (expandedId === visitId) setExpandedId(null);
    } catch (error: unknown) {
      setFormError(extractApiError(error, "تعذر حذف الزيارة"));
    } finally {
      setDeletingId(null);
    }
  };

  const createNextAppointment = async (visit: OrthoVisit) => {
    setSchedulingId(visit.id);
    setScheduleError(null);
    try {
      await api.post(`/api/ortho-cases/${caseId}/visits/${visit.id}/next-appointment`, {
        appointmentDate: visit.nextAppointmentDate || undefined,
        appointmentType: visit.nextAppointmentType || "OrthoFollowUp",
      });
      setScheduledVisits((previous) => new Set(previous).add(visit.id));
    } catch (error: unknown) {
      setScheduleError(extractApiError(error, "تعذر إنشاء موعد المتابعة"));
    } finally {
      setSchedulingId(null);
    }
  };

  return (
    <div className="space-y-4">
      {/* Header: count badge + add button */}
      <div className="flex items-center justify-between">
        <div className="inline-flex items-center gap-2 rounded-full bg-clinic-blue-50 px-3 py-1 text-xs font-bold text-clinic-blue">
          <CalendarClock className="h-3.5 w-3.5" />
          {visits.length} زيارة
        </div>
        <button
          type="button"
          onClick={openCreateForm}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          تسجيل زيارة
        </button>
      </div>

      {/* Last-visit summary card — at-a-glance current appliance state */}
      {latestVisit && !showForm && (
        <div className="rounded-lg border border-clinic-blue-100 bg-clinic-blue-50/40 p-4">
          <div className="flex items-center gap-2 text-xs font-bold text-clinic-navy">
            <CalendarClock className="h-4 w-4" />
            آخر زيارة — {formatArabicDate(latestVisit.visitDate)}
            {latestVisit.visitType && (
              <span className="rounded bg-white/70 px-1.5 py-0.5 text-[10px] font-medium text-clinic-blue">
                {VISIT_TYPE_LABELS[latestVisit.visitType] ?? latestVisit.visitType}
              </span>
            )}
          </div>
          <div className="mt-2 grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
            {latestVisit.currentStage && (
              <div>
                <p className="text-gray-400">المرحلة الحالية</p>
                <p className="text-sm font-medium text-gray-900">{latestVisit.currentStage}</p>
              </div>
            )}
            {latestVisit.wireUpper && (
              <div>
                <p className="text-gray-400">السلك العلوي</p>
                <p className="text-sm font-medium text-gray-900">{latestVisit.wireUpper}</p>
              </div>
            )}
            {latestVisit.wireLower && (
              <div>
                <p className="text-gray-400">السلك السفلي</p>
                <p className="text-sm font-medium text-gray-900">{latestVisit.wireLower}</p>
              </div>
            )}
            {latestVisit.nextAppointmentDate && (
              <div>
                <p className="text-gray-400">الموعد التالي</p>
                <p className="text-sm font-medium text-gray-900">
                  {formatArabicDate(latestVisit.nextAppointmentDate)}
                </p>
              </div>
            )}
            {latestVisit.clinicalNotes && (
              <div className="col-span-2 md:col-span-4">
                <p className="text-gray-400">آخر ملاحظة</p>
                <p className="text-sm text-gray-700 line-clamp-2">{latestVisit.clinicalNotes}</p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* New/Edit Visit Form (reused RHF form, mode toggled by editingVisitId) */}
      {showForm && (
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="bg-clinic-blue-50 rounded-xl border border-clinic-blue-100 p-4 space-y-3"
        >
          <div className="flex items-center justify-between">
            <h3 className="font-semibold text-clinic-navy text-sm">
              {editingVisitId ? "تعديل بيانات الزيارة" : "تسجيل زيارة جديدة"}
            </h3>
            <button
              type="button"
              onClick={closeForm}
              className="rounded-lg p-1 text-gray-400 hover:text-gray-600"
              aria-label="إغلاق"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">التاريخ *</label>
              <input {...register("visitDate")} type="date" className={inputCls} />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">نوع الزيارة</label>
              <select {...register("visitType")} className={inputCls}>
                {/* Backend requires VisitType (NotEmpty). Default to "review" (متابعة)
                    — the most common visit type — so the form validates even if the
                    user doesn't change this select. */}
                <option value="review">متابعة</option>
                <option value="activation">تنشيط</option>
                <option value="bonding">تركيب</option>
                <option value="debonding">فك</option>
                <option value="retention">احتفاظ</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">المرحلة الحالية</label>
              <input {...register("currentStage")} className={inputCls} placeholder="Alignment / Leveling / ..." />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Elastics</label>
              <input {...register("elasticsType")} className={inputCls} placeholder="Class II / Triangle / ..." />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">السلك العلوي</label>
              <input {...register("wireUpper")} className={inputCls} placeholder="0.014 NiTi" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">السلك السفلي</label>
              <input {...register("wireLower")} className={inputCls} placeholder="0.014 NiTi" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Overjet (mm)</label>
              <input {...register("currentOverjet")} type="number" step="0.1" className={inputCls} />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Overbite (mm)</label>
              <input {...register("currentOverbite")} type="number" step="0.1" className={inputCls} />
            </div>
            <div className="md:col-span-2">
              <label className="block text-xs font-medium text-gray-700 mb-1">الملاحظات السريرية</label>
              <textarea {...register("clinicalNotes")} rows={2} className={inputCls} />
            </div>
            <div className="md:col-span-2">
              <label className="block text-xs font-medium text-gray-700 mb-1">تعليمات للمريض</label>
              <textarea {...register("patientInstructions")} rows={2} className={inputCls} />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">الموعد التالي</label>
              <input {...register("nextAppointmentDate")} type="date" className={inputCls} />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">نوع الموعد التالي</label>
              <input {...register("nextAppointmentType")} className={inputCls} placeholder="تنشيط، متابعة..." />
            </div>
          </div>
          {formError && (
            <p className="text-xs font-medium text-red-600">{formError}</p>
          )}
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={closeForm}
              className="px-4 py-1.5 text-sm border border-gray-300 rounded-lg hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={saving}
              className="px-4 py-1.5 text-sm bg-clinic-blue text-white rounded-lg hover:opacity-90 disabled:opacity-60 transition"
            >
              {saving
                ? "جارٍ الحفظ..."
                : editingVisitId
                  ? "حفظ التعديلات"
                  : "حفظ الزيارة"}
            </button>
          </div>
        </form>
      )}

      {/* Visits list */}
      {visits.length === 0 ? (
        <p className="text-gray-400 text-sm text-center py-8">لا توجد زيارات مسجلة</p>
      ) : (
        <div className="space-y-3">
          {visits.map((v) => (
            <div key={v.id} className="bg-white rounded-lg border border-gray-200 overflow-hidden">
              <button
                type="button"
                onClick={() => setExpandedId(expandedId === v.id ? null : v.id)}
                className="w-full flex items-center justify-between px-4 py-3 text-start hover:bg-gray-50 transition"
              >
                <div className="flex items-center gap-3">
                  <span className="w-7 h-7 rounded-full bg-clinic-blue/10 text-clinic-blue text-xs font-bold flex items-center justify-center flex-shrink-0">
                    {v.visitNumber}
                  </span>
                  <div>
                    <div className="text-sm font-semibold text-gray-900">{formatArabicDate(v.visitDate)}</div>
                    <div className="text-xs text-gray-500">
                      {v.visitType ? (VISIT_TYPE_LABELS[v.visitType] ?? v.visitType) : "متابعة"}
                      {v.doctorName ? ` · ${v.doctorName}` : ""}
                    </div>
                  </div>
                </div>
                {expandedId === v.id ? <ChevronUp className="w-4 h-4 text-gray-400" /> : <ChevronDown className="w-4 h-4 text-gray-400" />}
              </button>

              {expandedId === v.id && (
                <div className="px-4 pb-4 pt-3 border-t border-gray-100">
                  <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                  {[
                    ["السلك العلوي", v.wireUpper],
                    ["السلك السفلي", v.wireLower],
                    ["Elastics", v.elasticsType],
                    ["Overjet", v.currentOverjet ? `${v.currentOverjet} mm` : null],
                    ["Overbite", v.currentOverbite ? `${v.currentOverbite} mm` : null],
                    ["الموعد التالي", v.nextAppointmentDate ? `${v.nextAppointmentDate} · ${v.nextAppointmentType ?? ""}` : null],
                  ].filter(([, val]) => val).map(([label, val]) => (
                    <div key={label as string}>
                      <p className="text-xs text-gray-400">{label}</p>
                      <p className="text-sm font-medium text-gray-900">{val}</p>
                    </div>
                  ))}
                  {v.clinicalNotes && (
                    <div className="col-span-2 md:col-span-3">
                      <p className="text-xs text-gray-400">الملاحظات</p>
                      <p className="text-sm text-gray-700">{v.clinicalNotes}</p>
                    </div>
                  )}
                  {v.patientInstructions && (
                    <div className="col-span-2 md:col-span-3">
                      <p className="text-xs text-gray-400">تعليمات للمريض</p>
                      <p className="text-sm text-gray-700">{v.patientInstructions}</p>
                    </div>
                  )}
                  </div>
                  <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-gray-100 pt-3">
                    <p className="text-xs text-gray-500">
                      {v.nextAppointmentDate
                        ? `التاريخ المقترح: ${v.nextAppointmentDate}`
                        : "سيُقترح الموعد تلقائياً بعد 21 يوماً من الزيارة"}
                    </p>
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => openEditForm(v)}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 transition hover:bg-gray-50"
                      >
                        <Pencil className="h-3.5 w-3.5" />
                        تعديل
                      </button>
                      <button
                        type="button"
                        onClick={() => confirmDelete(v)}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-red-200 bg-red-50 px-3 py-1.5 text-xs font-medium text-red-700 transition hover:bg-red-100"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                        حذف
                      </button>
                      <button
                        type="button"
                        onClick={() => createNextAppointment(v)}
                        disabled={schedulingId === v.id || scheduledVisits.has(v.id)}
                        className="inline-flex items-center gap-2 rounded-lg border border-violet-200 bg-violet-50 px-3 py-1.5 text-xs font-bold text-violet-700 transition hover:bg-violet-100 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {scheduledVisits.has(v.id)
                          ? <CheckCircle2 className="h-4 w-4" />
                          : <CalendarPlus className="h-4 w-4" />}
                        {scheduledVisits.has(v.id)
                          ? "تم إنشاء الموعد"
                          : schedulingId === v.id
                            ? "جارٍ الإنشاء..."
                            : "إنشاء الموعد التالي"}
                      </button>
                    </div>
                  </div>
                  {scheduleError && expandedId === v.id && (
                    <p className="mt-2 text-xs font-medium text-red-600">{scheduleError}</p>
                  )}

                  {/* Inline delete confirmation */}
                  {deletingId === v.id && (
                    <div className="mt-3 rounded-lg border border-red-200 bg-red-50 p-3">
                      <p className="text-xs font-medium text-red-800">
                        هل أنت متأكد من حذف زيارة رقم {v.visitNumber} بتاريخ {formatArabicDate(v.visitDate)}؟
                        سيتم إلغاء ربط زيارة اليوميات المقابلة (بدون حذفها — قد تحمل مدفوعات).
                      </p>
                      <div className="mt-2 flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={cancelDelete}
                          className="rounded-lg border border-gray-300 bg-white px-3 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                        >
                          إلغاء
                        </button>
                        <button
                          type="button"
                          onClick={() => performDelete(v.id)}
                          className="rounded-lg bg-red-600 px-3 py-1 text-xs font-medium text-white hover:bg-red-700"
                        >
                          نعم، احذف
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {formError && !showForm && (
        <p className="text-xs font-medium text-red-600">{formError}</p>
      )}
    </div>
  );
}
