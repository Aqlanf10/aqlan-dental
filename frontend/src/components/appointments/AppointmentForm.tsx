"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, AlertTriangle, CalendarDays } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { PatientListItem } from "@/types/patient";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

interface Doctor {
  id: string;
  name: string;
  specialty?: string;
  color?: string;
}

const schema = z.object({
  patientId:       z.string().min(1, "اختر مريضاً"),
  doctorId:        z.string().min(1, "اختر طبيباً"),
  appointmentDate: z.string().min(1, "التاريخ مطلوب"),
  startTime:       z.string().min(1, "وقت البداية مطلوب"),
  durationMinutes: z.number().min(5).max(240),
  appointmentType: z.string().min(1, "نوع الموعد مطلوب"),
  notes:           z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
    err ? "border-red-400" : "border-gray-300"
  );

interface Props {
  defaultPatientId?:   string;
  defaultPatientName?: string;
  appointmentId?:      string;
  editDefaults?: {
    doctorId:        string;
    appointmentDate: string;
    startTime:       string;
    durationMinutes: number;
    appointmentType: string;
    notes?:          string;
  };
}

export function AppointmentForm({ defaultPatientId, defaultPatientName, appointmentId, editDefaults }: Props) {
  const isEditMode = Boolean(appointmentId);
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [isConflict, setIsConflict] = useState(false);
  const [doctors, setDoctors] = useState<Doctor[]>([]);

  const {
    register,
    handleSubmit,
    setValue,
    control,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      durationMinutes: editDefaults?.durationMinutes ?? 30,
      patientId:       defaultPatientId ?? "",
      doctorId:        editDefaults?.doctorId ?? "",
      appointmentDate: editDefaults?.appointmentDate ?? new Date().toISOString().slice(0, 10),
      startTime:       editDefaults?.startTime ?? "",
      appointmentType: editDefaults?.appointmentType ?? "",
      notes:           editDefaults?.notes ?? "",
    },
  });

  const watchedDate   = useWatch({ control, name: "appointmentDate" });
  const watchedDoctor = useWatch({ control, name: "doctorId" });

  // Load doctors
  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    setIsConflict(false);
    try {
      const payload = {
        patientId:       data.patientId,
        doctorId:        data.doctorId,
        appointmentDate: data.appointmentDate,
        startTime:       data.startTime + ":00",
        durationMinutes: data.durationMinutes,
        appointmentType: data.appointmentType,
        notes:           data.notes,
      };
      if (isEditMode) {
        await api.put(`/api/appointments/${appointmentId}`, payload);
      } else {
        await api.post("/api/appointments", payload);
      }
      router.push(`/appointments?date=${data.appointmentDate}`);
    } catch (err: unknown) {
      const axiosErr = err as { response?: { status?: number; data?: { message?: string } } };
      const status = axiosErr?.response?.status;
      const msg    = axiosErr?.response?.data?.message;
      if (status === 409) {
        setIsConflict(true);
        setServerError(msg ?? "يوجد تعارض مع موعد آخر في هذا الوقت");
      } else {
        setServerError(msg ?? "حدث خطأ أثناء الحفظ");
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
      {serverError && (
        isConflict ? (
          <div className="bg-amber-50 border border-amber-300 rounded-lg p-4 flex gap-3">
            <AlertTriangle className="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" />
            <div className="flex-1">
              <p className="text-sm font-semibold text-amber-800">تعارض في المواعيد</p>
              <p className="text-sm text-amber-700 mt-0.5">{serverError}</p>
              {watchedDate && (
                <Link
                  href={`/appointments?date=${watchedDate}${watchedDoctor ? `&doctorId=${watchedDoctor}` : ""}`}
                  className="inline-flex items-center gap-1.5 mt-2 text-xs font-medium text-amber-700 underline underline-offset-2 hover:text-amber-900"
                >
                  <CalendarDays className="w-3.5 h-3.5" />
                  عرض جدول اليوم للاطلاع على الأوقات المتاحة
                </Link>
              )}
            </div>
          </div>
        ) : (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
            {serverError}
          </div>
        )
      )}

      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Patient */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            المريض <span className="text-red-500">*</span>
          </label>
          <PatientCombobox
            defaultDisplayValue={defaultPatientName ?? ""}
            onSelect={(p: PatientListItem) => setValue("patientId", p.id)}
            error={errors.patientId?.message}
            readOnly={isEditMode}
          />
          {errors.patientId && (
            <p className="mt-1 text-xs text-red-600">{errors.patientId.message}</p>
          )}
          <input type="hidden" {...register("patientId")} />
        </div>

        {/* Doctor */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            الطبيب <span className="text-red-500">*</span>
          </label>
          <select {...register("doctorId")} className={inputCls(errors.doctorId?.message)}>
            <option value="">اختر الطبيب...</option>
            {doctors.map((d) => (
              <option key={d.id} value={d.id}>{d.name}</option>
            ))}
          </select>
          {errors.doctorId && (
            <p className="mt-1 text-xs text-red-600">{errors.doctorId.message}</p>
          )}
        </div>

        {/* Appointment type */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            نوع الموعد <span className="text-red-500">*</span>
          </label>
          <input
            {...register("appointmentType")}
            className={inputCls(errors.appointmentType?.message)}
            placeholder="تقويم، حشو، قلع، فحص..."
          />
          {errors.appointmentType && (
            <p className="mt-1 text-xs text-red-600">{errors.appointmentType.message}</p>
          )}
        </div>

        {/* Date */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            التاريخ <span className="text-red-500">*</span>
          </label>
          <input
            {...register("appointmentDate")}
            type="date"
            className={inputCls(errors.appointmentDate?.message)}
          />
          {errors.appointmentDate && (
            <p className="mt-1 text-xs text-red-600">{errors.appointmentDate.message}</p>
          )}
        </div>

        {/* Start time */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            وقت البداية <span className="text-red-500">*</span>
          </label>
          <input
            {...register("startTime")}
            type="time"
            className={inputCls(errors.startTime?.message)}
          />
          {errors.startTime && (
            <p className="mt-1 text-xs text-red-600">{errors.startTime.message}</p>
          )}
        </div>

        {/* Duration */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">المدة (دقيقة)</label>
          <select
            {...register("durationMinutes", { valueAsNumber: true })}
            className={inputCls()}
          >
            {[15, 20, 30, 45, 60, 90, 120].map((m) => (
              <option key={m} value={m}>{m} دقيقة</option>
            ))}
          </select>
        </div>

        {/* Notes */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
          <textarea
            {...register("notes")}
            rows={2}
            className={inputCls()}
            placeholder="ملاحظات اختيارية..."
          />
        </div>
      </div>

      <div className="flex justify-end gap-3 pb-4">
        <button
          type="button"
          onClick={() => router.back()}
          className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
        >
          إلغاء
        </button>
        <button
          type="submit"
          disabled={saving}
          className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : isEditMode ? "حفظ التعديلات" : "حفظ الموعد"}
        </button>
      </div>
    </form>
  );
}
