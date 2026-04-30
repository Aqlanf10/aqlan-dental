"use client";
import { useState, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, ChevronDown, AlertTriangle, ExternalLink, X } from "lucide-react";
import type { CreatePatientRequest } from "@/types/patient";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

// ─── توحيد صيغة رقم الهاتف اليمنية ──────────────────────────────────────────
function normalizePhone(phone: string): string {
  if (!phone) return "";
  let result = phone
    // Convert Arabic/Indic digits to English
    .replace(/[٠٠-٩۹]/g, (c) => {
      const map: Record<string, string> = {
        '٠': '0', '۰': '0', '١': '1', '۱': '1', '٢': '2', '۲': '2',
        '٣': '3', '۳': '3', '٤': '4', '۴': '4', '٥': '5', '۵': '5',
        '٦': '6', '۶': '6', '٧': '7', '۷': '7', '٨': '8', '۸': '8',
        '٩': '9', '۹': '9',
      };
      return map[c] ?? c;
    })
    // Remove spaces, dashes, parentheses, dots
    .replace(/[\s\-.()]/g, "");
  
  if (result.startsWith("+")) result = result.slice(1);
  if (result.startsWith("00")) result = result.slice(2);
  if (result.startsWith("0") && result.length >= 9) result = "967" + result.slice(1);
  else if (result.startsWith("7") && result.length === 9) result = "967" + result;
  
  return result;
}

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
  chronicDiseases:    z.string().optional(),
  currentMedications: z.string().optional(),
  drugAllergies:      z.string().optional(),
  bleedingDisorders:  z.boolean(),
  isPregnant:         z.enum(["yes", "no", "na"]).optional(),
  tmjProblems:        z.boolean(),
  previousSurgeries:  z.string().optional(),
  medNotes:           z.string().optional(),
  chiefComplaint:     z.string().optional(),
  previousTreatments: z.string().optional(),
  mouthBreathing:     z.boolean(),
  bruxism:            z.boolean(),
  thumbSucking:       z.boolean(),
  tongueThrusing:     z.boolean(),
  dentalNotes:        z.string().optional(),
});
type FormData = z.infer<typeof schema>;

interface DuplicateMatch {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
  matchType: string;
}

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

function Field({ label, error, required, children }: { label: string; error?: string; required?: boolean; children: React.ReactNode }) {
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
  cn("w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal", err ? "border-red-400" : "border-gray-300");

const checkboxCls = "w-4 h-4 accent-clinic-teal rounded";

export function PatientForm({ defaultValues, patientId }: Props) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [duplicateWarning, setDuplicateWarning] = useState<DuplicateMatch[] | null>(null);
  const [forceSave, setForceSave] = useState(false);
  const checkTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const { register, handleSubmit, watch, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: defaultValues ?? {
      bleedingDisorders: false, tmjProblems: false, mouthBreathing: false,
      bruxism: false, thumbSucking: false, tongueThrusing: false,
    },
  });

  // Watch phone/whatsApp/name fields for real-time duplicate check
  const phone = watch("phone");
  const whatsApp = watch("whatsApp");
  const firstName = watch("firstName");
  const lastName = watch("lastName");
  const dateOfBirth = watch("dateOfBirth");

  const checkDuplicates = useCallback(async () => {
    if (patientId) return; // Skip for editing existing patient
    try {
      const params = new URLSearchParams();
      if (phone) params.set("phone", normalizePhone(phone));
      if (whatsApp) params.set("whatsApp", normalizePhone(whatsApp));
      if (firstName) params.set("firstName", firstName);
      if (lastName) params.set("lastName", lastName);
      if (dateOfBirth) params.set("dateOfBirth", dateOfBirth);
      if (!params.toString()) { setDuplicateWarning(null); return; }

      const { data } = await api.get(`/api/patients/check-duplicate?${params}`);
      if (data.isDuplicate) {
        setDuplicateWarning(data.matches);
        setForceSave(false);
      } else {
        setDuplicateWarning(null);
      }
    } catch { /* silent */ }
  }, [phone, whatsApp, firstName, lastName, dateOfBirth, patientId]);

  // Debounced duplicate check on field change
  const handleFieldChange = () => {
    if (checkTimer.current) clearTimeout(checkTimer.current);
    checkTimer.current = setTimeout(checkDuplicates, 500);
  };

  const onSubmit = async (data: FormData) => {
    // If duplicates found by phone/whatsapp/patientNumber — prevent save entirely
    if (duplicateWarning && duplicateWarning.length > 0) {
      const hasPhoneOrNumberMatch = duplicateWarning.some(
        (m) => m.matchType === "phone" || m.matchType === "whatsapp" || m.matchType === "patientNumber"
      );
      if (hasPhoneOrNumberMatch) {
        setServerError("لا يمكن حفظ المريض — رقم الهاتف أو الواتساب أو رقم الملف مكرر. راجع الملف الموجود.");
        return;
      }
      // Name-only match: allow force save
      if (!forceSave) {
        setServerError("يوجد مريض مشابه بالاسم — راجع التحذير أعلاه أو اضغط 'حفظ على أي حال'");
        return;
      }
    }

    setSaving(true);
    setServerError("");
    setForceSave(false);
    try {
      const payload: CreatePatientRequest = {
        firstName: data.firstName, middleName: data.middleName, lastName: data.lastName,
        dateOfBirth: data.dateOfBirth, gender: data.gender, phone: data.phone,
        whatsApp: data.whatsApp, address: data.address, occupation: data.occupation,
        referralSource: data.referralSource,
        medicalHistory: {
          chronicDiseases: data.chronicDiseases, currentMedications: data.currentMedications,
          drugAllergies: data.drugAllergies, bleedingDisorders: data.bleedingDisorders,
          isPregnant: data.isPregnant, tmjProblems: data.tmjProblems,
          previousSurgeries: data.previousSurgeries, notes: data.medNotes,
        },
        dentalHistory: {
          chiefComplaint: data.chiefComplaint, previousTreatments: data.previousTreatments,
          mouthBreathing: data.mouthBreathing, bruxism: data.bruxism,
          thumbSucking: data.thumbSucking, tongueThrusing: data.tongueThrusing,
          notes: data.dentalNotes,
        },
      };

      if (patientId) {
        await api.put(`/api/patients/${patientId}`, payload);
        router.push(`/patients/${patientId}`);
      } else {
        const { data: created } = await api.post<{ id: string }>("/api/patients", payload);
        router.push(`/patients/${created.id}`);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  const MATCH_LABELS: Record<string, string> = {
    phone: "رقم الهاتف", whatsapp: "واتساب", patientNumber: "رقم الملف", name: "الاسم",
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
      {serverError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
          {serverError}
        </div>
      )}

      {/* Duplicate Warning */}
      {duplicateWarning && duplicateWarning.length > 0 && (
        <div className="bg-amber-50 border border-amber-300 rounded-xl p-4">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2 text-amber-800 font-bold">
              <AlertTriangle className="w-5 h-5" />
              تحذير: يوجد مريض مشابه!
            </div>
            <button type="button" onClick={() => setDuplicateWarning(null)} className="text-amber-600 hover:text-amber-800">
              <X className="w-4 h-4" />
            </button>
          </div>
          <p className="text-sm text-amber-700 mb-3">
            هذا الرقم أو الاسم مضاف مسبقاً لمريض آخر. يرجى مراجعة ملف المريض الموجود بدلاً من إنشاء ملف جديد.
          </p>
          <div className="space-y-2">
            {duplicateWarning.map((m, i) => (
              <div key={i} className="flex items-center justify-between bg-white rounded-lg border border-amber-200 p-3">
                <div>
                  <p className="font-semibold text-gray-900">{m.fullName}</p>
                  <p className="text-xs text-gray-500">
                    ملف رقم: {m.patientNumber} {m.phone && `· هاتف: ${m.phone}`} · تطابق: {MATCH_LABELS[m.matchType] ?? m.matchType}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => router.push(`/patients/${m.id}`)}
                  className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
                >
                  <ExternalLink className="w-3 h-3" />
                  فتح الملف
                </button>
              </div>
            ))}
          </div>
          <div className="mt-3 flex items-center gap-2">
            {duplicateWarning.every((m) => m.matchType === "name") && (
              <button
                type="button"
                onClick={() => { setForceSave(true); setServerError(""); }}
                className="px-3 py-1.5 text-xs font-medium rounded-lg border border-amber-400 text-amber-800 hover:bg-amber-100 transition"
              >
                حفظ على أي حال (مريض مختلف)
              </button>
            )}
          </div>
        </div>
      )}

      {/* Basic Info */}
      <Section title="البيانات الأساسية">
        <Field label="الاسم الأول" required error={errors.firstName?.message}>
          <input {...register("firstName")} className={inputCls(errors.firstName?.message)} placeholder="الاسم الأول" onChange={(e) => { register("firstName").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="اسم الأب" error={errors.middleName?.message}>
          <input {...register("middleName")} className={inputCls()} placeholder="اسم الأب (اختياري)" />
        </Field>
        <Field label="الاسم الأخير" required error={errors.lastName?.message}>
          <input {...register("lastName")} className={inputCls(errors.lastName?.message)} placeholder="اسم العائلة" onChange={(e) => { register("lastName").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="تاريخ الميلاد">
          <input {...register("dateOfBirth")} type="date" className={inputCls()} onChange={(e) => { register("dateOfBirth").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="الجنس">
          <select {...register("gender")} className={inputCls()}>
            <option value="">اختر...</option>
            <option value="Male">ذكر</option>
            <option value="Female">أنثى</option>
          </select>
        </Field>
        <Field label="رقم الهاتف">
          <input {...register("phone")} className={inputCls()} placeholder="07XXXXXXXX" dir="ltr" onChange={(e) => { register("phone").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="واتساب">
          <input {...register("whatsApp")} className={inputCls()} placeholder="07XXXXXXXX" dir="ltr" onChange={(e) => { register("whatsApp").onChange(e); handleFieldChange(); }} />
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
        <button type="button" onClick={() => router.back()} className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
          إلغاء
        </button>
        <button type="submit" disabled={saving} className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition">
          <Save className="w-4 h-4" />
          {saving ? "جارٍ الحفظ..." : "حفظ المريض"}
        </button>
      </div>
    </form>
  );
}
