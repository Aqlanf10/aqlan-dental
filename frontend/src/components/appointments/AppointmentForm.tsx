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
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" dir="rtl">
      {serverError && (
        isConflict ? (
          <div
            className="border rounded-lg p-4 flex gap-3"
            style={{
              backgroundColor: "#f59e0b18",
              borderColor: "#f59e0b40",
            }}
          >
            <AlertTriangle className="w-5 h-5 flex-shrink-0 mt-0.5" style={{ color: "#f59e0b" }} />
            <div className="flex-1">
              <p className="text-sm font-semibold" style={{ color: "#d97706" }}>تعارض في المواعيد</p>
              <p className="text-sm mt-0.5" style={{ color: "#92400e" }}>{serverError}</p>
              {watchedDate && (
                <Link
                  href={`/appointments?date=${watchedDate}${watchedDoctor ? `&doctorId=${watchedDoctor}` : ""}`}
                  className="inline-flex items-center gap-1.5 mt-2 text-xs font-medium underline underline-offset-2"
                  style={{ color: "#92400e" }}
                >
                  <CalendarDays className="w-3.5 h-3.5" />
                  عرض جدول اليوم للاطلاع على الأوقات المتاحة
                </Link>
              )}
            </div>
          </div>
        ) : (
          <div
            className="border rounded-lg p-3 text-sm"
            style={{
              backgroundColor: "#ef444418",
              borderColor: "#ef444440",
              color: "#ef4444",
            }}
          >
            {serverError}
          </div>
        )
      )}

      <div
        className="rounded-xl border p-5 grid grid-cols-1 md:grid-cols-2 gap-4"
        style={{
          backgroundColor: "#ffffff",
          borderColor: "#e8f0f9",
          boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
        }}
      >
        {/* Patient */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>
            المريض <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <PatientCombobox
            defaultDisplayValue={defaultPatientName ?? ""}
            onSelect={(p: PatientListItem) => setValue("patientId", p.id)}
            error={errors.patientId?.message}
            readOnly={isEditMode}
          />
          {errors.patientId && (
            <p className="mt-1 text-xs" style={{ color: "#ef4444" }}>{errors.patientId.message}</p>
          )}
          <input type="hidden" {...register("patientId")} />
        </div>

        {/* Doctor */}
        <div>
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>
            الطبيب <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <select {...register("doctorId")} className={cn("aqlan-select", errors.doctorId && "border-[#ef4444]")}>
            <option value="">اختر الطبيب...</option>
            {doctors.map((d) => (
              <option key={d.id} value={d.id}>{d.name}</option>
            ))}
          </select>
          {errors.doctorId && (
            <p className="mt-1 text-xs" style={{ color: "#ef4444" }}>{errors.doctorId.message}</p>
          )}
        </div>

        {/* Appointment type */}
        <div>
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>
            نوع الموعد <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <input
            {...register("appointmentType")}
            className={cn("aqlan-input", errors.appointmentType && "border-[#ef4444]")}
            placeholder="تقويم، حشو، قلع، فحص..."
          />
          {errors.appointmentType && (
            <p className="mt-1 text-xs" style={{ color: "#ef4444" }}>{errors.appointmentType.message}</p>
          )}
        </div>

        {/* Date */}
        <div>
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>
            التاريخ <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <input
            {...register("appointmentDate")}
            type="date"
            className={cn("aqlan-input", errors.appointmentDate && "border-[#ef4444]")}
          />
          {errors.appointmentDate && (
            <p className="mt-1 text-xs" style={{ color: "#ef4444" }}>{errors.appointmentDate.message}</p>
          )}
        </div>

        {/* Start time */}
        <div>
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>
            وقت البداية <span style={{ color: "#ef4444" }}>*</span>
          </label>
          <input
            {...register("startTime")}
            type="time"
            className={cn("aqlan-input", errors.startTime && "border-[#ef4444]")}
          />
          {errors.startTime && (
            <p className="mt-1 text-xs" style={{ color: "#ef4444" }}>{errors.startTime.message}</p>
          )}
        </div>

        {/* Duration */}
        <div>
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>المدة (دقيقة)</label>
          <select
            {...register("durationMinutes", { valueAsNumber: true })}
            className="aqlan-select"
          >
            {[15, 20, 30, 45, 60, 90, 120].map((m) => (
              <option key={m} value={m}>{m} دقيقة</option>
            ))}
          </select>
        </div>

        {/* Notes */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium mb-1.5" style={{ color: "#0d2137" }}>ملاحظات</label>
          <textarea
            {...register("notes")}
            rows={2}
            className="aqlan-input"
            placeholder="ملاحظات اختيارية..."
          />
        </div>
      </div>

      <div className="flex justify-end gap-3 pb-4">
        <button
          type="button"
          onClick={() => router.back()}
          className="btn-ghost rounded-lg"
        >
          إلغاء
        </button>
        <button
          type="submit"
          disabled={saving}
          className="btn-blue"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : isEditMode ? "حفظ التعديلات" : "حفظ الموعد"}
        </button>
      </div>
    </form>
  );
}
