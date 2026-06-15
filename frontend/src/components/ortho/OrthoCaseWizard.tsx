"use client";

import { useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import {
  User, MessageSquare, Camera, Scan, Smile, Stethoscope, Image as ImageIcon,
  Ruler, Microscope, Sigma, ListChecks, ClipboardList, Target, GitBranch,
  FileText, Wrench, CalendarClock, Trophy, Shield, Presentation,
  CheckCircle2, CircleDashed, AlertCircle, BadgeCheck, ChevronLeft, Loader2,
  Upload, Pencil, Sparkles,
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
  icon: typeof User;
  kind: "patient" | "data" | "photos" | "generate";
  tab?: Tab;
  slideTypes?: string[];      // definition slide types whose hasData marks completion
  approvedFlag?: "diagnosis" | "plan";
  photoSlots?: PhotoSlot[];
}

const STEPS: WizardStep[] = [
  { order: 1, key: "patient", title: "بيانات المريض", required: "الاسم، رقم الملف، العمر، الجنس", icon: User, kind: "patient", slideTypes: ["PatientInformation"] },
  { order: 2, key: "complaint", title: "المقابلة والشكوى الرئيسية", required: "الشكوى الرئيسية والتاريخ", icon: MessageSquare, kind: "data", tab: "exam", slideTypes: ["ChiefComplaint"] },
  { order: 3, key: "extraoral", title: "الصور خارج الفم", required: "أمامي، جانبي، ابتسامة", icon: Camera, kind: "photos", slideTypes: ["ExtraoralPhotos"], photoSlots: [
    { key: "exf", label: "أمامي", category: "Extraoral", subtype: "Frontal", phase: "Initial", capture: "user" },
    { key: "exp", label: "جانبي (بروفايل)", category: "Extraoral", subtype: "Profile", phase: "Initial", capture: "user" },
    { key: "exs", label: "الابتسامة", category: "Extraoral", subtype: "Smile", phase: "Initial", capture: "user" },
  ] },
  { order: 4, key: "facial", title: "التحليل الوجهي", required: "تحليل الملف/الأنسجة الرخوة", icon: Smile, kind: "data", tab: "facial", slideTypes: ["FacialAnalysis"] },
  { order: 5, key: "intraoral", title: "الصور داخل الفم", required: "أمامي، يمين، يسار، علوي، سفلي", icon: Scan, kind: "photos", slideTypes: ["IntraoralPhotos"], photoSlots: [
    { key: "inf", label: "أمامي", category: "Intraoral", subtype: "Frontal", phase: "Initial", capture: "environment" },
    { key: "inr", label: "يمين", category: "Intraoral", subtype: "Right", phase: "Initial", capture: "environment" },
    { key: "inl", label: "يسار", category: "Intraoral", subtype: "Left", phase: "Initial", capture: "environment" },
    { key: "inu", label: "إطباقي علوي", category: "Intraoral", subtype: "Upper Occlusal", phase: "Initial", capture: "environment" },
    { key: "ind", label: "إطباقي سفلي", category: "Intraoral", subtype: "Lower Occlusal", phase: "Initial", capture: "environment" },
  ] },
  { order: 6, key: "occlusion", title: "تقييم الإطباق", required: "علاقة الأرحاء/الأنياب، البزوغ، التطابق، الازدحام", icon: Stethoscope, kind: "data", tab: "exam", slideTypes: ["OcclusionAssessment"] },
  { order: 7, key: "pano", title: "الأشعة البانورامية", required: "صورة OPG", icon: ImageIcon, kind: "photos", slideTypes: ["PanoramicXray"], photoSlots: [
    { key: "opg", label: "بانوراما", category: "Radiograph", subtype: "OPG", phase: "Initial", capture: "environment" },
  ] },
  { order: 8, key: "cephxray", title: "الأشعة السيفالومترية", required: "صورة سيفالو جانبية", icon: ImageIcon, kind: "photos", slideTypes: ["CephalometricSummary"], photoSlots: [
    { key: "ceph", label: "سيفالو جانبي", category: "Radiograph", subtype: "Lateral Ceph", phase: "Initial", capture: "environment" },
  ] },
  { order: 9, key: "cephmeas", title: "قياسات السيفالو", required: "تحليل سيفالومتري محسوب", icon: Ruler, kind: "data", tab: "ceph", slideTypes: ["CephalometricMeasurements"] },
  { order: 10, key: "cast", title: "تحليل النماذج", required: "تحليل النماذج (ALD/Pont)", icon: Microscope, kind: "data", tab: "cast", slideTypes: ["CastAnalysis"] },
  { order: 11, key: "bolton", title: "تحليل بولتون", required: "نسب بولتون", icon: Sigma, kind: "data", tab: "cast", slideTypes: ["Bolton"] },
  { order: 12, key: "problems", title: "قائمة المشاكل التشخيصية", required: "مشاكل هيكلية/سنية/أنسجة رخوة/وظيفية", icon: ListChecks, kind: "data", tab: "problems", slideTypes: ["ProblemList"] },
  { order: 13, key: "diagnosis", title: "التشخيص", required: "هيكلي، سني، أنسجة رخوة، وظيفي + اعتماد", icon: ClipboardList, kind: "data", tab: "diagnosis", slideTypes: ["Diagnosis"], approvedFlag: "diagnosis" },
  { order: 14, key: "objectives", title: "أهداف العلاج", required: "أهداف العلاج", icon: Target, kind: "data", tab: "plan", slideTypes: ["TreatmentObjectives"] },
  { order: 15, key: "strategies", title: "استراتيجيات العلاج", required: "قلع/ارتكاز/توسيع/مطاطات…", icon: GitBranch, kind: "data", tab: "plan", slideTypes: ["TreatmentPlan"] },
  { order: 16, key: "plan", title: "خطة العلاج", required: "الجهاز، القلع، المدة + اعتماد", icon: FileText, kind: "data", tab: "plan", slideTypes: ["TreatmentPlan"], approvedFlag: "plan" },
  { order: 17, key: "mechano", title: "الميكانيكا العلاجية", required: "الحاصرات، السلك، الارتكاز", icon: Wrench, kind: "data", tab: "plan", slideTypes: ["Mechanotherapy"] },
  { order: 18, key: "visits", title: "الزيارات والتقدّم", required: "زيارة واحدة على الأقل", icon: CalendarClock, kind: "data", tab: "visits", slideTypes: ["VisitProgress"] },
  { order: 19, key: "final", title: "النتائج بعد العلاج", required: "صور نهائية (طور النهاية)", icon: Trophy, kind: "photos", slideTypes: ["FinalRecords"], photoSlots: [
    { key: "ff", label: "أمامي نهائي", category: "Extraoral", subtype: "Frontal", phase: "Final", capture: "user" },
    { key: "fif", label: "داخل الفم أمامي نهائي", category: "Intraoral", subtype: "Frontal", phase: "Final", capture: "environment" },
  ] },
  { order: 20, key: "retention", title: "مرحلة الاحتفاظ", required: "المثبّتات والتعليمات", icon: Shield, kind: "data", tab: "retention", slideTypes: ["Retention"] },
  { order: 21, key: "generate", title: "المعاينة وإنشاء العرض", required: "مراجعة الجاهزية ثم التوليد", icon: Presentation, kind: "generate" },
];

const STATUS_META: Record<StepStatus, { label: string; cls: string; icon: typeof CheckCircle2 }> = {
  missing: { label: "ناقص", cls: "bg-gray-100 text-gray-500", icon: CircleDashed },
  partial: { label: "جزئي", cls: "bg-amber-50 text-amber-700", icon: AlertCircle },
  complete: { label: "مكتمل", cls: "bg-green-50 text-green-700", icon: CheckCircle2 },
  approved: { label: "معتمد", cls: "bg-clinic-blue-50 text-clinic-blue", icon: BadgeCheck },
};

const eq = (a?: string | null, b?: string) => (a ?? "").trim().toLowerCase() === (b ?? "").toLowerCase();

export function OrthoCaseWizard({ caseId, patientId, onNavigate }:
  { caseId: string; patientId?: string; onNavigate: (tab: Tab) => void }) {
  const [open, setOpen] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [prepPhoto, setPrepPhoto] = useState<OrthoPhoto | null>(null);

  const overviewQ = useOrthoOverview(caseId);
  const photosQ = useOrthoPhotos(caseId);
  const defQ = useQuery({
    queryKey: ["ortho-presentation-definition", caseId],
    enabled: !!caseId, retry: false,
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
  const readyCount = steps.filter((s) => s.step.kind !== "generate" && (s.status === "complete" || s.status === "approved")).length;
  const totalSteps = steps.filter((s) => s.step.kind !== "generate").length;

  const refresh = () => { photosQ.refetch(); defQ.refetch(); overviewQ.refetch(); };

  const generate = async (includeEmpty: boolean) => {
    setBusy(true);
    try {
      const res = await api.post(`/api/ortho-cases/${caseId}/case-presentation/pptx`,
        { includeEmptyOptionalSlides: includeEmpty }, { responseType: "blob" });
      const url = window.URL.createObjectURL(new Blob([res.data],
        { type: "application/vnd.openxmlformats-officedocument.presentationml.presentation" }));
      const a = document.createElement("a");
      a.href = url; a.download = `ortho-case-${caseId}.pptx`;
      document.body.appendChild(a); a.click(); a.remove(); window.URL.revokeObjectURL(url);
    } catch { toast.error("تعذّر إنشاء العرض"); }
    finally { setBusy(false); }
  };

  return (
    <div className="space-y-4" dir="rtl">
      <div className="rounded-lg border border-gray-200 p-4">
        <div className="flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-bold text-clinic-navy">
            <Presentation className="h-4 w-4 text-clinic-blue" />معالج عرض الحالة الموجّه
          </h3>
          <span className={cn("rounded-full px-2.5 py-1 text-xs font-bold",
            readyCount === totalSteps ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700")}>
            {readyCount}/{totalSteps} خطوة جاهزة
          </span>
        </div>
        <div className="mt-3 h-2 overflow-hidden rounded-full bg-gray-100">
          <div className="h-full rounded-full bg-clinic-blue transition-all" style={{ width: `${(readyCount / totalSteps) * 100}%` }} />
        </div>
        <p className="mt-2 text-[11px] text-gray-400">اتبع الخطوات بالترتيب — كل خطوة تُظهر المطلوب والموجود والناقص. الصور تُلتقط من الكاميرا مباشرة على الجوال.</p>
      </div>

      <ol className="space-y-2">
        {steps.map(({ step, status }) => {
          const meta = STATUS_META[status];
          const Icon = step.icon;
          const StatusIcon = meta.icon;
          const isOpen = open === step.key;
          return (
            <li key={step.key} className="overflow-hidden rounded-lg border border-gray-200 bg-white">
              <button type="button" onClick={() => setOpen(isOpen ? null : step.key)}
                className="flex w-full items-center gap-3 p-3 text-right hover:bg-gray-50">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-clinic-blue-50 text-clinic-blue">
                  <Icon className="h-5 w-5" />
                </span>
                <span className="flex-1">
                  <span className="flex items-center gap-2">
                    <span className="text-[11px] text-gray-400 tabular-nums">{step.order}.</span>
                    <span className="text-sm font-semibold text-gray-800">{step.title}</span>
                  </span>
                  <span className="text-[11px] text-gray-500">{step.required}</span>
                </span>
                <span className={cn("inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium", meta.cls)}>
                  <StatusIcon className="h-3.5 w-3.5" />{meta.label}
                </span>
                <ChevronLeft className={cn("h-4 w-4 text-gray-300 transition", isOpen && "-rotate-90")} />
              </button>

              {isOpen && (
                <div className="border-t border-gray-100 bg-gray-50/50 p-3">
                  {step.kind === "patient" && (
                    <Link href={patientId ? `/patients/${patientId}` : "#"}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white hover:opacity-90">
                      <Pencil className="h-3.5 w-3.5" />فتح ملف المريض
                    </Link>
                  )}

                  {step.kind === "data" && step.tab && (
                    <div className="flex flex-wrap items-center gap-2">
                      <button type="button" onClick={() => onNavigate(step.tab!)}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white hover:opacity-90">
                        <Pencil className="h-3.5 w-3.5" />تعديل / إضافة
                      </button>
                      {(step.key === "problems" || step.key === "diagnosis" || step.key === "objectives" ||
                        step.key === "strategies" || step.key === "plan" || step.key === "mechano") && (
                        <span className="inline-flex items-center gap-1 rounded-lg border border-dashed border-clinic-blue/40 px-2.5 py-1.5 text-[11px] text-gray-500">
                          <Sparkles className="h-3.5 w-3.5 text-clinic-blue" />مساعدة الذكاء الاصطناعي — قريبًا (مسودة تتطلب اعتماد الطبيب)
                        </span>
                      )}
                    </div>
                  )}

                  {step.kind === "photos" && step.photoSlots && (
                    <div className="grid gap-2 sm:grid-cols-2">
                      {step.photoSlots.map((slot) => (
                        <PhotoSlotCard key={slot.key} caseId={caseId} slot={slot}
                          photo={photosFor(slot) ?? null} disabled={busy}
                          onChanged={refresh} onPrepare={(p) => setPrepPhoto(p)} />
                      ))}
                    </div>
                  )}

                  {step.kind === "generate" && (
                    <div className="space-y-3">
                      <div className="rounded-lg border border-gray-200 bg-white p-3">
                        <p className="text-xs font-semibold text-gray-600">الجاهزية: {readyCount}/{totalSteps}</p>
                        <ul className="mt-2 grid gap-1 sm:grid-cols-2">
                          {steps.filter((s) => s.step.kind !== "generate" && s.status === "missing").map((s) => (
                            <li key={s.step.key} className="flex items-center gap-1.5 text-[11px] text-amber-700">
                              <AlertCircle className="h-3.5 w-3.5" />ناقص: {s.step.title}
                            </li>
                          ))}
                          {steps.every((s) => s.step.kind === "generate" || s.status !== "missing") && (
                            <li className="text-[11px] text-green-700">كل الخطوات الأساسية مكتملة ✓</li>
                          )}
                        </ul>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <button type="button" onClick={() => generate(false)} disabled={busy}
                          className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-bold text-white hover:opacity-90 disabled:opacity-50">
                          {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Presentation className="h-3.5 w-3.5" />}
                          إنشاء العرض (تخطّي الشرائح الفارغة)
                        </button>
                        <button type="button" onClick={() => generate(true)} disabled={busy}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50">
                          مسودة تشمل الفارغة
                        </button>
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

function PhotoSlotCard({ caseId, slot, photo, disabled, onChanged, onPrepare }:
  { caseId: string; slot: PhotoSlot; photo: OrthoPhoto | null; disabled: boolean;
    onChanged: () => void; onPrepare: (p: OrthoPhoto) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);

  const onFile = async (file: File | undefined) => {
    if (!file) return;
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const { data } = await api.post<{ url: string }>("/api/uploads", form,
        { headers: { "Content-Type": "multipart/form-data" } });
      await orthoService.addPhoto(caseId, {
        photoUrl: data.url, category: slot.category, subtype: slot.subtype,
        photoType: slot.category, treatmentPhase: slot.phase, isSelectedForReport: true,
        caption: slot.label,
      } as Partial<OrthoPhoto>);
      toast.success("تم حفظ الصورة");
      onChanged();
    } catch { toast.error("تعذّر رفع الصورة"); }
    finally { setUploading(false); if (inputRef.current) inputRef.current.value = ""; }
  };

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-2.5">
      <div className="flex items-center justify-between">
        <span className="text-xs font-semibold text-gray-700">{slot.label}</span>
        {photo
          ? <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-1.5 py-0.5 text-[10px] text-green-700"><CheckCircle2 className="h-3 w-3" />ملتقطة</span>
          : <span className="rounded-full bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-500">مطلوبة</span>}
      </div>

      {photo && (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={resolveImageUrl(photo.photoUrl)} alt={slot.label}
          className="mt-2 h-24 w-full rounded-md object-cover" />
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
            <Upload className="h-3 w-3" />تجهيز للعرض
          </button>
        )}
      </div>
    </div>
  );
}
