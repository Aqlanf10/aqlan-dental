"use client";

import { useEffect, useState } from "react";
import { CheckCircle2, Info } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  useExtractionDecision,
  useSaveExtractionDecision,
} from "@/hooks/useOrtho";
import type { ExtractionDecision } from "@/types/ortho";
import { EXTRACTION_FACTORS } from "@/types/ortho";
import { Field, SaveButton } from "./_shared";
import { inputCls } from "../_lib/types";

export function OrthoExtractionTab({ caseId }: { caseId: string }) {
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
