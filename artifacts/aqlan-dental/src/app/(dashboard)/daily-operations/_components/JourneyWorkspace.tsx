/**
 * DailyOperationsJourneyWorkspace — Journey view with the queue list on the
 * left and the PatientDetailPanel on the right.
 *
 * Extracted from daily-operations/page.tsx (Sprint 11B) without behavior
 * changes. RTL Arabic. Frontend-only.
 */


import { RefreshCw, Activity } from "lucide-react";

import type { DailyJourneySummary } from "@/types/journey";

import {
  NAVY, BLUE,
  fmtDate,
  computeDayStats,
  STATUS_COLORS, APPT_STATUS_LABELS, ACTION_LABELS,
  type TodayJourneyItem,
} from "../_lib/constants";

import { PatientDetailPanel } from "./PatientDetailPanel";

export function DailyOperationsJourneyWorkspace({
  items,
  loading,
  selectedItem,
  summary,
  waitTime,
  dayStats,
  filterDate,
  onRefresh,
  onSelect,
  onClearSelected,
  onContextMenu,
  onQuickPayment,
  onCreateLabOrder,
  onBookAppointment,
  onWhatsApp,
  onViewPatient,
  medicalAlerts,
  finance,
  activeContract,
  activeOrtho,
}: {
  items: TodayJourneyItem[];
  loading: boolean;
  selectedItem: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
  dayStats: ReturnType<typeof computeDayStats>;
  filterDate: string;
  onRefresh: () => void;
  onSelect: (item: TodayJourneyItem) => void;
  onClearSelected: () => void;
  onContextMenu: (e: React.MouseEvent, item: TodayJourneyItem) => void;
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
  return (
    <div className="flex-1 min-w-0 flex overflow-hidden">
      <aside className="w-[360px] max-w-[42vw] flex-shrink-0 bg-white border-l border-[#e5edf5] flex flex-col">
        <div className="p-3 border-b border-[#edf2f7] flex items-center justify-between gap-2">
          <div>
            <h3 className="text-[13px] font-extrabold" style={{ color: NAVY }}>رحلة المرضى اليومية</h3>
            <p className="text-[10px] text-gray-500 mt-0.5">من الوصول إلى الحساب والخروج داخل التشغيل اليومي</p>
          </div>
          <button
            type="button"
            onClick={onRefresh}
            className="h-8 px-2.5 rounded-lg border border-[#dbe6f1] bg-white text-[10px] font-bold text-[#48627d] flex items-center gap-1 hover:bg-[#f7fbff]"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            تحديث
          </button>
        </div>

        <div className="grid grid-cols-3 gap-2 p-3 border-b border-[#edf2f7]">
          <div className="rounded-xl bg-blue-50 px-2 py-2 text-center">
            <p className="text-[15px] font-extrabold text-[#1a3a5c]">{items.length}</p>
            <p className="text-[9px] font-bold text-[#6b7f96]">رحلة</p>
          </div>
          <div className="rounded-xl bg-amber-50 px-2 py-2 text-center">
            <p className="text-[15px] font-extrabold text-[#b45309]">{dayStats.waiting}</p>
            <p className="text-[9px] font-bold text-[#92400e]">انتظار</p>
          </div>
          <div className="rounded-xl bg-emerald-50 px-2 py-2 text-center">
            <p className="text-[15px] font-extrabold text-[#047857]">{dayStats.completed}</p>
            <p className="text-[9px] font-bold text-[#047857]">مكتمل</p>
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          {loading ? (
            <div className="h-full min-h-[220px] flex items-center justify-center text-xs font-bold text-gray-400">
              جار تحميل رحلة المرضى...
            </div>
          ) : items.length === 0 ? (
            <div className="h-full min-h-[220px] flex flex-col items-center justify-center text-center px-6">
              <Activity className="w-10 h-10 text-gray-200 mb-3" />
              <p className="text-sm font-bold text-gray-500">لا توجد رحلات مطابقة</p>
              <p className="text-[11px] text-gray-400 mt-1">غيّر البحث أو الفلاتر لعرض المرضى</p>
            </div>
          ) : (
            items.map((item) => {
              const isSelected = selectedItem?.patientId === item.patientId;
              const statusColor = STATUS_COLORS[item.appointmentStatus] ?? STATUS_COLORS.Scheduled;
              const initials = item.patientName.split(" ").filter(Boolean).slice(0, 2).map(part => part[0]).join("") || "P";
              return (
                <button
                  key={`${item.appointmentId ?? item.visitId ?? item.patientId}-journey`}
                  type="button"
                  onClick={() => onSelect(item)}
                  onContextMenu={(event) => onContextMenu(event, item)}
                  className="w-full px-3 py-3 text-right border-b border-[#f0f3f7] hover:bg-[#f8fbff] transition"
                  style={{
                    background: isSelected ? "#eef6ff" : "#fff",
                    borderRight: isSelected ? `3px solid ${BLUE}` : "3px solid transparent",
                  }}
                >
                  <div className="flex items-center gap-2.5">
                    <div className="w-9 h-9 rounded-xl flex items-center justify-center text-[11px] font-extrabold text-white flex-shrink-0" style={{ background: statusColor.text }}>
                      {initials}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5">
                        <p className="text-[12px] font-extrabold truncate" style={{ color: NAVY }}>{item.patientName}</p>
                        <span className="text-[9px] px-1.5 py-0.5 rounded-full font-bold flex-shrink-0" style={{ background: statusColor.bg, color: statusColor.text }}>
                          {APPT_STATUS_LABELS[item.appointmentStatus] ?? item.appointmentStatus}
                        </span>
                      </div>
                      <p className="text-[10px] text-gray-500 truncate mt-0.5">
                        {item.doctorName} · {item.serviceName ?? "بدون خدمة"} · {item.appointmentTime ?? fmtDate(filterDate)}
                      </p>
                      <p className="text-[10px] font-semibold mt-1" style={{ color: BLUE }}>
                        التالي: {ACTION_LABELS[item.nextAction] ?? item.nextAction}
                      </p>
                    </div>
                  </div>
                </button>
              );
            })
          )}
        </div>
      </aside>

      <section className="flex-1 min-w-0 overflow-auto bg-white">
        {selectedItem ? (
          <PatientDetailPanel
            item={selectedItem}
            summary={summary}
            waitTime={waitTime}
            onClose={onClearSelected}
            onQuickPayment={onQuickPayment}
            onCreateLabOrder={onCreateLabOrder}
            onBookAppointment={onBookAppointment}
            onWhatsApp={onWhatsApp}
            onViewPatient={onViewPatient}
            medicalAlerts={medicalAlerts}
            finance={finance}
            activeContract={activeContract}
            activeOrtho={activeOrtho}
          />
        ) : (
          <div className="h-full min-h-[420px] flex flex-col items-center justify-center text-center px-8">
            <div className="w-16 h-16 rounded-2xl flex items-center justify-center mb-4" style={{ background: `${NAVY}10` }}>
              <Activity className="w-8 h-8" style={{ color: NAVY }} />
            </div>
            <h2 className="text-lg font-extrabold mb-2" style={{ color: NAVY }}>اختر مريضاً من القائمة</h2>
            <p className="text-sm text-gray-500 max-w-md leading-7">
              ستظهر هنا تفاصيل رحلة المريض، حالته الحالية، التنبيهات الطبية، الرصيد، والإجراءات السريعة للتشغيل اليومي.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
