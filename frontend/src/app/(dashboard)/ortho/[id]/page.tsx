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
  ChevronDown,
  ClipboardCheck,
  FileText,
  GitBranch,
  GitCompareArrows,
  Images,
  Info,
  ListChecks,
  Plus,
  Save,
  Scissors,
  ScanLine,
  ShieldCheck,
  Star,
  Stethoscope,
  Trash2,
  User,
  UserSquare2,
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
  useCaseCephAnalyses,
  useCaseCephAnalysis,
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
  useUpdateOrthoPhoto,
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
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import {
  ANGLE_CLASS_LABELS,
  ARCH_FORM_LABELS,
  CHIN_POSITION_LABELS,
  CROSSBITE_TYPE_LABELS,
  CURVE_OF_SPEE_LABELS,
  EXTRACTION_FACTORS,
  HABIT_LABELS,
  INCISOR_RELATION_LABELS,
  LIP_COMPETENCE_LABELS,
  NASOLABIAL_LABELS,
  ORAL_HYGIENE_LABELS,
  ORTHO_PHOTO_CATEGORY_LABELS,
  ORTHO_PHOTO_SUBTYPES,
  ORTHO_STATUS_LABELS,
  RECORDS_CHECKLIST_ITEMS,
  TREATMENT_PHASE_LABELS,
  orthoSubtypeLabel,
} from "@/types/ortho";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { OrthoStagesTimeline } from "@/components/ortho/OrthoStagesTimeline";
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
  | "ceph"
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
  { key: "ceph", label: "السيفالو", icon: ScanLine },
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

const EMPTY_PHOTO_FORM = {
  photoUrl: "",
  photoType: "Intraoral",
  caption: "",
  category: "",
  subtype: "",
  treatmentPhase: "",
  isSelectedForReport: false,
};

const PHASE_BADGE_CLS: Record<string, string> = {
  Initial: "bg-green-600/90",
  Progress: "bg-blue-600/90",
  Final: "bg-violet-600/90",
};

function RecordsPanel({ caseId }: { caseId: string }) {
  const { data: photos = [] as OrthoPhoto[], refetch: refetchPhotos } =
    useOrthoPhotos(caseId);
  const { data: checklist, refetch: refetchChecklist } =
    useRecordsChecklist(caseId);
  const saveChecklist = useSaveChecklist(caseId);
  const updatePhoto = useUpdateOrthoPhoto(caseId);
  const [form, setForm] = useState({ ...EMPTY_PHOTO_FORM });
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewIndex, setPreviewIndex] = useState(0);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const [phaseFilter, setPhaseFilter] = useState<string>("all");
  const [categoryFilter, setCategoryFilter] = useState<string>("all");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const PHOTO_TYPE_LABELS: Record<string, string> = {
    Intraoral: "داخل الفم",
    Extraoral: "خارج الفم",
    Progress: "متابعة",
    Radiograph: "أشعة",
  };

  // Gallery filters (client-side — instant, no extra requests)
  const filteredPhotos = useMemo(() => {
    return photos.filter((p) => {
      if (phaseFilter !== "all" && p.treatmentPhase !== phaseFilter) return false;
      if (categoryFilter !== "all" && p.category !== categoryFilter) return false;
      return true;
    });
  }, [photos, phaseFilter, categoryFilter]);

  // إعادة ضبط مؤشر المعاينة عند تغيير التصفية حتى لا يخرج عن النطاق
  useEffect(() => {
    setPreviewIndex(0);
  }, [phaseFilter, categoryFilter]);

  /** الحقول الاختيارية الجديدة — تُرسل فقط عند تعبئتها (الرفع السريع القديم يبقى كما هو) */
  const tagPayload = () => ({
    category: form.category || undefined,
    subtype: form.subtype || undefined,
    treatmentPhase: form.treatmentPhase || undefined,
    isSelectedForReport: form.isSelectedForReport || undefined,
  });

  const toggleReportSelection = (p: OrthoPhoto) => {
    updatePhoto.mutate({
      photoId: p.id,
      data: { isSelectedForReport: !p.isSelectedForReport },
    });
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
      ).orthoService.addPhoto(caseId, {
        photoUrl: form.photoUrl,
        photoType: form.photoType,
        caption: form.caption,
        ...tagPayload(),
      });
      setForm({ ...EMPTY_PHOTO_FORM });
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
        ...tagPayload(),
      });
      setPhotoFile(null);
      setPhotoPreview(null);
      setForm((f) => ({ ...EMPTY_PHOTO_FORM, photoUrl: f.photoUrl }));
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
            <div className="grid grid-cols-2 gap-3">
              <Field label="فئة الصورة">
                <select
                  className={inputCls}
                  value={form.category}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      category: e.target.value,
                      subtype: "", // الأنواع الفرعية تتبع الفئة
                    }))
                  }
                >
                  <option value="">— غير محدد —</option>
                  {Object.entries(ORTHO_PHOTO_CATEGORY_LABELS).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="نوع فرعي">
                <select
                  className={inputCls}
                  value={form.subtype}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, subtype: e.target.value }))
                  }
                  disabled={
                    !form.category ||
                    (ORTHO_PHOTO_SUBTYPES[form.category] ?? []).length === 0
                  }
                >
                  <option value="">— غير محدد —</option>
                  {(ORTHO_PHOTO_SUBTYPES[form.category] ?? []).map((s) => (
                    <option key={s.value} value={s.value}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </Field>
            </div>
            <Field label="مرحلة العلاج">
              <select
                className={inputCls}
                value={form.treatmentPhase}
                onChange={(e) =>
                  setForm((f) => ({ ...f, treatmentPhase: e.target.value }))
                }
              >
                <option value="">— غير محدد —</option>
                {Object.entries(TREATMENT_PHASE_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </Field>
            <label className="flex cursor-pointer items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={form.isSelectedForReport}
                onChange={(e) =>
                  setForm((f) => ({ ...f, isSelectedForReport: e.target.checked }))
                }
                className="h-4 w-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
              />
              إدراج في التقرير
            </label>
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
        <div className="space-y-3">
          {/* Filter pills */}
          {photos.length > 0 && (
            <div className="flex flex-wrap items-center gap-2">
              {[
                { value: "all", label: "الكل" },
                ...Object.entries(TREATMENT_PHASE_LABELS).map(([value, label]) => ({
                  value,
                  label,
                })),
              ].map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setPhaseFilter(opt.value)}
                  className={cn(
                    "rounded-full border px-3 py-1 text-xs font-medium transition",
                    phaseFilter === opt.value
                      ? "border-clinic-blue bg-clinic-blue text-white"
                      : "border-gray-200 bg-white text-gray-600 hover:border-clinic-blue/40"
                  )}
                >
                  {opt.label}
                </button>
              ))}
              <span className="mx-1 h-4 w-px bg-gray-200" />
              {[
                { value: "all", label: "كل الفئات" },
                ...Object.entries(ORTHO_PHOTO_CATEGORY_LABELS).map(
                  ([value, label]) => ({ value, label })
                ),
              ].map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setCategoryFilter(opt.value)}
                  className={cn(
                    "rounded-full border px-3 py-1 text-xs font-medium transition",
                    categoryFilter === opt.value
                      ? "border-clinic-blue bg-clinic-blue text-white"
                      : "border-gray-200 bg-white text-gray-600 hover:border-clinic-blue/40"
                  )}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          )}
          {photos.length === 0 ? (
            <EmptyState text="لا توجد صور أو سجلات مرتبطة بحالة التقويم." />
          ) : filteredPhotos.length === 0 ? (
            <EmptyState text="لا توجد صور مطابقة للتصفية المحددة." />
          ) : (
            <div className="grid gap-3 grid-cols-2 md:grid-cols-3">
              {filteredPhotos.map((p: OrthoPhoto, idx: number) => (
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
                  {/* Phase + subtype/type badges */}
                  <div className="absolute top-2 right-2 flex max-w-[75%] flex-wrap justify-end gap-1">
                    {p.treatmentPhase && TREATMENT_PHASE_LABELS[p.treatmentPhase] && (
                      <span
                        className={cn(
                          "rounded px-1.5 py-0.5 text-[10px] font-bold text-white",
                          PHASE_BADGE_CLS[p.treatmentPhase] ?? "bg-black/50"
                        )}
                      >
                        {TREATMENT_PHASE_LABELS[p.treatmentPhase]}
                      </span>
                    )}
                    <span className="rounded bg-black/50 px-1.5 py-0.5 text-[10px] font-medium text-white">
                      {orthoSubtypeLabel(p.subtype) ||
                        (p.category && ORTHO_PHOTO_CATEGORY_LABELS[p.category]) ||
                        PHOTO_TYPE_LABELS[p.photoType] ||
                        p.photoType}
                    </span>
                  </div>
                  {/* Report selection toggle */}
                  <button
                    type="button"
                    title={
                      p.isSelectedForReport
                        ? "إزالة من التقرير"
                        : "إدراج في التقرير"
                    }
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleReportSelection(p);
                    }}
                    className={cn(
                      "absolute bottom-2 left-2 z-10 rounded-full p-1.5 transition",
                      p.isSelectedForReport
                        ? "bg-amber-400 text-white shadow"
                        : "bg-black/40 text-white opacity-0 group-hover:opacity-100 hover:bg-amber-400"
                    )}
                  >
                    <Star
                      className="h-3.5 w-3.5"
                      fill={p.isSelectedForReport ? "currentColor" : "none"}
                    />
                  </button>
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

      {/* Image Preview Modal — navigates within the filtered gallery */}
      <ImagePreviewModal
        isOpen={previewOpen}
        onClose={() => setPreviewOpen(false)}
        url={filteredPhotos[previewIndex]?.photoUrl ?? ""}
        fileName={
          filteredPhotos[previewIndex]?.caption ||
          filteredPhotos[previewIndex]?.photoType
        }
        items={filteredPhotos.map((p) => ({
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

/** قسم قابل للطي في نموذج الفحص السريري (RTL) */
function ExamSection({
  title,
  defaultOpen = true,
  children,
}: {
  title: string;
  defaultOpen?: boolean;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="rounded-lg border border-gray-200 bg-white">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between px-5 py-3 text-sm font-semibold text-clinic-navy"
      >
        <span>{title}</span>
        <ChevronDown
          className={cn(
            "h-4 w-4 text-gray-400 transition-transform",
            open && "rotate-180"
          )}
        />
      </button>
      {open && <div className="border-t border-gray-100 p-5">{children}</div>}
    </div>
  );
}

/** قائمة منسدلة لقيمة معيارية (القيمة المخزنة → تسمية عربية) */
function ExamEnumSelect({
  label,
  value,
  labels,
  onChange,
  className,
}: {
  label: string;
  value?: string;
  labels: Record<string, string>;
  onChange: (value?: string) => void;
  className?: string;
}) {
  return (
    <Field label={label} className={className}>
      <select
        className={inputCls}
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value || undefined)}
      >
        <option value="">اختر</option>
        {Object.entries(labels).map(([v, l]) => (
          <option key={v} value={v}>
            {l}
          </option>
        ))}
      </select>
    </Field>
  );
}

function ExamNumberInput({
  label,
  value,
  onChange,
  step = 0.1,
  min,
  max,
}: {
  label: string;
  value?: number;
  onChange: (value?: number) => void;
  step?: number;
  min?: number;
  max?: number;
}) {
  return (
    <Field label={label}>
      <input
        type="number"
        step={step}
        min={min}
        max={max}
        className={inputCls}
        value={value ?? ""}
        onChange={(e) =>
          onChange(e.target.value === "" ? undefined : Number(e.target.value))
        }
      />
    </Field>
  );
}

function ExamCheckbox({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked?: boolean;
  onChange: (checked: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50">
      <input
        type="checkbox"
        className="h-4 w-4 rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
        checked={checked ?? false}
        onChange={(e) => onChange(e.target.checked)}
      />
      {label}
    </label>
  );
}

/** مفاتيح العادات الفموية المُهيكلة في ClinicalExam */
const HABIT_FLAG_KEYS = [
  "thumbSucking",
  "mouthBreathing",
  "tongueThrust",
  "lipBiting",
  "nailBiting",
  "bruxism",
] as const;

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
      {/* ١) الفحص خارج الفم */}
      <ExamSection title="الفحص خارج الفم">
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
          <Field label="انطباق الشفاه (نعم/لا)">
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
              <option value="true">منطبقة</option>
              <option value="false">غير منطبقة</option>
            </select>
          </Field>
          <ExamEnumSelect
            label="درجة انطباق الشفاه"
            value={form.lipCompetenceGrade}
            labels={LIP_COMPETENCE_LABELS}
            onChange={(v) => set("lipCompetenceGrade", v)}
          />
          <ExamEnumSelect
            label="الزاوية الأنفية الشفوية"
            value={form.nasolabialAngle}
            labels={NASOLABIAL_LABELS}
            onChange={(v) => set("nasolabialAngle", v)}
          />
          <ExamEnumSelect
            label="وضع الذقن"
            value={form.chinPosition}
            labels={CHIN_POSITION_LABELS}
            onChange={(v) => set("chinPosition", v)}
          />
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
          <Field label="انزياح وظيفي">
            <input
              className={inputCls}
              value={form.functionalShift ?? ""}
              onChange={(e) => set("functionalShift", e.target.value)}
              placeholder="انزياح الفك عند الإطباق إن وجد"
            />
          </Field>
          <div className="flex items-end">
            <ExamCheckbox
              label="ابتسامة لثوية"
              checked={form.gummySmile}
              onChange={(v) => set("gummySmile", v)}
            />
          </div>
        </div>
      </ExamSection>

      {/* ٢) العادات الفموية */}
      <ExamSection title="العادات الفموية">
        <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-3">
          {HABIT_FLAG_KEYS.map((key) => (
            <ExamCheckbox
              key={key}
              label={HABIT_LABELS[key]}
              checked={form[key]}
              onChange={(v) => set(key, v)}
            />
          ))}
        </div>
        <div className="mt-4">
          <Field label="تفاصيل العادات (نص حر)">
            <textarea
              rows={2}
              className={inputCls}
              value={form.habits ?? ""}
              onChange={(e) => set("habits", e.target.value)}
              placeholder="تنفس فمي، مص إصبع..."
            />
          </Field>
        </div>
      </ExamSection>

      {/* ٣) الفحص داخل الفم */}
      <ExamSection title="الفحص داخل الفم">
        <div className="grid gap-4 md:grid-cols-3">
          <ExamEnumSelect
            label="نظافة الفم"
            value={form.oralHygiene}
            labels={ORAL_HYGIENE_LABELS}
            onChange={(v) => set("oralHygiene", v)}
          />
          <Field label="حالة اللثة">
            <input
              className={inputCls}
              value={form.gingivalCondition ?? ""}
              onChange={(e) => set("gingivalCondition", e.target.value)}
            />
          </Field>
          <Field label="مشاكل دواعم الأسنان">
            <input
              className={inputCls}
              value={form.periodontalConcerns ?? ""}
              onChange={(e) => set("periodontalConcerns", e.target.value)}
            />
          </Field>
          <Field label="أسنان مفقودة (FDI)">
            <input
              className={inputCls}
              value={form.missingTeethFdi ?? ""}
              onChange={(e) => set("missingTeethFdi", e.target.value)}
              placeholder="مثال: 11,21"
            />
          </Field>
          <Field label="أسنان لبنية متبقية (FDI)">
            <input
              className={inputCls}
              value={form.retainedDeciduousFdi ?? ""}
              onChange={(e) => set("retainedDeciduousFdi", e.target.value)}
              placeholder="مثال: 11,21"
            />
          </Field>
          <Field label="أسنان منطمرة (FDI)">
            <input
              className={inputCls}
              value={form.impactedTeethFdi ?? ""}
              onChange={(e) => set("impactedTeethFdi", e.target.value)}
              placeholder="مثال: 11,21"
            />
          </Field>
          <Field label="أسنان زائدة">
            <input
              className={inputCls}
              value={form.supernumeraryNote ?? ""}
              onChange={(e) => set("supernumeraryNote", e.target.value)}
            />
          </Field>
          <Field label="بزوغ منتبذ">
            <input
              className={inputCls}
              value={form.ectopicEruptionNote ?? ""}
              onChange={(e) => set("ectopicEruptionNote", e.target.value)}
            />
          </Field>
          <Field label="اللجام">
            <input
              className={inputCls}
              value={form.frenumNote ?? ""}
              onChange={(e) => set("frenumNote", e.target.value)}
            />
          </Field>
          <Field label="اللسان">
            <input
              className={inputCls}
              value={form.tongueNote ?? ""}
              onChange={(e) => set("tongueNote", e.target.value)}
            />
          </Field>
          <Field label="التسوس">
            <input
              className={inputCls}
              value={form.cariesNote ?? ""}
              onChange={(e) => set("cariesNote", e.target.value)}
            />
          </Field>
        </div>
      </ExamSection>

      {/* ٤) فحص الإطباق */}
      <ExamSection title="فحص الإطباق">
        <div className="space-y-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="grid grid-cols-2 gap-3 rounded-lg bg-gray-50 p-3">
              <ExamEnumSelect
                label="علاقة الأرحاء — يمين"
                value={form.molarRelationRight}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("molarRelationRight", v)}
              />
              <ExamEnumSelect
                label="علاقة الأرحاء — يسار"
                value={form.molarRelationLeft}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("molarRelationLeft", v)}
              />
            </div>
            <div className="grid grid-cols-2 gap-3 rounded-lg bg-gray-50 p-3">
              <ExamEnumSelect
                label="علاقة الأنياب — يمين"
                value={form.canineRelationRight}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("canineRelationRight", v)}
              />
              <ExamEnumSelect
                label="علاقة الأنياب — يسار"
                value={form.canineRelationLeft}
                labels={ANGLE_CLASS_LABELS}
                onChange={(v) => set("canineRelationLeft", v)}
              />
            </div>
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <ExamEnumSelect
              label="العلاقة القاطعية"
              value={form.incisorRelation}
              labels={INCISOR_RELATION_LABELS}
              onChange={(v) => set("incisorRelation", v)}
            />
            <Field label="علاقة الأرحاء (عام)">
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
            <Field label="علاقة الأنياب (عام)">
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
            <ExamNumberInput
              label="Overjet (mm)"
              min={-30}
              max={30}
              value={form.overjet}
              onChange={(v) => set("overjet", v)}
            />
            <ExamNumberInput
              label="Overbite (mm)"
              min={-30}
              max={30}
              value={form.overbite}
              onChange={(v) => set("overbite", v)}
            />
            <ExamNumberInput
              label="Overbite (%)"
              min={0}
              max={200}
              step={1}
              value={form.overbitePercent}
              onChange={(v) => set("overbitePercent", v)}
            />
          </div>
          <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-4">
            <ExamCheckbox
              label="عضة معكوسة (Crossbite)"
              checked={form.crossbite}
              onChange={(v) => set("crossbite", v)}
            />
            <ExamCheckbox
              label="عضة مفتوحة (Open Bite)"
              checked={form.openBite}
              onChange={(v) => set("openBite", v)}
            />
            <ExamCheckbox
              label="عضة عميقة (Deep Bite)"
              checked={form.deepBite}
              onChange={(v) => set("deepBite", v)}
            />
            <ExamCheckbox
              label="عضة مقصية (Scissor Bite)"
              checked={form.scissorBite}
              onChange={(v) => set("scissorBite", v)}
            />
          </div>
          {form.crossbite && (
            <div className="grid gap-4 md:grid-cols-3">
              <ExamEnumSelect
                label="نوع العضة المعكوسة"
                value={form.crossbiteType}
                labels={CROSSBITE_TYPE_LABELS}
                onChange={(v) => set("crossbiteType", v)}
              />
            </div>
          )}
          <div className="grid gap-4 md:grid-cols-3">
            <ExamNumberInput
              label="انحراف الخط الناصف العلوي (mm، + = يمين)"
              min={-30}
              max={30}
              value={form.midlineUpperShiftMm}
              onChange={(v) => set("midlineUpperShiftMm", v)}
            />
            <ExamNumberInput
              label="انحراف الخط الناصف السفلي (mm، + = يمين)"
              min={-30}
              max={30}
              value={form.midlineLowerShiftMm}
              onChange={(v) => set("midlineLowerShiftMm", v)}
            />
            <Field label="الخط الناصف العلوي (وصف)">
              <input
                className={inputCls}
                value={form.midlineUpper ?? ""}
                onChange={(e) => set("midlineUpper", e.target.value)}
                placeholder="متوافق / منحرف يمين / منحرف يسار"
              />
            </Field>
            <Field label="الخط الناصف السفلي (وصف)">
              <input
                className={inputCls}
                value={form.midlineLower ?? ""}
                onChange={(e) => set("midlineLower", e.target.value)}
                placeholder="متوافق / منحرف يمين / منحرف يسار"
              />
            </Field>
            <ExamNumberInput
              label="تكدس علوي (mm)"
              min={-30}
              max={30}
              value={form.upperCrowdingMm}
              onChange={(v) => set("upperCrowdingMm", v)}
            />
            <ExamNumberInput
              label="تكدس سفلي (mm)"
              min={-30}
              max={30}
              value={form.lowerCrowdingMm}
              onChange={(v) => set("lowerCrowdingMm", v)}
            />
            <Field label="تكدس علوي (وصف)">
              <input
                className={inputCls}
                value={form.upperCrowding ?? ""}
                onChange={(e) => set("upperCrowding", e.target.value)}
                placeholder="خفيف / متوسط / شديد"
              />
            </Field>
            <Field label="تكدس سفلي (وصف)">
              <input
                className={inputCls}
                value={form.lowerCrowding ?? ""}
                onChange={(e) => set("lowerCrowding", e.target.value)}
                placeholder="خفيف / متوسط / شديد"
              />
            </Field>
            <ExamNumberInput
              label="مسافات علوية (mm)"
              value={form.upperSpacing}
              onChange={(v) => set("upperSpacing", v)}
            />
            <ExamNumberInput
              label="مسافات سفلية (mm)"
              min={-30}
              max={30}
              value={form.lowerSpacingMm}
              onChange={(v) => set("lowerSpacingMm", v)}
            />
            <ExamEnumSelect
              label="منحنى شبي (Curve of Spee)"
              value={form.curveOfSpee}
              labels={CURVE_OF_SPEE_LABELS}
              onChange={(v) => set("curveOfSpee", v)}
            />
            <ExamEnumSelect
              label="شكل القوس العلوي"
              value={form.archFormUpper}
              labels={ARCH_FORM_LABELS}
              onChange={(v) => set("archFormUpper", v)}
            />
            <ExamEnumSelect
              label="شكل القوس السفلي"
              value={form.archFormLower}
              labels={ARCH_FORM_LABELS}
              onChange={(v) => set("archFormLower", v)}
            />
            <Field label="ملاحظة تحليل بولتون" className="md:col-span-2">
              <input
                className={inputCls}
                value={form.boltonDiscrepancyNote ?? ""}
                onChange={(e) => set("boltonDiscrepancyNote", e.target.value)}
              />
            </Field>
          </div>
        </div>
      </ExamSection>

      {/* ٥) وظيفي وملاحظات */}
      <ExamSection title="وظيفي وملاحظات">
        <div className="grid gap-4 md:grid-cols-2">
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
          <Field label="ملاحظات TMJ">
            <textarea
              rows={2}
              className={inputCls}
              value={form.tmjFindings ?? ""}
              onChange={(e) => set("tmjFindings", e.target.value)}
            />
          </Field>
          <Field label="ملاحظات عامة" className="md:col-span-2">
            <textarea
              rows={3}
              className={inputCls}
              value={form.notes ?? ""}
              onChange={(e) => set("notes", e.target.value)}
            />
          </Field>
        </div>
      </ExamSection>

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
/*  CephPanel                                                          */
/* ------------------------------------------------------------------ */

function CephPanel({ caseId }: { caseId: string }) {
  const { data: analyses = [], isLoading } = useCaseCephAnalyses(caseId);
  const latest = analyses[0];
  const { data: latestDetail } = useCaseCephAnalysis(latest?.id);
  const { data: diagnosis } = useDiagnosis(caseId);

  const keyMeasurements = ["SNA", "SNB", "ANB", "Wits", "FMA", "IMPA"]
    .map((name) => latestDetail?.measurements.find((m) => m.name === name))
    .filter((measurement) => measurement !== undefined);

  const syncState = diagnosis?.isApproved && diagnosis.isCephSyncOutdated
    ? {
        tone: "border-amber-200 bg-amber-50 text-amber-800",
        title: "يوجد تحليل أحدث من التشخيص المعتمد",
        detail: "حمايةً للقرار السريري لم تُعدّل النتائج المعتمدة تلقائياً. راجع التحليل الجديد قبل تحديث التشخيص.",
      }
    : diagnosis?.cephSourceAnalysisId === latest?.id && diagnosis?.cephSyncedAt
      ? {
          tone: "border-green-200 bg-green-50 text-green-800",
          title: "القياسات متزامنة مع تشخيص الحالة",
          detail: `آخر مزامنة: ${formatArabicDate(diagnosis.cephSyncedAt)}`,
        }
      : latest?.hasMeasurements
        ? {
            tone: "border-blue-200 bg-blue-50 text-blue-800",
            title: "التحليل محفوظ داخل الحالة",
            detail: "ستنتقل القياسات تلقائياً إلى التشخيص عند حفظ نقاط التحليل.",
          }
        : null;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold text-gray-900">مساحة السيفالو للحالة</h2>
          <p className="mt-1 text-sm text-gray-500">
            الأشعة والقياسات والتشخيص السيفالومتري مرتبطة بهذه الحالة تلقائياً.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link
            href={`/ceph/photo?orthoCaseId=${caseId}`}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
          >
            <UserSquare2 className="h-4 w-4" />
            تحليل صورة البروفايل
          </Link>
          <Link
            href={`/ceph/new?orthoCaseId=${caseId}`}
            className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-3 py-2 text-sm font-medium text-white transition hover:opacity-90"
          >
            <Plus className="h-4 w-4" />
            تحليل سيفالو جديد
          </Link>
        </div>
      </div>

      {syncState && (
        <div className={cn("rounded-lg border px-4 py-3", syncState.tone)}>
          <p className="text-sm font-semibold">{syncState.title}</p>
          <p className="mt-1 text-xs leading-5">{syncState.detail}</p>
        </div>
      )}

      {isLoading ? (
        <div className="grid gap-3 md:grid-cols-2">
          <div className="h-44 animate-pulse rounded-lg bg-gray-100" />
          <div className="h-44 animate-pulse rounded-lg bg-gray-100" />
        </div>
      ) : analyses.length === 0 ? (
        <EmptyState text="لا يوجد تحليل سيفالومتري لهذه الحالة بعد" />
      ) : (
        <>
          <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
            <section className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-xs text-gray-500">أحدث تحليل</p>
                  <h3 className="mt-1 font-semibold text-gray-900">
                    {ANALYSIS_TYPE_AR[latest.analysisType] ?? latest.analysisType}
                  </h3>
                  <p className="mt-1 text-xs text-gray-500">
                    {formatArabicDate(latest.analysisDate)}
                  </p>
                </div>
                <span className="rounded-full bg-clinic-blue-50 px-2 py-1 text-xs font-medium text-clinic-blue">
                  {latest.landmarkCount}/24 نقطة
                </span>
              </div>

              <div className="mt-4 grid grid-cols-3 gap-2">
                {keyMeasurements.length > 0 ? keyMeasurements.map((measurement) => (
                  <div
                    key={measurement.name}
                    className="rounded-lg border border-gray-100 bg-gray-50 px-2 py-3 text-center"
                  >
                    <p className="text-[11px] font-medium text-gray-500">{measurement.name}</p>
                    <p className="mt-1 font-mono text-base font-bold text-gray-900" dir="ltr">
                      {measurement.value ?? "—"}{measurement.value !== null ? measurement.unit : ""}
                    </p>
                  </div>
                )) : (
                  <p className="col-span-3 py-4 text-center text-xs text-gray-400">
                    أكمل وضع النقاط وحفظها لإظهار القياسات.
                  </p>
                )}
              </div>

              {latestDetail?.diagnosis && (
                <div className="mt-4 space-y-1 rounded-lg bg-clinic-blue-50/60 p-3 text-xs text-clinic-navy">
                  <p><span className="font-semibold">الهيكلي:</span> {latestDetail.diagnosis.skeletalClass ?? "—"}</p>
                  <p><span className="font-semibold">النمط الرأسي:</span> {latestDetail.diagnosis.verticalPattern ?? "—"}</p>
                  <p><span className="font-semibold">القواطع:</span> {latestDetail.diagnosis.incisorInclination ?? "—"}</p>
                </div>
              )}

              <Link
                href={`/ceph/${latest.id}`}
                className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-lg border border-clinic-blue px-3 py-2 text-sm font-medium text-clinic-blue transition hover:bg-clinic-blue-50"
              >
                <ScanLine className="h-4 w-4" />
                فتح مساحة التحليل
              </Link>
            </section>

            <section className="overflow-hidden rounded-lg border border-gray-200 bg-white">
              <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">سجل تحاليل الحالة</h3>
                  <p className="text-xs text-gray-500">{analyses.length} تحليل محفوظ</p>
                </div>
                {analyses.length >= 2 && (
                  <Link
                    href={`/ceph/compare?baseId=${analyses[1].id}&targetId=${analyses[0].id}`}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-2.5 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
                  >
                    <GitCompareArrows className="h-3.5 w-3.5" />
                    مقارنة آخر تحليلين
                  </Link>
                )}
              </div>
              <div className="divide-y divide-gray-100">
                {analyses.map((analysis) => (
                  <div
                    key={analysis.id}
                    className="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
                  >
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        {ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}
                      </p>
                      <p className="mt-0.5 text-xs text-gray-500">
                        {formatArabicDate(analysis.analysisDate)} · {analysis.landmarkCount} نقطة
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className={cn(
                        "text-xs font-medium",
                        analysis.hasMeasurements ? "text-green-600" : "text-amber-600"
                      )}>
                        {analysis.hasMeasurements ? "القياسات مكتملة" : "قيد الإعداد"}
                      </span>
                      <Link
                        href={`/ceph/${analysis.id}`}
                        className="text-xs font-medium text-clinic-blue hover:underline"
                      >
                        فتح
                      </Link>
                    </div>
                  </div>
                ))}
              </div>
            </section>
          </div>
        </>
      )}
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
      {form.cephSourceAnalysisId && (
        <div className={cn(
          "rounded-lg border px-4 py-3",
          form.isApproved && form.isCephSyncOutdated
            ? "border-amber-200 bg-amber-50 text-amber-800"
            : "border-blue-200 bg-blue-50 text-blue-800"
        )}>
          <p className="text-sm font-semibold">
            {form.isApproved && form.isCephSyncOutdated
              ? "التشخيص المعتمد محفوظ ويوجد تحليل سيفالو أحدث"
              : "القياسات السيفالومترية منقولة تلقائياً من أحدث تحليل"}
          </p>
          <div className="mt-1 flex flex-wrap items-center gap-3 text-xs">
            {form.cephSyncedAt && (
              <span>آخر مزامنة: {formatArabicDate(form.cephSyncedAt)}</span>
            )}
            <Link
              href={`/ceph/${form.latestCephAnalysisId ?? form.cephSourceAnalysisId}`}
              className="font-medium underline underline-offset-2"
            >
              فتح التحليل المصدر
            </Link>
          </div>
        </div>
      )}

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
  useEffect(() => {
    const requestedTab = new URLSearchParams(window.location.search).get("tab") as Tab | null;
    if (requestedTab && TABS.some((tab) => tab.key === requestedTab)) {
      setActiveTab(requestedTab);
    }
  }, []);

  const selectTab = (tab: Tab) => {
    setActiveTab(tab);
    const url = new URL(window.location.href);
    if (tab === "overview") url.searchParams.delete("tab");
    else url.searchParams.set("tab", tab);
    window.history.replaceState(null, "", `${url.pathname}${url.search}`);
  };

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
              onClick={() => selectTab(key)}
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
            <OverviewPanel caseId={id} patientId={orthoCase.patientId} setActiveTab={selectTab} />
          )}
          {activeTab === "records" && <RecordsPanel caseId={id} />}
          {activeTab === "compare" && <OrthoBeforeAfterCompare caseId={id} />}
          {activeTab === "exam" && <ClinicalExamPanel caseId={id} />}
          {activeTab === "problems" && <ProblemsPanel caseId={id} />}
          {activeTab === "ceph" && <CephPanel caseId={id} />}
          {activeTab === "diagnosis" && <DiagnosisPanel caseId={id} />}
          {activeTab === "plan" && <TreatmentPlanPanel caseId={id} />}
          {activeTab === "stages" && (
            <div className="space-y-6">
              <OrthoStagesTimeline stages={localStages} />
              {localStages.length > 0 && (
                <div className="border-t border-gray-100 pt-5">
                  <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
                    إدارة المراحل
                  </h3>
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
                </div>
              )}
            </div>
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
