"use client";

import {
  BadgeCheck,
  Check,
  ChevronDown,
  CircleAlert,
  ClipboardCheck,
  CopyPlus,
  GripVertical,
  Plus,
  Save,
  ShieldCheck,
  Trash2,
  UserCheck,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  useApproveSpecificTreatmentPlan,
  useCreateTreatmentPlan,
  useRecordPatientPlanDecision,
  useTreatmentPlans,
  useUpdateTreatmentPlan,
} from "@/hooks/useOrtho";
import type {
  PatientPlanDecisionRequest,
  TreatmentPlan,
  TreatmentPlanObjective,
  TreatmentPlanPhase,
} from "@/types/ortho";
import { cn } from "@/lib/utils";

const inputClass =
  "w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 outline-none transition focus:border-clinic-blue focus:ring-2 focus:ring-clinic-blue/15 disabled:bg-gray-50 disabled:text-gray-500";

const PLAN_LABELS: Record<string, string> = {
  A: "الخطة A",
  B: "الخطة B",
  C: "الخطة C",
};

const OBJECTIVE_CATEGORIES = [
  ["Skeletal", "هيكلي"],
  ["Dental", "سني"],
  ["SoftTissue", "أنسجة رخوة"],
  ["Functional", "وظيفي"],
  ["Stability", "الثبات والاحتفاظ"],
  ["Other", "أخرى"],
] as const;

const PHASE_STATUS = [
  ["Planned", "مخططة"],
  ["Active", "نشطة"],
  ["Completed", "مكتملة"],
  ["OnHold", "متوقفة"],
] as const;

const DECISION_STATUS: {
  value: PatientPlanDecisionRequest["status"];
  label: string;
}[] = [
  { value: "NotPresented", label: "لم تُعرض" },
  { value: "Presented", label: "عُرضت للمريض" },
  { value: "Accepted", label: "مقبولة" },
  { value: "Declined", label: "مرفوضة" },
];

function emptyObjective(sortOrder = 0): TreatmentPlanObjective {
  return {
    category: "Dental",
    description: "",
    priority: 2,
    sortOrder,
  };
}

function emptyPhase(sequenceNumber = 1): TreatmentPlanPhase {
  return {
    phaseName: "",
    sequenceNumber,
    status: "Planned",
  };
}

function emptyPlan(planLabel: string): TreatmentPlan {
  return {
    planLabel,
    applianceType: "",
    bracketSystem: "",
    initialWire: "",
    extractionPlan: "",
    anchoragePlan: "",
    useTads: false,
    useElastics: false,
    treatmentGoals: "",
    mechanicsPlan: "",
    auxiliaryAppliances: "",
    spaceManagementPlan: "",
    interdisciplinaryPlan: "",
    retentionPlan: "",
    risksLimitations: "",
    objectives: [emptyObjective()],
    phases: [emptyPhase()],
  };
}

function readiness(plan: TreatmentPlan) {
  const checks = [
    { label: "ملخص الأهداف", done: Boolean(plan.treatmentGoals?.trim()) },
    { label: "المدة المتوقعة", done: Boolean(plan.expectedDurationMonths) },
    {
      label: "هدف منظم",
      done: Boolean(plan.objectives?.some((item) => item.description.trim())),
    },
    {
      label: "مرحلة علاجية",
      done: Boolean(plan.phases?.some((item) => item.phaseName.trim())),
    },
    { label: "الجهاز أو الميكانيكا", done: Boolean(plan.applianceType?.trim() || plan.mechanicsPlan?.trim()) },
  ];
  return {
    checks,
    requiredReady: checks.slice(0, 4).every((item) => item.done),
    percent: Math.round((checks.filter((item) => item.done).length / checks.length) * 100),
  };
}

function Field({
  label,
  children,
  hint,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="block space-y-1.5">
      <span className="text-xs font-semibold text-gray-700">{label}</span>
      {children}
      {hint && <span className="block text-[11px] text-gray-400">{hint}</span>}
    </label>
  );
}

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <section className="border-t border-gray-200 pt-5 first:border-t-0 first:pt-0">
      <div className="mb-4">
        <h3 className="text-sm font-bold text-clinic-navy">{title}</h3>
        {description && <p className="mt-1 text-xs text-gray-500">{description}</p>}
      </div>
      {children}
    </section>
  );
}

export default function OrthoTreatmentPlanWorkspace({ caseId }: { caseId: string }) {
  const { data: plans = [], isLoading } = useTreatmentPlans(caseId);
  const createPlan = useCreateTreatmentPlan(caseId);
  const updatePlan = useUpdateTreatmentPlan(caseId);
  const approvePlan = useApproveSpecificTreatmentPlan(caseId);
  const recordDecision = useRecordPatientPlanDecision(caseId);
  const [selectedId, setSelectedId] = useState<string | "new">("");
  const [draft, setDraft] = useState<TreatmentPlan>(() => emptyPlan("A"));
  const [decision, setDecision] = useState<PatientPlanDecisionRequest>({
    status: "NotPresented",
  });

  const usedLabels = useMemo(
    () => new Set(plans.map((plan) => plan.planLabel ?? "A")),
    [plans]
  );
  const availableLabels = ["A", "B", "C"].filter((label) => !usedLabels.has(label));
  const selectedPlan = plans.find((plan) => plan.id === selectedId);
  const isCreating = selectedId === "new";
  const isLocked = Boolean(selectedPlan?.isApproved);
  const planReadiness = readiness(draft);

  useEffect(() => {
    if (!selectedId && plans.length > 0) {
      const preferred = plans.find((plan) => plan.isApproved) ?? plans[0];
      setSelectedId(preferred.id ?? "");
    } else if (!selectedId && plans.length === 0) {
      setSelectedId("new");
      setDraft(emptyPlan("A"));
    }
  }, [plans, selectedId]);

  useEffect(() => {
    if (selectedPlan) {
      setDraft({
        ...selectedPlan,
        objectives: selectedPlan.objectives?.map((item) => ({ ...item })) ?? [],
        phases: selectedPlan.phases?.map((item) => ({ ...item })) ?? [],
      });
      setDecision({
        status: selectedPlan.patientDecisionStatus ?? "NotPresented",
        decisionBy: selectedPlan.patientDecisionBy ?? "",
        consentMethod: selectedPlan.patientConsentMethod ?? "",
        notes: selectedPlan.patientDecisionNotes ?? "",
      });
    }
  }, [selectedPlan]);

  const beginCreate = () => {
    const label = availableLabels[0];
    if (!label) return;
    setSelectedId("new");
    setDraft(emptyPlan(label));
  };

  const save = () => {
    const normalized: TreatmentPlan = {
      ...draft,
      objectives: (draft.objectives ?? [])
        .filter((item) => item.description.trim())
        .map((item, index) => ({ ...item, sortOrder: index })),
      phases: (draft.phases ?? [])
        .filter((item) => item.phaseName.trim())
        .map((item, index) => ({ ...item, sequenceNumber: index + 1 })),
    };

    if (isCreating) {
      createPlan.mutate(normalized, {
        onSuccess: (response) => setSelectedId(response.data.id ?? ""),
      });
      return;
    }
    if (!selectedPlan?.id) return;
    updatePlan.mutate({ planId: selectedPlan.id, data: normalized });
  };

  const updateObjective = (
    index: number,
    patch: Partial<TreatmentPlanObjective>
  ) => {
    setDraft((current) => ({
      ...current,
      objectives: (current.objectives ?? []).map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item
      ),
    }));
  };

  const updatePhase = (index: number, patch: Partial<TreatmentPlanPhase>) => {
    setDraft((current) => ({
      ...current,
      phases: (current.phases ?? []).map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item
      ),
    }));
  };

  if (isLoading) {
    return <div className="py-16 text-center text-sm text-gray-500">جاري تحميل خطط العلاج...</div>;
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 border-b border-gray-200 pb-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h2 className="text-base font-bold text-clinic-navy">خطة العلاج التقويمية</h2>
          <p className="mt-1 text-xs text-gray-500">
            قارن خطط A/B/C، وثّق الأهداف والمراحل والميكانيكا، ثم اعتمد الخطة وسجّل قرار المريض.
          </p>
        </div>
        {availableLabels.length > 0 && selectedId !== "new" && (
          <button
            type="button"
            onClick={beginCreate}
            className="inline-flex items-center justify-center gap-2 rounded-md bg-clinic-blue px-3 py-2 text-sm font-semibold text-white hover:bg-clinic-blue-600"
          >
            <CopyPlus className="h-4 w-4" />
            خطة بديلة
          </button>
        )}
      </div>

      <div className="grid gap-5 xl:grid-cols-[220px_minmax(0,1fr)]">
        <aside className="space-y-2">
          {plans.map((plan) => {
            const score = readiness(plan);
            return (
              <button
                key={plan.id}
                type="button"
                onClick={() => setSelectedId(plan.id ?? "")}
                className={cn(
                  "w-full rounded-md border p-3 text-start transition",
                  selectedId === plan.id
                    ? "border-clinic-blue bg-clinic-blue-50"
                    : "border-gray-200 bg-white hover:border-gray-300"
                )}
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="font-bold text-clinic-navy">
                    {PLAN_LABELS[plan.planLabel ?? "A"]}
                  </span>
                  {plan.isApproved && <BadgeCheck className="h-4 w-4 text-green-600" />}
                </div>
                <p className="mt-1 line-clamp-2 text-xs text-gray-500">
                  {plan.treatmentGoals || "لم يكتب ملخص الأهداف بعد"}
                </p>
                <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-gray-100">
                  <div
                    className="h-full rounded-full bg-clinic-blue"
                    style={{ width: `${score.percent}%` }}
                  />
                </div>
              </button>
            );
          })}
          {isCreating && (
            <div className="rounded-md border border-dashed border-clinic-blue bg-clinic-blue-50 p-3">
              <div className="flex items-center justify-between">
                <span className="font-bold text-clinic-navy">
                  {PLAN_LABELS[draft.planLabel ?? "A"]} الجديدة
                </span>
                {plans.length > 0 && (
                  <button
                    type="button"
                    onClick={() => setSelectedId(plans[0].id ?? "")}
                    className="rounded p-1 text-gray-400 hover:bg-white hover:text-gray-700"
                    title="إلغاء إنشاء الخطة"
                  >
                    <X className="h-4 w-4" />
                  </button>
                )}
              </div>
            </div>
          )}
        </aside>

        <div className="min-w-0 space-y-6">
          <div className="flex flex-col gap-3 rounded-md border border-gray-200 bg-gray-50 p-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <ClipboardCheck className="h-5 w-5 text-clinic-blue" />
                <span className="font-bold text-gray-900">
                  {PLAN_LABELS[draft.planLabel ?? "A"]}
                </span>
                {isLocked && (
                  <span className="rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-semibold text-green-700">
                    معتمدة ومقفلة
                  </span>
                )}
              </div>
              <p className="mt-1 text-xs text-gray-500">
                اكتمال التوثيق {planReadiness.percent}%
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {!isCreating && !isLocked && selectedPlan?.id && (
                <button
                  type="button"
                  onClick={() => approvePlan.mutate(selectedPlan.id!)}
                  disabled={!planReadiness.requiredReady || approvePlan.isPending}
                  title={
                    planReadiness.requiredReady
                      ? "اعتماد الخطة وقفل محتواها السريري"
                      : "أكمل ملخص الأهداف والمدة والهدف المنظم والمرحلة أولًا"
                  }
                  className="inline-flex items-center gap-2 rounded-md border border-green-300 bg-white px-3 py-2 text-sm font-semibold text-green-700 hover:bg-green-50 disabled:cursor-not-allowed disabled:opacity-45"
                >
                  <ShieldCheck className="h-4 w-4" />
                  اعتماد سريري
                </button>
              )}
              {!isLocked && (
                <button
                  type="button"
                  onClick={save}
                  disabled={createPlan.isPending || updatePlan.isPending}
                  className="inline-flex items-center gap-2 rounded-md bg-clinic-blue px-4 py-2 text-sm font-semibold text-white hover:bg-clinic-blue-600 disabled:opacity-50"
                >
                  <Save className="h-4 w-4" />
                  {isCreating ? "إنشاء الخطة" : "حفظ التعديلات"}
                </button>
              )}
            </div>
          </div>

          {!planReadiness.requiredReady && !isLocked && (
            <div className="flex gap-3 rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
              <CircleAlert className="mt-0.5 h-4 w-4 shrink-0" />
              <div className="flex flex-wrap gap-x-4 gap-y-1">
                {planReadiness.checks.slice(0, 4).map((item) => (
                  <span key={item.label} className="inline-flex items-center gap-1">
                    {item.done ? <Check className="h-3.5 w-3.5" /> : "○"} {item.label}
                  </span>
                ))}
              </div>
            </div>
          )}

          <fieldset disabled={isLocked} className="space-y-6 disabled:opacity-80">
            <Section
              title="ملخص الخطة"
              description="التصور السريري المختصر الذي سيظهر عند مقارنة البدائل."
            >
              <div className="grid gap-4 md:grid-cols-3">
                <Field label="نوع الجهاز">
                  <input
                    className={inputClass}
                    value={draft.applianceType ?? ""}
                    onChange={(event) => setDraft({ ...draft, applianceType: event.target.value })}
                    placeholder="ثابت، شفاف، وظيفي..."
                  />
                </Field>
                <Field label="نظام البراكت">
                  <input
                    className={inputClass}
                    value={draft.bracketSystem ?? ""}
                    onChange={(event) => setDraft({ ...draft, bracketSystem: event.target.value })}
                    placeholder="MBT 0.022 مثلًا"
                  />
                </Field>
                <Field label="المدة المتوقعة بالأشهر">
                  <input
                    className={inputClass}
                    type="number"
                    min={1}
                    max={120}
                    value={draft.expectedDurationMonths ?? ""}
                    onChange={(event) =>
                      setDraft({
                        ...draft,
                        expectedDurationMonths: event.target.value
                          ? Number(event.target.value)
                          : undefined,
                      })
                    }
                  />
                </Field>
              </div>
              <div className="mt-4">
                <Field label="ملخص أهداف العلاج">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.treatmentGoals ?? ""}
                    onChange={(event) => setDraft({ ...draft, treatmentGoals: event.target.value })}
                    placeholder="النتيجة السريرية التي تسعى الخطة لتحقيقها..."
                  />
                </Field>
              </div>
            </Section>

            <Section
              title="الأهداف المنظمة"
              description="أهداف قابلة للمراجعة موزعة حسب المجال والأولوية."
            >
              <div className="space-y-2">
                {(draft.objectives ?? []).map((objective, index) => (
                  <div
                    key={objective.id ?? `objective-${index}`}
                    className="grid gap-2 rounded-md border border-gray-200 bg-white p-3 md:grid-cols-[24px_130px_minmax(0,1fr)_110px_36px]"
                  >
                    <GripVertical className="mt-2 h-4 w-4 text-gray-300" />
                    <select
                      className={inputClass}
                      value={objective.category}
                      onChange={(event) => updateObjective(index, { category: event.target.value })}
                    >
                      {OBJECTIVE_CATEGORIES.map(([value, label]) => (
                        <option key={value} value={value}>{label}</option>
                      ))}
                    </select>
                    <input
                      className={inputClass}
                      value={objective.description}
                      onChange={(event) => updateObjective(index, { description: event.target.value })}
                      placeholder="وصف الهدف العلاجي"
                    />
                    <select
                      className={inputClass}
                      value={objective.priority}
                      onChange={(event) => updateObjective(index, { priority: Number(event.target.value) })}
                    >
                      <option value={1}>عالية</option>
                      <option value={2}>متوسطة</option>
                      <option value={3}>منخفضة</option>
                    </select>
                    <button
                      type="button"
                      onClick={() =>
                        setDraft({
                          ...draft,
                          objectives: (draft.objectives ?? []).filter((_, itemIndex) => itemIndex !== index),
                        })
                      }
                      className="rounded-md p-2 text-gray-400 hover:bg-red-50 hover:text-red-600"
                      title="حذف الهدف"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  onClick={() =>
                    setDraft({
                      ...draft,
                      objectives: [
                        ...(draft.objectives ?? []),
                        emptyObjective(draft.objectives?.length ?? 0),
                      ],
                    })
                  }
                  className="inline-flex items-center gap-2 rounded-md border border-dashed border-gray-300 px-3 py-2 text-xs font-semibold text-clinic-blue hover:border-clinic-blue hover:bg-clinic-blue-50"
                >
                  <Plus className="h-4 w-4" />
                  إضافة هدف
                </button>
              </div>
            </Section>

            <Section
              title="المراحل الزمنية"
              description="قسّم التنفيذ إلى مراحل واضحة مع الجهاز والميكانيكا والمدة المستهدفة."
            >
              <div className="space-y-3">
                {(draft.phases ?? []).map((phase, index) => (
                  <details
                    key={phase.id ?? `phase-${index}`}
                    className="group rounded-md border border-gray-200 bg-white"
                    open={index === 0}
                  >
                    <summary className="flex cursor-pointer list-none items-center gap-3 p-3">
                      <span className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-clinic-blue-50 text-xs font-bold text-clinic-blue">
                        {index + 1}
                      </span>
                      <span className="min-w-0 flex-1 truncate text-sm font-semibold text-gray-800">
                        {phase.phaseName || "مرحلة جديدة"}
                      </span>
                      {phase.targetDurationMonths && (
                        <span className="text-xs text-gray-400">{phase.targetDurationMonths} شهر</span>
                      )}
                      <ChevronDown className="h-4 w-4 text-gray-400 transition group-open:rotate-180" />
                    </summary>
                    <div className="grid gap-4 border-t border-gray-100 p-4 md:grid-cols-2">
                      <Field label="اسم المرحلة">
                        <input
                          className={inputClass}
                          value={phase.phaseName}
                          onChange={(event) => updatePhase(index, { phaseName: event.target.value })}
                          placeholder="المحاذاة والتسوية"
                        />
                      </Field>
                      <Field label="الحالة">
                        <select
                          className={inputClass}
                          value={phase.status ?? "Planned"}
                          onChange={(event) =>
                            updatePhase(index, {
                              status: event.target.value as TreatmentPlanPhase["status"],
                            })
                          }
                        >
                          {PHASE_STATUS.map(([value, label]) => (
                            <option key={value} value={value}>{label}</option>
                          ))}
                        </select>
                      </Field>
                      <Field label="هدف المرحلة">
                        <textarea
                          className={inputClass}
                          rows={2}
                          value={phase.objectiveSummary ?? ""}
                          onChange={(event) => updatePhase(index, { objectiveSummary: event.target.value })}
                        />
                      </Field>
                      <Field label="الجهاز أو الملحق المخطط">
                        <textarea
                          className={inputClass}
                          rows={2}
                          value={phase.plannedAppliance ?? ""}
                          onChange={(event) => updatePhase(index, { plannedAppliance: event.target.value })}
                        />
                      </Field>
                      <Field label="الميكانيكا">
                        <textarea
                          className={inputClass}
                          rows={2}
                          value={phase.mechanics ?? ""}
                          onChange={(event) => updatePhase(index, { mechanics: event.target.value })}
                        />
                      </Field>
                      <Field label="المدة المستهدفة بالأشهر">
                        <input
                          className={inputClass}
                          type="number"
                          min={1}
                          max={60}
                          value={phase.targetDurationMonths ?? ""}
                          onChange={(event) =>
                            updatePhase(index, {
                              targetDurationMonths: event.target.value
                                ? Number(event.target.value)
                                : undefined,
                            })
                          }
                        />
                      </Field>
                      <Field label="البداية المخططة">
                        <input
                          className={inputClass}
                          type="date"
                          value={phase.plannedStartDate ?? ""}
                          onChange={(event) => updatePhase(index, { plannedStartDate: event.target.value })}
                        />
                      </Field>
                      <Field label="النهاية المخططة">
                        <input
                          className={inputClass}
                          type="date"
                          value={phase.plannedEndDate ?? ""}
                          onChange={(event) => updatePhase(index, { plannedEndDate: event.target.value })}
                        />
                      </Field>
                      <div className="md:col-span-2 flex justify-end">
                        <button
                          type="button"
                          onClick={() =>
                            setDraft({
                              ...draft,
                              phases: (draft.phases ?? [])
                                .filter((_, itemIndex) => itemIndex !== index)
                                .map((item, itemIndex) => ({ ...item, sequenceNumber: itemIndex + 1 })),
                            })
                          }
                          className="inline-flex items-center gap-1.5 rounded-md px-2 py-1.5 text-xs font-semibold text-red-600 hover:bg-red-50"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                          حذف المرحلة
                        </button>
                      </div>
                    </div>
                  </details>
                ))}
                <button
                  type="button"
                  onClick={() =>
                    setDraft({
                      ...draft,
                      phases: [...(draft.phases ?? []), emptyPhase((draft.phases?.length ?? 0) + 1)],
                    })
                  }
                  className="inline-flex items-center gap-2 rounded-md border border-dashed border-gray-300 px-3 py-2 text-xs font-semibold text-clinic-blue hover:border-clinic-blue hover:bg-clinic-blue-50"
                >
                  <Plus className="h-4 w-4" />
                  إضافة مرحلة
                </button>
              </div>
            </Section>

            <Section title="الأجهزة والميكانيكا">
              <div className="grid gap-4 md:grid-cols-2">
                <Field label="السلك الأولي">
                  <input
                    className={inputClass}
                    value={draft.initialWire ?? ""}
                    onChange={(event) => setDraft({ ...draft, initialWire: event.target.value })}
                  />
                </Field>
                <Field label="خطة الارتكاز">
                  <input
                    className={inputClass}
                    value={draft.anchoragePlan ?? ""}
                    onChange={(event) => setDraft({ ...draft, anchoragePlan: event.target.value })}
                  />
                </Field>
                <Field label="خطة الميكانيكا">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.mechanicsPlan ?? ""}
                    onChange={(event) => setDraft({ ...draft, mechanicsPlan: event.target.value })}
                  />
                </Field>
                <Field label="الأجهزة المساعدة">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.auxiliaryAppliances ?? ""}
                    onChange={(event) => setDraft({ ...draft, auxiliaryAppliances: event.target.value })}
                  />
                </Field>
                <Field label="إدارة المسافات والخلع/IPR">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.spaceManagementPlan ?? ""}
                    onChange={(event) => setDraft({ ...draft, spaceManagementPlan: event.target.value })}
                  />
                </Field>
                <Field label="الخطة متعددة التخصصات">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.interdisciplinaryPlan ?? ""}
                    onChange={(event) => setDraft({ ...draft, interdisciplinaryPlan: event.target.value })}
                  />
                </Field>
              </div>
              <div className="mt-4 flex flex-wrap gap-5">
                <label className="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input
                    type="checkbox"
                    checked={draft.useTads ?? false}
                    onChange={(event) => setDraft({ ...draft, useTads: event.target.checked })}
                    className="rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
                  />
                  استخدام TADs
                </label>
                <label className="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input
                    type="checkbox"
                    checked={draft.useElastics ?? false}
                    onChange={(event) => setDraft({ ...draft, useElastics: event.target.checked })}
                    className="rounded border-gray-300 text-clinic-blue focus:ring-clinic-blue"
                  />
                  استخدام المطاطات
                </label>
              </div>
            </Section>

            <Section title="الخلع والاحتفاظ والمخاطر">
              <div className="grid gap-4 md:grid-cols-2">
                <Field label="خطة الخلع">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.extractionPlan ?? ""}
                    onChange={(event) => setDraft({ ...draft, extractionPlan: event.target.value })}
                  />
                </Field>
                <Field label="خطة الاحتفاظ">
                  <textarea
                    className={inputClass}
                    rows={3}
                    value={draft.retentionPlan ?? ""}
                    onChange={(event) => setDraft({ ...draft, retentionPlan: event.target.value })}
                  />
                </Field>
                <div className="md:col-span-2">
                  <Field label="المخاطر والحدود المتوقعة">
                    <textarea
                      className={inputClass}
                      rows={3}
                      value={draft.risksLimitations ?? ""}
                      onChange={(event) => setDraft({ ...draft, risksLimitations: event.target.value })}
                    />
                  </Field>
                </div>
              </div>
            </Section>
          </fieldset>

          {isLocked && selectedPlan?.id && (
            <Section
              title="عرض الخطة وقرار المريض"
              description="اعتماد الطبيب لا يعني موافقة المريض. سجّل العرض والقرار بشكل مستقل."
            >
              <div className="rounded-md border border-gray-200 bg-white p-4">
                <div className="mb-4 flex items-center gap-2">
                  <UserCheck className="h-5 w-5 text-clinic-blue" />
                  <span className="text-sm font-bold text-gray-800">
                    {DECISION_STATUS.find((item) => item.value === decision.status)?.label}
                  </span>
                </div>
                <div className="grid gap-4 md:grid-cols-2">
                  <Field label="حالة الخطة لدى المريض">
                    <select
                      className={inputClass}
                      value={decision.status}
                      onChange={(event) =>
                        setDecision({
                          ...decision,
                          status: event.target.value as PatientPlanDecisionRequest["status"],
                        })
                      }
                    >
                      {DECISION_STATUS.map((item) => (
                        <option key={item.value} value={item.value}>{item.label}</option>
                      ))}
                    </select>
                  </Field>
                  <Field label="طريقة الموافقة">
                    <select
                      className={inputClass}
                      value={decision.consentMethod ?? ""}
                      onChange={(event) => setDecision({ ...decision, consentMethod: event.target.value })}
                    >
                      <option value="">غير محددة</option>
                      <option value="Written">موافقة مكتوبة</option>
                      <option value="Digital">موافقة رقمية</option>
                      <option value="Verbal">موافقة شفهية موثقة</option>
                      <option value="Guardian">موافقة ولي الأمر</option>
                    </select>
                  </Field>
                  <Field label="اسم المريض أو ولي الأمر">
                    <input
                      className={inputClass}
                      value={decision.decisionBy ?? ""}
                      onChange={(event) => setDecision({ ...decision, decisionBy: event.target.value })}
                    />
                  </Field>
                  <Field label="ملاحظات القرار">
                    <input
                      className={inputClass}
                      value={decision.notes ?? ""}
                      onChange={(event) => setDecision({ ...decision, notes: event.target.value })}
                    />
                  </Field>
                </div>
                <div className="mt-4 flex justify-end">
                  <button
                    type="button"
                    onClick={() =>
                      recordDecision.mutate({ planId: selectedPlan.id!, data: decision })
                    }
                    disabled={
                      recordDecision.isPending ||
                      ((decision.status === "Accepted" || decision.status === "Declined") &&
                        !decision.decisionBy?.trim())
                    }
                    className="inline-flex items-center gap-2 rounded-md bg-clinic-navy px-4 py-2 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
                  >
                    <UserCheck className="h-4 w-4" />
                    حفظ قرار المريض
                  </button>
                </div>
              </div>
            </Section>
          )}
        </div>
      </div>
    </div>
  );
}
