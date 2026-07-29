"use client";
// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, FlaskConical, Banknote } from "lucide-react";
import Link from "next/link";
import api from "@/lib/api";
import { inputCls } from "./_shared";

// ─── Clinic Info Tab ──────────────────────────────────────────────────────────
// FE-30: migrated from ad-hoc useState to react-hook-form + zod. The clinic info
// form is a flat settings-key/value map; zod just enforces non-undefined strings
// (the backend stores everything as text via PUT /api/settings/{key}).
const clinicSettingsSchema = z.object({
  "clinic.name": z.string().optional(),
  "clinic.location": z.string().optional(),
  "clinic.phones": z.string().optional(),
  "clinic.lead_doctor": z.string().optional(),
  "clinic.lead_doctor_title": z.string().optional(),
  "clinic.lead_doctor_credentials": z.string().optional(),
  "patient.number_prefix": z.string()
    .trim()
    .min(1, "بادئة رقم المريض مطلوبة")
    .max(8, "الحد الأقصى 8 أحرف")
    .regex(/^[\p{L}\p{N}]+$/u, "استخدم أحرفًا وأرقامًا فقط"),
});
type ClinicSettingsFormData = z.infer<typeof clinicSettingsSchema>;

export function ClinicTab() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ClinicSettingsFormData>({
    resolver: zodResolver(clinicSettingsSchema),
    defaultValues: {
      "clinic.name": "",
      "clinic.location": "",
      "clinic.phones": "",
      "clinic.lead_doctor": "",
      "clinic.lead_doctor_title": "",
      "clinic.lead_doctor_credentials": "",
      "patient.number_prefix": "GM",
    },
  });

  useEffect(() => {
    api.get<ClinicSettingsFormData>("/api/settings")
      .then((r) => {
        const data = r.data ?? {};
        reset({
          "clinic.name": data["clinic.name"] ?? "",
          "clinic.location": data["clinic.location"] ?? "",
          "clinic.phones": data["clinic.phones"] ?? "",
          "clinic.lead_doctor": data["clinic.lead_doctor"] ?? "",
          "clinic.lead_doctor_title": data["clinic.lead_doctor_title"] ?? "",
          "clinic.lead_doctor_credentials": data["clinic.lead_doctor_credentials"] ?? "",
          "patient.number_prefix": data["patient.number_prefix"] ?? "GM",
        });
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [reset]);

  const onSubmit = handleSubmit(async (formData) => {
    setSaving(true);
    setSaved(false);
    try {
      const settingsFields = [
        { key: "clinic.name", category: "clinic" },
        { key: "clinic.location", category: "clinic" },
        { key: "clinic.phones", category: "clinic" },
        { key: "clinic.lead_doctor", category: "clinic" },
        { key: "clinic.lead_doctor_title", category: "clinic" },
        { key: "clinic.lead_doctor_credentials", category: "clinic" },
        { key: "patient.number_prefix", category: "patients" },
      ] as const;
      await Promise.all(
        settingsFields.map(({ key, category }) =>
          api.put(`/api/settings/${encodeURIComponent(key)}`, {
            value: formData[key] ?? "",
            category,
          })
        )
      );
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
    } finally {
      setSaving(false);
    }
  });

  if (loading) {
    return <div className="animate-pulse space-y-3">{Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-10 bg-gray-100 rounded-lg" />)}</div>;
  }

  return (
    <form className="space-y-4" onSubmit={onSubmit}>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">اسم المركز</label>
        <input
          {...register("clinic.name")}
          className={inputCls}
          placeholder="مركز د. عقلان الكامل لطب وتقويم الأسنان"
        />
        {errors["clinic.name"] && (
          <p className="text-xs text-red-600 mt-1">{errors["clinic.name"].message}</p>
        )}
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">العنوان</label>
        <input
          {...register("clinic.location")}
          className={inputCls}
          placeholder="تعز، اليمن"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">أرقام الهاتف</label>
        <input
          {...register("clinic.phones")}
          className={inputCls}
          placeholder="04-253028، 770XXXXXX"
          dir="ltr"
        />
      </div>
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-4">
        <p className="text-sm font-semibold text-gray-700">هوية الطبيب على التقارير</p>
        <p className="text-xs text-gray-500">تظهر هذه البيانات في ترويسة وتوقيع تقارير السيفالومتري والتقويم وكل ملفات PDF.</p>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">اسم الطبيب</label>
          <input
            {...register("clinic.lead_doctor")}
            className={inputCls}
            placeholder="د. عقلان الكامل"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">التخصص</label>
          <input
            {...register("clinic.lead_doctor_title")}
            className={inputCls}
            placeholder="أخصائي تقويم الأسنان"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">المؤهل العلمي (يظهر تحت التخصص)</label>
          <input
            {...register("clinic.lead_doctor_credentials")}
            className={inputCls}
            placeholder="جامعة مانيلا المركزية — الفلبين"
          />
        </div>
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1.5">بادئة رقم المريض</label>
        <input
          {...register("patient.number_prefix")}
          className={inputCls}
          placeholder="GM"
          dir="ltr"
          maxLength={8}
        />
        {errors["patient.number_prefix"] && (
          <p className="text-xs text-red-600 mt-1">{errors["patient.number_prefix"].message}</p>
        )}
        <p className="text-xs text-gray-500 mt-1">تُستخدم في أرقام المرضى الجدد فقط، مثل GM-2026-001.</p>
      </div>

      <div className="rounded-xl border border-cyan-100 bg-cyan-50/50 p-4">
        <div className="flex items-center gap-2 mb-3">
          <FlaskConical className="w-4 h-4 text-cyan-700" />
          <h3 className="text-sm font-bold text-gray-900">إعدادات المعامل</h3>
        </div>
        <div className="grid gap-2 sm:grid-cols-3">
          <Link href="/settings/labs" className="rounded-lg border border-white bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:text-cyan-700 hover:shadow-sm transition">
            إدارة المعامل
          </Link>
          <Link href="/settings/lab-work-types" className="rounded-lg border border-white bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:text-cyan-700 hover:shadow-sm transition">
            أنواع الأعمال
          </Link>
          <Link href="/settings/lab-pricing" className="rounded-lg border border-white bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:text-cyan-700 hover:shadow-sm transition">
            <span className="inline-flex items-center gap-1">
              <Banknote className="w-3.5 h-3.5" />
              تسعير المعامل
            </span>
          </Link>
        </div>
      </div>

      <div className="flex items-center gap-3 pt-2">
        <button
          type="submit"
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ الإعدادات"}
        </button>
        {saved && <span className="text-sm text-green-600 font-medium">✓ تم الحفظ</span>}
      </div>
    </form>
  );
}
