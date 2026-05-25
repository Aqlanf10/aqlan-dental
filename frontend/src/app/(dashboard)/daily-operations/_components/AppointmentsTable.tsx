/**
 * Appointments Table with quick action buttons for each row.
 * Supports desktop table and mobile card views.
 * Includes: wait time display, side panel trigger.
 */

"use client";

import { useState } from "react";
import {
  Calendar, UserCheck, ClipboardList, DoorOpen, CheckCircle,
  CreditCard, CalendarPlus, MessageCircle, XCircle,
  UserX, MoreHorizontal, Eye, Phone, Clock,
  Building2, PanelRight, Timer, AlertCircle, Heart,
} from "lucide-react";
import {
  APPT_STATUS_LABELS, STATUS_COLORS, ACTION_LABELS,
  fmtTime, normalizePhone, ORANGE, NAVY,
  isAppointmentOverdue, fmtOverdueMinutes, fmtSessionDuration,
  type TodayJourneyItem,
} from "../_lib/constants";

/* ─── Row action button ──────────────────────────────────────────────────── */
function ActionBtn({
  icon: Icon, label, color, onClick, disabled,
}: {
  icon: React.ElementType; label: string; color: string;
  onClick: () => void; disabled?: boolean;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      title={label}
      className="w-8 h-8 rounded-lg flex items-center justify-center transition-opacity hover:opacity-80 disabled:opacity-30"
      style={{ background: color + "15", color }}
    >
      <Icon className="w-3.5 h-3.5" />
    </button>
  );
}

/* ─── Status badge ────────────────────────────────────────────────────────── */
function StatusBadge({ status }: { status: string }) {
  const c = STATUS_COLORS[status] ?? STATUS_COLORS.Scheduled;
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold"
      style={{ background: c.bg, color: c.text, border: `1px solid ${c.border}` }}>
      {APPT_STATUS_LABELS[status] ?? status}
    </span>
  );
}

/* ─── Next Action badge ───────────────────────────────────────────────────── */
function NextActionBadge({ action }: { action: string }) {
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
  isReception: boolean;
  isAccountant: boolean;
  queueWaitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  // Callbacks
  onIntake: (item: TodayJourneyItem) => void;
  onSendToQueue: (item: TodayJourneyItem) => void;
  onCallPatient: (item: TodayJourneyItem) => void;
  onEnterRoom: (item: TodayJourneyItem) => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onWhatsApp: (item: TodayJourneyItem) => void;
  onNoShow: (item: TodayJourneyItem) => void;
  onCancel: (item: TodayJourneyItem) => void;
  onViewPatient: (item: TodayJourneyItem) => void;
  onCompleteVisit: (item: TodayJourneyItem) => void;
  onOpenSidePanel: (item: TodayJourneyItem) => void;
}

export default function AppointmentsTable({
  items, loading, isDoctor, queueWaitTime,
  onIntake, onSendToQueue, onCallPatient, onEnterRoom,
  onQuickPayment, onBookAppointment, onWhatsApp,
  onNoShow, onCancel, onViewPatient, onCompleteVisit, onOpenSidePanel,
}: Omit<AppointmentsTableProps, "isReception" | "isAccountant">) {
  // Mobile: expanded row
  const [expandedId, setExpandedId] = useState<string | null>(null);

  if (loading) {
    return (
      <div className="space-y-2">
        {[1, 2, 3, 4, 5].map(i => (
          <div key={i} className="h-14 rounded-xl bg-gray-100 animate-pulse" />
        ))}
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="text-center py-12" style={{ color: "#94a3b8" }}>
        <Calendar className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm font-bold">لا توجد مواعيد</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      {/* Queue Wait Banner */}
      {queueWaitTime && queueWaitTime.estimatedMinutes > 0 && (
        <div className="mb-3 p-2.5 rounded-lg flex items-center gap-2"
          style={{ background: "#fff7ed", border: "1px solid #fde8d0" }}>
          <Clock className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
          <span className="text-xs font-bold" style={{ color: NAVY }}>
            وقت الانتظار المتوقع في الطابور: ~{queueWaitTime.estimatedMinutes < 60
              ? `${queueWaitTime.estimatedMinutes} دقيقة`
              : `${Math.floor(queueWaitTime.estimatedMinutes / 60)} ساعة ${queueWaitTime.estimatedMinutes % 60} دقيقة`}
          </span>
          <span className="text-[10px]" style={{ color: "#94a3b8" }}>
            ({queueWaitTime.patientsAhead} مرضى في الانتظار)
          </span>
        </div>
      )}

      {/* Desktop table */}
      <table className="w-full hidden lg:table">
        <thead>
          <tr className="text-[11px] font-bold border-b-2" style={{ color: "#64748b", borderColor: "#e8f0f9" }}>
            <th className="text-right py-2.5 px-2">الوقت</th>
            <th className="text-right py-2.5 px-2">المريض</th>
            {!isDoctor && <th className="text-right py-2.5 px-2">الهاتف</th>}
            <th className="text-right py-2.5 px-2">الطبيب</th>
            <th className="text-right py-2.5 px-2">الخدمة</th>
            <th className="text-right py-2.5 px-2">الغرفة</th>
            <th className="text-right py-2.5 px-2">الحالة</th>
            <th className="text-right py-2.5 px-2">الإجراء التالي</th>
            <th className="text-right py-2.5 px-2">إجراءات</th>
          </tr>
        </thead>
        <tbody>
          {items.map(item => (
            <AppointmentRow
              key={item.appointmentId}
              item={item}
              isDoctor={isDoctor}
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
              onBookAppointment={onBookAppointment}
              onWhatsApp={onWhatsApp}
              onNoShow={onNoShow}
              onCancel={onCancel}
              onViewPatient={onViewPatient}
              onCompleteVisit={onCompleteVisit}
              onOpenSidePanel={onOpenSidePanel}
            />
          ))}
        </tbody>
      </table>

      {/* Mobile cards */}
      <div className="lg:hidden space-y-2">
        {items.map(item => (
          <MobileCard
            key={item.appointmentId}
            item={item}
            isDoctor={isDoctor}
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
            onBookAppointment={onBookAppointment}
            onWhatsApp={onWhatsApp}
            onNoShow={onNoShow}
            onCancel={onCancel}
            onViewPatient={onViewPatient}
            onCompleteVisit={onCompleteVisit}
            onOpenSidePanel={onOpenSidePanel}
          />
        ))}
      </div>
    </div>
  );
}

/* ─── Desktop row ──────────────────────────────────────────────────────────── */
function AppointmentRow({
  item, isDoctor, queueWaitMinutes,
  onIntake, onSendToQueue, onCallPatient, onEnterRoom,
  onQuickPayment, onBookAppointment, onWhatsApp,
  onNoShow, onCancel, onViewPatient, onCompleteVisit, onOpenSidePanel,
}: {
  item: TodayJourneyItem; isDoctor: boolean; queueWaitMinutes?: number;
} & Omit<AppointmentsTableProps, "items" | "loading" | "isAccountant" | "isReception" | "queueWaitTime">) {
  const canAct = item.appointmentStatus !== "Cancelled" && item.appointmentStatus !== "Completed" && item.appointmentStatus !== "NoShow";
  const overdueText = isAppointmentOverdue(item) ? fmtOverdueMinutes(item) : "";

  return (
    <tr className="border-b hover:bg-[#f8fafc] transition-colors" style={{
      borderColor: "#f1f5f9",
      background: overdueText ? "#fef2f208" : undefined,
    }}>
      <td className="py-2.5 px-2 text-sm font-bold" style={{ color: NAVY }}>
        {fmtTime(item.appointmentTime)}
        {overdueText && <OverdueBadge text={overdueText} />}
      </td>
      <td className="py-2.5 px-2">
        <div className="flex items-center gap-1">
          {item.hasMedicalAlerts && <MedicalAlertBadge />}
          <button onClick={() => onOpenSidePanel(item)} className="text-sm font-semibold hover:underline flex items-center gap-1.5" style={{ color: "#3d7ab5" }}>
            {item.patientName}
            <PanelRight className="w-3 h-3 opacity-40" />
          </button>
          {item.visitCount != null && item.visitCount <= 1 && <NewPatientBadge />}
        </div>
      </td>
      {!isDoctor && (
        <td className="py-2.5 px-2">
          {item.patientPhone ? (
            <a href={`https://wa.me/${normalizePhone(item.patientPhone)}`} target="_blank" rel="noopener noreferrer"
              className="text-xs flex items-center gap-1 hover:underline" style={{ color: "#64748b" }}>
              <Phone className="w-3 h-3" />
              {item.patientPhone}
            </a>
          ) : (
            <span className="text-xs" style={{ color: "#94a3b8" }}>—</span>
          )}
        </td>
      )}
      <td className="py-2.5 px-2 text-xs" style={{ color: "#475569" }}>{item.doctorName}</td>
      <td className="py-2.5 px-2 text-xs" style={{ color: "#475569" }}>{item.serviceName ?? "—"}</td>
      <td className="py-2.5 px-2 text-xs" style={{ color: "#475569" }}>
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
      <td className="py-2.5 px-2">
        <div className="flex items-center gap-1.5">
          <StatusBadge status={item.appointmentStatus} />
          {queueWaitMinutes && <WaitTimeChip minutes={queueWaitMinutes} />}
        </div>
      </td>
      <td className="py-2.5 px-2"><NextActionBadge action={item.nextAction} /></td>
      <td className="py-2.5 px-2">
        <div className="flex items-center gap-1 flex-wrap">
          {/* View patient */}
          <ActionBtn icon={Eye} label="فتح الملف" color="#3d7ab5" onClick={() => onViewPatient(item)} />
          {/* Intake (Arrived) */}
          {canAct && item.nextAction === "Intake" && (
            <ActionBtn icon={UserCheck} label="تسجيل حضور" color="#16a34a" onClick={() => onIntake(item)} />
          )}
          {/* Send to queue */}
          {canAct && item.nextAction === "SendToQueue" && (
            <ActionBtn icon={ClipboardList} label="إضافة للطابور" color="#f5922e" onClick={() => onSendToQueue(item)} />
          )}
          {/* Call patient */}
          {canAct && item.queueItemId && (item.queueStatus === "Waiting" || item.nextAction === "CallPatient") && (
            <ActionBtn icon={Phone} label="نداء المريض" color="#d97706" onClick={() => onCallPatient(item)} />
          )}
          {/* Enter room */}
          {canAct && item.queueItemId && (item.queueStatus === "Called" || item.nextAction === "EnterRoom") && (
            <ActionBtn icon={DoorOpen} label="دخول الغرفة" color="#9333ea" onClick={() => onEnterRoom(item)} />
          )}
          {/* Complete / Checkout */}
          {canAct && (item.nextAction === "Checkout" || item.checkoutStatus === "ReadyForCheckout") && (
            <ActionBtn icon={CheckCircle} label="إنهاء الزيارة" color="#16a34a" onClick={() => onCompleteVisit(item)} />
          )}
          {/* Quick payment */}
          {!isDoctor && (
            <ActionBtn icon={CreditCard} label="دفعة سريعة" color="#22c55e" onClick={() => onQuickPayment(item)} />
          )}
          {/* Book follow-up */}
          <ActionBtn icon={CalendarPlus} label="حجز متابعة" color="#3d7ab5" onClick={() => onBookAppointment(item)} />
          {/* WhatsApp */}
          {!isDoctor && item.patientPhone && (
            <ActionBtn icon={MessageCircle} label="واتساب" color="#25D366" onClick={() => onWhatsApp(item)} />
          )}
          {/* No show */}
          {canAct && (item.appointmentStatus === "Scheduled" || item.appointmentStatus === "Confirmed") && (
            <ActionBtn icon={UserX} label="لم يحضر" color="#ef4444" onClick={() => onNoShow(item)} />
          )}
          {/* Cancel */}
          {canAct && item.appointmentStatus !== "NoShow" && (
            <ActionBtn icon={XCircle} label="إلغاء" color="#6b7280" onClick={() => onCancel(item)} />
          )}
        </div>
      </td>
    </tr>
  );
}

/* ─── Mobile card ──────────────────────────────────────────────────────────── */
function MobileCard({
  item, isDoctor, expanded, onToggle, queueWaitMinutes,
  onIntake, onSendToQueue, onCallPatient, onEnterRoom,
  onQuickPayment, onBookAppointment, onWhatsApp,
  onNoShow, onCancel, onViewPatient, onCompleteVisit, onOpenSidePanel,
}: {
  item: TodayJourneyItem; isDoctor: boolean; expanded: boolean; onToggle: () => void; queueWaitMinutes?: number;
} & Omit<AppointmentsTableProps, "items" | "loading" | "isAccountant" | "isReception" | "queueWaitTime">) {
  const canAct = item.appointmentStatus !== "Cancelled" && item.appointmentStatus !== "Completed" && item.appointmentStatus !== "NoShow";
  const overdueText = isAppointmentOverdue(item) ? fmtOverdueMinutes(item) : "";

  return (
    <div className="rounded-xl border p-3" style={{
      borderColor: overdueText ? "#fecaca" : "#e8f0f9",
      background: overdueText ? "#fef2f208" : "#fff",
    }}>
      <div className="flex items-center gap-2">
        <span className="text-sm font-bold" style={{ color: NAVY }}>{fmtTime(item.appointmentTime)}</span>
        <StatusBadge status={item.appointmentStatus} />
        {queueWaitMinutes && <WaitTimeChip minutes={queueWaitMinutes} />}
        {overdueText && <OverdueBadge text={overdueText} />}
        <div className="flex-1" />
        <button onClick={onToggle} className="p-1 rounded-lg hover:bg-gray-50">
          <MoreHorizontal className="w-4 h-4 text-gray-400" />
        </button>
      </div>
      <div className="mt-1.5 flex items-center gap-2">
        {item.hasMedicalAlerts && <MedicalAlertBadge />}
        <button onClick={() => onOpenSidePanel(item)} className="text-sm font-semibold" style={{ color: "#3d7ab5" }}>
          {item.patientName}
        </button>
        {item.visitCount != null && item.visitCount <= 1 && <NewPatientBadge />}
        <span className="text-xs" style={{ color: "#64748b" }}>{item.doctorName}</span>
        <NextActionBadge action={item.nextAction} />
        {(item.appointmentStatus === "InRoom" || item.appointmentStatus === "InProgress") && item.inRoomSince && (
          <SessionDurationChip since={item.inRoomSince} />
        )}
      </div>

      {expanded && (
        <div className="mt-3 pt-3 border-t space-y-2" style={{ borderColor: "#f1f5f9" }}>
          <div className="flex flex-wrap gap-1.5 text-xs" style={{ color: "#64748b" }}>
            {item.serviceName && <span>الخدمة: {item.serviceName}</span>}
            {item.roomName && <span>الغرفة: {item.roomName}</span>}
          </div>
          <div className="flex flex-wrap gap-1.5">
            <ActionBtn icon={Eye} label="فتح الملف" color="#3d7ab5" onClick={() => onViewPatient(item)} />
            {canAct && item.nextAction === "Intake" && (
              <ActionBtn icon={UserCheck} label="تسجيل حضور" color="#16a34a" onClick={() => onIntake(item)} />
            )}
            {canAct && item.nextAction === "SendToQueue" && (
              <ActionBtn icon={ClipboardList} label="إضافة للطابور" color="#f5922e" onClick={() => onSendToQueue(item)} />
            )}
            {canAct && item.queueItemId && (item.queueStatus === "Waiting" || item.nextAction === "CallPatient") && (
              <ActionBtn icon={Phone} label="نداء" color="#d97706" onClick={() => onCallPatient(item)} />
            )}
            {canAct && item.queueItemId && (item.queueStatus === "Called" || item.nextAction === "EnterRoom") && (
              <ActionBtn icon={DoorOpen} label="دخول" color="#9333ea" onClick={() => onEnterRoom(item)} />
            )}
            {canAct && (item.nextAction === "Checkout" || item.checkoutStatus === "ReadyForCheckout") && (
              <ActionBtn icon={CheckCircle} label="إنهاء" color="#16a34a" onClick={() => onCompleteVisit(item)} />
            )}
            {!isDoctor && (
              <ActionBtn icon={CreditCard} label="دفعة" color="#22c55e" onClick={() => onQuickPayment(item)} />
            )}
            <ActionBtn icon={CalendarPlus} label="متابعة" color="#3d7ab5" onClick={() => onBookAppointment(item)} />
            {!isDoctor && item.patientPhone && (
              <ActionBtn icon={MessageCircle} label="واتساب" color="#25D366" onClick={() => onWhatsApp(item)} />
            )}
            {canAct && (item.appointmentStatus === "Scheduled" || item.appointmentStatus === "Confirmed") && (
              <ActionBtn icon={UserX} label="لم يحضر" color="#ef4444" onClick={() => onNoShow(item)} />
            )}
            {canAct && (
              <ActionBtn icon={XCircle} label="إلغاء" color="#6b7280" onClick={() => onCancel(item)} />
            )}
          </div>
        </div>
      )}
    </div>
  );
}
