/**
 * Appointments Table with Microsoft Fluent Design styling.
 * Supports desktop table and mobile card views.
 * Includes: wait time display, side panel trigger, selected row highlight.
 */

"use client";

import { useState } from "react";
import {
  Calendar, UserCheck, ClipboardList, DoorOpen, CheckCircle,
  MoreHorizontal, Eye, Phone, Clock,
  Building2, PanelRight, Timer, AlertCircle, Heart,
  ChevronDown, ChevronUp, GitBranch,
  Wallet, ClipboardCheck, MessageCircle,
} from "lucide-react";
import {
  ACTION_LABELS,
  fmtTime, normalizePhone, ORANGE, NAVY,
  isAppointmentOverdue, fmtOverdueMinutes, fmtSessionDuration,
  type TodayJourneyItem,
} from "../_lib/constants";
import { StatusBadge as SharedStatusBadge } from "@/components/shared/journey";

/* ─── Status badge (wraps shared component) ─────────────────────────────── */
function StatusBadge({ status }: { status: string }) {
  return <SharedStatusBadge status={status} variant="hex" />;
}

/* ─── Next Action badge (wraps shared component with hex-style overrides) ─ */
function NextActionBadge({ action }: { action: string }) {
  // The daily-ops page uses hex-style inline colors for the NextActionBadge.
  // We wrap the shared NextActionBadge and apply the hex color scheme.
  const label = ACTION_LABELS[action] ?? "—";
  const colorMap: Record<string, string> = {
    Intake: "#16a34a",
    SendToQueue: "#f5922e",
    CallPatient: "#d97706",
    EnterRoom: "#9333ea",
    StartVisit: "#dc2626",
    InProgress: "#dc2626",
    Checkout: "#22c55e",
    Handoff: "#3d7ab5",
    None: "#94a3b8",
  };
  const color = colorMap[action] ?? "#f5922e";
  return (
    <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-bold"
      style={{ background: color + "12", color }}>
      {label}
    </span>
  );
}

/* ─── Wait Time Chip ──────────────────────────────────────────────────────── */
function WaitTimeChip({ minutes }: { minutes?: number }) {
  if (!minutes || minutes <= 0) return null;
  return (
    <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-bold"
      style={{ background: "#fff7ed", color: ORANGE }}>
      <Timer className="w-2.5 h-2.5" />
      ~{minutes < 60 ? `${minutes}د` : `${Math.floor(minutes / 60)}س ${minutes % 60}د`}
    </span>
  );
}

/* ─── Medical Alert Badge ──────────────────────────────────────────────────── */
function MedicalAlertBadge() {
  return (
    <span className="inline-flex items-center gap-0.5 px-1 py-0.5 rounded text-[9px] font-bold"
      style={{ background: "#fef2f2", color: "#dc2626" }}>
      <Heart className="w-2.5 h-2.5" />
    </span>
  );
}

function OrthoBadge({ item }: { item: TodayJourneyItem }) {
  if (!item.hasActiveOrthoCase) return null;
  const details = [
    item.orthoCaseNumber,
    item.orthoCurrentStage,
    item.orthoNextAppointmentDate ? `الموعد: ${item.orthoNextAppointmentDate}` : null,
  ].filter(Boolean).join(" | ");

  return (
    <span
      className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-bold"
      style={{ background: "#f5f3ff", color: "#7c3aed", border: "1px solid #ddd6fe" }}
      title={details || "حالة تقويم نشطة"}
    >
      <GitBranch className="w-2.5 h-2.5" />
      تقويم
    </span>
  );
}

/* ─── CLIN-05: Today's ortho visit fields chip ───────────────────────────────
   Shown inline on the desktop row when the linked Visit carries today's ortho
   clinical data (WireUpper / WireLower / CurrentStage). The full Arabic-label
   breakdown is in the title tooltip; the chip itself shows a compact summary so
   the daily-operations screen can see at a glance that the orthodontist did an
   adjustment today. Subtle, RTL, only rendered when at least one field is set.
*/
function OrthoVisitFieldsChip({ item }: { item: TodayJourneyItem }) {
  const wireUpper = item.orthoVisitWireUpper ?? null;
  const wireLower = item.orthoVisitWireLower ?? null;
  const stage = item.orthoVisitCurrentStage ?? null;
  if (!wireUpper && !wireLower && !stage) return null;

  const tooltipParts: string[] = [];
  if (wireUpper) tooltipParts.push(`السلك العلوي: ${wireUpper}`);
  if (wireLower) tooltipParts.push(`السلك السفلي: ${wireLower}`);
  if (stage) tooltipParts.push(`المرحلة الحالية: ${stage}`);

  // Compact inline summary — first non-empty wire value, fallback to stage.
  const compact = wireUpper ?? wireLower ?? stage ?? "";

  return (
    <span
      className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-semibold"
      style={{ background: "#eef2ff", color: "#4338ca", border: "1px solid #c7d2fe" }}
      title={tooltipParts.join(" • ")}
    >
      <GitBranch className="w-2.5 h-2.5" />
      <span>{compact}</span>
    </span>
  );
}

/* ─── CLIN-05: Today's ortho visit fields block (mobile expanded) ────────────
   A subtle labeled block rendered inside the existing expanded ortho section on
   mobile cards. Shows the three Arabic-labeled fields when any is non-null.
*/
function OrthoVisitFieldsBlock({ item }: { item: TodayJourneyItem }) {
  const wireUpper = item.orthoVisitWireUpper ?? null;
  const wireLower = item.orthoVisitWireLower ?? null;
  const stage = item.orthoVisitCurrentStage ?? null;
  if (!wireUpper && !wireLower && !stage) return null;

  return (
    <div className="mt-1.5 rounded-lg border border-indigo-100 bg-indigo-50/60 p-1.5 text-[10px] text-indigo-900">
      <div className="font-bold mb-0.5">إجراءات اليوم التقويمية</div>
      <div className="flex flex-wrap gap-x-3 gap-y-1">
        {wireUpper && <span><strong>السلك العلوي:</strong> {wireUpper}</span>}
        {wireLower && <span><strong>السلك السفلي:</strong> {wireLower}</span>}
        {stage && <span><strong>المرحلة الحالية:</strong> {stage}</span>}
      </div>
    </div>
  );
}

/* ─── Overdue Badge ─────────────────────────────────────────────────────────── */
function OverdueBadge({ text }: { text: string }) {
  return (
    <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[9px] font-bold animate-pulse"
      style={{ background: "#fef2f2", color: "#ef4444" }}>
      <AlertCircle className="w-2.5 h-2.5" />
      {text}
    </span>
  );
}

/* ─── Session Duration Chip ─────────────────────────────────────────────────── */
function SessionDurationChip({ since }: { since?: string }) {
  const text = fmtSessionDuration(since);
  if (!text) return null;
  return (
    <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[9px] font-bold"
      style={{ background: "#faf5ff", color: "#9333ea" }}>
      <Clock className="w-2.5 h-2.5" />
      {text}
    </span>
  );
}

/* ─── New Patient Badge ─────────────────────────────────────────────────────── */
function NewPatientBadge() {
  return (
    <span className="inline-flex items-center px-1 py-0.5 rounded text-[8px] font-extrabold"
      style={{ background: "#eff6ff", color: "#2563eb", border: "1px solid #bfdbfe" }}>
      جديد
    </span>
  );
}

/* ─── Props ───────────────────────────────────────────────────────────────── */

interface AppointmentsTableProps {
  items: TodayJourneyItem[];
  loading: boolean;
  isDoctor: boolean;
  canProcessCheckout: boolean;
  isReception: boolean;
  isAccountant: boolean;
  queueWaitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  selectedPatientId?: string;
  // Callbacks
  onIntake: (item: TodayJourneyItem) => void;
  onSendToQueue: (item: TodayJourneyItem) => void;
  onCallPatient: (item: TodayJourneyItem) => void;
  onEnterRoom: (item: TodayJourneyItem) => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateDraftInvoice: (item: TodayJourneyItem) => void;
  createDraftInvoicePending: boolean;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onWhatsApp: (item: TodayJourneyItem) => void;
  onNoShow: (item: TodayJourneyItem) => void;
  onCancel: (item: TodayJourneyItem) => void;
  onViewPatient: (item: TodayJourneyItem) => void;
  onCompleteVisit: (item: TodayJourneyItem) => void;
  onOpenSidePanel: (item: TodayJourneyItem) => void;
  // Right-click context menu
  onContextMenu?: (e: React.MouseEvent, item: TodayJourneyItem) => void;
}

export default function AppointmentsTable({
  items, loading, isDoctor, canProcessCheckout, queueWaitTime, selectedPatientId,
  onIntake, onSendToQueue, onCallPatient, onEnterRoom,
  onQuickPayment, onCreateDraftInvoice, createDraftInvoicePending, onBookAppointment, onWhatsApp,
  onNoShow, onCancel, onViewPatient, onCompleteVisit, onOpenSidePanel,
  onContextMenu,
}: AppointmentsTableProps) {
  // Mobile: expanded row
  const [expandedId, setExpandedId] = useState<string | null>(null);

  if (loading) {
    return (
      <div className="space-y-1 p-4">
        {[1, 2, 3, 4, 5, 6, 7, 8].map(i => (
          <div key={i} className="h-[52px] rounded-lg bg-gray-50 animate-pulse" style={{ border: "1px solid #f1f5f9" }} />
        ))}
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20" style={{ color: "#94a3b8" }}>
        <Calendar className="w-12 h-12 mb-3 opacity-20" />
        <p className="text-sm font-bold">لا توجد مواعيد</p>
        <p className="text-xs mt-1">اختر تبويباً آخر أو غيّر الفلاتر</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      {/* Queue Wait Banner */}
      {queueWaitTime && queueWaitTime.estimatedMinutes > 0 && (
        <div className="mx-4 mt-3 p-2.5 rounded-lg flex items-center gap-2"
          style={{ background: "#fff7ed", border: "1px solid #fde8d0" }}>
          <Clock className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
          <span className="text-xs font-bold" style={{ color: NAVY }}>
            وقت الانتظار المتوقع في الانتظار: ~{queueWaitTime.estimatedMinutes < 60
              ? `${queueWaitTime.estimatedMinutes} دقيقة`
              : `${Math.floor(queueWaitTime.estimatedMinutes / 60)} ساعة ${queueWaitTime.estimatedMinutes % 60} دقيقة`}
          </span>
          <span className="text-[10px]" style={{ color: "#94a3b8" }}>
            ({queueWaitTime.patientsAhead} مرضى في الانتظار)
          </span>
        </div>
      )}

      {/* Desktop table — Microsoft DataGrid style */}
      <table className="w-full hidden lg:table" style={{ borderCollapse: "separate", borderSpacing: 0 }}>
        <thead>
          <tr className="text-[11px] font-semibold sticky top-0 z-10"
            style={{ color: "#64748b", background: "#f8fafc" }}>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الوقت</th>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>المريض</th>
            {!isDoctor && <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الهاتف</th>}
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الطبيب</th>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الخدمة</th>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الغرفة</th>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الحالة</th>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>الإجراء التالي</th>
            <th className="text-right py-2.5 px-3 font-semibold" style={{ borderBottom: "1px solid #e5e7eb" }}>إجراءات</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, idx) => (
            <AppointmentRow
              key={item.appointmentId}
              item={item}
              isDoctor={isDoctor}
              canProcessCheckout={canProcessCheckout}
              isSelected={selectedPatientId === item.patientId}
              isEven={idx % 2 === 1}
              queueWaitMinutes={
                (item.queueStatus === "Waiting" || item.queueStatus === "Called")
                  ? queueWaitTime?.estimatedMinutes
                  : undefined
              }
              onIntake={onIntake}
              onSendToQueue={onSendToQueue}
              onCallPatient={onCallPatient}
              onEnterRoom={onEnterRoom}
              onQuickPayment={onQuickPayment}
              onCreateDraftInvoice={onCreateDraftInvoice}
              createDraftInvoicePending={createDraftInvoicePending}
              onBookAppointment={onBookAppointment}
              onWhatsApp={onWhatsApp}
              onNoShow={onNoShow}
              onCancel={onCancel}
              onViewPatient={onViewPatient}
              onCompleteVisit={onCompleteVisit}
              onOpenSidePanel={onOpenSidePanel}
              onContextMenu={onContextMenu}
            />
          ))}
        </tbody>
      </table>

      {/* Mobile cards */}
      <div className="lg:hidden p-3 space-y-2">
        {items.map(item => (
          <MobileCard
            key={item.appointmentId}
            item={item}
            isDoctor={isDoctor}
            canProcessCheckout={canProcessCheckout}
            expanded={expandedId === item.appointmentId}
            onToggle={() => setExpandedId(expandedId === item.appointmentId ? null : item.appointmentId)}
            queueWaitMinutes={
              (item.queueStatus === "Waiting" || item.queueStatus === "Called")
                ? queueWaitTime?.estimatedMinutes
                : undefined
            }
            onIntake={onIntake}
            onSendToQueue={onSendToQueue}
            onCallPatient={onCallPatient}
            onEnterRoom={onEnterRoom}
            onQuickPayment={onQuickPayment}
            onCreateDraftInvoice={onCreateDraftInvoice}
            createDraftInvoicePending={createDraftInvoicePending}
            onBookAppointment={onBookAppointment}
            onWhatsApp={onWhatsApp}
            onNoShow={onNoShow}
            onCancel={onCancel}
            onViewPatient={onViewPatient}
            onCompleteVisit={onCompleteVisit}
            onOpenSidePanel={onOpenSidePanel}
            onContextMenu={onContextMenu}
          />
        ))}
      </div>
    </div>
  );
}

/* ─── Desktop row — Microsoft DataGrid style ───────────────────────────────── */
function AppointmentRow({
  item, isDoctor, canProcessCheckout, isSelected, isEven, queueWaitMinutes,
  onIntake, onSendToQueue, onCallPatient, onEnterRoom,
  onQuickPayment, onWhatsApp,
  onViewPatient, onCompleteVisit, onOpenSidePanel, onContextMenu,
}: {
  item: TodayJourneyItem; isDoctor: boolean; canProcessCheckout: boolean; isSelected: boolean; isEven: boolean; queueWaitMinutes?: number;
} & Omit<AppointmentsTableProps, "items" | "loading" | "isAccountant" | "isReception" | "queueWaitTime" | "selectedPatientId">) {
  const canAct = item.appointmentStatus !== "Cancelled" && item.appointmentStatus !== "Completed" && item.appointmentStatus !== "NoShow";
  const overdueText = isAppointmentOverdue(item) ? fmtOverdueMinutes(item) : "";

  // Determine next patient highlight
  const isNextPatient = item.nextAction === "CallPatient" || item.nextAction === "EnterRoom" ||
    (item.nextAction === "Intake" && (item.appointmentStatus === "Scheduled" || item.appointmentStatus === "Confirmed"));

  // ── Secondary quick-action visibility rules ──────────────────────────────
  // Surface the most common secondary actions inline so the user does not need
  // to open the context menu. Only render a button when its condition is met
  // (no disabled buttons — keep the row clean).
  const showQuickPayment = canProcessCheckout && (
    item.checkoutStatus === "ReadyForCheckout" || item.nextAction === "Checkout"
  );
  // Visit is in-progress and can be completed.
  const showCompleteVisit =
    item.appointmentStatus === "InRoom" ||
    item.appointmentStatus === "InProgress" ||
    item.queueStatus === "InRoom" ||
    item.queueStatus === "InProgress";
  const showWhatsApp = !!item.patientPhone;

  // Render the primary labeled action button based on the nextAction status.
  // Track whether a real (non-fallback) primary action was set so the
  // secondary View-File icon can be hidden when the fallback already covers it.
  let primaryBtn: React.ReactNode = null;
  let hasPrimaryAction = false;
  if (canAct && !isDoctor) {
    if (item.nextAction === "Intake") {
      hasPrimaryAction = true;
      primaryBtn = (
        <button
          onClick={() => onIntake(item)}
          className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#16a34a" }}
        >
          <UserCheck className="w-3.5 h-3.5" />
          <span>تسجيل وصول</span>
        </button>
      );
    } else if (item.nextAction === "SendToQueue") {
      hasPrimaryAction = true;
      primaryBtn = (
        <button
          onClick={() => onSendToQueue(item)}
          className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#f5922e" }}
        >
          <ClipboardList className="w-3.5 h-3.5" />
          <span>إضافة للانتظار</span>
        </button>
      );
    } else if (item.queueItemId && (item.queueStatus === "Waiting" || item.nextAction === "CallPatient")) {
      hasPrimaryAction = true;
      primaryBtn = (
        <button
          onClick={() => onCallPatient(item)}
          className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#d97706" }}
        >
          <Phone className="w-3.5 h-3.5" />
          <span>نداء المريض</span>
        </button>
      );
    } else if (item.queueItemId && (item.queueStatus === "Called" || item.nextAction === "EnterRoom")) {
      hasPrimaryAction = true;
      primaryBtn = (
        <button
          onClick={() => onEnterRoom(item)}
          className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#9333ea" }}
        >
          <DoorOpen className="w-3.5 h-3.5" />
          <span>دخول العيادة</span>
        </button>
      );
    }
  }

  // Checkout primary button
  if (canProcessCheckout && (item.nextAction === "Checkout" || item.checkoutStatus === "ReadyForCheckout")) {
    hasPrimaryAction = true;
    primaryBtn = (
      <button
        onClick={() => onCompleteVisit(item)}
        className="px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
        style={{ background: "#10b981" }} // Emerald/Green for checkout
      >
        <CheckCircle className="w-3.5 h-3.5" />
        <span>التحصيل والخروج</span>
      </button>
    );
  }

  if (!hasPrimaryAction) {
    primaryBtn = (
      <button
        onClick={() => onViewPatient(item)}
        className="px-3 py-1.5 rounded-lg text-xs font-bold transition hover:bg-gray-100 flex items-center justify-center gap-1.5"
        style={{ color: "#3d7ab5", border: "1px solid #3d7ab530" }}
      >
        <Eye className="w-3.5 h-3.5" />
        <span>عرض الملف</span>
      </button>
    );
  }

  return (
    <tr
      className="transition-colors cursor-default group"
      onContextMenu={(e) => {
        e.preventDefault();
        onContextMenu?.(e, item);
      }}
      style={{
        height: 52,
        background: isSelected
          ? "#eff6ff"
          : isNextPatient
            ? "linear-gradient(90deg, #f0f7ff, #faf5ff)"
            : isEven
              ? "#fafbfc"
              : "#fff",
        borderRight: isSelected ? "3px solid #3d7ab5" : "3px solid transparent",
        borderBottom: "1px solid #f1f5f9",
      }}
      onMouseEnter={e => {
        if (!isSelected) (e.currentTarget as HTMLElement).style.background = "#eff6ff";
      }}
      onMouseLeave={e => {
        if (!isSelected) {
          (e.currentTarget as HTMLElement).style.background = isNextPatient
            ? "linear-gradient(90deg, #f0f7ff, #faf5ff)"
            : isEven ? "#fafbfc" : "#fff";
        }
      }}
    >
      <td className="py-2 px-3 text-sm font-bold" style={{ color: NAVY }}>
        <div className="flex items-center gap-1.5">
          {fmtTime(item.appointmentTime)}
          {overdueText && <OverdueBadge text={overdueText} />}
        </div>
      </td>
      <td className="py-2 px-3">
        <div className="flex items-center gap-1.5">
          {item.hasMedicalAlerts && <MedicalAlertBadge />}
          <button onClick={() => onOpenSidePanel(item)}
            className="text-sm font-semibold hover:underline flex items-center gap-1.5 transition-colors"
            style={{ color: isSelected ? NAVY : "#3d7ab5" }}>
            {item.patientName}
            <PanelRight className="w-3 h-3 opacity-0 group-hover:opacity-40 transition-opacity" />
          </button>
          {item.visitCount != null && item.visitCount <= 1 && <NewPatientBadge />}
          <OrthoBadge item={item} />
          <OrthoVisitFieldsChip item={item} />
        </div>
      </td>
      {!isDoctor && (
        <td className="py-2 px-3">
          {item.patientPhone ? (
            <a href={`https://wa.me/${normalizePhone(item.patientPhone)}`} target="_blank" rel="noopener noreferrer"
              className="text-xs flex items-center gap-1 hover:underline transition-colors" style={{ color: "#64748b" }}>
              <Phone className="w-3 h-3" />
              {item.patientPhone}
            </a>
          ) : (
            <span className="text-xs" style={{ color: "#94a3b8" }}>—</span>
          )}
        </td>
      )}
      <td className="py-2 px-3 text-xs font-medium" style={{ color: "#475569" }}>{item.doctorName}</td>
      <td className="py-2 px-3 text-xs font-medium" style={{ color: "#475569" }}>
        <div>{item.serviceName ?? "—"}</div>
        {item.proposedProcedure && (
          <div className="text-[10px] text-purple-600 font-bold mt-0.5 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-100 inline-block">
            📌 المقترح: {item.proposedProcedure}
          </div>
        )}
      </td>
      <td className="py-2 px-3 text-xs font-medium" style={{ color: "#475569" }}>
        {item.roomName ? (
          <span className="inline-flex items-center gap-1">
            <Building2 className="w-3 h-3" />
            {item.roomName}
          </span>
        ) : "—"}
        {(item.appointmentStatus === "InRoom" || item.appointmentStatus === "InProgress") && item.inRoomSince && (
          <SessionDurationChip since={item.inRoomSince} />
        )}
      </td>
      <td className="py-2 px-3">
        <div className="flex items-center gap-1.5">
          <StatusBadge status={item.appointmentStatus} />
          {queueWaitMinutes && <WaitTimeChip minutes={queueWaitMinutes} />}
        </div>
      </td>
      <td className="py-2 px-3"><NextActionBadge action={item.nextAction} /></td>
      <td className="py-2 px-3">
        <div className="flex items-center gap-2">
          {/* Labeled Dynamic Primary Action */}
          {primaryBtn}

          {/* ── Secondary quick-action icon buttons ──────────────────────
              Surface the most common secondary actions inline so the user
              does not need to open the context menu. Only rendered when
              their condition is met (no disabled buttons). The View-File
              icon is hidden when the primary button already falls back to
              "عرض الملف" to avoid redundancy. */}
          <div className="flex items-center gap-1">
            {showQuickPayment && (
              <button
                type="button"
                onClick={() => onQuickPayment(item)}
                title="تحصيل سريع"
                aria-label="تحصيل سريع"
                className="w-8 h-8 rounded-lg flex items-center justify-center bg-emerald-50 text-emerald-600 hover:bg-emerald-100 transition-colors"
              >
                <Wallet className="w-4 h-4" />
              </button>
            )}
            {showCompleteVisit && (
              <button
                type="button"
                onClick={() => onCompleteVisit(item)}
                title="إكمال الزيارة"
                aria-label="إكمال الزيارة"
                className="w-8 h-8 rounded-lg flex items-center justify-center bg-blue-50 text-blue-600 hover:bg-blue-100 transition-colors"
              >
                <ClipboardCheck className="w-4 h-4" />
              </button>
            )}
            {showWhatsApp && (
              <button
                type="button"
                onClick={() => onWhatsApp(item)}
                title="إرسال رسالة واتساب"
                aria-label="إرسال رسالة واتساب"
                className="w-8 h-8 rounded-lg flex items-center justify-center bg-green-50 text-green-600 hover:bg-green-100 transition-colors"
              >
                <MessageCircle className="w-4 h-4" />
              </button>
            )}
            {hasPrimaryAction && (
              <button
                type="button"
                onClick={() => onViewPatient(item)}
                title="عرض الملف"
                aria-label="عرض الملف"
                className="w-8 h-8 rounded-lg flex items-center justify-center bg-gray-50 text-gray-600 hover:bg-gray-100 transition-colors"
              >
                <Eye className="w-4 h-4" />
              </button>
            )}
          </div>

          {/* Inline Context Trigger */}
          <button
            onClick={(e) => {
              e.preventDefault();
              onContextMenu?.(e, item);
            }}
            title="خيارات إضافية"
            className="w-8 h-8 rounded-lg flex items-center justify-center border hover:bg-gray-50 transition-all text-[#64748b] border-[#e2e8f0]"
          >
            <MoreHorizontal className="w-4 h-4" />
          </button>
        </div>
      </td>
    </tr>
  );
}

/* ─── Mobile card ──────────────────────────────────────────────────────────── */
function MobileCard({
  item, isDoctor, canProcessCheckout, expanded, onToggle, queueWaitMinutes,
  onIntake, onSendToQueue, onCallPatient, onEnterRoom,
  onViewPatient, onCompleteVisit, onOpenSidePanel, onContextMenu,
}: {
  item: TodayJourneyItem; isDoctor: boolean; canProcessCheckout: boolean; expanded: boolean; onToggle: () => void; queueWaitMinutes?: number;
} & Omit<AppointmentsTableProps, "items" | "loading" | "isAccountant" | "isReception" | "queueWaitTime" | "selectedPatientId">) {
  const canAct = item.appointmentStatus !== "Cancelled" && item.appointmentStatus !== "Completed" && item.appointmentStatus !== "NoShow";
  const overdueText = isAppointmentOverdue(item) ? fmtOverdueMinutes(item) : "";

  // Render the primary labeled action button based on the nextAction status
  let primaryBtn = null;
  if (canAct && !isDoctor) {
    if (item.nextAction === "Intake") {
      primaryBtn = (
        <button
          onClick={() => onIntake(item)}
          className="w-full px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#16a34a" }}
        >
          <UserCheck className="w-3.5 h-3.5" />
          <span>تسجيل وصول</span>
        </button>
      );
    } else if (item.nextAction === "SendToQueue") {
      primaryBtn = (
        <button
          onClick={() => onSendToQueue(item)}
          className="w-full px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#f5922e" }}
        >
          <ClipboardList className="w-3.5 h-3.5" />
          <span>إضافة للانتظار</span>
        </button>
      );
    } else if (item.queueItemId && (item.queueStatus === "Waiting" || item.nextAction === "CallPatient")) {
      primaryBtn = (
        <button
          onClick={() => onCallPatient(item)}
          className="w-full px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#d97706" }}
        >
          <Phone className="w-3.5 h-3.5" />
          <span>نداء المريض</span>
        </button>
      );
    } else if (item.queueItemId && (item.queueStatus === "Called" || item.nextAction === "EnterRoom")) {
      primaryBtn = (
        <button
          onClick={() => onEnterRoom(item)}
          className="w-full px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
          style={{ background: "#9333ea" }}
        >
          <DoorOpen className="w-3.5 h-3.5" />
          <span>دخول العيادة</span>
        </button>
      );
    }
  }

  // Checkout primary button
  if (canProcessCheckout && (item.nextAction === "Checkout" || item.checkoutStatus === "ReadyForCheckout")) {
    primaryBtn = (
      <button
        onClick={() => onCompleteVisit(item)}
        className="w-full px-3 py-1.5 rounded-lg text-xs font-bold text-white transition hover:opacity-90 flex items-center justify-center gap-1.5 shadow-sm"
        style={{ background: "#10b981" }} // Emerald/Green for checkout
      >
        <CheckCircle className="w-3.5 h-3.5" />
        <span>التحصيل والخروج</span>
      </button>
    );
  }

  if (!primaryBtn) {
    primaryBtn = (
      <button
        onClick={() => onViewPatient(item)}
        className="w-full px-3 py-1.5 rounded-lg text-xs font-bold transition hover:bg-gray-100 flex items-center justify-center gap-1.5"
        style={{ color: "#3d7ab5", border: "1px solid #3d7ab530" }}
      >
        <Eye className="w-3.5 h-3.5" />
        <span>عرض الملف</span>
      </button>
    );
  }

  return (
    <div
      className="rounded-xl border p-3 transition-shadow"
      style={{
        borderColor: overdueText ? "#fecaca" : "#e5e7eb",
        background: overdueText ? "#fef2f208" : "#fff",
      }}
      onContextMenu={(e) => {
        e.preventDefault();
        onContextMenu?.(e, item);
      }}
    >
      <div className="flex items-center gap-2">
        <span className="text-sm font-bold" style={{ color: NAVY }}>{fmtTime(item.appointmentTime)}</span>
        <StatusBadge status={item.appointmentStatus} />
        {queueWaitMinutes && <WaitTimeChip minutes={queueWaitMinutes} />}
        {overdueText && <OverdueBadge text={overdueText} />}
        <div className="flex-1" />
        <button onClick={onToggle} className="p-1 rounded-lg hover:bg-gray-50 flex items-center justify-center gap-1" title="تفاصيل إضافية">
          {expanded ? <ChevronUp className="w-4 h-4 text-gray-400" /> : <ChevronDown className="w-4 h-4 text-gray-400" />}
        </button>
      </div>
      <div className="mt-1.5 flex items-center gap-2">
        {item.hasMedicalAlerts && <MedicalAlertBadge />}
        <button onClick={() => onOpenSidePanel(item)} className="text-sm font-semibold" style={{ color: "#3d7ab5" }}>
          {item.patientName}
        </button>
        {item.visitCount != null && item.visitCount <= 1 && <NewPatientBadge />}
        <OrthoBadge item={item} />
        <OrthoVisitFieldsChip item={item} />
        <span className="text-xs" style={{ color: "#64748b" }}>{item.doctorName}</span>
        <NextActionBadge action={item.nextAction} />
        {(item.appointmentStatus === "InRoom" || item.appointmentStatus === "InProgress") && item.inRoomSince && (
          <SessionDurationChip since={item.inRoomSince} />
        )}
      </div>

      {expanded && (
        <div className="mt-3 pt-3 border-t space-y-2" style={{ borderColor: "#f1f5f9" }}>
          <div className="flex flex-wrap gap-3 text-xs" style={{ color: "#64748b" }}>
            {item.serviceName && (
              <span className="flex items-center gap-1">
                <strong>الخدمة:</strong> {item.serviceName}
              </span>
            )}
            {item.proposedProcedure && (
              <div className="text-[10px] text-purple-600 font-bold mt-1 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-100 block w-full">
                📌 الإجراء المقترح للمحاسبة: {item.proposedProcedure}
              </div>
            )}
            {item.roomName && (
              <span className="flex items-center gap-1">
                <strong>الغرفة:</strong> {item.roomName}
              </span>
            )}
            {item.hasActiveOrthoCase && (
              <div className="w-full rounded-lg border border-violet-100 bg-violet-50 p-2 text-[10px] text-violet-800">
                <div className="font-bold">{item.orthoCaseNumber ?? "حالة تقويم نشطة"}</div>
                <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-1">
                  {item.orthoCurrentStage && <span>المرحلة: {item.orthoCurrentStage}</span>}
                  {item.orthoLastVisitDate && <span>آخر زيارة: {item.orthoLastVisitDate}</span>}
                  {item.orthoNextAppointmentDate && <span>الموعد التالي: {item.orthoNextAppointmentDate}</span>}
                  {item.orthoContractRemaining != null && (
                    <span>المتبقي: {item.orthoContractRemaining.toLocaleString("ar-YE")} ر.ي</span>
                  )}
                </div>
                <OrthoVisitFieldsBlock item={item} />
              </div>
            )}
          </div>
        </div>
      )}

      {/* Unified Action Footer */}
      <div className="mt-3 pt-2.5 border-t flex items-center gap-2" style={{ borderColor: "#f1f5f9" }}>
        <div className="flex-1">
          {primaryBtn}
        </div>

        {/* Options Button to Trigger Context Menu */}
        <button
          onClick={(e) => {
            e.preventDefault();
            onContextMenu?.(e, item);
          }}
          title="خيارات إضافية"
          className="w-8 h-8 rounded-lg flex items-center justify-center border hover:bg-gray-50 transition-all text-[#64748b] border-[#e2e8f0]"
        >
          <MoreHorizontal className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
