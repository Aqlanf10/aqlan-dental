"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { CheckCircle2, CircleAlert, ListPlus, Loader2 } from "lucide-react";
import api from "@/lib/api";
import { buildCephAssessment, type CephAssessmentFinding } from "@/lib/cephAssessment";
import type { CephDiagnosis, CephMeasurement } from "@/types/ceph";
import { cn } from "@/lib/utils";

interface Props {
  analysisId: string;
  orthoCaseId: string;
  analysisApproved: boolean;
  diagnosis: CephDiagnosis | null;
  measurements: CephMeasurement[];
}

interface ExistingProblem {
  description: string;
}

interface ImportResult {
  added: number;
  skippedExisting: number;
  items: Array<{ description: string }>;
}

const SECTIONS = [
  { key: "skeletal", label: "التقييم الهيكلي" },
  { key: "dental", label: "التقييم السني" },
  { key: "soft_tissue", label: "تقييم الأنسجة الرخوة" },
] as const;

export function CephAssessmentPanel({
  analysisId,
  orthoCaseId,
  analysisApproved,
  diagnosis,
  measurements,
}: Props) {
  const findings = useMemo(() => buildCephAssessment(diagnosis, measurements), [diagnosis, measurements]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [existing, setExisting] = useState<Set<string>>(new Set());
  const [loadingExisting, setLoadingExisting] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoadingExisting(true);
    api.get<ExistingProblem[]>(`/api/ortho-cases/${orthoCaseId}/problem-list`)
      .then(response => {
        if (!cancelled) setExisting(new Set(response.data.map(item => item.description.trim().toLocaleLowerCase())));
      })
      .catch(() => {
        if (!cancelled) setError("تعذر تحميل قائمة المشكلات الحالية");
      })
      .finally(() => {
        if (!cancelled) setLoadingExisting(false);
      });
    return () => { cancelled = true; };
  }, [orthoCaseId]);

  const diagnosisApproved = diagnosis?.doctorApproved === true;
  const canTransfer = analysisApproved && diagnosisApproved;
  const selectedFindings = findings.filter(finding => selected.has(finding.id));

  const toggle = (finding: CephAssessmentFinding) => {
    if (!finding.isProblem || existing.has(finding.description.trim().toLocaleLowerCase())) return;
    setSelected(current => {
      const next = new Set(current);
      if (next.has(finding.id)) next.delete(finding.id);
      else next.add(finding.id);
      return next;
    });
    setResult(null);
  };

  const transfer = async () => {
    if (!canTransfer || selectedFindings.length === 0 || submitting) return;
    setSubmitting(true);
    setError(null);
    setResult(null);
    try {
      const response = await api.post<ImportResult>(`/api/ceph/${analysisId}/assessment/problem-list`, {
        items: selectedFindings.map(finding => ({
          category: finding.section,
          description: finding.description,
          severity: finding.severity,
        })),
      });
      setResult(response.data);
      setExisting(current => {
        const next = new Set(current);
        selectedFindings.forEach(finding => next.add(finding.description.trim().toLocaleLowerCase()));
        return next;
      });
      setSelected(new Set());
    } catch (requestError) {
      const message = (requestError as { response?: { data?: { message?: string } } })
        .response?.data?.message;
      setError(message ?? "تعذر نقل العناصر إلى قائمة المشكلات");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="min-h-0 flex-1 overflow-y-auto px-1 pb-3">
        {!canTransfer && (
          <div className="mb-3 flex items-start gap-2 border-b border-amber-200 bg-amber-50 px-3 py-2 text-[10px] text-amber-800">
            <CircleAlert className="mt-0.5 h-3.5 w-3.5 flex-shrink-0" />
            <span>
              {!analysisApproved ? "اعتماد التحليل مطلوب قبل التسليم." : "موافقة الطبيب على التشخيص مطلوبة قبل التسليم."}
            </span>
          </div>
        )}

        {SECTIONS.map(section => {
          const sectionFindings = findings.filter(finding => finding.section === section.key);
          return (
            <section key={section.key} className="border-b border-gray-100 py-3 last:border-b-0">
              <h3 className="mb-2 text-[11px] font-bold text-gray-900">{section.label}</h3>
              {sectionFindings.length === 0 ? (
                <p className="text-[10px] text-gray-400">لا توجد نتائج محفوظة في هذا القسم.</p>
              ) : (
                <div className="space-y-1.5">
                  {sectionFindings.map(finding => {
                    const alreadyAdded = existing.has(finding.description.trim().toLocaleLowerCase());
                    const selectable = canTransfer && finding.isProblem && !alreadyAdded && !loadingExisting;
                    return (
                      <label
                        key={finding.id}
                        className={cn(
                          "flex items-start gap-2 rounded-md border px-2.5 py-2",
                          finding.isProblem ? "border-amber-200 bg-amber-50/50" : "border-emerald-100 bg-emerald-50/50",
                          selectable && "cursor-pointer hover:border-clinic-blue",
                        )}
                      >
                        {finding.isProblem ? (
                          <input
                            type="checkbox"
                            checked={selected.has(finding.id)}
                            disabled={!selectable}
                            onChange={() => toggle(finding)}
                            className="mt-0.5 h-3.5 w-3.5 accent-clinic-blue"
                            aria-label={`إضافة ${finding.label}`}
                          />
                        ) : (
                          <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 flex-shrink-0 text-emerald-500" />
                        )}
                        <span className="min-w-0 flex-1">
                          <span className="flex items-center justify-between gap-2">
                            <span className="text-[10px] font-semibold text-gray-800">{finding.label}</span>
                            <span className="font-mono text-[9px] text-gray-500">{finding.value}</span>
                          </span>
                          {alreadyAdded && <span className="mt-0.5 block text-[9px] text-emerald-700">موجودة في قائمة المشكلات</span>}
                        </span>
                      </label>
                    );
                  })}
                </div>
              )}
            </section>
          );
        })}

        {error && <p className="mt-2 text-[10px] text-red-600">{error}</p>}
        {result && (
          <p className="mt-2 text-[10px] text-emerald-700">
            تمت إضافة {result.added}، وتجاوز {result.skippedExisting} موجودة مسبقاً.
          </p>
        )}
      </div>

      <div className="flex flex-shrink-0 items-center gap-2 border-t border-gray-100 pt-2">
        <button
          type="button"
          onClick={transfer}
          disabled={!canTransfer || selectedFindings.length === 0 || submitting || loadingExisting}
          className="flex flex-1 items-center justify-center gap-1.5 rounded-md bg-clinic-blue px-3 py-2 text-[10px] font-bold text-white hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
        >
          {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <ListPlus className="h-3.5 w-3.5" />}
          إضافة المحدد ({selectedFindings.length})
        </button>
        <Link
          href={`/ortho/${orthoCaseId}?tab=problems`}
          className="rounded-md border border-gray-200 px-3 py-2 text-[10px] font-semibold text-gray-600 hover:bg-gray-50"
        >
          قائمة المشكلات
        </Link>
      </div>
    </div>
  );
}
