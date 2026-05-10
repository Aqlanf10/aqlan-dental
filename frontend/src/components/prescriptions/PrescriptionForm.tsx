"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useForm, useFieldArray } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, Plus, Trash2 } from "lucide-react";
import type { PatientListItem } from "@/types/patient";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

interface Doctor { id: string; name: string; }

const drugSchema = z.object({
  name:      z.string().min(1, "اسم الدواء مطلوب"),
  dose:      z.string().min(1, "الجرعة مطلوبة"),
  frequency: z.string().min(1, "التكرار مطلوب"),
  duration:  z.string().min(1, "المدة مطلوبة"),
  notes:     z.string().optional(),
});

const schema = z.object({
  patientId: z.string().min(1, "اختر مريضاً"),
  doctorId:  z.string().optional(),
  diagnosis: z.string().optional(),
  drugs:     z.array(drugSchema).min(1, "أضف دواء واحداً على الأقل"),
  notes:     z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
    err ? "border-red-400" : "border-gray-300"
  );

// Common dental drugs for quick-add
const COMMON_DRUGS = [
  { name: "أموكسيسيللين", dose: "500mg", frequency: "3 مرات يومياً", duration: "5 أيام" },
  { name: "ميترونيدازول", dose: "400mg", frequency: "3 مرات يومياً", duration: "5 أيام" },
  { name: "إيبوبروفين",   dose: "400mg", frequency: "3 مرات يومياً بعد الأكل", duration: "3 أيام" },
  { name: "ديكلوفيناك",   dose: "50mg",  frequency: "مرتين يومياً",   duration: "3 أيام" },
  { name: "باراسيتامول",  dose: "500mg", frequency: "4 مرات يومياً", duration: "3 أيام" },
  { name: "كلاريثروميسين",dose: "500mg", frequency: "مرتين يومياً",   duration: "7 أيام" },
];

interface Props {
  defaultPatientId?: string;
  defaultPatientName?: string;
}

export function PrescriptionForm({ defaultPatientId, defaultPatientName }: Props) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);

  const { register, handleSubmit, setValue, control, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      patientId: defaultPatientId ?? "",
      drugs: [{ name: "", dose: "", frequency: "", duration: "", notes: "" }],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "drugs" });

  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  const quickAdd = (drug: typeof COMMON_DRUGS[0]) => {
    append({ ...drug, notes: "" });
  };

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const { data: created } = await api.post<{ id: string }>("/api/prescriptions", {
        patientId: data.patientId,
        doctorId:  data.doctorId || undefined,
        diagnosis: data.diagnosis,
        drugs:     data.drugs,
        notes:     data.notes,
      });
      router.push(`/prescriptions/${created.id}`);
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
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{serverError}</div>
      )}

      {/* Patient + Doctor + Diagnosis */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        <h3 className="font-bold text-gray-900 text-sm">بيانات الوصفة</h3>

        {/* Patient */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              المريض <span className="text-red-500">*</span>
            </label>
            <PatientCombobox
              defaultDisplayValue={defaultPatientName ?? ""}
              onSelect={(p: PatientListItem) => setValue("patientId", p.id)}
              error={errors.patientId?.message}
              readOnly={!!defaultPatientId}
            />
            <input type="hidden" {...register("patientId")} />
            {errors.patientId && <p className="mt-1 text-xs text-red-600">{errors.patientId.message}</p>}
          </div>

          {/* Doctor */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب</label>
            <select {...register("doctorId")} className={inputCls()}>
              <option value="">الطبيب الحالي</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>

          {/* Diagnosis */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">التشخيص</label>
            <input {...register("diagnosis")} className={inputCls()} placeholder="التشخيص (اختياري)" />
          </div>
        </div>
      </div>

      {/* Quick-add common drugs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-bold text-gray-900 text-sm">الأدوية</h3>
          <button
            type="button"
            onClick={() => append({ name: "", dose: "", frequency: "", duration: "", notes: "" })}
            className="flex items-center gap-1.5 text-xs text-clinic-blue hover:underline font-medium"
          >
            <Plus className="w-3.5 h-3.5" /> إضافة دواء
          </button>
        </div>

        {/* Common drugs shortcuts */}
        <div className="flex flex-wrap gap-1.5 mb-4">
          <span className="text-xs text-gray-400 self-center">إضافة سريعة:</span>
          {COMMON_DRUGS.map((d) => (
            <button
              key={d.name}
              type="button"
              onClick={() => quickAdd(d)}
              className="text-xs px-2.5 py-1 rounded-full border border-gray-200 text-gray-600 hover:border-clinic-blue hover:text-clinic-blue transition"
            >
              {d.name}
            </button>
          ))}
        </div>

        {errors.drugs?.root && (
          <p className="text-xs text-red-600 mb-2">{errors.drugs.root.message}</p>
        )}

        {/* Drug rows */}
        <div className="space-y-4">
          {fields.map((field, i) => (
            <div key={field.id} className="bg-gray-50 rounded-lg p-4 relative">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-bold text-clinic-blue">دواء {i + 1}</span>
                {fields.length > 1 && (
                  <button type="button" onClick={() => remove(i)} className="text-red-400 hover:text-red-600 transition">
                    <Trash2 className="w-4 h-4" />
                  </button>
                )}
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="md:col-span-2">
                  <label className="block text-xs font-medium text-gray-600 mb-1">اسم الدواء *</label>
                  <input
                    {...register(`drugs.${i}.name`)}
                    className={inputCls(errors.drugs?.[i]?.name?.message)}
                    placeholder="اسم الدواء"
                  />
                  {errors.drugs?.[i]?.name && <p className="mt-0.5 text-xs text-red-600">{errors.drugs[i].name?.message}</p>}
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">الجرعة *</label>
                  <input {...register(`drugs.${i}.dose`)} className={inputCls(errors.drugs?.[i]?.dose?.message)} placeholder="500mg" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">التكرار *</label>
                  <input {...register(`drugs.${i}.frequency`)} className={inputCls(errors.drugs?.[i]?.frequency?.message)} placeholder="3 مرات يومياً" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">المدة *</label>
                  <input {...register(`drugs.${i}.duration`)} className={inputCls(errors.drugs?.[i]?.duration?.message)} placeholder="5 أيام" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظة</label>
                  <input {...register(`drugs.${i}.notes`)} className={inputCls()} placeholder="بعد الأكل، قبل النوم..." />
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* General notes */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات عامة</label>
        <textarea {...register("notes")} rows={2} className={inputCls()} placeholder="تعليمات إضافية للمريض..." />
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 pb-4">
        <button type="button" onClick={() => router.back()}
          className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
        >
          إلغاء
        </button>
        <button type="submit" disabled={saving}
          className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ الوصفة"}
        </button>
      </div>
    </form>
  );
}
