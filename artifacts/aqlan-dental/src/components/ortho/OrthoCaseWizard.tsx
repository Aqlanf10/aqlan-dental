
import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "@/lib/nextLinkCompat";
import {
  AlertCircle,
  ArrowUpCircle,
  BadgeCheck,
  Camera,
  CheckCircle2,
  ChevronLeft,
  CircleDashed,
  Clipboard,
  FileText,
  GitBranch,
  Image as ImageIcon,
  ListChecks,
  Loader2,
  MessageSquare,
  Microscope,
  Pencil,
  Presentation,
  Ruler,
  Scan,
  Shield,
  Sigma,
  Smile,
  Sparkles,
  Stethoscope,
  Target,
  Trophy,
  Upload,
  User,
  Wrench,
  CalendarClock,
  ExternalLink,
} from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { cn } from "@/lib/utils";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { useOrthoOverview, useOrthoPhotos, useCaseCephAnalyses, useCaseCephAnalysis } from "@/hooks/useOrtho";
import { orthoService, type OrthoPresentationDefinition } from "@/services/orthoService";
import { toast } from "@/stores/toastStore";
import type { OrthoPhoto } from "@/types/ortho";
import { CephReadinessBadge } from "@/components/ceph/CephReadinessBadge";
import { cephReadinessFromAnalysis } from "@/lib/cephReadiness";
import { pickLatestCeph } from "@/lib/cephSelection";
import { OrthoCaseDraftAiButton } from "./OrthoCaseDraftAiButton";
import { OrthoImagePreparationDialog } from "./OrthoImagePreparationDialog";

type StepStatus = "missing" | "partial" | "complete" | "approved";
type StepPriority = "critical" | "supporting" | "optional";
type DraftKind = "problems" | "diagnosis" | "objectives" | "strategies" | "plan" | "mechano";
type Tab = "overview" | "exam" | "cast" | "ceph" | "facial" | "problems" | "diagnosis" | "plan" | "stages" | "visits" | "retention" | "records" | "reports";

interface PhotoSlot { key: string; label: string; category: string; subtype: string; phase: string; capture: "environment" | "user"; }
interface WizardStep { order: number; key: string; title: string; required: string; minimum: string; doctorAction: string; icon: typeof User; kind: "patient" | "data" | "photos" | "generate"; priority: StepPriority; tab?: Tab; slideTypes?: string[]; approvedFlag?: "diagnosis" | "plan"; photoSlots?: PhotoSlot[]; draftKind?: DraftKind; }

const slot = (key: string, label: string, category: string, subtype: string, phase = "Initial", capture: "environment" | "user" = "environment"): PhotoSlot => ({ key, label, category, subtype, phase, capture });
const s = (order: number, key: string, title: string, required: string, minimum: string, doctorAction: string, icon: typeof User, kind: WizardStep["kind"], priority: StepPriority, tab?: Tab, slideTypes?: string[]): WizardStep => ({ order, key, title, required, minimum, doctorAction, icon, kind, priority, tab, slideTypes });

const STEPS: WizardStep[] = [
  s(1, "patient", "بيانات المريض", "الاسم، رقم الملف، العمر، الجنس", "تأكد أن بيانات المريض صحيحة.", "افتح ملف المريض وعدّل البيانات الأساسية عند الحاجة.", User, "patient", "critical", undefined, ["PatientInformation"]),
  s(2, "complaint", "المقابلة والشكوى", "الشكوى الرئيسية والتاريخ", "أدخل الشكوى الرئيسية والتاريخ المختصر.", "افتح الفحص السريري.", MessageSquare, "data", "critical", "exam", ["ChiefComplaint"]),
  { ...s(3, "extraoral", "الصور خارج الفم", "أمامي، جانبي، ابتسامة", "التقط الصور الأساسية.", "التقط ثم جهز الصور للعرض.", Camera, "photos", "critical", undefined, ["ExtraoralPhotos"]), photoSlots: [slot("exf", "أمامي", "Extraoral", "Frontal", "Initial", "user"), slot("exp", "جانبي", "Extraoral", "Profile", "Initial", "user"), slot("exs", "ابتسامة", "Extraoral", "Smile", "Initial", "user")] },
  s(4, "facial", "التحليل الوجهي", "تحليل الوجه والأنسجة الرخوة", "أدخل التحليل عند توفره.", "افتح تحليل الصور أو أضف الملاحظات.", Smile, "data", "supporting", "facial", ["FacialAnalysis"]),
  { ...s(5, "intraoral", "الصور داخل الفم", "أمامي، يمين، يسار، علوي، سفلي", "التقط خمس صور داخل الفم.", "استخدم الكاميرا الخلفية ثم جهز الصور.", Scan, "photos", "critical", undefined, ["IntraoralPhotos"]), photoSlots: [slot("inf", "أمامي", "Intraoral", "Frontal"), slot("inr", "يمين", "Intraoral", "Right"), slot("inl", "يسار", "Intraoral", "Left"), slot("inu", "علوي", "Intraoral", "Upper Occlusal"), slot("ind", "سفلي", "Intraoral", "Lower Occlusal")] },
  s(6, "occlusion", "تقييم الإطباق", "العلاقات، OJ/OB، الازدحام", "سجل علاقات الإطباق الأساسية.", "افتح الفحص السريري.", Stethoscope, "data", "critical", "exam", ["OcclusionAssessment"]),
  { ...s(7, "pano", "الأشعة البانورامية", "OPG", "ارفع البانوراما عند توفرها.", "ارفع الصورة أو التقطها من شاشة الأشعة.", ImageIcon, "photos", "supporting", undefined, ["PanoramicXray"]), photoSlots: [slot("opg", "بانوراما", "Radiograph", "OPG")] },
  { ...s(8, "cephxray", "الأشعة السيفالومترية", "سيفالو جانبي", "أضف السيفالو أو التحليل.", "افتح وحدة السيفالو.", ImageIcon, "photos", "supporting", undefined, ["CephalometricSummary"]), photoSlots: [slot("ceph", "سيفالو", "Radiograph", "Lateral Ceph")] },
  s(9, "cephmeas", "قياسات السيفالو", "تحليل محفوظ", "أكمل قياسات السيفالو.", "افتح السيفالو.", Ruler, "data", "critical", "ceph", ["CephalometricMeasurements"]),
  s(10, "cast", "تحليل النماذج", "ALD/Pont", "أكمل تحليل النماذج.", "افتح تحليل النماذج.", Microscope, "data", "critical", "cast", ["CastAnalysis"]),
  s(11, "bolton", "تحليل بولتون", "نسب بولتون", "أكمل بولتون عند الحاجة.", "افتح تحليل النماذج.", Sigma, "data", "supporting", "cast", ["Bolton"]),
  { ...s(12, "problems", "قائمة المشاكل", "مشاكل هيكلية/سنية/وظيفية", "اكتب قائمة المشاكل.", "اكتب المسودة ثم انسخها للمدخل الرسمي.", ListChecks, "data", "critical", "problems", ["ProblemList"]), draftKind: "problems" },
  { ...s(13, "diagnosis", "التشخيص", "هيكلي، سني، رخوة، وظيفي", "راجع التشخيص واعتمده.", "اكتب المسودة ثم انسخها لشاشة التشخيص.", Clipboard, "data", "critical", "diagnosis", ["Diagnosis"]), approvedFlag: "diagnosis", draftKind: "diagnosis" },
  { ...s(14, "objectives", "أهداف العلاج", "أهداف علاجية", "اربط الأهداف بالمشاكل.", "اكتب الأهداف العلاجية.", Target, "data", "critical", "plan", ["TreatmentObjectives"]), draftKind: "objectives" },
  { ...s(15, "strategies", "استراتيجيات العلاج", "قلع/ارتكاز/توسيع", "اكتب الاستراتيجية والمبرر.", "انسخها للخطة الرسمية بعد المراجعة.", GitBranch, "data", "critical", "plan", ["TreatmentPlan"]), draftKind: "strategies" },
  { ...s(16, "plan", "خطة العلاج", "الجهاز، القلع، المدة", "اكتب الخطة واعتمدها رسميًا.", "افتح شاشة الخطة.", FileText, "data", "critical", "plan", ["TreatmentPlan"]), approvedFlag: "plan", draftKind: "plan" },
  { ...s(17, "mechano", "الميكانيكا", "الحاصرات، الأسلاك، الارتكاز", "اكتب مراحل التنفيذ.", "انسخها للخطة الرسمية بعد المراجعة.", Wrench, "data", "critical", "plan", ["Mechanotherapy"]), draftKind: "mechano" },
  s(18, "visits", "الزيارات والتقدم", "زيارات العلاج", "أضف الزيارات المهمة.", "افتح تبويب الزيارات.", CalendarClock, "data", "supporting", "visits", ["VisitProgress"]),
  { ...s(19, "final", "النتائج النهائية", "صور نهائية", "تستخدم عند اكتمال الحالة.", "التقط صور النهاية.", Trophy, "photos", "optional", undefined, ["FinalRecords"]), photoSlots: [slot("ff", "أمامي نهائي", "Extraoral", "Frontal", "Final", "user"), slot("fif", "داخل الفم نهائي", "Intraoral", "Frontal", "Final")] },
  s(20, "retention", "الاحتفاظ", "المثبتات والتعليمات", "أضف خطة الاحتفاظ عند الحاجة.", "افتح تبويب الاحتفاظ.", Shield, "data", "optional", "retention", ["Retention"]),
  s(21, "generate", "إنشاء العرض", "مراجعة الجاهزية", "راجع النواقص ثم أنشئ العرض.", "أنشئ العرض النهائي أو المسودة.", Presentation, "generate", "critical"),
];

const STATUS_META: Record<StepStatus, { label: string; cls: string; icon: typeof CheckCircle2 }> = {
  missing: { label: "ناقص", cls: "bg-gray-100 text-gray-500", icon: CircleDashed },
  partial: { label: "جزئي", cls: "bg-amber-50 text-amber-700", icon: AlertCircle },
  complete: { label: "مكتمل", cls: "bg-green-50 text-green-700", icon: CheckCircle2 },
  approved: { label: "معتمد", cls: "bg-clinic-blue-50 text-clinic-blue", icon: BadgeCheck },
};
const PRIORITY_META: Record<StepPriority, { label: string; cls: string }> = {
  critical: { label: "أساسي", cls: "bg-red-50 text-red-700" },
  supporting: { label: "داعم", cls: "bg-blue-50 text-blue-700" },
  optional: { label: "اختياري", cls: "bg-gray-100 text-gray-600" },
};
const eq = (a?: string | null, b?: string) => (a ?? "").trim().toLowerCase() === (b ?? "").toLowerCase();
const isReady = (status: StepStatus) => status === "complete" || status === "approved";

export function OrthoCaseWizard({ caseId, patientId, onNavigate }: { caseId: string; patientId?: string; onNavigate: (tab: Tab) => void }) {
  const [open, setOpen] = useState<string | null>("patient");
  const [busy, setBusy] = useState(false);
  const [prepPhoto, setPrepPhoto] = useState<OrthoPhoto | null>(null);
  const overviewQ = useOrthoOverview(caseId);
  const photosQ = useOrthoPhotos(caseId);
  const defQ = useQuery({ queryKey: ["ortho-presentation-definition", caseId], enabled: !!caseId, retry: false, queryFn: async () => (await orthoService.getCasePresentationDefinition(caseId)).data });
  const overview = (overviewQ.data ?? {}) as Record<string, unknown>;
  const photos = photosQ.data ?? [];
  // Cephalometric state & quality. Select the SAME analysis the deck generator
  // renders (analysisDate DESC, then createdAt DESC) so wizard readiness matches
  // the generated deck, then read its readiness via the shared gate.
  const cephQ = useCaseCephAnalyses(caseId);
  const latestCeph = pickLatestCeph(cephQ.data);
  const cephDetailQ = useCaseCephAnalysis(latestCeph?.id);
  const cephReadiness = cephDetailQ.data ? cephReadinessFromAnalysis(cephDetailQ.data, false) : null;
  // Image-preparation quality of the photos that will actually appear in the deck.
  const selectedPhotos = photos.filter((photo) => photo.isSelectedForReport);
  const preparedSelected = selectedPhotos.filter((photo) => photo.isPreparedForReport);
  const photosNeedPrep = selectedPhotos.length > 0 && preparedSelected.length < selectedPhotos.length;
  const hasDataByType = useMemo(() => {
    const map: Record<string, boolean> = {};
    (defQ.data as OrthoPresentationDefinition | undefined)?.slides.forEach((slide) => { map[slide.type] = slide.hasData; });
    return map;
  }, [defQ.data]);

  const photosFor = (slotValue: PhotoSlot) => photos.find((photo) => (eq(photo.category, slotValue.category) || eq(photo.photoType, slotValue.category)) && eq(photo.subtype, slotValue.subtype) && (slotValue.phase === "Initial" ? !photo.treatmentPhase || eq(photo.treatmentPhase, "Initial") : eq(photo.treatmentPhase, slotValue.phase)));
  const statusFor = (step: WizardStep): StepStatus => {
    if (step.kind === "generate") return "complete";
    if (step.approvedFlag === "diagnosis" && overview.isDiagnosisApproved === true) return "approved";
    if (step.approvedFlag === "plan" && overview.isTreatmentPlanApproved === true) return "approved";
    if (step.photoSlots) {
      const filled = step.photoSlots.filter((photoSlot) => photosFor(photoSlot)).length;
      if (filled === 0) return "missing";
      return filled === step.photoSlots.length ? "complete" : "partial";
    }
    // Ceph measurements: reflect the real analysis readiness, not just slide data.
    if (step.key === "cephmeas") {
      if (!latestCeph) return "missing";
      return cephReadiness && cephReadiness.ready ? "complete" : "partial";
    }
    return (step.slideTypes ?? []).some((type) => hasDataByType[type]) ? "complete" : "missing";
  };
  const steps = STEPS.map((step) => ({ step, status: statusFor(step) }));
  const workingSteps = steps.filter((item) => item.step.kind !== "generate");
  const readyCount = workingSteps.filter((item) => isReady(item.status)).length;
  const criticalMissing = workingSteps.filter((item) => item.step.priority === "critical" && !isReady(item.status));
  const partialSteps = workingSteps.filter((item) => item.status === "partial");
  const firstBlocking = criticalMissing[0] ?? partialSteps[0];
  const canGenerateFinal = criticalMissing.length === 0;
  const refresh = () => { photosQ.refetch(); defQ.refetch(); overviewQ.refetch(); };

  const generate = async (includeEmpty: boolean) => {
    if (!includeEmpty && !canGenerateFinal) { toast.error("أكمل الخطوات الأساسية أولًا"); if (firstBlocking) setOpen(firstBlocking.step.key); return; }
    setBusy(true);
    try {
      const res = await api.post(`/api/ortho-cases/${caseId}/case-presentation/pptx`, { includeEmptyOptionalSlides: includeEmpty }, { responseType: "blob" });
      const url = window.URL.createObjectURL(new Blob([res.data], { type: "application/vnd.openxmlformats-officedocument.presentationml.presentation" }));
      const a = document.createElement("a"); a.href = url; a.download = `ortho-case-${caseId}.pptx`; document.body.appendChild(a); a.click(); a.remove(); window.URL.revokeObjectURL(url);
      toast.success(includeEmpty ? "تم إنشاء مسودة العرض" : "تم إنشاء العرض النهائي");
    } catch { toast.error("تعذر إنشاء العرض"); } finally { setBusy(false); }
  };

  return <div className="space-y-4">
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div><h3 className="flex items-center gap-2 text-base font-bold text-clinic-navy"><Presentation className="h-5 w-5 text-clinic-blue" />معالج عرض الحالة</h3><p className="mt-1 max-w-3xl text-xs leading-6 text-gray-500">تسلسل منظم لإدخال بيانات الحالة والصور والتحاليل والمسودات السريرية.</p></div>
        <div className="flex flex-wrap gap-2"><span className={cn("rounded-full px-3 py-1 text-xs font-bold", canGenerateFinal ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700")}>{readyCount}/{workingSteps.length} جاهزة</span>{criticalMissing.length > 0 && <button type="button" onClick={() => setOpen(criticalMissing[0].step.key)} className="inline-flex items-center gap-1 rounded-full bg-red-50 px-3 py-1 text-xs font-bold text-red-700"><ArrowUpCircle className="h-3.5 w-3.5" />{criticalMissing[0].step.title}</button>}</div>
      </div>
    </div>

    <ol className="space-y-2">{steps.map(({ step, status }) => {
      const meta = STATUS_META[status]; const priority = PRIORITY_META[step.priority]; const Icon = step.icon; const StatusIcon = meta.icon; const isOpen = open === step.key; const captured = step.photoSlots?.filter((photoSlot) => photosFor(photoSlot)).length ?? 0; const totalSlots = step.photoSlots?.length ?? 0;
      return <li key={step.key} className={cn("overflow-hidden rounded-xl border bg-white shadow-sm", step.priority === "critical" && !isReady(status) ? "border-red-200" : "border-gray-200")}>
        <button type="button" onClick={() => setOpen(isOpen ? null : step.key)} className="flex w-full items-center gap-3 p-3 text-right hover:bg-gray-50"><span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-clinic-blue-50 text-clinic-blue"><Icon className="h-5 w-5" /></span><span className="min-w-0 flex-1"><span className="flex flex-wrap items-center gap-2"><span className="text-[11px] text-gray-400 tabular-nums">{step.order}.</span><span className="text-sm font-semibold text-gray-800">{step.title}</span><span className={cn("rounded-full px-2 py-0.5 text-[10px] font-medium", priority.cls)}>{priority.label}</span></span><span className="block truncate text-[11px] text-gray-500">{step.required}</span></span>{totalSlots > 0 && <span className="hidden rounded-full bg-gray-100 px-2 py-0.5 text-[10px] text-gray-600 sm:inline">صور {captured}/{totalSlots}</span>}<span className={cn("inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium", meta.cls)}><StatusIcon className="h-3.5 w-3.5" />{meta.label}</span><ChevronLeft className={cn("h-4 w-4 text-gray-300 transition", isOpen && "-rotate-90")} /></button>
        {isOpen && <div className="border-t border-gray-100 bg-gray-50/50 p-3"><StepGuidance step={step} status={status} />
          {step.kind === "patient" && <div className="mt-3"><Link href={patientId ? `/patients/${patientId}` : "#"} className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white"><Pencil className="h-3.5 w-3.5" />فتح ملف المريض</Link></div>}
          {step.kind === "data" && step.tab && <div className="mt-3 space-y-3">{step.draftKind && <ClinicalDraftPanel caseId={caseId} step={step} />}<div className="flex flex-wrap items-center gap-2"><button type="button" onClick={() => onNavigate(step.tab!)} className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white"><Pencil className="h-3.5 w-3.5" />فتح شاشة الإدخال</button>{step.draftKind && <span className="inline-flex items-center gap-1 rounded-lg border border-dashed border-clinic-blue/40 bg-white px-2.5 py-1.5 text-[11px] text-gray-600"><Sparkles className="h-3.5 w-3.5 text-clinic-blue" />مسودة فقط ولا تعتمد تلقائيًا</span>}</div></div>}
          {step.key === "cephmeas" && <div className="mt-3 space-y-2">{!latestCeph ? <p className="text-[11px] text-amber-700">لا يوجد تحليل سيفالومتري محسوب لهذه الحالة — أنشئ تحليلًا واحفظ نقاطه من تبويب السيفالو.</p> : <>{cephReadiness && <CephReadinessBadge readiness={cephReadiness} variant="bar" />}<Link href={`/ceph/${latestCeph.id}`} className="inline-flex items-center gap-1.5 rounded-lg border border-clinic-blue/30 bg-clinic-blue/5 px-3 py-1.5 text-[11px] font-medium text-clinic-blue hover:bg-clinic-blue/10"><ExternalLink className="h-3.5 w-3.5" />فتح تحليل السيفالو</Link></>}</div>}
          {step.kind === "photos" && step.photoSlots && <div className="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-3">{step.photoSlots.map((photoSlot) => <PhotoSlotCard key={photoSlot.key} caseId={caseId} slot={photoSlot} photo={photosFor(photoSlot) ?? null} disabled={busy} onChanged={refresh} onPrepare={(photo) => setPrepPhoto(photo)} />)}</div>}
          {step.kind === "generate" && <div className="mt-3 space-y-3">{((latestCeph && cephReadiness && !cephReadiness.ready) || photosNeedPrep) ? <div className="space-y-1.5 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2"><p className="flex items-center gap-1.5 text-xs font-semibold text-amber-800"><AlertCircle className="h-3.5 w-3.5" />فحص الجودة قبل التوليد</p>{latestCeph && cephReadiness && !cephReadiness.ready && <p className="text-[11px] text-amber-700">تحليل السيفالو غير مكتمل ({cephReadiness.reason}) — قد تظهر شريحة القياسات ناقصة. <Link href={`/ceph/${latestCeph.id}`} className="font-medium text-clinic-blue hover:underline">فتح التحليل لإكماله</Link></p>}{photosNeedPrep && <p className="text-[11px] text-amber-700">بعض الصور المختارة غير مُجهّزة للعرض ({preparedSelected.length}/{selectedPhotos.length}) — جهّزها من خطوات الصور لتظهر دون قص أو تشويه.</p>}</div> : (selectedPhotos.length > 0 && latestCeph) ? <p className="flex items-center gap-1.5 text-[11px] text-green-700"><CheckCircle2 className="h-3.5 w-3.5" />السيفالو جاهز والصور المختارة مُجهّزة للعرض.</p> : null}<div className="flex flex-wrap gap-2"><button type="button" onClick={() => generate(false)} disabled={busy || !canGenerateFinal} className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-bold text-white disabled:opacity-50">{busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Presentation className="h-3.5 w-3.5" />}إنشاء عرض نهائي</button><button type="button" onClick={() => generate(true)} disabled={busy} className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs">مسودة تعليمية</button></div></div>}
        </div>}
      </li>;
    })}</ol>
    <OrthoImagePreparationDialog caseId={caseId} photo={prepPhoto} open={prepPhoto !== null} onClose={() => setPrepPhoto(null)} onSaved={() => { setPrepPhoto(null); refresh(); }} />
  </div>;
}

function StepGuidance({ step, status }: { step: WizardStep; status: StepStatus }) { const statusLabel = STATUS_META[status].label; return <div className="grid gap-2 lg:grid-cols-3"><div className="rounded-lg border border-gray-200 bg-white p-2"><div className="text-[11px] font-semibold text-gray-500">المطلوب الآن</div><div className="mt-1 text-xs leading-5 text-gray-800">{step.minimum}</div></div><div className="rounded-lg border border-gray-200 bg-white p-2"><div className="text-[11px] font-semibold text-gray-500">إجراء الطبيب</div><div className="mt-1 text-xs leading-5 text-gray-800">{step.doctorAction}</div></div><div className="rounded-lg border border-gray-200 bg-white p-2"><div className="text-[11px] font-semibold text-gray-500">الحالة الحالية</div><div className="mt-1 text-xs leading-5 text-gray-800">{statusLabel}{step.priority === "critical" && !isReady(status) ? " — تمنع العرض النهائي" : ""}</div></div></div>; }

function ClinicalDraftPanel({ caseId, step }: { caseId: string; step: WizardStep }) {
  const storageKey = `ortho-wizard-draft:${caseId}:${step.key}`;
  const template = draftTemplate(step.draftKind);
  const [draft, setDraft] = useState(template);
  useEffect(() => { const saved = window.localStorage.getItem(storageKey); if (saved) setDraft(saved); }, [storageKey]);
  useEffect(() => { window.localStorage.setItem(storageKey, draft); }, [draft, storageKey]);
  const copy = async () => { await navigator.clipboard.writeText(draft); toast.success("تم نسخ المسودة"); };
  return <div className="rounded-xl border border-clinic-blue/20 bg-white p-3 shadow-sm"><div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"><div><div className="flex items-center gap-1.5 text-xs font-bold text-clinic-navy"><Clipboard className="h-3.5 w-3.5 text-clinic-blue" />مسودة سريرية</div><p className="mt-1 text-[11px] leading-5 text-gray-500">يمكن طلب مسودة من بيانات الحالة كاملة. الناتج لا يُحفظ ولا يُعتمد تلقائيًا.</p></div><div className="flex flex-wrap gap-1.5"><OrthoCaseDraftAiButton caseId={caseId} draftKind={step.draftKind} currentDraft={draft} template={template} onDraft={setDraft} /><button type="button" onClick={copy} className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-2.5 py-1.5 text-[11px] font-medium text-gray-700"><Clipboard className="h-3.5 w-3.5" />نسخ</button></div></div><textarea value={draft} onChange={(event) => setDraft(event.target.value)} rows={8} className="mt-3 w-full rounded-lg border border-gray-200 bg-gray-50 p-3 text-xs leading-6 text-gray-800 outline-none focus:border-clinic-blue focus:bg-white" /><div className="mt-2 rounded-lg bg-amber-50 p-2 text-[11px] leading-5 text-amber-800">راجع المسودة طبيًا قبل نسخها إلى الشاشة الرسمية.</div></div>;
}

// ORTHO-TASK-003: section labels must be Arabic — the draft feeds an Arabic RTL
// clinical field (same rule as OrthoCaseDraftAiButton's composed labels).
function draftTemplate(kind?: DraftKind) {
  switch (kind) {
    case "problems": return "المشاكل الهيكلية:\n- \n\nالمشاكل السنية:\n- \n\nمشاكل الأنسجة الرخوة:\n- \n\nالمشاكل الوظيفية:\n- ";
    case "diagnosis": return "التشخيص الهيكلي:\n- \n\nالتشخيص السني:\n- \n\nتشخيص الأنسجة الرخوة:\n- \n\nالتشخيص الوظيفي:\n- ";
    case "objectives": return "أهداف العلاج:\n1. \n2. \n3. ";
    case "strategies": return "استراتيجيات العلاج:\n\nالمشكلة:\nالاستراتيجية:\nالمسوّغ:\nالبديل:";
    case "plan": return "خطة العلاج:\n1. الجهاز:\n2. قرار الخلع:\n3. الارتكاز:\n4. المدة:\n5. الاحتفاظ:";
    case "mechano": return "الميكانيكا العلاجية:\n\nالرصف:\n- \n\nالتصحيح:\n- \n\nالإنهاء:\n- ";
    default: return "مسودة سريرية:\n- ";
  }
}

function PhotoSlotCard({ caseId, slot: photoSlot, photo, disabled, onChanged, onPrepare }: { caseId: string; slot: PhotoSlot; photo: OrthoPhoto | null; disabled: boolean; onChanged: () => void; onPrepare: (p: OrthoPhoto) => void }) {
  const inputRef = useRef<HTMLInputElement>(null); const [uploading, setUploading] = useState(false); const preparedUrl = typeof (photo as Record<string, unknown> | null)?.preparedImageUrl === "string" ? (photo as unknown as { preparedImageUrl?: string }).preparedImageUrl : undefined;
  const onFile = async (file: File | undefined) => { if (!file) return; setUploading(true); try { const form = new FormData(); form.append("file", file); const { data } = await api.post<{ url: string }>("/api/uploads", form, { headers: { "Content-Type": "multipart/form-data" } }); await orthoService.addPhoto(caseId, { photoUrl: data.url, category: photoSlot.category, subtype: photoSlot.subtype, photoType: photoSlot.category, treatmentPhase: photoSlot.phase, isSelectedForReport: true, caption: photoSlot.label } as Partial<OrthoPhoto>); toast.success("تم حفظ الصورة"); onChanged(); } catch (err) { toast.error(extractErrorMessage(err, "تعذر رفع الصورة")); } finally { setUploading(false); if (inputRef.current) inputRef.current.value = ""; } };
  return <div className="rounded-lg border border-gray-200 bg-white p-2.5"><div className="flex items-center justify-between gap-2"><div><span className="block text-xs font-semibold text-gray-700">{photoSlot.label}</span><span className="text-[10px] text-gray-400">{photoSlot.phase === "Final" ? "نهائية" : "بداية"}</span></div>{photo ? <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-1.5 py-0.5 text-[10px] text-green-700"><CheckCircle2 className="h-3 w-3" />محفوظة</span> : <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-500">مطلوبة</span>}</div>{photo ? <img src={resolveImageUrl(preparedUrl ?? photo.photoUrl)} alt={photoSlot.label} className="mt-2 h-28 w-full rounded-md object-cover" /> : <div className="mt-2 grid h-28 place-items-center rounded-md border border-dashed border-gray-200 bg-gray-50 text-[11px] text-gray-400">لم تُلتقط بعد</div>}<input ref={inputRef} type="file" accept="image/*" capture={photoSlot.capture} className="hidden" onChange={(event) => onFile(event.target.files?.[0])} /><div className="mt-2 flex flex-wrap gap-1.5"><button type="button" disabled={disabled || uploading} onClick={() => inputRef.current?.click()} className="rounded-md bg-clinic-blue px-2.5 py-1 text-[11px] font-medium text-white disabled:opacity-50">{uploading ? "..." : photo ? "إعادة" : "التقاط / رفع"}</button>{photo && <button type="button" onClick={() => onPrepare(photo)} className="inline-flex items-center gap-1 rounded-md border border-gray-200 px-2.5 py-1 text-[11px] font-medium text-gray-600"><Upload className="h-3 w-3" />قص وتجهيز</button>}{preparedUrl && <span className="inline-flex items-center gap-1 rounded-md bg-blue-50 px-2.5 py-1 text-[11px] text-blue-700"><BadgeCheck className="h-3 w-3" />جاهزة</span>}</div></div>;
}
