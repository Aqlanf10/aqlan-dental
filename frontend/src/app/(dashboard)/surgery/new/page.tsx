"use client";
import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Search, ArrowRight } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import { SURGERY_TYPES } from "@/types/surgery";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { cn } from "@/lib/utils";

interface Doctor { id: string; name: string; color?: string; }

const schema = z.object({
  patientId:     z.string().min(1, "اختر مريضاً"),
  doctorId:      z.string().optional(),
  surgeryType:   z.string().min(1, "نوع الجراحة مطلوب"),
  teethInvolved: z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
  err ? "border-red-400" : "border-gray-300"
);

function NewSurgeryForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const prePatientId = searchParams.get("patientId");
  const prePatientName = searchParams.get("patientName");
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  // FE-13: useDoctors() replaces useState + useEffect + api.get.
  const { data: doctors = [] } = useDoctors();
  const [patientSearch, setPatientSearch] = useState("");
  const [patientResults, setPatientResults] = useState<PatientListItem[]>([]);
  const [showPatientDropdown, setShowPatientDropdown] = useState(false);

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  useEffect(() => {
    if (prePatientId) setValue("patientId", prePatientId);
    if (prePatientName) setPatientSearch(prePatientName);
  }, [prePatientId, prePatientName, setValue]);

  useEffect(() => {
    if (patientSearch.length < 2) { setPatientResults([]); return; }
    const t = setTimeout(() => {
      api.get<import("@/types/api").PaginatedResponse<PatientListItem>>(`/api/patients?search=${encodeURIComponent(patientSearch)}&pageSize=8`)
        .then((r) => setPatientResults(r.data.data))
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
      await api.post("/api/surgery-cases", data);
      router.push("/surgery");
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
        <Link href="/surgery" className="hover:text-clinic-blue transition">الجراحة</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">حالة جديدة</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/surgery" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إنشاء حالة جراحية</h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {serverError && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{serverError}</div>
        )}

        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
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

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">نوع الجراحة <span className="text-red-500">*</span></label>
            <select {...register("surgeryType")} className={inputCls(errors.surgeryType?.message)}>
              <option value="">اختر...</option>
              {SURGERY_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
            {errors.surgeryType && <p className="mt-1 text-xs text-red-600">{errors.surgeryType.message}</p>}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب الجراح</label>
            <select {...register("doctorId")} className={inputCls()}>
              <option value="">اختر...</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>

          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الأسنان المعنية</label>
            <input {...register("teethInvolved")} className={inputCls()} placeholder="مثلاً: 18، 28، 38، 48" dir="ltr" />
          </div>
        </div>

        <div className="flex justify-end gap-3 pb-4">
          <Link href="/surgery" className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
            إلغاء
          </Link>
          <button type="submit" disabled={saving}
            className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "إنشاء الحالة"}
          </button>
        </div>
      </form>
    </div>
  );
}

export default function NewSurgeryPage() {
  return (
    <Suspense fallback={<div className="animate-pulse h-96 bg-gray-100 rounded-xl" />}>
      <NewSurgeryForm />
    </Suspense>
  );
}
