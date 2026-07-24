"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Calendar, Activity, Wallet, Clock, Scissors,
  Phone, Stethoscope, AlertTriangle, FileText,
  CreditCard, Camera, FolderOpen, Plus, MessageCircle,
  ClipboardList, ChevronLeft, ScanLine, Printer,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";
import type { PatientProfile } from "@/types/patient";
import type { OrthoOverview } from "@/types/ortho";
import { financeV3CollectionsUrl } from "@/lib/financeRoutes";
import { SURGERY_STATUS_LABELS } from "@/types/surgery";
// FE-09: centralized appointment status colors (includes 'signed' for mixed timeline views)
import { APPOINTMENT_STATUS_COLORS as STATUS_COLORS } from "@/lib/statusStyles";

// ─── Interfaces ──────────────────────────────────────────────────────────────

interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number | null;
  totalOutstanding: number | null;
  /** QA-596: performed sessions with AmountDueReference but no linked invoice. Included in totalOutstanding. */
  unbilledVisitsAmount?: number | null;
  prescriptionsCount: number;
  // Extended fields
  lastVisitDate?: string;
  lastVisitDoctor?: string;
  lastVisitDiagnosis?: string;
  nextAppointmentDate?: string;
  nextAppointmentTime?: string;
  nextAppointmentType?: string;
  nextAppointmentDoctor?: string;
  chiefComplaint?: string;
  currentDiagnosis?: string;
  nextPlannedStep?: string;
  activeOrthoSummary?: { caseNumber: string; applianceType?: string; stagePercentage: number }[];
  activeSurgerySummary?: { caseNumber: string; surgeryType: string; status: string }[];
  medicalAlerts?: string[];
}

interface TimelineEvent {
  type: string;
  id: string;
  date: string;
  title: string;
  description: string;
  status?: string;
}

interface OrthoCase {
  id: string;
  caseNumber: string;
  applianceType?: string;
  status: string;
  stagePercentage: number;
  currentStage?: string;
  doctorName?: string;
}

interface SurgeryCase {
  id: string;
  caseNumber: string;
  surgeryType: string;
  status: string;
  doctorName?: string;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const ORTHO_STATUS_LABELS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };

const EVENT_ICONS: Record<string, { icon: LucideIcon; color: string; bg: string }> = {
  appointment: { icon: Calendar, color: "text-[#3d7ab5]", bg: "bg-[#3d7ab5]" },
  visit: { icon: ClipboardList, color: "text-green-600", bg: "bg-green-600" },
  payment: { icon: CreditCard, color: "text-purple-600", bg: "bg-purple-600" },
  document: { icon: FolderOpen, color: "text-amber-600", bg: "bg-amber-600" },
  photo: { icon: Camera, color: "text-pink-600", bg: "bg-pink-600" },
  radiograph: { icon: ScanLine, color: "text-purple-600", bg: "bg-purple-600" },
};

const EVENT_TYPE_LABELS: Record<string, string> = {
  appointment: "موعد",
  visit: "زيارة",
  payment: "دفعة",
  document: "مستند",
  photo: "صورة",
  radiograph: "أشعة",
};

interface OverviewTabProps {
  patientId: string;
  summary: PatientSummary | null;
  patient: PatientProfile;
  canViewFinance?: boolean;
  onAddVisit?: () => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function OverviewTab({ patientId, summary, patient, canViewFinance = false, onAddVisit }: OverviewTabProps) {
  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [orthoCases, setOrthoCases] = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Ortho case overview (next appointment + contract remaining). Fetched
  // opportunistically for the active ortho case only — keeps the patient file
  // summary card useful without forcing every patient into an extra round-trip
  // when they have no ortho case.
  const [orthoOverview, setOrthoOverview] = useState<OrthoOverview | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    Promise.all([
      api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`).then((r) => r.data).catch(() => []),
      api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=5`).then((r) => r.data).catch(() => []),
      api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientId}&pageSize=5`).then((r) => r.data.data ?? []).catch(() => []),
    ]).then(([timeline, ortho, surgery]) => {
      setEvents(timeline);
      setOrthoCases(ortho);
      setSurgeryCases(surgery);
    }).catch(() => {
      setError("حدث خطأ أثناء تحميل البيانات");
    }).finally(() => {
      setLoading(false);
    });
  }, [patientId]);

  const activeOrthoCase = orthoCases.find((c) => c.status?.toLowerCase() === "active") ?? null;

  // Once we know the active ortho case id, fetch its overview (best-effort —
  // a 404/403 just leaves the summary card without the next-appointment /
  // contract-remaining lines; the card still shows stage + appliance).
  useEffect(() => {
    if (!activeOrthoCase) {
      setOrthoOverview(null);
      return;
    }
    let cancelled = false;
    api.get<OrthoOverview>(`/api/ortho-cases/${activeOrthoCase.id}/overview`)
      .then((r) => { if (!cancelled) setOrthoOverview(r.data); })
      .catch(() => { if (!cancelled) setOrthoOverview(null); });
    return () => { cancelled = true; };
  }, [activeOrthoCase?.id]);

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-20 bg-[#f1f5f9] rounded-xl" />
        <div className="grid grid-cols-2 gap-3">
          <div className="h-28 bg-[#f1f5f9] rounded-xl" />
          <div className="h-28 bg-[#f1f5f9] rounded-xl" />
        </div>
        <div className="h-32 bg-[#f1f5f9] rounded-xl" />
        <div className="h-48 bg-[#f1f5f9] rounded-xl" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-center py-12">
        <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
        <p className="text-sm text-red-500">{error}</p>
        <button
          onClick={() => window.location.reload()}
          className="mt-3 text-xs font-semibold text-[#3d7ab5] hover:underline"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  const whatsappNumber = patient.whatsApp || patient.phone;
  const patientName = `${patient.firstName} ${patient.lastName}`;

  return (
    <div className="space-y-5">

      {/* ════════════════════ Quick Actions ════════════════════ */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
        <button
          onClick={onAddVisit}
          className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl text-white transition shadow-sm"
          style={{ background: "#22c55e" }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "#16a34a")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "#22c55e")}
        >
          <Plus className="w-3.5 h-3.5" />
          إضافة زيارة
        </button>
        <Link
          href={`/appointments/new?patientId=${patientId}&patientName=${encodeURIComponent(patientName)}`}
          className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl transition shadow-sm"
          style={{ border: "1.5px solid #3d7ab5", color: "#3d7ab5", background: "#fff" }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "#eef3f9")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
        >
          <Calendar className="w-3.5 h-3.5" />
          حجز موعد
        </Link>
        {canViewFinance && (
          <Link
            href={financeV3CollectionsUrl(patientId)}
            className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl transition shadow-sm"
            style={{ border: "1.5px solid #3d7ab5", color: "#3d7ab5", background: "#fff" }}
            onMouseEnter={(e) => (e.currentTarget.style.background = "#eef3f9")}
            onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
          >
            <Wallet className="w-3.5 h-3.5" />
            إضافة دفعة
          </Link>
        )}
        <Link
          href={`/patients/${patientId}?tab=documents`}
          className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl transition shadow-sm"
          style={{ border: "1.5px solid #dce8f5", color: "#64748b", background: "#fff" }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "#eef3f9")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
        >
          <Camera className="w-3.5 h-3.5" />
          رفع صورة/مستند
        </Link>
        <Link
          href={`/messages?patientId=${patientId}`}
          className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl transition shadow-sm"
          style={{ border: "1.5px solid #dce8f5", color: "#64748b", background: "#fff" }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "#eef3f9")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
        >
          <MessageCircle className="w-3.5 h-3.5" />
          إرسال رسالة
        </Link>
        <Link
          href={`/patients/${patientId}/print/summary`}
          className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl transition shadow-sm"
          style={{ border: "1.5px solid #3d7ab5", color: "#3d7ab5", background: "#fff" }}
          onMouseEnter={(e) => (e.currentTarget.style.background = "#eef3f9")}
          onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
        >
          <Printer className="w-3.5 h-3.5" />
          طباعة ملخص المريض
        </Link>
        {whatsappNumber && (
          <a
            href={`https://wa.me/${whatsappNumber.startsWith("0") ? "967" + whatsappNumber.slice(1) : whatsappNumber}`}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center justify-center gap-1.5 px-3.5 py-2 text-xs font-semibold rounded-xl transition shadow-sm"
            style={{ border: "1.5px solid #25D366", color: "#25D366", background: "#fff" }}
            onMouseEnter={(e) => (e.currentTarget.style.background = "#f0fdf4")}
            onMouseLeave={(e) => (e.currentTarget.style.background = "#fff")}
          >
            <Phone className="w-3.5 h-3.5" />
            فتح واتساب
          </a>
        )}
      </div>

      {/* ════════════════════ Ortho Case Summary ════════════════════ */}
      {activeOrthoCase && (
        <div className="rounded-xl border-2 border-violet-200 bg-violet-50/60 p-4">
          <div className="flex items-center justify-between gap-2 mb-2.5 flex-wrap">
            <div className="flex items-center gap-2">
              <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-violet-100">
                <Activity className="w-3.5 h-3.5 text-violet-600" />
              </div>
              <h3 className="text-sm font-bold text-[#0d2137]">حالة التقويم</h3>
              <span className="text-xs px-2 py-0.5 rounded-full font-semibold bg-violet-100 text-violet-700">
                نشطة
              </span>
            </div>
            <Link
              href={`/ortho/${activeOrthoCase.id}`}
              className="flex items-center gap-1 px-3 py-1.5 text-xs font-semibold rounded-lg bg-violet-600 text-white hover:bg-violet-700 transition"
            >
              فتح ملف التقويم
              <ChevronLeft className="w-3 h-3" />
            </Link>
          </div>
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-xs">
            <span className="font-medium text-[#0d2137]">{activeOrthoCase.caseNumber}</span>
            {activeOrthoCase.applianceType && (
              <span className="text-[#64748b]">الجهاز: {activeOrthoCase.applianceType}</span>
            )}
            {activeOrthoCase.currentStage && (
              <span className="text-[#64748b]">المرحلة الحالية: {activeOrthoCase.currentStage}</span>
            )}
            {activeOrthoCase.doctorName && (
              <span className="text-[#64748b]">د. {activeOrthoCase.doctorName}</span>
            )}
            {/* Next appointment — pulled from the ortho overview so the orthodontist
                can see at a glance when the patient is due back without leaving the
                patient file. Best-effort: hidden when the overview call failed or
                no next appointment is recorded. */}
            {orthoOverview?.nextAppointmentDate && (
              <span className="text-[#64748b] inline-flex items-center gap-1">
                <Calendar className="w-3 h-3" />
                الموعد القادم: {formatArabicDate(orthoOverview.nextAppointmentDate)}
              </span>
            )}
            {/* Contract remaining — only shown to users who can view finance.
                The amount is the contract total minus what's been paid so far,
                straight from OrthoOverview (computed server-side). */}
            {canViewFinance
              && typeof orthoOverview?.contractRemaining === "number"
              && orthoOverview.contractRemaining > 0 && (
              <span className="text-[#64748b] inline-flex items-center gap-1">
                <Wallet className="w-3 h-3" />
                المتبقي على العقد: {orthoOverview.contractRemaining.toLocaleString("ar-YE")} ر.ي
              </span>
            )}
          </div>
          <div className="flex items-center gap-2 mt-2.5">
            <div className="flex-1 h-1.5 bg-violet-100 rounded-full overflow-hidden">
              <div
                className="h-full bg-violet-500 rounded-full"
                style={{ width: `${Math.min(100, Math.max(0, activeOrthoCase.stagePercentage ?? 0))}%` }}
              />
            </div>
            <span className="text-xs font-semibold text-violet-700">{activeOrthoCase.stagePercentage ?? 0}%</span>
          </div>
        </div>
      )}

      {/* ════════════════════ Medical Alerts ════════════════════ */}
      {summary?.medicalAlerts && summary.medicalAlerts.length > 0 && (
        <div className="rounded-xl p-4 border-2 border-red-200 bg-red-50">
          <div className="flex items-center gap-2 mb-2">
            <AlertTriangle className="w-4 h-4 text-red-600" />
            <h3 className="text-sm font-bold text-red-700">تنبيهات طبية</h3>
          </div>
          <div className="flex flex-wrap gap-2">
            {summary.medicalAlerts.map((alert, idx) => (
              <span key={idx} className="text-xs px-2.5 py-1 rounded-full bg-red-100 text-red-700 font-semibold">
                {alert}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* ════════════════════ Patient Clinical Overview ════════════════════ */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        {/* Last Visit */}
        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-green-50">
              <ClipboardList className="w-3.5 h-3.5 text-green-600" />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">آخر زيارة</span>
          </div>
          {summary?.lastVisitDate ? (
            <div>
              <p className="text-sm font-bold text-[#0d2137]">{formatArabicDate(summary.lastVisitDate)}</p>
              {summary.lastVisitDoctor && <p className="text-xs text-[#64748b] mt-0.5">د. {summary.lastVisitDoctor}</p>}
              {summary.lastVisitDiagnosis && <p className="text-xs text-[#94a3b8] mt-0.5 truncate">{summary.lastVisitDiagnosis}</p>}
            </div>
          ) : (
            <p className="text-xs text-[#94a3b8]">لا توجد زيارات سابقة</p>
          )}
        </div>

        {/* Next Appointment */}
        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-[#3d7ab518]">
              <Calendar className="w-3.5 h-3.5 text-[#3d7ab5]" />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">الموعد القادم</span>
          </div>
          {summary?.nextAppointmentDate ? (
            <div>
              <p className="text-sm font-bold text-[#0d2137]">
                {formatArabicDate(summary.nextAppointmentDate)}
                {summary.nextAppointmentTime && (
                  <span className="text-[#64748b] font-normal"> · {summary.nextAppointmentTime}</span>
                )}
              </p>
              {summary.nextAppointmentType && <p className="text-xs text-[#64748b] mt-0.5">{summary.nextAppointmentType}</p>}
              {summary.nextAppointmentDoctor && <p className="text-xs text-[#94a3b8] mt-0.5">د. {summary.nextAppointmentDoctor}</p>}
            </div>
          ) : (
            <p className="text-xs text-[#94a3b8]">لا يوجد موعد قادم</p>
          )}
        </div>

        {/* Outstanding Balance — Admin / Accountant only */}
        {canViewFinance ? (
          <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
            <div className="flex items-center gap-2 mb-2">
              <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-orange-50">
                <Wallet className="w-3.5 h-3.5 text-orange-600" />
              </div>
              <span className="text-xs font-semibold text-[#94a3b8]">الرصيد المتبقي</span>
            </div>
            <p className="text-sm font-bold text-orange-600">
              {summary?.totalOutstanding != null ? `${summary.totalOutstanding.toLocaleString()} ر.ي` : "—"}
            </p>
            <p className="text-xs text-[#94a3b8] mt-0.5">
              مدفوع: {summary?.totalPaid != null ? `${summary.totalPaid.toLocaleString()} ر.ي` : "—"}
            </p>
            {/* QA-596: surface unbilled sessions as provisional debt */}
            {summary?.unbilledVisitsAmount != null && summary.unbilledVisitsAmount > 0 && (
              <p className="text-[10px] text-amber-700 mt-1 leading-snug" title="جلسات أُجريت بدون فاتورة أو عقد — يُنصح بإنشاء فاتورة لها">
                منها {summary.unbilledVisitsAmount.toLocaleString()} ر.ي من جلسات غير مفوترة
              </p>
            )}
          </div>
        ) : null}

        {/* Primary Doctor / Branch */}
        <div className="rounded-xl p-3.5 border border-[#e8f0f9] bg-white">
          <div className="flex items-center gap-2 mb-2">
            <div className="w-7 h-7 rounded-lg flex items-center justify-center bg-[#3d7ab518]">
              <Stethoscope className="w-3.5 h-3.5 text-[#3d7ab5]" />
            </div>
            <span className="text-xs font-semibold text-[#94a3b8]">الطبيب / الفرع</span>
          </div>
          <p className="text-sm font-bold text-[#0d2137]">
            {patient.primaryDoctorName || "غير محدد"}
          </p>
          <p className="text-xs text-[#94a3b8] mt-0.5">
            {patient.branchName || "—"}
          </p>
        </div>
      </div>

      {/* ════════════════════ Clinical Summary Card ════════════════════ */}
      <div className="rounded-xl border border-[#e8f0f9] bg-white overflow-hidden">
        <div className="px-4 py-3 flex items-center justify-between" style={{ background: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
          <div className="flex items-center gap-2">
            <FileText className="w-4 h-4 text-[#3d7ab5]" />
            <h3 className="text-sm font-bold text-[#0d2137]">الملخص السريري</h3>
          </div>
          <Link
            href={`/patients/${patientId}?tab=dental`}
            className="text-xs font-semibold text-[#3d7ab5] flex items-center gap-1 hover:underline"
          >
            التفاصيل
            <ChevronLeft className="w-3 h-3" />
          </Link>
        </div>
        <div className="p-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Chief Complaint */}
            <div>
              <p className="text-xs font-semibold text-[#94a3b8] mb-1">الشكوى الرئيسية</p>
              <p className="text-sm text-[#0d2137]">
                {summary?.chiefComplaint || patient.dentalHistory?.chiefComplaint || "غير مسجلة"}
              </p>
            </div>
            {/* Current Diagnosis */}
            <div>
              <p className="text-xs font-semibold text-[#94a3b8] mb-1">التشخيص الحالي</p>
              <p className="text-sm text-[#0d2137]">
                {summary?.currentDiagnosis || "غير مسجل"}
              </p>
            </div>
            {/* Next Planned Step */}
            <div>
              <p className="text-xs font-semibold text-[#94a3b8] mb-1">الخطوة المخططة</p>
              <p className="text-sm text-[#0d2137]">
                {summary?.nextPlannedStep || "غير محددة"}
              </p>
            </div>
            {/* Treatment Status */}
            <div>
              <p className="text-xs font-semibold text-[#94a3b8] mb-1">حالة العلاج</p>
              {(summary?.activeOrthoSummary && summary.activeOrthoSummary.length > 0) ||
               (summary?.activeSurgerySummary && summary.activeSurgerySummary.length > 0) ? (
                <div className="flex flex-wrap gap-1.5">
                  {summary.activeOrthoSummary && summary.activeOrthoSummary.length > 0 && (
                    <span className="text-xs px-2 py-0.5 rounded-full bg-purple-50 text-purple-700 font-semibold">
                      تقويم ({summary.activeOrthoSummary.length})
                    </span>
                  )}
                  {summary.activeSurgerySummary && summary.activeSurgerySummary.length > 0 && (
                    <span className="text-xs px-2 py-0.5 rounded-full bg-red-50 text-red-700 font-semibold">
                      جراحة ({summary.activeSurgerySummary.length})
                    </span>
                  )}
                </div>
              ) : (
                <p className="text-sm text-[#64748b]">لا يوجد علاج نشط</p>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ════════════════════ Active Cases ════════════════════ */}
      {(orthoCases.length > 0 || surgeryCases.length > 0) && (
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-[#0d2137]">الحالات النشطة</h3>
          {orthoCases.length > 0 && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-[#94a3b8] uppercase tracking-wide">الحالات التقويمية</p>
              {orthoCases.map((c) => (
                <Link key={c.id} href={`/ortho/${c.id}`}
                  className="flex items-center justify-between p-2.5 bg-[#f7fafd] rounded-lg hover:bg-[#eef3f9] border border-transparent transition"
                >
                  <div className="flex items-center gap-2">
                    <Activity className="w-3.5 h-3.5 text-[#3d7ab5] flex-shrink-0" />
                    <span className="text-sm font-medium text-[#0d2137]">{c.caseNumber}</span>
                    {c.applianceType && <span className="text-xs text-[#64748b]">{c.applianceType}</span>}
                  </div>
                  <div className="flex items-center gap-3">
                    <div className="flex items-center gap-1.5">
                      <div className="w-16 h-1.5 bg-[#dce8f5] rounded-full overflow-hidden">
                        <div className="h-full bg-[#3d7ab5] rounded-full" style={{ width: `${c.stagePercentage}%` }} />
                      </div>
                      <span className="text-xs text-[#64748b]">{c.stagePercentage}%</span>
                    </div>
                    <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                      c.status === "active" ? "bg-[#3d7ab518] text-[#3d7ab5]" : "bg-[#f1f5f9] text-[#64748b]"
                    )}>
                      {ORTHO_STATUS_LABELS[c.status] ?? c.status}
                    </span>
                  </div>
                </Link>
              ))}
            </div>
          )}
          {surgeryCases.length > 0 && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-[#94a3b8] uppercase tracking-wide">الحالات الجراحية</p>
              {surgeryCases.map((c) => (
                <Link key={c.id} href={`/surgery/${c.id}`}
                  className="flex items-center justify-between p-2.5 bg-[#f7fafd] rounded-lg hover:bg-red-50 hover:border-red-200 border border-transparent transition"
                >
                  <div className="flex items-center gap-2">
                    <Scissors className="w-3.5 h-3.5 text-red-600 flex-shrink-0" />
                    <span className="text-sm font-medium text-[#0d2137]">{c.caseNumber}</span>
                    <span className="text-xs text-[#64748b]">{c.surgeryType}</span>
                  </div>
                  <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                    c.status === "completed" ? "bg-green-50 text-green-700" :
                    c.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
                    "bg-[#f1f5f9] text-[#64748b]"
                  )}>
                    {SURGERY_STATUS_LABELS[c.status] ?? c.status}
                  </span>
                </Link>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ════════════════════ Patient Timeline ════════════════════ */}
      <div className="rounded-xl border border-[#e8f0f9] bg-white overflow-hidden">
        <div className="px-4 py-3 flex items-center justify-between" style={{ background: "#f7fafd", borderBottom: "1px solid #e8f0f9" }}>
          <div className="flex items-center gap-2">
            <Clock className="w-4 h-4 text-[#3d7ab5]" />
            <h3 className="text-sm font-bold text-[#0d2137]">السجل الزمني</h3>
          </div>
          <Link
            href={`/patients/${patientId}?tab=timeline`}
            className="text-xs font-semibold text-[#3d7ab5] flex items-center gap-1 hover:underline"
          >
            عرض الكل
            <ChevronLeft className="w-3 h-3" />
          </Link>
        </div>
        <div className="p-4">
          {events.length === 0 ? (
            <EmptyState
              icon={Clock}
              title="لا يوجد نشاط بعد"
              description="ستظهر هنا المواعيد والزيارات والمدفوعات والمستندات"
            />
          ) : (
            <div className="relative">
              <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-[#f1f5f9]" />
              <div className="space-y-3">
                {events.slice(0, 8).map((ev) => {
                  const eventStyle = EVENT_ICONS[ev.type] ?? EVENT_ICONS.appointment;
                  const EventIcon = eventStyle.icon;
                  return (
                    <div key={`${ev.type}-${ev.id}`} className="flex gap-3 relative">
                      <div className={cn("w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 z-10 bg-white border-2", eventStyle.bg.replace("bg-", "border-"))}>
                        <EventIcon className={cn("w-3.5 h-3.5", eventStyle.color)} />
                      </div>
                      <div className="flex-1 bg-[#f7fafd] rounded-lg p-2.5 border border-[#e8f0f9]">
                        <div className="flex items-center justify-between gap-2 flex-wrap">
                          <div className="flex items-center gap-2">
                            <span className="text-xs px-1.5 py-0.5 rounded font-medium bg-[#f1f5f9] text-[#64748b]">
                              {EVENT_TYPE_LABELS[ev.type] ?? ev.type}
                            </span>
                            <span className="font-semibold text-xs text-[#0d2137]">{ev.title}</span>
                          </div>
                          {ev.status && (
                            <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", STATUS_COLORS[ev.status] ?? "bg-[#f1f5f9] text-[#64748b]")}>
                              {ev.status === "signed" ? "موقع" : (APPOINTMENT_STATUS_LABELS[ev.status] ?? ev.status)}
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-[#64748b] mt-0.5">{ev.description}</p>
                        <p className="text-xs text-[#94a3b8] mt-1">{formatArabicDate(ev.date)}</p>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      </div>

    </div>
  );
}
