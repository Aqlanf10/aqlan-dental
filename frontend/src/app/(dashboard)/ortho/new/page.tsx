"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Search, ArrowRight } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import type { CreateOrthoCaseRequest } from "@/types/ortho";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface Doctor { id: string; name: string; color?: string; }

const schema = z.object({
  patientId:              z.string().min(1, "اختر مريضاً"),
  doctorId:               z.string().optional(),
  applianceType:          z.string().optional(),
  startDate:              z.string().optional(),
  expectedDurationMonths: z.number().optional(),
  totalFee:               z.number().optional(),
  notes:                  z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
  err ? "border-red-400" : "border-gray-300"
);

const APPLIANCE_TYPES = ["MBT 0.022", "MBT 0.018", "Damon", "Invisalign", "Removable", "Functional"];

export default function NewOrthoPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [showPatientDropdown, setShowPatientDropdown] = useState(false);

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { startDate: new Date().toISOString().slice(0, 10) }
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
      const req: CreateOrthoCaseRequest = {
        patientId: data.patientId,
        doctorId: data.doctorId,
        applianceType: data.applianceType,
        startDate: data.startDate,
        expectedDurationMonths: data.expectedDurationMonths,
        totalFee: data.totalFee,
        notes: data.notes,
      };
      const { data: created } = await api.post<{ id: string }>("/api/ortho-cases", req);
      router.push(`/ortho/${created.id}`);
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
        <Link href="/ortho" className="hover:text-clinic-teal transition">التقويم</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">حالة جديدة</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/ortho" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إنشاء حالة تقويمية جديدة</h1>
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
                placeholder="ابحث بالاسم أو رقم المريض..."
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

          {/* Doctor */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب المسؤول</label>
            <select {...register("doctorId")} className={inputCls()}>
              <option value="">اختر الطبيب...</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>

          {/* Appliance type */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">نوع الجهاز</label>
            <select {...register("applianceType")} className={inputCls()}>
              <option value="">اختر...</option>
              {APPLIANCE_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>

          {/* Start date */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">تاريخ البداية</label>
            <input {...register("startDate")} type="date" className={inputCls()} />
          </div>

          {/* Duration */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">المدة المتوقعة (أشهر)</label>
            <input {...register("expectedDurationMonths", { valueAsNumber: true })} type="number" min={1} max={60} className={inputCls()} placeholder="18" />
          </div>

          {/* Total fee */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الرسوم الإجمالية (ريال)</label>
            <input {...register("totalFee", { valueAsNumber: true })} type="number" min={0} className={inputCls()} placeholder="0" dir="ltr" />
          </div>

          {/* Notes */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
            <textarea {...register("notes")} rows={2} className={inputCls()} />
          </div>
        </div>

        <div className="flex justify-end gap-3 pb-4">
          <Link href="/ortho" className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
            إلغاء
          </Link>
          <button type="submit" disabled={saving}
            className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الإنشاء..." : "إنشاء الحالة"}
          </button>
        </div>
      </form>
    </div>
  );
}
