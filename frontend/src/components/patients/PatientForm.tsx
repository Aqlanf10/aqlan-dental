"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, ChevronDown, Shield, AlertTriangle, Copy, Check } from "lucide-react";
import type { CreatePatientRequest } from "@/types/patient";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

const schema = z.object({
  firstName:   z.string().min(1, "الاسم الأول مطلوب"),
  middleName:  z.string().optional(),
  lastName:    z.string().min(1, "الاسم الأخير مطلوب"),
  dateOfBirth: z.string().optional(),
  gender:      z.enum(["Male", "Female"]).optional(),
  phone:       z.string().optional(),
  whatsApp:    z.string().optional(),
  address:     z.string().optional(),
  occupation:  z.string().optional(),
  referralSource: z.string().optional(),
  // Medical history
  chronicDiseases:    z.string().optional(),
  currentMedications: z.string().optional(),
  drugAllergies:      z.string().optional(),
  bleedingDisorders:  z.boolean(),
  isPregnant:         z.enum(["yes", "no", "na"]).optional(),
  tmjProblems:        z.boolean(),
  previousSurgeries:  z.string().optional(),
  medNotes:           z.string().optional(),
  // Dental history
  chiefComplaint:     z.string().optional(),
  previousTreatments: z.string().optional(),
  mouthBreathing:     z.boolean(),
  bruxism:            z.boolean(),
  thumbSucking:       z.boolean(),
  tongueThrusing:     z.boolean(),
  dentalNotes:        z.string().optional(),
});
type FormData = z.infer<typeof schema>;

interface Props {
  defaultValues?: Partial<FormData>;
  patientId?: string;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(true);
  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-5 py-4 text-start hover:bg-gray-50 transition"
      >
        <h3 className="font-bold text-gray-900">{title}</h3>
        <ChevronDown className={cn("w-4 h-4 text-gray-400 transition-transform", open && "rotate-180")} />
      </button>
      {open && <div className="px-5 pb-5 grid grid-cols-1 md:grid-cols-2 gap-4">{children}</div>}
    </div>
  );
}

function Field({
  label, error, required, children,
}: {
  label: string; error?: string; required?: boolean; children: React.ReactNode;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1.5">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
      {error && <p className="mt-1 text-xs text-red-600">{error}</p>}
    </div>
  );
}

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
    err ? "border-red-400" : "border-gray-300"
  );

const checkboxCls = "w-4 h-4 accent-clinic-teal rounded";

export function PatientForm({ defaultValues, patientId }: Props) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [portalCredentials, setPortalCredentials] = useState<{ username: string; temporaryPassword: string; patientId: string } | null>(null);
  const [copied, setCopied] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: defaultValues ?? {
      bleedingDisorders: false,
      tmjProblems: false,
      mouthBreathing: false,
      bruxism: false,
      thumbSucking: false,
      tongueThrusing: false,
    },
  });

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const payload: CreatePatientRequest = {
        firstName: data.firstName,
        middleName: data.middleName,
        lastName: data.lastName,
        dateOfBirth: data.dateOfBirth,
        gender: data.gender,
        phone: data.phone,
        whatsApp: data.whatsApp,
        address: data.address,
        occupation: data.occupation,
        referralSource: data.referralSource,
        medicalHistory: {
          chronicDiseases: data.chronicDiseases,
          currentMedications: data.currentMedications,
          drugAllergies: data.drugAllergies,
          bleedingDisorders: data.bleedingDisorders,
          isPregnant: data.isPregnant,
          tmjProblems: data.tmjProblems,
          previousSurgeries: data.previousSurgeries,
          notes: data.medNotes,
        },
        dentalHistory: {
          chiefComplaint: data.chiefComplaint,
          previousTreatments: data.previousTreatments,
          mouthBreathing: data.mouthBreathing,
          bruxism: data.bruxism,
          thumbSucking: data.thumbSucking,
          tongueThrusing: data.tongueThrusing,
          notes: data.dentalNotes,
        },
      };

      if (patientId) {
        await api.put(`/api/patients/${patientId}`, payload);
        router.push(`/patients/${patientId}`);
      } else {
        const { data: created } = await api.post<{ id: string; portalUsername?: string; portalTemporaryPassword?: string }>('/api/patients', payload);
        // Show portal credentials if returned
        if (created.portalUsername && created.portalTemporaryPassword) {
          setPortalCredentials({ username: created.portalUsername, temporaryPassword: created.portalTemporaryPassword, patientId: created.id });
        } else {
          router.push(`/patients/${created.id}`);
        }
        return;
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  const handleCopy = () => {
    if (!portalCredentials) return;
    navigator.clipboard.writeText(`اسم المستخدم: ${portalCredentials.username}\nكلمة المرور: ${portalCredentials.temporaryPassword}`);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  if (portalCredentials) {
    return (
      <div className="max-w-md mx-auto text-center space-y-6 py-8">
        <div className="w-16 h-16 bg-green-100 rounded-2xl flex items-center justify-center mx-auto">
          <Check className="w-8 h-8 text-green-600" />
        </div>
        <div>
          <h2 className="text-xl font-bold text-gray-900">تم تسجيل المريض بنجاح</h2>
          <p className="text-sm text-gray-500 mt-1">تم إنشاء حساب بوابة المريض تلقائياً</p>
        </div>

        <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-right space-y-3">
          <div className="flex items-start gap-2">
            <AlertTriangle className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-bold text-amber-800">اعرض هذه البيانات للمريض الآن، لن تظهر مرة أخرى</p>
            </div>
          </div>
          <div className="bg-white rounded-lg p-3 border border-amber-200 space-y-2">
            <div className="flex items-center gap-2">
              <Shield className="w-4 h-4 text-teal-600 flex-shrink-0" />
              <span className="text-xs font-semibold text-gray-600">بيانات دخول بوابة المريض</span>
            </div>
            <div>
              <p className="text-xs text-gray-500">اسم المستخدم</p>
              <p className="text-sm font-mono font-bold text-gray-900" dir="ltr">{portalCredentials.username}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">كلمة المرور المؤقتة</p>
              <p className="text-sm font-mono font-bold text-amber-700" dir="ltr">{portalCredentials.temporaryPassword}</p>
            </div>
          </div>
          <button
            onClick={handleCopy}
            className="flex items-center gap-1.5 text-xs text-teal-700 hover:text-teal-800 transition"
          >
            {copied ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
            {copied ? "تم النسخ" : "نسخ البيانات"}
          </button>
        </div>

        <div className="flex gap-3 justify-center">
          <button
            onClick={() => router.push(`/patients/${portalCredentials.patientId}`)}
            className="px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            الذهاب لملف المريض
          </button>
          <button
            onClick={() => router.push('/patients')}
            className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
          >
            قائمة المرضى
          </button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
      {serverError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
          {serverError}
        </div>
      )}

      {/* Basic Info */}
      <Section title="البيانات الأساسية">
        <Field label="الاسم الأول" required error={errors.firstName?.message}>
          <input {...register("firstName")} className={inputCls(errors.firstName?.message)} placeholder="الاسم الأول" />
        </Field>
        <Field label="اسم الأب" error={errors.middleName?.message}>
          <input {...register("middleName")} className={inputCls()} placeholder="اسم الأب (اختياري)" />
        </Field>
        <Field label="الاسم الأخير" required error={errors.lastName?.message}>
          <input {...register("lastName")} className={inputCls(errors.lastName?.message)} placeholder="اسم العائلة" />
        </Field>
        <Field label="تاريخ الميلاد">
          <input {...register("dateOfBirth")} type="date" className={inputCls()} />
        </Field>
        <Field label="الجنس">
          <select {...register("gender")} className={inputCls()}>
            <option value="">اختر...</option>
            <option value="Male">ذكر</option>
            <option value="Female">أنثى</option>
          </select>
        </Field>
        <Field label="رقم الهاتف">
          <input {...register("phone")} className={inputCls()} placeholder="07XXXXXXXX" dir="ltr" />
        </Field>
        <Field label="واتساب">
          <input {...register("whatsApp")} className={inputCls()} placeholder="07XXXXXXXX" dir="ltr" />
        </Field>
        <Field label="العنوان">
          <input {...register("address")} className={inputCls()} placeholder="المدينة، الحي..." />
        </Field>
        <Field label="المهنة">
          <input {...register("occupation")} className={inputCls()} placeholder="المهنة" />
        </Field>
        <Field label="مصدر الإحالة">
          <input {...register("referralSource")} className={inputCls()} placeholder="كيف سمع عن المركز؟" />
        </Field>
      </Section>

      {/* Medical History */}
      <Section title="التاريخ الطبي">
        <div className="md:col-span-2">
          <Field label="الأمراض المزمنة">
            <textarea {...register("chronicDiseases")} rows={2} className={inputCls()} placeholder="ضغط، سكر، قلب..." />
          </Field>
        </div>
        <Field label="الأدوية الحالية">
          <input {...register("currentMedications")} className={inputCls()} placeholder="أسماء الأدوية" />
        </Field>
        <Field label="حساسية الأدوية">
          <input {...register("drugAllergies")} className={inputCls()} placeholder="نوع الحساسية" />
        </Field>
        <Field label="العمليات السابقة">
          <input {...register("previousSurgeries")} className={inputCls()} placeholder="نوع العملية والتاريخ" />
        </Field>
        <Field label="الحمل">
          <select {...register("isPregnant")} className={inputCls()}>
            <option value="na">لا ينطبق</option>
            <option value="no">لا</option>
            <option value="yes">نعم</option>
          </select>
        </Field>
        <div className="flex flex-col gap-2 md:col-span-2">
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" {...register("bleedingDisorders")} className={checkboxCls} />
            اضطرابات النزيف
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" {...register("tmjProblems")} className={checkboxCls} />
            مشاكل المفصل الفكي (TMJ)
          </label>
        </div>
        <div className="md:col-span-2">
          <Field label="ملاحظات طبية">
            <textarea {...register("medNotes")} rows={2} className={inputCls()} />
          </Field>
        </div>
      </Section>

      {/* Dental History */}
      <Section title="التاريخ السني">
        <div className="md:col-span-2">
          <Field label="الشكوى الرئيسية">
            <textarea {...register("chiefComplaint")} rows={2} className={inputCls()} placeholder="وصف الشكوى الرئيسية..." />
          </Field>
        </div>
        <div className="md:col-span-2">
          <Field label="العلاجات السابقة">
            <textarea {...register("previousTreatments")} rows={2} className={inputCls()} />
          </Field>
        </div>
        <div className="flex flex-col gap-2 md:col-span-2">
          <p className="text-sm font-medium text-gray-700">العادات الضارة</p>
          {[
            { name: "mouthBreathing" as const, label: "التنفس الفموي" },
            { name: "bruxism" as const,        label: "صرير الأسنان (Bruxism)" },
            { name: "thumbSucking" as const,   label: "مص الإبهام" },
            { name: "tongueThrusing" as const, label: "وضع اللسان الخاطئ (Tongue Thrusting)" },
          ].map(({ name, label }) => (
            <label key={name} className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
              <input type="checkbox" {...register(name)} className={checkboxCls} />
              {label}
            </label>
          ))}
        </div>
        <div className="md:col-span-2">
          <Field label="ملاحظات سنية">
            <textarea {...register("dentalNotes")} rows={2} className={inputCls()} />
          </Field>
        </div>
      </Section>

      {/* Submit */}
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
          {saving ? "جارٍ الحفظ..." : "حفظ المريض"}
        </button>
      </div>
    </form>
  );
}
