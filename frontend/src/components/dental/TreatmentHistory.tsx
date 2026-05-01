"use client";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Plus, Stethoscope } from "lucide-react";
import type { GeneralTreatment, CreateGeneralTreatmentRequest } from "@/types/dental";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";

interface Doctor { id: string; name: string; color?: string; }

const schema = z.object({
  treatmentType:  z.string().min(1, "نوع العلاج مطلوب"),
  toothNumber:    z.string().optional(),
  materialUsed:   z.string().optional(),
  anesthesiaType: z.string().optional(),
  cost:           z.number().optional(),
  doctorId:       z.string().optional(),
  notes:          z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const TREATMENT_TYPES = [
  "حشو كومبوزيت", "حشو أملغم", "علاج جذر", "قلع", "تنظيف", "تلميع",
  "تاج", "جسر", "طقم", "زرع", "تبييض", "فحص"
];

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
  err ? "border-red-400" : "border-gray-300"
);

interface Props {
  patientId: string;
}

export function TreatmentHistory({ patientId }: Props) {
  const [treatments, setTreatments] = useState<GeneralTreatment[]>([]);
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  useEffect(() => {
    Promise.all([
      api.get<GeneralTreatment[]>(`/api/general-treatments/${patientId}`),
      api.get<Doctor[]>("/api/doctors"),
    ])
      .then(([tr, dr]) => {
        setTreatments(tr.data);
        setDoctors(dr.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    try {
      const req: CreateGeneralTreatmentRequest = {
        patientId,
        treatmentType: data.treatmentType,
        toothNumber: data.toothNumber,
        materialUsed: data.materialUsed,
        anesthesiaType: data.anesthesiaType,
        cost: data.cost,
        doctorId: data.doctorId,
        notes: data.notes,
      };
      const { data: created } = await api.post<GeneralTreatment>("/api/general-treatments", req);
      setTreatments([created, ...treatments]);
      reset();
      setShowForm(false);
    } catch {
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-gray-900">سجل المعالجات</h3>
        <button
          onClick={() => setShowForm(!showForm)}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          معالجة جديدة
        </button>
      </div>

      {/* Form */}
      {showForm && (
        <form onSubmit={handleSubmit(onSubmit)} className="bg-clinic-blue-50 rounded-xl border border-clinic-blue-100 p-4 space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">نوع العلاج *</label>
              <select {...register("treatmentType")} className={inputCls(errors.treatmentType?.message)}>
                <option value="">اختر...</option>
                {TREATMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">السن</label>
              <input {...register("toothNumber")} className={inputCls()} placeholder="مثلاً: 16، 21" dir="ltr" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">الطبيب</label>
              <select {...register("doctorId")} className={inputCls()}>
                <option value="">اختر...</option>
                {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">التكلفة (ريال)</label>
              <input {...register("cost", { valueAsNumber: true })} type="number" min={0} className={inputCls()} dir="ltr" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">المادة المستخدمة</label>
              <input {...register("materialUsed")} className={inputCls()} placeholder="كومبوزيت، أملغم..." />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">التخدير</label>
              <select {...register("anesthesiaType")} className={inputCls()}>
                <option value="">بدون</option>
                <option value="ليدوكائين">ليدوكائين</option>
                <option value="أرتيكائين">أرتيكائين</option>
                <option value="موضعي">موضعي</option>
              </select>
            </div>
            <div className="md:col-span-2">
              <label className="block text-xs font-medium text-gray-700 mb-1">ملاحظات</label>
              <textarea {...register("notes")} rows={2} className={inputCls()} />
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => { setShowForm(false); reset(); }} className="px-4 py-1.5 text-sm border border-gray-300 rounded-lg hover:bg-gray-50 transition">
              إلغاء
            </button>
            <button type="submit" disabled={saving} className="px-4 py-1.5 text-sm bg-clinic-blue text-white rounded-lg hover:opacity-90 disabled:opacity-60 transition">
              {saving ? "جارٍ الحفظ..." : "حفظ المعالجة"}
            </button>
          </div>
        </form>
      )}

      {/* Treatments list */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 3 }).map((_, i) => <div key={i} className="h-16 bg-gray-100 rounded-lg" />)}
        </div>
      ) : treatments.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Stethoscope className="w-10 h-10 mx-auto mb-2 opacity-30" />
          <p className="text-sm">لا توجد معالجات مسجلة</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {["التاريخ", "نوع العلاج", "السن", "المادة", "الطبيب", "التكلفة"].map((h) => (
                  <th key={h} className="text-start px-4 py-2.5 text-xs font-semibold text-gray-500">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {treatments.map((t) => (
                <tr key={t.id} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-2.5 text-gray-600 text-xs">{formatArabicDate(t.createdAt)}</td>
                  <td className="px-4 py-2.5 font-medium text-gray-900">{t.treatmentType}</td>
                  <td className="px-4 py-2.5 font-mono text-gray-700">{t.toothNumber ?? "—"}</td>
                  <td className="px-4 py-2.5 text-gray-600">{t.materialUsed ?? "—"}</td>
                  <td className="px-4 py-2.5 text-gray-600">{t.doctorName ?? "—"}</td>
                  <td className="px-4 py-2.5 font-mono text-green-700">
                    {t.cost ? formatYemeniRiyal(t.cost) : "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
