"use client";

import { useCallback, useEffect, useState } from "react";
import {
  AlertCircle,
  CheckCircle2,
  Circle,
  FileDown,
  GitBranch,
  Loader2,
  Play,
  Scissors,
  Send,
  ShieldCheck,
  Stethoscope,
} from "lucide-react";
import type {
  OrthoSurgicalCaseDetail,
  OrthoSurgicalCaseListItem,
  OrthoSurgicalReadiness,
  OrthoSurgicalStatus,
} from "@/types/orthoSurgical";
import {
  ORTHO_SURGICAL_STATUS_COLORS,
  ORTHO_SURGICAL_STATUS_LABELS,
  ORTHO_SURGICAL_TIMELINE,
} from "@/types/orthoSurgical";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { CommentsPanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/CommentsPanel";
import { AuditTrailPanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/AuditTrailPanel";
import { JointPlanPanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/JointPlanPanel";
import { SurgeryExecutionPanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/SurgeryExecutionPanel";
import { AiAssistantPanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/AiAssistantPanel";
import { OrthoSurgicalVtoPanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/OrthoSurgicalVtoPanel";
import { OrthoSurgicalExportPackagePanel } from "@/app/(dashboard)/ortho-surgical/[id]/_components/OrthoSurgicalExportPackagePanel";

interface OrthoSurgicalPlanningTabProps {
  caseId: string;
}

const primaryBtn =
  "inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-3 py-2 text-sm font-medium text-white transition hover:bg-clinic-navy disabled:opacity-60";
const secondaryBtn =
  "inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50 disabled:opacity-60";

export function OrthoSurgicalPlanningTab({ caseId }: OrthoSurgicalPlanningTabProps) {
  const { user } = useAuthStore();
  const role = user?.role;
  const isOrtho = role === "Admin" || role === "Orthodontist";
  const isSurgeon = role === "Admin" || role === "OralSurgeon";

  const [data, setData] = useState<OrthoSurgicalCaseDetail | null>(null);
  const [readiness, setReadiness] = useState<OrthoSurgicalReadiness | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [pdfBusy, setPdfBusy] = useState<"doctor" | "patient" | null>(null);

  const fetchWorkspace = useCallback(async () => {
    setLoading(true);
    try {
      const listRes = await api.get<{ data: OrthoSurgicalCaseListItem[] }>(
        "/api/ortho-surgical-cases",
        { params: { orthoCaseId: caseId, pageSize: "1" } }
      );
      const existing = listRes.data?.data?.[0];
      if (!existing) {
        setData(null);
        setReadiness(null);
        return;
      }

      const [detailRes, readinessRes] = await Promise.all([
        api.get<OrthoSurgicalCaseDetail>(`/api/ortho-surgical-cases/${existing.id}`),
        api.get<OrthoSurgicalReadiness>(`/api/ortho-surgical-cases/${existing.id}/readiness`).catch(() => null),
      ]);
      setData(detailRes.data);
      setReadiness(readinessRes?.data ?? null);
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "تعذر تحميل التخطيط الجراحي لحالة التقويم");
    } finally {
      setLoading(false);
    }
  }, [caseId]);

  useEffect(() => { fetchWorkspace(); }, [fetchWorkspace]);

  const act = async (label: string, fn: () => Promise<unknown>, okMsg: string, errMsg: string) => {
    setBusy(label);
    try {
      await fn();
      toast.success(okMsg);
      await fetchWorkspace();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? errMsg);
    } finally {
      setBusy(null);
    }
  };

  const createWorkspace = () =>
    act(
      "create",
      () => api.post("/api/ortho-surgical-cases", { orthoCaseId: caseId }),
      "تم فتح التخطيط الجراحي داخل حالة التقويم",
      "فشل فتح التخطيط الجراحي"
    );

  const sendToSurgeon = () =>
    data && act("send", () => api.post(`/api/ortho-surgical-cases/${data.id}/send-to-surgeon`), "أُرسلت للجراح", "فشل الإرسال للجراح");

  const approveOrtho = () =>
    data && act("approveOrtho", () => api.post(`/api/ortho-surgical-cases/${data.id}/approve-orthodontist`), "تم اعتماد التقويم", "فشل الاعتماد");

  const approveSurgeon = () =>
    data && act("approveSurgeon", () => api.post(`/api/ortho-surgical-cases/${data.id}/approve-surgeon`), "تم اعتماد الجراحة", "فشل الاعتماد");

  const moveStatus = (status: OrthoSurgicalStatus) =>
    data && act(
      `status-${status}`,
      () => api.put(`/api/ortho-surgical-cases/${data.id}/status`, { status }),
      `تم تحديث الحالة إلى "${ORTHO_SURGICAL_STATUS_LABELS[status]}"`,
      "فشل تحديث الحالة"
    );

  const createSurgery = () =>
    data && act("createSurgery", () => api.post(`/api/ortho-surgical-cases/${data.id}/create-surgery-case`, {}), "تم فتح الحالة الجراحية", "فشل فتح الحالة الجراحية");

  const downloadReport = async (kind: "doctor" | "patient") => {
    if (!data) return;
    setPdfBusy(kind);
    try {
      if (kind === "doctor") {
        await downloadPdfFromApi(`/api/ortho-surgical-cases/${data.id}/report/pdf`, `ortho-surgical-report-${data.id}.pdf`);
      } else {
        await downloadPdfFromApi(`/api/ortho-surgical-cases/${data.id}/patient-explanation/pdf`, `ortho-surgical-patient-explanation-${data.id}.pdf`);
      }
    } catch {
      toast.error("فشل إنشاء التقرير");
    } finally {
      setPdfBusy(null);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-28 rounded-lg bg-gray-100" />
        <div className="h-64 rounded-lg bg-gray-100" />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50/60 p-8 text-center">
        <GitBranch className="mx-auto mb-3 h-10 w-10 text-clinic-blue" />
        <h3 className="text-base font-bold text-clinic-navy">التخطيط الجراحي داخل حالة التقويم</h3>
        <p className="mx-auto mt-2 max-w-2xl text-sm leading-7 text-gray-600">
          هذا المسار لا يُنشئ وحدة منفصلة. سيتم ربط السيفالو، التشخيص، مراجعة الجراح، والخطة المشتركة بهذه الحالة التقويمية نفسها.
        </p>
        <button type="button" onClick={createWorkspace} disabled={busy === "create"} className={cn(primaryBtn, "mt-5")}>
          {busy === "create" ? <Loader2 className="h-4 w-4 animate-spin" /> : <GitBranch className="h-4 w-4" />}
          فتح التخطيط الجراحي لهذه الحالة
        </button>
      </div>
    );
  }

  const activeStageIdx = ORTHO_SURGICAL_TIMELINE.findIndex((stage) => stage.statuses.includes(data.status));

  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-xs font-semibold text-clinic-blue">{data.caseNumber}</span>
              <span className={cn("rounded-full px-3 py-1 text-xs font-medium", ORTHO_SURGICAL_STATUS_COLORS[data.status])}>
                {data.statusLabel}
              </span>
            </div>
            <h3 className="mt-2 text-lg font-bold text-clinic-navy">التخطيط الجراحي التقويمي</h3>
            <div className="mt-2 flex flex-wrap gap-3 text-sm text-gray-500">
              <span className="inline-flex items-center gap-1"><Stethoscope className="h-4 w-4" /> {data.orthodontistName ?? "بدون أخصائي تقويم"}</span>
              <span className="inline-flex items-center gap-1"><Scissors className="h-4 w-4" /> {data.surgeonName ?? "لم يحدد الجراح"}</span>
              <span>المسؤول الآن: <b className="text-gray-700">{data.responsibleParty}</b></span>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => downloadReport("doctor")} disabled={pdfBusy === "doctor"} className={secondaryBtn}>
              {pdfBusy === "doctor" ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileDown className="h-4 w-4" />}
              تقرير الطبيب
            </button>
            <button type="button" onClick={() => downloadReport("patient")} disabled={pdfBusy === "patient"} className={secondaryBtn}>
              {pdfBusy === "patient" ? <Loader2 className="h-4 w-4 animate-spin" /> : <FileDown className="h-4 w-4" />}
              شرح للمريض
            </button>
          </div>
        </div>

        <div className="mt-4 flex flex-wrap gap-4 border-t border-gray-100 pt-4 text-sm">
          <span className={cn("inline-flex items-center gap-1.5", data.orthodontistApprovedAt ? "text-green-600" : "text-gray-400")}>
            {data.orthodontistApprovedAt ? <CheckCircle2 className="h-4 w-4" /> : <Circle className="h-4 w-4" />}
            اعتماد التقويم
          </span>
          <span className={cn("inline-flex items-center gap-1.5", data.surgeonApprovedAt ? "text-green-600" : "text-gray-400")}>
            {data.surgeonApprovedAt ? <CheckCircle2 className="h-4 w-4" /> : <Circle className="h-4 w-4" />}
            اعتماد الجراحة
          </span>
          <span className="text-gray-400">تاريخ الفتح: {formatArabicDate(data.createdAt)}</span>
        </div>
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        <h4 className="mb-4 text-sm font-semibold text-clinic-navy">مسار الحالة</h4>
        <div className="grid gap-3 md:grid-cols-4">
          {ORTHO_SURGICAL_TIMELINE.map((stage, idx) => {
            const done = activeStageIdx >= idx;
            const active = activeStageIdx === idx;
            return (
              <div key={stage.key} className={cn("rounded-lg border px-3 py-2 text-xs", active ? "border-clinic-blue bg-clinic-blue-50 text-clinic-blue" : done ? "border-green-100 bg-green-50 text-green-700" : "border-gray-100 bg-gray-50 text-gray-400")}>
                <span className="font-semibold">{stage.label}</span>
              </div>
            );
          })}
        </div>
      </div>

      {readiness && (
        <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
          <h4 className="mb-3 text-sm font-semibold text-clinic-navy">جاهزية السجلات والسيفالو</h4>
          <div className="grid gap-2 sm:grid-cols-4">
            {[
              ["السجلات", readiness.recordsReady],
              ["السيفالو المعتمد", readiness.cephReady],
              ["التشخيص", readiness.diagnosisReady],
              ["مراجعة الجراح", readiness.surgeonReviewReady],
            ].map(([label, ok]) => (
              <div key={String(label)} className={cn("flex items-center gap-2 rounded-lg px-3 py-2 text-sm", ok ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700")}>
                {ok ? <CheckCircle2 className="h-4 w-4" /> : <AlertCircle className="h-4 w-4" />}
                {label}
              </div>
            ))}
          </div>
          {readiness.missing.length > 0 && (
            <ul className="mt-3 list-disc space-y-1 pr-5 text-xs text-amber-700">
              {readiness.missing.map((item) => <li key={item}>{item}</li>)}
            </ul>
          )}
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        {isOrtho && (
          <button type="button" onClick={sendToSurgeon} disabled={busy === "send"} className={primaryBtn}>
            <Send className="h-4 w-4" /> إرسال للجراح
          </button>
        )}
        {isOrtho && (
          <button type="button" onClick={approveOrtho} disabled={busy === "approveOrtho"} className={secondaryBtn}>
            <ShieldCheck className="h-4 w-4" /> اعتماد التقويم
          </button>
        )}
        {isSurgeon && (
          <button type="button" onClick={approveSurgeon} disabled={busy === "approveSurgeon"} className={secondaryBtn}>
            <ShieldCheck className="h-4 w-4" /> اعتماد الجراحة
          </button>
        )}
        {data.allowedTransitions.map((status) => (
          <button key={status} type="button" onClick={() => moveStatus(status)} disabled={busy === `status-${status}`} className={secondaryBtn}>
            <Play className="h-4 w-4" /> {ORTHO_SURGICAL_STATUS_LABELS[status]}
          </button>
        ))}
        {data.status === "ReadyForSurgery" && (
          <button type="button" onClick={createSurgery} disabled={busy === "createSurgery"} className={primaryBtn}>
            <Scissors className="h-4 w-4" /> فتح الحالة الجراحية
          </button>
        )}
      </div>

      <JointPlanPanel
        orthoSurgicalCaseId={data.id}
        jointPlan={data.jointPlan}
        canEdit={isOrtho || isSurgeon}
        onSaved={fetchWorkspace}
      />

      {/* Sprint A9 — Surgical VTO 2D. Below the joint plan per §8 of the A9 handoff:
          a planning aid over the approved CephAnalysis baseline. The mandatory Arabic
          disclaimer is rendered inside the panel on every VTO card. */}
      <OrthoSurgicalVtoPanel orthoSurgicalCaseId={data.id} />
      <OrthoSurgicalExportPackagePanel orthoSurgicalCaseId={data.id} />

      <SurgeryExecutionPanel orthoSurgicalCaseId={data.id} />
      <AiAssistantPanel orthoSurgicalCaseId={data.id} />
      <CommentsPanel orthoSurgicalCaseId={data.id} />
      <AuditTrailPanel orthoSurgicalCaseId={data.id} />

      <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-xs leading-6 text-amber-800">
        هذه محاكاة تخطيطية مشتركة؛ القرار الجراحي النهائي يعتمد على مراجعة أخصائي جراحة الفم والفكين وموافقة المريض.
      </div>
    </div>
  );
}
