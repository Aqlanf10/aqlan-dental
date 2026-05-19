"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, AlertTriangle, CalendarDays, Loader2, Clock } from "lucide-react";
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

interface ServiceOption {
  id: string;
  arabicName: string;
  defaultDurationMinutes: number;
  defaultPrice: number;
  category?: string;
}

interface RoomOption {
  id: string;
  arabicName: string;
  roomType?: string;
}

const schema = z.object({
  patientId:       z.string().min(1, "اختر مريضاً"),
  doctorId:        z.string().min(1, "اختر طبيباً"),
  appointmentDate: z.string().min(1, "التاريخ مطلوب"),
  startTime:       z.string().min(1, "وقت البداية مطلوب"),
  durationMinutes: z.number().min(5).max(240),
  appointmentType: z.string().min(1, "نوع الموعد مطلوب"),
  notes:           z.string().optional(),
  serviceId:       z.string().optional(),
  clinicRoomId:    z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
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
    serviceId?:      string;
    clinicRoomId?:   string;
  };
}

export function AppointmentForm({ defaultPatientId, defaultPatientName, appointmentId, editDefaults }: Props) {
  const isEditMode = Boolean(appointmentId);
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [isConflict, setIsConflict] = useState(false);
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [services, setServices] = useState<ServiceOption[]>([]);
  const [rooms, setRooms] = useState<RoomOption[]>([]);

  // Available slots state
  const [slots, setSlots] = useState<string[]>([]);
  const [slotDuration, setSlotDuration] = useState<number | null>(null);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [doctorAvailable, setDoctorAvailable] = useState<boolean | null>(null);
  const [selectedSlot, setSelectedSlot] = useState<string>("");

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
      serviceId:       editDefaults?.serviceId ?? "",
      clinicRoomId:    editDefaults?.clinicRoomId ?? "",
    },
  });

  const watchedDate   = useWatch({ control, name: "appointmentDate" });
  const watchedDoctor = useWatch({ control, name: "doctorId" });
  const watchedServiceId = useWatch({ control, name: "serviceId" });

  // Load doctors
  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  // Load services (ShowInReception=true)
  useEffect(() => {
    api.get<ServiceOption[]>("/api/settings/services", { params: { showInReception: true } })
      .then((r) => setServices(r.data ?? []))
      .catch(() => {});
  }, []);

  // Load rooms
  useEffect(() => {
    api.get<RoomOption[]>("/api/settings/rooms")
      .then((r) => setRooms(r.data ?? []))
      .catch(() => {});
  }, []);

  // Auto-set type & duration when service changes
  useEffect(() => {
    if (!watchedServiceId) return;
    const svc = services.find((s) => s.id === watchedServiceId);
    if (svc) {
      setValue("appointmentType", svc.arabicName, { shouldValidate: true });
      setValue("durationMinutes", svc.defaultDurationMinutes, { shouldValidate: true });
    }
  }, [watchedServiceId, services, setValue]);

  // Fetch available slots when doctor + date change
  useEffect(() => {
    if (!watchedDoctor || !watchedDate) {
      setSlots([]);
      setSlotDuration(null);
      setDoctorAvailable(null);
      return;
    }
    setSlotsLoading(true);
    setSlots([]);
    setSlotDuration(null);
    setDoctorAvailable(null);

    api
      .get<{ available: boolean; slots: string[]; slotDuration: number }>(
        `/api/doctors/${watchedDoctor}/schedule/slots?date=${watchedDate}`
      )
      .then((r) => {
        setDoctorAvailable(r.data.available);
        setSlots(r.data.slots ?? []);
        setSlotDuration(r.data.slotDuration ?? null);
      })
      .catch(() => {
        setSlots([]);
        setSlotDuration(null);
        setDoctorAvailable(null);
      })
      .finally(() => setSlotsLoading(false));
  }, [watchedDoctor, watchedDate]);

  function handleSlotSelect(slot: string) {
    setSelectedSlot(slot);
    setValue("startTime", slot, { shouldValidate: true });
    if (slotDuration) setValue("durationMinutes", slotDuration);
  }

  function formatSlot(t: string): string {
    const [hStr, mStr] = t.split(":");
    let h = parseInt(hStr, 10);
    const suffix = h >= 12 ? "م" : "ص";
    if (h > 12) h -= 12;
    if (h === 0) h = 12;
    return `${h}:${mStr} ${suffix}`;
  }

  const showSlots  = !!watchedDoctor && !!watchedDate && !slotsLoading && slots.length > 0;
  const noWorkDay  = !!watchedDoctor && !!watchedDate && !slotsLoading && doctorAvailable === false;
  const allBooked  = !!watchedDoctor && !!watchedDate && !slotsLoading && doctorAvailable === true && slots.length === 0;
  const useTimePicker = !watchedDoctor || !watchedDate || (doctorAvailable === null && !slotsLoading);

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
        serviceId:       data.serviceId || undefined,
        clinicRoomId:    data.clinicRoomId || undefined,
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

        {/* Service (optional) */}
        {services.length > 0 && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              الخدمة <span className="text-gray-400 font-normal">(اختياري)</span>
            </label>
            <select {...register("serviceId")} className={inputCls()}>
              <option value="">— اختر الخدمة —</option>
              {services.map((s) => (
                <option key={s.id} value={s.id}>{s.arabicName}</option>
              ))}
            </select>
          </div>
        )}

        {/* Room (optional) */}
        {rooms.length > 0 && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              الغرفة <span className="text-gray-400 font-normal">(اختياري)</span>
            </label>
            <select {...register("clinicRoomId")} className={inputCls()}>
              <option value="">— اختر الغرفة —</option>
              {rooms.map((r) => (
                <option key={r.id} value={r.id}>{r.arabicName}</option>
              ))}
            </select>
          </div>
        )}

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

        {/* Start time — smart slot picker */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5 flex items-center gap-2">
            <Clock className="w-3.5 h-3.5 text-gray-400" />
            وقت البداية <span className="text-red-500">*</span>
            {slotDuration && (
              <span className="text-[11px] font-normal text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">
                {slotDuration} دقيقة / موعد
              </span>
            )}
          </label>

          {/* Loading */}
          {slotsLoading && (
            <div className="flex items-center gap-2 text-gray-400 text-sm py-2">
              <Loader2 className="w-4 h-4 animate-spin" />
              جارٍ تحميل الأوقات المتاحة...
            </div>
          )}

          {/* Doctor not working this day */}
          {noWorkDay && (
            <div className="flex items-center gap-2 text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-4 py-2.5 text-sm">
              <AlertTriangle className="w-4 h-4 flex-shrink-0" />
              الطبيب لا يعمل في هذا اليوم. اختر يوماً آخر أو أدخل الوقت يدوياً.
            </div>
          )}

          {/* All slots booked */}
          {allBooked && (
            <div className="flex items-center gap-2 text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-4 py-2.5 text-sm">
              <AlertTriangle className="w-4 h-4 flex-shrink-0" />
              جميع الأوقات محجوزة. اختر يوماً آخر أو أدخل الوقت يدوياً أدناه.
            </div>
          )}

          {/* Slot grid */}
          {showSlots && (
            <div className="grid grid-cols-5 sm:grid-cols-7 gap-1.5 mb-2">
              {slots.map((slot) => {
                const isSelected = selectedSlot === slot;
                return (
                  <button
                    key={slot}
                    type="button"
                    onClick={() => handleSlotSelect(slot)}
                    className={cn(
                      "py-2 px-1 rounded-lg border text-xs font-semibold transition-all",
                      isSelected
                        ? "border-clinic-blue bg-clinic-blue text-white shadow-sm"
                        : "border-gray-200 text-gray-600 hover:border-clinic-blue/60 hover:bg-clinic-blue/5"
                    )}
                  >
                    {formatSlot(slot)}
                  </button>
                );
              })}
            </div>
          )}

          {/* Always show manual time input as fallback / override */}
          <div className={cn("flex items-center gap-2", showSlots ? "mt-2" : "")}>
            {showSlots && (
              <span className="text-xs text-gray-400 whitespace-nowrap">أو أدخل يدوياً:</span>
            )}
            <input
              {...register("startTime")}
              type="time"
              className={cn(inputCls(errors.startTime?.message), showSlots ? "w-36" : "w-full")}
              onChange={(e) => {
                setSelectedSlot("");
                register("startTime").onChange(e);
              }}
            />
          </div>
          {errors.startTime && (
            <p className="mt-1 text-xs text-red-600">{errors.startTime.message}</p>
          )}

          {/* No doctor/date yet */}
          {useTimePicker && !slotsLoading && (
            <p className="text-[11px] text-gray-400 mt-1">اختر الطبيب والتاريخ لعرض الأوقات المتاحة تلقائياً</p>
          )}
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
          className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : isEditMode ? "حفظ التعديلات" : "حفظ الموعد"}
        </button>
      </div>
    </form>
  );
}
