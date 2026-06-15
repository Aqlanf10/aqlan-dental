"use client";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import type { OrthoVisit, CreateOrthoVisitRequest } from "@/types/ortho";
import api from "@/lib/api";
import { formatArabicDate, localDateString } from "@/lib/utils";
import { Plus, ChevronDown, ChevronUp, CalendarPlus, CheckCircle2 } from "lucide-react";

interface Props {
  caseId: string;
  visits: OrthoVisit[];
  onVisitAdded?: (visit: OrthoVisit) => void;
}

const schema = z.object({
  visitDate:       z.string().min(1, "التاريخ مطلوب"),
  visitType:       z.string().optional(),
  currentStage:    z.string().optional(),
  wireUpper:       z.string().optional(),
  wireLower:       z.string().optional(),
  elasticsType:    z.string().optional(),
  currentOverjet:  z.string().optional(),
  currentOverbite: z.string().optional(),
  clinicalNotes:   z.string().optional(),
  patientInstructions: z.string().optional(),
  nextAppointmentDate: z.string().optional(),
  nextAppointmentType: z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

export function OrthoVisitTimeline({ caseId, visits: initialVisits, onVisitAdded }: Props) {
  const [visits, setVisits] = useState(initialVisits);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [schedulingId, setSchedulingId] = useState<string | null>(null);
  const [scheduledVisits, setScheduledVisits] = useState<Set<string>>(new Set());
  const [scheduleError, setScheduleError] = useState<string | null>(null);

  const { register, handleSubmit, reset } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { visitDate: localDateString() }
  });

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    try {
      const req: CreateOrthoVisitRequest = {
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
      const { data: newVisit } = await api.post<OrthoVisit>(`/api/ortho-cases/${caseId}/visits`, req);
      setVisits([newVisit, ...visits]);
      onVisitAdded?.(newVisit);
      reset();
      setShowForm(false);
    } catch {
    } finally {
      setSaving(false);
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
      setScheduledVisits(previous => new Set(previous).add(visit.id));
    } catch (error: unknown) {
      const message =
        typeof error === "object"
        && error !== null
        && "response" in error
        && typeof (error as { response?: { data?: { message?: string } } }).response?.data?.message === "string"
          ? (error as { response: { data: { message: string } } }).response.data.message
          : "تعذر إنشاء موعد المتابعة";
      setScheduleError(message);
    } finally {
      setSchedulingId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button
          onClick={() => setShowForm(!showForm)}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          تسجيل زيارة
        </button>
      </div>

      {/* New Visit Form */}
      {showForm && (
        <form onSubmit={handleSubmit(onSubmit)} className="bg-clinic-blue-50 rounded-xl border border-clinic-blue-100 p-4 space-y-3">
          <h3 className="font-semibold text-clinic-navy text-sm">تسجيل زيارة جديدة</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">التاريخ *</label>
              <input {...register("visitDate")} type="date" className={inputCls} />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">نوع الزيارة</label>
              <select {...register("visitType")} className={inputCls}>
                <option value="">اختر...</option>
                <option value="activation">تنشيط</option>
                <option value="review">متابعة</option>
                <option value="bonding">تركيب</option>
                <option value="debonding">فك</option>
                <option value="retention">احتفاظ</option>
              </select>
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
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">الموعد التالي</label>
              <input {...register("nextAppointmentDate")} type="date" className={inputCls} />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">نوع الموعد التالي</label>
              <input {...register("nextAppointmentType")} className={inputCls} placeholder="تنشيط، متابعة..." />
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setShowForm(false)} className="px-4 py-1.5 text-sm border border-gray-300 rounded-lg hover:bg-gray-50 transition">
              إلغاء
            </button>
            <button type="submit" disabled={saving} className="px-4 py-1.5 text-sm bg-clinic-blue text-white rounded-lg hover:opacity-90 disabled:opacity-60 transition">
              {saving ? "جارٍ الحفظ..." : "حفظ الزيارة"}
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
                    <div className="text-xs text-gray-500">{v.visitType ?? "متابعة"}{v.doctorName ? ` · ${v.doctorName}` : ""}</div>
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
                  </div>
                  <div className="mt-4 flex flex-wrap items-center justify-between gap-2 border-t border-gray-100 pt-3">
                    <p className="text-xs text-gray-500">
                      {v.nextAppointmentDate
                        ? `التاريخ المقترح: ${v.nextAppointmentDate}`
                        : "سيُقترح الموعد تلقائياً بعد 21 يوماً من الزيارة"}
                    </p>
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
                  {scheduleError && expandedId === v.id && (
                    <p className="mt-2 text-xs font-medium text-red-600">{scheduleError}</p>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
