"use client";

import { useState, useEffect, useCallback } from "react";
import { Save, CheckCircle2, Plus, ArrowRightLeft } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { TreatmentPlanDto } from "@/types/ortho";

interface Props {
  orthoCaseId: string;
  plans: TreatmentPlanDto[];
  selectedPlanId?: string;
  onPlansChange: () => void;
}

const APPLIANCE_OPTIONS = [
  { value: "MBT 0.022", label: "MBT 0.022" },
  { value: "MBT 0.018", label: "MBT 0.018" },
  { value: "Damon", label: "Damon" },
  { value: "Invisalign", label: "Invisalign" },
  { value: "Removable", label: "جهاز متحرك" },
  { value: "Functional", label: "جهاز وظيفي" },
];

interface PlanFormData {
  applianceType?: string;
  bracketSystem?: string;
  initialWire?: string;
  extractionPlan?: string;
  anchoragePlan?: string;
  useTads?: boolean;
  useElastics?: boolean;
  expectedDurationMonths?: number;
  retentionPlan?: string;
  treatmentGoals?: string;
  risksLimitations?: string;
}

function PlanPanel({
  label,
  plan,
  isSelected,
  onSelect,
  onFieldChange,
}: {
  label: string;
  plan: TreatmentPlanDto | null;
  isSelected: boolean;
  onSelect: () => void;
  onFieldChange: (field: string, value: unknown) => void;
}) {
  const inputCls = "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal";
  const data: PlanFormData = plan ?? {};

  return (
    <div className={cn(
      "rounded-xl border-2 p-4 space-y-4 transition",
      isSelected ? "border-clinic-teal bg-teal-50/30" : "border-gray-200 bg-white"
    )}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className={cn(
            "w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold",
            label === "خطة A" ? "bg-teal-100 text-teal-700" : "bg-orange-100 text-orange-700"
          )}>
            {label === "خطة A" ? "A" : "B"}
          </span>
          <h4 className="text-sm font-semibold text-gray-800">{label}</h4>
          {isSelected && (
            <span className="text-xs px-2 py-0.5 rounded-full bg-clinic-teal text-white font-medium">
              مختارة
            </span>
          )}
        </div>
        {!isSelected && (
          <button
            onClick={onSelect}
            className="flex items-center gap-1 px-3 py-1 text-xs font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-clinic-teal hover:text-white transition"
          >
            اختيار
          </button>
        )}
      </div>

      {/* Form fields */}
      <div className="grid grid-cols-1 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">نوع الجهاز</label>
          <select
            className={inputCls}
            value={data.applianceType ?? ""}
            onChange={(e) => onFieldChange("applianceType", e.target.value || undefined)}
          >
            <option value="">— اختر —</option>
            {APPLIANCE_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">نظام البراكيت</label>
          <input
            className={inputCls}
            value={data.bracketSystem ?? ""}
            onChange={(e) => onFieldChange("bracketSystem", e.target.value || undefined)}
            placeholder="MBT / Roth / Damon..."
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">السلك الأولي</label>
          <input
            className={inputCls}
            value={data.initialWire ?? ""}
            onChange={(e) => onFieldChange("initialWire", e.target.value || undefined)}
            placeholder="0.014 NiTi..."
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">خطة الخلع</label>
          <input
            className={inputCls}
            value={data.extractionPlan ?? ""}
            onChange={(e) => onFieldChange("extractionPlan", e.target.value || undefined)}
            placeholder="بدون خلع / خلع 4 أضراس..."
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">خطة التثبيت (Anchorage)</label>
          <input
            className={inputCls}
            value={data.anchoragePlan ?? ""}
            onChange={(e) => onFieldChange("anchoragePlan", e.target.value || undefined)}
          />
        </div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input
              type="checkbox"
              checked={data.useTads ?? false}
              onChange={(e) => onFieldChange("useTads", e.target.checked)}
              className="w-4 h-4 accent-clinic-teal rounded"
            />
            استخدام TADs
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input
              type="checkbox"
              checked={data.useElastics ?? false}
              onChange={(e) => onFieldChange("useElastics", e.target.checked)}
              className="w-4 h-4 accent-clinic-teal rounded"
            />
            استخدام مطاطات
          </label>
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">المدة المتوقعة (أشهر)</label>
          <input
            type="number"
            className={inputCls}
            value={data.expectedDurationMonths ?? ""}
            onChange={(e) => onFieldChange("expectedDurationMonths", e.target.value ? Number(e.target.value) : undefined)}
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">خطة الاحتفاظ</label>
          <textarea
            rows={2}
            className={inputCls}
            value={data.retentionPlan ?? ""}
            onChange={(e) => onFieldChange("retentionPlan", e.target.value || undefined)}
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">أهداف العلاج</label>
          <textarea
            rows={2}
            className={inputCls}
            value={data.treatmentGoals ?? ""}
            onChange={(e) => onFieldChange("treatmentGoals", e.target.value || undefined)}
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">المخاطر والقيود</label>
          <textarea
            rows={2}
            className={inputCls}
            value={data.risksLimitations ?? ""}
            onChange={(e) => onFieldChange("risksLimitations", e.target.value || undefined)}
          />
        </div>
      </div>
    </div>
  );
}

export function TreatmentPlanAB({ orthoCaseId, plans, selectedPlanId, onPlansChange }: Props) {
  const [planA, setPlanA] = useState<TreatmentPlanDto | null>(null);
  const [planB, setPlanB] = useState<TreatmentPlanDto | null>(null);
  const [savingA, setSavingA] = useState(false);
  const [savingB, setSavingB] = useState(false);
  const [saved, setSaved] = useState(false);
  const [creatingB, setCreatingB] = useState(false);
  const [selectingPlan, setSelectingPlan] = useState(false);

  useEffect(() => {
    const a = plans.find((p) => p.planLabel === "A") ?? null;
    const b = plans.find((p) => p.planLabel === "B") ?? null;
    setPlanA(a);
    setPlanB(b);
  }, [plans]);

  const handleFieldChange = useCallback(
    (planLabel: "A" | "B", field: string, value: unknown) => {
      const setter = planLabel === "A" ? setPlanA : setPlanB;
      setter((prev) => prev ? { ...prev, [field]: value } : { orthoCaseId, planLabel, isSelected: false, id: "", [field]: value } as TreatmentPlanDto);
    },
    [orthoCaseId]
  );

  const handleSave = async (planLabel: "A" | "B") => {
    const planData = planLabel === "A" ? planA : planB;
    if (!planData) return;

    const setSaving = planLabel === "A" ? setSavingA : setSavingB;
    setSaving(true);
    try {
      await api.put(`/api/ortho-cases/${orthoCaseId}/treatment-plan`, {
        ...planData,
        planLabel,
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
      onPlansChange();
    } catch {
      // Silent fail
    } finally {
      setSaving(false);
    }
  };

  const handleSelectPlan = async (planId: string) => {
    setSelectingPlan(true);
    try {
      await api.put(`/api/ortho-cases/${orthoCaseId}/treatment-plan/${planId}/select`);
      onPlansChange();
    } catch {
      // Silent fail
    } finally {
      setSelectingPlan(false);
    }
  };

  const handleCreatePlanB = async () => {
    setCreatingB(true);
    try {
      await api.put(`/api/ortho-cases/${orthoCaseId}/treatment-plan`, {
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
      onPlansChange();
    } catch {
      // Silent fail
    } finally {
      setCreatingB(false);
    }
  };

  const currentSelectedId = selectedPlanId ?? plans.find((p) => p.isSelected)?.id ?? planA?.id;

  // Comparison view
  const showComparison = planA && planB;

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-800">خطط العلاج</h3>
        {!planB && (
          <button
            onClick={handleCreatePlanB}
            disabled={creatingB}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-clinic-teal hover:text-white transition disabled:opacity-60"
          >
            <Plus className="w-4 h-4" />
            {creatingB ? "جاري الإنشاء..." : "إنشاء خطة بديلة"}
          </button>
        )}
      </div>

      {/* Side-by-side panels */}
      <div className={cn(
        "grid gap-4",
        planB ? "grid-cols-1 lg:grid-cols-2" : "grid-cols-1"
      )}>
        <PlanPanel
          label="خطة A"
          plan={planA}
          isSelected={currentSelectedId === planA?.id}
          onSelect={() => planA?.id && handleSelectPlan(planA.id)}
          onFieldChange={(field, value) => handleFieldChange("A", field, value)}
        />

        {planB && (
          <PlanPanel
            label="خطة B"
            plan={planB}
            isSelected={currentSelectedId === planB?.id}
            onSelect={() => planB?.id && handleSelectPlan(planB.id)}
            onFieldChange={(field, value) => handleFieldChange("B", field, value)}
          />
        )}
      </div>

      {/* Comparison Table */}
      {showComparison && (
        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <div className="p-4 border-b border-gray-100">
            <div className="flex items-center gap-2">
              <ArrowRightLeft className="w-4 h-4 text-clinic-teal" />
              <h4 className="text-sm font-semibold text-gray-700">مقارنة بين الخطة A و B</h4>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50">
                  <th className="text-right px-4 py-2 text-xs font-medium text-gray-500">المعيار</th>
                  <th className="text-center px-4 py-2 text-xs font-medium text-clinic-teal">خطة A</th>
                  <th className="text-center px-4 py-2 text-xs font-medium text-orange-600">خطة B</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {[
                  { label: "نوع الجهاز", key: "applianceType" as const },
                  { label: "نظام البراكيت", key: "bracketSystem" as const },
                  { label: "السلك الأولي", key: "initialWire" as const },
                  { label: "خطة الخلع", key: "extractionPlan" as const },
                  { label: "خطة التثبيت", key: "anchoragePlan" as const },
                  { label: "TADs", key: "useTads" as const },
                  { label: "مطاطات", key: "useElastics" as const },
                  { label: "المدة (أشهر)", key: "expectedDurationMonths" as const },
                  { label: "خطة الاحتفاظ", key: "retentionPlan" as const },
                  { label: "أهداف العلاج", key: "treatmentGoals" as const },
                  { label: "المخاطر", key: "risksLimitations" as const },
                ].map(({ label, key }) => {
                  const valA = planA?.[key];
                  const valB = planB?.[key];
                  const isDiff = valA !== valB;
                  return (
                    <tr key={key} className={cn(isDiff && "bg-amber-50/50")}>
                      <td className="px-4 py-2 text-gray-600 text-xs">{label}</td>
                      <td className={cn("px-4 py-2 text-center text-xs", isDiff ? "font-semibold text-gray-900" : "text-gray-500")}>
                        {typeof valA === "boolean" ? (valA ? "نعم" : "لا") : (valA ?? "—")}
                      </td>
                      <td className={cn("px-4 py-2 text-center text-xs", isDiff ? "font-semibold text-gray-900" : "text-gray-500")}>
                        {typeof valB === "boolean" ? (valB ? "نعم" : "لا") : (valB ?? "—")}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Save All */}
      <div className="flex items-center gap-3 border-t border-gray-100 pt-4">
        <button
          onClick={() => { handleSave("A"); if (planB) handleSave("B"); }}
          disabled={savingA || savingB}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <Save className="w-4 h-4" />
          {(savingA || savingB) ? "جاري الحفظ..." : "حفظ الكل"}
        </button>
        {saved && (
          <span className="flex items-center gap-1 text-sm text-teal-600 font-medium">
            <CheckCircle2 className="w-4 h-4" />
            تم الحفظ بنجاح
          </span>
        )}
        {selectingPlan && (
          <span className="text-xs text-gray-500">جاري تبديل الخطة...</span>
        )}
      </div>
    </div>
  );
}
