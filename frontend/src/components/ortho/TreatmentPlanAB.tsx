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
  const inputCls = "w-full px-3 py-2 text-sm rounded-xl border bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]";
  const inputStyle = { borderColor: "#dce8f5" };
  const data: PlanFormData = plan ?? {};

  return (
    <div
      className="rounded-xl border-2 p-4 space-y-4 transition"
      style={isSelected
        ? { borderColor: "#3d7ab5", backgroundColor: "#3d7ab508" }
        : { borderColor: "#e8f0f9", backgroundColor: "#ffffff" }
      }
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span
            className="w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold"
            style={label === "خطة A"
              ? { backgroundColor: "#3d7ab518", color: "#3d7ab5" }
              : { backgroundColor: "#f5922e18", color: "#f5922e" }
            }
          >
            {label === "خطة A" ? "A" : "B"}
          </span>
          <h4 className="text-sm font-semibold" style={{ color: "#0d2137" }}>{label}</h4>
          {isSelected && (
            <span
              className="text-xs py-0.5 rounded-full font-medium"
              style={{ padding: "2px 10px", backgroundColor: "#3d7ab518", color: "#3d7ab5" }}
            >
              مختارة
            </span>
          )}
        </div>
        {!isSelected && (
          <button
            onClick={onSelect}
            className="flex items-center gap-1 px-3 py-1 text-xs font-medium rounded-xl border transition"
            style={{ borderColor: "#3d7ab5", color: "#3d7ab5" }}
          >
            اختيار
          </button>
        )}
      </div>

      {/* Toggle switch - Plan A/B indicator */}
      <div className="flex items-center gap-3">
        <div className="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
          style={{ backgroundColor: isSelected ? "#3d7ab5" : "#dce8f5" }}
        >
          <span
            className="inline-block h-4 w-4 transform rounded-full bg-white transition-transform shadow-sm"
            style={{ transform: isSelected ? "translateX(24px)" : "translateX(4px)" }}
          />
        </div>
        <span className="text-xs font-medium" style={{ color: isSelected ? "#3d7ab5" : "#94a3b8" }}>
          {isSelected ? "الخطة النشطة" : "خطة بديلة"}
        </span>
      </div>

      {/* Form fields */}
      <div className="grid grid-cols-1 gap-3">
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>نوع الجهاز</label>
          <select
            className={inputCls}
            style={inputStyle}
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
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>نظام البراكيت</label>
          <input
            className={inputCls}
            style={inputStyle}
            value={data.bracketSystem ?? ""}
            onChange={(e) => onFieldChange("bracketSystem", e.target.value || undefined)}
            placeholder="MBT / Roth / Damon..."
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>السلك الأولي</label>
          <input
            className={inputCls}
            style={inputStyle}
            value={data.initialWire ?? ""}
            onChange={(e) => onFieldChange("initialWire", e.target.value || undefined)}
            placeholder="0.014 NiTi..."
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>خطة الخلع</label>
          <input
            className={inputCls}
            style={inputStyle}
            value={data.extractionPlan ?? ""}
            onChange={(e) => onFieldChange("extractionPlan", e.target.value || undefined)}
            placeholder="بدون خلع / خلع 4 أضراس..."
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>خطة التثبيت (Anchorage)</label>
          <input
            className={inputCls}
            style={inputStyle}
            value={data.anchoragePlan ?? ""}
            onChange={(e) => onFieldChange("anchoragePlan", e.target.value || undefined)}
          />
        </div>
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 text-sm cursor-pointer" style={{ color: "#0d2137" }}>
            <input
              type="checkbox"
              checked={data.useTads ?? false}
              onChange={(e) => onFieldChange("useTads", e.target.checked)}
              className="w-4 h-4 rounded accent-[#a855f7]"
            />
            استخدام TADs
          </label>
          <label className="flex items-center gap-2 text-sm cursor-pointer" style={{ color: "#0d2137" }}>
            <input
              type="checkbox"
              checked={data.useElastics ?? false}
              onChange={(e) => onFieldChange("useElastics", e.target.checked)}
              className="w-4 h-4 rounded accent-[#a855f7]"
            />
            استخدام مطاطات
          </label>
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>المدة المتوقعة (أشهر)</label>
          <input
            type="number"
            className={inputCls}
            style={inputStyle}
            value={data.expectedDurationMonths ?? ""}
            onChange={(e) => onFieldChange("expectedDurationMonths", e.target.value ? Number(e.target.value) : undefined)}
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>خطة الاحتفاظ</label>
          <textarea
            rows={2}
            className={inputCls}
            style={inputStyle}
            value={data.retentionPlan ?? ""}
            onChange={(e) => onFieldChange("retentionPlan", e.target.value || undefined)}
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>أهداف العلاج</label>
          <textarea
            rows={2}
            className={inputCls}
            style={inputStyle}
            value={data.treatmentGoals ?? ""}
            onChange={(e) => onFieldChange("treatmentGoals", e.target.value || undefined)}
          />
        </div>
        <div>
          <label className="block text-xs font-medium mb-1" style={{ color: "#64748b" }}>المخاطر والقيود</label>
          <textarea
            rows={2}
            className={inputCls}
            style={inputStyle}
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

  const showComparison = planA && planB;

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold" style={{ color: "#0d2137" }}>خطط العلاج</h3>
        {!planB && (
          <button
            onClick={handleCreatePlanB}
            disabled={creatingB}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-xl border transition disabled:opacity-60"
            style={{ borderColor: "#a855f7", color: "#a855f7" }}
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
        <div className="bg-white rounded-xl border overflow-hidden" style={{ borderColor: "#e8f0f9", boxShadow: "0 1px 3px rgba(13,33,55,0.06)" }}>
          <div className="p-4 border-b" style={{ borderColor: "#e8f0f9" }}>
            <div className="flex items-center gap-2">
              <ArrowRightLeft className="w-4 h-4" style={{ color: "#3d7ab5" }} />
              <h4 className="text-sm font-semibold" style={{ color: "#0d2137" }}>مقارنة بين الخطة A و B</h4>
            </div>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr style={{ backgroundColor: "#f7fafd" }}>
                  <th className="text-right px-4 py-2 text-xs font-medium" style={{ color: "#64748b" }}>المعيار</th>
                  <th className="text-center px-4 py-2 text-xs font-medium" style={{ color: "#3d7ab5" }}>خطة A</th>
                  <th className="text-center px-4 py-2 text-xs font-medium" style={{ color: "#f5922e" }}>خطة B</th>
                </tr>
              </thead>
              <tbody className="divide-y" style={{ dividerColor: "#f1f5f9" }}>
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
                    <tr key={key} style={isDiff ? { backgroundColor: "#f5922e08" } : {}}>
                      <td className="px-4 py-2 text-xs" style={{ color: "#64748b" }}>{label}</td>
                      <td className={cn("px-4 py-2 text-center text-xs", isDiff ? "font-semibold" : "")} style={{ color: isDiff ? "#0d2137" : "#94a3b8" }}>
                        {typeof valA === "boolean" ? (valA ? "نعم" : "لا") : (valA ?? "—")}
                      </td>
                      <td className={cn("px-4 py-2 text-center text-xs", isDiff ? "font-semibold" : "")} style={{ color: isDiff ? "#0d2137" : "#94a3b8" }}>
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
      <div className="flex items-center gap-3 border-t pt-4" style={{ borderColor: "#e8f0f9" }}>
        <button
          onClick={() => { handleSave("A"); if (planB) handleSave("B"); }}
          disabled={savingA || savingB}
          className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-xl text-white hover:opacity-90 disabled:opacity-60 transition"
          style={{ backgroundColor: "#a855f7" }}
        >
          <Save className="w-4 h-4" />
          {(savingA || savingB) ? "جاري الحفظ..." : "حفظ الكل"}
        </button>
        {saved && (
          <span className="flex items-center gap-1 text-sm font-medium" style={{ color: "#22c55e" }}>
            <CheckCircle2 className="w-4 h-4" />
            تم الحفظ بنجاح
          </span>
        )}
        {selectingPlan && (
          <span className="text-xs" style={{ color: "#64748b" }}>جاري تبديل الخطة...</span>
        )}
      </div>
    </div>
  );
}
