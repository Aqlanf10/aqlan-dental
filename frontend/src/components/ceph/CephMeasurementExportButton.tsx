"use client";

import { Sheet } from "lucide-react";
import type { CephAnalysis } from "@/types/ceph";
import { downloadCephMeasurementCsv } from "@/lib/cephMeasurementCsv";

interface Props {
  analysis: CephAnalysis;
  placedCount: number;
  hasUnsavedEdits: boolean;
}

export function CephMeasurementExportButton({
  analysis,
  placedCount,
  hasUnsavedEdits,
}: Props) {
  const hasSavedMeasurements = Boolean(analysis.measurements?.length);
  const disabled = !analysis.isApproved
    || placedCount === 0
    || hasUnsavedEdits
    || !hasSavedMeasurements;

  const title = !analysis.isApproved
    ? "لا يمكن تصدير القياسات قبل اعتماد الطبيب للتحليل"
    : placedCount === 0
      ? "ضع المعالم واحسب القياسات أولًا"
      : hasUnsavedEdits || !hasSavedMeasurements
        ? "اضغط «احسب» لحفظ المعالم والقياسات قبل التصدير"
        : "تصدير جدول القياسات المحفوظة بصيغة CSV متوافقة مع Excel";

  const handleExport = () => {
    if (disabled) return;
    downloadCephMeasurementCsv({
      analysisId: analysis.id,
      patientName: analysis.patientName,
      caseNumber: analysis.caseNumber,
      analysisDate: analysis.analysisDate,
      measurements: analysis.measurements ?? [],
    });
  };

  return (
    <button
      type="button"
      onClick={handleExport}
      disabled={disabled}
      title={title}
      className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 disabled:opacity-50 disabled:cursor-not-allowed transition"
    >
      <Sheet className="w-3.5 h-3.5" />
      تصدير القياسات CSV
    </button>
  );
}
