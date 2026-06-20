"use client";

import Link from "next/link";
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  FileText,
  Plus,
  ScanLine,
  Target,
} from "lucide-react";
import { cn, formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import { financeV3ContractsUrl } from "@/lib/financeRoutes";
import {
  useCaseCephAnalyses,
  useCaseCephAnalysis,
  useDiagnosis,
  useOrthoOverview,
} from "@/hooks/useOrtho";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import { CephReadinessBadge } from "@/components/ceph/CephReadinessBadge";
import { cephReadinessFromAnalysis } from "@/lib/cephReadiness";
import { pickLatestCeph } from "@/lib/cephSelection";
import type { Tab } from "../_lib/types";

/* ------------------------------------------------------------------ */
/*  CephCaseStatusCard — latest ceph at a glance from the case file    */
/* ------------------------------------------------------------------ */

/**
 * Surfaces the latest cephalometric analysis inside the ortho case file:
 * is it ready for a report/VTO, is the PDF report available, is the VTO ready,
 * and a direct link to open it. The "ready" verdict reuses the same saved-data
 * gate as the analysis page (image + calibration + points + measurements),
 * which needs the full record — so the detail is fetched (React Query caches it
 * and shares it with the ceph tab).
 */
function CephCaseStatusCard({
  caseId,
  onViewAll,
}: {
  caseId: string;
  onViewAll: () => void;
}) {
  const { data: analyses = [], isLoading } = useCaseCephAnalyses(caseId);
  // Same selection the deck generator uses (analysisDate DESC, then createdAt DESC).
  const latest = pickLatestCeph(analyses);
  const { data: detail } = useCaseCephAnalysis(latest?.id);

  const readiness = detail ? cephReadinessFromAnalysis(detail, false) : null;
  const hasReport = detail
    ? (detail.measurements?.length ?? 0) > 0
    : Boolean(latest?.hasMeasurements);
  // VTO requires the full saved set (image + calibration + 24 points +
  // measurements, no unsaved edits) — the same unified readiness gate, not just
  // "any landmark placed".
  const vtoReady = readiness?.ready ?? false;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5">
      <div className="flex items-center justify-between gap-2">
        <p className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <ScanLine className="h-4 w-4 text-clinic-blue" />
          حالة السيفالو
        </p>
        {analyses.length > 0 && (
          <button
            type="button"
            onClick={onViewAll}
            className="text-xs font-medium text-clinic-blue hover:underline"
          >
            عرض الكل ({analyses.length})
          </button>
        )}
      </div>

      {isLoading ? (
        <div className="mt-3 h-16 animate-pulse rounded-lg bg-gray-100" />
      ) : !latest ? (
        <div className="mt-3">
          <p className="text-sm text-gray-400">لا يوجد تحليل سيفالومتري بعد.</p>
          <Link
            href={`/ceph/new?orthoCaseId=${caseId}`}
            className="mt-2 inline-flex items-center gap-1.5 text-xs font-medium text-clinic-blue hover:underline"
          >
            <Plus className="h-3.5 w-3.5" />
            بدء تحليل سيفالو
          </Link>
        </div>
      ) : (
        <div className="mt-3 space-y-3">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-gray-800">
                {ANALYSIS_TYPE_AR[latest.analysisType] ?? latest.analysisType}
              </p>
              <p className="text-xs text-gray-500">
                {formatArabicDate(latest.analysisDate)} · {latest.landmarkCount}/24 نقطة
              </p>
            </div>
            {readiness && <CephReadinessBadge readiness={readiness} variant="compact" />}
          </div>

          <div className="flex flex-wrap gap-2 text-[11px]">
            <span
              className={cn(
                "inline-flex items-center gap-1 rounded-md px-2 py-1 font-medium",
                hasReport ? "bg-emerald-50 text-emerald-700" : "bg-gray-50 text-gray-400",
              )}
            >
              <FileText className="h-3.5 w-3.5" />
              تقرير PDF: {hasReport ? "متاح" : "غير متاح"}
            </span>
            <span
              className={cn(
                "inline-flex items-center gap-1 rounded-md px-2 py-1 font-medium",
                vtoReady ? "bg-emerald-50 text-emerald-700" : "bg-gray-50 text-gray-400",
              )}
            >
              <Target className="h-3.5 w-3.5" />
              VTO: {vtoReady ? "جاهز" : "غير جاهز"}
            </span>
          </div>

          <Link
            href={`/ceph/${latest.id}`}
            className="inline-flex items-center gap-1.5 rounded-md border border-clinic-blue/30 bg-clinic-blue/5 px-3 py-1.5 text-xs font-medium text-clinic-blue transition hover:bg-clinic-blue/10"
          >
            فتح التحليل مباشرة
            <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  OverviewPanel                                                      */
/* ------------------------------------------------------------------ */

export function OrthoOverviewTab({
  caseId,
  patientId,
  setActiveTab,
}: {
  caseId: string;
  patientId: string;
  setActiveTab: (tab: Tab) => void;
}) {
  const { data: overview } = useOrthoOverview(caseId);
  const { data: diagnosis } = useDiagnosis(caseId);

  const readiness = [
    {
      label: "الفحص السريري",
      done: overview?.hasClinicalExam,
      tab: "exam" as Tab,
    },
    {
      label: "قائمة المشاكل",
      done: (overview?.problemsCount ?? 0) > 0,
      tab: "problems" as Tab,
    },
    {
      label: "التشخيص",
      done: overview?.hasDiagnosis,
      tab: "diagnosis" as Tab,
    },
    {
      label: "خطة العلاج",
      done: overview?.hasTreatmentPlan,
      tab: "plan" as Tab,
    },
    {
      label: "اعتماد الخطة",
      done: overview?.isTreatmentPlanApproved,
      tab: "plan" as Tab,
    },
    {
      label: "السجلات والصور",
      done:
        (overview?.photosCount ?? 0) > 0 ||
        (overview?.cephAnalysesCount ?? 0) > 0,
      tab: "records" as Tab,
    },
  ];

  const progress =
    overview && overview.totalStages > 0
      ? Math.round((overview.completedStages / overview.totalStages) * 100)
      : 0;

  const checklistPercent =
    overview && overview.checklistTotal > 0
      ? Math.round(
          (overview.checklistCompleted / overview.checklistTotal) * 100
        )
      : 0;

  return (
    <div className="grid gap-5 lg:grid-cols-[1.35fr_0.65fr]">
      <div className="space-y-5">
        {/* Stats grid */}
        <div className="grid gap-3 md:grid-cols-5">
          {[
            ["زيارات", overview?.visitsCount ?? 0],
            ["مشاكل", overview?.problemsCount ?? 0],
            ["صور", overview?.photosCount ?? 0],
            ["تحليلات Ceph", overview?.cephAnalysesCount ?? 0],
            ["تحليلات صور الوجه", overview?.photoAnalysesCount ?? 0],
            ["إكمال السجلات", `${checklistPercent}%`],
          ].map(([label, value]) => (
            <div
              key={label}
              className="rounded-lg border border-gray-200 bg-white p-4"
            >
              <p className="text-xs text-gray-500">{label}</p>
              <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
            </div>
          ))}
        </div>

        {/* Readiness checklist */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="font-semibold text-gray-900">
                جاهزية ملف التقويم
              </h2>
              <p className="text-sm text-gray-500">
                الخطوات الأساسية قبل بدء العلاج والمتابعة المالية.
              </p>
            </div>
            <span className="rounded-full bg-clinic-blue-50 px-3 py-1 text-sm font-semibold text-clinic-blue">
              {readiness.filter((i) => i.done).length}/{readiness.length}
            </span>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            {readiness.map((item) => (
              <button
                key={item.label}
                type="button"
                onClick={() => setActiveTab(item.tab)}
                className="flex items-center justify-between rounded-lg border border-gray-200 px-4 py-3 text-start transition hover:border-clinic-blue/50 hover:bg-clinic-blue-50/40"
              >
                <span className="text-sm font-medium text-gray-800">
                  {item.label}
                </span>
                {item.done ? (
                  <CheckCircle2 className="h-5 w-5 text-green-500" />
                ) : (
                  <AlertTriangle className="h-5 w-5 text-amber-500" />
                )}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="space-y-5">
        {/* Stage progress */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">تقدم المراحل</p>
          <div className="mt-4 h-2 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-clinic-blue transition-all"
              style={{ width: `${progress}%` }}
            />
          </div>
          <p className="mt-2 text-sm text-gray-500">
            {overview?.completedStages ?? 0} من{" "}
            {overview?.totalStages ?? 0} مراحل مكتملة
          </p>
        </div>

        {/* Cephalometric status — latest analysis readiness at a glance */}
        <CephCaseStatusCard caseId={caseId} onViewAll={() => setActiveTab("ceph")} />

        {/* Finance card */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">
            المالية المرتبطة
          </p>
          {overview?.contractId ? (
            <div className="mt-3 space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-500">العقد</span>
                <span>
                  {formatYemeniRiyal(overview.contractTotal ?? 0)}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">المدفوع</span>
                <span className="text-green-600">
                  {formatYemeniRiyal(overview.contractPaid ?? 0)}
                </span>
              </div>
              <div className="flex justify-between font-semibold">
                <span>المتبقي</span>
                <span className="text-red-600">
                  {formatYemeniRiyal(overview.contractRemaining ?? 0)}
                </span>
              </div>
              <Link
                href={financeV3ContractsUrl(patientId)}
                className="mt-3 inline-flex text-sm font-medium text-clinic-blue hover:underline"
              >
                فتح العقد المالي
              </Link>
            </div>
          ) : (
            <p className="mt-3 text-sm text-gray-400">
              لا يوجد عقد مالي مرتبط بالحالة.
            </p>
          )}
        </div>

        {/* Diagnosis approval status */}
        <div className="rounded-lg border border-gray-200 bg-white p-5">
          <p className="text-sm font-semibold text-gray-900">
            حالة اعتماد التشخيص
          </p>
          {diagnosis?.isApproved ? (
            <div className="mt-3 flex items-center gap-2">
              <CheckCircle2 className="h-5 w-5 text-green-500" />
              <div>
                <p className="text-sm font-medium text-green-700">
                  التشخيص معتمد
                </p>
                {diagnosis.approvedByName && (
                  <p className="text-xs text-gray-500">
                    بواسطة {diagnosis.approvedByName}
                    {diagnosis.approvedAt &&
                      ` · ${formatArabicDate(diagnosis.approvedAt)}`}
                  </p>
                )}
              </div>
            </div>
          ) : (
            <div className="mt-3 flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-amber-500" />
              <p className="text-sm text-amber-700">التشخيص غير معتمد بعد</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
