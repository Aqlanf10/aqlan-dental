"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Presentation, CheckCircle2, Circle, FileDown, Loader2, Sparkles } from "lucide-react";
import api from "@/lib/api";
import { useOrthoOverview } from "@/hooks/useOrtho";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { cn } from "@/lib/utils";

interface Checklist {
  extraoralFrontal?: boolean; extraoralProfile?: boolean; extraoralSmile?: boolean;
  intraoralFrontal?: boolean; intraoralRight?: boolean; intraoralLeft?: boolean;
  upperOcclusal?: boolean; lowerOcclusal?: boolean; opg?: boolean; lateralCeph?: boolean; studyModels?: boolean;
}

/** Reports / Case-Presentation tab. Sprint 1: a readiness checklist of what the
 *  PowerPoint case presentation will need + links to the existing PDF reports.
 *  The PPTX generator itself lands in a later sprint. */
export function CasePresentationPanel({ caseId }: { caseId: string }) {
  const [busy, setBusy] = useState<string | null>(null);

  const overviewQ = useOrthoOverview(caseId);
  const checklistQ = useQuery({
    queryKey: ["ortho-checklist", caseId],
    enabled: !!caseId, retry: false,
    queryFn: async () => (await api.get<Checklist>(`/api/ortho-cases/${caseId}/checklist`)).data,
  });
  const modelQ = useQuery({
    queryKey: ["ortho-model-latest", caseId],
    enabled: !!caseId, retry: false,
    queryFn: async () => {
      try { return (await api.get<{ id: string } | null>(`/api/ortho-cases/${caseId}/model-analyses/latest`)).data; }
      catch { return null; }
    },
  });

  const o = (overviewQ.data ?? {}) as Record<string, unknown>;
  const c = checklistQ.data ?? {};
  const bool = (v: unknown) => v === true;
  const num = (v: unknown) => (typeof v === "number" ? v : 0);

  const items: { label: string; ready: boolean; note?: string }[] = [
    { label: "صور خارج الفم", ready: bool(c.extraoralFrontal) && bool(c.extraoralProfile) && bool(c.extraoralSmile) },
    { label: "صور داخل الفم", ready: bool(c.intraoralFrontal) && bool(c.intraoralRight) && bool(c.intraoralLeft) && bool(c.upperOcclusal) && bool(c.lowerOcclusal) },
    { label: "أشعة بانوراما", ready: bool(c.opg) },
    { label: "أشعة سيفالو", ready: bool(c.lateralCeph) || num(o.cephAnalysesCount) > 0 },
    { label: "تحليل النماذج / Bolton", ready: Boolean(modelQ.data) },
    { label: "تشخيص معتمد", ready: bool(o.isDiagnosisApproved) },
    { label: "خطة علاج معتمدة", ready: bool(o.isTreatmentPlanApproved) },
    { label: "زيارات / صور تقدّم", ready: num(o.visitsCount) > 0 },
    { label: "خطة الاحتفاظ", ready: bool(o.hasRetention) },
  ];
  const readyCount = items.filter((i) => i.ready).length;
  const latestCephId = o.latestCephAnalysisId as string | undefined;
  const hasModel = Boolean(modelQ.data);

  const download = async (key: string, url: string, filename: string) => {
    setBusy(key);
    try { await downloadPdfFromApi(url, filename); }
    catch { /* download helper surfaces errors; allow retry */ }
    finally { setBusy(null); }
  };

  const reports: { key: string; label: string; url: string; filename: string; enabled: boolean }[] = [
    { key: "summary", label: "ملخّص الحالة PDF", url: `/api/ortho-cases/${caseId}/case-summary/report/pdf`, filename: `case-summary-${caseId}.pdf`, enabled: true },
    { key: "model", label: "تحليل النماذج PDF", url: `/api/ortho-cases/${caseId}/model-analyses/latest/report/pdf`, filename: `model-analysis-${caseId}.pdf`, enabled: hasModel },
    { key: "ceph", label: "تقرير السيفالو PDF", url: `/api/ceph/${latestCephId}/report/pdf`, filename: `ceph-${latestCephId}.pdf`, enabled: Boolean(latestCephId) },
  ];

  const loading = overviewQ.isLoading || checklistQ.isLoading;

  return (
    <div className="space-y-5" dir="rtl">
      <div className="flex items-center gap-2">
        <Presentation className="h-5 w-5 text-clinic-blue" />
        <h3 className="text-sm font-bold text-clinic-navy">عرض الحالة والتقارير</h3>
      </div>

      {/* Readiness checklist */}
      <div className="rounded-lg border border-gray-200 p-4">
        <div className="mb-3 flex items-center justify-between">
          <h4 className="text-xs font-semibold text-gray-600">جاهزية عرض الحالة</h4>
          <span className={cn("rounded-full px-2 py-0.5 text-[11px] font-bold",
            readyCount === items.length ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700")}>
            {readyCount}/{items.length} جاهز
          </span>
        </div>
        {loading ? (
          <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
        ) : (
          <ul className="grid gap-1.5 sm:grid-cols-2">
            {items.map((i) => (
              <li key={i.label} className="flex items-center gap-2 text-sm">
                {i.ready ? <CheckCircle2 className="h-4 w-4 text-green-500" /> : <Circle className="h-4 w-4 text-gray-300" />}
                <span className={i.ready ? "text-gray-800" : "text-gray-400"}>{i.label}</span>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Ready reports */}
      <div className="rounded-lg border border-gray-200 p-4 space-y-2">
        <h4 className="text-xs font-semibold text-gray-600">التقارير الجاهزة (PDF)</h4>
        <div className="flex flex-wrap gap-2">
          {reports.map((r) => (
            <button key={r.key} onClick={() => download(r.key, r.url, r.filename)}
              disabled={!r.enabled || busy !== null}
              title={r.enabled ? undefined : "غير متاح بعد لهذه الحالة"}
              className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50">
              {busy === r.key ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileDown className="h-3.5 w-3.5" />}
              {r.label}
            </button>
          ))}
        </div>
        <span className="text-[11px] text-gray-400">تقارير تحليل الصور متاحة في تبويب «تحليل الصور».</span>
      </div>

      {/* PPTX placeholder */}
      <div className="rounded-lg border border-dashed border-clinic-blue/40 bg-clinic-blue-50/40 p-4">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <Sparkles className="h-4 w-4 text-clinic-blue" />
            <span className="text-sm font-medium text-clinic-navy">إنشاء عرض الحالة (PowerPoint)</span>
          </div>
          <button disabled
            className="cursor-not-allowed rounded-lg bg-gray-200 px-3 py-1.5 text-xs font-medium text-gray-500">
            قريبًا
          </button>
        </div>
        <p className="mt-2 text-[11px] text-gray-500">
          سيولّد عرضًا أكاديميًا (PowerPoint) من بيانات الحالة المعتمدة وصورها المحضّرة. أكمل عناصر الجاهزية أعلاه أولًا.
        </p>
      </div>
    </div>
  );
}
