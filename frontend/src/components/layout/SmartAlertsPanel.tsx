"use client";
import { useState, useRef, useEffect } from "react";
import { Bell, AlertTriangle, Clock, Calendar, Info, CheckCheck, X } from "lucide-react";
import { useSmartAlerts, useMarkAllNotificationsRead, useMarkNotificationRead, useNotifications } from "@/hooks/useAiAdvisory";
import type { SmartAlert } from "@/types/ai";

const ALERT_ICONS: Record<string, typeof AlertTriangle> = {
  overdue_payment: AlertTriangle,
  delayed_ortho_case: Clock,
  today_appointment: Calendar,
  unconfirmed_appointment: Info,
};

const SEVERITY_STYLES: Record<string, string> = {
  info: "bg-[#f0f5fb] border-[#dce8f5]",
  warning: "bg-amber-50 border-amber-200",
  error: "bg-red-50 border-red-200",
};

export function SmartAlertsPanel() {
  const [open, setOpen] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const { data: alerts = [], isLoading: alertsLoading } = useSmartAlerts();
  const { data: notifData } = useNotifications(true);
  const markAllRead = useMarkAllNotificationsRead();
  const markRead = useMarkNotificationRead();
  const unreadCount = notifData?.unreadCount ?? alerts.length;

  // Close on click outside
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) setOpen(false);
    }
    if (open) document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [open]);

  return (
    <div className="relative" ref={panelRef}>
      <button
        onClick={() => setOpen(!open)}
        className="relative w-[38px] h-[38px] rounded-lg bg-[#f0f5fb] hover:bg-[#e3edf7] flex items-center justify-center text-[#0d2137]/60 transition-colors"
      >
        <Bell className="w-[18px] h-[18px]" />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -end-1 min-w-[18px] h-[18px] flex items-center justify-center bg-[#ef4444] text-white text-[10px] font-extrabold rounded-full px-1">
            {unreadCount > 9 ? "9+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute end-0 top-full mt-2 w-96 max-h-[500px] overflow-y-auto bg-white rounded-xl shadow-xl border border-[#e8f0f9] z-50">
          <div className="sticky top-0 bg-white px-4 py-3 border-b border-[#e8f0f9] flex items-center justify-between">
            <h3 className="text-sm font-bold text-[#0d2137]">التنبيهات والإشعارات</h3>
            {unreadCount > 0 && (
              <button
                onClick={() => markAllRead.mutate()}
                className="text-xs text-[#3d7ab5] hover:underline flex items-center gap-1"
              >
                <CheckCheck className="w-3.5 h-3.5" />
                قراءة الكل
              </button>
            )}
          </div>

          {alertsLoading ? (
            <div className="p-6 text-center text-sm text-[#94a3b8]">جاري التحميل...</div>
          ) : alerts.length === 0 ? (
            <div className="p-6 text-center text-sm text-[#94a3b8]">
              <Bell className="w-8 h-8 mx-auto mb-2 opacity-30" />
              لا توجد تنبيهات جديدة
            </div>
          ) : (
            <div className="divide-y divide-gray-50">
              {alerts.map((alert: SmartAlert, i: number) => {
                const Icon = ALERT_ICONS[alert.type] ?? Info;
                return (
                  <div
                    key={`${alert.type}-${i}`}
                    className={`px-4 py-3 border-s-4 ${SEVERITY_STYLES[alert.severity] ?? ""}`}
                  >
                    <div className="flex items-start gap-3">
                      <Icon className="w-4 h-4 mt-0.5 flex-shrink-0 text-[#0d2137]/50" />
                      <div className="flex-1 min-w-0">
                        <p className="text-xs font-semibold text-[#0d2137]">{alert.title}</p>
                        <p className="text-xs text-[#94a3b8] mt-0.5 leading-relaxed">{alert.message}</p>
                      </div>
                      <button
                        onClick={() => setOpen(false)}
                        className="text-gray-300 hover:text-gray-500 flex-shrink-0"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {notifData?.notifications && notifData.notifications.length > 0 && (
            <div className="border-t border-[#e8f0f9]">
              <div className="px-4 py-2 text-xs font-semibold text-[#94a3b8] bg-[#f7fafd]">الإشعارات</div>
              {notifData.notifications.slice(0, 5).map((n) => (
                <div
                  key={n.id}
                  className={`px-4 py-2.5 border-b border-gray-50 cursor-pointer hover:bg-[#f7fafd] transition-colors ${!n.isRead ? "bg-[#3d7ab5]/5" : ""}`}
                  onClick={() => { markRead.mutate(n.id); }}
                >
                  <div className="flex items-start gap-2">
                    {!n.isRead && <span className="w-1.5 h-1.5 rounded-full bg-[#3d7ab5] mt-1.5 flex-shrink-0" />}
                    <div className="flex-1 min-w-0">
                      <p className="text-xs font-medium text-[#0d2137]">{n.title}</p>
                      {n.body && <p className="text-xs text-[#94a3b8] mt-0.5">{n.body}</p>}
                      <p className="text-[10px] text-[#94a3b8]/60 mt-0.5">{n.createdAt}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
