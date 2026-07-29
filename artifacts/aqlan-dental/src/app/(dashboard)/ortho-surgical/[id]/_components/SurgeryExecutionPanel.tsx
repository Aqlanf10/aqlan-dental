
import { useEffect, useState, useCallback } from "react";
import Link from "@/lib/nextLinkCompat";
import { Scissors, CheckCircle2, Circle, ExternalLink } from "lucide-react";
import type { OrthoSurgicalSurgerySummary } from "@/types/orthoSurgical";
import { SURGERY_CASE_STATUS_LABELS } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface SurgeryExecutionPanelProps {
  orthoSurgicalCaseId: string;
}

/**
 * Sprint A6 — an inline glance at surgery execution (preop/operative/postop) once
 * create-surgery-case has linked a SurgeryCase. The full record stays at /surgery/{id};
 * this only summarizes it and links out, mirroring nothing else in the surgery module.
 */
export function SurgeryExecutionPanel({ orthoSurgicalCaseId }: SurgeryExecutionPanelProps) {
  const [summary, setSummary] = useState<OrthoSurgicalSurgerySummary | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchSummary = useCallback(async () => {
    try {
      const res = await api.get<OrthoSurgicalSurgerySummary>(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/surgery-summary`
      );
      setSummary(res.data);
    } catch {
      /* silent — this panel is supplementary */
    } finally {
      setLoading(false);
    }
  }, [orthoSurgicalCaseId]);

  useEffect(() => { fetchSummary(); }, [fetchSummary]);

  if (loading || !summary?.linked) return null;

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
          <Scissors className="w-4 h-4 text-[#3d7ab5]" /> تنفيذ الجراحة
        </h2>
        <Link href={`/surgery/${summary.id}`}
          className="flex items-center gap-1 text-xs font-medium text-[#3d7ab5] hover:underline">
          الملف الجراحي الكامل <ExternalLink className="w-3 h-3" />
        </Link>
      </div>

      <div className="flex items-center gap-2 flex-wrap text-sm">
        <span className="font-mono text-xs font-semibold text-[#3d7ab5]">{summary.caseNumber}</span>
        <span className="text-gray-300">·</span>
        <span className="text-gray-600">{summary.surgeryType}</span>
        <span className="text-gray-300">·</span>
        <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-blue-50 text-blue-700">
          {SURGERY_CASE_STATUS_LABELS[summary.status ?? ""] ?? summary.status}
        </span>
        {summary.doctorName && <span className="text-xs text-gray-500">{summary.doctorName}</span>}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
        {[
          { label: "ما قبل الجراحة", ok: !!summary.preop?.consentSigned, note: summary.preop?.surgeryDate ? formatArabicDate(summary.preop.surgeryDate) : null },
          { label: "تقرير الجراحة", ok: !!summary.operative?.approvedAt, note: summary.operative?.outcome ?? null },
          { label: "ما بعد الجراحة", ok: !!summary.postop?.hasInstructions, note: null },
        ].map((row) => (
          <div key={row.label} className={cn(
            "flex flex-col gap-1 px-3 py-2 rounded-lg text-xs",
            row.ok ? "bg-emerald-50 text-emerald-700" : "bg-gray-50 text-gray-400"
          )}>
            <span className="flex items-center gap-1.5 font-medium">
              {row.ok ? <CheckCircle2 className="w-3.5 h-3.5" /> : <Circle className="w-3.5 h-3.5" />}
              {row.label}
            </span>
            {row.note && <span className="truncate">{row.note}</span>}
          </div>
        ))}
      </div>
    </div>
  );
}
