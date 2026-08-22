/**
 * PatientDetailPanel — Right-side patient detail panel for daily-ops.
 *
 * Extracted from daily-operations/page.tsx (Sprint 11B) without behavior
 * changes. Shows patient header, medical alerts, financial summary, active
 * contract/ortho, recent visits, and sticky quick-action buttons.
 *
 * RTL Arabic. Frontend-only.
 */

"use client";

import {
  X, Phone, AlertTriangle, Printer, CreditCard,
  Calendar, ClipboardList, MessageCircle,
} from "lucide-react";

import { toast } from "@/stores/toastStore";
import type { DailyJourneySummary } from "@/types/journey";

import {
  NAVY, BLUE, ORANGE,
  fmtDate, fmtRial,
  STATUS_COLORS, APPT_STATUS_LABELS, ACTION_LABELS,
  type TodayJourneyItem,
} from "../_lib/constants";

export function PatientDetailPanel({
  item, summary, waitTime, onClose,
  onQuickPayment, onCreateLabOrder, onBookAppointment, onWhatsApp, onViewPatient,
  medicalAlerts, finance, activeContract, activeOrtho,
}: {
  item: TodayJourneyItem;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  onClose: () => void;
  onQuickPayment: (item: TodayJourneyItem) => void;
  onCreateLabOrder: (item: TodayJourneyItem) => void;
  onBookAppointment: (item: TodayJourneyItem) => void;
  onWhatsApp: (item: TodayJourneyItem) => void;
  onViewPatient: (item: TodayJourneyItem) => void;
  medicalAlerts: DailyJourneySummary["medicalAlerts"];
  finance: DailyJourneySummary["financeSummary"] | undefined;
  activeContract: DailyJourneySummary["activeContract"] | undefined;
  activeOrtho: DailyJourneySummary["activeOrthoCase"] | undefined;
}) {
  const getInitials = (name: string) => {
    const parts = name.split(" ").filter(Boolean);
    if (parts.length >= 2) return parts[0][0] + parts[1][0];
    return parts[0]?.[0] ?? "?";
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex-shrink-0 px-4 py-3 flex items-start gap-3"
        style={{ borderBottom: "1px solid #e5e7eb" }}>
        <button onClick={onClose}
          className="w-7 h-7 rounded-lg flex items-center justify-center hover:bg-gray-100 transition flex-shrink-0 mt-0.5">
          <X className="w-4 h-4" style={{ color: "#64748b" }} />
        </button>
        <div className="flex items-center gap-3 flex-1 min-w-0">
          {/* Avatar */}
          <div className="w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 text-sm font-bold text-white"
            style={{ background: NAVY }}>
            {getInitials(item.patientName)}
          </div>
          <div className="min-w-0">
            <div className="text-sm font-extrabold truncate" style={{ color: NAVY }}>{item.patientName}</div>
            {item.patientPhone && (
              <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>
                <Phone className="w-3 h-3 inline me-1" />
                {item.patientPhone}
              </div>
            )}
            <div className="text-[11px]" style={{ color: "#94a3b8" }}>
              {item.doctorName} — {item.serviceName ?? "—"}
            </div>
          </div>
        </div>
      </div>

      {/* Scrollable content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Medical Alerts */}
        {medicalAlerts && medicalAlerts.length > 0 && (
          <div>
            <div className="text-[11px] font-bold mb-1.5 flex items-center gap-1" style={{ color: "#ef4444" }}>
              <AlertTriangle className="w-3 h-3" /> تنبيهات طبية
            </div>
            <div className="space-y-1">
              {medicalAlerts.map((alert, i) => (
                <div key={i} className="px-2.5 py-1.5 rounded-lg text-[11px] font-medium"
                  style={{
                    background: alert.severity === "danger" ? "#fef2f2" : alert.severity === "warning" ? "#fff7ed" : "#f0f5fb",
                    color: alert.severity === "danger" ? "#dc2626" : alert.severity === "warning" ? "#d97706" : NAVY,
                    border: `1px solid ${alert.severity === "danger" ? "#fecaca" : alert.severity === "warning" ? "#fde8d0" : "#dce8f5"}`,
                  }}>
                  {alert.label ?? alert.type ?? "تنبيه"}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Quick Status */}
        <div className="flex items-center gap-2 flex-wrap">
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold"
            style={{
              background: STATUS_COLORS[item.appointmentStatus]?.bg ?? "#f5f5f5",
              color: STATUS_COLORS[item.appointmentStatus]?.text ?? "#6b7280",
              border: `1px solid ${STATUS_COLORS[item.appointmentStatus]?.border ?? "#e5e7eb"}`,
            }}>
            {APPT_STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
          </span>
          {item.nextAction && item.nextAction !== "None" && (
            <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold"
              style={{ background: ORANGE + "12", color: ORANGE }}>
              {ACTION_LABELS[item.nextAction] ?? item.nextAction}
            </span>
          )}
          {waitTime && waitTime.estimatedMinutes > 0 && (
            <span className="text-[10px] font-bold" style={{ color: ORANGE }}>
              ~{waitTime.estimatedMinutes}د انتظار
            </span>
          )}
        </div>

        {/* Financial Summary */}
        {finance && (
          <div>
            <div className="text-[11px] font-bold mb-1.5 flex items-center justify-between" style={{ color: NAVY }}>
              <span>💰 الملخص المالي</span>
              {finance.latestPayment?.id && (
                <button onClick={async () => {
                  try {
                    const { downloadPdfFromApi } = await import("@/lib/pdfDownload");
                    const filename = `receipt-${finance.latestPayment!.receiptNumber || finance.latestPayment!.id}.pdf`;
                    await downloadPdfFromApi(`/api/payments/${finance.latestPayment!.id}/pdf`, filename);
                  } catch (err) {
                    const reason = err instanceof Error ? err.message : "خطأ";
                    toast.error(`فشل تحميل السند: ${reason}`);
                  }
                }}
                  className="flex items-center gap-1 px-2 py-0.5 rounded text-[9px] font-bold transition hover:bg-purple-100"
                  style={{ color: "#7c3aed", background: "#7c3aed10", border: "1px solid #7c3aed30" }}
                  title="طباعة سند آخر دفعة">
                  <Printer className="w-3 h-3" />
                  طباعة سند
                </button>
              )}
            </div>
            <div className="grid grid-cols-3 gap-1.5">
              <div className="p-2 rounded-lg text-center" style={{ background: "#fff7ed" }}>
                <div className="text-[9px] font-medium" style={{ color: ORANGE }}>المستحق</div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>{fmtRial(finance.outstandingBalance)}</div>
              </div>
              <div className="p-2 rounded-lg text-center" style={{ background: finance.overdueAmount > 0 ? "#fef2f2" : "#f0fdf4" }}>
                <div className="text-[9px] font-medium" style={{ color: finance.overdueAmount > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>{fmtRial(finance.overdueAmount)}</div>
              </div>
              <div className="p-2 rounded-lg text-center" style={{ background: "#f0fdf4" }}>
                <div className="text-[9px] font-medium" style={{ color: "#16a34a" }}>المدفوع</div>
                <div className="text-xs font-bold" style={{ color: NAVY }}>{fmtRial(finance.totalPaid ?? 0)}</div>
              </div>
            </div>
          </div>
        )}

        {/* Active Contract / Ortho */}
        {(activeContract || activeOrtho) && (
          <div>
            <div className="text-[11px] font-bold mb-1.5" style={{ color: NAVY }}>
              {activeOrtho ? "🦷 تقويم نشط" : "📋 عقد نشط"}
            </div>
            {activeContract && (
              <div className="p-2.5 rounded-lg" style={{ background: "#f0f5fb", border: "1px solid #dce8f5" }}>
                <div className="text-[11px] font-bold" style={{ color: NAVY }}>
                  {activeContract.specialty ?? "عقد علاج"}
                </div>
                <div className="mt-1.5">
                  <div className="flex items-center justify-between text-[10px] mb-0.5">
                    <span style={{ color: "#64748b" }}>التقدم</span>
                    <span className="font-bold" style={{ color: BLUE }}>
                      {fmtRial(activeContract.paidAmount)} / {fmtRial(activeContract.totalAmount)}
                    </span>
                  </div>
                  <div className="h-1.5 rounded-full overflow-hidden" style={{ background: "#e2e8f0" }}>
                    <div className="h-full rounded-full transition-all" style={{
                      width: `${activeContract.totalAmount ? (activeContract.paidAmount / activeContract.totalAmount) * 100 : 0}%`,
                      background: BLUE,
                    }} />
                  </div>
                </div>
              </div>
            )}
            {activeOrtho && !activeContract && (
              <div className="p-2.5 rounded-lg" style={{ background: "#faf5ff", border: "1px solid #e9d5ff" }}>
                <div className="text-[11px] font-bold" style={{ color: "#9333ea" }}>
                  حالة تقويم نشطة
                </div>
              </div>
            )}
          </div>
        )}

        {/* Recent Visits */}
        {summary?.recentVisits && summary.recentVisits.length > 0 && (
          <div>
            <div className="text-[11px] font-bold mb-1.5" style={{ color: NAVY }}>📅 زيارات سابقة</div>
            <div className="space-y-1">
              {summary.recentVisits.slice(0, 3).map((visit, i) => (
                <div key={i} className="px-2.5 py-1.5 rounded-lg text-[11px] flex items-center justify-between"
                  style={{ background: "#f8fafc", border: "1px solid #f1f5f9" }}>
                  <span className="font-medium" style={{ color: NAVY }}>
                    {visit.treatmentDone ?? visit.chiefComplaint ?? "زيارة"}
                  </span>
                  <span style={{ color: "#94a3b8" }}>{visit.visitDate ? fmtDate(visit.visitDate) : "—"}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Quick Actions (sticky bottom) */}
      <div className="flex-shrink-0 p-3 flex items-center gap-1.5 border-t"
        style={{ borderColor: "#e5e7eb", background: "#fafbfc" }}>
        <button onClick={() => onQuickPayment(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: "#22c55e15", color: "#22c55e", border: "1px solid #22c55e30" }}>
          <CreditCard className="w-3 h-3" />
          دفع
        </button>
        <button onClick={() => onBookAppointment(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: BLUE + "12", color: BLUE, border: `1px solid ${BLUE}30` }}>
          <Calendar className="w-3 h-3" />
          حجز
        </button>
        <button onClick={() => onCreateLabOrder(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: "#0891b214", color: "#0891b2", border: "1px solid #0891b230" }}>
          <ClipboardList className="w-3 h-3" />
          طلب معمل
        </button>
        {item.patientPhone && (
          <button onClick={() => onWhatsApp(item)}
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
            style={{ background: "#25D36612", color: "#25D366", border: "1px solid #25D36630" }}>
            <MessageCircle className="w-3 h-3" />
            واتساب
          </button>
        )}
        <div className="flex-1" />
        <button onClick={() => onViewPatient(item)}
          className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-[11px] font-bold transition hover:opacity-80"
          style={{ background: NAVY + "0d", color: NAVY, border: `1px solid ${NAVY}20` }}>
          فتح الملف
        </button>
      </div>
    </div>
  );
}
