"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  BadgeCheck,
  Calendar,
  Camera,
  CheckCircle2,
  ClipboardCheck,
  FileText,
  GitBranch,
  Images,
  Info,
  ListChecks,
  Plus,
  Save,
  Scissors,
  ShieldCheck,
  Stethoscope,
  Trash2,
  User,
  Wallet,
  X,
} from "lucide-react";
import { cn, formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import { financeV3ContractsUrl } from "@/lib/financeRoutes";
import {
  useAddProblem,
  useAddRetentionVisit,
  useApproveDiagnosis,
  useApproveSpecificTreatmentPlan,
  useClinicalExam,
  useCreateTreatmentPlan,
  useDeleteProblem,
  useDiagnosis,
  useExtractionDecision,
  useOrthoCase,
  useOrthoOverview,
  useOrthoPhotos,
  useOrthoStages,
  useOrthoVisits,
  useProblemList,
  useRecordsChecklist,
  useRetention,
  useSaveChecklist,
  useSaveClinicalExam,
  useSaveDiagnosis,
  useSaveExtractionDecision,
  useSaveRetention,
  useTreatmentPlans,
} from "@/hooks/useOrtho";
import { toast } from "@/stores/toastStore";
import type {
  ClinicalExam,
  ExtractionDecision,
  OrthoDiagnosis,
  OrthoPhoto,
  ProblemListItem,
  RecordsChecklist,
  RetentionRecord,
  RetentionVisit,
  TreatmentPlan,
  TreatmentStage,
} from "@/types/ortho";
import {
  EXTRACTION_FACTORS,
  ORTHO_STATUS_LABELS,
  RECORDS_CHECKLIST_ITEMS,
} from "@/types/ortho";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { ImagePreviewModal } from "@/components/shared/ImagePreviewModal";
import { OrthoVisitTimeline } from "@/components/ortho/OrthoVisitTimeline";
import { OrthoBeforeAfterCompare } from "@/components/ortho/OrthoBeforeAfterCompare";
import api from "@/lib/api";

/* ------------------------------------------------------------------ */
/*  Types                                                              */
/* ------------------------------------------------------------------ */

type Tab =
  | "overview"
  | "records"
  | "compare"
  | "exam"
  | "problems"
  | "diagnosis"
  | "plan"
  | "stages"
  | "visits"
  | "extraction"
  | "retention"
  | "finance";

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

const TABS: { key: Tab; label: string; icon: typeof Activity }[] = [
  { key: "overview", label: "الملخص", icon: Activity },
  { key: "records", label: "السجلات", icon: Camera },
  { key: "compare", label: "مقارنة قبل/بعد", icon: Images },
  { key: "exam", label: "الفحص", icon: Stethoscope },
  { key: "problems", label: "المشاكل", icon: ListChecks },
  { key: "diagnosis", label: "التشخيص", icon: ClipboardCheck },
  { key: "plan", label: "الخطة", icon: FileText },
  { key: "stages", label: "المراحل", icon: GitBranch },
  { key: "visits", label: "الزيارات", icon: Calendar },
  { key: "extraction", label: "الخلع", icon: Scissors },
  { key: "retention", label: "الاحتفاظ", icon: ShieldCheck },
  { key: "finance", label: "المالية", icon: Wallet },
];

const inputCls =
  "w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-clinic-blue";

const PLAN_LABELS: Record<string, string> = {
  A: "خطة A",
  B: "خطة B",
  C: "خطة C",
};

/* ------------------------------------------------------------------ */
/*  Shared components                                                  */
/* ------------------------------------------------------------------ */

function Field({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <label className={cn("block", className)}>
      <span className="mb-1 block text-xs font-medium text-gray-500">
        {label}
      </span>
      {children}
    </label>
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 py-10 text-center text-sm text-gray-400">
      {text}
    </div>
  );
}

function SaveButton({
  saving,
  children,
}: {
  saving?: boolean;
  children: React.ReactNode;
}) {
  return (
    <button
      type="submit"
      disabled={saving}
      className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white transition hover:opacity-90 disabled:opacity-60"
    >
      <Save className="h-4 w-4" />
      {saving ? "جاري الحفظ..." : children}
    </button>
  );
}

/* ------------------------------------------------------------------ */
/*  OverviewPanel                                                      */
/* ------------------------------------------------------------------ */

function OverviewPanel({
  caseId,
  patientId,
  setActiveTab,
}: {
  caseId: string;
  patientId: string;
  setActiveTab: (tab: Tab) => void;
}) {
  const { data: overview } = useOrthoOverview(caseId);
  const { data: diagnosis } = useDiagnosis(caseId);

  const readiness = [
    {
      label: "الفحص السريري",
      done: overview?.hasClinicalExam,
      tab: "exam" as Tab,
    },
    {
      label: "قائمة المشاكل",
      done: (overview?.problemsCount ?? 0) > 0,
      tab: "problems" as Tab,
    },
    {
      label: "التشخيص",
      done: overview?.hasDiagnosis,
      tab: "diagnosis" as Tab,
    },
    {
      label: "خطة العلاج",
      done: overview?.hasTreatmentPlan,
      tab: "plan" as Tab,
    },
    {
      label: "اعتماد الخطة",
      done: overview?.isTreatmentPlanApproved,
      tab: "plan" as Tab,
    },
    {
      label: "السجلات والصور",
      done:
        (overview?.photosCount ?? 0) > 0 ||
        (overview?.cephAnalysesCount ?? 0) > 0,
      tab: "records" as Tab,
    },
  ];

  const progress =
    overview && overview.totalStages > 0
      ? Math.round((overview.completedStages / overview.totalStages) * 100)
      : 0;

  const checklistPercent =
    overview && overview.checklistTotal > 0
      ? Math.round(
          (overview.checklistCompleted / overview.checklistTotal) * 100
        )
      : 0;

  return (
    <div className="grid gap-5 lg:grid-cols-[1.35fr_0.65fr]">
      <div className="space-y-5">
        {/* Stats grid */}
        <div className="grid gap-3 md:grid-cols-5">
          {[
            ["زيارات", overview?.visitsCount ?? 0],
            ["مشاكل", overview?.problemsCount ?? 0],
            ["صور", overview?.photosCount ?? 0],
            ["تحليلات Ceph", overview?.cephAnalysesCount ?? 0],
            ["إكمال السجلات", `${checklistPercent}%`],
          ].map(([label, value]) => (
            <div
              key={label}
              className="rounded-lg border border-gray-200 bg-white p-4"
            >
              <p className="text-xs text-gray-500">{label}</p>
              <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
            </div>
          ))}
        </div>

        {/* Readiness checklist */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-gray-900">
                جاهزية ملف التقويم
              </h2>
              <p className="text-sm text-gray-500">
                الخطوات الأساسية قبل بدء العلاج والمتابعة المالية.
              </p>
            </div>
            <span className="rounded-full bg-clinic-blue-50 px-3 py-1 text-sm font-semibold text-clinic-blue">
              {readiness.filter((i) => i.done).length}/{readiness.length}
            </span>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            {readiness.map((item) => (
              <button
                key={item.label}
                type="button"
                onClick={() => setActiveTab(item.tab)}
                className="flex items-center justify-between rounded-lg border border-gray-200 px-4 py-3 text-start transition hover:border-clinic-blue/50 hover:bg-clinic-blue-50/40"
              >
                <span className="text-sm font-medium text-gray-800">
                  {item.label}
                </span>
                {item.done ? (
                  <CheckCircle2 className="h-5 w-5 text-green-500" />
                ) : (
                  <AlertTriangle className="h-5 w-5 text-amber-500" />
                )}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="space-y-5">
        {/* Stage progress */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">تقدم المراحل</p>
          <div className="mt-4 h-2 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-clinic-blue transition-all"
              style={{ width: `${progress}%` }}
            />
          </div>
          <p className="mt-2 text-sm text-gray-500">
            {overview?.completedStages ?? 0} من{" "}
            {overview?.totalStages ?? 0} مراحل مكتملة
          </p>
        </div>

        {/* Finance card */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">
            المالية المرتبطة
          </p>
          {overview?.contractId ? (
            <div className="mt-3 space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-500">العقد</span>
                <span>
                  {formatYemeniRiyal(overview.contractTotal ?? 0)}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">المدفوع</span>
                <span className="text-green-600">
                  {formatYemeniRiyal(overview.contractPaid ?? 0)}
                </span>
              </div>
              <div className="flex justify-between font-semibold">
                <span>المتبقي</span>
                <span className="text-red-600">
                  {formatYemeniRiyal(overview.contractRemaining ?? 0)}
                </span>
              </div>
              <Link
                href={financeV3ContractsUrl(patientId)}
                className="mt-3 inline-flex text-sm font-medium text-clinic-blue hover:underline"
              >
                فتح العقد المالي
              </Link>
            </div>
          ) : (
            <p className="mt-3 text-sm text-gray-400">
              لا يوجد عقد مالي مرتبط بالحالة.
            </p>
          )}
        </div>

        {/* Diagnosis approval status */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">
            حالة اعتماد التشخيص
          </p>
          {diagnosis?.isApproved ? (
            <div className="mt-3 flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-green-500" />
              <div>
                <p className="text-sm font-medium text-green-700">
                  التشخيص معتمد
                </p>
                {diagnosis.approvedByName && (
                  <p className="text-xs text-gray-500">
                    بواسطة {diagnosis.approvedByName}
                    {diagnosis.approvedAt &&
                      ` · ${formatArabicDate(diagnosis.approvedAt)}`}
                  </p>
                )}
              </div>
            </div>
          ) : (
            <div className="mt-3 flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-amber-500" />
              <p className="text-sm text-amber-700">التشخيص غير معتمد بعد</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  RecordsPanel                                                       */
/* ------------------------------------------------------------------ */

function RecordsPanel({ caseId }: { caseId: string }) {
  const { data: photos = [] as OrthoPhoto[], refetch: refetchPhotos } =
    useOrthoPhotos(caseId);
  const { data: checklist, refetch: refetchChecklist } =
    useRecordsChecklist(caseId);
  const saveChecklist = useSaveChecklist(caseId);
  const [form, setForm] = useState({
    photoUrl: "",
    photoType: "Intraoral",
    caption: "",
  });
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const PHOTO_TYPE_LABELS: Record<string, string> = {
    Intraoral: "داخل الفم",
    Extraoral: "خارج الفم",
    Progress: "متابعة",
    Radiograph: "أشعة",
  };

  // Group checklist items
  const grouped = useMemo(() => {
    const map = new Map<string, typeof RECORDS_CHECKLIST_ITEMS>();
    for (const item of RECORDS_CHECKLIST_ITEMS) {
      const list = map.get(item.group) ?? [];
      list.push(item);
      map.set(item.group, list);
    }
    return map;
  }, []);

  const completedCount = useMemo(() => {
    if (!checklist) return 0;
    return RECORDS_CHECKLIST_ITEMS.filter(
      (item) => checklist[item.key]
    ).length;
  }, [checklist]);

  const totalCount = RECORDS_CHECKLIST_ITEMS.length;
  const percent =
    totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

  const toggleItem = (key: keyof RecordsChecklist) => {
    if (!checklist) return;
    const newValue = !checklist[key];
    saveChecklist.mutate(
      { [key]: newValue },
      {
        onSuccess: () => {
          refetchChecklist();
        },
      }
    );
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    // Validate
    const validExts = [".jpg", ".jpeg", ".png", ".webp"];
    const ext = file.name.substring(file.name.lastIndexOf(".")).toLowerCase();
    if (!validExts.includes(ext)) {
      toast.error("صيغة الملف غير مدعومة. استخدم JPG أو PNG أو WebP");
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      toast.error("حجم الملف يتجاوز 10 ميجابايت");
      return;
    }
    setPhotoFile(file);
    // Generate preview
    const reader = new FileReader();
    reader.onload = (ev) => setPhotoPreview(ev.target?.result as string);
    reader.readAsDataURL(file);
  };

  const addPhotoFromUrl = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.photoUrl.trim()) return;
    setSaving(true);
    try {
      await (
        await import("@/services/orthoService")
      ).orthoService.addPhoto(caseId, form);
      setForm({ photoUrl: "", photoType: "Intraoral", caption: "" });
      await refetchPhotos();
      toast.success("تمت إضافة السجل");
    } catch {
      toast.error("فشل إضافة السجل");
    } finally {
      setSaving(false);
    }
  };

  const uploadAndAddPhoto = async (event: FormEvent) => {
    event.preventDefault();
    if (!photoFile) return;
    setUploading(true);
    try {
      // Step 1: Upload file via authenticated api client (sends to Railway backend with Bearer token)
      const formData = new FormData();
      formData.append("file", photoFile);
      const uploadRes = await api.post<{ url: string }>("/api/uploads", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      const url = uploadRes.data.url;
      // Step 2: Add photo to ortho case
      await (
        await import("@/services/orthoService")
      ).orthoService.addPhoto(caseId, {
        photoUrl: url,
        photoType: form.photoType,
        caption: form.caption,
      });
      setPhotoFile(null);
      setPhotoPreview(null);
      setForm((f) => ({ ...f, photoType: "Intraoral", caption: "" }));
      if (fileInputRef.current) fileInputRef.current.value = "";
      await refetchPhotos();
      toast.success("تم رفع الصورة وإضافتها بنجاح");
    } catch {
      toast.error("فشل رفع الصورة");
    } finally {
      setUploading(false);
    }
  };

  const deletePhoto = async (photoId: string) => {
    try {
      await (
        await import("@/services/orthoService")
      ).orthoService.deletePhoto(caseId, photoId);
      setDeleteConfirm(null);
      await refetchPhotos();
      toast.success("تم حذف الصورة");
    } catch {
      toast.error("فشل حذف الصورة");
    }
  };

  const resolveImageUrl = (url: string) => {
    if (url.startsWith("http") || url.startsWith("data:")) return url;
    const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
    return apiBase ? `${apiBase}${url.startsWith("/") ? "" : "/"}${url}` : url;
  };

  return (
    <div className="space-y-5">
      {/* Checklist */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="font-semibold text-gray-900">
              قائمة السجلات المطلوبة
            </h2>
            <p className="text-sm text-gray-500">
              {completedCount} من {totalCount} عنصر مكتمل ({percent}%)
            </p>
          </div>
          <div className="flex items-center gap-3">
            <div className="h-2 w-24 overflow-hidden rounded-full bg-gray-100">
              <div
                className="h-full rounded-full bg-clinic-blue transition-all"
                style={{ width: `${percent}%` }}
              />
            </div>
            <span className="text-sm font-semibold text-clinic-blue">
              {percent}%
            </span>
          </div>
        </div>

        <div className="space-y-5">
          {Array.from(grouped.entries()).map(([group, items]) => (
            <div key={group}>
              <h3 className="mb-2 text-xs font-semibold uppercase text-gray-400">
                {group}
              </h3>
              <div className="grid gap-2 md:grid-cols-2 lg:grid-cols-3">
                {items.map((item) => {
                  const checked = checklist?.[item.key] ?? false;
                  return (
                    <button
                      key={item.key}
                      type="button"
                      onClick={() => toggleItem(item.key)}
                      className={cn(
                        "flex items-center gap-3 rounded-lg border px-3 py-2.5 text-start transition",
                        checked
                          ? "border-green-200 bg-green-50"
                          : "border-gray-200 bg-white hover:border-clinic-blue/40"
                      )}
                    >
                      <div
                        className={cn(
                          "flex h-5 w-5 flex-shrink-0 items-center justify-center rounded border transition",
                          checked
                            ? "border-green-500 bg-green-500"
                            : "border-gray-300 bg-white"
                        )}
                      >
                        {checked && (
                          <CheckCircle2 className="h-3.5 w-3.5 text-white" />
                        )}
                      </div>
                      <span
                        className={cn(
                          "text-sm",
                          checked
                            ? "font-medium text-green-800"
                            : "text-gray-700"
                        )}
                      >
                        {item.label}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Photo upload + gallery */}
      <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
        <div className="space-y-4">
          {/* File Upload Form */}
          <form
            onSubmit={uploadAndAddPhoto}
            className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
          >
            <h2 className="font-semibold text-gray-900">رفع صورة</h2>
            <div>
              <input
                ref={fileInputRef}
                type="file"
                accept=".jpg,.jpeg,.png,.webp"
                onChange={handleFileSelect}
                className="block w-full text-sm text-gray-500 file:ml-2 file:rounded-lg file:border-0 file:bg-[#3d7ab5] file:px-3 file:py-2 file:text-sm file:font-medium file:text-white hover:file:bg-[#1a3a5c] file:cursor-pointer"
              />
            </div>
            {photoPreview && (
              <div className="relative aspect-square w-full max-w-[200px] overflow-hidden rounded-lg border border-gray-200">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={photoPreview}
                  alt="معاينة"
                  className="h-full w-full object-cover"
                />
              </div>
            )}
            <Field label="النوع">
              <select
                className={inputCls}
                value={form.photoType}
                onChange={(e) =>
                  setForm((f) => ({ ...f, photoType: e.target.value }))
                }
              >
                <option value="Intraoral">داخل الفم</option>
                <option value="Extraoral">خارج الفم</option>
                <option value="Progress">متابعة</option>
                <option value="Radiograph">أشعة</option>
              </select>
            </Field>
            <Field label="ملاحظة">
              <input
                className={inputCls}
                value={form.caption}
                onChange={(e) =>
                  setForm((f) => ({ ...f, caption: e.target.value }))
                }
                placeholder="وصف الصورة"
              />
            </Field>
            <SaveButton saving={uploading}>رفع وإضافة</SaveButton>
          </form>

          {/* URL paste fallback */}
          <details className="rounded-lg border border-gray-200 bg-white">
            <summary className="cursor-pointer px-5 py-3 text-sm font-medium text-gray-600 hover:text-gray-900">
              إضافة عبر رابط (متقدم)
            </summary>
            <form
              onSubmit={addPhotoFromUrl}
              className="space-y-3 border-t border-gray-100 p-5"
            >
              <Field label="رابط الصورة">
                <input
                  className={inputCls}
                  value={form.photoUrl}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, photoUrl: e.target.value }))
                  }
                  placeholder="https://..."
                  dir="ltr"
                />
              </Field>
              <SaveButton saving={saving}>إضافة</SaveButton>
            </form>
          </details>
        </div>

        {/* Photo Gallery */}
        <div>
          {photos.length === 0 ? (
            <EmptyState text="لا توجد صور أو سجلات مرتبطة بحالة التقويم." />
          ) : (
            <div className="grid gap-3 grid-cols-2 md:grid-cols-3">
              {photos.map((p: OrthoPhoto, idx: number) => (
                <div
                  key={p.id}
                  className="group relative aspect-square overflow-hidden rounded-lg border border-gray-200 bg-gray-50 cursor-pointer"
                  onClick={() => {
                    setPreviewIndex(idx);
                    setPreviewOpen(true);
                  }}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={resolveImageUrl(p.photoUrl)}
                    alt={p.caption || PHOTO_TYPE_LABELS[p.photoType] || p.photoType}
                    className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
                    onError={(e) => {
                      (e.target as HTMLImageElement).style.display = "none";
                    }}
                  />
                  {/* Overlay on hover */}
                  <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity">
                    <div className="absolute bottom-0 right-0 left-0 p-2">
                      <p className="text-xs font-medium text-white truncate">
                        {p.caption || PHOTO_TYPE_LABELS[p.photoType] || p.photoType}
                      </p>
                      {p.takenAt && (
                        <p className="text-[10px] text-white/70">
                          {formatArabicDate(p.takenAt)}
                        </p>
                      )}
                    </div>
                  </div>
                  {/* Type badge */}
                  <span className="absolute top-2 right-2 rounded bg-black/50 px-1.5 py-0.5 text-[10px] font-medium text-white">
                    {PHOTO_TYPE_LABELS[p.photoType] || p.photoType}
                  </span>
                  {/* Delete button */}
                  {deleteConfirm === p.id ? (
                    <div
                      className="absolute top-2 left-2 flex items-center gap-1"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <button
                        type="button"
                        onClick={() => deletePhoto(p.id)}
                        className="rounded bg-red-600 px-2 py-1 text-[10px] font-bold text-white"
                      >
                        تأكيد
                      </button>
                      <button
                        type="button"
                        onClick={() => setDeleteConfirm(null)}
                        className="rounded bg-gray-600 px-2 py-1 text-[10px] font-bold text-white"
                      >
                        إلغاء
                      </button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        setDeleteConfirm(p.id);
                      }}
                      className="absolute top-2 left-2 rounded bg-black/40 p-1 text-white opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-600"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Image Preview Modal */}
      <ImagePreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        url={photos[previewIndex]?.photoUrl ?? ""}
        fileName={photos[previewIndex]?.caption || photos[previewIndex]?.photoType}
        items={photos.map((p) => ({
          url: resolveImageUrl(p.photoUrl),
          fileName: p.caption || p.photoType,
        }))}
        currentIndex={previewIndex}
        onNavigate={setPreviewIndex}
      />
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  ClinicalExamPanel                                                  */
/* ------------------------------------------------------------------ */

function ClinicalExamPanel({ caseId }: { caseId: string }) {
  const { data } = useClinicalExam(caseId);
  const save = useSaveClinicalExam(caseId);
  const [form, setForm] = useState<ClinicalExam>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof ClinicalExam>(
    key: K,
    value: ClinicalExam[K]
  ) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate(form);
      }}
      className="space-y-5"
    >
      {/* Extraoral */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          فحص خارج الفم
        </h3>
        <div className="grid gap-4 md:grid-cols-3">
          <Field label="تاريخ الفحص">
            <input
              type="date"
              className={inputCls}
              value={form.examDate ?? ""}
              onChange={(e) => set("examDate", e.target.value)}
            />
          </Field>
          <Field label="البروفايل">
            <select
              className={inputCls}
              value={form.profile ?? ""}
              onChange={(e) => set("profile", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Class I</option>
              <option>Convex</option>
              <option>Concave</option>
            </select>
          </Field>
          <Field label="التماثل الوجهي">
            <select
              className={inputCls}
              value={form.facialSymmetry ?? ""}
              onChange={(e) => set("facialSymmetry", e.target.value)}
            >
              <option value="">اختر</option>
              <option>متماثل</option>
              <option>غير متماثل</option>
            </select>
          </Field>
          <Field label="انطباق الشفاه">
            <select
              className={inputCls}
              value={form.lipsCompetence ? "true" : form.lipsCompetence === false ? "false" : ""}
              onChange={(e) =>
                set(
                  "lipsCompetence",
                  e.target.value === "true"
                    ? true
                    : e.target.value === "false"
                      ? false
                    : undefined
                )
              }
            >
              <option value="">اختر</option>
              <option value="true">مندانغم</option>
              <option value="false">غير منتظم</option>
            </select>
          </Field>
          <Field label="خط الابتسامة">
            <input
              className={inputCls}
              value={form.smileLine ?? ""}
              onChange={(e) => set("smileLine", e.target.value)}
              placeholder="منخفض / متوسط / عالي"
            />
          </Field>
          <Field label="النسب العمودية">
            <input
              className={inputCls}
              value={form.verticalProportion ?? ""}
              onChange={(e) => set("verticalProportion", e.target.value)}
              placeholder="طبيعي / طويل / قصير"
            />
          </Field>
          <Field label="الخط الناصف" className="md:col-span-3">
            <input
              className={inputCls}
              value={form.midlineUpper ?? ""}
              onChange={(e) => set("midlineUpper", e.target.value)}
              placeholder="متوافق / منحرف يمين / منحرف يسار"
            />
          </Field>
        </div>
      </div>

      {/* Intraoral */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          فحص داخل الفم
        </h3>
        <div className="grid gap-4 md:grid-cols-3">
          <Field label="علاقة الأرحاء">
            <select
              className={inputCls}
              value={form.molarRelation ?? ""}
              onChange={(e) => set("molarRelation", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Class I</option>
              <option>Class II Div 1</option>
              <option>Class II Div 2</option>
              <option>Class III</option>
            </select>
          </Field>
          <Field label="علاقة الأنياب">
            <select
              className={inputCls}
              value={form.canineRelation ?? ""}
              onChange={(e) => set("canineRelation", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Class I</option>
              <option>Class II</option>
              <option>Class III</option>
            </select>
          </Field>
          <Field label="Overjet (mm)">
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={form.overjet ?? ""}
              onChange={(e) =>
                set(
                  "overjet",
                  e.target.value ? Number(e.target.value) : undefined
                )
              }
            />
          </Field>
          <Field label="Overbite (mm)">
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={form.overbite ?? ""}
              onChange={(e) =>
                set(
                  "overbite",
                  e.target.value ? Number(e.target.value) : undefined
                )
              }
            />
          </Field>
          <Field label="Crossbite">
            <select
              className={inputCls}
              value={
                form.crossbite ? "true" : form.crossbite === false ? "false" : ""
              }
              onChange={(e) =>
                set(
                  "crossbite",
                  e.target.value === "true"
                    ? true
                    : e.target.value === "false"
                      ? false
                    : undefined
                )
              }
            >
              <option value="">اختر</option>
              <option value="true">نعم</option>
              <option value="false">لا</option>
            </select>
          </Field>
          <Field label="Open Bite">
            <select
              className={inputCls}
              value={
                form.openBite ? "true" : form.openBite === false ? "false" : ""
              }
              onChange={(e) =>
                set(
                  "openBite",
                  e.target.value === "true"
                    ? true
                    : e.target.value === "false"
                      ? false
                    : undefined
                )
              }
            >
              <option value="">اختر</option>
              <option value="true">نعم</option>
              <option value="false">لا</option>
            </select>
          </Field>
          <Field label="تكدس علوي">
            <input
              className={inputCls}
              value={form.upperCrowding ?? ""}
              onChange={(e) => set("upperCrowding", e.target.value)}
              placeholder="خفيف / متوسط / شديد"
            />
          </Field>
          <Field label="تكدس سفلي">
            <input
              className={inputCls}
              value={form.lowerCrowding ?? ""}
              onChange={(e) => set("lowerCrowding", e.target.value)}
              placeholder="خفيف / متوسط / شديد"
            />
          </Field>
          <Field label="مسافات (mm)">
            <input
              type="number"
              step="0.1"
              className={inputCls}
              value={form.upperSpacing ?? ""}
              onChange={(e) =>
                set(
                  "upperSpacing",
                  e.target.value ? Number(e.target.value) : undefined
                )
              }
            />
          </Field>
          <Field label="انحراف الخط الناصف العلوي">
            <input
              className={inputCls}
              value={form.midlineUpper ?? ""}
              onChange={(e) => set("midlineUpper", e.target.value)}
            />
          </Field>
          <Field label="انحراف الخط الناصف السفلي">
            <input
              className={inputCls}
              value={form.midlineLower ?? ""}
              onChange={(e) => set("midlineLower", e.target.value)}
            />
          </Field>
          <Field label="تناقض Co-Cr">
            <select
              className={inputCls}
              value={
                form.coCrDiscrepancy
                  ? "true"
                  : form.coCrDiscrepancy === false
                    ? "false"
                    : ""
              }
              onChange={(e) =>
                set(
                  "coCrDiscrepancy",
                  e.target.value === "true"
                    ? true
                    : e.target.value === "false"
                      ? false
                    : undefined
                )
              }
            >
              <option value="">اختر</option>
              <option value="true">نعم</option>
              <option value="false">لا</option>
            </select>
          </Field>
        </div>
      </div>

      {/* Functional */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          الفحص الوظيفي
        </h3>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="ملاحظات TMJ">
            <textarea
              rows={2}
              className={inputCls}
              value={form.tmjFindings ?? ""}
              onChange={(e) => set("tmjFindings", e.target.value)}
            />
          </Field>
          <Field label="العادات">
            <textarea
              rows={2}
              className={inputCls}
              value={form.habits ?? ""}
              onChange={(e) => set("habits", e.target.value)}
              placeholder="تنفس فمي، مص إصبع..."
            />
          </Field>
        </div>
      </div>

      {/* Notes */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <Field label="ملاحظات عامة">
          <textarea
            rows={3}
            className={inputCls}
            value={form.notes ?? ""}
            onChange={(e) => set("notes", e.target.value)}
          />
        </Field>
      </div>

      <SaveButton saving={save.isPending}>حفظ الفحص</SaveButton>
    </form>
  );
}

/* ------------------------------------------------------------------ */
/*  ProblemsPanel                                                      */
/* ------------------------------------------------------------------ */

function ProblemsPanel({ caseId }: { caseId: string }) {
  const { data: problems = [] as ProblemListItem[] } = useProblemList(caseId);
  const add = useAddProblem(caseId);
  const remove = useDeleteProblem(caseId);
  const [form, setForm] = useState({
    category: "skeletal",
    description: "",
    severity: "moderate",
  });
  const categories = {
    skeletal: "هيكلية",
    dental: "سنية",
    soft_tissue: "أنسجة رخوة",
    functional: "وظيفية",
    space: "مسافات",
    esthetic: "جمالية",
  };

  return (
    <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!form.description.trim()) return;
          add.mutate(form, {
            onSuccess: () =>
              setForm({
                category: "skeletal",
                description: "",
                severity: "moderate",
              }),
          });
        }}
        className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
      >
        <h2 className="font-semibold text-gray-900">
          إضافة مشكلة تشخيصية
        </h2>
        <Field label="التصنيف">
          <select
            className={inputCls}
            value={form.category}
            onChange={(e) =>
              setForm((f) => ({ ...f, category: e.target.value }))
            }
          >
            {Object.entries(categories).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
        </Field>
        <Field label="الوصف">
          <textarea
            rows={3}
            className={inputCls}
            value={form.description}
            onChange={(e) =>
              setForm((f) => ({ ...f, description: e.target.value }))
            }
          />
        </Field>
        <Field label="الشدة">
          <select
            className={inputCls}
            value={form.severity}
            onChange={(e) =>
              setForm((f) => ({ ...f, severity: e.target.value }))
            }
          >
            <option value="mild">خفيفة</option>
            <option value="moderate">متوسطة</option>
            <option value="severe">شديدة</option>
          </select>
        </Field>
        <SaveButton saving={add.isPending}>إضافة المشكلة</SaveButton>
      </form>
      <div className="space-y-3">
        {problems.length === 0 ? (
          <EmptyState text="لم يتم تسجيل مشاكل تشخيصية بعد." />
        ) : (
          problems.map((p: ProblemListItem) => (
            <div
              key={p.id}
              className="flex items-start justify-between gap-3 rounded-lg border border-gray-200 bg-white p-4"
            >
              <div>
                <p className="font-medium text-gray-900">{p.description}</p>
                <p className="mt-1 text-xs text-gray-500">
                  {categories[p.category as keyof typeof categories] ??
                    p.category}{" "}
                  · {p.severity ?? "غير محدد"}
                </p>
              </div>
              <button
                type="button"
                onClick={() => remove.mutate(p.id)}
                className="rounded-lg p-2 text-gray-400 hover:bg-red-50 hover:text-red-600"
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  DiagnosisPanel (Enhanced)                                          */
/* ------------------------------------------------------------------ */

function DiagnosisPanel({ caseId }: { caseId: string }) {
  const { data } = useDiagnosis(caseId);
  const save = useSaveDiagnosis(caseId);
  const approve = useApproveDiagnosis(caseId);
  const [form, setForm] = useState<OrthoDiagnosis>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof OrthoDiagnosis>(
    key: K,
    value: OrthoDiagnosis[K]
  ) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate(form);
      }}
      className="space-y-5"
    >
      {/* Classification */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          التصنيف
        </h3>
        <div className="grid gap-4 md:grid-cols-3">
          <Field label="التصنيف الهيكلي">
            <select
              className={inputCls}
              value={form.skeletalClassification ?? ""}
              onChange={(e) => set("skeletalClassification", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Class I</option>
              <option>Class II</option>
              <option>Class III</option>
            </select>
          </Field>
          <Field label="التصنيف السني">
            <input
              className={inputCls}
              value={form.dentalClassification ?? ""}
              onChange={(e) => set("dentalClassification", e.target.value)}
              placeholder="Class I / II / III"
            />
          </Field>
          <Field label="النمط الوجهي">
            <select
              className={inputCls}
              value={form.facialPattern ?? ""}
              onChange={(e) => set("facialPattern", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Hypodivergent</option>
              <option>Normodivergent</option>
              <option>Hyperdivergent</option>
            </select>
          </Field>
        </div>
      </div>

      {/* Extended diagnosis fields */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          تشخيص تفصيلي
        </h3>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="تشخيص الأنسجة الرخوة">
            <textarea
              rows={3}
              className={inputCls}
              value={form.softTissueDiagnosis ?? ""}
              onChange={(e) => set("softTissueDiagnosis", e.target.value)}
            />
          </Field>
          <Field label="التشخيص الوظيفي">
            <textarea
              rows={3}
              className={inputCls}
              value={form.functionalDiagnosis ?? ""}
              onChange={(e) => set("functionalDiagnosis", e.target.value)}
            />
          </Field>
          <Field label="الأسباب (Etiology)" className="md:col-span-2">
            <textarea
              rows={2}
              className={inputCls}
              value={form.etiology ?? ""}
              onChange={(e) => set("etiology", e.target.value)}
            />
          </Field>
        </div>
      </div>

      {/* Cephalometric measurements */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          القياسات السيفالومترية
        </h3>
        <div className="grid gap-4 md:grid-cols-3">
          {(["anb", "wits", "fma", "sna", "snb", "impa"] as const).map(
            (key) => (
              <Field key={key} label={key.toUpperCase()}>
                <input
                  type="number"
                  step="0.1"
                  className={inputCls}
                  value={form[key] ?? ""}
                  onChange={(e) =>
                    set(
                      key,
                      e.target.value ? Number(e.target.value) : undefined
                    )
                  }
                />
              </Field>
            )
          )}
        </div>
      </div>

      {/* Summary */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <Field label="ملخص التشخيص">
          <textarea
            rows={4}
            className={inputCls}
            value={form.summary ?? ""}
            onChange={(e) => set("summary", e.target.value)}
          />
        </Field>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between">
        <SaveButton saving={save.isPending}>حفظ التشخيص</SaveButton>

        {/* Approval section */}
        {form.isApproved ? (
          <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-2">
            <CheckCircle2 className="h-5 w-5 text-green-500" />
            <div>
              <p className="text-sm font-medium text-green-700">
                التشخيص معتمد
              </p>
              {form.approvedByName && (
                <p className="text-xs text-green-600">
                  بواسطة {form.approvedByName}
                  {form.approvedAt &&
                    ` · ${formatArabicDate(form.approvedAt)}`}
                </p>
              )}
            </div>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => approve.mutate()}
            disabled={approve.isPending}
            className="inline-flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-green-100 disabled:opacity-50"
          >
            <BadgeCheck className="h-4 w-4" />
            اعتماد التشخيص
          </button>
        )}
      </div>
    </form>
  );
}

/* ------------------------------------------------------------------ */
/*  TreatmentPlanPanel (Multi-Plan)                                    */
/* ------------------------------------------------------------------ */

function TreatmentPlanPanel({ caseId }: { caseId: string }) {
  const { data: plans = [] as TreatmentPlan[] } = useTreatmentPlans(caseId);
  const createPlan = useCreateTreatmentPlan(caseId);
  const approvePlan = useApproveSpecificTreatmentPlan(caseId);
  const [showCreate, setShowCreate] = useState(false);
  const [newPlan, setNewPlan] = useState<Partial<TreatmentPlan>>({
    planLabel: "B",
    applianceType: "",
    bracketSystem: "",
    initialWire: "",
    extractionPlan: "",
    anchoragePlan: "",
    useTads: false,
    useElastics: false,
    expectedDurationMonths: undefined,
    retentionPlan: "",
    treatmentGoals: "",
    risksLimitations: "",
  });

  // Determine which labels are already used
  const usedLabels = useMemo(
    () => new Set(plans.map((p: TreatmentPlan) => p.planLabel)),
    [plans]
  );
  const availableLabels = ["A", "B", "C"].filter(
    (l) => !usedLabels.has(l)
  );

  const handleCreate = () => {
    if (!newPlan.planLabel) return;
    createPlan.mutate(newPlan as Partial<TreatmentPlan>, {
      onSuccess: () => {
        setShowCreate(false);
        setNewPlan({
          planLabel: availableLabels[0] ?? "C",
          applianceType: "",
          bracketSystem: "",
          initialWire: "",
          extractionPlan: "",
          anchoragePlan: "",
          useTads: false,
          useElastics: false,
          expectedDurationMonths: undefined,
          retentionPlan: "",
          treatmentGoals: "",
          risksLimitations: "",
        });
      },
    });
  };

  return (
    <div className="space-y-5">
      {/* Header with create button */}
      <div className="flex items-center justify-between">
        <h2 className="font-semibold text-gray-900">خطط العلاج</h2>
        {availableLabels.length > 0 && (
          <button
            type="button"
            onClick={() => setShowCreate(!showCreate)}
            className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-3 py-2 text-sm font-medium text-white transition hover:opacity-90"
          >
            <Plus className="h-4 w-4" />
            إنشاء خطة جديدة
          </button>
        )}
      </div>

      {/* Create form */}
      {showCreate && (
        <div className="rounded-lg border border-clinic-blue-100 bg-clinic-blue-50 p-5 space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="font-semibold text-clinic-navy">
              إنشاء خطة علاج جديدة
            </h3>
            <button
              type="button"
              onClick={() => setShowCreate(false)}
              className="rounded-lg p-1 text-gray-400 hover:text-gray-600"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <Field label="تسمية الخطة">
              <select
                className={inputCls}
                value={newPlan.planLabel ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, planLabel: e.target.value }))
                }
              >
                {availableLabels.map((l) => (
                  <option key={l} value={l}>
                    {PLAN_LABELS[l]}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="نوع الجهاز">
              <input
                className={inputCls}
                value={newPlan.applianceType ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, applianceType: e.target.value }))
                }
              />
            </Field>
            <Field label="نظام البراكت">
              <input
                className={inputCls}
                value={newPlan.bracketSystem ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, bracketSystem: e.target.value }))
                }
              />
            </Field>
            <Field label="السلك الأولي">
              <input
                className={inputCls}
                value={newPlan.initialWire ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, initialWire: e.target.value }))
                }
              />
            </Field>
            <Field label="المدة المتوقعة (أشهر)">
              <input
                type="number"
                className={inputCls}
                value={newPlan.expectedDurationMonths ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({
                    ...f,
                    expectedDurationMonths: e.target.value
                      ? Number(e.target.value)
                      : undefined,
                  }))
                }
              />
            </Field>
            <Field label="خطة الخلع">
              <input
                className={inputCls}
                value={newPlan.extractionPlan ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, extractionPlan: e.target.value }))
                }
              />
            </Field>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Anchorage">
              <textarea
                rows={2}
                className={inputCls}
                value={newPlan.anchoragePlan ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, anchoragePlan: e.target.value }))
                }
              />
            </Field>
            <Field label="خطة الاحتفاظ">
              <textarea
                rows={2}
                className={inputCls}
                value={newPlan.retentionPlan ?? ""}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, retentionPlan: e.target.value }))
                }
              />
            </Field>
          </div>
          <div className="flex items-center gap-4">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={newPlan.useTads ?? false}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, useTads: e.target.checked }))
                }
                className="rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
              />
              استخدام TADs
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={newPlan.useElastics ?? false}
                onChange={(e) =>
                  setNewPlan((f) => ({ ...f, useElastics: e.target.checked }))
                }
                className="rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
              />
              استخدام Elastics
            </label>
          </div>
          <Field label="أهداف العلاج">
            <textarea
              rows={3}
              className={inputCls}
              value={newPlan.treatmentGoals ?? ""}
              onChange={(e) =>
                setNewPlan((f) => ({ ...f, treatmentGoals: e.target.value }))
              }
            />
          </Field>
          <Field label="المخاطر والحدود">
            <textarea
              rows={3}
              className={inputCls}
              value={newPlan.risksLimitations ?? ""}
              onChange={(e) =>
                setNewPlan((f) => ({ ...f, risksLimitations: e.target.value }))
              }
            />
          </Field>
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setShowCreate(false)}
              className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              إلغاء
            </button>
            <button
              type="button"
              onClick={handleCreate}
              disabled={createPlan.isPending}
              className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white transition hover:opacity-90 disabled:opacity-60"
            >
              <Save className="h-4 w-4" />
              {createPlan.isPending ? "جاري الحفظ..." : "إنشاء الخطة"}
            </button>
          </div>
        </div>
      )}

      {/* Plans list */}
      {plans.length === 0 ? (
        <EmptyState text="لا توجد خطط علاج مسجلة بعد." />
      ) : (
        <div className="space-y-4">
          {plans.map((plan: TreatmentPlan) => (
            <div
              key={plan.id}
              className={cn(
                "rounded-lg border bg-white p-5 transition",
                plan.isApproved
                  ? "border-green-300 bg-green-50/30"
                  : "border-gray-200"
              )}
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex items-center gap-3">
                  <span
                    className={cn(
                      "inline-flex h-9 w-9 items-center justify-center rounded-lg text-sm font-bold text-white",
                      plan.isApproved ? "bg-green-500" : "bg-clinic-navy"
                    )}
                  >
                    {plan.planLabel ?? "A"}
                  </span>
                  <div>
                    <p className="font-semibold text-gray-900">
                      {PLAN_LABELS[plan.planLabel ?? "A"] ??
                        `خطة ${plan.planLabel}`}
                    </p>
                    <div className="mt-1 flex flex-wrap gap-2 text-xs text-gray-500">
                      {plan.applianceType && <span>{plan.applianceType}</span>}
                      {plan.bracketSystem && (
                        <span>· {plan.bracketSystem}</span>
                      )}
                      {plan.expectedDurationMonths && (
                        <span>· {plan.expectedDurationMonths} شهر</span>
                      )}
                      {plan.extractionPlan && (
                        <span>· خلع: {plan.extractionPlan}</span>
                      )}
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {plan.isApproved ? (
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-700">
                      <CheckCircle2 className="h-3.5 w-3.5" />
                      معتمدة
                      {plan.approvedByName && ` بواسطة ${plan.approvedByName}`}
                    </span>
                  ) : (
                    <button
                      type="button"
                      onClick={() =>
                        plan.id && approvePlan.mutate(plan.id)
                      }
                      disabled={!plan.id || approvePlan.isPending}
                      className="inline-flex items-center gap-1.5 rounded-lg border border-green-200 bg-green-50 px-3 py-1.5 text-xs font-medium text-green-700 transition hover:bg-green-100 disabled:opacity-50"
                    >
                      <BadgeCheck className="h-3.5 w-3.5" />
                      اعتماد
                    </button>
                  )}
                </div>
              </div>

              {/* Plan details */}
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                {plan.treatmentGoals && (
                  <div>
                    <p className="text-xs font-medium text-gray-400">
                      أهداف العلاج
                    </p>
                    <p className="mt-1 text-sm text-gray-700 whitespace-pre-wrap">
                      {plan.treatmentGoals}
                    </p>
                  </div>
                )}
                {plan.risksLimitations && (
                  <div>
                    <p className="text-xs font-medium text-gray-400">
                      المخاطر والحدود
                    </p>
                    <p className="mt-1 text-sm text-gray-700 whitespace-pre-wrap">
                      {plan.risksLimitations}
                    </p>
                  </div>
                )}
                {plan.anchoragePlan && (
                  <div>
                    <p className="text-xs font-medium text-gray-400">
                      Anchorage
                    </p>
                    <p className="mt-1 text-sm text-gray-700">
                      {plan.anchoragePlan}
                    </p>
                  </div>
                )}
                <div className="flex gap-3 text-xs text-gray-500">
                  {plan.useTads && <span>TADs</span>}
                  {plan.useElastics && <span>Elastics</span>}
                  {plan.retentionPlan && (
                    <span>احتفاظ: {plan.retentionPlan}</span>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  ExtractionPanel (Enhanced Worksheet)                               */
/* ------------------------------------------------------------------ */

function ExtractionPanel({ caseId }: { caseId: string }) {
  const { data } = useExtractionDecision(caseId);
  const save = useSaveExtractionDecision(caseId);
  const [form, setForm] = useState<ExtractionDecision>({});
  useEffect(() => setForm(data ?? {}), [data]);

  const proExtraction = form.proExtraction ?? {};
  const factorCount = EXTRACTION_FACTORS.filter(
    (f) => proExtraction[f.key]
  ).length;
  const totalFactors = EXTRACTION_FACTORS.length;
  const factorPercent =
    totalFactors > 0 ? Math.round((factorCount / totalFactors) * 100) : 0;

  // Compute recommendation badge based on factor count
  let recommendation: {
    label: string;
    color: string;
    bgColor: string;
  };
  if (factorCount >= 6) {
    recommendation = {
      label: "الخلع مفضل",
      color: "text-red-700",
      bgColor: "bg-red-50 border-red-200",
    };
  } else if (factorCount >= 4) {
    recommendation = {
      label: "حالة حدية",
      color: "text-amber-700",
      bgColor: "bg-amber-50 border-amber-200",
    };
  } else {
    recommendation = {
      label: "بدون خلع مفضل",
      color: "text-green-700",
      bgColor: "bg-green-50 border-green-200",
    };
  }

  const toggleFactor = (key: string) => {
    setForm((f) => ({
      ...f,
      proExtraction: {
        ...(f.proExtraction ?? {}),
        [key]: !(f.proExtraction ?? {})[key],
      },
    }));
  };

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate(form);
      }}
      className="space-y-5"
    >
      {/* Decision support info banner */}
      <div className="flex items-start gap-3 rounded-lg border border-blue-200 bg-blue-50 p-4">
        <Info className="mt-0.5 h-5 w-5 flex-shrink-0 text-clinic-blue" />
        <p className="text-sm text-blue-800">
          هذا الدعم القراري مبني على معايير سريرية ثابتة. القرار النهائي يعود
          حصرًا للطبيب المعالج.
        </p>
      </div>

      {/* Factor checkboxes */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          معايير دعم قرار الخلع
        </h3>
        <div className="grid gap-3 md:grid-cols-2">
          {EXTRACTION_FACTORS.map((factor) => {
            const checked = proExtraction[factor.key] ?? false;
            return (
              <button
                key={factor.key}
                type="button"
                onClick={() => toggleFactor(factor.key)}
                className={cn(
                  "flex items-center gap-3 rounded-lg border px-4 py-3 text-start transition",
                  checked
                    ? "border-clinic-blue bg-clinic-blue-50"
                    : "border-gray-200 bg-white hover:border-gray-300"
                )}
              >
                <div
                  className={cn(
                    "flex h-5 w-5 flex-shrink-0 items-center justify-center rounded border transition",
                    checked
                      ? "border-clinic-blue bg-clinic-blue"
                      : "border-gray-300 bg-white"
                  )}
                >
                  {checked && (
                    <CheckCircle2 className="h-3.5 w-3.5 text-white" />
                  )}
                </div>
                <span
                  className={cn(
                    "text-sm",
                    checked
                      ? "font-medium text-clinic-navy"
                      : "text-gray-700"
                  )}
                >
                  {factor.label}
                </span>
              </button>
            );
          })}
        </div>

        {/* Factor progress bar */}
        <div className="mt-5">
          <div className="mb-2 flex items-center justify-between text-sm">
            <span className="text-gray-500">
              معايير تدعم الخلع: {factorCount} من {totalFactors}
            </span>
            <span className="font-semibold text-gray-700">
              {factorPercent}%
            </span>
          </div>
          <div className="h-2.5 overflow-hidden rounded-full bg-gray-100">
            <div
              className={cn(
                "h-full rounded-full transition-all",
                factorCount >= 6
                  ? "bg-red-500"
                  : factorCount >= 4
                    ? "bg-amber-500"
                    : "bg-green-500"
              )}
              style={{ width: `${factorPercent}%` }}
            />
          </div>
        </div>

        {/* Recommendation badge */}
        <div
          className={cn(
            "mt-4 inline-flex items-center gap-2 rounded-lg border px-4 py-2",
            recommendation.bgColor
          )}
        >
          <span className={cn("text-sm font-semibold", recommendation.color)}>
            {recommendation.label}
          </span>
        </div>
      </div>

      {/* Doctor final decision */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          القرار النهائي للطبيب
        </h3>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="القرار">
            <select
              className={inputCls}
              value={form.decision ?? ""}
              onChange={(e) =>
                setForm((f) => ({ ...f, decision: e.target.value }))
              }
            >
              <option value="">اختر</option>
              <option value="Extraction">خلع</option>
              <option value="NonExtraction">بدون خلع</option>
              <option value="Borderline">حالة حدية</option>
            </select>
          </Field>
          <Field label="ملاحظات الطبيب">
            <textarea
              rows={3}
              className={inputCls}
              value={form.doctorNotes ?? ""}
              onChange={(e) =>
                setForm((f) => ({ ...f, doctorNotes: e.target.value }))
              }
            />
          </Field>
        </div>
      </div>

      <SaveButton saving={save.isPending}>حفظ القرار</SaveButton>
    </form>
  );
}

/* ------------------------------------------------------------------ */
/*  RetentionPanel                                                     */
/* ------------------------------------------------------------------ */

function RetentionPanel({ caseId }: { caseId: string }) {
  const { data } = useRetention(caseId);
  const save = useSaveRetention(caseId);
  const addVisit = useAddRetentionVisit(caseId);
  const [form, setForm] = useState<RetentionRecord>({});
  const [visit, setVisit] = useState({
    visitDate: "",
    period: "",
    toothStability: "",
    retainerStatus: "",
    notes: "",
  });
  useEffect(() => setForm(data ?? {}), [data]);

  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate(form);
        }}
        className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
      >
        <h2 className="font-semibold text-gray-900">سجل الاحتفاظ</h2>
        <Field label="تاريخ فك الجهاز">
          <input
            type="date"
            className={inputCls}
            value={form.debondDate ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, debondDate: e.target.value }))
            }
          />
        </Field>
        <Field label="Retainer علوي">
          <input
            className={inputCls}
            value={form.upperRetainer ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, upperRetainer: e.target.value }))
            }
          />
        </Field>
        <Field label="Retainer سفلي">
          <input
            className={inputCls}
            value={form.lowerRetainer ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, lowerRetainer: e.target.value }))
            }
          />
        </Field>
        <Field label="تعليمات">
          <textarea
            rows={3}
            className={inputCls}
            value={form.instructions ?? ""}
            onChange={(e) =>
              setForm((f) => ({ ...f, instructions: e.target.value }))
            }
          />
        </Field>
        <SaveButton saving={save.isPending}>حفظ الاحتفاظ</SaveButton>
      </form>
      <div className="space-y-4">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            addVisit.mutate(visit, {
              onSuccess: () =>
                setVisit({
                  visitDate: "",
                  period: "",
                  toothStability: "",
                  retainerStatus: "",
                  notes: "",
                }),
            });
          }}
          className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
        >
          <h2 className="font-semibold text-gray-900">زيارة احتفاظ</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="التاريخ">
              <input
                type="date"
                className={inputCls}
                value={visit.visitDate}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, visitDate: e.target.value }))
                }
              />
            </Field>
            <Field label="الفترة">
              <input
                className={inputCls}
                value={visit.period}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, period: e.target.value }))
                }
              />
            </Field>
            <Field label="ثبات الأسنان">
              <input
                className={inputCls}
                value={visit.toothStability}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, toothStability: e.target.value }))
                }
              />
            </Field>
            <Field label="حالة الجهاز">
              <input
                className={inputCls}
                value={visit.retainerStatus}
                onChange={(e) =>
                  setVisit((v) => ({ ...v, retainerStatus: e.target.value }))
                }
              />
            </Field>
          </div>
          <Field label="ملاحظات">
            <textarea
              rows={2}
              className={inputCls}
              value={visit.notes}
              onChange={(e) =>
                setVisit((v) => ({ ...v, notes: e.target.value }))
              }
            />
          </Field>
          <SaveButton saving={addVisit.isPending}>إضافة زيارة</SaveButton>
        </form>
        {(data?.visits?.length ?? 0) === 0 ? (
          <EmptyState text="لا توجد زيارات احتفاظ." />
        ) : (
          data?.visits?.map((v: RetentionVisit) => (
            <div
              key={v.id}
              className="rounded-lg border border-gray-200 bg-white p-4 text-sm"
            >
              <p className="font-semibold">
                {v.visitDate ? formatArabicDate(v.visitDate) : "بدون تاريخ"} ·{" "}
                {v.period}
              </p>
              <p className="mt-1 text-gray-500">
                {v.retainerStatus}{" "}
                {v.toothStability ? `· ${v.toothStability}` : ""}
              </p>
              {v.notes && <p className="mt-2 text-gray-700">{v.notes}</p>}
            </div>
          ))
        )}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  FinancePanel                                                       */
/* ------------------------------------------------------------------ */

function FinancePanel({
  caseId,
  patientId,
}: {
  caseId: string;
  patientId: string;
}) {
  const { data: overview } = useOrthoOverview(caseId);
  return (
    <div className="grid gap-5 md:grid-cols-3">
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">إجمالي العقد</p>
        <p className="mt-2 text-2xl font-bold text-gray-900">
          {formatYemeniRiyal(overview?.contractTotal ?? 0)}
        </p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">المدفوع</p>
        <p className="mt-2 text-2xl font-bold text-green-600">
          {formatYemeniRiyal(overview?.contractPaid ?? 0)}
        </p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">المتبقي</p>
        <p className="mt-2 text-2xl font-bold text-red-600">
          {formatYemeniRiyal(overview?.contractRemaining ?? 0)}
        </p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5 md:col-span-3">
        <div className="flex flex-wrap gap-3">
          {overview?.contractId && (
            <Link
              href={financeV3ContractsUrl(patientId)}
              className="rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white"
            >
              فتح العقد
            </Link>
          )}
          <Link
            href={financeV3ContractsUrl(patientId, { relatedCaseId: caseId })}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            إنشاء عقد تقويم
          </Link>
          <Link
            href={financeV3ContractsUrl(patientId)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            مالية المريض
          </Link>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Main Page                                                          */
/* ------------------------------------------------------------------ */

export default function OrthoCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<Tab>("overview");
  const { data: orthoCase, isLoading } = useOrthoCase(id);
  const { data: stages = [] } = useOrthoStages(id);
  const { data: visits = [] } = useOrthoVisits(id);
  const [localStages, setLocalStages] = useState<TreatmentStage[]>([]);
  useEffect(() => setLocalStages(stages), [stages]);

  const stageProgress = useMemo(
    () => orthoCase?.stagePercentage ?? 0,
    [orthoCase]
  );

  if (isLoading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 rounded-lg bg-gray-100" />
        <div className="h-80 rounded-lg bg-gray-100" />
      </div>
    );
  }

  if (!orthoCase) {
    return (
      <div className="py-20 text-center text-gray-400">
        الحالة غير موجودة
      </div>
    );
  }

  return (
    <div className="max-w-7xl space-y-5" dir="rtl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link
          href="/ortho"
          className="inline-flex items-center gap-1 hover:text-clinic-blue"
        >
          <ArrowRight className="h-4 w-4" /> التقويم
        </Link>
        <span>/</span>
        <span className="font-medium text-gray-900">
          {orthoCase.caseNumber}
        </span>
      </div>

      {/* Case header card */}
      <section className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div
              className="flex h-12 w-12 items-center justify-center rounded-lg text-white"
              style={{ backgroundColor: orthoCase.doctorColor ?? "#2563EB" }}
            >
              <GitBranch className="h-6 w-6" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-xl font-bold text-gray-900">
                  {orthoCase.patientName}
                </h1>
                <span className="rounded bg-gray-100 px-2 py-1 font-mono text-xs text-gray-600">
                  {orthoCase.caseNumber}
                </span>
                <span className="rounded-full bg-clinic-blue-50 px-2 py-1 text-xs font-medium text-clinic-blue">
                  {ORTHO_STATUS_LABELS[orthoCase.status] ??
                    orthoCase.status}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap gap-4 text-sm text-gray-500">
                <span>{orthoCase.doctorName ?? "بدون طبيب"}</span>
                {orthoCase.applianceType && (
                  <span>{orthoCase.applianceType}</span>
                )}
                {orthoCase.startDate && (
                  <span>بدأت: {formatArabicDate(orthoCase.startDate)}</span>
                )}
                {orthoCase.totalFee && (
                  <span>{formatYemeniRiyal(orthoCase.totalFee)}</span>
                )}
              </div>
            </div>
          </div>
          <Link
            href={`/patients/${orthoCase.patientId}`}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            <User className="h-4 w-4" />
            ملف المريض
          </Link>
        </div>
        <div className="mt-4 flex items-center gap-3">
          <div className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-clinic-blue transition-all"
              style={{ width: `${stageProgress}%` }}
            />
          </div>
          <span className="text-sm font-medium text-gray-600">
            {stageProgress}%
          </span>
        </div>
      </section>

      {/* Tab navigation */}
      <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
        <div className="flex overflow-x-auto border-b border-gray-100">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              type="button"
              onClick={() => setActiveTab(key)}
              className={cn(
                "inline-flex items-center gap-2 whitespace-nowrap border-b-2 px-4 py-3 text-sm font-medium transition",
                activeTab === key
                  ? "border-clinic-blue text-clinic-blue"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="h-4 w-4" />
              {label}
            </button>
          ))}
        </div>
        <div className="p-5">
          {activeTab === "overview" && (
            <OverviewPanel caseId={id} patientId={orthoCase.patientId} setActiveTab={setActiveTab} />
          )}
          {activeTab === "records" && <RecordsPanel caseId={id} />}
          {activeTab === "compare" && <OrthoBeforeAfterCompare caseId={id} />}
          {activeTab === "exam" && <ClinicalExamPanel caseId={id} />}
          {activeTab === "problems" && <ProblemsPanel caseId={id} />}
          {activeTab === "diagnosis" && <DiagnosisPanel caseId={id} />}
          {activeTab === "plan" && <TreatmentPlanPanel caseId={id} />}
          {activeTab === "stages" && (
            <TreatmentStagesPanel
              caseId={id}
              stages={localStages}
              onUpdate={(stage) =>
                setLocalStages((items) =>
                  items.map((item) =>
                    item.id === stage.id ? stage : item
                  )
                )
              }
            />
          )}
          {activeTab === "visits" && (
            <OrthoVisitTimeline caseId={id} visits={visits} />
          )}
          {activeTab === "extraction" && <ExtractionPanel caseId={id} />}
          {activeTab === "retention" && <RetentionPanel caseId={id} />}
          {activeTab === "finance" && (
            <FinancePanel caseId={id} patientId={orthoCase.patientId} />
          )}
        </div>
      </section>
    </div>
  );
}
