"use client";

import { useEffect, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowRight, User, GitBranch, Send, ShieldCheck, Loader2, AlertCircle,
  CheckCircle2, Circle, Stethoscope, Scissors, ClipboardCheck, Play, ListChecks,
  FileDown,
} from "lucide-react";
import type {
  OrthoSurgicalCaseDetail, OrthoSurgicalStatus, OrthoSurgicalReadiness,
} from "@/types/orthoSurgical";
import {
  ORTHO_SURGICAL_STATUS_LABELS, ORTHO_SURGICAL_STATUS_COLORS,
  ORTHO_SURGICAL_TIMELINE, SURGEON_REVIEW_DECISIONS,
} from "@/types/orthoSurgical";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { CommentsPanel } from "./_components/CommentsPanel";
import { AuditTrailPanel } from "./_components/AuditTrailPanel";
import { JointPlanPanel } from "./_components/JointPlanPanel";
import { SurgeryExecutionPanel } from "./_components/SurgeryExecutionPanel";
import { AiAssistantPanel } from "./_components/AiAssistantPanel";

const primaryBtn =
  "flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition";
const okBtn =
  "flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-60 transition";
const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] transition";

export default function OrthoSurgicalDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuthStore();
  const role = user?.role;
  const isOrtho = role === "Admin" || role === "Orthodontist";
  const isSurgeon = role === "Admin" || role === "OralSurgeon";

  const [data, setData] = useState<OrthoSurgicalCaseDetail | null>(null);
  const [readiness, setReadiness] = useState<OrthoSurgicalReadiness | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);

  // Inline surgeon-review form.
  const [reviewDecision, setReviewDecision] = useState("Approved");
  const [reviewNotes, setReviewNotes] = useState("");
  const [reviewProcedure, setReviewProcedure] = useState("");

  const fetchCase = useCallback(async () => {
    try {
      setLoading(true);
      const [caseRes, readinessRes] = await Promise.all([
        api.get<OrthoSurgicalCaseDetail>(`/api/ortho-surgical-cases/${id}`),
        api.get<OrthoSurgicalReadiness>(`/api/ortho-surgical-cases/${id}/readiness`).catch(() => null),
      ]);
      setData(caseRes.data);
      setReadiness(readinessRes?.data ?? null);
    } catch {
      toast.error("فشل تحميل الحالة");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { fetchCase(); }, [fetchCase]);

  const [pdfBusy, setPdfBusy] = useState<"doctor" | "patient" | null>(null);
  const downloadReport = async (kind: "doctor" | "patient") => {
    setPdfBusy(kind);
    try {
      if (kind === "doctor")
        await downloadPdfFromApi(`/api/ortho-surgical-cases/${id}/report/pdf`, `ortho-surgical-report-${id}.pdf`);
      else
        await downloadPdfFromApi(`/api/ortho-surgical-cases/${id}/patient-explanation/pdf`, `ortho-surgical-patient-explanation-${id}.pdf`);
    } catch {
      toast.error("فشل إنشاء التقرير");
    } finally {
      setPdfBusy(null);
    }
  };

  const act = async (label: string, fn: () => Promise<unknown>, okMsg: string, errMsg: string) => {
    setBusy(label);
    try {
      await fn();
      toast.success(okMsg);
      await fetchCase();
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? errMsg);
    } finally {
      setBusy(null);
    }
  };

  const sendToSurgeon = () =>
    act("send", () => api.post(`/api/ortho-surgical-cases/${id}/send-to-surgeon`), "أُرسلت للجراح", "فشل الإرسال للجراح");

  const submitReview = () =>
    act("review", () => api.post(`/api/ortho-surgical-cases/${id}/surgeon-review`, {
      decision: reviewDecision, notes: reviewNotes || undefined, proposedProcedure: reviewProcedure || undefined,
    }), "تم حفظ مراجعة الجراح", "فشل حفظ المراجعة");

  const approveOrtho = () =>
    act("approveOrtho", () => api.post(`/api/ortho-surgical-cases/${id}/approve-orthodontist`), "تم اعتماد التقويم", "فشل الاعتماد");

  const approveSurgeon = () =>
    act("approveSurgeon", () => api.post(`/api/ortho-surgical-cases/${id}/approve-surgeon`), "تم اعتماد الجراحة", "فشل الاعتماد");

  const moveStatus = (status: OrthoSurgicalStatus) =>
    act(`status-${status}`, () => api.put(`/api/ortho-surgical-cases/${id}/status`, { status }),
      `تم تحديث الحالة إلى "${ORTHO_SURGICAL_STATUS_LABELS[status]}"`, "فشل تحديث الحالة");

  const createSurgery = () =>
    act("createSurgery", () => api.post(`/api/ortho-surgical-cases/${id}/create-surgery-case`, {}),
      "تم فتح الحالة الجراحية", "فشل فتح الحالة الجراحية");

  if (loading) {
    return (
      <div className="space-y-4 max-w-4xl animate-pulse">
        <div className="h-5 w-48 bg-gray-100 rounded" />
        <div className="h-32 bg-gray-100 rounded-xl" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-gray-400">
        <AlertCircle className="w-12 h-12 mb-3 opacity-40" />
        <p className="text-sm">لم يتم العثور على الحالة</p>
        <Link href="/ortho-surgical" className="mt-4 text-sm text-[#3d7ab5] hover:underline">
          العودة إلى القائمة
        </Link>
      </div>
    );
  }

  const activeStageIdx = ORTHO_SURGICAL_TIMELINE.findIndex((s) => s.statuses.includes(data.status));

  return (
    <div className="space-y-5 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/ortho-surgical" className="hover:text-[#3d7ab5] transition">التخطيط التقويمي الجراحي</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium font-mono">{data.caseNumber}</span>
      </div>

      <div className="flex items-center gap-3">
        <Link href="/ortho-surgical" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <GitBranch className="w-5 h-5 text-[#3d7ab5]" />
        <h1 className="text-2xl font-extrabold text-gray-900">حالة تقويمية جراحية</h1>
      </div>

      {/* Header card */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <User className="w-4 h-4 text-gray-400" />
              <Link href={`/patients/${data.patientId}`} className="text-base font-semibold text-gray-900 hover:text-[#3d7ab5] transition">
                {data.patientName}
              </Link>
              <span className="text-xs text-gray-400 font-mono">{data.patientNumber}</span>
            </div>
            <div className="flex items-center gap-2 flex-wrap text-sm">
              <span className="font-mono text-xs font-semibold text-[#3d7ab5]">{data.caseNumber}</span>
              <span className="text-gray-300">·</span>
              <span className="flex items-center gap-1 text-gray-600"><Stethoscope className="w-3.5 h-3.5" /> {data.orthodontistName ?? "—"}</span>
              <span className="text-gray-300">·</span>
              <span className="flex items-center gap-1 text-gray-600"><Scissors className="w-3.5 h-3.5" /> {data.surgeonName ?? "لم يُحدَّد جراح"}</span>
            </div>
          </div>
          <div className="flex flex-col items-end gap-2">
            <span className={cn("text-sm px-3 py-1 rounded-full font-medium", ORTHO_SURGICAL_STATUS_COLORS[data.status])}>
              {data.statusLabel}
            </span>
            <span className="text-xs text-gray-500">
              المسؤول الآن: <span className="font-semibold text-gray-700">{data.responsibleParty}</span>
            </span>
            <div className="flex items-center gap-1">
              <button onClick={() => downloadReport("doctor")} disabled={pdfBusy === "doctor"}
                className="flex items-center gap-1 text-xs font-medium text-[#3d7ab5] hover:underline disabled:opacity-50">
                {pdfBusy === "doctor" ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileDown className="w-3.5 h-3.5" />}
                تقرير الطبيب
              </button>
              <span className="text-gray-300">·</span>
              <button onClick={() => downloadReport("patient")} disabled={pdfBusy === "patient"}
                className="flex items-center gap-1 text-xs font-medium text-[#3d7ab5] hover:underline disabled:opacity-50">
                {pdfBusy === "patient" ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileDown className="w-3.5 h-3.5" />}
                شرح للمريض
              </button>
            </div>
          </div>
        </div>

        {/* Dual approval indicators */}
        <div className="flex items-center gap-4 pt-3 border-t border-[#e8f0f9] text-sm">
          <span className={cn("flex items-center gap-1.5", data.orthodontistApprovedAt ? "text-green-600" : "text-gray-400")}>
            {data.orthodontistApprovedAt ? <CheckCircle2 className="w-4 h-4" /> : <Circle className="w-4 h-4" />}
            اعتماد التقويم
          </span>
          <span className={cn("flex items-center gap-1.5", data.surgeonApprovedAt ? "text-green-600" : "text-gray-400")}>
            {data.surgeonApprovedAt ? <CheckCircle2 className="w-4 h-4" /> : <Circle className="w-4 h-4" />}
            اعتماد الجراحة
          </span>
        </div>
      </div>

      {/* Vertical workflow timeline */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5">
        <h2 className="text-sm font-semibold text-gray-700 mb-4">مسار الحالة</h2>
        <ol className="space-y-0">
          {ORTHO_SURGICAL_TIMELINE.map((stage, idx) => {
            const done = activeStageIdx >= 0 && idx < activeStageIdx;
            const current = idx === activeStageIdx;
            return (
              <li key={stage.key} className="flex items-start gap-3">
                <div className="flex flex-col items-center">
                  <div className={cn("w-6 h-6 rounded-full flex items-center justify-center text-white text-xs",
                    done ? "bg-green-500" : current ? "bg-[#3d7ab5]" : "bg-gray-200")}>
                    {done ? <CheckCircle2 className="w-4 h-4" /> : idx + 1}
                  </div>
                  {idx < ORTHO_SURGICAL_TIMELINE.length - 1 && (
                    <div className={cn("w-0.5 h-8", done ? "bg-green-400" : "bg-gray-200")} />
                  )}
                </div>
                <span className={cn("text-sm pt-0.5", current ? "font-semibold text-[#3d7ab5]" : done ? "text-gray-700" : "text-gray-400")}>
                  {stage.label}
                </span>
              </li>
            );
          })}
        </ol>
      </div>

      {/* Readiness gates (Sprint A3) — read-only projection of RecordsChecklist/OrthoDiagnosis/CephAnalysis */}
      {readiness && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
            <ListChecks className="w-4 h-4 text-[#3d7ab5]" /> جاهزية الحالة
          </h2>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            {[
              { label: "السجلات", ok: readiness.recordsReady },
              { label: "السيفالو", ok: readiness.cephReady },
              { label: "التشخيص", ok: readiness.diagnosisReady },
              { label: "جاهز لمراجعة الجراح", ok: readiness.surgeonReviewReady },
            ].map((gate) => (
              <div key={gate.label} className={cn(
                "flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs",
                gate.ok ? "bg-emerald-50 text-emerald-700" : "bg-gray-50 text-gray-400"
              )}>
                {gate.ok ? <CheckCircle2 className="w-3.5 h-3.5" /> : <Circle className="w-3.5 h-3.5" />}
                <span className="truncate">{gate.label}</span>
              </div>
            ))}
          </div>
          {readiness.missing.length > 0 && (
            <div className="text-xs text-amber-700 bg-amber-50 rounded-lg border border-amber-100 p-3 space-y-1">
              <p className="font-semibold">ينقص:</p>
              <ul className="list-disc pr-4 space-y-0.5">
                {readiness.missing.map((m) => <li key={m}>{m}</li>)}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Workflow actions (A2 shell: wired to A1 endpoints) */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
        <h2 className="text-sm font-semibold text-gray-700">الإجراءات</h2>

        <div className="flex flex-wrap items-center gap-2">
          {/* Send to surgeon (orthodontist side) */}
          {isOrtho && data.allowedTransitions.includes("SentToSurgeon") && (
            <button onClick={sendToSurgeon} disabled={busy === "send"} className={primaryBtn}>
              {busy === "send" ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              إرسال للجراح
            </button>
          )}

          {/* Advance CephReady from draft */}
          {isOrtho && data.allowedTransitions.includes("CephReady") && (
            <button onClick={() => moveStatus("CephReady")} disabled={busy === "status-CephReady"} className={primaryBtn}>
              {busy === "status-CephReady" ? <Loader2 className="w-4 h-4 animate-spin" /> : <ClipboardCheck className="w-4 h-4" />}
              تحديد السيفالو جاهزًا
            </button>
          )}

          {/* Dual approvals */}
          {isOrtho && !data.orthodontistApprovedAt &&
            (data.status === "SurgeonReviewPending" || data.status === "JointPlanApproved") && (
            <button onClick={approveOrtho} disabled={busy === "approveOrtho"} className={okBtn}>
              {busy === "approveOrtho" ? <Loader2 className="w-4 h-4 animate-spin" /> : <ShieldCheck className="w-4 h-4" />}
              اعتماد التقويم
            </button>
          )}
          {isSurgeon && !data.surgeonApprovedAt &&
            (data.status === "SurgeonReviewPending" || data.status === "JointPlanApproved") && (
            <button onClick={approveSurgeon} disabled={busy === "approveSurgeon"} className={okBtn}>
              {busy === "approveSurgeon" ? <Loader2 className="w-4 h-4 animate-spin" /> : <ShieldCheck className="w-4 h-4" />}
              اعتماد الجراحة
            </button>
          )}

          {/* Ready for surgery */}
          {data.allowedTransitions.includes("ReadyForSurgery") && (
            <button onClick={() => moveStatus("ReadyForSurgery")} disabled={busy === "status-ReadyForSurgery"} className={primaryBtn}>
              {busy === "status-ReadyForSurgery" ? <Loader2 className="w-4 h-4 animate-spin" /> : <ClipboardCheck className="w-4 h-4" />}
              جاهزة للجراحة
            </button>
          )}

          {/* Create surgery case */}
          {data.status === "ReadyForSurgery" && (
            <button onClick={createSurgery} disabled={busy === "createSurgery"} className={okBtn}>
              {busy === "createSurgery" ? <Loader2 className="w-4 h-4 animate-spin" /> : <Play className="w-4 h-4" />}
              فتح حالة جراحية
            </button>
          )}

          {/* Link to created surgery case */}
          {data.surgeryCaseId && (
            <Link href={`/surgery/${data.surgeryCaseId}`} className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition">
              <Scissors className="w-4 h-4" /> فتح الحالة الجراحية
            </Link>
          )}
        </div>

        {/* Surgeon review inline form (surgeon side, while pending) */}
        {isSurgeon && (data.status === "SentToSurgeon" || data.status === "SurgeonReviewPending") && (
          <div className="bg-gray-50 rounded-lg border border-gray-200 p-4 space-y-3">
            <h3 className="text-xs font-semibold text-gray-500">مراجعة الجراح</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">القرار</label>
                <select value={reviewDecision} onChange={(e) => setReviewDecision(e.target.value)} className={inputCls}>
                  {SURGEON_REVIEW_DECISIONS.map((d) => <option key={d.value} value={d.value}>{d.label}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">الإجراء المقترح</label>
                <input value={reviewProcedure} onChange={(e) => setReviewProcedure(e.target.value)} placeholder="مثلاً: Le Fort I + BSSO" className={inputCls} />
              </div>
              <div className="sm:col-span-2">
                <label className="block text-xs font-medium text-gray-600 mb-1">ملاحظات</label>
                <textarea value={reviewNotes} onChange={(e) => setReviewNotes(e.target.value)} rows={2} className={inputCls} />
              </div>
            </div>
            <div className="flex justify-end">
              <button onClick={submitReview} disabled={busy === "review"} className={primaryBtn}>
                {busy === "review" ? <Loader2 className="w-4 h-4 animate-spin" /> : <ClipboardCheck className="w-4 h-4" />}
                حفظ المراجعة
              </button>
            </div>
          </div>
        )}

        {/* Existing surgeon review summary */}
        {data.surgeonReview && (
          <div className="text-sm text-gray-600 bg-gray-50 rounded-lg border border-gray-200 p-3 space-y-1">
            <div><span className="text-gray-400">قرار الجراح:</span> {SURGEON_REVIEW_DECISIONS.find((d) => d.value === data.surgeonReview!.decision)?.label ?? data.surgeonReview.decision}</div>
            {data.surgeonReview.proposedProcedure && <div><span className="text-gray-400">الإجراء المقترح:</span> {data.surgeonReview.proposedProcedure}</div>}
            {data.surgeonReview.notes && <div><span className="text-gray-400">ملاحظات:</span> {data.surgeonReview.notes}</div>}
          </div>
        )}

        <p className="text-[11px] text-gray-400 pt-2 border-t border-[#e8f0f9]">
          هذه محاكاة تخطيطية مشتركة؛ القرار الجراحي النهائي يعتمد على مراجعة أخصائي جراحة الفم والفكين وموافقة المريض.
        </p>
      </div>

      {/* Joint plan (Sprint A5) */}
      <JointPlanPanel
        orthoSurgicalCaseId={data.id}
        jointPlan={data.jointPlan}
        canEdit={isOrtho || isSurgeon}
        onSaved={fetchCase}
      />

      {/* Surgery execution glance (Sprint A6) */}
      <SurgeryExecutionPanel orthoSurgicalCaseId={data.id} />

      {/* AI text assistant (Sprint A8) */}
      <AiAssistantPanel orthoSurgicalCaseId={data.id} />

      {/* Discussion + audit trail (Sprint A4) */}
      <CommentsPanel orthoSurgicalCaseId={data.id} />
      <AuditTrailPanel orthoSurgicalCaseId={data.id} />

      <p className="text-xs text-gray-400">أُنشئت {formatArabicDate(data.createdAt)}</p>
    </div>
  );
}
