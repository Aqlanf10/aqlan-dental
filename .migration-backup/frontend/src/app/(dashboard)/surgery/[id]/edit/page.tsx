"use client";
import { Suspense, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, ArrowRight, Scissors, User } from "lucide-react";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { SURGERY_TYPES } from "@/types/surgery";
import type { SurgeryCase } from "@/types/surgery";

// ── Types ────────────────────────────────────────────────────────────────────

interface Doctor {
  id: string;
  name: string;
  color?: string;
}

// ── Schema ───────────────────────────────────────────────────────────────────

const schema = z.object({
  doctorId:      z.string().optional(),
  surgeryType:   z.string().min(1, "نوع الجراحة مطلوب"),
  teethInvolved: z.string().optional(),
});
type FormData = z.infer<typeof schema>;

// ── Shared styles ────────────────────────────────────────────────────────────

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
    err ? "border-red-400" : "border-gray-300"
  );

// ── Form Component ───────────────────────────────────────────────────────────

function EditSurgeryForm() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();

  // ── State ──────────────────────────────────────────────────────────────────
  const [surgeryCase, setSurgeryCase] = useState<SurgeryCase | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [serverError, setServerError] = useState("");
  const [saving, setSaving] = useState(false);
  // FE-13: useDoctors() replaces the doctors half of the Promise.all.
  const { data: doctors = [] } = useDoctors();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  // ── Fetch existing case (doctors come from useDoctors()) ────────────────
  useEffect(() => {
    api.get<SurgeryCase>(`/api/surgery-cases/${id}`)
      .then((caseRes) => {
        const sc = caseRes.data;
        setSurgeryCase(sc);

        // Pre-populate form
        reset({
          doctorId: sc.doctorId ?? "",
          surgeryType: sc.surgeryType,
          teethInvolved: Array.isArray(sc.teethInvolved)
            ? sc.teethInvolved.join(", ")
            : sc.teethInvolved ?? "",
        });
      })
      .catch((err) => {
        if (err?.response?.status === 404) {
          setNotFound(true);
        }
      })
      .finally(() => setLoading(false));
  }, [id, reset]);

  // ── Submit ─────────────────────────────────────────────────────────────────
  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const payload = {
        doctorId: data.doctorId || undefined,
        surgeryType: data.surgeryType,
        teethInvolved: data.teethInvolved
          ? data.teethInvolved
              .split(/[,،\s]+/)
              .map((t) => t.trim())
              .filter(Boolean)
          : undefined,
      };
      await api.put(`/api/surgery-cases/${id}`, payload);
      toast.success("تم تحديث الحالة الجراحية بنجاح");
      router.push(`/surgery/${id}`);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  // ── Loading skeleton ──────────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="space-y-4 max-w-3xl animate-pulse">
        <div className="h-5 w-48 bg-gray-100 rounded" />
        <div className="h-8 w-64 bg-gray-100 rounded" />
        <div className="h-48 bg-gray-100 rounded-xl" />
        <div className="h-10 w-32 bg-gray-100 rounded-lg" />
      </div>
    );
  }

  // ── Not found ─────────────────────────────────────────────────────────────
  if (notFound || !surgeryCase) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-gray-400">
        <Scissors className="w-12 h-12 mb-3 opacity-40" />
        <p className="text-sm">لم يتم العثور على الحالة الجراحية</p>
        <Link href="/surgery" className="mt-4 text-sm text-clinic-blue hover:underline">
          العودة إلى قائمة الجراحة
        </Link>
      </div>
    );
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="space-y-5 max-w-3xl">
      {/* ── Breadcrumb ──────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/surgery" className="hover:text-clinic-blue transition">
          الجراحة
        </Link>
        <span>/</span>
        <Link
          href={`/surgery/${id}`}
          className="hover:text-clinic-blue transition font-mono"
        >
          {surgeryCase.caseNumber}
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">تعديل</span>
      </div>

      {/* ── Back + Title ────────────────────────────────────────────────────── */}
      <div className="flex items-center gap-3">
        <Link
          href={`/surgery/${id}`}
          className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
        >
          <ArrowRight className="w-4 h-4" />
        </Link>
        <Scissors className="w-5 h-5 text-clinic-blue" />
        <h1 className="text-2xl font-extrabold text-gray-900">تعديل الحالة الجراحية</h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {/* ── Server error ──────────────────────────────────────────────────── */}
        {serverError && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">
            {serverError}
          </div>
        )}

        {/* ── Patient & Case Info (read-only) ─────────────────────────────── */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-3">معلومات الحالة</h2>

          <div className="flex flex-wrap items-center gap-4">
            {/* Patient */}
            <div className="flex items-center gap-2">
              <User className="w-4 h-4 text-gray-400" />
              <Link
                href={`/patients/${surgeryCase.patientId}`}
                className="text-sm font-semibold text-gray-900 hover:text-clinic-blue transition"
              >
                {surgeryCase.patientName}
              </Link>
              <span className="text-xs text-gray-400 font-mono">
                {surgeryCase.patientNumber}
              </span>
            </div>

            {/* Case number badge */}
            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-mono font-semibold bg-clinic-blue/10 text-clinic-blue border border-clinic-blue/20">
              {surgeryCase.caseNumber}
            </span>

            {/* Created date */}
            <span className="text-xs text-gray-400">
              {formatArabicDate(surgeryCase.createdAt)}
            </span>
          </div>
        </div>

        {/* ── Editable fields ──────────────────────────────────────────────── */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Surgery type */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              نوع الجراحة <span className="text-red-500">*</span>
            </label>
            <select
              {...register("surgeryType")}
              className={inputCls(errors.surgeryType?.message)}
            >
              <option value="">اختر...</option>
              {SURGERY_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
            {errors.surgeryType && (
              <p className="mt-1 text-xs text-red-600">{errors.surgeryType.message}</p>
            )}
          </div>

          {/* Doctor */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              الطبيب الجراح
            </label>
            <select {...register("doctorId")} className={inputCls()}>
              <option value="">اختر...</option>
              {doctors.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>

          {/* Teeth involved */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              الأسنان المعنية
            </label>
            <input
              {...register("teethInvolved")}
              className={inputCls()}
              placeholder="مثلاً: 18، 28، 38، 48"
              dir="ltr"
            />
          </div>
        </div>

        {/* ── Actions ───────────────────────────────────────────────────────── */}
        <div className="flex justify-end gap-3 pb-4">
          <Link
            href={`/surgery/${id}`}
            className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
          >
            إلغاء
          </Link>
          <button
            type="submit"
            disabled={saving}
            className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "حفظ التعديلات"}
          </button>
        </div>
      </form>
    </div>
  );
}

// ── Page (wrapped in Suspense) ───────────────────────────────────────────────

export default function EditSurgeryPage() {
  return (
    <Suspense fallback={<div className="animate-pulse h-96 bg-gray-100 rounded-xl" />}>
      <EditSurgeryForm />
    </Suspense>
  );
}
