"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, AlertTriangle, CalendarDays, Loader2, Clock, Users } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { cn, localDateString } from "@/lib/utils";
import type { PatientListItem } from "@/types/patient";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import type { TreatmentPackage } from "@/types/appointment";
import {
  APPOINTMENT_COLOR_SUGGESTIONS,
  resolveAppointmentColor,
} from "@/lib/appointmentColors";

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
  // YOLO-S1: companion + color + package (all optional)
  companionName:         z.string().optional(),
  companionPhone:        z.string().optional(),
  companionRelationship: z.string().optional(),
  appointmentColor:      z.string().optional(),
  packageId:             z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
    err ? "border-red-400" : "border-gray-300"
  );

// Quick-pick chips for the common orthodontic visit types. The orthodontist
// taps one to pre-fill the appointmentType field instead of typing the full
// Arabic phrase. The labels mirror APPOINTMENT_TYPES in @/components/shared/
// journey/constants.ts and ORTHO_STAGE_LABELS in @/types/ortho so the same
// vocabulary appears across the booking, daily-operations, and ortho module.
const ORTHO_APPOINTMENT_SHORTCUTS: ReadonlyArray<{ value: string; label: string }> = [
  { value: "OrthoBonding",    label: "تركيب بندات التقويم" },
  { value: "OrthoAdjustment", label: "تعديل تقويم" },
  { value: "OrthoDebonding",  label: "فك بندات التقويم" },
  { value: "OrthoRetainer",   label: "تركيب مثبتات" },
];

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
    // YOLO-S1
    companionName?:         string;
    companionPhone?:        string;
    companionRelationship?: string;
    appointmentColor?:      string;
    packageId?:             string;
  };
}

export function AppointmentForm({ defaultPatientId, defaultPatientName, appointmentId, editDefaults }: Props) {
  const isEditMode = Boolean(appointmentId);
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [isConflict, setIsConflict] = useState(false);
  // FE-13: useDoctors() replaces useState + useEffect + api.get.
  const { data: doctors = [] } = useDoctors();
  const [services, setServices] = useState<ServiceOption[]>([]);
  const [rooms, setRooms] = useState<RoomOption[]>([]);
  // YOLO-S1: treatment packages dropdown
  const [packages, setPackages] = useState<TreatmentPackage[]>([]);

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
      appointmentDate: editDefaults?.appointmentDate ?? localDateString(),
      startTime:       editDefaults?.startTime ?? "",
      appointmentType: editDefaults?.appointmentType ?? "",
      notes:           editDefaults?.notes ?? "",
      serviceId:       editDefaults?.serviceId ?? "",
      clinicRoomId:    editDefaults?.clinicRoomId ?? "",
      // YOLO-S1
      companionName:         editDefaults?.companionName ?? "",
      companionPhone:        editDefaults?.companionPhone ?? "",
      companionRelationship: editDefaults?.companionRelationship ?? "",
      appointmentColor:      editDefaults?.appointmentColor ?? "",
      packageId:             editDefaults?.packageId ?? "",
    },
  });

  const watchedDate   = useWatch({ control, name: "appointmentDate" });
  const watchedDoctor = useWatch({ control, name: "doctorId" });
  const watchedServiceId = useWatch({ control, name: "serviceId" });
  const watchedPackageId = useWatch({ control, name: "packageId" });
  const watchedColor = useWatch({ control, name: "appointmentColor" });

  // FE-13: Removed useEffect that fetched doctors — useDoctors() handles it.

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

  // YOLO-S1: load active treatment packages for the dropdown
  useEffect(() => {
    api.get<TreatmentPackage[]>("/api/treatment-packages?activeOnly=true")
      .then((r) => setPackages(r.data ?? []))
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

  // YOLO-S1: when a treatment package is selected, pre-fill the appointment type
  // from the package name AND adopt the package color if the user hasn't picked one.
  useEffect(() => {
    if (!watchedPackageId) return;
    const pkg = packages.find((p) => p.id === watchedPackageId);
    if (!pkg) return;
    setValue("appointmentType", pkg.name, { shouldValidate: true });
    if (!watchedColor && pkg.color) {
      setValue("appointmentColor", pkg.color, { shouldValidate: false });
    }
  }, [watchedPackageId, packages, setValue, watchedColor]);

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

  // Preview the resolved color for the left border swatch next to the picker.
  const colorPreview = resolveAppointmentColor(watchedColor, undefined, undefined) ?? "#2563EB";

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
        // YOLO-S1 — only send non-empty values so null/empty doesn't overwrite on update
        companionName:         data.companionName?.trim() || null,
        companionPhone:        data.companionPhone?.trim() || null,
        companionRelationship: data.companionRelationship?.trim() || null,
        appointmentColor:      data.appointmentColor?.trim() || null,
        packageId:             data.packageId || null,
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
          {/* Quick-pick chips for the common orthodontic visit types. The
              orthodontist taps one to pre-fill the field instead of typing the
              full Arabic phrase. Mirrors the APPOINTMENT_TYPES vocabulary used
              by the daily-operations booking modal so the same appointment type
              shows up consistently across both screens. */}
          <div className="mt-1.5 flex flex-wrap gap-1">
            {ORTHO_APPOINTMENT_SHORTCUTS.map(s => (
              <button
                key={s.value}
                type="button"
                onClick={() => setValue("appointmentType", s.label, { shouldValidate: true })}
                className="rounded-full border border-violet-200 bg-violet-50 px-2 py-0.5 text-[10px] font-semibold text-violet-700 hover:bg-violet-100 transition"
                title={`تعبئة نوع الموعد بـ: ${s.label}`}
              >
                {s.label}
              </button>
            ))}
          </div>
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

        {/* Treatment Package (optional — YOLO-S1) */}
        {packages.length > 0 && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              باقة العلاج <span className="text-gray-400 font-normal">(اختياري)</span>
            </label>
            <select {...register("packageId")} className={inputCls()}>
              <option value="">— اختر الباقة —</option>
              {packages.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} {p.sessionCount > 1 ? `(${p.sessionCount} جلسات)` : ""}
                </option>
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

        {/* ── YOLO-S1: Companion/Guardian section (children/ortho patients) ── */}
        <div className="md:col-span-2 mt-2 pt-3 border-t border-gray-100">
          <div className="flex items-center gap-2 mb-3">
            <Users className="w-4 h-4 text-clinic-blue" />
            <h3 className="text-sm font-semibold text-gray-800">
              المرافق / ولي الأمر
              <span className="text-gray-400 font-normal mr-1"> (للأطفال — اختياري)</span>
            </h3>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">اسم المرافق</label>
              <input
                {...register("companionName")}
                className={inputCls()}
                placeholder="مثال: فاطمة أحمد"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">هاتف المرافق (واتساب)</label>
              <input
                {...register("companionPhone")}
                className={inputCls()}
                placeholder="مثال: 967777123456+"
                dir="ltr"
              />
              <p className="text-[10px] text-gray-400 mt-1">
                عند إرسال تذكير واتساب، يُرسل إلى المريض والمرافق معًا
              </p>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">صلة القرابة</label>
              <select {...register("companionRelationship")} className={inputCls()}>
                <option value="">— اختر —</option>
                <option value="الأم">الأم</option>
                <option value="الأب">الأب</option>
                <option value="الجد">الجد</option>
                <option value="الجدة">الجدة</option>
                <option value="الأخ">الأخ</option>
                <option value="الأخت">الأخت</option>
                <option value="العم">العم</option>
                <option value="العمة">العمة</option>
                <option value="الخال">الخال</option>
                <option value="الخالة">الخالة</option>
                <option value="أخرى">أخرى</option>
              </select>
            </div>
          </div>
        </div>

        {/* ── YOLO-S1: Appointment color picker ─────────────────────────── */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5 flex items-center gap-2">
            لون الموعد على التقويم
            <span className="text-gray-400 font-normal text-xs">(اختياري — يُستخدم للحدود على بطاقة الموعد)</span>
          </label>
          <div className="flex items-center gap-3 flex-wrap">
            <input
              {...register("appointmentColor")}
              type="color"
              className="w-12 h-9 p-1 rounded-lg border border-gray-300 cursor-pointer bg-white"
              value={watchedColor || "#3b82f6"}
              onChange={(e) => setValue("appointmentColor", e.target.value, { shouldValidate: false })}
            />
            <input
              {...register("appointmentColor")}
              type="text"
              dir="ltr"
              className={cn(inputCls(), "w-32 font-mono text-xs")}
              placeholder="#3b82f6"
            />
            {/* Live preview swatch */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">معاينة:</span>
              <div
                className="w-6 h-6 rounded-md border border-gray-300"
                style={{ backgroundColor: colorPreview, borderLeft: `4px solid ${colorPreview}` }}
              />
            </div>
            {/* Quick suggested colors */}
            <div className="flex items-center gap-1.5 flex-wrap">
              {APPOINTMENT_COLOR_SUGGESTIONS.map((s) => (
                <button
                  key={s.color}
                  type="button"
                  onClick={() => setValue("appointmentColor", s.color, { shouldValidate: false })}
                  className={cn(
                    "flex items-center gap-1 px-2 py-1 rounded-full border text-[11px] transition",
                    watchedColor === s.color
                      ? "border-clinic-blue bg-clinic-blue/5 text-clinic-blue"
                      : "border-gray-200 text-gray-600 hover:border-gray-300"
                  )}
                  title={s.label}
                >
                  <span
                    className="w-2.5 h-2.5 rounded-full inline-block"
                    style={{ backgroundColor: s.color }}
                  />
                  {s.label}
                </button>
              ))}
              {/* Clear color → fall back to doctor color on the calendar */}
              {watchedColor && (
                <button
                  type="button"
                  onClick={() => setValue("appointmentColor", "", { shouldValidate: false })}
                  className="px-2 py-1 rounded-full border border-gray-200 text-[11px] text-gray-500 hover:text-red-600 hover:border-red-200 transition"
                >
                  مسح اللون
                </button>
              )}
            </div>
          </div>
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
