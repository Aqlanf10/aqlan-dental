"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Microscope, ExternalLink, CheckCircle2, Loader2 } from "lucide-react";
import api from "@/lib/api";
import { formatArabicDate } from "@/lib/utils";

interface ModelAnalysis {
  id: string;
  analysisDate?: string;
  boltonOverall?: number | null;
  boltonAnterior?: number | null;
  upperAld?: number | null;
  lowerAld?: number | null;
  pontIndex?: number | null;
  dentitionStage?: string | null;
  approvedAt?: string | null;
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-gray-100 bg-gray-50/60 px-3 py-2">
      <div className="text-[11px] text-gray-500">{label}</div>
      <div className="font-mono text-sm font-bold text-gray-900" dir="ltr">{value}</div>
    </div>
  );
}

/** Surfaces the existing cast / dental-model analysis (PR #364) inside the case
 *  workspace as a read summary + a link to the full calculator. No rebuild. */
export function CastAnalysisPanel({ caseId }: { caseId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ["ortho-model-latest", caseId],
    enabled: !!caseId,
    retry: false,
    queryFn: async () => {
      try {
        return (await api.get<ModelAnalysis | null>(`/api/ortho-cases/${caseId}/model-analyses/latest`)).data;
      } catch {
        return null; // 404 / none yet
      }
    },
  });

  const fullToolHref = `/ortho/${caseId}/model-analysis`;
  const num = (v: number | null | undefined, suffix = "") =>
    v === null || v === undefined ? "—" : `${v}${suffix}`;

  if (isLoading) {
    return <div className="flex items-center gap-2 py-10 text-sm text-gray-400"><Loader2 className="h-4 w-4 animate-spin" />جارٍ التحميل…</div>;
  }

  return (
    <div className="space-y-4" dir="rtl">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="flex items-center gap-2 text-sm font-bold text-clinic-navy">
          <Microscope className="h-4 w-4 text-clinic-blue" />تحليل النماذج (Cast)
        </h3>
        <Link href={fullToolHref}
          className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white hover:opacity-90">
          <ExternalLink className="h-3.5 w-3.5" />
          {data ? "فتح / تحرير التحليل" : "إنشاء تحليل نماذج"}
        </Link>
      </div>

      {!data ? (
        <div className="rounded-lg border border-dashed border-gray-300 py-10 text-center text-sm text-gray-400">
          لا يوجد تحليل نماذج محفوظ بعد لهذه الحالة.
        </div>
      ) : (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
            {data.analysisDate && <span>التاريخ: {formatArabicDate(data.analysisDate)}</span>}
            {data.dentitionStage && <span className="rounded bg-gray-100 px-2 py-0.5">{data.dentitionStage}</span>}
            {data.approvedAt && (
              <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-green-700">
                <CheckCircle2 className="h-3 w-3" />معتمد
              </span>
            )}
          </div>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            <Stat label="Bolton الكلي" value={num(data.boltonOverall, "%")} />
            <Stat label="Bolton الأمامي" value={num(data.boltonAnterior, "%")} />
            <Stat label="Pont" value={num(data.pontIndex)} />
            <Stat label="ALD علوي" value={num(data.upperAld, " مم")} />
            <Stat label="ALD سفلي" value={num(data.lowerAld, " مم")} />
          </div>
          <p className="text-[11px] text-gray-400">
            ملخّص للقراءة فقط — افتح الأداة الكاملة للحساب والتحرير والاعتماد.
          </p>
        </div>
      )}
    </div>
  );
}
