"use client";

import { useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import {
  User,
  MessageSquare,
  Camera,
  Scan,
  Smile,
  Stethoscope,
  Image as ImageIcon,
  Ruler,
  Microscope,
  Sigma,
  ListChecks,
  ClipboardList,
  Target,
  GitBranch,
  FileText,
  Wrench,
  CalendarClock,
  Trophy,
  Shield,
  Presentation,
  CheckCircle2,
  CircleDashed,
  AlertCircle,
  BadgeCheck,
  ChevronLeft,
  Loader2,
  Upload,
  Pencil,
  Sparkles,
  ArrowUpCircle,
} from "lucide-react";
import api from "@/lib/api";
import { orthoService, type OrthoPresentationDefinition } from "@/services/orthoService";
import { useOrthoOverview, useOrthoPhotos } from "@/hooks/useOrtho";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { toast } from "@/stores/toastStore";
import { cn } from "@/lib/utils";
import type { OrthoPhoto } from "@/types/ortho";
import { OrthoImagePreparationDialog } from "./OrthoImagePreparationDialog";

type StepStatus = "missing" | "partial" | "complete" | "approved";
type StepPriority = "critical" | "supporting" | "optional";
type Tab =
  | "overview" | "exam" | "cast" | "ceph" | "facial" | "problems" | "diagnosis"
  | "plan" | "stages" | "visits" | "retention" | "records" | "reports";

interface PhotoSlot {
  key: string;
  label: string;
  category: string;
  subtype: string;
  phase: string;
  capture: "environment" | "user";
}

interface WizardStep {
  order: number;
  key: string;
  title: string;
  required: string;
  minimum: string;
  doctorAction: string;
  icon: typeof User;
  kind: "patient" | "data" | "photos" | "generate";
  priority: StepPriority;
  tab?: Tab;
  slideTypes?: string[];
  approvedFlag?: "diagnosis" | "plan";
  photoSlots?: PhotoSlot[];
  aiDraft?: boolean;
}

const STEPS: WizardStep[] = [
  {
    order: 1, key: "patient", title: "بيانات المريض", required: "الاسم، رقم الملف، العمر، الجنس",
    minimum: "تأكد أن بيانات المريض صحيحة؛ هذه المعلومات تظهر في غلاف العرض وشرائح البيانات.",
    doctorAction: "افتح ملف المريض وعدّل البيانات الأساسية إن لزم.",
    icon: User, kind: "patient", priority: "critical", slideTypes: ["PatientInformation"],
  },
  {
    order: 2, key: "complaint", title: "المقابلة والشكوى الرئيسية", required: "الشكوى الرئيسية والتاريخ",
    minimum: "يجب وجود شكوى رئيسية واضحة أو ملاحظات سريرية تلخص سبب الحضور.",
    doctorAction: "انتقل للفحص السريري وأضف الشكوى والتاريخ المختصر.",
    icon: MessageSquare, kind: "data", priority: "critical", tab: "exam", slideTypes: ["ChiefComplaint"],
  },
  {
    order: 3, key: "extraoral", title: "الصور خارج الفم", required: "أمامي، جانبي، ابتسامة",
    minimum: "الحد الأدنى للعرض: صورة أمامية، بروفايل، وابتسامة في طور البداية.",
    doctorAction: "التقط الصور مباشرة من الجوال، ثم جهّزها للعرض بالقص والتدوير إن احتاجت.",
    icon: Camera, kind: "photos", priority: "critical", slideTypes: ["ExtraoralPhotos"], photoSlots: [
      { key: "exf", label: "أمامي", category: "Extraoral", subtype: "Frontal", phase: "Initial", capture: "user" },
      { key: "exp", label: "جانبي (بروفايل)", category: "Extraoral", subtype: "Profile", phase: "Initial", capture: "user" },
      { key: "exs", label: "الابتسامة", category: "Extraoral", subtype: "Smile", phase: "Initial", capture: "user" },
    ],
  },
  {
    order: 4, key: "facial", title: "التحليل الوجهي", required: "تحليل الملف/الأنسجة الرخوة",
    minimum: "وجود تحليل وجهي أو ملاحظات أنسجة رخوة يجعل التشخيص النهائي أوضح.",
    doctorAction: "افتح تحليل الصور أو أضف ملاحظات الوجه والبروفايل.",
    icon: Smile, kind: "data", priority: "supporting", tab: "facial", slideTypes: ["FacialAnalysis"],
  },
  {
    order: 5, key: "intraoral", title: "الصور داخل الفم", required: "أمامي، يمين، يسار، علوي، سفلي",
    minimum: "الحد الأدنى السريري: أمامي، يمين، يسار، إطباقي علوي، إطباقي سفلي.",
    doctorAction: "استخدم كاميرا الجوال الخلفية، ثم جهّز الصور بنسب عرض مناسبة دون تشويه.",
    icon: Scan, kind: "photos", priority: "critical", slideTypes: ["IntraoralPhotos"], photoSlots: [
      { key: "inf", label: "أمامي", category: "Intraoral", subtype: "Frontal", phase: "Initial", capture: "environment" },
      { key: "inr", label: "يمين", category: "Intraoral", subtype: "Right", phase: "Initial", capture: "environment" },
      { key: "inl", label: "يسار", category: "Intraoral", subtype: "Left", phase: "Initial", capture: "environment" },
      { key: "inu", label: "إطباقي علوي", category: "Intraoral", subtype: "Upper Occlusal", phase: "Initial", capture: "environment" },
      { key: "ind", label: "إطباقي سفلي", category: "Intraoral", subtype: "Lower Occlusal", phase: "Initial", capture: "environment" },
    ],
  },
  {
    order: 6, key: "occlusion", title: "تقييم الإطباق", required: "علاقة الأرحاء/الأنياب، البزوغ، التطابق، الازدحام",
    minimum: "لا تولّد عرضًا نهائيًا قبل تسجيل العلاقات الإطباقية الأساسية وOJ/OB والازدحام.",
    doctorAction: "افتح الفحص السريري وسجّل الإطباق يمين/يسار والـ overjet/overbite والازدحام.",
    icon: Stethoscope, kind: "data", priority: "critical", tab: "exam", slideTypes: ["OcclusionAssessment"],
  },
  {
    order: 7, key: "pano", title: "الأشعة البانورامية", required: "صورة OPG",
    minimum: "OPG مهم لعرض الحالة، لكنه يمكن أن يُخفى من العرض إذا غير متوفر.",
    doctorAction: "ارفع صورة البانوراما أو التقطها من شاشة الأشعة عند الحاجة.",
    icon: ImageIcon, kind: "photos", priority: "supporting", slideTypes: ["PanoramicXray"], photoSlots: [
      { key: "opg", label: "بانوراما", category: "Radiograph", subtype: "OPG", phase: "Initial", capture: "environment" },
    ],
  },
  {
    order: 8, key: "cephxray", title: "الأشعة السيفالومترية", required: "صورة سيفالو جانبية",
    minimum: "صورة السيفالو أو تحليل سيفالومتري محفوظ يكفي لشرائح السيفالو.",
    doctorAction: "ارفع السيفالو أو افتح وحدة السيفالو لإضافة التحليل.",
    icon: ImageIcon, kind: "photos", priority: "supporting", slideTypes: ["CephalometricSummary"], photoSlots: [
      { key: "ceph", label: "سيفالو جانبي", category: "Radiograph", subtype: "Lateral Ceph", phase: "Initial", capture: "environment" },
    ],
  },
  {
    order: 9, key: "cephmeas", title: "قياسات السيفالو", required: "تحليل سيفالومتري محسوب",
    minimum: "التحليل السيفالومتري يعطي التصنيف الهيكلي والقياسات المهمة للعرض.",
    doctorAction: "افتح السيفالو وأكمل التحليل أو راجع آخر تحليل محفوظ.",
    icon: Ruler, kind: "data", priority: "critical", tab: "ceph", slideTypes: ["CephalometricMeasurements"],
  },
  {
    order: 10, key: "cast", title: "تحليل النماذج", required: "تحليل النماذج (ALD/Pont)",
    minimum: "وجود تحليل النماذج يمنع شرائح Cast/Space من الخروج فارغة.",
    doctorAction: "افتح تحليل النماذج وأكمل قياسات القوس والمسافات.",
    icon: Microscope, kind: "data", priority: "critical", tab: "cast", slideTypes: ["CastAnalysis"],
  },
  {
    order: 11, key: "bolton", title: "تحليل بولتون", required: "نسب بولتون",
    minimum: "بولتون مهم عند وجود اختلاف حجم الأسنان؛ إن لم يتوفر سيُخفى من العرض النهائي.",
    doctorAction: "أكمل حساب بولتون داخل تحليل النماذج عند الحاجة.",
    icon: Sigma, kind: "data", priority: "supporting", tab: "cast", slideTypes: ["Bolton"],
  },
  {
    order: 12, key: "problems", title: "قائمة المشاكل التشخيصية", required: "مشاكل هيكلية/سنية/أنسجة رخوة/وظيفية",
    minimum: "هذه هي نقطة التحويل من البيانات إلى التفكير السريري؛ لا تعتمد التشخيص بدون قائمة مشاكل.",
    doctorAction: "افتح قائمة المشاكل وأضف كل مشكلة مع مصدر الدليل وشدتها.",
    icon: ListChecks, kind: "data", priority: "critical", tab: "problems", slideTypes: ["ProblemList"], aiDraft: true,
  },
  {
    order: 13, key: "diagnosis", title: "التشخيص", required: "هيكلي، سني، أنسجة رخوة، وظيفي + اعتماد",
    minimum: "التشخيص النهائي يجب أن يكون معتمدًا من الطبيب قبل العرض النهائي.",
    doctorAction: "افتح التشخيص، راجع المسودة، واعتمد التشخيص النهائي.",
    icon: ClipboardList, kind: "data", priority: "critical", tab: "diagnosis", slideTypes: ["Diagnosis"], approvedFlag: "diagnosis", aiDraft: true,
  },
  {
    order: 14, key: "objectives", title: "أهداف العلاج", required: "أهداف العلاج",
    minimum: "الأهداف تربط المشاكل بالخطة؛ يجب أن تظهر بوضوح في العرض.",
    doctorAction: "افتح الخطة وسجّل أهداف العلاج السريرية.",
    icon: Target, kind: "data", priority: "critical", tab: "plan", slideTypes: ["TreatmentObjectives"], aiDraft: true,
  },
  {
    order: 15, key: "strategies", title: "استراتيجيات العلاج", required: "قلع/ارتكاز/توسيع/مطاطات…",
    minimum: "الاستراتيجيات تشرح لماذا اخترت الخطة، وليس فقط ماذا ستعمل.",
    doctorAction: "اكتب الاستراتيجية والمبرر والبدائل قبل الخطة النهائية.",
    icon: GitBranch, kind: "data", priority: "critical", tab: "plan", slideTypes: ["TreatmentPlan"], aiDraft: true,
  },
  {
    order: 16, key: "plan", title: "خطة العلاج", required: "الجهاز، القلع، المدة + اعتماد",
    minimum: "الخطة المعتمدة هي مصدر أهداف وميكانيكا وشرائح العلاج.",
    doctorAction: "افتح الخطة، أكمل الجهاز والقلع والمدة، ثم اعتمدها.",
    icon: FileText, kind: "data", priority: "critical", tab: "plan", slideTypes: ["TreatmentPlan"], approvedFlag: "plan", aiDraft: true,
  },
  {
    order: 17, key: "mechano", title: "الميكانيكا العلاجية", required: "الحاصرات، السلك، الارتكاز",
    minimum: "الميكانيكا تشرح مراحل التنفيذ؛ لا تجعلها شريحة فارغة.",
    doctorAction: "أضف تسلسل الميكانيكا والأسلاك والارتكاز والمطاطات.",
    icon: Wrench, kind: "data", priority: "critical", tab: "plan", slideTypes: ["Mechanotherapy"], aiDraft: true,
  },
  {
    order: 18, key: "visits", title: "الزيارات والتقدّم", required: "زيارة واحدة على الأقل",
    minimum: "الزيارات تحكي قصة العلاج زمنيًا؛ يمكن توليد المسودة بدونها في بداية الحالة.",
    doctorAction: "أضف الزيارات المهمة مع ما تم عمله والصور المرحلية.",
    icon: CalendarClock, kind: "data", priority: "supporting", tab: "visits", slideTypes: ["VisitProgress"],
  },
  {
    order: 19, key: "final", title: "النتائج بعد العلاج", required: "صور نهائية (طور النهاية)",
    minimum: "صور النهاية مطلوبة فقط عند عرض حالة مكتملة أو قبل/بعد.",
    doctorAction: "التقط أو ارفع صور النهاية بعد اكتمال العلاج.",
    icon: Trophy, kind: "photos", priority: "optional", slideTypes: ["FinalRecords"], photoSlots: [
      { key: "ff", label: "أمامي نهائي", category: "Extraoral", subtype: "Frontal", phase: "Final", capture: "user" },
      { key: "fif", label: "داخل الفم أمامي نهائي", category: "Intraoral", subtype: "Frontal", phase: "Final", capture: "environment" },
    ],
  },
  {
    order: 20, key: "retention", title: "مرحلة الاحتفاظ", required: "المثبّتات والتعليمات",
    minimum: "تظهر عند الحالات المنتهية أو عند وجود خطة احتفاظ.",
    doctorAction: "أضف المثبتات والتعليمات عند الوصول لمرحلة الاحتفاظ.",
    icon: Shield, kind: "data", priority: "optional", tab: "retention", slideTypes: ["Retention"],
  },
  {
    order: 21, key: "generate", title: "المعاينة وإنشاء العرض", required: "مراجعة الجاهزية ثم التوليد",
    minimum: "الزر النهائي يمنع الشرائح الناقصة الأساسية؛ المسودة فقط تسمح بالفارغ.",
    doctorAction: "راجع النواقص ثم أنشئ العرض النهائي أو مسودة تدريبية.",
    icon: Presentation, kind: "generate", priority: "critical",
  },
];

const STATUS_META: Record<StepStatus, { label: string; cls: string; icon: typeof CheckCircle2 }> = {
  missing: { label: "ناقص", cls: "bg-gray-100 text-gray-500", icon: CircleDashed },
  partial: { label: "جزئي", cls: "bg-amber-50 text-amber-700", icon: AlertCircle },
  complete: { label: "مكتمل", cls: "bg-green-50 text-green-700", icon: CheckCircle2 },
  approved: { label: "معتمد", cls: "bg-clinic-blue-50 text-clinic-blue", icon: BadgeCheck },
};

const PRIORITY_META: Record<StepPriority, { label: string; cls: string }> = {
  critical: { label: "أساسي للعرض النهائي", cls: "bg-red-50 text-red-700" },
  supporting: { label: "داعم — يُخفى إذا ناقص", cls: "bg-blue-50 text-blue-700" },
  optional: { label: "اختياري حسب مرحلة الحالة", cls: "bg-gray-100 text-gray-600" },
};

const eq = (a?: string | null, b?: string) => (a ?? "").trim().toLowerCase() === (b ?? "").toLowerCase();
const isReady = (status: StepStatus) => status === "complete" || status === "approved";

export function OrthoCaseWizard({ caseId, patientId, onNavigate }:
  { caseId: string; patientId?: string; onNavigate: (tab: Tab) => void }) {
  const [open, setOpen] = useState<string | null>("patient");
  const [busy, setBusy] = useState(false);
  const [prepPhoto, setPrepPhoto] = useState<OrthoPhoto | null>(null);

  const overviewQ = useOrthoOverview(caseId);
  const photosQ = useOrthoPhotos(caseId);
  const defQ = useQuery({
    queryKey: ["ortho-presentation-definition", caseId],
    enabled: !!caseId,
    retry: false,
    queryFn: async () => (await orthoService.getCasePresentationDefinition(caseId)).data,
  });

  const o = (overviewQ.data ?? {}) as Record<string, unknown>;
  const photos = photosQ.data ?? [];
  const hasDataByType = useMemo(() => {
    const map: Record<string, boolean> = {};
    (defQ.data as OrthoPresentationDefinition | undefined)?.slides.forEach((s) => { map[s.type] = s.hasData; });
    return map;
  }, [defQ.data]);

  const photosFor = (slot: PhotoSlot) =>
    photos.find((p) => (eq(p.category, slot.category) || eq(p.photoType, slot.category)) &&
      eq(p.subtype, slot.subtype) && (slot.phase === "Initial" ? !p.treatmentPhase || eq(p.treatmentPhase, "Initial") : eq(p.treatmentPhase, slot.phase)));

  const statusFor = (step: WizardStep): StepStatus => {
    if (step.kind === "generate") return "complete";
    if (step.approvedFlag === "diagnosis" && o.isDiagnosisApproved === true) return "approved";
    if (step.approvedFlag === "plan" && o.isTreatmentPlanApproved === true) return "approved";
    if (step.photoSlots) {
      const filled = step.photoSlots.filter((s) => photosFor(s)).length;
      if (filled === 0) return "missing";
      return filled === step.photoSlots.length ? "complete" : "partial";
    }
    const has = (step.slideTypes ?? []).some((t) => hasDataByType[t]);
    return has ? "complete" : "missing";
  };

  const steps = STEPS.map((s) => ({ step: s, status: statusFor(s) }));
  const nonGenerateSteps = steps.filter((s) => s.step.kind !== "generate");
  const readyCount = nonGenerateSteps.filter((s) => isReady(s.status)).length;
  const totalSteps = nonGenerateSteps.length;
  const criticalMissing = nonGenerateSteps.filter((s) => s.step.priority === "critical" && !isReady(s.status));
  const partialSteps = nonGenerateSteps.filter((s) => s.status === "partial");
  const firstBlocking = criticalMissing[0] ?? partialSteps[0];
  const canGenerateFinal = criticalMissing.length === 0;

  const refresh = () => { photosQ.refetch(); defQ.refetch(); overviewQ.refetch(); };

  const generate = async (includeEmpty: boolean) => {
    if (!includeEmpty && !canGenerateFinal) {
      toast.error("لا يمكن إنشاء عرض نهائي قبل إكمال الخطوات الأساسية الناقصة");
      if (firstBlocking) setOpen(firstBlocking.step.key);
      return;
    }

    setBusy(true);
    try {
      const res = await api.post(`/api/ortho-cases/${caseId}/case-presentation/pptx`,
        { includeEmptyOptionalSlides: includeEmpty }, { responseType: "blob" });
      const url = window.URL.createObjectURL(new Blob([res.data],
        { type: "application/vnd.openxmlformats-officedocument.presentationml.presentation" }));
      const a = document.createElement("a");
      a.href = url;
      a.download = `ortho-case-${caseId}.pptx`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      toast.success(includeEmpty ? "تم إنشاء مسودة العرض" : "تم إنشاء العرض النهائي");
    } catch {
      toast.error("تعذّر إنشاء العرض");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-4" dir="rtl">
      <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <h3 className="flex items-center gap-2 text-base font-bold text-clinic-navy">
              <Presentation className="h-5 w-5 text-clinic-blue" />
              معالج عرض الحالة الموجّه
            </h3>
            <p className="mt-1 max-w-3xl text-xs leading-6 text-gray-500">
              اتبع نفس تسلسل عرض الحالة: بيانات → صور → فحص وتحاليل → قائمة مشاكل → تشخيص → أهداف واستراتيجيات → خطة وميكانيكا → توليد. الهدف أن لا يخرج PowerPoint بشرائح فارغة أو مدخلات ناقصة.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <span className={cn("rounded-full px-3 py-1 text-xs font-bold",
              canGenerateFinal ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700")}>
              {readyCount}/{totalSteps} خطوة جاهزة
            </span>
            {criticalMissing.length > 0 && (
              <button type="button" onClick={() => setOpen(criticalMissing[0].step.key)}
                className="inline-flex items-center gap-1 rounded-full bg-red-50 px-3 py-1 text-xs font-bold text-red-700 hover:bg-red-100">
                <ArrowUpCircle className="h-3.5 w-3.5" />
                أول نقص أساسي: {criticalMissing[0].step.title}
              </button>
            )}
          </div>
        </div>
        <div className="mt-4 h-2 overflow-hidden rounded-full bg-gray-100">
          <div className="h-full rounded-full bg-clinic-blue transition-all" style={{ width: `${(readyCount / totalSteps) * 100}%` }} />
        </div>
        <div className="mt-3 grid gap-2 md:grid-cols-3">
          <SummaryCard label="خطوات أساسية ناقصة" value={criticalMissing.length} tone={criticalMissing.length ? "danger" : "success"} />
          <SummaryCard label="خطوات جزئية" value={partialSteps.length} tone={partialSteps.length ? "warning" : "success"} />
          <SummaryCard label="الجاهزية النهائية" value={canGenerateFinal ? "جاهز" : "غير جاهز"} tone={canGenerateFinal ? "success" : "warning"} />
        </div>
      </div>

      <ol className="space-y-2">
        {steps.map(({ step, status }) => {
          const meta = STATUS_META[status];
          const priority = PRIORITY_META[step.priority];
          const Icon = step.icon;
          const StatusIcon = meta.icon;
          const isOpen = open === step.key;
          const captured = step.photoSlots?.filter((slot) => photosFor(slot)).length ?? 0;
          const totalSlots = step.photoSlots?.length ?? 0;

          return (
            <li key={step.key} className={cn(
              "overflow-hidden rounded-xl border bg-white shadow-sm",
              step.priority === "critical" && !isReady(status) ? "border-red-200" : "border-gray-200",
            )}>
              <button type="button" onClick={() => setOpen(isOpen ? null : step.key)}
                className="flex w-full items-center gap-3 p-3 text-right hover:bg-gray-50">
                <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-clinic-blue-50 text-clinic-blue">
                  <Icon className="h-5 w-5" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-2">
                    <span className="text-[11px] text-gray-400 tabular-nums">{step.order}.</span>
                    <span className="text-sm font-semibold text-gray-800">{step.title}</span>
                    <span className={cn("rounded-full px-2 py-0.5 text-[10px] font-medium", priority.cls)}>{priority.label}</span>
                  </span>
                  <span className="block truncate text-[11px] text-gray-500">{step.required}</span>
                </span>
                {totalSlots > 0 && (
                  <span className="hidden rounded-full bg-gray-100 px-2 py-0.5 text-[10px] text-gray-600 sm:inline">
                    صور {captured}/{totalSlots}
                  </span>
                )}
                <span className={cn("inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium", meta.cls)}>
                  <StatusIcon className="h-3.5 w-3.5" />{meta.label}
                </span>
                <ChevronLeft className={cn("h-4 w-4 text-gray-300 transition", isOpen && "-rotate-90")} />
              </button>

              {isOpen && (
                <div className="border-t border-gray-100 bg-gray-50/50 p-3">
                  <StepGuidance step={step} status={status} />

                  {step.kind === "patient" && (
                    <div className="mt-3 flex flex-wrap gap-2">
                      <Link href={patientId ? `/patients/${patientId}` : "#"}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white hover:opacity-90">
                        <Pencil className="h-3.5 w-3.5" />فتح ملف المريض
                      </Link>
                    </div>
                  )}

                  {step.kind === "data" && step.tab && (
                    <div className="mt-3 flex flex-wrap items-center gap-2">
                      <button type="button" onClick={() => onNavigate(step.tab!)}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white hover:opacity-90">
                        <Pencil className="h-3.5 w-3.5" />فتح شاشة الإدخال المرتبطة
                      </button>
                      {step.aiDraft && (
                        <span className="inline-flex items-center gap-1 rounded-lg border border-dashed border-clinic-blue/40 bg-white px-2.5 py-1.5 text-[11px] text-gray-600">
                          <Sparkles className="h-3.5 w-3.5 text-clinic-blue" />
                          مساعدة الذكاء الاصطناعي — قريبًا، مسودة لا تعتمد إلا بمراجعة الطبيب
                        </span>
                      )}
                    </div>
                  )}

                  {step.kind === "photos" && step.photoSlots && (
                    <div className="mt-3 space-y-2">
                      <div className="rounded-lg border border-blue-100 bg-blue-50 p-2 text-[11px] leading-5 text-blue-800">
                        التقط الصورة من نفس هذه الخطوة حتى تُحفظ تلقائيًا بالفئة والطور الصحيحين. بعد الحفظ اضغط «تجهيز للعرض» للقص والتعديل. الأصل يبقى محفوظًا كما هو.
                      </div>
                      <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                        {step.photoSlots.map((slot) => (
                          <PhotoSlotCard key={slot.key} caseId={caseId} slot={slot}
                            photo={photosFor(slot) ?? null} disabled={busy}
                            onChanged={refresh} onPrepare={(p) => setPrepPhoto(p)} />
                        ))}
                      </div>
                    </div>
                  )}

                  {step.kind === "generate" && (
                    <div className="mt-3 space-y-3">
                      <div className={cn("rounded-lg border p-3", canGenerateFinal ? "border-green-200 bg-green-50" : "border-amber-200 bg-amber-50")}>
                        <p className="text-xs font-semibold text-gray-700">
                          الجاهزية النهائية: {readyCount}/{totalSteps}
                        </p>
                        {criticalMissing.length > 0 ? (
                          <ul className="mt-2 grid gap-1 sm:grid-cols-2">
                            {criticalMissing.map((s) => (
                              <li key={s.step.key} className="flex items-center gap-1.5 text-[11px] text-amber-800">
                                <AlertCircle className="h-3.5 w-3.5" />
                                ناقص أساسي: {s.step.title}
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p className="mt-1 text-[11px] text-green-800">كل الخطوات الأساسية مكتملة. يمكن إنشاء عرض نهائي بدون شرائح فارغة.</p>
                        )}
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <button type="button" onClick={() => generate(false)} disabled={busy || !canGenerateFinal}
                          className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-bold text-white hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50">
                          {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Presentation className="h-3.5 w-3.5" />}
                          إنشاء عرض نهائي بلا شرائح فارغة
                        </button>
                        <button type="button" onClick={() => generate(true)} disabled={busy}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50">
                          مسودة تعليمية تشمل الفارغة
                        </button>
                        {!canGenerateFinal && (
                          <span className="self-center text-[11px] text-amber-700">العرض النهائي مقفل حتى تكتمل الخطوات الأساسية.</span>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </li>
          );
        })}
      </ol>

      <OrthoImagePreparationDialog caseId={caseId} photo={prepPhoto} open={prepPhoto !== null}
        onClose={() => setPrepPhoto(null)} onSaved={() => { setPrepPhoto(null); refresh(); }} />
    </div>
  );
}

function SummaryCard({ label, value, tone }: { label: string; value: string | number; tone: "success" | "warning" | "danger" }) {
  const cls = tone === "success"
    ? "border-green-100 bg-green-50 text-green-800"
    : tone === "danger"
      ? "border-red-100 bg-red-50 text-red-800"
      : "border-amber-100 bg-amber-50 text-amber-800";
  return (
    <div className={cn("rounded-lg border p-2", cls)}>
      <div className="text-[11px] opacity-80">{label}</div>
      <div className="text-sm font-bold">{value}</div>
    </div>
  );
}

function StepGuidance({ step, status }: { step: WizardStep; status: StepStatus }) {
  const statusLabel = STATUS_META[status].label;
  return (
    <div className="grid gap-2 lg:grid-cols-3">
      <div className="rounded-lg border border-gray-200 bg-white p-2">
        <div className="text-[11px] font-semibold text-gray-500">المطلوب الآن</div>
        <div className="mt-1 text-xs leading-5 text-gray-800">{step.minimum}</div>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-2">
        <div className="text-[11px] font-semibold text-gray-500">إجراء الطبيب</div>
        <div className="mt-1 text-xs leading-5 text-gray-800">{step.doctorAction}</div>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-2">
        <div className="text-[11px] font-semibold text-gray-500">الحالة الحالية</div>
        <div className="mt-1 text-xs leading-5 text-gray-800">
          {statusLabel}{step.priority === "critical" && !isReady(status) ? " — تمنع العرض النهائي" : ""}
        </div>
      </div>
    </div>
  );
}

function PhotoSlotCard({ caseId, slot, photo, disabled, onChanged, onPrepare }:
  { caseId: string; slot: PhotoSlot; photo: OrthoPhoto | null; disabled: boolean;
    onChanged: () => void; onPrepare: (p: OrthoPhoto) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const preparedUrl = typeof (photo as Record<string, unknown> | null)?.preparedImageUrl === "string"
    ? (photo as unknown as { preparedImageUrl?: string }).preparedImageUrl
    : undefined;

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const { data } = await api.post<{ url: string }>("/api/uploads", form,
        { headers: { "Content-Type": "multipart/form-data" } });
      await orthoService.addPhoto(caseId, {
        photoUrl: data.url,
        category: slot.category,
        subtype: slot.subtype,
        photoType: slot.category,
        treatmentPhase: slot.phase,
        isSelectedForReport: true,
        caption: slot.label,
      } as Partial<OrthoPhoto>);
      toast.success("تم حفظ الصورة في مكانها الصحيح");
      onChanged();
    } catch {
      toast.error("تعذّر رفع الصورة");
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-2.5">
      <div className="flex items-center justify-between gap-2">
        <div>
          <span className="block text-xs font-semibold text-gray-700">{slot.label}</span>
          <span className="text-[10px] text-gray-400">{slot.phase === "Final" ? "صورة نهائية" : "صورة بداية"}</span>
        </div>
        {photo
          ? <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-1.5 py-0.5 text-[10px] text-green-700"><CheckCircle2 className="h-3 w-3" />محفوظة</span>
          : <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-500">مطلوبة</span>}
      </div>

      {photo ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={resolveImageUrl(preparedUrl ?? photo.photoUrl)} alt={slot.label}
          className="mt-2 h-28 w-full rounded-md object-cover" />
      ) : (
        <div className="mt-2 grid h-28 place-items-center rounded-md border border-dashed border-gray-200 bg-gray-50 text-[11px] text-gray-400">
          لم تُلتقط بعد
        </div>
      )}

      <input ref={inputRef} type="file" accept="image/*" capture={slot.capture} className="hidden"
        onChange={(e) => onFile(e.target.files?.[0])} />

      <div className="mt-2 flex flex-wrap gap-1.5">
        <button type="button" disabled={disabled || uploading} onClick={() => inputRef.current?.click()}
          className="inline-flex items-center gap-1 rounded-md bg-clinic-blue px-2.5 py-1 text-[11px] font-medium text-white hover:opacity-90 disabled:opacity-50">
          {uploading ? <Loader2 className="h-3 w-3 animate-spin" /> : <Camera className="h-3 w-3" />}
          {photo ? "إعادة الالتقاط" : "التقاط / رفع"}
        </button>
        {photo && (
          <button type="button" onClick={() => onPrepare(photo)}
            className="inline-flex items-center gap-1 rounded-md border border-gray-200 px-2.5 py-1 text-[11px] font-medium text-gray-600 hover:bg-gray-50">
            <Upload className="h-3 w-3" />قص وتجهيز
          </button>
        )}
        {preparedUrl && (
          <span className="inline-flex items-center gap-1 rounded-md bg-blue-50 px-2.5 py-1 text-[11px] text-blue-700">
            <BadgeCheck className="h-3 w-3" />جاهزة للعرض
          </span>
        )}
      </div>
    </div>
  );
}
