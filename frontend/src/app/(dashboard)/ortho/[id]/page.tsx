"use client";

import { useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  Activity,
  ArrowRight,
  Calendar,
  Camera,
  FileText,
  FlaskConical,
  GitBranch,
  Images,
  ListChecks,
  ClipboardCheck,
  Microscope,
  Presentation,
  ScanLine,
  Scissors,
  ShieldCheck,
  Smile,
  Sparkles,
  Stethoscope,
  User,
  UserSquare2,
  Wallet,
} from "lucide-react";
import { cn, formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import {
  useOrthoCase,
  useOrthoOverview,
  useOrthoStages,
  useOrthoVisits,
} from "@/hooks/useOrtho";
import type { TreatmentStage } from "@/types/ortho";
import { ORTHO_STATUS_LABELS } from "@/types/ortho";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { OrthoStagesTimeline } from "@/components/ortho/OrthoStagesTimeline";
import { OrthoVisitTimeline } from "@/components/ortho/OrthoVisitTimeline";
import { OrthoBeforeAfterCompare } from "@/components/ortho/OrthoBeforeAfterCompare";
import { FacialPhotoPanel } from "@/components/ortho/FacialPhotoPanel";
import { LabOrdersPanel } from "@/components/ortho/LabOrdersPanel";
import { CasePresentationPanel } from "@/components/ortho/CasePresentationPanel";

import type { Tab } from "./_lib/types";
import { useActiveTab } from "./_lib/hooks";
import { OrthoOverviewTab } from "./_components/OrthoOverviewTab";
import { OrthoClinicalExamTab } from "./_components/OrthoClinicalExamTab";
import { OrthoProblemListTab } from "./_components/OrthoProblemListTab";
import { OrthoTreatmentPlansTab } from "./_components/OrthoTreatmentPlansTab";
import { OrthoExtractionTab } from "./_components/OrthoExtractionTab";
import { OrthoRecordsChecklistTab } from "./_components/OrthoRecordsChecklistTab";
import { OrthoPhotosTab } from "./_components/OrthoPhotosTab";
import { OrthoDiagnosisTab, OrthoCephPanel } from "./_components/OrthoDiagnosisTab";
import { OrthoRetentionTab } from "./_components/OrthoRetentionTab";
import { OrthoModelAnalysisTab } from "./_components/OrthoModelAnalysisTab";
import { OrthoAiDraftPanel } from "./_components/OrthoAiDraftPanel";
import { OrthoFinanceTab } from "./_components/OrthoFinanceTab";

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

const TABS: { key: Tab; label: string; icon: typeof Activity }[] = [
  { key: "overview", label: "الملخص", icon: Activity },
  { key: "records", label: "السجلات", icon: Camera },
  { key: "compare", label: "مقارنة قبل/بعد", icon: Images },
  { key: "exam", label: "الفحص", icon: Stethoscope },
  { key: "cast", label: "تحليل النماذج", icon: Microscope },
  { key: "ceph", label: "السيفالو", icon: ScanLine },
  { key: "facial", label: "تحليل الصور", icon: Smile },
  { key: "problems", label: "المشاكل", icon: ListChecks },
  { key: "diagnosis", label: "التشخيص", icon: ClipboardCheck },
  { key: "plan", label: "الخطة", icon: FileText },
  { key: "stages", label: "المراحل", icon: GitBranch },
  { key: "visits", label: "الزيارات", icon: Calendar },
  { key: "extraction", label: "الخلع", icon: Scissors },
  { key: "retention", label: "الاحتفاظ", icon: ShieldCheck },
  { key: "lab", label: "المختبر", icon: FlaskConical },
  { key: "finance", label: "المالية", icon: Wallet },
  { key: "wizard", label: "المعالج", icon: Sparkles },
  { key: "reports", label: "التقارير", icon: Presentation },
];

/* ------------------------------------------------------------------ */
/*  Main Page                                                          */
/* ------------------------------------------------------------------ */

export default function OrthoCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { activeTab, setActiveTab } = useActiveTab(TABS);
  const { data: orthoCase, isLoading } = useOrthoCase(id);
  const { data: overview } = useOrthoOverview(id);
  const { data: stages = [] } = useOrthoStages(id);
  const { data: visits = [] } = useOrthoVisits(id);
  const [localStages, setLocalStages] = useState<TreatmentStage[]>([]);
  useEffect(() => setLocalStages(stages), [stages]);

  const stageProgress = useMemo(
    () => orthoCase?.stagePercentage ?? 0,
    [orthoCase]
  );

  if (isLoading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 rounded-lg bg-gray-100" />
        <div className="h-80 rounded-lg bg-gray-100" />
      </div>
    );
  }

  if (!orthoCase) {
    return (
      <div className="py-20 text-center text-gray-400">
        الحالة غير موجودة
      </div>
    );
  }

  return (
    <div className="max-w-7xl space-y-5">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link
          href="/ortho"
          className="inline-flex items-center gap-1 hover:text-clinic-blue"
        >
          <ArrowRight className="h-4 w-4" /> التقويم
        </Link>
        <span>/</span>
        <span className="font-medium text-gray-900">
          {orthoCase.caseNumber}
        </span>
      </div>

      {/* Case header card */}
      <section className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex items-start gap-4">
            <div
              className="flex h-12 w-12 items-center justify-center rounded-lg text-white"
              style={{ backgroundColor: orthoCase.doctorColor ?? "#2563EB" }}
            >
              <GitBranch className="h-6 w-6" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-xl font-bold text-gray-900">
                  {orthoCase.patientName}
                </h1>
                <span className="rounded bg-gray-100 px-2 py-1 font-mono text-xs text-gray-600">
                  {orthoCase.caseNumber}
                </span>
                <span className="rounded-full bg-clinic-blue-50 px-2 py-1 text-xs font-medium text-clinic-blue">
                  {ORTHO_STATUS_LABELS[orthoCase.status] ??
                    orthoCase.status}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap gap-4 text-sm text-gray-500">
                <span>{orthoCase.doctorName ?? "بدون طبيب"}</span>
                {orthoCase.applianceType && (
                  <span>{orthoCase.applianceType}</span>
                )}
                {orthoCase.startDate && (
                  <span>بدأت: {formatArabicDate(orthoCase.startDate)}</span>
                )}
                {orthoCase.totalFee && (
                  <span>{formatYemeniRiyal(orthoCase.totalFee)}</span>
                )}
              </div>
            </div>
          </div>
          <Link
            href={`/patients/${orthoCase.patientId}`}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            <User className="h-4 w-4" />
            ملف المريض
          </Link>
        </div>
        <div className="mt-4 flex items-center gap-3">
          <div className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100">
            <div
              className="h-full rounded-full bg-clinic-blue transition-all"
              style={{ width: `${stageProgress}%` }}
            />
          </div>
          <span className="text-sm font-medium text-gray-600">
            {stageProgress}%
          </span>
        </div>

        {/* Quick case stats */}
        <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
          {[
            { label: "المرحلة الحالية", value: orthoCase.currentStage || "—" },
            { label: "الموعد القادم", value: overview?.nextAppointmentDate ? formatArabicDate(overview.nextAppointmentDate) : "—" },
            { label: "جاهزية السجلات", value: overview ? `${overview.checklistCompleted ?? 0}/${overview.checklistTotal ?? 0}` : "—" },
            { label: "المتبقّي المالي", value: overview?.contractRemaining != null ? formatYemeniRiyal(overview.contractRemaining) : "—" },
          ].map((s) => (
            <div key={s.label} className="rounded-lg border border-gray-100 bg-gray-50/60 px-3 py-2">
              <div className="text-[11px] text-gray-500">{s.label}</div>
              <div className="truncate text-sm font-semibold text-gray-900">{s.value}</div>
            </div>
          ))}
        </div>

        {/* Quick actions */}
        <div className="mt-3 flex flex-wrap gap-2">
          <Link href={`/ceph/new?orthoCaseId=${orthoCase.id}`}
            className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50">
            <ScanLine className="h-3.5 w-3.5" />تحليل سيفالو جديد
          </Link>
          <Link href={`/ceph/photo?orthoCaseId=${orthoCase.id}`}
            className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50">
            <UserSquare2 className="h-3.5 w-3.5" />تحليل صورة
          </Link>
          <Link href={`/ortho/${orthoCase.id}/model-analysis`}
            className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50">
            <Microscope className="h-3.5 w-3.5" />تحليل نماذج
          </Link>
        </div>
      </section>

      {/* Tab navigation */}
      <section className="rounded-lg border border-gray-200 bg-white shadow-sm">
        <div className="flex overflow-x-auto border-b border-gray-100">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              type="button"
              onClick={() => setActiveTab(key)}
              className={cn(
                "inline-flex items-center gap-2 whitespace-nowrap border-b-2 px-4 py-3 text-sm font-medium transition",
                activeTab === key
                  ? "border-clinic-blue text-clinic-blue"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="h-4 w-4" />
              {label}
            </button>
          ))}
        </div>
        <div className="p-5">
          {activeTab === "overview" && (
            <OrthoOverviewTab caseId={id} patientId={orthoCase.patientId} setActiveTab={setActiveTab} />
          )}
          {activeTab === "records" && (
            <div className="space-y-5">
              <OrthoRecordsChecklistTab caseId={id} />
              <OrthoPhotosTab caseId={id} />
            </div>
          )}
          {activeTab === "compare" && <OrthoBeforeAfterCompare caseId={id} />}
          {activeTab === "exam" && <OrthoClinicalExamTab caseId={id} />}
          {activeTab === "cast" && <OrthoModelAnalysisTab caseId={id} />}
          {activeTab === "problems" && <OrthoProblemListTab caseId={id} />}
          {activeTab === "ceph" && <OrthoCephPanel caseId={id} />}
          {activeTab === "facial" && <FacialPhotoPanel caseId={id} />}
          {activeTab === "diagnosis" && <OrthoDiagnosisTab caseId={id} />}
          {activeTab === "plan" && <OrthoTreatmentPlansTab caseId={id} />}
          {activeTab === "stages" && (
            <div className="space-y-6">
              <OrthoStagesTimeline stages={localStages} />
              {localStages.length > 0 && (
                <div className="border-t border-gray-100 pt-5">
                  <h3 className="mb-3 text-sm font-semibold text-clinic-navy">
                    إدارة المراحل
                  </h3>
                  <TreatmentStagesPanel
                    caseId={id}
                    stages={localStages}
                    onUpdate={(stage) =>
                      setLocalStages((items) =>
                        items.map((item) =>
                          item.id === stage.id ? stage : item
                        )
                      )
                    }
                  />
                </div>
              )}
            </div>
          )}
          {activeTab === "visits" && (
            <OrthoVisitTimeline caseId={id} visits={visits} />
          )}
          {activeTab === "extraction" && <OrthoExtractionTab caseId={id} />}
          {activeTab === "retention" && <OrthoRetentionTab caseId={id} />}
          {activeTab === "lab" && <LabOrdersPanel caseId={id} />}
          {activeTab === "finance" && (
            <OrthoFinanceTab caseId={id} patientId={orthoCase.patientId} />
          )}
          {activeTab === "wizard" && (
            <OrthoAiDraftPanel
              caseId={id}
              patientId={orthoCase.patientId}
              onNavigate={setActiveTab}
            />
          )}
          {activeTab === "reports" && <CasePresentationPanel caseId={id} />}
        </div>
      </section>
    </div>
  );
}
