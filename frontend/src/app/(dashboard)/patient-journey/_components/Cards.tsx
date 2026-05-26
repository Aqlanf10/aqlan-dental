"use client";

import Link from "next/link";
import {
  Users, UserCheck, Clock, DoorOpen, CreditCard, CheckCircle2,
  Save, RefreshCw, Stethoscope, ArrowRight, Phone, CalendarDays,
  Filter, FileText, AlertTriangle, ExternalLink, Megaphone,
  PlayCircle, ShieldAlert, Wallet, CircleDot,
  Wrench, HeartPulse,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { QUEUE_STATUS_ARABIC } from "@/types/journey";
import type {
  DailyJourneySummary,
  MedicalAlert,
  DailyJourneyRecentVisit,
  TimelineEvent,
} from "@/types/journey";
import {
  JOURNEY_STEPS, STATUS_LABELS, STATUS_COLORS, ACTION_LABELS, ACTION_COLORS,
  PAYMENT_METHODS, SEVERITY_STYLES, TIMELINE_DOT_COLORS,
  inputCls, fmtRial, fmtDate, fmtTime, getInitials, getStepIndex, getStepStatus,
} from "../_lib/constants";
import type { JourneyItem, ServiceOption, RoomOption } from "../_lib/constants";

// ─── 1. Patient Header Card ──────────────────────────────────────────────────

export function PatientHeaderCard({ summary, isDoctor }: { summary: DailyJourneySummary; isDoctor: boolean }) {
  const { patient, journeyStep } = summary;
  const currentStepIdx = getStepIndex(journeyStep);

  return (
    <div className="bg-[#1a3a5c] rounded-xl p-4 text-white">
      <div className="flex items-start gap-3">
        <div className="w-12 h-12 rounded-full bg-[#3d7ab5] flex items-center justify-center text-lg font-bold flex-shrink-0">
          {getInitials(patient.fullName)}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <h2 className="text-[15px] font-bold truncate">{patient.fullName}</h2>
            <span className="text-[10px] font-mono text-white/60">#{patient.patientNumber}</span>
          </div>
          <div className="flex flex-wrap gap-3 mt-1.5">
            {patient.age != null && (
              <span className="text-[11px] text-white/80">{patient.age} سنة</span>
            )}
            {patient.gender && (
              <span className="text-[11px] text-white/80">
                {patient.gender === "Male" ? "ذكر" : patient.gender === "Female" ? "أنثى" : patient.gender}
              </span>
            )}
            {patient.phone && !isDoctor && (
              <span className="text-[11px] text-[#9fe1cb] flex items-center gap-1" dir="ltr">
                <Phone className="w-3 h-3" />
                {patient.phone}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Journey Step Progress Bar */}
      <div className="mt-4">
        <div className="flex items-center gap-0 overflow-x-auto pb-1">
          {JOURNEY_STEPS.map((step, idx) => {
            const status = getStepStatus(step.key, journeyStep);
            return (
              <div key={step.key} className="flex items-center">
                <div className="flex flex-col items-center gap-0.5 min-w-[52px]">
                  <div
                    className={cn(
                      "w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold border-2 transition-all",
                      status === "done" && "bg-[#3d7ab5] text-white border-[#3d7ab5]",
                      status === "current" && "bg-white text-[#f5922e] border-[#f5922e] shadow-[0_0_8px_rgba(245,146,46,0.4)]",
                      status === "pending" && "bg-white/10 text-white/40 border-white/20"
                    )}
                  >
                    {status === "done" ? <CheckCircle2 className="w-3.5 h-3.5" /> : idx + 1}
                  </div>
                  <span
                    className={cn(
                      "text-[8px] text-center leading-tight",
                      status === "current" ? "text-[#f5922e] font-bold" : "text-white/50"
                    )}
                  >
                    {step.label}
                  </span>
                </div>
                {idx < JOURNEY_STEPS.length - 1 && (
                  <div
                    className={cn(
                      "flex-1 h-0.5 min-w-[8px]",
                      getStepIndex(step.key) < currentStepIdx ? "bg-[#3d7ab5]" : "bg-white/20"
                    )}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── 2. Today's Appointment ──────────────────────────────────────────────────

export function TodaysAppointmentCard({ summary }: { summary: DailyJourneySummary }) {
  const apt = summary.todayAppointment!;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <CalendarDays className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">موعد اليوم</span>
        <span className={cn(
          "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
          STATUS_COLORS[apt.status] ?? "bg-gray-50 text-gray-700"
        )}>
          {STATUS_LABELS[apt.status] ?? apt.status}
        </span>
      </div>
      <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-[12px]">
        <div>
          <span className="text-[10px] text-gray-500 font-semibold">الوقت</span>
          <p className="text-gray-800 font-medium" dir="ltr">
            {fmtTime(apt.startTime)}{apt.endTime ? ` — ${fmtTime(apt.endTime)}` : ""}
          </p>
        </div>
        <div>
          <span className="text-[10px] text-gray-500 font-semibold">الطبيب</span>
          <p className="text-gray-800 font-medium">{apt.doctorName}</p>
        </div>
        {apt.roomName && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">الغرفة</span>
            <p className="text-gray-800 font-medium">{apt.roomName}</p>
          </div>
        )}
        {apt.appointmentType && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">نوع الزيارة</span>
            <p className="text-gray-800 font-medium">{apt.appointmentType}</p>
          </div>
        )}
        {apt.specialty && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">التخصص</span>
            <p className="text-gray-800 font-medium">{apt.specialty}</p>
          </div>
        )}
        {apt.arrivedAt && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">وقت الوصول</span>
            <p className="text-gray-800 font-medium">{fmtTime(apt.arrivedAt)}</p>
          </div>
        )}
        {apt.calledAt && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">وقت النداء</span>
            <p className="text-gray-800 font-medium">{fmtTime(apt.calledAt)}</p>
          </div>
        )}
        {apt.inRoomAt && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">وقت دخول الغرفة</span>
            <p className="text-gray-800 font-medium">{fmtTime(apt.inRoomAt)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 3. Queue Status ─────────────────────────────────────────────────────────

export function QueueStatusCard({ summary }: { summary: DailyJourneySummary }) {
  const q = summary.queueStatus!;

  return (
    <div className="bg-blue-50 rounded-xl border border-blue-200 p-3">
      <div className="flex items-center gap-2 mb-2">
        <Clock className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">حالة الطابور</span>
        <span className={cn(
          "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
          q.status === "Waiting" ? "bg-orange-100 text-orange-700" :
          q.status === "Called" ? "bg-purple-100 text-purple-700" :
          q.status === "InRoom" ? "bg-cyan-100 text-cyan-700" :
          q.status === "InProgress" ? "bg-emerald-100 text-emerald-700" :
          "bg-gray-100 text-gray-700"
        )}>
          {QUEUE_STATUS_ARABIC[q.status] ?? q.status}
        </span>
      </div>
      <div className="flex gap-4 text-[12px]">
        {q.roomName && (
          <div>
            <span className="text-[10px] text-gray-500">الغرفة</span>
            <p className="text-gray-800 font-medium">{q.roomName}</p>
          </div>
        )}
        {q.calledAt && (
          <div>
            <span className="text-[10px] text-gray-500">وقت النداء</span>
            <p className="text-gray-800 font-medium">{fmtTime(q.calledAt)}</p>
          </div>
        )}
        {q.inRoomAt && (
          <div>
            <span className="text-[10px] text-gray-500">دخول الغرفة</span>
            <p className="text-gray-800 font-medium">{fmtTime(q.inRoomAt)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 4. Medical Safety Alerts ────────────────────────────────────────────────

export function MedicalAlertsCard({ alerts }: { alerts: MedicalAlert[] }) {
  return (
    <div className="rounded-xl border border-red-200 bg-[#fcebeb]/50 p-3">
      <div className="flex items-center gap-2 mb-2">
        <ShieldAlert className="w-4 h-4 text-red-600" />
        <span className="text-sm font-bold text-red-800">تنبيهات طبية</span>
      </div>
      <div className="flex flex-wrap gap-1.5">
        {alerts.map((alert, i) => {
          const style = SEVERITY_STYLES[alert.severity] ?? SEVERITY_STYLES.info;
          return (
            <span
              key={i}
              className={cn(
                "inline-flex items-center gap-1 px-2 py-1 rounded-full text-[10px] font-bold border",
                style.bg, style.border, style.text
              )}
            >
              <AlertTriangle className="w-3 h-3" />
              {alert.label}{alert.value ? `: ${alert.value}` : ""}
            </span>
          );
        })}
      </div>
    </div>
  );
}

// ─── 5. Journey Actions Panel ────────────────────────────────────────────────

interface JourneyActionsPanelProps {
  summary: DailyJourneySummary;
  selectedItem: JourneyItem;

  services: ServiceOption[];
  rooms: RoomOption[];
  intakeService: string;
  setIntakeService: (v: string) => void;
  intakeComplaint: string;
  setIntakeComplaint: (v: string) => void;
  intakeRoom: string;
  setIntakeRoom: (v: string) => void;
  intakeConsultFee: boolean;
  setIntakeConsultFee: (v: boolean) => void;
  intakeConsultAmount: number;
  setIntakeConsultAmount: (v: number) => void;
  intakeNotes: string;
  setIntakeNotes: (v: string) => void;
  handoffForm: { treatmentDone: string; diagnosis: string; nextVisitPlan: string; instructions: string; amountDue: number; notes: string };
  setHandoffForm: (v: { treatmentDone: string; diagnosis: string; nextVisitPlan: string; instructions: string; amountDue: number; notes: string }) => void;
  checkoutAmount: number;
  setCheckoutAmount: (v: number) => void;
  checkoutPayment: string;
  setCheckoutPayment: (v: string) => void;
  checkoutNextDate: string;
  setCheckoutNextDate: (v: string) => void;
  checkoutNextService: string;
  setCheckoutNextService: (v: string) => void;
  checkoutNotes: string;
  setCheckoutNotes: (v: string) => void;
  checkoutSuccess: boolean;
  draftInvoiceResult: { invoiceId: string; invoiceNumber: string } | null;
  intakeMutation: { isPending: boolean };
  sendToQueueMutation: { isPending: boolean };
  startVisitMutation: { isPending: boolean };
  handoffMutation: { isPending: boolean };
  checkoutMutation: { isPending: boolean };
  draftInvoiceMutation: { isPending: boolean };
  onSimpleAction: (item: JourneyItem) => void;
  onIntakeSubmit: (e: React.FormEvent) => void;
  onHandoffSubmit: (e: React.FormEvent) => void;
  onCheckoutSubmit: (e: React.FormEvent) => void;
  isDoctor: boolean;
  onCreateDraftInvoice: () => void;
  onIssueInvoice: () => void;
  issueInvoiceMutation: { isPending: boolean };
  onDownloadPdf: (url: string, filename: string) => void;
}

export function JourneyActionsPanel({
  summary, selectedItem, isDoctor, services, rooms,
  intakeService, setIntakeService, intakeComplaint, setIntakeComplaint,
  intakeRoom, setIntakeRoom, intakeConsultFee, setIntakeConsultFee,
  intakeConsultAmount, setIntakeConsultAmount, intakeNotes, setIntakeNotes,
  handoffForm, setHandoffForm,
  checkoutAmount, setCheckoutAmount, checkoutPayment, setCheckoutPayment,
  checkoutNextDate, setCheckoutNextDate, checkoutNextService, setCheckoutNextService,
  checkoutNotes, setCheckoutNotes, checkoutSuccess, draftInvoiceResult,
  intakeMutation, sendToQueueMutation, startVisitMutation,
  handoffMutation, checkoutMutation, draftInvoiceMutation,
  onSimpleAction, onIntakeSubmit, onHandoffSubmit, onCheckoutSubmit, onCreateDraftInvoice,
  onIssueInvoice, issueInvoiceMutation, onDownloadPdf,
}: JourneyActionsPanelProps) {
  const { nextAction } = summary;
  const actionLabel = ACTION_LABELS[nextAction] ?? nextAction;
  const actionColor = ACTION_COLORS[nextAction] ?? "bg-gray-400";

  return (
    <div className="rounded-xl border-2 border-[#3d7ab5]/30 bg-[#f0f6fc] p-4 space-y-3">
      {/* Next Action Banner */}
      <div className="flex items-center gap-2">
        <CircleDot className="w-5 h-5 text-[#f5922e]" />
        <span className="text-sm font-bold text-[#1a3a5c]">الإجراء التالي</span>
        <span className={cn(
          "text-[11px] px-3 py-1 rounded-full font-bold text-white mr-auto",
          actionColor
        )}>
          {actionLabel}
        </span>
      </div>

      {/* ── Intake Form (Inline) ── */}
      {nextAction === "Intake" && (
        <form onSubmit={onIntakeSubmit} className="space-y-3 bg-white rounded-xl border border-[#9fe1cb] p-3">
          <span className="text-xs font-bold text-[#2d5e8e] flex items-center gap-1.5">
            <UserCheck className="w-4 h-4" />
            تسجيل الوصول وإدخال الطابور
          </span>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الخدمة المطلوبة</label>
              <select
                value={intakeService}
                onChange={(e) => {
                  setIntakeService(e.target.value);
                  const svc = services.find((s) => s.id === e.target.value);
                  if (svc) {
                    setIntakeConsultFee(svc.requiresConsultationFee);
                    setIntakeConsultAmount(svc.defaultPrice);
                  }
                }}
                className={inputCls}
              >
                <option value="">— اختر خدمة —</option>
                {services.map((s) => (
                  <option key={s.id} value={s.id}>{s.arabicName}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الشكوى الرئيسية</label>
              <input
                value={intakeComplaint}
                onChange={(e) => setIntakeComplaint(e.target.value)}
                className={inputCls}
                placeholder="مثل: ألم في الضرس"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الغرفة</label>
              <select
                value={intakeRoom}
                onChange={(e) => setIntakeRoom(e.target.value)}
                className={inputCls}
              >
                <option value="">— اختر غرفة —</option>
                {rooms.map((r) => (
                  <option key={r.id} value={r.id}>{r.arabicName}</option>
                ))}
              </select>
            </div>
            <div className="flex items-end gap-2">
              <label className="flex items-center gap-1.5 text-xs text-gray-700 flex-1">
                <input
                  type="checkbox"
                  checked={intakeConsultFee}
                  onChange={(e) => setIntakeConsultFee(e.target.checked)}
                  className="rounded border-gray-300"
                />
                رسوم معاينة
              </label>
              {intakeConsultFee && (
                <input
                  type="number"
                  value={intakeConsultAmount}
                  onChange={(e) => setIntakeConsultAmount(parseInt(e.target.value) || 0)}
                  className={cn(inputCls, "w-28")}
                  dir="ltr"
                  min={0}
                />
              )}
            </div>
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
            <textarea
              value={intakeNotes}
              onChange={(e) => setIntakeNotes(e.target.value)}
              className={cn(inputCls, "h-16 resize-none")}
              placeholder="ملاحظات إضافية"
            />
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={intakeMutation.isPending || sendToQueueMutation.isPending}
              className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Save className="w-3.5 h-3.5" />
              {(intakeMutation.isPending || sendToQueueMutation.isPending) ? "جارٍ الحفظ..." : "تسجيل الوصول وإدخال الطابور"}
            </button>
          </div>
        </form>
      )}

      {/* ── Simple Action Buttons ── */}
      {(nextAction === "SendToQueue" || nextAction === "CallPatient" || nextAction === "EnterRoom" || nextAction === "StartVisit") && (
        <div className="flex gap-2">
          <button
            onClick={() => onSimpleAction(selectedItem)}
            disabled={nextAction === "SendToQueue" ? sendToQueueMutation.isPending :
                      nextAction === "StartVisit" ? startVisitMutation.isPending : false}
            className={cn(
              "flex items-center gap-1.5 px-5 py-2.5 text-sm font-bold rounded-lg text-white transition disabled:opacity-50",
              actionColor
            )}
          >
            {nextAction === "SendToQueue" && <ArrowRight className="w-4 h-4" />}
            {nextAction === "CallPatient" && <Megaphone className="w-4 h-4" />}
            {nextAction === "EnterRoom" && <DoorOpen className="w-4 h-4" />}
            {nextAction === "StartVisit" && <PlayCircle className="w-4 h-4" />}
            {(nextAction === "SendToQueue" && sendToQueueMutation.isPending) ||
             (nextAction === "StartVisit" && startVisitMutation.isPending)
              ? "جارٍ التنفيذ..."
              : actionLabel}
          </button>
        </div>
      )}

      {/* ── Handoff Form (Doctor Inline) ── */}
      {nextAction === "Handoff" && isDoctor && (
        <form onSubmit={onHandoffSubmit} className="space-y-3 bg-white rounded-xl border border-[#9fe1cb] p-3">
          <span className="text-xs font-bold text-[#2d5e8e] flex items-center gap-1.5">
            <ArrowRight className="w-4 h-4" />
            تسليم المريض للاستقبال
          </span>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ما تم عمله</label>
              <textarea
                value={handoffForm.treatmentDone}
                onChange={(e) => setHandoffForm({ ...handoffForm, treatmentDone: e.target.value })}
                className={cn(inputCls, "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
              <input
                value={handoffForm.diagnosis}
                onChange={(e) => setHandoffForm({ ...handoffForm, diagnosis: e.target.value })}
                className={inputCls}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">تعليمات للمريض</label>
              <textarea
                value={handoffForm.instructions}
                onChange={(e) => setHandoffForm({ ...handoffForm, instructions: e.target.value })}
                className={cn(inputCls, "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">المبلغ المطلوب</label>
              <input
                type="number"
                value={handoffForm.amountDue}
                onChange={(e) => setHandoffForm({ ...handoffForm, amountDue: parseInt(e.target.value) || 0 })}
                className={inputCls}
                dir="ltr"
                min={0}
              />
            </div>
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={handoffMutation.isPending}
              className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Save className="w-3.5 h-3.5" />
              {handoffMutation.isPending ? "جارٍ التسليم..." : "تسليم للاستقبال"}
            </button>
          </div>
        </form>
      )}

      {/* ── Checkout Form (Inline) — Reception/Admin only, not Doctor ── */}
      {nextAction === "Checkout" && !isDoctor && (
        <div className="space-y-3 bg-white rounded-xl border border-green-200 p-3">
          {checkoutSuccess ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2 bg-green-50 border border-green-200 rounded-lg px-3 py-2.5">
                <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0" />
                <span className="text-sm font-medium text-green-800">تم إنهاء حساب الزيارة بنجاح</span>
              </div>
              {draftInvoiceResult && (
                <div className="border border-green-200 bg-green-50 rounded-lg p-2.5 space-y-2">
                  <p className="text-xs font-medium text-green-800">
                    الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span>
                  </p>
                  <div className="flex flex-wrap gap-2">
                    <Link
                      href={`/finance/invoices/${draftInvoiceResult.invoiceId}`}
                      className="text-xs text-[#3d7ab5] hover:underline flex items-center gap-1"
                    >
                      <FileText className="w-3 h-3" />
                      عرض الفاتورة
                    </Link>
                    {/* Phase 2b: Issue Invoice */}
                    <button
                      onClick={onIssueInvoice}
                      disabled={issueInvoiceMutation.isPending}
                      className="flex items-center gap-1 px-2 py-1 text-[10px] rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-50 transition"
                    >
                      <FileText className="w-3 h-3" />
                      {issueInvoiceMutation.isPending ? "جارٍ..." : "إصدار فاتورة"}
                    </button>
                    {/* Phase 2c: Download Invoice PDF */}
                    <button
                      onClick={() => onDownloadPdf(`/api/invoices/${draftInvoiceResult.invoiceId}/pdf`, `invoice-${draftInvoiceResult.invoiceNumber}.pdf`)}
                      className="flex items-center gap-1 px-2 py-1 text-[10px] rounded-lg bg-gray-100 text-gray-700 hover:bg-gray-200 transition"
                    >
                      <Filter className="w-3 h-3" />
                      تحميل PDF
                    </button>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <form onSubmit={onCheckoutSubmit} className="space-y-3">
              <span className="text-xs font-bold text-green-700 flex items-center gap-1.5">
                <CreditCard className="w-4 h-4" />
                إنهاء الحساب
              </span>

              {/* Draft Invoice */}
              {selectedItem.checkoutStatus === "ReadyForCheckout" && !draftInvoiceResult && (
                <div className="border border-[#3d7ab5]/20 bg-[#f7fafd] rounded-lg p-2.5 flex items-center justify-between">
                  <div className="flex items-center gap-1.5 text-xs font-medium text-[#1a3a5c]">
                    <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" />
                    إنشاء فاتورة مسودة
                  </div>
                  <button
                    type="button"
                    disabled={draftInvoiceMutation.isPending}
                    onClick={onCreateDraftInvoice}
                    className="flex items-center gap-1 px-2.5 py-1 text-[10px] rounded-lg bg-[#3d7ab5] text-white hover:opacity-90 disabled:opacity-50 transition"
                  >
                    <FileText className="w-3 h-3" />
                    {draftInvoiceMutation.isPending ? "جارٍ..." : "إنشاء فاتورة"}
                  </button>
                </div>
              )}
              {draftInvoiceResult && (
                <div className="border border-green-200 bg-green-50 rounded-lg p-2.5 space-y-1">
                  <p className="text-xs font-medium text-green-800">
                    تم إنشاء الفاتورة: <span className="font-mono">{draftInvoiceResult.invoiceNumber}</span>
                  </p>
                  <Link
                    href={`/finance/invoices/${draftInvoiceResult.invoiceId}`}
                    className="text-[10px] text-[#3d7ab5] hover:underline flex items-center gap-1"
                  >
                    <FileText className="w-3 h-3" />
                    عرض الفاتورة
                  </Link>
                </div>
              )}

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">المبلغ المطلوب</label>
                  {summary?.todayVisit?.amountDueReference != null && (
                    <span className="text-[9px] text-[#3d7ab5] font-medium">المبلغ المرجعي: {fmtRial(summary.todayVisit.amountDueReference)}</span>
                  )}
                  <input
                    type="number"
                    value={checkoutAmount}
                    onChange={(e) => setCheckoutAmount(parseInt(e.target.value) || 0)}
                    className={inputCls}
                    dir="ltr"
                    min={0}
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">طريقة الدفع</label>
                  <select
                    value={checkoutPayment}
                    onChange={(e) => setCheckoutPayment(e.target.value)}
                    className={inputCls}
                  >
                    {PAYMENT_METHODS.map((m) => (
                      <option key={m.value} value={m.value}>{m.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">حجز موعد قادم</label>
                  <input
                    type="date"
                    value={checkoutNextDate}
                    onChange={(e) => setCheckoutNextDate(e.target.value)}
                    className={inputCls}
                    dir="ltr"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-gray-600 mb-0.5">الخدمة القادمة</label>
                  <select
                    value={checkoutNextService}
                    onChange={(e) => setCheckoutNextService(e.target.value)}
                    className={inputCls}
                  >
                    <option value="">— اختر خدمة —</option>
                    {services.map((s) => (
                      <option key={s.id} value={s.id}>{s.arabicName}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-[10px] font-bold text-gray-600 mb-0.5">ملاحظات</label>
                <textarea
                  value={checkoutNotes}
                  onChange={(e) => setCheckoutNotes(e.target.value)}
                  className={cn(inputCls, "h-14 resize-none")}
                  placeholder="ملاحظات إضافية"
                />
              </div>
              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={checkoutMutation.isPending}
                  className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-green-600 text-white hover:bg-green-700 transition disabled:opacity-50"
                >
                  <CreditCard className="w-3.5 h-3.5" />
                  {checkoutMutation.isPending ? "جارٍ الإنهاء..." : "إنهاء الحساب"}
                </button>
              </div>
            </form>
          )}
        </div>
      )}

      {/* ── InProgress indicator ── */}
      {nextAction === "InProgress" && (
        <div className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2.5">
          <Stethoscope className="w-5 h-5 text-emerald-600" />
          <span className="text-sm font-semibold text-emerald-800">المريض عند الطبيب حاليًا</span>
        </div>
      )}
    </div>
  );
}

// ─── 6. Today's Visit ────────────────────────────────────────────────────────

export function TodaysVisitCard({
  summary, isReception, isAccountant,
}: {
  summary: DailyJourneySummary;
  isReception: boolean;
  isAccountant: boolean;
}) {
  const visit = summary.todayVisit!;
  const hideClinical = isReception || isAccountant;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Stethoscope className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">زيارة اليوم</span>
        {visit.checkoutStatus && (
          <span className={cn(
            "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
            visit.checkoutStatus === "Completed" ? "bg-green-50 text-green-700" :
            visit.checkoutStatus === "ReadyForCheckout" ? "bg-amber-50 text-amber-700" :
            "bg-gray-50 text-gray-700"
          )}>
            {visit.checkoutStatus === "Completed" ? "مكتمل" :
             visit.checkoutStatus === "ReadyForCheckout" ? "جاهز للحساب" :
             visit.checkoutStatus}
          </span>
        )}
      </div>
      <div className="space-y-2 text-[12px]">
        {visit.chiefComplaint && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">الشكوى الرئيسية</span>
            <p className="text-gray-800 font-medium">{visit.chiefComplaint}</p>
          </div>
        )}
        {!hideClinical && visit.treatmentDone && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">ما تم عمله</span>
            <p className="text-gray-800 font-medium">{visit.treatmentDone}</p>
          </div>
        )}
        {!hideClinical && visit.diagnosis && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">التشخيص</span>
            <p className="text-gray-800 font-medium">{visit.diagnosis}</p>
          </div>
        )}
        {!hideClinical && visit.instructions && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">تعليمات للمريض</span>
            <p className="text-gray-800 font-medium">{visit.instructions}</p>
          </div>
        )}
        {visit.cost != null && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">التكلفة</span>
            <p className="text-gray-800 font-bold">{fmtRial(visit.cost)}</p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── 7. Finance Summary ──────────────────────────────────────────────────────

export function FinanceSummaryCard({
  summary, isAccountant, canViewFinance, canViewInvoices,
}: {
  summary: DailyJourneySummary;
  isAccountant: boolean;
  canViewFinance: boolean;
  canViewInvoices: boolean;
}) {
  const fin = summary.financeSummary!;
  const showFullFinance = canViewFinance;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Wallet className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">الملف المالي</span>
        <span className={cn(
          "text-[10px] px-2 py-0.5 rounded-full font-medium mr-auto",
          fin.financialStatus === "paid_full" ? "bg-green-50 text-green-700" :
          fin.financialStatus === "overdue" ? "bg-red-50 text-red-700" :
          fin.financialStatus === "has_balance" ? "bg-amber-50 text-amber-700" :
          "bg-gray-50 text-gray-600"
        )}>
          {fin.financialStatus === "paid_full" ? "مسدد" :
           fin.financialStatus === "overdue" ? "متأخر" :
           fin.financialStatus === "has_balance" ? "رصيد مستحق" :
           "بدون خطة"}
        </span>
      </div>
      <div className="grid grid-cols-2 gap-3 text-[12px]">
        <div className="bg-amber-50 rounded-lg p-2.5">
          <span className="text-[10px] text-amber-700 font-semibold">المستحق</span>
          <p className="text-amber-800 font-bold text-sm">{fmtRial(fin.outstandingBalance)}</p>
        </div>
        <div className="bg-red-50 rounded-lg p-2.5">
          <span className="text-[10px] text-red-700 font-semibold">متأخر</span>
          <p className="text-red-800 font-bold text-sm">{fmtRial(fin.overdueAmount)}</p>
        </div>
        {showFullFinance && fin.totalTreatmentCost != null && (
          <div className="bg-gray-50 rounded-lg p-2.5">
            <span className="text-[10px] text-gray-600 font-semibold">إجمالي التكلفة</span>
            <p className="text-gray-800 font-bold text-sm">{fmtRial(fin.totalTreatmentCost)}</p>
          </div>
        )}
        {showFullFinance && fin.totalPaid != null && (
          <div className="bg-green-50 rounded-lg p-2.5">
            <span className="text-[10px] text-green-700 font-semibold">المدفوع</span>
            <p className="text-green-800 font-bold text-sm">{fmtRial(fin.totalPaid)}</p>
          </div>
        )}
      </div>
      {fin.latestPayment && (
        <div className="mt-2 text-[11px] text-gray-600 border-t border-gray-100 pt-2">
          <span className="font-semibold">آخر دفعة:</span>{" "}
          {fmtRial(fin.latestPayment.amount)} — {fmtDate(fin.latestPayment.paymentDate)}
          {fin.latestPayment.paymentMethod && ` (${PAYMENT_METHODS.find(m => m.value === fin.latestPayment!.paymentMethod)?.label ?? fin.latestPayment.paymentMethod})`}
        </div>
      )}
      {summary.unpaidInvoicesCount > 0 && canViewInvoices && (
        <div className="mt-2 text-[11px] text-red-600 font-medium">
          فواتير غير مدفوعة: {summary.unpaidInvoicesCount}
        </div>
      )}
      {/* Active Contract — Admin/Accountant only */}
      {summary.activeContract && (isAccountant || canViewFinance) && (
        <div className="mt-3 border-t border-gray-100 pt-3">
          <div className="flex items-center gap-1.5 mb-2">
            <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" />
            <span className="text-[11px] font-bold text-[#1a3a5c]">عقد نشط</span>
          </div>
          <div className="grid grid-cols-2 gap-2 text-[11px]">
            <div>
              <span className="text-[9px] text-gray-500">الإجمالي</span>
              <p className="text-gray-800 font-medium">{fmtRial(summary.activeContract.totalAmount)}</p>
            </div>
            <div>
              <span className="text-[9px] text-gray-500">المدفوع</span>
              <p className="text-green-700 font-medium">{fmtRial(summary.activeContract.paidAmount)}</p>
            </div>
            <div>
              <span className="text-[9px] text-gray-500">المتبقي</span>
              <p className="text-amber-700 font-medium">{fmtRial(summary.activeContract.remainingAmount)}</p>
            </div>
            <div>
              <span className="text-[9px] text-gray-500">الأقساط</span>
              <p className="text-gray-800 font-medium">{summary.activeContract.installmentsCount}</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── 8. Active Ortho Case ────────────────────────────────────────────────────

export function ActiveOrthoCard({ summary }: { summary: DailyJourneySummary }) {
  const ortho = summary.activeOrthoCase!;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Wrench className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">ملف التقويم النشط</span>
        <Link
          href={`/ortho/${ortho.id}`}
          className="mr-auto text-[10px] text-[#3d7ab5] hover:underline flex items-center gap-0.5"
        >
          التفاصيل <ExternalLink className="w-3 h-3" />
        </Link>
      </div>
      <div className="grid grid-cols-2 gap-2 text-[12px]">
        {ortho.caseNumber && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">رقم القضية</span>
            <p className="text-gray-800 font-medium font-mono">{ortho.caseNumber}</p>
          </div>
        )}
        {ortho.applianceType && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">نوع الجهاز</span>
            <p className="text-gray-800 font-medium">{ortho.applianceType}</p>
          </div>
        )}
        {ortho.currentStage && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">المرحلة الحالية</span>
            <p className="text-gray-800 font-medium">{ortho.currentStage}</p>
          </div>
        )}
        {ortho.totalFee != null && (
          <div>
            <span className="text-[10px] text-gray-500 font-semibold">الرسوم الإجمالية</span>
            <p className="text-gray-800 font-bold">{fmtRial(ortho.totalFee)}</p>
          </div>
        )}
      </div>
      {ortho.stagePercentage != null && (
        <div className="mt-2">
          <div className="flex items-center justify-between text-[10px] text-gray-600 mb-1">
            <span>نسبة الإنجاز</span>
            <span className="font-bold text-[#3d7ab5]">{ortho.stagePercentage}%</span>
          </div>
          <div className="w-full bg-gray-100 rounded-full h-1.5">
            <div
              className="bg-[#3d7ab5] rounded-full h-1.5 transition-all"
              style={{ width: `${Math.min(ortho.stagePercentage, 100)}%` }}
            />
          </div>
        </div>
      )}
    </div>
  );
}

// ─── 9. Recent Visits ────────────────────────────────────────────────────────

export function RecentVisitsCard({
  visits, isReception, isAccountant,
}: {
  visits: DailyJourneyRecentVisit[];
  isReception: boolean;
  isAccountant: boolean;
}) {
  const hideDiagnosis = isReception || isAccountant;

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <HeartPulse className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">زيارات سابقة</span>
      </div>
      <div className="space-y-2">
        {visits.map((visit) => (
          <div
            key={visit.id}
            className="border border-gray-100 rounded-lg p-2.5 hover:bg-gray-50 transition"
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-[11px] text-gray-500">{fmtDate(visit.visitDate)}</span>
                  {visit.visitType && (
                    <span className="text-[9px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">{visit.visitType}</span>
                  )}
                </div>
                {visit.chiefComplaint && (
                  <p className="text-[11px] text-gray-800 mt-0.5 truncate">{visit.chiefComplaint}</p>
                )}
                {!hideDiagnosis && visit.diagnosis && (
                  <p className="text-[10px] text-gray-500 truncate">التشخيص: {visit.diagnosis}</p>
                )}
              </div>
              {visit.cost != null && (
                <span className="text-[11px] font-bold text-gray-700 flex-shrink-0">{fmtRial(visit.cost)}</span>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── 10. Timeline ────────────────────────────────────────────────────────────

export function TimelineCard({ events }: { events: TimelineEvent[] }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Clock className="w-4 h-4 text-[#3d7ab5]" />
        <span className="text-sm font-bold text-[#1a3a5c]">السجل الزمني</span>
      </div>
      <div className="relative space-y-0">
        {events.map((event, i) => {
          const dotColor = TIMELINE_DOT_COLORS[event.type] ?? TIMELINE_DOT_COLORS.default;
          return (
            <div key={i} className="flex gap-3 pb-3 last:pb-0">
              {/* Timeline line + dot */}
              <div className="flex flex-col items-center flex-shrink-0">
                <div className={cn("w-2.5 h-2.5 rounded-full mt-1", dotColor)} />
                {i < events.length - 1 && (
                  <div className="w-0.5 flex-1 bg-gray-200 mt-0.5" />
                )}
              </div>
              {/* Content */}
              <div className="flex-1 min-w-0 -mt-0.5">
                <div className="flex items-center gap-2">
                  <span className="text-[11px] font-semibold text-gray-800 truncate">{event.title}</span>
                  {event.status && (
                    <span className="text-[9px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">
                      {event.status}
                    </span>
                  )}
                </div>
                {event.sub && (
                  <p className="text-[10px] text-gray-500 truncate">{event.sub}</p>
                )}
                <span className="text-[9px] text-gray-400">{fmtDate(event.date)}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
