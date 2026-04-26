"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { GitBranch, User, Calendar, Wallet, ClipboardList, Activity } from "lucide-react";
import type { OrthoCase, TreatmentStage, OrthoVisit } from "@/types/ortho";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { TreatmentStagesPanel } from "@/components/ortho/TreatmentStagesPanel";
import { OrthoVisitTimeline } from "@/components/ortho/OrthoVisitTimeline";

type Tab = "info" | "stages" | "visits" | "finance";

const TABS: { key: Tab; label: string; icon: typeof User }[] = [
  { key: "info",    label: "المعلومات",     icon: User },
  { key: "stages",  label: "مراحل العلاج", icon: GitBranch },
  { key: "visits",  label: "سجل الزيارات", icon: Calendar },
  { key: "finance", label: "العقد المالي",  icon: Wallet },
];

export default function OrthoCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [orthoCase, setOrthoCase] = useState<OrthoCase | null>(null);
  const [visits, setVisits] = useState<OrthoVisit[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<Tab>("info");

  useEffect(() => {
    Promise.all([
      api.get<OrthoCase>(`/api/ortho-cases/${id}`),
      api.get<OrthoVisit[]>(`/api/ortho-cases/${id}/visits`),
    ])
      .then(([caseRes, visitsRes]) => {
        setOrthoCase(caseRes.data);
        setVisits(visitsRes.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const handleStageUpdate = (updated: TreatmentStage) => {
    setOrthoCase((prev) =>
      prev
        ? {
            ...prev,
            stages: prev.stages?.map((s) => (s.id === updated.id ? updated : s)),
          }
        : prev
    );
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!orthoCase) {
    return <div className="text-center py-20 text-gray-400">الحالة غير موجودة</div>;
  }

  const progress = orthoCase.stagePercentage;

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/ortho" className="hover:text-clinic-teal transition">التقويم</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{orthoCase.caseNumber}</span>
      </div>

      {/* Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl flex items-center justify-center text-white flex-shrink-0"
              style={{ backgroundColor: orthoCase.doctorColor ?? "#0E7490" }}
            >
              <GitBranch className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-3 flex-wrap">
                <h1 className="text-xl font-extrabold text-gray-900">{orthoCase.patientName}</h1>
                <span className="font-mono text-xs bg-gray-100 px-2.5 py-1 rounded text-gray-600">
                  {orthoCase.caseNumber}
                </span>
                <span className={cn(
                  "text-xs px-2 py-0.5 rounded-full font-medium",
                  orthoCase.status === "active" ? "bg-teal-50 text-teal-700" : "bg-gray-100 text-gray-500"
                )}>
                  {orthoCase.status === "active" ? "نشطة" : orthoCase.status}
                </span>
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-gray-500">
                {orthoCase.applianceType && (
                  <span className="flex items-center gap-1">
                    <ClipboardList className="w-3.5 h-3.5" />
                    {orthoCase.applianceType}
                  </span>
                )}
                {orthoCase.doctorName && (
                  <span>{orthoCase.doctorName}</span>
                )}
                {orthoCase.startDate && (
                  <span>بدأت: {formatArabicDate(orthoCase.startDate)}</span>
                )}
                {orthoCase.totalFee && (
                  <span className="font-mono">{formatYemeniRiyal(orthoCase.totalFee)}</span>
                )}
              </div>

              {/* Progress bar */}
              <div className="mt-3 flex items-center gap-3">
                <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden max-w-xs">
                  <div
                    className="h-full bg-clinic-teal rounded-full transition-all"
                    style={{ width: `${progress}%` }}
                  />
                </div>
                <span className="text-xs font-medium text-gray-600">{progress}% مكتمل</span>
              </div>
            </div>
          </div>
          <Link
            href={`/patients/${orthoCase.patientId}`}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 transition text-gray-600"
          >
            <User className="w-3.5 h-3.5" />
            ملف المريض
          </Link>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex border-b border-gray-100 overflow-x-auto">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={cn(
                "flex items-center gap-2 px-5 py-3.5 text-sm font-medium whitespace-nowrap border-b-2 transition",
                activeTab === key
                  ? "border-clinic-teal text-clinic-teal"
                  : "border-transparent text-gray-500 hover:text-gray-900"
              )}
            >
              <Icon className="w-4 h-4" />
              {label}
            </button>
          ))}
        </div>

        <div className="p-5">
          {activeTab === "info" && (
            <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {[
                ["المريض", orthoCase.patientName],
                ["رقم الحالة", orthoCase.caseNumber],
                ["الطبيب", orthoCase.doctorName ?? "—"],
                ["الجهاز", orthoCase.applianceType ?? "—"],
                ["تاريخ البداية", orthoCase.startDate ? formatArabicDate(orthoCase.startDate) : "—"],
                ["المدة المتوقعة", orthoCase.expectedDurationMonths ? `${orthoCase.expectedDurationMonths} أشهر` : "—"],
                ["المرحلة الحالية", orthoCase.currentStage ?? "—"],
                ["قرار الخلع", orthoCase.extractionDecisionValue ?? "—"],
                ["خطة الاحتفاظ", orthoCase.retentionPlan ?? "—"],
                ["الرسوم", orthoCase.totalFee ? formatYemeniRiyal(orthoCase.totalFee) : "—"],
              ].map(([label, value]) => (
                <div key={label} className="border-b border-gray-50 pb-3 last:border-0">
                  <p className="text-xs text-gray-400 mb-0.5">{label}</p>
                  <p className="text-sm font-medium text-gray-900">{value}</p>
                </div>
              ))}
            </div>
            <div className="pt-2 border-t border-gray-100">
              <Link
                href={`/ceph/new?orthoCaseId=${id}`}
                className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-clinic-teal/10 transition"
              >
                <Activity className="w-4 h-4" />
                إنشاء تحليل سيفالومتري
              </Link>
            </div>
            </div>
          )}

          {activeTab === "stages" && (
            <TreatmentStagesPanel
              caseId={id}
              stages={orthoCase.stages ?? []}
              onUpdate={handleStageUpdate}
            />
          )}

          {activeTab === "visits" && (
            <OrthoVisitTimeline
              caseId={id}
              visits={visits}
              onVisitAdded={(v) => setVisits([v, ...visits])}
            />
          )}

          {activeTab === "finance" && (
            <div className="text-center py-12 text-gray-400">
              <Wallet className="w-10 h-10 mx-auto mb-2 opacity-30" />
              <p className="text-sm">لإدارة العقد المالي</p>
              <Link
                href="/finance/contracts"
                className="mt-3 inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
              >
                الذهاب إلى المالية
              </Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
