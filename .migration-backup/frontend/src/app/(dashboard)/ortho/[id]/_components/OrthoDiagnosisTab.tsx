"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
  AlertTriangle,
  ArrowRight,
  BadgeCheck,
  CheckCircle2,
  GitCompareArrows,
  Plus,
  ScanLine,
  User,
  UserSquare2,
} from "lucide-react";
import { cn, formatArabicDate } from "@/lib/utils";
import {
  useApproveDiagnosis,
  useCaseCephAnalyses,
  useCaseCephAnalysis,
  useCasePhotoAnalyses,
  useDiagnosis,
  useSaveDiagnosis,
} from "@/hooks/useOrtho";
import type { OrthoDiagnosis } from "@/types/ortho";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import { CephReadinessBadge } from "@/components/ceph/CephReadinessBadge";
import { cephReadinessFromAnalysis } from "@/lib/cephReadiness";
import { pickLatestCeph } from "@/lib/cephSelection";
import { Field, EmptyState, SaveButton } from "./_shared";
import { inputCls } from "../_lib/types";

/* ------------------------------------------------------------------ */
/*  CephPanel — cephalometric + photo-analysis workspace inside case   */
/* ------------------------------------------------------------------ */

/**
 * Ceph tab. Co-located with the Diagnosis tab (FE-20) because both depend on
 * the same ceph/photo sync state surfaced via `useDiagnosis`. The page shell
 * renders this for the `ceph` tab and `OrthoDiagnosisTab` for the `diagnosis`
 * tab.
 */
export function OrthoCephPanel({ caseId }: { caseId: string }) {
  const { data: analyses = [], isLoading, isError: analysesError, refetch: refetchAnalyses } = useCaseCephAnalyses(caseId);
  const { data: photoAnalyses = [], isLoading: photoAnalysesLoading } =
    useCasePhotoAnalyses(caseId);
  // Same selection the deck generator uses (analysisDate DESC, then createdAt DESC).
  const latest = pickLatestCeph(analyses);
  const latestProfile = photoAnalyses.find((analysis) => analysis.viewType === "profile");
  const latestFrontal = photoAnalyses.find((analysis) => analysis.viewType === "frontal");
  const { data: latestDetail } = useCaseCephAnalysis(latest?.id);
  const { data: diagnosis } = useDiagnosis(caseId);

  const keyMeasurements = ["SNA", "SNB", "ANB", "Wits", "FMA", "IMPA"]
    .map((name) => latestDetail?.measurements.find((m) => m.name === name))
    .filter((measurement) => measurement !== undefined);

  const syncState = diagnosis?.isApproved && diagnosis.isCephSyncOutdated
    ? {
        tone: "border-amber-200 bg-amber-50 text-amber-800",
        title: "يوجد تحليل أحدث من التشخيص المعتمد",
        detail: "حمايةً للقرار السريري لم تُعدّل النتائج المعتمدة تلقائياً. راجع التحليل الجديد قبل تحديث التشخيص.",
      }
    : diagnosis?.cephSourceAnalysisId === latest?.id && diagnosis?.cephSyncedAt
      ? {
          tone: "border-green-200 bg-green-50 text-green-800",
          title: "القياسات متزامنة مع تشخيص الحالة",
          detail: `آخر مزامنة: ${formatArabicDate(diagnosis.cephSyncedAt)}`,
        }
      : latest?.hasMeasurements
        ? {
            tone: "border-blue-200 bg-blue-50 text-blue-800",
            title: "التحليل محفوظ داخل الحالة",
            detail: "ستنتقل القياسات تلقائياً إلى التشخيص عند حفظ نقاط التحليل.",
          }
      : null;
  const photoSyncState = diagnosis?.isApproved && diagnosis.isPhotoAnalysisSyncOutdated
    ? {
        tone: "border-amber-200 bg-amber-50 text-amber-800",
        title: "يوجد تحليل صورة وجه أحدث من التشخيص المعتمد",
        detail: "لم يُكتب فوق التشخيص المعتمد. راجع تحليل البروفايل أو الصورة الأمامية ثم حدّث التشخيص سريريًا.",
      }
    : diagnosis?.photoAnalysisSyncedAt && !diagnosis.isPhotoAnalysisSyncOutdated
      ? {
          tone: "border-green-200 bg-green-50 text-green-800",
          title: "تحليل صور الوجه متزامن مع تشخيص الحالة",
          detail: `آخر مزامنة: ${formatArabicDate(diagnosis.photoAnalysisSyncedAt)}`,
        }
      : null;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold text-gray-900">مساحة السيفالو للحالة</h2>
          <p className="mt-1 text-sm text-gray-500">
            الأشعة والقياسات والتشخيص السيفالومتري مرتبطة بهذه الحالة تلقائياً.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link
            href={`/ceph/photo?orthoCaseId=${caseId}`}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
          >
            <UserSquare2 className="h-4 w-4" />
            تحليل صورة البروفايل
          </Link>
          <Link
            href={`/ceph/photo/frontal?orthoCaseId=${caseId}`}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
          >
            <User className="h-4 w-4" />
            تحليل الصورة الأمامية
          </Link>
          <Link
            href={`/ceph/new?orthoCaseId=${caseId}`}
            className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-3 py-2 text-sm font-medium text-white transition hover:opacity-90"
          >
            <Plus className="h-4 w-4" />
            تحليل سيفالو جديد
          </Link>
        </div>
      </div>

      {syncState && (
        <div className={cn("rounded-lg border px-4 py-3", syncState.tone)}>
          <p className="text-sm font-semibold">{syncState.title}</p>
          <p className="mt-1 text-xs leading-5">{syncState.detail}</p>
        </div>
      )}
      {photoSyncState && (
        <div className={cn("rounded-lg border px-4 py-3", photoSyncState.tone)}>
          <p className="text-sm font-semibold">{photoSyncState.title}</p>
          <p className="mt-1 text-xs leading-5">{photoSyncState.detail}</p>
        </div>
      )}

      {photoAnalysesLoading ? (
        <div className="grid gap-3 md:grid-cols-2">
          <div className="h-28 animate-pulse rounded-lg bg-gray-100" />
          <div className="h-28 animate-pulse rounded-lg bg-gray-100" />
        </div>
      ) : (latestProfile || latestFrontal) && (
        <div className="grid gap-3 md:grid-cols-2">
          {[
            {
              key: "profile",
              title: "تحليل البروفايل والأنسجة الرخوة",
              analysis: latestProfile,
              href: `/ceph/photo?orthoCaseId=${caseId}`,
            },
            {
              key: "frontal",
              title: "تحليل التناظر والنسب الأمامية",
              analysis: latestFrontal,
              href: `/ceph/photo/frontal?orthoCaseId=${caseId}`,
            },
          ].map((item) => (
            <section key={item.key} className="rounded-lg border border-gray-200 bg-white p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-gray-900">{item.title}</p>
                  <p className="mt-1 text-xs text-gray-500">
                    {item.analysis
                      ? `آخر حفظ: ${formatArabicDate(item.analysis.analysisDate)}`
                      : "لم يُحفظ تحليل بعد"}
                  </p>
                </div>
                <UserSquare2 className="h-5 w-5 text-clinic-blue" />
              </div>
              <Link
                href={item.href}
                className="mt-3 inline-flex items-center gap-1.5 text-xs font-medium text-clinic-blue hover:underline"
              >
                {item.analysis ? "فتح التحليل المحفوظ" : "بدء التحليل"}
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </section>
          ))}
        </div>
      )}

      {isLoading ? (
        <div className="grid gap-3 md:grid-cols-2">
          <div className="h-44 animate-pulse rounded-lg bg-gray-100" />
          <div className="h-44 animate-pulse rounded-lg bg-gray-100" />
        </div>
      ) : analysesError ? (
        // ORTHO-REQ-006: a failed fetch must never render the same "no ceph yet"
        // empty state — that hides a real server error from the doctor.
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2"
          style={{ background: "#fef2f2", borderColor: "#fecaca" }}>
          <div className="flex items-center gap-2 text-xs font-bold" style={{ color: "#b91c1c" }}>
            <AlertTriangle className="h-4 w-4 flex-shrink-0" />
            تعذر تحميل تحاليل السيفالو من الخادم
          </div>
          <button onClick={() => refetchAnalyses()}
            className="flex-shrink-0 rounded-lg px-3 py-1.5 text-xs font-bold text-white transition hover:opacity-90"
            style={{ background: "#b91c1c" }}>
            إعادة المحاولة
          </button>
        </div>
      ) : !latest ? (
        <EmptyState text="لا يوجد تحليل سيفالومتري لهذه الحالة بعد" />
      ) : (
        <>
          <div className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
            <section className="rounded-lg border border-gray-200 bg-white p-5">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-xs text-gray-500">أحدث تحليل</p>
                  <h3 className="mt-1 font-semibold text-gray-900">
                    {ANALYSIS_TYPE_AR[latest.analysisType] ?? latest.analysisType}
                  </h3>
                  <p className="mt-1 text-xs text-gray-500">
                    {formatArabicDate(latest.analysisDate)}
                  </p>
                </div>
                <span className="rounded-full bg-clinic-blue-50 px-2 py-1 text-xs font-medium text-clinic-blue">
                  {latest.landmarkCount}/24 نقطة
                </span>
              </div>

              {latestDetail && (
                <div className="mt-3">
                  <CephReadinessBadge
                    readiness={cephReadinessFromAnalysis(latestDetail, false)}
                    variant="bar"
                  />
                </div>
              )}

              <div className="mt-4 grid grid-cols-3 gap-2">
                {keyMeasurements.length > 0 ? keyMeasurements.map((measurement) => (
                  <div
                    key={measurement.name}
                    className="rounded-lg border border-gray-100 bg-gray-50 px-2 py-3 text-center"
                  >
                    <p className="text-[11px] font-medium text-gray-500">{measurement.name}</p>
                    <p className="mt-1 font-mono text-base font-bold text-gray-900" dir="ltr">
                      {measurement.value ?? "—"}{measurement.value !== null ? measurement.unit : ""}
                    </p>
                  </div>
                )) : (
                  <p className="col-span-3 py-4 text-center text-xs text-gray-400">
                    أكمل وضع النقاط وحفظها لإظهار القياسات.
                  </p>
                )}
              </div>

              {latestDetail?.diagnosis && (
                <div className="mt-4 space-y-1 rounded-lg bg-clinic-blue-50/60 p-3 text-xs text-clinic-navy">
                  <p><span className="font-semibold">الهيكلي:</span> {latestDetail.diagnosis.skeletalClass ?? "—"}</p>
                  <p><span className="font-semibold">النمط الرأسي:</span> {latestDetail.diagnosis.verticalPattern ?? "—"}</p>
                  <p><span className="font-semibold">القواطع:</span> {latestDetail.diagnosis.incisorInclination ?? "—"}</p>
                </div>
              )}

              <Link
                href={`/ceph/${latest.id}`}
                className="mt-4 inline-flex w-full items-center justify-center gap-2 rounded-lg border border-clinic-blue px-3 py-2 text-sm font-medium text-clinic-blue transition hover:bg-clinic-blue-50"
              >
                <ScanLine className="h-4 w-4" />
                فتح مساحة التحليل
              </Link>
            </section>

            <section className="overflow-hidden rounded-lg border border-gray-200 bg-white">
              <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">سجل تحاليل الحالة</h3>
                  <p className="text-xs text-gray-500">{analyses.length} تحليل محفوظ</p>
                </div>
                {analyses.length >= 2 && (
                  <Link
                    href={`/ceph/compare?baseId=${analyses[1].id}&targetId=${analyses[0].id}`}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-2.5 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
                  >
                    <GitCompareArrows className="h-3.5 w-3.5" />
                    مقارنة آخر تحليلين
                  </Link>
                )}
              </div>
              <div className="divide-y divide-gray-100">
                {analyses.map((analysis) => (
                  <div
                    key={analysis.id}
                    className="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
                  >
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        {ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}
                      </p>
                      <p className="mt-0.5 text-xs text-gray-500">
                        {formatArabicDate(analysis.analysisDate)} · {analysis.landmarkCount} نقطة
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className={cn(
                        "text-xs font-medium",
                        analysis.hasMeasurements ? "text-green-600" : "text-amber-600"
                      )}>
                        {analysis.hasMeasurements ? "القياسات مكتملة" : "قيد الإعداد"}
                      </span>
                      <Link
                        href={`/ceph/${analysis.id}`}
                        className="text-xs font-medium text-clinic-blue hover:underline"
                      >
                        فتح
                      </Link>
                    </div>
                  </div>
                ))}
              </div>
            </section>
          </div>
        </>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  DiagnosisPanel (Enhanced)                                          */
/* ------------------------------------------------------------------ */

export function OrthoDiagnosisTab({ caseId }: { caseId: string }) {
  const { data } = useDiagnosis(caseId);
  const save = useSaveDiagnosis(caseId);
  const approve = useApproveDiagnosis(caseId);
  const [form, setForm] = useState<OrthoDiagnosis>({});
  useEffect(() => setForm(data ?? {}), [data]);
  const set = <K extends keyof OrthoDiagnosis>(
    key: K,
    value: OrthoDiagnosis[K]
  ) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate(form);
      }}
      className="space-y-5"
    >
      {form.cephSourceAnalysisId && (
        <div className={cn(
          "rounded-lg border px-4 py-3",
          form.isApproved && form.isCephSyncOutdated
            ? "border-amber-200 bg-amber-50 text-amber-800"
            : "border-blue-200 bg-blue-50 text-blue-800"
        )}>
          <p className="text-sm font-semibold">
            {form.isApproved && form.isCephSyncOutdated
              ? "التشخيص المعتمد محفوظ ويوجد تحليل سيفالو أحدث"
              : "القياسات السيفالومترية منقولة تلقائياً من أحدث تحليل"}
          </p>
          <div className="mt-1 flex flex-wrap items-center gap-3 text-xs">
            {form.cephSyncedAt && (
              <span>آخر مزامنة: {formatArabicDate(form.cephSyncedAt)}</span>
            )}
            <Link
              href={`/ceph/${form.latestCephAnalysisId ?? form.cephSourceAnalysisId}`}
              className="font-medium underline underline-offset-2"
            >
              فتح التحليل المصدر
            </Link>
          </div>
        </div>
      )}
      {form.photoAnalysisSummary && (
        <div className={cn(
          "rounded-lg border px-4 py-3",
          form.isApproved && form.isPhotoAnalysisSyncOutdated
            ? "border-amber-200 bg-amber-50 text-amber-800"
            : "border-cyan-200 bg-cyan-50 text-cyan-900"
        )}>
          <p className="text-sm font-semibold">
            {form.isApproved && form.isPhotoAnalysisSyncOutdated
              ? "التشخيص المعتمد محفوظ ويوجد تحليل صورة وجه أحدث"
              : "ملخص صور الوجه منقول تلقائيًا إلى الحالة"}
          </p>
          <p className="mt-2 whitespace-pre-line text-xs leading-6">
            {form.photoAnalysisSummary}
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-3 text-xs">
            {form.photoAnalysisSyncedAt && (
              <span>آخر مزامنة: {formatArabicDate(form.photoAnalysisSyncedAt)}</span>
            )}
            {(form.profileSourceAnalysisId || form.latestProfileAnalysisId) && (
              <Link
                href={`/ceph/photo?orthoCaseId=${caseId}`}
                className="font-medium underline underline-offset-2"
              >
                فتح تحليل البروفايل
              </Link>
            )}
            {(form.frontalSourceAnalysisId || form.latestFrontalAnalysisId) && (
              <Link
                href={`/ceph/photo/frontal?orthoCaseId=${caseId}`}
                className="font-medium underline underline-offset-2"
              >
                فتح التحليل الأمامي
              </Link>
            )}
          </div>
        </div>
      )}

      {/* Classification */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          التصنيف
        </h3>
        <div className="grid gap-4 md:grid-cols-3">
          <Field label="التصنيف الهيكلي">
            <select
              className={inputCls}
              value={form.skeletalClassification ?? ""}
              onChange={(e) => set("skeletalClassification", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Class I</option>
              <option>Class II</option>
              <option>Class III</option>
            </select>
          </Field>
          <Field label="التصنيف السني">
            <input
              className={inputCls}
              value={form.dentalClassification ?? ""}
              onChange={(e) => set("dentalClassification", e.target.value)}
              placeholder="Class I / II / III"
            />
          </Field>
          <Field label="النمط الوجهي">
            <select
              className={inputCls}
              value={form.facialPattern ?? ""}
              onChange={(e) => set("facialPattern", e.target.value)}
            >
              <option value="">اختر</option>
              <option>Hypodivergent</option>
              <option>Normodivergent</option>
              <option>Hyperdivergent</option>
            </select>
          </Field>
        </div>
      </div>

      {/* Extended diagnosis fields */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          تشخيص تفصيلي
        </h3>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="تشخيص الأنسجة الرخوة">
            <textarea
              rows={3}
              className={inputCls}
              value={form.softTissueDiagnosis ?? ""}
              onChange={(e) => set("softTissueDiagnosis", e.target.value)}
            />
          </Field>
          <Field label="التشخيص الوظيفي">
            <textarea
              rows={3}
              className={inputCls}
              value={form.functionalDiagnosis ?? ""}
              onChange={(e) => set("functionalDiagnosis", e.target.value)}
            />
          </Field>
          <Field label="الأسباب (Etiology)" className="md:col-span-2">
            <textarea
              rows={2}
              className={inputCls}
              value={form.etiology ?? ""}
              onChange={(e) => set("etiology", e.target.value)}
            />
          </Field>
        </div>
      </div>

      {/* Cephalometric measurements */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
          القياسات السيفالومترية
        </h3>
        <div className="grid gap-4 md:grid-cols-3">
          {(["anb", "wits", "fma", "sna", "snb", "impa"] as const).map(
            (key) => (
              <Field key={key} label={key.toUpperCase()}>
                <input
                  type="number"
                  step="0.1"
                  className={inputCls}
                  value={form[key] ?? ""}
                  onChange={(e) =>
                    set(
                      key,
                      e.target.value ? Number(e.target.value) : undefined
                    )
                  }
                />
              </Field>
            )
          )}
        </div>
      </div>

      {/* Summary */}
      <div className="rounded-lg border border-gray-200 bg-white p-5">
        <Field label="ملخص التشخيص">
          <textarea
            rows={4}
            className={inputCls}
            value={form.summary ?? ""}
            onChange={(e) => set("summary", e.target.value)}
          />
        </Field>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between">
        <SaveButton saving={save.isPending}>حفظ التشخيص</SaveButton>

        {/* Approval section */}
        {form.isApproved ? (
          <div className="flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-2">
            <CheckCircle2 className="h-5 w-5 text-green-500" />
            <div>
              <p className="text-sm font-medium text-green-700">
                التشخيص معتمد
              </p>
              {form.approvedByName && (
                <p className="text-xs text-green-600">
                  بواسطة {form.approvedByName}
                  {form.approvedAt &&
                    ` · ${formatArabicDate(form.approvedAt)}`}
                </p>
              )}
            </div>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => approve.mutate()}
            disabled={approve.isPending}
            className="inline-flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-green-100 disabled:opacity-50"
          >
            <BadgeCheck className="h-4 w-4" />
            اعتماد التشخيص
          </button>
        )}
      </div>
    </form>
  );
}
