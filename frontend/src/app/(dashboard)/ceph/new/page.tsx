"use client";
import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { Save, ArrowRight, Search } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface OrthoCase { id: string; caseNumber: string; patientName: string; }

const schema = z.object({
  orthoCaseId:  z.string().min(1, "اختر حالة تقويمية"),
  analysisType: z.string().min(1),
  xrayFileUrl:  z.string().url("رابط غير صالح").optional().or(z.literal("")),
  notes:        z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
  err ? "border-red-400" : "border-gray-300"
);

function NewCephPageInner() {
  const router = useRouter();
  const params = useSearchParams();
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [caseSearch, setCaseSearch] = useState("");
  const [caseResults, setCaseResults] = useState<OrthoCase[]>([]);
  const [showDropdown, setShowDropdown] = useState(false);

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { analysisType: "full", orthoCaseId: params.get("orthoCaseId") ?? "" },
  });

  // Pre-fill if orthoCaseId is in query
  useEffect(() => {
    const id = params.get("orthoCaseId");
    if (!id) return;
    api.get<OrthoCase[]>("/api/ortho-cases").then((r) => {
      const c = r.data.find((x) => x.id === id);
      if (c) {
        setValue("orthoCaseId", c.id);
        setCaseSearch(`${c.patientName} (${c.caseNumber})`);
      }
    }).catch(() => {});
  }, [params, setValue]);

  useEffect(() => {
    if (caseSearch.length < 2) { setCaseResults([]); return; }
    const t = setTimeout(() => {
      api.get<OrthoCase[]>(`/api/ortho-cases?search=${encodeURIComponent(caseSearch)}&pageSize=8`)
        .then((r) => setCaseResults(r.data))
        .catch(() => {});
    }, 300);
    return () => clearTimeout(t);
  }, [caseSearch]);

  const selectCase = (c: OrthoCase) => {
    setValue("orthoCaseId", c.id);
    setCaseSearch(`${c.patientName} (${c.caseNumber})`);
    setShowDropdown(false);
  };

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const res = await api.post<{ id: string }>("/api/ceph", {
        orthoCaseId: data.orthoCaseId,
        analysisType: data.analysisType,
        xrayFileUrl: data.xrayFileUrl || undefined,
        notes: data.notes,
      });
      router.push(`/ceph/${res.data.id}`);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setServerError(msg ?? "حدث خطأ أثناء الإنشاء");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-5 max-w-2xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/ceph" className="hover:text-clinic-teal transition">السيفالومتري</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">تحليل جديد</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/ceph" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إنشاء تحليل سيفالومتري</h1>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {serverError && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{serverError}</div>
        )}

        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
          {/* Ortho case */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              الحالة التقويمية <span className="text-red-500">*</span>
            </label>
            <div className="relative">
              <Search className="absolute right-3 top-2.5 w-4 h-4 text-gray-400" />
              <input
                value={caseSearch}
                onChange={(e) => { setCaseSearch(e.target.value); setShowDropdown(true); }}
                onFocus={() => caseSearch.length >= 2 && setShowDropdown(true)}
                onBlur={() => setTimeout(() => setShowDropdown(false), 150)}
                placeholder="ابحث باسم المريض أو رقم الحالة..."
                className={cn(inputCls(errors.orthoCaseId?.message), "pe-9")}
                autoComplete="off"
              />
              {showDropdown && caseResults.length > 0 && (
                <div className="absolute z-10 w-full mt-1 bg-white rounded-lg border border-gray-200 shadow-lg max-h-48 overflow-y-auto">
                  {caseResults.map((c) => (
                    <button key={c.id} type="button" onMouseDown={() => selectCase(c)}
                      className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between"
                    >
                      <span className="font-medium">{c.patientName}</span>
                      <span className="text-xs text-gray-400 font-mono">{c.caseNumber}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
            <input type="hidden" {...register("orthoCaseId")} />
            {errors.orthoCaseId && <p className="mt-1 text-xs text-red-600">{errors.orthoCaseId.message}</p>}
          </div>

          {/* Analysis type */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">نوع التحليل</label>
            <select {...register("analysisType")} className={inputCls()}>
              <option value="full">شامل (جميع التحاليل)</option>
              <option value="steiner">ستاينر (يشمل الأنسجة الرخوة - خط S)</option>
              <option value="tweed">تويد</option>
              <option value="mcnamara">ماكنامارا</option>
              <option value="ricketts">ريكتس</option>
              <option value="downs">داونز</option>
              <option value="wits">وتس (Wits)</option>
            </select>
          </div>

          {/* X-ray URL */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">رابط صورة الأشعة</label>
            <input {...register("xrayFileUrl")} type="url"
              className={inputCls(errors.xrayFileUrl?.message)}
              placeholder="https://... (PACS أو خادم الأشعة)"
              dir="ltr"
            />
            {errors.xrayFileUrl && <p className="mt-1 text-xs text-red-600">{errors.xrayFileUrl.message}</p>}
            <p className="text-xs text-gray-400 mt-1">
              يمكن إضافة الرابط لاحقاً من داخل التحليل
            </p>
          </div>

          {/* Notes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
            <textarea {...register("notes")} rows={2} className={inputCls()} />
          </div>
        </div>

        <div className="flex justify-end gap-3 pb-4">
          <Link href="/ceph" className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
            إلغاء
          </Link>
          <button type="submit" disabled={saving}
            className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الإنشاء..." : "إنشاء التحليل"}
          </button>
        </div>
      </form>
    </div>
  );
}

export default function NewCephPage() {
  return (
    <Suspense>
      <NewCephPageInner />
    </Suspense>
  );
}
