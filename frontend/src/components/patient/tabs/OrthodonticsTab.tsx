"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Activity, Camera, ClipboardCheck, ShieldCheck, Calendar,
  Wrench, ChevronLeft, CheckCircle2, AlertCircle, FileImage
} from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { EmptyState } from "./EmptyState";
import { cn } from "@/lib/utils";

interface OrthoCaseDto {
  id: string;
  caseNumber: string;
  applianceType?: string;
  status: string;
  stagePercentage: number;
  doctorName?: string;
  startDate?: string;
}

interface OrthoOverviewDto {
  hasClinicalExam: boolean;
  problemsCount: number;
  hasDiagnosis: boolean;
  isDiagnosisApproved: boolean;
  hasTreatmentPlan: boolean;
  isTreatmentPlanApproved: boolean;
  treatmentPlansCount: number;
  completedStages: number;
  totalStages: number;
  visitsCount: number;
  photosCount: number;
  cephAnalysesCount: number;
  hasRetention: boolean;
  checklistCompleted: number;
  checklistTotal: number;
  contractId?: string;
  contractTotal?: number;
  contractPaid?: number;
  contractRemaining?: number;
  latestVisitDate?: string;
  nextAppointmentDate?: string;
}

const ORTHO_STATUS_LABELS: Record<string, string> = {
  active: "نشطة",
  completed: "مكتملة",
  cancelled: "ملغاة",
};

const ORTHO_STATUS_COLORS: Record<string, string> = {
  active: "bg-emerald-50 text-emerald-700",
  completed: "bg-blue-50 text-blue-700",
  cancelled: "bg-gray-100 text-gray-500",
};

interface OrthodonticsTabProps {
  patientId: string;
}

export function OrthodonticsTab({ patientId }: OrthodonticsTabProps) {
  const [cases, setCases] = useState<OrthoCaseDto[]>([]);
  const [overviews, setOverviews] = useState<Record<string, OrthoOverviewDto>>({});
  const [failedOverviewIds, setFailedOverviewIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [overviewLoading, setOverviewLoading] = useState(false);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [overviewError, setOverviewError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    let cancelled = false;

    const loadCases = async () => {
      setLoading(true);
      setOverviewLoading(false);
      setFetchError(null);
      setOverviewError(null);
      setCases([]);
      setOverviews({});
      setFailedOverviewIds([]);

      try {
        const response = await api.get<OrthoCaseDto[]>(`/api/ortho-cases?patientId=${patientId}`);
        if (cancelled) return;

        const nextCases = response.data;
        setCases(nextCases);
        setLoading(false);

        if (nextCases.length === 0) return;

        setOverviewLoading(true);
        const overviewResults = await Promise.allSettled(
          nextCases.map((orthoCase) =>
            api.get<OrthoOverviewDto>(`/api/ortho-cases/${orthoCase.id}/overview`)
          )
        );
        if (cancelled) return;

        const nextOverviews: Record<string, OrthoOverviewDto> = {};
        const failures: string[] = [];
        overviewResults.forEach((result, index) => {
          const caseId = nextCases[index].id;
          if (result.status === "fulfilled") {
            nextOverviews[caseId] = result.value.data;
          } else {
            failures.push(caseId);
          }
        });

        setOverviews(nextOverviews);
        setFailedOverviewIds(failures);
        if (failures.length > 0) {
          setOverviewError(
            failures.length === nextCases.length
              ? "تعذر تحميل تفاصيل الحالات التقويمية — أعد المحاولة"
              : `تعذر تحميل تفاصيل ${failures.length} من الحالات التقويمية`
          );
        }
        setOverviewLoading(false);
      } catch (error: unknown) {
        if (cancelled) return;
        setCases([]);
        setFetchError(extractErrorMessage(error, "فشل تحميل الحالات التقويمية"));
        setLoading(false);
      }
    };

    void loadCases();
    return () => { cancelled = true; };
  }, [patientId, retryKey]);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-40 bg-[#f1f5f9] rounded-xl" />
        ))}
      </div>
    );
  }

  if (fetchError) {
    return (
      <div role="alert" className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">{fetchError}</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }

  if (cases.length === 0) {
    return (
      <EmptyState
        icon={Activity}
        title="لا توجد حالات تقويمية"
        description="لم يتم تسجيل أي حالات تقويمية لهذا المريض بعد. يمكنك إنشاء حالة جديدة لبدء متابعة العلاج التقويمي."
        actionLabel="حالة تقويمية جديدة"
        actionHref={`/ortho/new?patientId=${patientId}`}
        actionVariant="outline"
      />
    );
  }

  return (
    <div className="space-y-4">
      {overviewError && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-center">
          <p className="text-sm text-amber-800">{overviewError}</p>
          <button
            type="button"
            onClick={() => setRetryKey((key) => key + 1)}
            className="mt-1 text-xs font-semibold text-blue-600 underline decoration-dotted"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {cases.map((c) => {
        const ov = overviews[c.id];
        const isActive = c.status === "active";

        return (
          <div
            key={c.id}
            className={cn(
              "rounded-xl border bg-white overflow-hidden transition",
              isActive ? "border-[#3d7ab5]/30 shadow-sm" : "border-[#e8f0f9]"
            )}
          >
            {/* ── Case Header ── */}
            <div className="flex items-center justify-between p-4 bg-gradient-to-l from-[#f7fafd] to-white">
              <div className="flex items-center gap-3">
                <div
                  className={cn(
                    "w-11 h-11 rounded-lg flex items-center justify-center flex-shrink-0",
                    isActive ? "bg-[#3d7ab5]/10" : "bg-purple-50"
                  )}
                >
                  <Wrench className={cn("w-5 h-5", isActive ? "text-[#3d7ab5]" : "text-purple-500")} />
                </div>
                <div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-bold text-[#1a3a5c]">
                      حالة تقويم {c.caseNumber}
                    </span>
                    <span
                      className={cn(
                        "text-xs px-2 py-0.5 rounded-full font-medium",
                        ORTHO_STATUS_COLORS[c.status] ?? "bg-gray-100 text-gray-500"
                      )}
                    >
                      {ORTHO_STATUS_LABELS[c.status] ?? c.status}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 mt-0.5 text-xs text-[#64748b]">
                    {c.applianceType && <span>{c.applianceType}</span>}
                    {c.doctorName && <span>• {c.doctorName}</span>}
                    {c.startDate && <span>• {c.startDate}</span>}
                  </div>
                </div>
              </div>
              <Link
                href={`/ortho/${c.id}`}
                className="flex items-center gap-1 text-sm font-medium text-[#3d7ab5] hover:text-[#1a3a5c] transition"
              >
                التفاصيل
                <ChevronLeft className="w-4 h-4" />
              </Link>
            </div>

            {/* ── Progress Bar ── */}
            <div className="px-4 pb-3">
              <div className="flex items-center gap-2">
                <div className="flex-1 h-2 bg-[#dce8f5] rounded-full overflow-hidden">
                  <div
                    className={cn(
                      "h-full rounded-full transition-all",
                      c.stagePercentage >= 100 ? "bg-emerald-500" : "bg-[#3d7ab5]"
                    )}
                    style={{ width: `${Math.min(c.stagePercentage, 100)}%` }}
                  />
                </div>
                <span className="text-xs font-bold text-[#1a3a5c] w-10 text-end">
                  {c.stagePercentage}%
                </span>
              </div>
              {ov && ov.totalStages > 0 && (
                <p className="text-xs text-[#94a3b8] mt-1">
                  المرحلة {ov.completedStages} من {ov.totalStages}
                </p>
              )}
            </div>

            {overviewLoading && !ov && (
              <div className="mx-4 mb-4 h-16 rounded-xl bg-slate-100 animate-pulse" aria-label="جار تحميل تفاصيل الحالة" />
            )}
            {!overviewLoading && failedOverviewIds.includes(c.id) && (
              <div className="mx-4 mb-4 rounded-lg border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
                تعذر تحميل تفاصيل هذه الحالة
              </div>
            )}

            {/* ── Status Chips (only if overview loaded) ── */}
            {ov && (
              <div className="px-4 pb-4">
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
                  {/* Diagnosis */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.hasDiagnosis
                        ? ov.isDiagnosisApproved
                          ? "bg-emerald-50 text-emerald-700"
                          : "bg-amber-50 text-amber-700"
                        : "bg-gray-50 text-gray-400"
                    )}
                  >
                    <ClipboardCheck className="w-3.5 h-3.5" />
                    <span className="truncate">
                      {ov.hasDiagnosis
                        ? ov.isDiagnosisApproved
                          ? "تشخيص معتمد"
                          : "تشخيص مسوّدة"
                        : "بدون تشخيص"}
                    </span>
                  </div>

                  {/* Treatment Plan */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.hasTreatmentPlan
                        ? ov.isTreatmentPlanApproved
                          ? "bg-emerald-50 text-emerald-700"
                          : "bg-amber-50 text-amber-700"
                        : "bg-gray-50 text-gray-400"
                    )}
                  >
                    <Activity className="w-3.5 h-3.5" />
                    <span className="truncate">
                      {ov.hasTreatmentPlan
                        ? ov.isTreatmentPlanApproved
                          ? "خطة معتمدة"
                          : `خطة (${ov.treatmentPlansCount})`
                        : "بدون خطة"}
                    </span>
                  </div>

                  {/* Clinical Photos */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.photosCount > 0 ? "bg-blue-50 text-blue-700" : "bg-gray-50 text-gray-400"
                    )}
                  >
                    <Camera className="w-3.5 h-3.5" />
                    <span className="truncate">
                      {ov.photosCount > 0 ? `${ov.photosCount} صورة` : "بدون صور"}
                    </span>
                  </div>

                  {/* Retention */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.hasRetention ? "bg-purple-50 text-purple-700" : "bg-gray-50 text-gray-400"
                    )}
                  >
                    <ShieldCheck className="w-3.5 h-3.5" />
                    <span className="truncate">
                      {ov.hasRetention ? "مرحلة الاحتفاظ" : "بدون احتفاظ"}
                    </span>
                  </div>
                </div>

                {/* Second row: Exam, Visits, Checklist, Finance */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 mt-2">
                  {/* Clinical Exam */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.hasClinicalExam ? "bg-teal-50 text-teal-700" : "bg-gray-50 text-gray-400"
                    )}
                  >
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    <span className="truncate">
                      {ov.hasClinicalExam ? "فحص سريري" : "بدون فحص"}
                    </span>
                  </div>

                  {/* Visits Count */}
                  <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs bg-gray-50 text-[#64748b]">
                    <Calendar className="w-3.5 h-3.5" />
                    <span className="truncate">{ov.visitsCount} زيارة</span>
                  </div>

                  {/* Records Checklist */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.checklistCompleted === ov.checklistTotal
                        ? "bg-emerald-50 text-emerald-700"
                        : "bg-amber-50 text-amber-700"
                    )}
                  >
                    <AlertCircle className="w-3.5 h-3.5" />
                    <span className="truncate">
                      سجلات {ov.checklistCompleted}/{ov.checklistTotal}
                    </span>
                  </div>

                  {/* Contract / Finance */}
                  <div
                    className={cn(
                      "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                      ov.contractId ? "bg-[#3d7ab5]/10 text-[#3d7ab5]" : "bg-gray-50 text-gray-400"
                    )}
                  >
                    <FileImage className="w-3.5 h-3.5" />
                    <span className="truncate">
                      {ov.contractId
                        ? `متبقي ${((ov.contractRemaining ?? 0)).toLocaleString()}`
                        : "بدون عقد"}
                    </span>
                  </div>
                </div>

                {/* Latest / Next dates */}
                {(ov.latestVisitDate || ov.nextAppointmentDate) && (
                  <div className="flex items-center gap-4 mt-3 text-xs text-[#94a3b8]">
                    {ov.latestVisitDate && (
                      <span>آخر زيارة: {ov.latestVisitDate}</span>
                    )}
                    {ov.nextAppointmentDate && (
                      <span>الموعد القادم: {ov.nextAppointmentDate}</span>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        );
      })}

      {/* New case button */}
      <Link
        href={`/ortho/new?patientId=${patientId}`}
        className="flex items-center justify-center gap-2 p-3 border-2 border-dashed border-[#3d7ab5]/30 rounded-xl text-sm font-medium text-[#3d7ab5] hover:bg-[#3d7ab5]/5 transition"
      >
        <Activity className="w-4 h-4" />
        حالة تقويمية جديدة
      </Link>
    </div>
  );
}
