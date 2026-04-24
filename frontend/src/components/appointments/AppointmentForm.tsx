"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Search } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { PatientListItem } from "@/types/patient";

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

export function AppointmentForm() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [showPatientDropdown, setShowPatientDropdown] = useState(false);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { durationMinutes: 30 },
  });

  // Load doctors
  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  // Patient search (debounced)
  useEffect(() => {
    if (patientSearch.length < 2) { setPatientResults([]); return; }
    const t = setTimeout(() => {
      api.get<{ items: PatientListItem[] }>(`/api/patients?search=${encodeURIComponent(patientSearch)}&pageSize=8`)
        .then((r) => setPatientResults(r.data.items))
        .catch(() => {});
    }, 300);
    return () => clearTimeout(t);
  }, [patientSearch]);

  const selectPatient = (p: PatientListItem) => {
    setValue("patientId", p.id);
    setPatientSearch(`${p.fullName} (${p.patientNumber})`);
    setShowPatientDropdown(false);
  };

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      await api.post("/api/appointments", {
        patientId:       data.patientId,
        doctorId:        data.doctorId,
        appointmentDate: data.appointmentDate,
        startTime:       data.startTime + ":00",
        durationMinutes: data.durationMinutes,
        appointmentType: data.appointmentType,
        notes:           data.notes,
      });
      router.push(`/appointments?date=${data.appointmentDate}`);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
      {serverError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
          {serverError}
        </div>
      )}

      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Patient search */}
        <div className="md:col-span-2">
          <label className="block text-sm font-medium text-gray-700 mb-1.5">
            المريض <span className="text-red-500">*</span>
          </label>
          <div className="relative">
            <Search className="absolute right-3 top-2.5 w-4 h-4 text-gray-400" />
            <input
              value={patientSearch}
              onChange={(e) => { setPatientSearch(e.target.value); setShowPatientDropdown(true); }}
              onFocus={() => patientSearch.length >= 2 && setShowPatientDropdown(true)}
              onBlur={() => setTimeout(() => setShowPatientDropdown(false), 150)}
              placeholder="ابحث بالاسم أو رقم المريض..."
              className={cn(inputCls(errors.patientId?.message), "pe-9")}
              autoComplete="off"
            />
            {showPatientDropdown && patientResults.length > 0 && (
              <div className="absolute z-10 w-full mt-1 bg-white rounded-lg border border-gray-200 shadow-lg max-h-48 overflow-y-auto">
                {patientResults.map((p) => (
                  <button
                    key={p.id}
                    type="button"
                    onMouseDown={() => selectPatient(p)}
                    className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between"
                  >
                    <span className="font-medium">{p.fullName}</span>
                    <span className="text-xs text-gray-400 font-mono">{p.patientNumber}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
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
            defaultValue={new Date().toISOString().slice(0, 10)}
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
          {saving ? "جارٍ الحفظ..." : "حفظ الموعد"}
        </button>
      </div>
    </form>
  );
}
