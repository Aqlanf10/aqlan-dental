"use client";

import { useEffect, useMemo, useState } from "react";
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
  ListChecks,
  Save,
  Scissors,
  ShieldCheck,
  Stethoscope,
  Trash2,
  User,
  Wallet,
} from "lucide-react";
import { cn, formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import { orthoService } from "@/services/orthoService";
import {
  useAddProblem,
  useAddRetentionVisit,
  useApproveTreatmentPlan,
  useClinicalExam,
  useDeleteProblem,
  useDiagnosis,
  useExtractionDecision,
  useOrthoCase,
  useOrthoOverview,
  useOrthoPhotos,
  useOrthoStages,
  useOrthoVisits,
  useProblemList,
  useRetention,
  useSaveClinicalExam,
  useSaveDiagnosis,
  useSaveExtractionDecision,
  useSaveRetention,
  useSaveTreatmentPlan,
  useTreatmentPlan,
} from "@/hooks/useOrtho";
import { toast } from "@/stores/toastStore";
import type {
  ClinicalExam,
  ExtractionDecision,
  OrthoDiagnosis,
  RetentionRecord,
  TreatmentPlan,
  TreatmentStage,
} from "@/types/ortho";
import { ORTHO_STATUS_LABELS } from "@/types/ortho";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { OrthoVisitTimeline } from "@/components/ortho/OrthoVisitTimeline";

type Tab =
  | "overview"
  | "records"
  | "exam"
  | "problems"
  | "diagnosis"
  | "plan"
  | "stages"
  | "visits"
  | "extraction"
  | "retention"
  | "finance";

const TABS: { key: Tab; label: string; icon: typeof Activity }[] = [
  { key: "overview", label: "الملخص", icon: Activity },
  { key: "records", label: "السجلات", icon: Camera },
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
      <span className="mb-1 block text-xs font-medium text-gray-500">{label}</span>
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

function SaveButton({ saving, children }: { saving?: boolean; children: React.ReactNode }) {
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

function OverviewPanel({
  caseId,
  setActiveTab,
}: {
  caseId: string;
  setActiveTab: (tab: Tab) => void;
}) {
  const { data: overview } = useOrthoOverview(caseId);
  const readiness = [
    { label: "الفحص السريري", done: overview?.hasClinicalExam, tab: "exam" as Tab },
    { label: "قائمة المشاكل", done: (overview?.problemsCount ?? 0) > 0, tab: "problems" as Tab },
    { label: "التشخيص", done: overview?.hasDiagnosis, tab: "diagnosis" as Tab },
    { label: "خطة العلاج", done: overview?.hasTreatmentPlan, tab: "plan" as Tab },
    { label: "اعتماد الخطة", done: overview?.isTreatmentPlanApproved, tab: "plan" as Tab },
    { label: "السجلات والصور", done: (overview?.photosCount ?? 0) > 0 || (overview?.cephAnalysesCount ?? 0) > 0, tab: "records" as Tab },
  ];
  const progress =
    overview && overview.totalStages > 0
      ? Math.round((overview.completedStages / overview.totalStages) * 100)
      : 0;

  return (
    <div className="grid gap-5 lg:grid-cols-[1.35fr_0.65fr]">
      <div className="space-y-5">
        <div className="grid gap-3 md:grid-cols-4">
          {[
            ["زيارات", overview?.visitsCount ?? 0],
            ["مشاكل", overview?.problemsCount ?? 0],
            ["صور", overview?.photosCount ?? 0],
            ["تحليلات Ceph", overview?.cephAnalysesCount ?? 0],
          ].map(([label, value]) => (
            <div key={label} className="rounded-lg border border-gray-200 bg-white p-4">
              <p className="text-xs text-gray-500">{label}</p>
              <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
            </div>
          ))}
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-gray-900">جاهزية ملف التقويم</h2>
              <p className="text-sm text-gray-500">الخطوات الأساسية قبل بدء العلاج والمتابعة المالية.</p>
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
                <span className="text-sm font-medium text-gray-800">{item.label}</span>
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
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">تقدم المراحل</p>
          <div className="mt-4 h-2 overflow-hidden rounded-full bg-gray-100">
            <div className="h-full rounded-full bg-clinic-blue" style={{ width: `${progress}%` }} />
          </div>
          <p className="mt-2 text-sm text-gray-500">
            {overview?.completedStages ?? 0} من {overview?.totalStages ?? 0} مراحل مكتملة
          </p>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">المالية المرتبطة</p>
          {overview?.contractId ? (
            <div className="mt-3 space-y-2 text-sm">
              <div className="flex justify-between"><span className="text-gray-500">العقد</span><span>{formatYemeniRiyal(overview.contractTotal ?? 0)}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">المدفوع</span><span className="text-green-600">{formatYemeniRiyal(overview.contractPaid ?? 0)}</span></div>
              <div className="flex justify-between font-semibold"><span>المتبقي</span><span className="text-red-600">{formatYemeniRiyal(overview.contractRemaining ?? 0)}</span></div>
              <Link href={`/finance/contracts/${overview.contractId}`} className="mt-3 inline-flex text-sm font-medium text-clinic-blue hover:underline">
                فتح العقد المالي
              </Link>
            </div>
          ) : (
            <p className="mt-3 text-sm text-gray-400">لا يوجد عقد مالي مرتبط بالحالة.</p>
          )}
        </div>
      </div>
    </div>
  );
}

function RecordsPanel({ caseId }: { caseId: string }) {
  const { data: photos = [], refetch } = useOrthoPhotos(caseId);
  const { data: overview } = useOrthoOverview(caseId);
  const [form, setForm] = useState({ photoUrl: "", photoType: "Intraoral", caption: "" });
  const [saving, setSaving] = useState(false);

  const addPhoto = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.photoUrl.trim()) return;
    setSaving(true);
    try {
      await orthoService.addPhoto(caseId, form);
      setForm({ photoUrl: "", photoType: "Intraoral", caption: "" });
      await refetch();
      toast.success("تمت إضافة السجل");
    } catch {
      toast.error("فشل إضافة السجل");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
      <form onSubmit={addPhoto} className="space-y-3 rounded-lg border border-gray-200 bg-white p-5">
        <h2 className="font-semibold text-gray-900">إضافة سجل أو صورة</h2>
        <Field label="الرابط">
          <input className={inputCls} value={form.photoUrl} onChange={(e) => setForm((f) => ({ ...f, photoUrl: e.target.value }))} />
        </Field>
        <Field label="النوع">
          <select className={inputCls} value={form.photoType} onChange={(e) => setForm((f) => ({ ...f, photoType: e.target.value }))}>
            <option value="Intraoral">داخل الفم</option>
            <option value="Extraoral">خارج الفم</option>
            <option value="Progress">متابعة</option>
            <option value="Radiograph">أشعة</option>
          </select>
        </Field>
        <Field label="ملاحظة">
          <input className={inputCls} value={form.caption} onChange={(e) => setForm((f) => ({ ...f, caption: e.target.value }))} />
        </Field>
        <SaveButton saving={saving}>إضافة السجل</SaveButton>
      </form>

      <div className="space-y-4">
        <div className="grid gap-3 md:grid-cols-3">
          <div className="rounded-lg border border-gray-200 bg-white p-4"><p className="text-xs text-gray-500">الصور</p><p className="text-2xl font-bold">{photos.length}</p></div>
          <div className="rounded-lg border border-gray-200 bg-white p-4"><p className="text-xs text-gray-500">Ceph</p><p className="text-2xl font-bold">{overview?.cephAnalysesCount ?? 0}</p></div>
          <div className="rounded-lg border border-gray-200 bg-white p-4"><p className="text-xs text-gray-500">الفحص</p><p className="text-2xl font-bold">{overview?.hasClinicalExam ? "✓" : "—"}</p></div>
        </div>
        {photos.length === 0 ? (
          <EmptyState text="لا توجد صور أو سجلات مرتبطة بحالة التقويم." />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {photos.map((p) => (
              <a key={p.id} href={p.photoUrl} target="_blank" rel="noreferrer" className="rounded-lg border border-gray-200 bg-white p-4 transition hover:border-clinic-blue/60">
                <p className="text-sm font-semibold text-gray-900">{p.caption || p.photoType}</p>
                <p className="mt-1 truncate text-xs text-gray-400">{p.photoUrl}</p>
                <p className="mt-2 text-xs text-gray-500">{p.takenAt ? formatArabicDate(p.takenAt) : ""}</p>
              </a>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function ClinicalExamPanel({ caseId }: { caseId: string }) {
  const { data } = useClinicalExam(caseId);
  const save = useSaveClinicalExam(caseId);
  const [form, setForm] = useState<ClinicalExam>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof ClinicalExam>(key: K, value: ClinicalExam[K]) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate(form);
      }}
      className="space-y-5"
    >
      <div className="grid gap-4 rounded-lg border border-gray-200 bg-white p-5 md:grid-cols-3">
        <Field label="تاريخ الفحص"><input type="date" className={inputCls} value={form.examDate ?? ""} onChange={(e) => set("examDate", e.target.value)} /></Field>
        <Field label="الملف الجانبي"><select className={inputCls} value={form.profile ?? ""} onChange={(e) => set("profile", e.target.value)}><option value="">اختر</option><option>Class I</option><option>Convex</option><option>Concave</option></select></Field>
        <Field label="علاقة الأرحاء"><select className={inputCls} value={form.molarRelation ?? ""} onChange={(e) => set("molarRelation", e.target.value)}><option value="">اختر</option><option>Class I</option><option>Class II</option><option>Class III</option></select></Field>
        <Field label="علاقة الأنياب"><select className={inputCls} value={form.canineRelation ?? ""} onChange={(e) => set("canineRelation", e.target.value)}><option value="">اختر</option><option>Class I</option><option>Class II</option><option>Class III</option></select></Field>
        <Field label="Overjet"><input type="number" step="0.1" className={inputCls} value={form.overjet ?? ""} onChange={(e) => set("overjet", e.target.value ? Number(e.target.value) : undefined)} /></Field>
        <Field label="Overbite"><input type="number" step="0.1" className={inputCls} value={form.overbite ?? ""} onChange={(e) => set("overbite", e.target.value ? Number(e.target.value) : undefined)} /></Field>
        <Field label="تكدس علوي"><input className={inputCls} value={form.upperCrowding ?? ""} onChange={(e) => set("upperCrowding", e.target.value)} /></Field>
        <Field label="تكدس سفلي"><input className={inputCls} value={form.lowerCrowding ?? ""} onChange={(e) => set("lowerCrowding", e.target.value)} /></Field>
        <Field label="خط الابتسامة"><input className={inputCls} value={form.smileLine ?? ""} onChange={(e) => set("smileLine", e.target.value)} /></Field>
        <Field label="ملاحظات TMJ" className="md:col-span-3"><textarea rows={2} className={inputCls} value={form.tmjFindings ?? ""} onChange={(e) => set("tmjFindings", e.target.value)} /></Field>
        <Field label="ملاحظات عامة" className="md:col-span-3"><textarea rows={3} className={inputCls} value={form.notes ?? ""} onChange={(e) => set("notes", e.target.value)} /></Field>
      </div>
      <SaveButton saving={save.isPending}>حفظ الفحص</SaveButton>
    </form>
  );
}

function ProblemsPanel({ caseId }: { caseId: string }) {
  const { data: problems = [] } = useProblemList(caseId);
  const add = useAddProblem(caseId);
  const remove = useDeleteProblem(caseId);
  const [form, setForm] = useState({ category: "skeletal", description: "", severity: "moderate" });
  const categories = { skeletal: "هيكلية", dental: "سنية", soft_tissue: "أنسجة رخوة", functional: "وظيفية", space: "مسافات", esthetic: "جمالية" };

  return (
    <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!form.description.trim()) return;
          add.mutate(form, { onSuccess: () => setForm({ category: "skeletal", description: "", severity: "moderate" }) });
        }}
        className="space-y-3 rounded-lg border border-gray-200 bg-white p-5"
      >
        <h2 className="font-semibold text-gray-900">إضافة مشكلة تشخيصية</h2>
        <Field label="التصنيف"><select className={inputCls} value={form.category} onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))}>{Object.entries(categories).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
        <Field label="الوصف"><textarea rows={3} className={inputCls} value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} /></Field>
        <Field label="الشدة"><select className={inputCls} value={form.severity} onChange={(e) => setForm((f) => ({ ...f, severity: e.target.value }))}><option value="mild">خفيفة</option><option value="moderate">متوسطة</option><option value="severe">شديدة</option></select></Field>
        <SaveButton saving={add.isPending}>إضافة المشكلة</SaveButton>
      </form>
      <div className="space-y-3">
        {problems.length === 0 ? <EmptyState text="لم يتم تسجيل مشاكل تشخيصية بعد." /> : problems.map((p) => (
          <div key={p.id} className="flex items-start justify-between gap-3 rounded-lg border border-gray-200 bg-white p-4">
            <div>
              <p className="font-medium text-gray-900">{p.description}</p>
              <p className="mt-1 text-xs text-gray-500">{categories[p.category as keyof typeof categories] ?? p.category} · {p.severity ?? "غير محدد"}</p>
            </div>
            <button type="button" onClick={() => remove.mutate(p.id)} className="rounded-lg p-2 text-gray-400 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
          </div>
        ))}
      </div>
    </div>
  );
}

function DiagnosisPanel({ caseId }: { caseId: string }) {
  const { data } = useDiagnosis(caseId);
  const save = useSaveDiagnosis(caseId);
  const [form, setForm] = useState<OrthoDiagnosis>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof OrthoDiagnosis>(key: K, value: OrthoDiagnosis[K]) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form onSubmit={(e) => { e.preventDefault(); save.mutate(form); }} className="space-y-5">
      <div className="grid gap-4 rounded-lg border border-gray-200 bg-white p-5 md:grid-cols-3">
        <Field label="Skeletal"><input className={inputCls} value={form.skeletalClassification ?? ""} onChange={(e) => set("skeletalClassification", e.target.value)} /></Field>
        <Field label="Dental"><input className={inputCls} value={form.dentalClassification ?? ""} onChange={(e) => set("dentalClassification", e.target.value)} /></Field>
        <Field label="Facial pattern"><input className={inputCls} value={form.facialPattern ?? ""} onChange={(e) => set("facialPattern", e.target.value)} /></Field>
        {(["anb", "wits", "fma", "sna", "snb", "impa"] as const).map((key) => (
          <Field key={key} label={key.toUpperCase()}><input type="number" step="0.1" className={inputCls} value={form[key] ?? ""} onChange={(e) => set(key, e.target.value ? Number(e.target.value) : undefined)} /></Field>
        ))}
        <Field label="ملخص التشخيص" className="md:col-span-3"><textarea rows={4} className={inputCls} value={form.summary ?? ""} onChange={(e) => set("summary", e.target.value)} /></Field>
      </div>
      <SaveButton saving={save.isPending}>حفظ التشخيص</SaveButton>
    </form>
  );
}

function TreatmentPlanPanel({ caseId }: { caseId: string }) {
  const { data } = useTreatmentPlan(caseId);
  const save = useSaveTreatmentPlan(caseId);
  const approve = useApproveTreatmentPlan(caseId);
  const [form, setForm] = useState<TreatmentPlan>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof TreatmentPlan>(key: K, value: TreatmentPlan[K]) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form onSubmit={(e) => { e.preventDefault(); save.mutate(form); }} className="space-y-5">
      <div className="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-4">
        <div>
          <p className="font-semibold text-gray-900">خطة العلاج</p>
          <p className="text-sm text-gray-500">{data?.isApproved ? `معتمدة ${data.approvedAt ? `بتاريخ ${formatArabicDate(data.approvedAt)}` : ""}` : "غير معتمدة"}</p>
        </div>
        <button type="button" onClick={() => approve.mutate()} disabled={!data?.id || approve.isPending} className="inline-flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm font-medium text-green-700 disabled:opacity-50">
          <BadgeCheck className="h-4 w-4" />
          اعتماد الخطة
        </button>
      </div>
      <div className="grid gap-4 rounded-lg border border-gray-200 bg-white p-5 md:grid-cols-2">
        <Field label="نوع الجهاز"><input className={inputCls} value={form.applianceType ?? ""} onChange={(e) => set("applianceType", e.target.value)} /></Field>
        <Field label="نظام البراكت"><input className={inputCls} value={form.bracketSystem ?? ""} onChange={(e) => set("bracketSystem", e.target.value)} /></Field>
        <Field label="السلك الأولي"><input className={inputCls} value={form.initialWire ?? ""} onChange={(e) => set("initialWire", e.target.value)} /></Field>
        <Field label="المدة المتوقعة"><input type="number" className={inputCls} value={form.expectedDurationMonths ?? ""} onChange={(e) => set("expectedDurationMonths", e.target.value ? Number(e.target.value) : undefined)} /></Field>
        <Field label="خطة الخلع" className="md:col-span-2"><textarea rows={2} className={inputCls} value={form.extractionPlan ?? ""} onChange={(e) => set("extractionPlan", e.target.value)} /></Field>
        <Field label="Anchorage" className="md:col-span-2"><textarea rows={2} className={inputCls} value={form.anchoragePlan ?? ""} onChange={(e) => set("anchoragePlan", e.target.value)} /></Field>
        <Field label="أهداف العلاج" className="md:col-span-2"><textarea rows={3} className={inputCls} value={form.treatmentGoals ?? ""} onChange={(e) => set("treatmentGoals", e.target.value)} /></Field>
        <Field label="المخاطر والحدود" className="md:col-span-2"><textarea rows={3} className={inputCls} value={form.risksLimitations ?? ""} onChange={(e) => set("risksLimitations", e.target.value)} /></Field>
        <Field label="خطة الاحتفاظ" className="md:col-span-2"><textarea rows={2} className={inputCls} value={form.retentionPlan ?? ""} onChange={(e) => set("retentionPlan", e.target.value)} /></Field>
      </div>
      <SaveButton saving={save.isPending}>حفظ الخطة</SaveButton>
    </form>
  );
}

function ExtractionPanel({ caseId }: { caseId: string }) {
  const { data } = useExtractionDecision(caseId);
  const save = useSaveExtractionDecision(caseId);
  const [form, setForm] = useState<ExtractionDecision>({});
  useEffect(() => setForm(data ?? {}), [data]);

  return (
    <form onSubmit={(e) => { e.preventDefault(); save.mutate(form); }} className="space-y-5">
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <div className="grid gap-3 md:grid-cols-2">
          {[
            ["no_extraction", "بدون خلع"],
            ["extract_4", "خلع 4 ضواحك"],
            ["extract_2_upper", "خلع ضاحكين علويين"],
            ["extract_2_lower", "خلع ضاحكين سفليين"],
            ["borderline", "حالة حدية تحتاج مراجعة"],
          ].map(([value, label]) => (
            <label key={value} className={cn("flex cursor-pointer items-center gap-3 rounded-lg border p-3", form.decision === value ? "border-clinic-blue bg-clinic-blue-50" : "border-gray-200")}>
              <input type="radio" checked={form.decision === value} onChange={() => setForm((f) => ({ ...f, decision: value }))} />
              <span className="text-sm font-medium">{label}</span>
            </label>
          ))}
        </div>
        <Field label="ملاحظات الطبيب" className="mt-4"><textarea rows={4} className={inputCls} value={form.doctorNotes ?? ""} onChange={(e) => setForm((f) => ({ ...f, doctorNotes: e.target.value }))} /></Field>
      </div>
      <SaveButton saving={save.isPending}>حفظ القرار</SaveButton>
    </form>
  );
}

function RetentionPanel({ caseId }: { caseId: string }) {
  const { data } = useRetention(caseId);
  const save = useSaveRetention(caseId);
  const addVisit = useAddRetentionVisit(caseId);
  const [form, setForm] = useState<RetentionRecord>({});
  const [visit, setVisit] = useState({ visitDate: "", period: "", toothStability: "", retainerStatus: "", notes: "" });
  useEffect(() => setForm(data ?? {}), [data]);

  return (
    <div className="grid gap-5 lg:grid-cols-2">
      <form onSubmit={(e) => { e.preventDefault(); save.mutate(form); }} className="space-y-3 rounded-lg border border-gray-200 bg-white p-5">
        <h2 className="font-semibold text-gray-900">سجل الاحتفاظ</h2>
        <Field label="تاريخ فك الجهاز"><input type="date" className={inputCls} value={form.debondDate ?? ""} onChange={(e) => setForm((f) => ({ ...f, debondDate: e.target.value }))} /></Field>
        <Field label="Retainer علوي"><input className={inputCls} value={form.upperRetainer ?? ""} onChange={(e) => setForm((f) => ({ ...f, upperRetainer: e.target.value }))} /></Field>
        <Field label="Retainer سفلي"><input className={inputCls} value={form.lowerRetainer ?? ""} onChange={(e) => setForm((f) => ({ ...f, lowerRetainer: e.target.value }))} /></Field>
        <Field label="تعليمات"><textarea rows={3} className={inputCls} value={form.instructions ?? ""} onChange={(e) => setForm((f) => ({ ...f, instructions: e.target.value }))} /></Field>
        <SaveButton saving={save.isPending}>حفظ الاحتفاظ</SaveButton>
      </form>
      <div className="space-y-4">
        <form onSubmit={(e) => { e.preventDefault(); addVisit.mutate(visit, { onSuccess: () => setVisit({ visitDate: "", period: "", toothStability: "", retainerStatus: "", notes: "" }) }); }} className="space-y-3 rounded-lg border border-gray-200 bg-white p-5">
          <h2 className="font-semibold text-gray-900">زيارة احتفاظ</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="التاريخ"><input type="date" className={inputCls} value={visit.visitDate} onChange={(e) => setVisit((v) => ({ ...v, visitDate: e.target.value }))} /></Field>
            <Field label="الفترة"><input className={inputCls} value={visit.period} onChange={(e) => setVisit((v) => ({ ...v, period: e.target.value }))} /></Field>
            <Field label="ثبات الأسنان"><input className={inputCls} value={visit.toothStability} onChange={(e) => setVisit((v) => ({ ...v, toothStability: e.target.value }))} /></Field>
            <Field label="حالة الجهاز"><input className={inputCls} value={visit.retainerStatus} onChange={(e) => setVisit((v) => ({ ...v, retainerStatus: e.target.value }))} /></Field>
          </div>
          <Field label="ملاحظات"><textarea rows={2} className={inputCls} value={visit.notes} onChange={(e) => setVisit((v) => ({ ...v, notes: e.target.value }))} /></Field>
          <SaveButton saving={addVisit.isPending}>إضافة زيارة</SaveButton>
        </form>
        {(data?.visits?.length ?? 0) === 0 ? <EmptyState text="لا توجد زيارات احتفاظ." /> : data?.visits?.map((v) => (
          <div key={v.id} className="rounded-lg border border-gray-200 bg-white p-4 text-sm">
            <p className="font-semibold">{v.visitDate ? formatArabicDate(v.visitDate) : "بدون تاريخ"} · {v.period}</p>
            <p className="mt-1 text-gray-500">{v.retainerStatus} {v.toothStability ? `· ${v.toothStability}` : ""}</p>
            {v.notes && <p className="mt-2 text-gray-700">{v.notes}</p>}
          </div>
        ))}
      </div>
    </div>
  );
}

function FinancePanel({ caseId, patientId }: { caseId: string; patientId: string }) {
  const { data: overview } = useOrthoOverview(caseId);
  return (
    <div className="grid gap-5 md:grid-cols-3">
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">إجمالي العقد</p>
        <p className="mt-2 text-2xl font-bold text-gray-900">{formatYemeniRiyal(overview?.contractTotal ?? 0)}</p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">المدفوع</p>
        <p className="mt-2 text-2xl font-bold text-green-600">{formatYemeniRiyal(overview?.contractPaid ?? 0)}</p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <p className="text-sm text-gray-500">المتبقي</p>
        <p className="mt-2 text-2xl font-bold text-red-600">{formatYemeniRiyal(overview?.contractRemaining ?? 0)}</p>
      </div>
      <div className="rounded-lg border border-gray-200 bg-white p-5 md:col-span-3">
        <div className="flex flex-wrap gap-3">
          {overview?.contractId && <Link href={`/finance/contracts/${overview.contractId}`} className="rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white">فتح العقد</Link>}
          <Link href={`/finance/contracts/new?patientId=${patientId}&relatedCaseId=${caseId}`} className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">إنشاء عقد تقويم</Link>
          <Link href={`/patients/${patientId}?tab=finance`} className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">مالية المريض</Link>
        </div>
      </div>
    </div>
  );
}

export default function OrthoCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<Tab>("overview");
  const { data: orthoCase, isLoading } = useOrthoCase(id);
  const { data: stages = [] } = useOrthoStages(id);
  const { data: visits = [] } = useOrthoVisits(id);
  const [localStages, setLocalStages] = useState<TreatmentStage[]>([]);
  useEffect(() => setLocalStages(stages), [stages]);

  const stageProgress = useMemo(() => orthoCase?.stagePercentage ?? 0, [orthoCase]);

  if (isLoading) {
    return <div className="space-y-4 animate-pulse"><div className="h-28 rounded-lg bg-gray-100" /><div className="h-80 rounded-lg bg-gray-100" /></div>;
  }

  if (!orthoCase) {
    return <div className="py-20 text-center text-gray-400">الحالة غير موجودة</div>;
  }

  return (
    <div className="max-w-7xl space-y-5" dir="rtl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/ortho" className="inline-flex items-center gap-1 hover:text-clinic-blue"><ArrowRight className="h-4 w-4" /> التقويم</Link>
        <span>/</span>
        <span className="font-medium text-gray-900">{orthoCase.caseNumber}</span>
      </div>

      <section className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="flex h-12 w-12 items-center justify-center rounded-lg text-white" style={{ backgroundColor: orthoCase.doctorColor ?? "#2563EB" }}>
              <GitBranch className="h-6 w-6" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-xl font-bold text-gray-900">{orthoCase.patientName}</h1>
                <span className="rounded bg-gray-100 px-2 py-1 font-mono text-xs text-gray-600">{orthoCase.caseNumber}</span>
                <span className="rounded-full bg-clinic-blue-50 px-2 py-1 text-xs font-medium text-clinic-blue">
                  {ORTHO_STATUS_LABELS[orthoCase.status] ?? orthoCase.status}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap gap-4 text-sm text-gray-500">
                <span>{orthoCase.doctorName ?? "بدون طبيب"}</span>
                {orthoCase.applianceType && <span>{orthoCase.applianceType}</span>}
                {orthoCase.startDate && <span>بدأت: {formatArabicDate(orthoCase.startDate)}</span>}
                {orthoCase.totalFee && <span>{formatYemeniRiyal(orthoCase.totalFee)}</span>}
              </div>
            </div>
          </div>
          <Link href={`/patients/${orthoCase.patientId}`} className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
            <User className="h-4 w-4" />
            ملف المريض
          </Link>
        </div>
        <div className="mt-4 flex items-center gap-3">
          <div className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100">
            <div className="h-full rounded-full bg-clinic-blue" style={{ width: `${stageProgress}%` }} />
          </div>
          <span className="text-sm font-medium text-gray-600">{stageProgress}%</span>
        </div>
      </section>

      <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
        <div className="flex overflow-x-auto border-b border-gray-100">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              type="button"
              onClick={() => setActiveTab(key)}
              className={cn(
                "inline-flex items-center gap-2 whitespace-nowrap border-b-2 px-4 py-3 text-sm font-medium transition",
                activeTab === key ? "border-clinic-blue text-clinic-blue" : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="h-4 w-4" />
              {label}
            </button>
          ))}
        </div>
        <div className="p-5">
          {activeTab === "overview" && <OverviewPanel caseId={id} setActiveTab={setActiveTab} />}
          {activeTab === "records" && <RecordsPanel caseId={id} />}
          {activeTab === "exam" && <ClinicalExamPanel caseId={id} />}
          {activeTab === "problems" && <ProblemsPanel caseId={id} />}
          {activeTab === "diagnosis" && <DiagnosisPanel caseId={id} />}
          {activeTab === "plan" && <TreatmentPlanPanel caseId={id} />}
          {activeTab === "stages" && <TreatmentStagesPanel caseId={id} stages={localStages} onUpdate={(stage) => setLocalStages((items) => items.map((item) => item.id === stage.id ? stage : item))} />}
          {activeTab === "visits" && <OrthoVisitTimeline caseId={id} visits={visits} />}
          {activeTab === "extraction" && <ExtractionPanel caseId={id} />}
          {activeTab === "retention" && <RetentionPanel caseId={id} />}
          {activeTab === "finance" && <FinancePanel caseId={id} patientId={orthoCase.patientId} />}
        </div>
      </section>
    </div>
  );
}
