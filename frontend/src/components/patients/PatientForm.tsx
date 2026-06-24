"use client";
import { useState, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, ChevronDown, AlertTriangle, ExternalLink, X, KeyRound, Copy, Check } from "lucide-react";
import type { CreatePatientRequest } from "@/types/patient";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { useAuthStore } from "@/stores/authStore";
import { cn, normalizePhone } from "@/lib/utils";

const schema = z.object({
  firstName:   z.string().min(1, "الاسم الأول مطلوب"),
  middleName:  z.string().optional(),
  lastName:    z.string().min(1, "الاسم الأخير مطلوب"),
  dateOfBirth: z.string().optional(),
  gender:      z.enum(["Male", "Female"]).optional(),
  phone:       z.string()
    .optional()
    .refine((val) => !val || /^[0-9+\-\s]{7,15}$/.test(val), {
      message: "رقم الهاتف غير صحيح",
    }),
  email:       z.string()
    .optional()
    .refine((val) => !val || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val), {
      message: "البريد الإلكتروني غير صحيح",
    }),
  whatsApp:    z.string()
    .optional()
    .refine((val) => !val || /^[0-9+\-\s]{7,15}$/.test(val), {
      message: "رقم واتساب غير صحيح",
    }),
  address:     z.string().optional(),
  occupation:  z.string().optional(),
  referralSource: z.string().optional(),
  chronicDiseases:    z.string().optional(),
  currentMedications: z.string().optional(),
  drugAllergies:      z.string().optional(),
  bleedingDisorders:  z.boolean(),
  isPregnant:         z.enum(["Yes", "No", "N/A"]).optional(),
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
  primaryDoctorId:    z.string().optional(),
});
type FormData = z.infer<typeof schema>;

interface DuplicateMatch {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string;
  matchType: string;
}

interface DoctorOption { id: string; name: string; }

interface Props {
  defaultValues?: Partial<FormData>;
  patientId?: string;
}

function FormPanel({ title, description, children }: { title: string; description?: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(true);
  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] overflow-hidden shadow-sm transition-all duration-300">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="w-full flex items-center justify-between px-5 py-4 text-start hover:bg-slate-50/50 transition border-b border-slate-100"
      >
        <div>
          <h3 className="font-extrabold text-sm text-[#0d2137]">{title}</h3>
          {description && <p className="text-[11px] text-slate-400 font-medium mt-0.5">{description}</p>}
        </div>
        <ChevronDown className={cn("w-4 h-4 text-slate-400 transition-transform duration-200", open && "rotate-180")} />
      </button>
      {open && <div className="p-5 grid grid-cols-1 md:grid-cols-2 gap-4 bg-white">{children}</div>}
    </div>
  );
}

function Field({ label, error, required, children }: { label: string; error?: string; required?: boolean; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-xs font-bold text-slate-700 mb-1.5">
        {label} {required && <span className="text-red-500 font-extrabold">*</span>}
      </label>
      {children}
      {error && <p className="mt-1 text-xs text-red-500 font-semibold">{error}</p>}
    </div>
  );
}

const inputCls = (err?: string) =>
  cn("w-full h-10 px-3 py-2 text-sm rounded-lg border bg-[#f8fafc] text-[#0d2137] outline-none transition-all duration-200 focus:bg-white focus:ring-2 focus:ring-[#3d7ab5]/20 focus:border-[#3d7ab5]", err ? "border-red-400 bg-red-50/30" : "border-slate-200");

const checkboxCls = "w-4 h-4 text-[#3d7ab5] border-slate-300 rounded focus:ring-[#3d7ab5]";

export function PatientForm({ defaultValues, patientId }: Props) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [duplicateWarning, setDuplicateWarning] = useState<DuplicateMatch[] | null>(null);
  const [forceSave, setForceSave] = useState(false);
  const [portalDialog, setPortalDialog] = useState<{ username: string; temporaryPassword: string; patientId: string } | null>(null);
  const [copiedField, setCopiedField] = useState<string | null>(null);
  // FE-13: useDoctors() replaces useEffect + api.get + useState.
  const { data: doctors = [] } = useDoctors();
  const checkTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const { register, handleSubmit, watch, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: defaultValues ?? {
      bleedingDisorders: false, tmjProblems: false, mouthBreathing: false,
      bruxism: false, thumbSucking: false, tongueThrusing: false,
    },
  });

  // Watch fields for duplicate detection
  const phone = watch("phone");
  const whatsApp = watch("whatsApp");
  const firstName = watch("firstName");
  const lastName = watch("lastName");
  const dateOfBirth = watch("dateOfBirth");

  const checkDuplicates = useCallback(async () => {
    try {
      const params = new URLSearchParams();
      if (phone) params.set("phone", normalizePhone(phone));
      if (whatsApp) params.set("whatsApp", normalizePhone(whatsApp));
      if (firstName) params.set("firstName", firstName);
      if (lastName) params.set("lastName", lastName);
      if (dateOfBirth) params.set("dateOfBirth", dateOfBirth);
      if (patientId) params.set("excludeId", patientId);
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

  const handleFieldChange = () => {
    if (checkTimer.current) clearTimeout(checkTimer.current);
    checkTimer.current = setTimeout(checkDuplicates, 500);
  };

  const onSubmit = async (data: FormData) => {
    if (duplicateWarning && duplicateWarning.length > 0) {
      const hasPhoneOrNumberMatch = duplicateWarning.some(
        (m) => m.matchType === "phone" || m.matchType === "whatsapp" || m.matchType === "patientNumber"
      );
      if (hasPhoneOrNumberMatch) {
        setServerError("لا يمكن حفظ المريض — رقم الهاتف أو الواتساب أو رقم الملف مكرر. راجع الملف الموجود.");
        return;
      }
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
        dateOfBirth: data.dateOfBirth, gender: data.gender,
        phone: data.phone?.trim() || undefined,
        email: data.email?.trim() || undefined,
        whatsApp: data.whatsApp?.trim() || undefined,
        address: data.address, occupation: data.occupation,
        referralSource: data.referralSource,
        primaryDoctorId: data.primaryDoctorId || undefined,
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
        const { data: created } = await api.post<{ id: string; patientNumber?: string }>("/api/patients", payload);

        const userRole = useAuthStore.getState().user?.role;
        if (userRole === "Admin" || userRole === "Reception") {
          try {
            const { data: portalData } = await api.post<{
              username: string;
              temporaryPassword?: string;
              message: string;
              alreadyExists: boolean;
            }>(`/api/portal/credentials/${created.id}/create`);

            if (!portalData.alreadyExists && portalData.temporaryPassword) {
              setPortalDialog({
                username: portalData.username,
                temporaryPassword: portalData.temporaryPassword,
                patientId: created.id,
              });
              return;
            }
          } catch {
            // non-blocking portal error
          }
        }

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

  const copyField = async (text: string, field: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedField(field);
      setTimeout(() => setCopiedField(null), 2000);
    } catch {}
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {serverError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3.5 text-sm font-semibold animate-pulse">
          {serverError}
        </div>
      )}

      {/* Duplicate Warning Panel */}
      {duplicateWarning && duplicateWarning.length > 0 && (
        <div className="bg-amber-50 border border-amber-300 rounded-xl p-4 space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2 text-amber-800 font-bold text-sm">
              <AlertTriangle className="w-5 h-5 text-amber-600 animate-bounce" />
              تنبيه هام: تم العثور على سجلات مطابقة في النظام!
            </div>
            <button type="button" onClick={() => setDuplicateWarning(null)} className="text-amber-600 hover:text-amber-800">
              <X className="w-4 h-4" />
            </button>
          </div>
          <p className="text-xs text-amber-700">
            رقم الهاتف أو الاسم المدخل مستخدم بالفعل لملف مريض آخر. يرجى مراجعة القائمة لتفادي تكرار الملفات.
          </p>
          <div className="space-y-2">
            {duplicateWarning.map((m, i) => (
              <div key={i} className="flex items-center justify-between bg-white rounded-lg border border-amber-200 p-3 shadow-sm">
                <div>
                  <p className="font-extrabold text-xs text-[#0d2137]">{m.fullName}</p>
                  <p className="text-[10px] text-slate-400 mt-1 font-mono">
                    ملف رقم: {m.patientNumber} {m.phone && `· هاتف: ${m.phone}`} · نوع التطابق: {MATCH_LABELS[m.matchType] ?? m.matchType}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => router.push(`/patients/${m.id}`)}
                  className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 transition duration-200"
                >
                  <ExternalLink className="w-3.5 h-3.5" />
                  فتح الملف الحالي
                </button>
              </div>
            ))}
          </div>
          {duplicateWarning.every((m) => m.matchType === "name") && (
            <button
              type="button"
              onClick={() => { setForceSave(true); setServerError(""); }}
              className="px-3 py-1.5 text-xs font-bold rounded-lg border border-amber-400 text-amber-800 hover:bg-amber-100/50 transition"
            >
              حفظ الملف على أي حال (حالة تشابه أسماء فقط)
            </button>
          )}
        </div>
      )}

      {/* 1. Basic Information Panel */}
      <FormPanel title="البيانات الأساسية" description="أدخل الاسم الشخصي للمريض وتاريخ الميلاد والبيانات الجندرية">
        <Field label="الاسم الأول" required error={errors.firstName?.message}>
          <input {...register("firstName")} className={inputCls(errors.firstName?.message)} placeholder="الاسم الأول" onChange={(e) => { register("firstName").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="اسم الأب والعائلة الوسطى" error={errors.middleName?.message}>
          <input {...register("middleName")} className={inputCls()} placeholder="اسم الأب (اختياري)" />
        </Field>
        <Field label="اللقب / الاسم الأخير" required error={errors.lastName?.message}>
          <input {...register("lastName")} className={inputCls(errors.lastName?.message)} placeholder="اسم العائلة" onChange={(e) => { register("lastName").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="تاريخ الميلاد">
          <input {...register("dateOfBirth")} type="date" className={inputCls()} onChange={(e) => { register("dateOfBirth").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="الجنس">
          <select {...register("gender")} className={inputCls()}>
            <option value="">اختر الجنس...</option>
            <option value="Male">ذكر</option>
            <option value="Female">أنثى</option>
          </select>
        </Field>
      </FormPanel>

      {/* 2. Contact Information Panel */}
      <FormPanel title="بيانات الاتصال" description="وسائل التواصل الهاتفي والإلكتروني لتنسيق المواعيد والتنبيهات">
        <Field label="رقم الهاتف الأساسي" error={errors.phone?.message}>
          <input {...register("phone")} className={inputCls(errors.phone?.message)} placeholder="7XXXXXXXX" dir="ltr" onChange={(e) => { register("phone").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="رقم الواتساب" error={errors.whatsApp?.message}>
          <input {...register("whatsApp")} className={inputCls(errors.whatsApp?.message)} placeholder="7XXXXXXXX" dir="ltr" onChange={(e) => { register("whatsApp").onChange(e); handleFieldChange(); }} />
        </Field>
        <Field label="البريد الإلكتروني" error={errors.email?.message}>
          <input {...register("email")} type="email" className={inputCls(errors.email?.message)} placeholder="example@email.com" dir="ltr" />
        </Field>
      </FormPanel>

      {/* 3. Address Panel */}
      <FormPanel title="العنوان والوظيفة" description="البيانات الجغرافية والمهنية للمريض">
        <Field label="العنوان السكني">
          <input {...register("address")} className={inputCls()} placeholder="المحافظة، المدينة، الحي، الشارع..." />
        </Field>
        <Field label="المهنة الحالية">
          <input {...register("occupation")} className={inputCls()} placeholder="طبيعة العمل أو الوظيفة" />
        </Field>
      </FormPanel>

      {/* 4. Medical Notes Panel */}
      <FormPanel title="التاريخ الطبي والصحي" description="سجل الحالات والتحذيرات الطبية لحماية سلامة المريض أثناء العلاج">
        <div className="md:col-span-2">
          <Field label="الأمراض المزمنة">
            <textarea {...register("chronicDiseases")} rows={2} className="w-full p-3 text-sm rounded-lg border border-slate-200 bg-[#f8fafc] text-[#0d2137] focus:bg-white focus:ring-2 focus:ring-[#3d7ab5]/20 focus:border-[#3d7ab5] focus:outline-none" placeholder="الضغط، السكري، أمراض القلب والكلى، الصرع..." />
          </Field>
        </div>
        <Field label="الأدوية المستعملة حالياً">
          <input {...register("currentMedications")} className={inputCls()} placeholder="المسكنات، مميعات الدم، إلخ..." />
        </Field>
        <Field label="حساسية الأدوية والمواد">
          <input {...register("drugAllergies")} className={inputCls()} placeholder="البنسلين، التخدير، اللاتكس..." />
        </Field>
        <Field label="العمليات الجراحية السابقة">
          <input {...register("previousSurgeries")} className={inputCls()} placeholder="العمليات السابقة وتواريخها..." />
        </Field>
        <Field label="حالة الحمل (للإناث)">
          <select {...register("isPregnant")} className={inputCls()}>
            {/* Values must match the backend MedicalHistoryDtoValidator
                ValidPregnancyValues set (Yes/No/N/A, case-insensitive).
                Previously sent "na"/"yes"/"no" — "na" was rejected (no slash)
                causing "خطأ" on patient create. Now sends "N/A"/"No"/"Yes". */}
            <option value="N/A">لا ينطبق</option>
            <option value="No">لا يوجد حمل</option>
            <option value="Yes">يوجد حمل</option>
          </select>
        </Field>
        <div className="flex flex-col gap-3 md:col-span-2 pt-2">
          <label className="flex items-center gap-2.5 text-xs font-bold text-slate-700 cursor-pointer">
            <input type="checkbox" {...register("bleedingDisorders")} className={checkboxCls} />
            اضطرابات نزيف الدم أو استخدام مميعات
          </label>
          <label className="flex items-center gap-2.5 text-xs font-bold text-slate-700 cursor-pointer">
            <input type="checkbox" {...register("tmjProblems")} className={checkboxCls} />
            مشاكل أو آلام في المفصل الفكي الصدغي (TMJ)
          </label>
        </div>
        <div className="md:col-span-2">
          <Field label="ملاحظات وتوصيات طبية إضافية">
            <textarea {...register("medNotes")} rows={2} className="w-full p-3 text-sm rounded-lg border border-slate-200 bg-[#f8fafc] text-[#0d2137] focus:bg-white focus:ring-2 focus:ring-[#3d7ab5]/20 focus:border-[#3d7ab5] focus:outline-none" placeholder="أي تفاصيل صحية أخرى يجب مراعاتها..." />
          </Field>
        </div>
      </FormPanel>

      {/* 5. Administrative & Dental Panel */}
      <FormPanel title="المعلومات الإدارية وتاريخ الأسنان" description="الطبيب المتابع، مصدر التعرف على المركز، والتفاصيل العلاجية السنية">
        {doctors.length > 0 && (
          <Field label="الطبيب المسؤول عن الملف">
            <select {...register("primaryDoctorId")} className={inputCls()}>
              <option value="">اختر الطبيب المسؤول...</option>
              {doctors.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
        )}
        <Field label="مصدر الإحالة / التعرف على المركز">
          <input {...register("referralSource")} className={inputCls()} placeholder="صديق، شبكات التواصل، لافتة، إلخ..." />
        </Field>
        <div className="md:col-span-2">
          <Field label="الشكوى الرئيسية السنية">
            <textarea {...register("chiefComplaint")} rows={2} className="w-full p-3 text-sm rounded-lg border border-slate-200 bg-[#f8fafc] text-[#0d2137] focus:bg-white focus:ring-2 focus:ring-[#3d7ab5]/20 focus:border-[#3d7ab5] focus:outline-none" placeholder="آلام الأسنان، تجميل، تقويم، زراعة..." />
          </Field>
        </div>
        <div className="md:col-span-2">
          <Field label="العلاجات السنية السابقة">
            <textarea {...register("previousTreatments")} rows={2} className="w-full p-3 text-sm rounded-lg border border-slate-200 bg-[#f8fafc] text-[#0d2137] focus:bg-white focus:ring-2 focus:ring-[#3d7ab5]/20 focus:border-[#3d7ab5] focus:outline-none" placeholder="حشوات، سحب عصب، خلع، تقويم سابق..." />
          </Field>
        </div>
        <div className="flex flex-col gap-3 md:col-span-2 pt-2 border-t border-slate-100">
          <p className="text-xs font-bold text-slate-700">العادات الفموية الضارة:</p>
          {[
            { name: "mouthBreathing" as const, label: "التنفس من الفم" },
            { name: "bruxism" as const,        label: "صرير أو كز الأسنان ليلاً (Bruxism)" },
            { name: "thumbSucking" as const,   label: "مص الأصابع والإبهام" },
            { name: "tongueThrusing" as const, label: "دفع اللسان للأمام أثناء البلع (Tongue Thrusting)" },
          ].map(({ name, label }) => (
            <label key={name} className="flex items-center gap-2.5 text-xs font-semibold text-slate-600 cursor-pointer">
              <input type="checkbox" {...register(name)} className={checkboxCls} />
              {label}
            </label>
          ))}
        </div>
        <div className="md:col-span-2">
          <Field label="ملاحظات وتفاصيل الفحص السني">
            <textarea {...register("dentalNotes")} rows={2} className="w-full p-3 text-sm rounded-lg border border-slate-200 bg-[#f8fafc] text-[#0d2137] focus:bg-white focus:ring-2 focus:ring-[#3d7ab5]/20 focus:border-[#3d7ab5] focus:outline-none" placeholder="تفاصيل الفحص السريري للأسنان واللثة..." />
          </Field>
        </div>
      </FormPanel>

      {/* Portal Account Credentials Popup - single rendering on creation */}
      {portalDialog && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl overflow-hidden">
            <div className="px-6 py-4 bg-[#0d2137] flex items-center gap-2">
              <KeyRound className="w-5 h-5 text-[#f5922e] animate-bounce" />
              <h3 className="text-white font-bold text-sm">تم إنشاء حساب بوابة المريض تلقائياً</h3>
            </div>
            <div className="p-6 space-y-4">
              <div className="flex items-start gap-2 p-3 rounded-lg bg-red-500/10 border border-red-500/20">
                <AlertTriangle className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
                <div>
                  <p className="text-xs font-bold text-red-500">تنبيه أمان هام</p>
                  <p className="text-[11px] text-red-600 mt-0.5">
                    تظهر كلمة المرور المؤقتة لمرة واحدة فقط! يرجى نسخها وتزويد المريض بها قبل إغلاق هذه النافذة.
                  </p>
                </div>
              </div>

              <div className="p-3 rounded-lg bg-slate-50 border border-slate-100">
                <p className="text-[10px] text-slate-400 font-bold mb-1">اسم المستخدم للمريض</p>
                <div className="flex items-center justify-between">
                  <span className="font-mono text-base font-extrabold text-[#0d2137]">{portalDialog.username}</span>
                  <button
                    type="button"
                    onClick={() => copyField(portalDialog.username, "dlg-username")}
                    className="p-1.5 rounded hover:bg-slate-200/55 transition"
                  >
                    {copiedField === "dlg-username" ? <Check className="w-4 h-4 text-green-500" /> : <Copy className="w-4 h-4 text-[#3d7ab5]" />}
                  </button>
                </div>
              </div>

              <div className="p-3 rounded-lg bg-orange-500/5 border border-orange-500/10">
                <p className="text-[10px] text-[#f5922e] font-bold mb-1">كلمة المرور المؤقتة</p>
                <div className="flex items-center justify-between">
                  <span className="font-mono text-base font-extrabold text-[#0d2137]" dir="ltr">{portalDialog.temporaryPassword}</span>
                  <button
                    type="button"
                    onClick={() => copyField(portalDialog.temporaryPassword, "dlg-password")}
                    className="p-1.5 rounded hover:bg-slate-200/55 transition"
                  >
                    {copiedField === "dlg-password" ? <Check className="w-4 h-4 text-green-500" /> : <Copy className="w-4 h-4 text-[#f5922e]" />}
                  </button>
                </div>
              </div>

              <div className="p-3 rounded-lg bg-slate-50 border border-slate-100">
                <p className="text-[10px] text-slate-400 font-bold mb-1">رابط تسجيل الدخول للبوابة</p>
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-mono text-[#3d7ab5] break-all" dir="ltr">
                    {typeof window !== "undefined" ? `${window.location.origin}/portal/login` : "/portal/login"}
                  </span>
                  <button
                    type="button"
                    onClick={() => copyField(`${typeof window !== "undefined" ? window.location.origin : ""}/portal/login`, "dlg-url")}
                    className="p-1.5 rounded hover:bg-slate-200/55 transition flex-shrink-0 ms-2"
                  >
                    {copiedField === "dlg-url" ? <Check className="w-4 h-4 text-green-500" /> : <Copy className="w-4 h-4 text-slate-400" />}
                  </button>
                </div>
              </div>

              <button
                type="button"
                onClick={() => {
                  const info = `بيانات دخول بوابة المريض\nاسم المستخدم: ${portalDialog.username}\nكلمة المرور المؤقتة: ${portalDialog.temporaryPassword}\nرابط البوابة: ${typeof window !== "undefined" ? window.location.origin : ""}/portal/login`;
                  copyField(info, "dlg-all");
                }}
                className="w-full flex items-center justify-center gap-2 py-2.5 rounded-lg text-sm font-semibold transition"
                style={{ background: copiedField === "dlg-all" ? "#22c55e" : "#3d7ab5", color: "#fff" }}
              >
                {copiedField === "dlg-all" ? <><Check className="w-4 h-4" /> تم نسخ كافة المعلومات</> : <><Copy className="w-4 h-4" /> نسخ بيانات الدخول بالكامل</>}
              </button>

              <button
                type="button"
                onClick={() => {
                  setPortalDialog(null);
                  router.push(`/patients/${portalDialog.patientId}`);
                }}
                className="w-full py-2.5 text-sm font-bold rounded-lg border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 transition"
              >
                إغلاق والذهاب لملف المريض المكتمل
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Form Action Buttons */}
      <div className="flex justify-end gap-3 pt-4 border-t border-slate-100">
        <button
          type="button"
          onClick={() => router.back()}
          className="px-5 h-10 text-xs font-bold rounded-lg border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 transition duration-200"
        >
          إلغاء العملية
        </button>
        <button
          type="submit"
          disabled={saving}
          className="flex items-center gap-2 px-6 h-10 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 disabled:opacity-60 transition duration-200"
        >
          <Save className="w-4.5 h-4.5" />
          {saving ? "جارٍ حفظ البيانات..." : "حفظ ملف المريض"}
        </button>
      </div>
    </form>
  );
}
