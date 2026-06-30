/**
 * PatientSidePanel — right-docked patient detail panel (medical alerts,
 * finance snapshot, active contract, active ortho case, today's ortho visit
 * fields, queue wait time, recent visits).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import {
  Clock, ChevronLeft, AlertCircle, Wallet, FileText, Stethoscope,
} from "lucide-react";
import {
  fmtRial, fmtDate, fmtWaitMinutes, NAVY, BLUE, ORANGE,
} from "../../_lib/constants";
import type { TodayJourneyItem } from "../../_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";

export function PatientSidePanel({
  open, onClose, item, summary, waitTime,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
}) {
  if (!open || !item) return null;

  const finance = summary?.financeSummary;
  const medicalAlerts = summary?.medicalAlerts ?? [];
  const activeContract = summary?.activeContract;
  const activeOrtho = summary?.activeOrthoCase;

  return (
    <div className="fixed inset-0 z-50 flex justify-end" onClick={onClose}>
      {/* Overlay */}
      <div className="flex-1 bg-black/20" />
      {/* Panel */}
      <div className="w-full max-w-md bg-white shadow-2xl overflow-y-auto"
        onClick={e => e.stopPropagation()} style={{ borderRight: "none" }}>
        {/* Header */}
        <div className="sticky top-0 bg-white z-10 px-5 py-4 border-b flex items-center gap-3"
          style={{ borderColor: "#e8f0f9" }}>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100">
            <ChevronLeft className="w-5 h-5" style={{ color: NAVY }} />
          </button>
          <div className="flex-1">
            <h3 className="font-extrabold text-sm" style={{ color: NAVY }}>{item.patientName}</h3>
            <p className="text-[11px]" style={{ color: "#64748b" }}>
              {item.doctorName} — {item.serviceName ?? "—"}
            </p>
          </div>
        </div>

        <div className="p-5 space-y-4">
          {/* Medical Alerts */}
          {medicalAlerts.length > 0 && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: "#ef4444" }}>
                <AlertCircle className="w-3.5 h-3.5" /> تنبيهات طبية
              </h4>
              <div className="space-y-1.5">
                {medicalAlerts.map((alert, i) => (
                  <div key={i} className="px-3 py-2 rounded-lg text-xs font-medium"
                    style={{
                      background: alert.severity === "danger" ? "#fef2f2" : alert.severity === "warning" ? "#fff7ed" : "#f0f5fb",
                      color: alert.severity === "danger" ? "#dc2626" : alert.severity === "warning" ? "#d97706" : "#3d7ab5",
                      border: `1px solid ${alert.severity === "danger" ? "#fecaca" : alert.severity === "warning" ? "#fde8d0" : "#dce8f5"}`,
                    }}>
                    {alert.label}: {alert.value}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Finance Summary */}
          {finance && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Wallet className="w-3.5 h-3.5" /> الوضع المالي
              </h4>
              <div className="grid grid-cols-2 gap-2">
                <div className="p-2.5 rounded-lg" style={{ background: "#fff7ed" }}>
                  <div className="text-[10px] font-medium" style={{ color: ORANGE }}>المستحق</div>
                  <div className="text-sm font-bold" style={{ color: NAVY }}>{fmtRial(finance.outstandingBalance)}</div>
                </div>
                <div className="p-2.5 rounded-lg" style={{ background: finance.overdueAmount > 0 ? "#fef2f2" : "#f0fdf4" }}>
                  <div className="text-[10px] font-medium" style={{ color: finance.overdueAmount > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
                  <div className="text-sm font-bold" style={{ color: NAVY }}>{fmtRial(finance.overdueAmount)}</div>
                </div>
                {finance.totalPaid != null && (
                  <div className="p-2.5 rounded-lg" style={{ background: "#f0fdf4" }}>
                    <div className="text-[10px] font-medium" style={{ color: "#16a34a" }}>المدفوع</div>
                    <div className="text-sm font-bold" style={{ color: NAVY }}>{fmtRial(finance.totalPaid)}</div>
                  </div>
                )}
                <div className="p-2.5 rounded-lg" style={{ background: "#f5f5f5" }}>
                  <div className="text-[10px] font-medium" style={{ color: "#64748b" }}>الحالة</div>
                  <div className="text-xs font-bold" style={{ color: NAVY }}>
                    {finance.financialStatus === "paid_full" ? "مكتمل" :
                     finance.financialStatus === "has_balance" ? "عليه رصيد" :
                     finance.financialStatus === "overdue" ? "متأخر" : "لا خطة"}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Active Contract */}
          {activeContract && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <FileText className="w-3.5 h-3.5" /> عقد نشط
              </h4>
              <div className="p-3 rounded-lg" style={{ background: "#f0f5fb", border: "1px solid #dce8f5" }}>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs font-bold" style={{ color: NAVY }}>
                    {activeContract.specialty ?? "عقد"} — {activeContract.status}
                  </span>
                  <span className="text-[10px] font-bold" style={{ color: BLUE }}>
                    {activeContract.installmentsCount} قسط
                  </span>
                </div>
                <div className="w-full rounded-full h-1.5 bg-gray-200 mt-1.5">
                  <div className="h-1.5 rounded-full" style={{
                    width: `${Math.min(100, (activeContract.paidAmount / activeContract.totalAmount) * 100)}%`,
                    background: BLUE,
                  }} />
                </div>
                <div className="flex justify-between mt-1.5 text-[10px]">
                  <span style={{ color: "#16a34a" }}>مدفوع: {fmtRial(activeContract.paidAmount)}</span>
                  <span style={{ color: ORANGE }}>متبقي: {fmtRial(activeContract.remainingAmount)}</span>
                </div>
              </div>
            </div>
          )}

          {/* Active Ortho Case */}
          {activeOrtho && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Stethoscope className="w-3.5 h-3.5" /> حالة تقويم
              </h4>
              <div className="p-3 rounded-lg" style={{ background: "#faf5ff", border: "1px solid #e9d5ff" }}>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs font-bold" style={{ color: "#9333ea" }}>
                    {activeOrtho.applianceType ?? "تقويم"} — {activeOrtho.status}
                  </span>
                  <span className="text-[10px] font-bold" style={{ color: "#9333ea" }}>
                    {activeOrtho.stagePercentage ?? 0}%
                  </span>
                </div>
                <div className="w-full rounded-full h-1.5 bg-gray-200 mt-1.5">
                  <div className="h-1.5 rounded-full" style={{
                    width: `${activeOrtho.stagePercentage ?? 0}%`,
                    background: "#9333ea",
                  }} />
                </div>
              </div>
            </div>
          )}

          {/* CLIN-05: Today's ortho visit fields — shown when the linked Visit
              carries today's ortho clinical data (wire info / current stage). */}
          {(item.orthoVisitWireUpper || item.orthoVisitWireLower || item.orthoVisitCurrentStage) && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Stethoscope className="w-3.5 h-3.5" /> إجراءات اليوم التقويمية
              </h4>
              <div className="p-3 rounded-lg space-y-1" style={{ background: "#eef2ff", border: "1px solid #c7d2fe" }}>
                {item.orthoVisitWireUpper && (
                  <div className="text-xs" style={{ color: "#4338ca" }}>
                    <strong>السلك العلوي:</strong> {item.orthoVisitWireUpper}
                  </div>
                )}
                {item.orthoVisitWireLower && (
                  <div className="text-xs" style={{ color: "#4338ca" }}>
                    <strong>السلك السفلي:</strong> {item.orthoVisitWireLower}
                  </div>
                )}
                {item.orthoVisitCurrentStage && (
                  <div className="text-xs" style={{ color: "#4338ca" }}>
                    <strong>المرحلة الحالية:</strong> {item.orthoVisitCurrentStage}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Queue Wait Time */}
          {waitTime && waitTime.estimatedMinutes > 0 && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Clock className="w-3.5 h-3.5" /> وقت الانتظار المتوقع
              </h4>
              <div className="p-3 rounded-lg flex items-center gap-3"
                style={{ background: "#fff7ed", border: "1px solid #fde8d0" }}>
                <Clock className="w-5 h-5" style={{ color: ORANGE }} />
                <div>
                  <div className="text-sm font-bold" style={{ color: NAVY }}>
                    {fmtWaitMinutes(waitTime.estimatedMinutes)}
                  </div>
                  <div className="text-[10px]" style={{ color: "#94a3b8" }}>
                    {waitTime.patientsAhead} مرضى قبله
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Recent Visits */}
          {summary?.recentVisits && summary.recentVisits.length > 0 && (
            <div>
              <h4 className="text-xs font-bold mb-2" style={{ color: NAVY }}>آخر الزيارات</h4>
              <div className="space-y-1.5">
                {summary.recentVisits.slice(0, 3).map((v, i) => (
                  <div key={i} className="px-3 py-2 rounded-lg text-xs"
                    style={{ background: "#f8fafc", border: "1px solid #f1f5f9" }}>
                    <div className="font-semibold" style={{ color: NAVY }}>
                      {v.treatmentDone || v.chiefComplaint || "زيارة"}
                    </div>
                    <div className="text-[10px] mt-0.5" style={{ color: "#94a3b8" }}>
                      {fmtDate(v.visitDate)} {v.cost ? `— ${fmtRial(v.cost)}` : ""}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
