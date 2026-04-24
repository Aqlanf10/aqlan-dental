"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Search, ArrowRight } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface Doctor { id: string; name: string; color?: string; specialty?: string; }

const schema = z.object({
  patientId:    z.string().min(1, "اختر مريضاً"),
  fromDoctorId: z.string().min(1, "اختر الطبيب المُحيل"),
  toDoctorId:   z.string().min(1, "اختر الطبيب المُحال إليه"),
  reason:       z.string().min(1, "سبب الإحالة مطلوب"),
  priority:     z.enum(["normal", "urgent", "emergency"]),
  notes:        z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
  err ? "border-red-400" : "border-gray-300"
);

export default function NewReferralPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [showPatientDropdown, setShowPatientDropdown] = useState(false);

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { priority: "normal" }
  });

  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

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
      await api.post("/api/referrals", data);
      router.push("/referrals");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-5 max-w-3xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/referrals" className="hover:text-clinic-teal transition">الإحالات</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">إحالة جديدة</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/referrals" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إحالة مريض</h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {serverError && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{serverError}</div>
        )}

        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Patient */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">المريض <span className="text-red-500">*</span></label>
            <div className="relative">
              <Search className="absolute right-3 top-2.5 w-4 h-4 text-gray-400" />
              <input
                value={patientSearch}
                onChange={(e) => { setPatientSearch(e.target.value); setShowPatientDropdown(true); }}
                onFocus={() => patientSearch.length >= 2 && setShowPatientDropdown(true)}
                onBlur={() => setTimeout(() => setShowPatientDropdown(false), 150)}
                placeholder="ابحث بالاسم أو الرقم..."
                className={cn(inputCls(errors.patientId?.message), "pe-9")}
                autoComplete="off"
              />
              {showPatientDropdown && patientResults.length > 0 && (
                <div className="absolute z-10 w-full mt-1 bg-white rounded-lg border border-gray-200 shadow-lg max-h-48 overflow-y-auto">
                  {patientResults.map((p) => (
                    <button key={p.id} type="button" onMouseDown={() => selectPatient(p)}
                      className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between"
                    >
                      <span className="font-medium">{p.fullName}</span>
                      <span className="text-xs text-gray-400 font-mono">{p.patientNumber}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
            <input type="hidden" {...register("patientId")} />
            {errors.patientId && <p className="mt-1 text-xs text-red-600">{errors.patientId.message}</p>}
          </div>

          {/* From Doctor */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">من الطبيب <span className="text-red-500">*</span></label>
            <select {...register("fromDoctorId")} className={inputCls(errors.fromDoctorId?.message)}>
              <option value="">اختر...</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
            {errors.fromDoctorId && <p className="mt-1 text-xs text-red-600">{errors.fromDoctorId.message}</p>}
          </div>

          {/* To Doctor */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">إلى الطبيب <span className="text-red-500">*</span></label>
            <select {...register("toDoctorId")} className={inputCls(errors.toDoctorId?.message)}>
              <option value="">اختر...</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` — ${d.specialty}` : ""}</option>)}
            </select>
            {errors.toDoctorId && <p className="mt-1 text-xs text-red-600">{errors.toDoctorId.message}</p>}
          </div>

          {/* Priority */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-2">الأولوية</label>
            <div className="flex gap-2">
              {[
                { v: "normal",    l: "عادية",  c: "bg-gray-50 border-gray-200 text-gray-700" },
                { v: "urgent",    l: "عاجلة",  c: "bg-orange-50 border-orange-300 text-orange-700" },
                { v: "emergency", l: "طارئة",  c: "bg-red-50 border-red-300 text-red-700" },
              ].map(({ v, l, c }) => (
                <label key={v} className="flex-1 cursor-pointer">
                  <input type="radio" value={v} {...register("priority")} className="sr-only peer" />
                  <div className={cn(
                    "text-center py-2.5 rounded-lg border-2 text-sm font-medium transition",
                    c, "peer-checked:border-clinic-teal peer-checked:ring-2 peer-checked:ring-clinic-teal/20"
                  )}>
                    {l}
                  </div>
                </label>
              ))}
            </div>
          </div>

          {/* Reason */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">سبب الإحالة <span className="text-red-500">*</span></label>
            <textarea {...register("reason")} rows={2} className={inputCls(errors.reason?.message)}
              placeholder="وصف موجز لسبب الإحالة..."
            />
            {errors.reason && <p className="mt-1 text-xs text-red-600">{errors.reason.message}</p>}
          </div>

          {/* Notes */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات إضافية</label>
            <textarea {...register("notes")} rows={2} className={inputCls()} />
          </div>
        </div>

        <div className="flex justify-end gap-3 pb-4">
          <Link href="/referrals" className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
            إلغاء
          </Link>
          <button type="submit" disabled={saving}
            className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الإرسال..." : "إرسال الإحالة"}
          </button>
        </div>
      </form>
    </div>
  );
}
