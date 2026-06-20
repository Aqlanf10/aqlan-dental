"use client";

import { useState, useMemo, useCallback } from "react";
import { printScreen } from "@/lib/printUtils";
import { useParams, useRouter } from "next/navigation";
import { printScreen } from "@/lib/printUtils";
import Link from "next/link";
import { printScreen } from "@/lib/printUtils";
import {
import { printScreen } from "@/lib/printUtils";
  Route, Stethoscope, CreditCard, Wrench, HeartPulse, Clock,
  FileText, RefreshCw, CalendarCheck, UserCheck,
  Megaphone, DoorOpen, PlayCircle, AlertTriangle,
  CheckCircle2, UserX, Ban,
  Calendar, Printer, Camera,
  Plus, Save, ArrowUpRight, CircleDot, Info, ShieldAlert,
  BadgeDollarSign, Receipt, FileSpreadsheet, ExternalLink,
  Wallet,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { printScreen } from "@/lib/printUtils";
import { useAuthStore } from "@/stores/authStore";
import { printScreen } from "@/lib/printUtils";
import { toast } from "@/stores/toastStore";
import { printScreen } from "@/lib/printUtils";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { printScreen } from "@/lib/printUtils";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { printScreen } from "@/lib/printUtils";
import {
import { printScreen } from "@/lib/printUtils";
  useDailyJourneySummary,
  useJourneyIntake,
  useJourneySendToQueue,
  useJourneyStartVisit,
  useJourneyHandoff,
  useJourneyCreateDraftInvoice,
} from "@/hooks/usePatientJourney";
import {
import { printScreen } from "@/lib/printUtils";
  JOURNEY_STEPS,
  getStepIndex,
  APPOINTMENT_STATUS_ARABIC,
  NEXT_ACTION_ARABIC,
} from "@/types/journey";
import type {
import { printScreen } from "@/lib/printUtils";
  TimelineEvent,
  DailyJourneyRecentVisit,
} from "@/types/journey";
import { Skeleton, CardSkeleton } from "@/components/ui/skeleton";
import { printScreen } from "@/lib/printUtils";
import api from "@/lib/api";
import { printScreen } from "@/lib/printUtils";
// FE-10: import shared helpers instead of re-declaring locally (was drifted: ar-SA vs ar-YE locale).
import {
import { printScreen } from "@/lib/printUtils";
  fmtRial,
  fmtDate,
  fmtTime,
  getInitials,
  SEVERITY_STYLES,
  TIMELINE_DOT_COLORS,
} from "@/components/shared/journey/constants";

// ─── Panel Keys ─────────────────────────────────────────────────────────────

type PanelKey = "journey" | "visit" | "payment" | "ortho" | "history" | "timeline" | "docs";

// FE-10: fmtRial, fmtDate, fmtTime, getInitials, SEVERITY_STYLES, TIMELINE_DOT_COLORS
// are now imported from @/components/shared/journey/constants (was re-declared locally with
// drifted ar-SA locale for fmtRial vs ar-YE in the shared version).

// ─── Step Status Determination ─────────────────────────────────────────────

function getStepStatus(stepKey: string, currentStep: string): "done" | "current" | "pending" {
  const currentIdx = getStepIndex(currentStep);
  const stepIdx = getStepIndex(stepKey);
  if (currentIdx < 0 || stepIdx < 0) return "pending";
  if (stepIdx < currentIdx) return "done";
  if (stepIdx === currentIdx) return "current";
  return "pending";
}

// ─── Main Page Component ────────────────────────────────────────────────────

export default function PatientDailyJourneyHub() {
  const params = useParams();
  const router = useRouter();
  const patientId = params.patientId as string;

  const [activePanel, setActivePanel] = useState<PanelKey>("journey");

  // Intake form state
  const [intakeComplaint, setIntakeComplaint] = useState("");
  const [intakeNotes, setIntakeNotes] = useState("");
  const [showIntakeForm, setShowIntakeForm] = useState(false);

  // Visit form state
  const [visitForm, setVisitForm] = useState({
    visitType: "",
    chiefComplaint: "",
    diagnosis: "",
    treatmentDone: "",
    instructions: "",
    nextVisitPlan: "",
    cost: 0,
    nextVisitDate: "",
  });

  // Payment form state
  const [paymentAmount, setPaymentAmount] = useState(0);
  const [paymentMethod, setPaymentMethod] = useState("cash");
  const [paymentNote, setPaymentNote] = useState("");

  // Handoff form state
  const [handoffForm, setHandoffForm] = useState({
    treatmentDone: "",
    diagnosis: "",
    nextVisitPlan: "",
    instructions: "",
    amountDue: 0,
    notes: "",
  });
  const [showHandoffForm, setShowHandoffForm] = useState(false);

  // Hooks
  const { data, isLoading, error, refetch } = useDailyJourneySummary(patientId);
  const intakeMutation = useJourneyIntake();
  const sendToQueueMutation = useJourneySendToQueue();
  const startVisitMutation = useJourneyStartVisit();
  const handoffMutation = useJourneyHandoff();
  const draftInvoiceMutation = useJourneyCreateDraftInvoice();

  const { user } = useAuthStore();
  const userRole = user?.role ?? "";

  // Permissions — use proper permission system, not just isDoctor
  const canViewJourney = useHasPermission(PERMISSION_KEYS.PATIENT_JOURNEY_VIEW);
  const canEditJourney = useHasPermission(PERMISSION_KEYS.PATIENT_JOURNEY_EDIT);
  const canViewVisits = useHasPermission(PERMISSION_KEYS.VISITS_VIEW);
  const canEditVisits = useHasPermission(PERMISSION_KEYS.VISITS_EDIT);
  const canViewFinance = useHasPermission(PERMISSION_KEYS.PAYMENTS_VIEW);
  const canCreatePayment = useHasPermission(PERMISSION_KEYS.PAYMENTS_CREATE);
  const canViewCheckout = useHasPermission(PERMISSION_KEYS.CHECKOUT_VIEW);
  const canViewInvoices = useHasPermission(PERMISSION_KEYS.INVOICES_VIEW);
  const canCreateInvoices = useHasPermission(PERMISSION_KEYS.INVOICES_CREATE);

  const isDoctor = userRole === "Orthodontist" || userRole === "GeneralDentist" || userRole === "OralSurgeon";

  // ─── Action Handlers ────────────────────────────────────────────────────────

  const handleIntake = useCallback(async () => {
    if (!data?.todayAppointment?.id) return;
    try {
      await intakeMutation.mutateAsync({
        appointmentId: data.todayAppointment.id,
        body: {
          chiefComplaint: intakeComplaint || undefined,
          notes: intakeNotes || undefined,
        },
      });
      toast.success("تم تسجيل الوصول بنجاح");
      setShowIntakeForm(false);
      setIntakeComplaint("");
      setIntakeNotes("");
    } catch {
      toast.error("فشل تسجيل الوصول");
    }
  }, [data?.todayAppointment?.id, intakeComplaint, intakeNotes, intakeMutation]);

  const handleSendToQueue = useCallback(async () => {
    if (!data?.todayAppointment?.id) return;
    try {
      await sendToQueueMutation.mutateAsync({
        appointmentId: data.todayAppointment.id,
      });
      toast.success("تم إضافة المريض للانتظار");
    } catch {
      toast.error("فشل إضافة المريض للانتظار");
    }
  }, [data?.todayAppointment?.id, sendToQueueMutation]);

  const handleCallPatient = useCallback(async () => {
    if (!data?.queueStatus?.id) return;
    try {
      await api.post(`/api/clinic-queue/${data.queueStatus.id}/call`);
      toast.success("تم نداء المريض");
      refetch();
    } catch {
      toast.error("فشل نداء المريض");
    }
  }, [data?.queueStatus?.id, refetch]);

  const handleEnterRoom = useCallback(async () => {
    if (!data?.queueStatus?.id) return;
    try {
      await api.post(`/api/clinic-queue/${data.queueStatus.id}/enter-room`);
      toast.success("تم تسجيل دخول الغرفة");
      refetch();
    } catch {
      toast.error("فشل تسجيل دخول الغرفة");
    }
  }, [data?.queueStatus?.id, refetch]);

  const handleStartVisit = useCallback(async () => {
    if (!data?.todayAppointment?.id) return;
    try {
      await startVisitMutation.mutateAsync(data.todayAppointment.id);
      toast.success("تم بدء الزيارة");
    } catch {
      toast.error("فشل بدء الزيارة");
    }
  }, [data?.todayAppointment?.id, startVisitMutation]);

  const handleHandoff = useCallback(async () => {
    if (!data?.todayVisit?.id) return;
    try {
      await handoffMutation.mutateAsync({
        visitId: data.todayVisit.id,
        body: {
          treatmentDone: handoffForm.treatmentDone || undefined,
          diagnosis: handoffForm.diagnosis || undefined,
          nextVisitPlan: handoffForm.nextVisitPlan || undefined,
          instructions: handoffForm.instructions || undefined,
          amountDue: handoffForm.amountDue || undefined,
          notes: handoffForm.notes || undefined,
        },
      });
      toast.success("تم تسليم المريض للاستقبال");
      setShowHandoffForm(false);
    } catch {
      toast.error("فشل تسليم المريض");
    }
  }, [data?.todayVisit?.id, handoffForm, handoffMutation]);

  const handleCreateDraftInvoice = useCallback(async () => {
    if (!data?.todayVisit?.id) return;
    try {
      await draftInvoiceMutation.mutateAsync(data.todayVisit.id);
      toast.success("تم إنشاء الفاتورة المسودة");
    } catch {
      toast.error("فشل إنشاء الفاتورة المسودة");
    }
  }, [data?.todayVisit?.id, draftInvoiceMutation]);

  const handleNoShow = useCallback(async () => {
    if (!data?.todayAppointment?.id) return;
    try {
      await api.put(`/api/appointments/${data.todayAppointment.id}/status`, { status: "NoShow" });
      toast.success("تم تسجيل عدم الحضور");
      refetch();
    } catch {
      toast.error("فشل تحديث الحالة");
    }
  }, [data?.todayAppointment?.id, refetch]);

  const handleCancel = useCallback(async () => {
    if (!data?.todayAppointment?.id) return;
    try {
      await api.put(`/api/appointments/${data.todayAppointment.id}/status`, { status: "Cancelled" });
      toast.success("تم إلغاء الموعد");
      refetch();
    } catch {
      toast.error("فشل إلغاء الموعد");
    }
  }, [data?.todayAppointment?.id, refetch]);

  const handleSaveVisit = useCallback(async () => {
    if (!patientId) return;
    try {
      if (data?.todayVisit?.id) {
        await api.put(`/api/visits/${data.todayVisit.id}`, visitForm);
        toast.success("تم تحديث الزيارة");
      } else {
        await api.post(`/api/visits`, {
          ...visitForm,
          patientId,
          appointmentId: data?.todayAppointment?.id,
        });
        toast.success("تم حفظ الزيارة");
      }
      refetch();
    } catch {
      toast.error("فشل حفظ الزيارة");
    }
  }, [patientId, data?.todayVisit?.id, data?.todayAppointment?.id, visitForm, refetch]);

  // ─── Sidebar Navigation Items ──────────────────────────────────────────────

  const sidebarNav = useMemo(() => {
    const items: { key: PanelKey; label: string; icon: React.ElementType; section: string; badge?: string; badgeType?: string; hidden?: boolean }[] = [
      { key: "journey", label: "رحلة المريض", icon: Route, section: "رحلة اليوم", hidden: !canViewJourney, badge: data?.nextAction && data.nextAction !== "None" ? NEXT_ACTION_ARABIC[data.nextAction] ?? undefined : undefined, badgeType: "teal" },
      { key: "visit", label: "تسجيل الزيارة", icon: Stethoscope, section: "رحلة اليوم", hidden: !canViewVisits },
      { key: "payment", label: "الدفع والحساب", icon: CreditCard, section: "رحلة اليوم", badge: data?.financeSummary?.outstandingBalance ? data.financeSummary.outstandingBalance.toLocaleString("ar-SA") : undefined, badgeType: "warn", hidden: !(canViewFinance || canViewCheckout) },
      { key: "ortho", label: "ملف التقويم", icon: Wrench, section: "الملف السريري", badge: data?.activeOrthoCase ? "نشط" : undefined, badgeType: "teal", hidden: !canViewVisits },
      { key: "history", label: "التاريخ الطبي", icon: HeartPulse, section: "الملف السريري", hidden: !canViewVisits },
      { key: "timeline", label: "السجل الزمني", icon: Clock, section: "الملف السريري", badge: data?.timeline?.length ? data.timeline.length.toLocaleString("ar-SA") : undefined, hidden: !canViewJourney },
      { key: "docs", label: "المستندات", icon: FileText, section: "المستندات", hidden: !canViewJourney },
    ];
    return items.filter((i) => !i.hidden);
  }, [data, canViewJourney, canViewVisits, canViewFinance, canViewCheckout]);

  // ─── Render: Loading State ──────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex h-[calc(100vh-4rem)]">
        <div className="w-[280px] border-l border-gray-200 bg-white p-4 space-y-4">
          <Skeleton className="h-48 w-full rounded-xl" />
          <Skeleton className="h-16 w-full rounded-lg" />
          <div className="space-y-2 mt-4">
            {Array.from({ length: 7 }).map((_, i) => (
              <Skeleton key={i} className="h-9 w-full rounded-lg" />
            ))}
          </div>
        </div>
        <div className="flex-1 p-6 space-y-4">
          <Skeleton className="h-12 w-full rounded-lg" />
          <div className="grid grid-cols-3 gap-4">
            <CardSkeleton />
            <CardSkeleton />
            <CardSkeleton />
          </div>
          <Skeleton className="h-40 w-full rounded-xl" />
        </div>
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="flex items-center justify-center h-[calc(100vh-4rem)]">
        <div className="text-center space-y-4">
          <AlertTriangle className="w-12 h-12 text-[#ba7517] mx-auto" />
          <p className="text-lg font-bold text-[#1a3a5c]">فشل تحميل بيانات رحلة المريض</p>
          <p className="text-sm text-gray-500">{(error as Error)?.message ?? "حدث خطأ غير متوقع"}</p>
          <button
            onClick={() => refetch()}
            className="px-4 py-2 text-sm font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition"
          >
            <RefreshCw className="w-4 h-4 inline ml-1" />
            إعادة المحاولة
          </button>
          <button
            onClick={() => router.push("/patient-journey")}
            className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition mr-2"
          >
            العودة لقائمة الرحلات
          </button>
        </div>
      </div>
    );
  }

  const { patient, todayAppointment, todayVisit, financeSummary, activeContract, activeOrthoCase, medicalAlerts, recentVisits, timeline, journeyStep, nextAction, unpaidInvoicesCount } = data;
  const currentStepIdx = getStepIndex(journeyStep);
  const payPercent = financeSummary && financeSummary.totalTreatmentCost && financeSummary.totalTreatmentCost > 0
    ? Math.round(((financeSummary.totalPaid ?? 0) / financeSummary.totalTreatmentCost) * 100)
    : 0;

  // ─── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="flex h-[calc(100vh-4rem)] overflow-hidden bg-[#f1efe8]">
      {/* ═══ SIDEBAR ═══ */}
      <aside className="w-[280px] flex-shrink-0 bg-white border-l border-[#d3d1c7] flex flex-col overflow-y-auto">
        {/* Patient Card */}
        <div className="p-4 bg-[#1a3a5c]">
          <div className="flex items-start gap-3">
            <div className="w-12 h-12 rounded-full bg-[#3d7ab5] flex items-center justify-center text-lg font-bold text-white flex-shrink-0">
              {getInitials(patient.fullName)}
            </div>
            <div className="flex-1 min-w-0">
              <h3 className="text-[15px] font-bold text-white truncate">{patient.fullName}</h3>
              <p className="text-[11px] text-white/60 font-mono mt-0.5">{patient.patientNumber}</p>
              <div className="flex flex-wrap gap-1.5 mt-2">
                {journeyStep && (
                  <span className={cn(
                    "inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-semibold",
                    journeyStep === "Completed" ? "bg-[#c0dd97] text-[#274f0a]" :
                    journeyStep === "InProgress" || journeyStep === "InRoom" ? "bg-[#9fe1cb] text-[#085041]" :
                    journeyStep === "Waiting" || journeyStep === "Called" ? "bg-[#b5d4f4] text-[#0c447c]" :
                    "bg-[#fac775] text-[#633806]"
                  )}>
                    {APPOINTMENT_STATUS_ARABIC[journeyStep] ?? journeyStep}
                  </span>
                )}
                {activeOrthoCase && (
                  <span className="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-semibold bg-[#9fe1cb] text-[#085041]">
                    تقويم نشط
                  </span>
                )}
              </div>
            </div>
          </div>
          {/* Patient Details */}
          <div className="mt-3 bg-white/[0.08] rounded-lg p-2.5 flex gap-4">
            {patient.age != null && (
              <div>
                <div className="text-[9px] text-white/50">العمر</div>
                <div className="text-[13px] font-bold text-white">{patient.age} سنة</div>
              </div>
            )}
            {patient.gender && (
              <div>
                <div className="text-[9px] text-white/50">الجنس</div>
                <div className="text-[13px] font-bold text-white">
                  {patient.gender === "Male" ? "ذكر" : patient.gender === "Female" ? "أنثى" : patient.gender}
                </div>
              </div>
            )}
            {patient.phone && !isDoctor && (
              <div>
                <div className="text-[9px] text-white/50">الهاتف</div>
                <div className="text-[11px] font-semibold text-[#9fe1cb]" dir="ltr">{patient.phone}</div>
              </div>
            )}
          </div>
        </div>

        {/* Today's Appointment Strip */}
        {todayAppointment && (
          <div className="px-3.5 py-2.5 bg-[#faeeda] border-b border-[#fac775]/50 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-[#ba7517] flex-shrink-0" />
            <div className="flex-1 min-w-0">
              <div className="text-[11px] font-bold text-[#633806]">
                موعد اليوم — {fmtTime(todayAppointment.startTime)}
              </div>
              <div className="text-[10px] text-[#854f0b] truncate">
                {todayAppointment.doctorName}
                {todayAppointment.appointmentType ? ` — ${todayAppointment.appointmentType}` : ""}
              </div>
            </div>
            <span className={cn(
              "inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold",
              todayAppointment.status === "Completed" ? "bg-[#eaf3de] text-[#3b6d11]" :
              todayAppointment.status === "InProgress" || todayAppointment.status === "InRoom" ? "bg-[#9fe1cb] text-[#2d5e8e]" :
              todayAppointment.status === "Cancelled" || todayAppointment.status === "NoShow" ? "bg-[#fcebeb] text-[#a32d2d]" :
              "bg-[#fac775]/50 text-[#633806]"
            )}>
              {APPOINTMENT_STATUS_ARABIC[todayAppointment.status] ?? todayAppointment.status}
            </span>
          </div>
        )}

        {/* Navigation Sections */}
        <nav className="flex-1 py-1">
          {(() => {
            let lastSection = "";
            return sidebarNav.map((item) => {
              const showSection = item.section !== lastSection;
              lastSection = item.section;
              return (
                <div key={item.key}>
                  {showSection && (
                    <div className="px-3.5 py-2.5 text-[10px] font-bold text-[#5f5e5a] uppercase tracking-wide border-b border-[#d3d1c7]/50">
                      {item.section}
                    </div>
                  )}
                  <button
                    onClick={() => {
                      if (item.key === "docs") {
                        router.push(`/patients/${patientId}`);
                      } else {
                        setActivePanel(item.key);
                      }
                    }}
                    className={cn(
                      "w-full flex items-center gap-2 px-3.5 py-2.5 text-start transition-colors border-b border-black/[0.04]",
                      activePanel === item.key
                        ? "bg-[#e1f5ee] border-r-[3px] border-r-[#3d7ab5]"
                        : "hover:bg-[#f8f9fa]"
                    )}
                  >
                    <item.icon className={cn(
                      "w-4 h-4 flex-shrink-0",
                      activePanel === item.key ? "text-[#2d5e8e]" : "text-[#5f5e5a]"
                    )} />
                    <span className={cn(
                      "flex-1 text-[12px]",
                      activePanel === item.key ? "text-[#2d5e8e] font-semibold" : "text-[#2c2c2a]"
                    )}>
                      {item.label}
                    </span>
                    {item.badge && (
                      <span className={cn(
                        "text-[10px] px-1.5 py-0.5 rounded-full font-bold",
                        item.badgeType === "warn" ? "bg-[#faeeda] text-[#ba7517]" :
                        item.badgeType === "danger" ? "bg-[#fcebeb] text-[#a32d2d]" :
                        "bg-[#e1f5ee] text-[#2d5e8e]"
                      )}>
                        {item.badge}
                      </span>
                    )}
                  </button>
                </div>
              );
            });
          })()}
        </nav>

        {/* Quick Actions */}
        <div className="p-3 border-t border-[#d3d1c7] space-y-2">
          <button
            onClick={() => setActivePanel("visit")}
            className="w-full flex items-center justify-center gap-1.5 px-3 py-2 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition"
          >
            <Plus className="w-3.5 h-3.5" />
            تسجيل زيارة جديدة
          </button>
          <button
            onClick={() => printScreen()}
            className="w-full flex items-center justify-center gap-1.5 px-3 py-2 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
          >
            <Printer className="w-3.5 h-3.5" />
            طباعة ملخص
          </button>
        </div>
      </aside>

      {/* ═══ MAIN CONTENT ═══ */}
      <main className="flex-1 overflow-y-auto">
        {/* ─── Panel: Journey ─── */}
        {activePanel === "journey" && (
          <div>
            {/* Section Bar */}
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <Route className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">رحلة المريض اليوم</h2>
                <p className="text-[11px] text-[#5f5e5a]">تتبع الحالة من لحظة الوصول حتى انتهاء الجلسة</p>
              </div>
              <button
                onClick={() => refetch()}
                className="flex items-center gap-1 px-2.5 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
              >
                <RefreshCw className="w-3.5 h-3.5" />
                تحديث
              </button>
            </div>

            <div className="p-4 space-y-4">
              {/* Step Flow */}
              <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                <div className="flex items-center justify-between mb-3">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5">
                    <CircleDot className="w-4 h-4 text-[#3d7ab5]" />
                    مسار الزيارة
                  </span>
                  <span className={cn(
                    "inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold",
                    journeyStep === "Completed" ? "bg-[#eaf3de] text-[#3b6d11]" :
                    ["InProgress", "InRoom"].includes(journeyStep) ? "bg-[#9fe1cb] text-[#2d5e8e]" :
                    ["Waiting", "Called"].includes(journeyStep) ? "bg-[#faeeda] text-[#633806]" :
                    "bg-[#faeeda] text-[#633806]"
                  )}>
                    {APPOINTMENT_STATUS_ARABIC[journeyStep] ?? journeyStep}
                  </span>
                </div>
                <div className="flex items-center gap-0 overflow-x-auto pb-2">
                  {JOURNEY_STEPS.map((step, idx) => {
                    const status = getStepStatus(step.key, journeyStep);
                    return (
                      <div key={step.key} className="flex items-center">
                        <div className="flex flex-col items-center gap-1 min-w-[72px]">
                          <div className={cn(
                            "w-8 h-8 rounded-full flex items-center justify-center text-[14px] font-bold border-2",
                            status === "done" && "bg-[#3d7ab5] text-white border-[#3d7ab5]",
                            status === "current" && "bg-white text-[#3d7ab5] border-[#3d7ab5]",
                            status === "pending" && "bg-[#f1efe8] text-[#5f5e5a] border-[#d3d1c7]"
                          )}>
                            {status === "done" ? <CheckCircle2 className="w-4 h-4" /> : idx + 1}
                          </div>
                          <span className={cn(
                            "text-[9px] text-center",
                            status === "current" ? "text-[#2d5e8e] font-bold" : "text-[#5f5e5a]"
                          )}>
                            {step.label}
                          </span>
                        </div>
                        {idx < JOURNEY_STEPS.length - 1 && (
                          <div className={cn(
                            "flex-1 h-0.5 min-w-[16px]",
                            getStepIndex(step.key) < currentStepIdx ? "bg-[#3d7ab5]" : "bg-[#d3d1c7]"
                          )} />
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Appointment Info + Actions Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Today's Appointment Card */}
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                    <CalendarCheck className="w-4 h-4 text-[#3d7ab5]" />
                    موعد اليوم
                  </span>
                  {todayAppointment ? (
                    <div className="space-y-2">
                      <div className="flex flex-col gap-1.5">
                        <div>
                          <span className="text-[10px] text-[#5f5e5a] font-semibold">الطبيب</span>
                          <p className="text-[12px] text-[#2c2c2a] font-medium">{todayAppointment.doctorName}</p>
                        </div>
                        <div>
                          <span className="text-[10px] text-[#5f5e5a] font-semibold">الوقت</span>
                          <p className="text-[12px] text-[#2c2c2a] font-medium">
                            {fmtTime(todayAppointment.startTime)}
                            {todayAppointment.endTime ? ` — ${fmtTime(todayAppointment.endTime)}` : ""}
                          </p>
                        </div>
                        {todayAppointment.appointmentType && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">نوع الزيارة</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{todayAppointment.appointmentType}</p>
                          </div>
                        )}
                        {todayAppointment.roomName && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">الغرفة</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{todayAppointment.roomName}</p>
                          </div>
                        )}
                        {todayAppointment.specialty && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">التخصص</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{todayAppointment.specialty}</p>
                          </div>
                        )}
                      </div>
                      {/* Quick Actions */}
                      {canEditJourney && nextAction && nextAction !== "None" && nextAction !== "InProgress" && (
                        <div className="flex flex-wrap gap-2 pt-2 border-t border-[#d3d1c7]/50">
                          {nextAction === "Intake" && !showIntakeForm && (
                            <button onClick={() => setShowIntakeForm(true)} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition">
                              <UserCheck className="w-3.5 h-3.5" />
                              تسجيل الوصول
                            </button>
                          )}
                          {nextAction === "SendToQueue" && (
                            <button onClick={handleSendToQueue} disabled={sendToQueueMutation.isPending} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                              <ArrowUpRight className="w-3.5 h-3.5" />
                              {sendToQueueMutation.isPending ? "جارٍ..." : "إضافة للانتظار"}
                            </button>
                          )}
                          {nextAction === "CallPatient" && (
                            <button onClick={handleCallPatient} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                              <Megaphone className="w-3.5 h-3.5" />
                              نداء المريض
                            </button>
                          )}
                          {nextAction === "EnterRoom" && (
                            <button onClick={handleEnterRoom} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                              <DoorOpen className="w-3.5 h-3.5" />
                              دخول الغرفة
                            </button>
                          )}
                          {nextAction === "StartVisit" && (
                            <button onClick={handleStartVisit} disabled={startVisitMutation.isPending} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                              <PlayCircle className="w-3.5 h-3.5" />
                              {startVisitMutation.isPending ? "جارٍ..." : "بدء الزيارة"}
                            </button>
                          )}
                          {nextAction === "Checkout" && (
                            <button onClick={() => setActivePanel("payment")} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3b6d11] text-white hover:bg-[#2d540d] transition disabled:opacity-50">
                              <CreditCard className="w-3.5 h-3.5" />
                              الحساب والخروج
                            </button>
                          )}
                        </div>
                      )}
                      {nextAction === "InProgress" && (
                        <div className="flex items-center gap-2 pt-2 border-t border-[#d3d1c7]/50">
                          <span className="inline-flex items-center gap-1 text-[11px] text-[#2d5e8e] font-semibold">
                            <Stethoscope className="w-3.5 h-3.5" />
                            عند الطبيب
                          </span>
                          {isDoctor && canEditVisits && todayVisit?.id && (
                            <button onClick={() => setShowHandoffForm(true)} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50 mr-auto">
                              <ArrowUpRight className="w-3.5 h-3.5" />
                              تسليم للاستقبال
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  ) : (
                    <p className="text-[12px] text-[#5f5e5a] text-center py-4">لا يوجد موعد مجدول اليوم</p>
                  )}
                </div>

                {/* Complaints & Instructions */}
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                    <Info className="w-4 h-4 text-[#3d7ab5]" />
                    الشكوى والتعليمات
                  </span>
                  <div className="space-y-2">
                    {todayVisit?.chiefComplaint ? (
                      <div>
                        <span className="text-[10px] text-[#5f5e5a] font-semibold">الشكوى اليوم</span>
                        <p className="text-[12px] text-[#2c2c2a] font-medium">{todayVisit.chiefComplaint}</p>
                      </div>
                    ) : todayAppointment?.notes ? (
                      <div>
                        <span className="text-[10px] text-[#5f5e5a] font-semibold">ملاحظات الموعد</span>
                        <p className="text-[12px] text-[#2c2c2a] font-medium">{todayAppointment.notes}</p>
                      </div>
                    ) : null}
                    {todayVisit?.instructions && (
                      <div>
                        <span className="text-[10px] text-[#5f5e5a] font-semibold">تعليمات سابقة</span>
                        <p className="text-[12px] text-[#2c2c2a] font-medium">{todayVisit.instructions}</p>
                      </div>
                    )}
                    {todayVisit?.treatmentDone && (
                      <div>
                        <span className="text-[10px] text-[#5f5e5a] font-semibold">ما تم عمله</span>
                        <p className="text-[12px] text-[#2c2c2a] font-medium">{todayVisit.treatmentDone}</p>
                      </div>
                    )}
                    {medicalAlerts.length > 0 && (
                      <div className="flex flex-wrap gap-1.5 mt-2">
                        {medicalAlerts.slice(0, 3).map((alert, i) => (
                          <span key={i} className={cn(
                            "inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold",
                            alert.severity === "danger" ? "bg-[#fcebeb] text-[#a32d2d]" :
                            alert.severity === "warning" ? "bg-[#faeeda] text-[#ba7517]" :
                            "bg-[#e6f1fb] text-[#185fa5]"
                          )}>
                            <AlertTriangle className="w-3 h-3" />
                            {alert.label}
                          </span>
                        ))}
                      </div>
                    )}
                    {!todayVisit?.chiefComplaint && !todayAppointment?.notes && !todayVisit?.instructions && medicalAlerts.length === 0 && (
                      <p className="text-[12px] text-[#5f5e5a] text-center py-4">لا توجد شكاوى أو تعليمات مسجلة</p>
                    )}
                  </div>
                </div>
              </div>

              {/* Intake Form (Inline) */}
              {showIntakeForm && nextAction === "Intake" && (
                <div className="bg-[#e1f5ee] rounded-xl border border-[#9fe1cb] p-4 space-y-3">
                  <span className="text-[12px] font-bold text-[#2d5e8e] flex items-center gap-1.5">
                    <UserCheck className="w-4 h-4" />
                    تسجيل الوصول
                  </span>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#2d5e8e]">الشكوى الرئيسية</label>
                      <input
                        value={intakeComplaint}
                        onChange={(e) => setIntakeComplaint(e.target.value)}
                        placeholder="مثل: ألم في الضرس"
                        className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      />
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#2d5e8e]">ملاحظات</label>
                      <input
                        value={intakeNotes}
                        onChange={(e) => setIntakeNotes(e.target.value)}
                        placeholder="ملاحظات إضافية..."
                        className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      />
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <button
                      onClick={handleIntake}
                      disabled={intakeMutation.isPending}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
                    >
                      <Save className="w-3.5 h-3.5" />
                      {intakeMutation.isPending ? "جارٍ الحفظ..." : "تسجيل الوصول وإدخال الانتظار"}
                    </button>
                    <button onClick={() => setShowIntakeForm(false)} className="px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition">
                      إلغاء
                    </button>
                  </div>
                </div>
              )}

              {/* Handoff Form (Inline) */}
              {showHandoffForm && isDoctor && canEditVisits && todayVisit?.id && (
                <div className="bg-[#e1f5ee] rounded-xl border border-[#9fe1cb] p-4 space-y-3">
                  <span className="text-[12px] font-bold text-[#2d5e8e] flex items-center gap-1.5">
                    <ArrowUpRight className="w-4 h-4" />
                    تسليم المريض للاستقبال
                  </span>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#2d5e8e]">ما تم عمله</label>
                      <textarea
                        value={handoffForm.treatmentDone}
                        onChange={(e) => setHandoffForm((f) => ({ ...f, treatmentDone: e.target.value }))}
                        className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10 h-16 resize-none"
                      />
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#2d5e8e]">التشخيص</label>
                      <input
                        value={handoffForm.diagnosis}
                        onChange={(e) => setHandoffForm((f) => ({ ...f, diagnosis: e.target.value }))}
                        className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      />
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#2d5e8e]">تعليمات</label>
                      <textarea
                        value={handoffForm.instructions}
                        onChange={(e) => setHandoffForm((f) => ({ ...f, instructions: e.target.value }))}
                        className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10 h-16 resize-none"
                      />
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#2d5e8e]">المبلغ المطلوب</label>
                      <input
                        type="number"
                        value={handoffForm.amountDue}
                        onChange={(e) => setHandoffForm((f) => ({ ...f, amountDue: Number(e.target.value) }))}
                        dir="ltr"
                        min={0}
                        className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      />
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <button onClick={handleHandoff} disabled={handoffMutation.isPending} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                      <Save className="w-3.5 h-3.5" />
                      {handoffMutation.isPending ? "جارٍ التسليم..." : "تسليم للاستقبال"}
                    </button>
                    <button onClick={() => setShowHandoffForm(false)} className="px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition">
                      إلغاء
                    </button>
                  </div>
                </div>
              )}

              {/* Alert Boxes */}
              {financeSummary && financeSummary.overdueAmount > 0 && canViewFinance && (
                <div className="flex gap-2 p-3 rounded-lg bg-[#faeeda] border border-[#fac775]/50 text-[#633806]">
                  <AlertTriangle className="w-4 h-4 text-[#ba7517] flex-shrink-0 mt-0.5" />
                  <div>
                    <div className="font-bold text-[12px] mb-1">تنبيه: دفعة متأخرة</div>
                    <p className="text-[12px]">المبلغ المتأخر {fmtRial(financeSummary.overdueAmount)}. يُنصح بمراجعة المريض قبل المغادرة.</p>
                  </div>
                </div>
              )}
              {medicalAlerts.filter((a) => a.severity === "danger").length > 0 && (
                <div className="flex gap-2 p-3 rounded-lg bg-[#fcebeb] border border-[#f09595]/50 text-[#a32d2d]">
                  <ShieldAlert className="w-4 h-4 flex-shrink-0 mt-0.5" />
                  <div>
                    <div className="font-bold text-[12px] mb-1">تنبيه طبي عاجل</div>
                    <p className="text-[12px]">
                      {medicalAlerts.filter((a) => a.severity === "danger").map((a) => `${a.label}: ${a.value}`).join(" — ")}
                    </p>
                  </div>
                </div>
              )}

              {/* Finance Snapshot */}
              {financeSummary && (canViewFinance || canViewCheckout) && (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <div className="flex items-center justify-between mb-3">
                    <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5">
                      <Wallet className="w-4 h-4 text-[#3d7ab5]" />
                      الوضع المالي السريع
                    </span>
                    <button
                      onClick={() => setActivePanel("payment")}
                      className="px-2.5 py-1 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
                    >
                      عرض التفاصيل
                    </button>
                  </div>
                  <div className={cn("grid gap-3", financeSummary.totalTreatmentCost != null ? "grid-cols-3" : "grid-cols-2")}>
                    {financeSummary.totalPaid != null && (
                    <div className="text-center p-2.5 bg-[#eaf3de] rounded-lg">
                      <div className="text-[10px] text-[#3b6d11] mb-1">إجمالي المدفوع</div>
                      <div className="text-[18px] font-extrabold text-[#3b6d11]">
                        {financeSummary.totalPaid.toLocaleString("ar-SA")}
                      </div>
                      <div className="text-[9px] text-[#3b6d11]">ريال</div>
                    </div>
                    )}
                    <div className="text-center p-2.5 bg-[#fcebeb] rounded-lg">
                      <div className="text-[10px] text-[#a32d2d] mb-1">المتبقي</div>
                      <div className="text-[18px] font-extrabold text-[#a32d2d]">
                        {financeSummary.outstandingBalance.toLocaleString("ar-SA")}
                      </div>
                      <div className="text-[9px] text-[#a32d2d]">ريال</div>
                    </div>
                    <div className="text-center p-2.5 bg-[#e6f1fb] rounded-lg">
                      <div className="text-[10px] text-[#185fa5] mb-1">المتأخر</div>
                      <div className="text-[18px] font-extrabold text-[#185fa5]">
                        {financeSummary.overdueAmount.toLocaleString("ar-SA")}
                      </div>
                      <div className="text-[9px] text-[#185fa5]">ريال</div>
                    </div>
                  </div>
                  {financeSummary.totalTreatmentCost != null && (
                  <div className="mt-3">
                    <div className="flex justify-between text-[10px] text-[#5f5e5a] mb-1">
                      <span>نسبة السداد</span>
                      <span>{payPercent}%</span>
                    </div>
                    <div className="bg-[#f1efe8] rounded h-1.5 overflow-hidden">
                      <div className="h-full rounded bg-[#3d7ab5] transition-all duration-500" style={{ width: `${payPercent}%` }} />
                    </div>
                  </div>
                  )}
                </div>
              )}
            </div>

            {/* Sticky Bottom Actions */}
            {canEditJourney && todayAppointment && nextAction !== "None" && journeyStep !== "Completed" && (
              <div className="sticky bottom-0 bg-white border-t border-[#d3d1c7] px-4 py-2.5 flex gap-2 justify-end">
                {nextAction !== "InProgress" && (
                  <>
                    <button
                      onClick={handleNoShow}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#a32d2d] text-white hover:opacity-90 transition"
                    >
                      <UserX className="w-3.5 h-3.5" />
                      لم يحضر
                    </button>
                    <button
                      onClick={handleCancel}
                      className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
                    >
                      <Ban className="w-3.5 h-3.5" />
                      إلغاء الموعد
                    </button>
                  </>
                )}
                {nextAction === "Checkout" && (
                  <button
                    onClick={() => setActivePanel("payment")}
                    className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition"
                  >
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    إنهاء الجلسة والحساب
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        {/* ─── Panel: Visit ─── */}
        {activePanel === "visit" && (
          <div>
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <Stethoscope className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">تسجيل الزيارة</h2>
                <p className="text-[11px] text-[#5f5e5a]">تفاصيل ما تم في هذه الجلسة</p>
              </div>
            </div>
            <div className="p-4 space-y-4">
              {/* Visit Form */}
              <div className="bg-[#e1f5ee] rounded-xl border border-[#9fe1cb] p-4">
                <span className="text-[12px] font-bold text-[#2d5e8e] flex items-center gap-1.5 mb-3">
                  <Save className="w-4 h-4" />
                  بيانات الزيارة الحالية
                </span>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
                  <div className="flex flex-col gap-1">
                    <label className="text-[10px] font-bold text-[#2d5e8e]">نوع الزيارة</label>
                    <select
                      value={visitForm.visitType}
                      onChange={(e) => setVisitForm((f) => ({ ...f, visitType: e.target.value }))}
                      className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                    >
                      <option value="">اختر النوع</option>
                      <option value="استشارة">استشارة</option>
                      <option value="تفعيل شهري تقويم">تفعيل شهري تقويم</option>
                      <option value="إزالة الجهاز">إزالة الجهاز</option>
                      <option value="طارئ">طارئ</option>
                      <option value="تسجيل سجلات">تسجيل سجلات</option>
                      <option value="علاج عام">علاج عام</option>
                      <option value="متابعة">متابعة</option>
                    </select>
                  </div>
                  <div className="flex flex-col gap-1">
                    <label className="text-[10px] font-bold text-[#2d5e8e]">تكلفة الزيارة</label>
                    <input
                      type="number"
                      value={visitForm.cost}
                      onChange={(e) => setVisitForm((f) => ({ ...f, cost: Number(e.target.value) }))}
                      dir="ltr"
                      min={0}
                      placeholder="0"
                      className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                    />
                  </div>
                </div>
                <div className="flex flex-col gap-1 mb-3">
                  <label className="text-[10px] font-bold text-[#2d5e8e]">الشكوى الرئيسية</label>
                  <input
                    value={visitForm.chiefComplaint}
                    onChange={(e) => setVisitForm((f) => ({ ...f, chiefComplaint: e.target.value }))}
                    placeholder="وصف الشكوى..."
                    className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                  />
                </div>
                <div className="flex flex-col gap-1 mb-3">
                  <label className="text-[10px] font-bold text-[#2d5e8e]">التشخيص</label>
                  <input
                    value={visitForm.diagnosis}
                    onChange={(e) => setVisitForm((f) => ({ ...f, diagnosis: e.target.value }))}
                    placeholder="التشخيص..."
                    className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                  />
                </div>
                <div className="flex flex-col gap-1 mb-3">
                  <label className="text-[10px] font-bold text-[#2d5e8e]">ما تم عمله</label>
                  <textarea
                    value={visitForm.treatmentDone}
                    onChange={(e) => setVisitForm((f) => ({ ...f, treatmentDone: e.target.value }))}
                    placeholder="وصف تفصيلي للإجراءات..."
                    className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10 h-16 resize-none"
                  />
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-3">
                  <div className="flex flex-col gap-1">
                    <label className="text-[10px] font-bold text-[#2d5e8e]">التعليمات للمريض</label>
                    <textarea
                      value={visitForm.instructions}
                      onChange={(e) => setVisitForm((f) => ({ ...f, instructions: e.target.value }))}
                      placeholder="تعليمات ما بعد الجلسة..."
                      className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10 h-16 resize-none"
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <label className="text-[10px] font-bold text-[#2d5e8e]">خطة الزيارة القادمة</label>
                    <textarea
                      value={visitForm.nextVisitPlan}
                      onChange={(e) => setVisitForm((f) => ({ ...f, nextVisitPlan: e.target.value }))}
                      placeholder="ما المخطط للزيارة التالية..."
                      className="border border-[#9fe1cb] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10 h-16 resize-none"
                    />
                  </div>
                </div>
                <div className="flex gap-2">
                  {canEditVisits && (
                    <button onClick={handleSaveVisit} className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50">
                      <Save className="w-3.5 h-3.5" />
                      حفظ الزيارة
                    </button>
                  )}
                  <button className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition">
                    <Camera className="w-3.5 h-3.5" />
                    إضافة صور
                  </button>
                  <button className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition">
                    <Printer className="w-3.5 h-3.5" />
                    طباعة التعليمات
                  </button>
                </div>
              </div>

              {/* Last 3 Visits */}
              {recentVisits.length > 0 && (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <div className="flex items-center justify-between mb-3">
                    <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5">
                      <Clock className="w-4 h-4 text-[#3d7ab5]" />
                      آخر 3 زيارات
                    </span>
                    <button onClick={() => setActivePanel("timeline")} className="px-2.5 py-1 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition">
                      الكل
                    </button>
                  </div>
                  <div className="space-y-0">
                    {recentVisits.slice(0, 3).map((v) => (
                      <VisitTimelineItem key={v.id} visit={v} />
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ─── Panel: Payment ─── */}
        {activePanel === "payment" && (canViewFinance || canViewCheckout) && (
          <div>
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <CreditCard className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">الدفع والحساب</h2>
                <p className="text-[11px] text-[#5f5e5a]">العقد، الدفعات، والرصيد المتبقي</p>
              </div>
            </div>
            <div className="p-4 space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Active Contract */}
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                    <Receipt className="w-4 h-4 text-[#3d7ab5]" />
                    العقد الحالي
                  </span>
                  {activeContract ? (
                    <div className="space-y-2">
                      <div>
                        <span className="text-[10px] text-[#5f5e5a] font-semibold">رقم العقد</span>
                        <p className="text-[12px] text-[#2c2c2a] font-medium">#{activeContract.id.slice(0, 8)}</p>
                      </div>
                      {activeContract.specialty && (
                        <div>
                          <span className="text-[10px] text-[#5f5e5a] font-semibold">التخصص</span>
                          <p className="text-[12px] text-[#2c2c2a] font-medium">{activeContract.specialty}</p>
                        </div>
                      )}
                      <div>
                        <span className="text-[10px] text-[#5f5e5a] font-semibold">إجمالي العقد</span>
                        <p className="text-[18px] font-extrabold text-[#1a3a5c]">{fmtRial(activeContract.totalAmount)}</p>
                      </div>
                      <div className="h-px bg-[#d3d1c7]" />
                      <div className="flex gap-4">
                        <div className="text-center">
                          <div className="text-[9px] text-[#3b6d11]">مدفوع</div>
                          <div className="text-[18px] font-extrabold text-[#3b6d11]">
                            {activeContract.paidAmount.toLocaleString("ar-SA")}
                          </div>
                        </div>
                        <div className="text-center">
                          <div className="text-[9px] text-[#a32d2d]">متبقي</div>
                          <div className="text-[18px] font-extrabold text-[#a32d2d]">
                            {activeContract.remainingAmount.toLocaleString("ar-SA")}
                          </div>
                        </div>
                        {financeSummary?.overdueAmount ? (
                          <div className="text-center">
                            <div className="text-[9px] text-[#185fa5]">متأخر</div>
                            <div className="text-[18px] font-extrabold text-[#185fa5]">
                              {financeSummary.overdueAmount.toLocaleString("ar-SA")}
                            </div>
                          </div>
                        ) : null}
                      </div>
                      <div className="bg-[#f1efe8] rounded h-1.5 overflow-hidden">
                        <div
                          className="h-full rounded bg-[#3d7ab5] transition-all duration-500"
                          style={{ width: `${activeContract.totalAmount > 0 ? Math.round((activeContract.paidAmount / activeContract.totalAmount) * 100) : 0}%` }}
                        />
                      </div>
                      <div className="text-[10px] text-[#5f5e5a] text-start">
                        {activeContract.totalAmount > 0 ? Math.round((activeContract.paidAmount / activeContract.totalAmount) * 100) : 0}% مسدد
                      </div>
                    </div>
                  ) : (
                    <p className="text-[12px] text-[#5f5e5a] text-center py-4">لا يوجد عقد نشط</p>
                  )}
                </div>

                {/* New Payment Form */}
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                    <BadgeDollarSign className="w-4 h-4 text-[#3d7ab5]" />
                    تسجيل دفعة جديدة
                  </span>
                  <div className="space-y-2">
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#5f5e5a]">المبلغ (ريال)</label>
                      <input
                        type="number"
                        value={paymentAmount || ""}
                        onChange={(e) => setPaymentAmount(Number(e.target.value))}
                        dir="ltr"
                        placeholder="0"
                        min={0}
                        className="border border-[#d3d1c7] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      />
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#5f5e5a]">طريقة الدفع</label>
                      <select
                        value={paymentMethod}
                        onChange={(e) => setPaymentMethod(e.target.value)}
                        className="border border-[#d3d1c7] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      >
                        <option value="cash">نقداً</option>
                        <option value="transfer">تحويل بنكي</option>
                        <option value="card">بطاقة</option>
                      </select>
                    </div>
                    <div className="flex flex-col gap-1">
                      <label className="text-[10px] font-bold text-[#5f5e5a]">ملاحظة</label>
                      <input
                        value={paymentNote}
                        onChange={(e) => setPaymentNote(e.target.value)}
                        placeholder="اختياري..."
                        className="border border-[#d3d1c7] rounded-lg px-2.5 py-1.5 text-[12px] bg-white text-[#2c2c2a] focus:outline-none focus:border-[#3d7ab5] focus:ring-2 focus:ring-[#3d7ab5]/10"
                      />
                    </div>
                    {canCreatePayment && (
                      <button
                        onClick={async () => {
                          if (paymentAmount <= 0) { toast.error("أدخل مبلغاً صحيحاً"); return; }
                          try {
                            await api.post("/api/payments", {
                              patientId,
                              amount: paymentAmount,
                              paymentMethod,
                              notes: paymentNote || undefined,
                            });
                            toast.success("تم تسجيل الدفعة بنجاح");
                            setPaymentAmount(0);
                            setPaymentNote("");
                            refetch();
                          } catch {
                            toast.error("فشل تسجيل الدفعة");
                          }
                        }}
                        className="flex items-center gap-1 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50 w-full justify-center"
                      >
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        تسجيل الدفع وإصدار إيصال
                      </button>
                    )}
                  </div>
                </div>
              </div>

              {/* Checkout Card */}
              {canViewCheckout && todayAppointment && (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                    <CreditCard className="w-4 h-4 text-[#3d7ab5]" />
                    إنهاء الحساب والخروج
                  </span>
                  <div className="space-y-3">
                    {todayVisit?.id ? (
                      <>
                        {canCreateInvoices && (
                          <button
                          onClick={handleCreateDraftInvoice}
                          disabled={draftInvoiceMutation.isPending}
                          className="flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-semibold rounded-lg bg-[#185fa5] text-white hover:opacity-90 transition disabled:opacity-50"
                        >
                          <FileSpreadsheet className="w-3.5 h-3.5" />
                          {draftInvoiceMutation.isPending ? "جارٍ الإنشاء..." : "إنشاء فاتورة مسودة"}
                        </button>
                        )}
                        {canViewInvoices && (
                          <Link
                            href={`/finance-v3?tab=invoices&patientId=${patientId}`}
                            className="flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
                          >
                            <Receipt className="w-3.5 h-3.5" />
                            دفع فاتورة صادرة
                          </Link>
                        )}
                        {financeSummary?.latestPayment?.id ? (
                          <button
                            type="button"
                            onClick={async () => {
                              try {
                                const filename = financeSummary.latestPayment!.receiptNumber
                                  ? `receipt-${financeSummary.latestPayment!.receiptNumber}.pdf`
                                  : `receipt-${financeSummary.latestPayment!.id}.pdf`;
                                await downloadPdfFromApi(`/api/payments/${financeSummary.latestPayment!.id}/pdf`, filename);
                              } catch {
                                toast.error("فشل تحميل إيصال الدفعة");
                              }
                            }}
                            className="flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition text-right"
                          >
                            <Printer className="w-3.5 h-3.5" />
                            طباعة إيصال
                          </button>
                        ) : (
                          <span className="text-[10px] text-gray-400">لا توجد دفعة لطباعة إيصال</span>
                        )}
                      </>
                    ) : (
                      <div className="flex gap-2 p-3 rounded-lg bg-[#e6f1fb] border border-[#85b7eb]/50 text-[#185fa5]">
                        <Info className="w-4 h-4 flex-shrink-0 mt-0.5" />
                        <p className="text-[12px]">أنشئ فاتورة من زيارة اليوم أولاً</p>
                      </div>
                    )}
                    {paymentAmount > 0 && !activeContract && (
                      <div className="flex gap-2 p-3 rounded-lg bg-[#faeeda] border border-[#fac775]/50 text-[#633806]">
                        <AlertTriangle className="w-4 h-4 text-[#ba7517] flex-shrink-0 mt-0.5" />
                        <p className="text-[12px]">هذه الدفعة غير مرتبطة بفاتورة أو عقد وقد تؤثر على دقة الحساب</p>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* Recent Payments Table */}
              {financeSummary && (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                    <FileText className="w-4 h-4 text-[#3d7ab5]" />
                    آخر الدفعات
                  </span>
                  {financeSummary.latestPayment ? (
                    <table className="w-full text-[11px] border-collapse">
                      <thead>
                        <tr>
                          <th className="text-start px-2 py-1.5 bg-[#f1efe8] text-[#5f5e5a] font-bold text-[10px]">التاريخ</th>
                          <th className="text-start px-2 py-1.5 bg-[#f1efe8] text-[#5f5e5a] font-bold text-[10px]">المبلغ</th>
                          <th className="text-start px-2 py-1.5 bg-[#f1efe8] text-[#5f5e5a] font-bold text-[10px]">الطريقة</th>
                          <th className="text-start px-2 py-1.5 bg-[#f1efe8] text-[#5f5e5a] font-bold text-[10px]">إيصال</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr className="border-b border-[#f1efe8]">
                          <td className="px-2 py-2">{fmtDate(financeSummary.latestPayment.paymentDate)}</td>
                          <td className="px-2 py-2 text-[#3b6d11] font-bold">{fmtRial(financeSummary.latestPayment.amount)}</td>
                          <td className="px-2 py-2">{financeSummary.latestPayment.paymentMethod ?? "—"}</td>
                          <td className="px-2 py-2">
                            <Link
                              href={`/patients/${patientId}`}
                              className="text-[10px] font-semibold text-[#185fa5] hover:underline"
                            >
                              طباعة
                            </Link>
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  ) : (
                    <p className="text-[12px] text-[#5f5e5a] text-center py-4">لا توجد دفعات مسجلة</p>
                  )}
                  <div className="flex gap-2 mt-3">
                    {financeSummary.totalPaymentsCount != null && (
                    <span className="text-[10px] text-[#5f5e5a]">إجمالي الدفعات: <strong className="text-[#1a3a5c]">{financeSummary.totalPaymentsCount}</strong></span>
                    )}
                    {financeSummary.activeContractsCount != null && (
                    <span className="text-[10px] text-[#5f5e5a]">العقود النشطة: <strong className="text-[#1a3a5c]">{financeSummary.activeContractsCount}</strong></span>
                    )}
                    {unpaidInvoicesCount > 0 && (
                      <span className="text-[10px] text-[#a32d2d] font-bold">فواتير غير مدفوعة: {unpaidInvoicesCount}</span>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ─── Panel: Ortho ─── */}
        {activePanel === "ortho" && (
          <div>
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <Wrench className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">ملف التقويم</h2>
                <p className="text-[11px] text-[#5f5e5a]">
                  {activeOrthoCase ? `${activeOrthoCase.caseNumber ?? activeOrthoCase.id.slice(0, 8)} — بدأ ${activeOrthoCase.startDate ? fmtDate(activeOrthoCase.startDate) : ""}` : "لا يوجد ملف تقويم نشط"}
                </p>
              </div>
              {activeOrthoCase && (
                <Link
                  href={`/ortho/${activeOrthoCase.id}`}
                  className="flex items-center gap-1 px-2.5 py-1 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
                >
                  فتح الملف الكامل
                  <ExternalLink className="w-3 h-3" />
                </Link>
              )}
            </div>
            <div className="p-4 space-y-4">
              {activeOrthoCase ? (
                <>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {/* Overview */}
                    <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                      <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                        <CircleDot className="w-4 h-4 text-[#3d7ab5]" />
                        نظرة عامة
                      </span>
                      <div className="space-y-2">
                        {activeOrthoCase.applianceType && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">نوع الجهاز</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{activeOrthoCase.applianceType}</p>
                          </div>
                        )}
                        <div>
                          <span className="text-[10px] text-[#5f5e5a] font-semibold">الحالة</span>
                          <span className={cn(
                            "inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold mr-1",
                            activeOrthoCase.status === "Active" ? "bg-[#9fe1cb] text-[#2d5e8e]" :
                            activeOrthoCase.status === "Completed" ? "bg-[#c0dd97] text-[#274f0a]" :
                            "bg-[#faeeda] text-[#633806]"
                          )}>
                            {activeOrthoCase.status === "Active" ? "نشط" :
                             activeOrthoCase.status === "Completed" ? "مكتمل" :
                             activeOrthoCase.status === "Paused" ? "متوقف" : activeOrthoCase.status}
                          </span>
                        </div>
                        {activeOrthoCase.startDate && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">تاريخ البدء</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{fmtDate(activeOrthoCase.startDate)}</p>
                          </div>
                        )}
                        {activeOrthoCase.expectedDurationMonths && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">المدة المتوقعة</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{activeOrthoCase.expectedDurationMonths} شهر</p>
                          </div>
                        )}
                        {activeOrthoCase.currentStage && (
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">المرحلة الحالية</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{activeOrthoCase.currentStage}</p>
                          </div>
                        )}
                        <div className="h-px bg-[#d3d1c7]" />
                        <div>
                          <div className="text-[10px] text-[#5f5e5a] mb-1">نسبة الإنجاز</div>
                          <div className="bg-[#f1efe8] rounded h-1.5 overflow-hidden">
                            <div className="h-full rounded bg-[#3d7ab5] transition-all duration-500" style={{ width: `${activeOrthoCase.stagePercentage ?? (activeOrthoCase.status === "Completed" ? 100 : 0)}%` }} />
                          </div>
                          <div className="text-[10px] text-[#2d5e8e] font-bold mt-1">
                            {activeOrthoCase.stagePercentage ?? (activeOrthoCase.status === "Completed" ? 100 : 0)}% مكتمل
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Contract Info */}
                    <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                      <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                        <Receipt className="w-4 h-4 text-[#3d7ab5]" />
                        العقد المرتبط
                      </span>
                      {activeContract ? (
                        <div className="space-y-2">
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">إجمالي العقد</span>
                            <p className="text-[18px] font-extrabold text-[#1a3a5c]">{fmtRial(activeContract.totalAmount)}</p>
                          </div>
                          <div className="flex gap-4">
                            <div className="text-center">
                              <div className="text-[9px] text-[#3b6d11]">مدفوع</div>
                              <div className="text-[18px] font-extrabold text-[#3b6d11]">
                                {activeContract.paidAmount.toLocaleString("ar-SA")}
                              </div>
                            </div>
                            <div className="text-center">
                              <div className="text-[9px] text-[#a32d2d]">متبقي</div>
                              <div className="text-[18px] font-extrabold text-[#a32d2d]">
                                {activeContract.remainingAmount.toLocaleString("ar-SA")}
                              </div>
                            </div>
                          </div>
                          <div className="bg-[#f1efe8] rounded h-1.5 overflow-hidden">
                            <div
                              className="h-full rounded bg-[#3d7ab5] transition-all duration-500"
                              style={{ width: `${activeContract.totalAmount > 0 ? Math.round((activeContract.paidAmount / activeContract.totalAmount) * 100) : 0}%` }}
                            />
                          </div>
                          {activeContract.installmentAmount && (
                            <div>
                              <span className="text-[10px] text-[#5f5e5a] font-semibold">القسط الشهري</span>
                              <p className="text-[12px] text-[#2c2c2a] font-medium">{fmtRial(activeContract.installmentAmount)}</p>
                            </div>
                          )}
                          <div>
                            <span className="text-[10px] text-[#5f5e5a] font-semibold">عدد الأقساط</span>
                            <p className="text-[12px] text-[#2c2c2a] font-medium">{activeContract.installmentsCount}</p>
                          </div>
                        </div>
                      ) : (
                        <p className="text-[12px] text-[#5f5e5a] text-center py-4">لا يوجد عقد مرتبط</p>
                      )}
                    </div>
                  </div>
                </>
              ) : (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-8 text-center">
                  <Wrench className="w-10 h-10 text-[#d3d1c7] mx-auto mb-3" />
                  <p className="text-[13px] font-bold text-[#1a3a5c]">لا يوجد ملف تقويم نشط</p>
                  <p className="text-[11px] text-[#5f5e5a] mt-1">لم يتم تسجيل حالة تقويم لهذا المريض بعد</p>
                  <Link
                    href={`/ortho/new?patientId=${patientId}`}
                    className="inline-flex items-center gap-1.5 px-4 py-2 mt-4 text-[11px] font-semibold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition"
                  >
                    <Plus className="w-3.5 h-3.5" />
                    إنشاء حالة تقويم جديدة
                  </Link>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ─── Panel: History (Medical Alerts) ─── */}
        {activePanel === "history" && (
          <div>
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <HeartPulse className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">التاريخ الطبي والسني</h2>
                <p className="text-[11px] text-[#5f5e5a]">المعلومات الصحية الأساسية</p>
              </div>
              <Link
                href={`/patients/${patientId}`}
                className="flex items-center gap-1 px-2.5 py-1 text-[11px] font-semibold rounded-lg border border-[#d3d1c7] bg-white text-[#1a3a5c] hover:bg-[#f8f9fa] transition"
              >
                <ExternalLink className="w-3 h-3" />
                فتح الملف الكامل
              </Link>
            </div>
            <div className="p-4 space-y-4">
              {/* Critical Alerts */}
              {medicalAlerts.filter((a) => a.severity === "danger").length > 0 && (
                <div className="flex gap-2 p-3 rounded-lg bg-[#fcebeb] border border-[#f09595]/50 text-[#a32d2d]">
                  <ShieldAlert className="w-4 h-4 flex-shrink-0 mt-0.5" />
                  <div>
                    <div className="font-bold text-[12px] mb-1">تنبيه طبي عاجل</div>
                    {medicalAlerts.filter((a) => a.severity === "danger").map((a, i) => (
                      <p key={i} className="text-[12px]">
                        <strong>{a.label}:</strong> {a.value}
                      </p>
                    ))}
                  </div>
                </div>
              )}

              {/* All Alerts Categorized */}
              {medicalAlerts.length > 0 ? (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {/* Medical History */}
                  <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                    <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                      <HeartPulse className="w-4 h-4 text-[#3d7ab5]" />
                      التاريخ الطبي
                    </span>
                    <div className="space-y-2">
                      {medicalAlerts
                        .filter((a) => ["allergy", "chronic", "medication"].includes(a.type))
                        .map((alert, i) => (
                          <div key={i} className="flex items-start gap-2">
                            <AlertTriangle className={cn("w-3.5 h-3.5 flex-shrink-0 mt-0.5", SEVERITY_STYLES[alert.severity]?.icon)} />
                            <div>
                              <span className="text-[10px] text-[#5f5e5a] font-semibold">{alert.label}</span>
                              <p className={cn("text-[12px] font-medium", SEVERITY_STYLES[alert.severity]?.text)}>{alert.value}</p>
                            </div>
                          </div>
                        ))}
                      {medicalAlerts.filter((a) => ["allergy", "chronic", "medication"].includes(a.type)).length === 0 && (
                        <p className="text-[12px] text-[#5f5e5a]">لا توجد تنبيهات طبية</p>
                      )}
                    </div>
                  </div>

                  {/* Dental Alerts */}
                  <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                    <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-3">
                      <Info className="w-4 h-4 text-[#3d7ab5]" />
                      التنبيهات السنية
                    </span>
                    <div className="space-y-2">
                      {medicalAlerts
                        .filter((a) => !["allergy", "chronic", "medication"].includes(a.type))
                        .map((alert, i) => (
                          <div key={i} className="flex items-start gap-2">
                            <AlertTriangle className={cn("w-3.5 h-3.5 flex-shrink-0 mt-0.5", SEVERITY_STYLES[alert.severity]?.icon)} />
                            <div>
                              <span className="text-[10px] text-[#5f5e5a] font-semibold">{alert.label}</span>
                              <p className={cn("text-[12px] font-medium", SEVERITY_STYLES[alert.severity]?.text)}>{alert.value}</p>
                            </div>
                          </div>
                        ))}
                      {medicalAlerts.filter((a) => !["allergy", "chronic", "medication"].includes(a.type)).length === 0 && (
                        <p className="text-[12px] text-[#5f5e5a]">لا توجد تنبيهات سنية</p>
                      )}
                    </div>
                  </div>
                </div>
              ) : (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-8 text-center">
                  <HeartPulse className="w-10 h-10 text-[#d3d1c7] mx-auto mb-3" />
                  <p className="text-[13px] font-bold text-[#1a3a5c]">لا توجد تنبيهات طبية</p>
                  <p className="text-[11px] text-[#5f5e5a] mt-1">لم يتم تسجيل أي حساسية أو أمراض مزمنة لهذا المريض</p>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ─── Panel: Timeline ─── */}
        {activePanel === "timeline" && (
          <div>
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <Clock className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">السجل الزمني الكامل</h2>
                <p className="text-[11px] text-[#5f5e5a]">كل أحداث المريض مرتبة زمنياً</p>
              </div>
            </div>
            <div className="p-4">
              {timeline.length > 0 ? (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-4">
                  <div className="space-y-0">
                    {timeline.map((evt, i) => (
                      <TimelineItem key={i} event={evt} />
                    ))}
                  </div>
                </div>
              ) : (
                <div className="bg-white rounded-xl border border-[#d3d1c7] p-8 text-center">
                  <Clock className="w-10 h-10 text-[#d3d1c7] mx-auto mb-3" />
                  <p className="text-[13px] font-bold text-[#1a3a5c]">لا توجد أحداث</p>
                  <p className="text-[11px] text-[#5f5e5a] mt-1">لم يتم تسجيل أي أحداث بعد</p>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ─── Panel: Messages ─── — REMOVED (Blocker-5: messaging/SMS not authorized in this PR) */}

        {/* ─── Panel: Docs ─── */}
        {activePanel === "docs" && (
          <div>
            <div className="bg-white px-5 py-3 border-b border-[#d3d1c7] sticky top-0 z-10 flex items-center gap-3">
              <FileText className="w-5 h-5 text-[#3d7ab5]" />
              <div className="flex-1">
                <h2 className="text-[15px] font-bold text-[#1a3a5c]">المستندات</h2>
                <p className="text-[11px] text-[#5f5e5a]">المستندات والملفات المرتبطة بالمريض</p>
              </div>
            </div>
            <div className="p-4 space-y-4">
              <div className="bg-white rounded-xl border border-[#d3d1c7] p-4 space-y-3">
                <span className="text-[13px] font-bold text-[#1a3a5c] flex items-center gap-1.5 mb-2">
                  <FileText className="w-4 h-4 text-[#3d7ab5]" />
                  الوصول السريع للمستندات
                </span>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  <Link
                    href={`/patients/${patientId}`}
                    className="flex items-center gap-2 p-3 rounded-lg bg-[#e1f5ee] border border-[#9fe1cb] hover:bg-[#9fe1cb]/30 transition"
                  >
                    <FileText className="w-4 h-4 text-[#2d5e8e]" />
                    <div className="flex-1">
                      <span className="text-[12px] font-semibold text-[#2d5e8e]">عرض المستندات</span>
                      <p className="text-[10px] text-[#5f5e5a]">ملفات وتقارير المريض</p>
                    </div>
                    <ExternalLink className="w-3.5 h-3.5 text-[#2d5e8e]" />
                  </Link>
                  <Link
                    href={`/patients/${patientId}/print/summary`}
                    className="flex items-center gap-2 p-3 rounded-lg border border-[#d3d1c7] hover:bg-[#f8f9fa] transition"
                  >
                    <Printer className="w-4 h-4 text-[#1a3a5c]" />
                    <span className="text-[11px] font-semibold text-[#1a3a5c]">طباعة ملخص المريض</span>
                  </Link>
                  <button
                    onClick={async () => {
                      try {
                        const filename = `financial-statement-${patient?.patientNumber || patientId}.pdf`;
                        await downloadPdfFromApi(`/api/patients/${patientId}/financial-statement/pdf`, filename);
                      } catch {
                        toast.error("فشل تحميل كشف الحساب المالي");
                      }
                    }}
                    className="flex items-center gap-2 p-3 rounded-lg border border-[#d3d1c7] hover:bg-[#f8f9fa] transition text-right w-full"
                  >
                    <Receipt className="w-4 h-4 text-[#1a3a5c]" />
                    <span className="text-[11px] font-semibold text-[#1a3a5c]">طباعة ملخص مالي</span>
                  </button>
                  <Link
                    href={`/patients/${patientId}/print/media`}
                    className="flex items-center gap-2 p-3 rounded-lg border border-[#d3d1c7] hover:bg-[#f8f9fa] transition"
                  >
                    <Camera className="w-4 h-4 text-[#1a3a5c]" />
                    <span className="text-[11px] font-semibold text-[#1a3a5c]">الصور والوسائط</span>
                  </Link>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

// ─── Sub-Components ─────────────────────────────────────────────────────────

/** Timeline event item */
function TimelineItem({ event }: { event: TimelineEvent }) {
  const dotColor = TIMELINE_DOT_COLORS[event.type] ?? TIMELINE_DOT_COLORS.default;
  return (
    <div className="flex gap-2.5 py-2 border-b border-[#f1efe8] last:border-b-0">
      <div className={cn("w-2.5 h-2.5 rounded-full flex-shrink-0 mt-1.5", dotColor)} />
      <div className="flex-1 min-w-0">
        <div className="text-[10px] text-[#5f5e5a]">{fmtDate(event.date)}</div>
        <div className="text-[12px] font-semibold text-[#1a3a5c]">{event.title}</div>
        {event.sub && (
          <div className="text-[11px] text-[#5f5e5a] mt-0.5">{event.sub}</div>
        )}
        {event.status && (
          <span className={cn(
            "inline-flex items-center px-2 py-0.5 rounded-full text-[9px] font-bold mt-1",
            event.status === "Completed" || event.status === "Paid" ? "bg-[#eaf3de] text-[#3b6d11]" :
            event.status === "Cancelled" ? "bg-[#fcebeb] text-[#a32d2d]" :
            "bg-[#faeeda] text-[#633806]"
          )}>
            {event.status}
          </span>
        )}
      </div>
    </div>
  );
}

/** Recent visit timeline item */
function VisitTimelineItem({ visit }: { visit: DailyJourneyRecentVisit }) {
  return (
    <div className="flex gap-2.5 py-2 border-b border-[#f1efe8] last:border-b-0">
      <div className="w-2.5 h-2.5 rounded-full bg-[#3d7ab5] flex-shrink-0 mt-1.5" />
      <div className="flex-1 min-w-0">
        <div className="text-[10px] text-[#5f5e5a]">{fmtDate(visit.visitDate)}</div>
        <div className="text-[12px] font-semibold text-[#1a3a5c]">
          {visit.visitType ?? "زيارة"}
          {visit.chiefComplaint ? ` — ${visit.chiefComplaint}` : ""}
        </div>
        {(visit.treatmentDone || visit.diagnosis) && (
          <div className="text-[11px] text-[#5f5e5a] mt-0.5">
            {visit.treatmentDone ?? visit.diagnosis}
          </div>
        )}
        {visit.cost != null && visit.cost > 0 && (
          <span className="text-[10px] text-[#3b6d11] font-bold">{fmtRial(visit.cost)}</span>
        )}
      </div>
    </div>
  );
}
