"use client";

import { useState, useCallback } from "react";
import { FileDown, Loader2 } from "lucide-react";
import type { CephAnalysis } from "@/types/ceph";
import { generateCephReportPdf } from "@/utils/cephPdf";

interface Props {
  analysis: CephAnalysis;
  /** PNG data URL exported from the Fabric.js canvas */
  canvasDataUrl?: string;
  patientName: string;
  centerName?: string;
}

export function CephPdfExport({
  analysis,
  canvasDataUrl,
  patientName,
  centerName,
}: Props) {
  const [generating, setGenerating] = useState(false);

  const handleExport = useCallback(async () => {
    setGenerating(true);
    try {
      // Small delay to let UI update
      await new Promise((r) => setTimeout(r, 50));

      const doc = generateCephReportPdf(
        analysis,
        canvasDataUrl ?? null,
        patientName,
        centerName
      );

      // Auto-download
      const fileName = `ceph-report-${patientName.replace(/\s+/g, "-")}-${new Date().toISOString().slice(0, 10)}.pdf`;
      doc.save(fileName);
    } catch (err) {
      console.error("PDF export failed:", err);
    } finally {
      setGenerating(false);
    }
  }, [analysis, canvasDataUrl, patientName, centerName]);

  return (
    <button
      onClick={handleExport}
      disabled={generating || !analysis.measurements?.length}
      className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-lg bg-rose-600 text-white hover:bg-rose-700 disabled:opacity-50 disabled:cursor-not-allowed transition"
      title="تصدير تقرير PDF"
    >
      {generating ? (
        <Loader2 className="w-3.5 h-3.5 animate-spin" />
      ) : (
        <FileDown className="w-3.5 h-3.5" />
      )}
      {generating ? "جارٍ التصدير..." : "PDF"}
    </button>
  );
}
