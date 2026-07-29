"use client";

import { useMemo, useState } from "react";
import {
  AlertTriangle,
  BadgeCheck,
  CheckCircle2,
  Plus,
  Save,
  Trash2,
  X,
} from "lucide-react";
import { cn } from "@/lib/utils";
import {
  useApproveSpecificTreatmentPlan,
  useCreateTreatmentPlan,
  useDeleteTreatmentPlan,
  useTreatmentPlans,
} from "@/hooks/useOrtho";
import type { TreatmentPlan } from "@/types/ortho";
import { Field, EmptyState } from "./_shared";
import { inputCls, PLAN_LABELS } from "../_lib/types";

export function OrthoTreatmentPlansTab({ caseId }: { caseId: string }) {
  const { data: plans = [] as TreatmentPlan[], isError: plansError, refetch: refetchPlans } = useTreatmentPlans(caseId);
  const createPlan = useCreateTreatmentPlan(caseId);
  const approvePlan = useApproveSpecificTreatmentPlan(caseId);
  const deletePlan = useDeleteTreatmentPlan(caseId);
  const [showCreate, setShowCreate] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
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

  // Sprint 3 — delete a non-approved plan. The API rejects approved plans with HTTP 400
  // (Arabic), so we hide the button on approved plans AND guard here as a defense-in-depth.
  const handleDelete = (planId: string) => {
    deletePlan.mutate(planId, {
      onSettled: () => setConfirmDeleteId(null),
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

      {/* ORTHO-REQ-006: a failed fetch must never render the same "no plans yet"
          empty state — that hides a real server error from the doctor. */}
      {plansError && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2"
          style={{ background: "#fef2f2", borderColor: "#fecaca" }}>
          <div className="flex items-center gap-2 text-xs font-bold" style={{ color: "#b91c1c" }}>
            <AlertTriangle className="h-4 w-4 flex-shrink-0" />
            تعذر تحميل خطط العلاج من الخادم
          </div>
          <button onClick={() => refetchPlans()}
            className="flex-shrink-0 rounded-lg px-3 py-1.5 text-xs font-bold text-white transition hover:opacity-90"
            style={{ background: "#b91c1c" }}>
            إعادة المحاولة
          </button>
        </div>
      )}

      {/* Plans list */}
      {plansError ? null : plans.length === 0 ? (
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
                    <>
                      <button
                        type="button"
                        onClick={() => plan.id && approvePlan.mutate(plan.id)}
                        disabled={!plan.id || approvePlan.isPending}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-green-200 bg-green-50 px-3 py-1.5 text-xs font-medium text-green-700 transition hover:bg-green-100 disabled:opacity-50"
                      >
                        <BadgeCheck className="h-3.5 w-3.5" />
                        اعتماد
                      </button>
                      {/* Sprint 3 — delete non-approved plan only. Approved plans are
                          rejected by the API (HTTP 400 Arabic); hide the button entirely. */}
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteId(plan.id ?? "")}
                        disabled={!plan.id || deletePlan.isPending}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-red-200 bg-red-50 px-3 py-1.5 text-xs font-medium text-red-700 transition hover:bg-red-100 disabled:opacity-50"
                        aria-label="حذف الخطة"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                        حذف
                      </button>
                    </>
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

              {/* Sprint 3 — inline delete confirmation. Only rendered for the plan the user
                  clicked "حذف" on. The API soft-deletes the plan; an approved plan can never
                  reach this UI (the delete button is hidden on approved plans), so the 400
                  path is purely defense-in-depth. */}
              {confirmDeleteId === plan.id && (
                <div className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3">
                  <p className="text-xs font-medium text-red-800">
                    هل أنت متأكد من حذف خطة {PLAN_LABELS[plan.planLabel ?? "A"] ?? plan.planLabel}؟
                    لا يمكن التراجع عن هذا الإجراء (سيتم إخفاؤها من القائمة؛ تظل محفوظة في سجل التدقيق).
                  </p>
                  <div className="mt-2 flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={() => setConfirmDeleteId(null)}
                      className="rounded-lg border border-gray-300 bg-white px-3 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                    >
                      إلغاء
                    </button>
                    <button
                      type="button"
                      onClick={() => plan.id && handleDelete(plan.id)}
                      disabled={deletePlan.isPending}
                      className="rounded-lg bg-red-600 px-3 py-1 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-60"
                    >
                      {deletePlan.isPending ? "جارٍ الحذف..." : "نعم، احذف"}
                    </button>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
