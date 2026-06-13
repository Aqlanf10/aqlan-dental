"use client";
import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Save,
  ArrowRight,
  Search,
  ImageIcon,
  ClipboardList,
  User2,
  Activity,
  FileText,
  AlertTriangle,
} from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import CephXrayUploader from "@/components/ceph/CephXrayUploader";

interface OrthoCase {
  id: string;
  caseNumber: string;
  patientName: string;
}

const schema = z.object({
  orthoCaseId: z.string().min(1, "اختر حالة تقويمية"),
  analysisType: z.string().min(1),
  notes: z.string().optional(),
});
type FormData = z.infer<typeof schema>;

const ANALYSIS_LABELS: Record<string, string> = {
  full: "شامل (جميع التحاليل)",
  steiner: "ستاينر",
  tweed: "تويد",
  mcnamara: "ماكنامارا",
  ricketts: "ريكتس",
  downs: "داونز",
  wits: "وتس (Wits)",
};

/** Extract a real Arabic reason from the server (message OR FluentValidation errors). */
function getApiErrorMessage(error: unknown, fallback: string): string {
  const r = error as {
    response?: { data?: { message?: string; errors?: Record<string, string[]> } };
  };
  const message = r.response?.data?.message;
  if (message) return message;
  const errors = r.response?.data?.errors;
  if (errors) {
    const first = Object.values(errors).flat()[0];
    if (first) return first;
  }
  return fallback;
}

const inputCls = (err?: string) =>
  cn(
    "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue",
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
  const [selectedCaseLabel, setSelectedCaseLabel] = useState("");

  // Image state — owned by the page, fed to the uploader, sent as xrayFileUrl.
  const [xrayFileUrl, setXrayFileUrl] = useState("");
  const [uploading, setUploading] = useState(false);

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      analysisType: "full",
      orthoCaseId: params.get("orthoCaseId") ?? "",
    },
  });

  const analysisType = watch("analysisType");

  // Pre-fill if orthoCaseId is in query
  useEffect(() => {
    const id = params.get("orthoCaseId");
    if (!id) return;
    api
      .get<OrthoCase[]>("/api/ortho-cases")
      .then((r) => {
        const c = r.data.find((x) => x.id === id);
        if (c) {
          setValue("orthoCaseId", c.id);
          setCaseSearch(`${c.patientName} (${c.caseNumber})`);
          setSelectedCaseLabel(`${c.patientName} (${c.caseNumber})`);
        }
      })
      .catch(() => {});
  }, [params, setValue]);

  useEffect(() => {
    if (caseSearch.length < 2) {
      setCaseResults([]);
      return;
    }
    const t = setTimeout(() => {
      api
        .get<OrthoCase[]>(
          `/api/ortho-cases?search=${encodeURIComponent(caseSearch)}&pageSize=8`
        )
        .then((r) => setCaseResults(r.data))
        .catch(() => {});
    }, 300);
    return () => clearTimeout(t);
  }, [caseSearch]);

  const selectCase = (c: OrthoCase) => {
    const label = `${c.patientName} (${c.caseNumber})`;
    setValue("orthoCaseId", c.id, { shouldValidate: true });
    setCaseSearch(label);
    setSelectedCaseLabel(label);
    setShowDropdown(false);
  };

  const onSubmit = async (data: FormData) => {
    setSaving(true);
    setServerError("");
    try {
      const res = await api.post<{ id: string }>("/api/ceph", {
        orthoCaseId: data.orthoCaseId,
        analysisType: data.analysisType,
        xrayFileUrl: xrayFileUrl || undefined,
        notes: data.notes,
      });
      router.push(`/ceph/${res.data.id}`);
    } catch (err: unknown) {
      setServerError(getApiErrorMessage(err, "حدث خطأ أثناء الإنشاء"));
      setSaving(false);
    }
  };

  const handleCancel = async () => {
    if (xrayFileUrl.startsWith("/uploads/")) {
      const fileName = xrayFileUrl.slice("/uploads/".length);
      if (fileName && !fileName.includes("/") && !fileName.includes("..")) {
        try {
          await api.delete(`/api/uploads/${encodeURIComponent(fileName)}`);
        } catch {
          // Best effort cleanup; cancellation should never trap the user.
        }
      }
    }
    router.push("/ceph");
  };

  return (
    <div className="space-y-5">
      {/* Breadcrumb + header */}
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-500">
          <Link href="/ceph" className="hover:text-clinic-blue transition">
            السيفالومتري
          </Link>
          <span>/</span>
          <span className="text-gray-900 font-medium">تحليل جديد</span>
        </div>
        <div className="mt-2 flex items-center gap-3">
          <Link
            href="/ceph"
            className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
          >
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div>
            <h1 className="text-2xl font-extrabold text-gray-900">
              إنشاء تحليل سيفالومتري
            </h1>
            <p className="text-sm text-gray-500 mt-0.5">
              ارفع صورة الأشعة الجانبية واختر الحالة ونوع التحليل
            </p>
          </div>
        </div>
      </div>

      {serverError && (
        <div className="flex items-start gap-2 rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="w-5 h-5 mt-0.5 flex-shrink-0" />
          <span className="leading-relaxed">{serverError}</span>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)}>
        <div className="grid grid-cols-1 lg:grid-cols-5 gap-5 items-start">
          {/* ── Image pane (prominent) ─────────────────────────── */}
          <section className="lg:col-span-3 bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
            <div className="flex items-center gap-2">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-clinic-blue-50 text-clinic-blue">
                <ImageIcon className="w-4.5 h-4.5" />
              </span>
              <div>
                <h2 className="text-sm font-bold text-gray-900">
                  صورة الأشعة السيفالومترية
                </h2>
                <p className="text-xs text-gray-400">اختيارية — يمكن إضافتها لاحقًا</p>
              </div>
            </div>

            <CephXrayUploader
              value={xrayFileUrl}
              onChange={setXrayFileUrl}
              onUploadingChange={setUploading}
            />
          </section>

          {/* ── Data pane ──────────────────────────────────────── */}
          <aside className="lg:col-span-2 space-y-5">
            <section className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex items-center gap-2">
                <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-clinic-blue-50 text-clinic-blue">
                  <ClipboardList className="w-4.5 h-4.5" />
                </span>
                <h2 className="text-sm font-bold text-gray-900">بيانات التحليل</h2>
              </div>

              {/* Ortho case */}
              <div>
                <label className="flex items-center gap-1.5 text-sm font-medium text-gray-700 mb-1.5">
                  <User2 className="w-4 h-4 text-gray-400" />
                  الحالة التقويمية <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <Search className="absolute right-3 top-2.5 w-4 h-4 text-gray-400" />
                  <input
                    value={caseSearch}
                    onChange={(e) => {
                      setCaseSearch(e.target.value);
                      setShowDropdown(true);
                    }}
                    onFocus={() => caseSearch.length >= 2 && setShowDropdown(true)}
                    onBlur={() => setTimeout(() => setShowDropdown(false), 150)}
                    placeholder="ابحث باسم المريض أو رقم الحالة..."
                    className={cn(inputCls(errors.orthoCaseId?.message), "pe-9")}
                    autoComplete="off"
                  />
                  {showDropdown && caseResults.length > 0 && (
                    <div className="absolute z-10 w-full mt-1 bg-white rounded-lg border border-gray-200 shadow-lg max-h-48 overflow-y-auto">
                      {caseResults.map((c) => (
                        <button
                          key={c.id}
                          type="button"
                          onMouseDown={() => selectCase(c)}
                          className="w-full text-start px-3 py-2.5 text-sm hover:bg-gray-50 flex items-center justify-between"
                        >
                          <span className="font-medium">{c.patientName}</span>
                          <span className="text-xs text-gray-400 font-mono">
                            {c.caseNumber}
                          </span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <input type="hidden" {...register("orthoCaseId")} />
                {errors.orthoCaseId && (
                  <p className="mt-1 text-xs text-red-600">
                    {errors.orthoCaseId.message}
                  </p>
                )}
              </div>

              {/* Analysis type */}
              <div>
                <label className="flex items-center gap-1.5 text-sm font-medium text-gray-700 mb-1.5">
                  <Activity className="w-4 h-4 text-gray-400" />
                  نوع التحليل
                </label>
                <select {...register("analysisType")} className={inputCls()}>
                  <option value="full">شامل (جميع التحاليل)</option>
                  <option value="steiner">
                    ستاينر (يشمل الأنسجة الرخوة - خط S)
                  </option>
                  <option value="tweed">تويد</option>
                  <option value="mcnamara">ماكنامارا</option>
                  <option value="ricketts">ريكتس</option>
                  <option value="downs">داونز</option>
                  <option value="wits">وتس (Wits)</option>
                </select>
              </div>

              {/* Notes */}
              <div>
                <label className="flex items-center gap-1.5 text-sm font-medium text-gray-700 mb-1.5">
                  <FileText className="w-4 h-4 text-gray-400" />
                  ملاحظات
                </label>
                <textarea
                  {...register("notes")}
                  rows={3}
                  className={inputCls()}
                  placeholder="ملاحظات اختيارية حول الحالة أو التحليل..."
                />
              </div>
            </section>

            {/* Summary card */}
            <section className="bg-clinic-blue-50/60 rounded-xl border border-clinic-blue-100 p-4 space-y-2.5">
              <h3 className="text-xs font-bold text-clinic-navy">ملخص</h3>
              <dl className="space-y-1.5 text-xs">
                <div className="flex items-center justify-between gap-2">
                  <dt className="text-gray-500">الحالة</dt>
                  <dd
                    className={cn(
                      "font-medium truncate max-w-[60%] text-end",
                      selectedCaseLabel ? "text-gray-900" : "text-gray-400"
                    )}
                  >
                    {selectedCaseLabel || "لم تُحدد"}
                  </dd>
                </div>
                <div className="flex items-center justify-between gap-2">
                  <dt className="text-gray-500">نوع التحليل</dt>
                  <dd className="font-medium text-gray-900">
                    {ANALYSIS_LABELS[analysisType] ?? analysisType}
                  </dd>
                </div>
                <div className="flex items-center justify-between gap-2">
                  <dt className="text-gray-500">صورة الأشعة</dt>
                  <dd
                    className={cn(
                      "font-medium",
                      uploading
                        ? "text-clinic-blue"
                        : xrayFileUrl
                          ? "text-emerald-600"
                          : "text-gray-400"
                    )}
                  >
                    {uploading ? "جارٍ الرفع..." : xrayFileUrl ? "مُرفقة" : "بدون صورة"}
                  </dd>
                </div>
              </dl>
            </section>

            {/* Actions */}
            <div className="flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={() => void handleCancel()}
                className="px-5 py-2.5 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
              <button
                type="submit"
                disabled={saving || uploading}
                className="flex items-center gap-2 px-6 py-2.5 text-sm font-semibold rounded-lg bg-clinic-blue text-white shadow-sm hover:bg-clinic-blue-600 disabled:opacity-60 disabled:cursor-not-allowed transition"
              >
                <Save className="w-4 h-4" />
                {saving
                  ? "جارٍ الإنشاء..."
                  : uploading
                    ? "انتظر رفع الصورة..."
                    : "إنشاء التحليل"}
              </button>
            </div>
          </aside>
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
