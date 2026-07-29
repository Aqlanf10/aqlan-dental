import { useEffect, useState, useCallback } from "react";
import {
  MessageSquare,
  Send,
  RefreshCw,
  Clock,
  CheckCircle2,
  XCircle,
  Activity,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import {
  type SmsDashboardDto,
  STATUS_MAP,
  formatRelativeTime,
} from "./types";

// ─── Dashboard Tab ────────────────────────────────────────────────────────────

export function DashboardTab() {
  const [dashboard, setDashboard] = useState<SmsDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const fetchDashboard = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const { data } = await api.get<SmsDashboardDto>("/api/sms/dashboard");
      setDashboard(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
        <XCircle className="w-12 h-12 text-red-400" />
        <p className="text-gray-600 text-sm">تعذّر تحميل لوحة التحكم</p>
        <button
          onClick={fetchDashboard}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <RefreshCw className="w-4 h-4" />
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-24 bg-gray-100 rounded-xl" />
          ))}
        </div>
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  const stats = [
    {
      label: "رسائل اليوم",
      value: dashboard?.sentToday ?? 0,
      color: "text-blue-700 bg-blue-50",
      icon: Send,
    },
    {
      label: "المرسلة هذا الشهر",
      value: dashboard?.sentThisMonth ?? 0,
      color: "text-green-700 bg-green-50",
      icon: CheckCircle2,
    },
    {
      label: "الفاشلة",
      value: dashboard?.failedToday ?? 0,
      color: "text-red-700 bg-red-50",
      icon: XCircle,
    },
    {
      label: "في الانتظار",
      value: dashboard?.pendingCount ?? 0,
      color: "text-amber-700 bg-amber-50",
      icon: Clock,
    },
    {
      label: "معدل التوصيل",
      value: `${(dashboard?.deliveryRate ?? 0).toFixed(1)}%`,
      color: "text-emerald-700 bg-emerald-50",
      icon: Activity,
    },
  ];

  return (
    <div className="space-y-5">
      {/* Stats Cards */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <div key={stat.label} className={cn("rounded-xl p-4", stat.color)}>
              <div className="flex items-center justify-between mb-2">
                <Icon className="w-5 h-5 opacity-70" />
              </div>
              <p className="text-2xl font-bold">{stat.value}</p>
              <p className="text-xs mt-1 opacity-80">{stat.label}</p>
            </div>
          );
        })}
      </div>

      {/* Gateway Status */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm px-5 py-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div
            className={cn(
              "w-3 h-3 rounded-full",
              dashboard?.isGatewayConnected ? "bg-green-500" : "bg-red-500"
            )}
          />
          <span className="text-sm font-medium text-gray-700">
            حالة بوابة الرسائل:
          </span>
          <span
            className={cn(
              "text-sm font-bold",
              dashboard?.isGatewayConnected ? "text-green-700" : "text-red-700"
            )}
          >
            {dashboard?.isGatewayConnected ? "متصل" : "غير متصل"}
          </span>
        </div>
        {dashboard?.senderName && (
          <span className="text-xs text-gray-500">
            المرسل: <span className="font-mono font-medium" dir="ltr">{dashboard.senderName}</span>
          </span>
        )}
        {dashboard && (
          <span className="text-xs text-gray-500">
            الحد اليومي: {dashboard.sentToday}/{dashboard.dailyLimit}
          </span>
        )}
      </div>

      {/* Recent Messages Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر الرسائل</h2>
          <span className="text-xs text-gray-400">
            {dashboard?.recentMessages?.length ?? 0} رسالة
          </span>
        </div>
        {!dashboard?.recentMessages || dashboard.recentMessages.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <MessageSquare className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد رسائل بعد</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["المريض", "الرقم", "الرسالة", "الحالة", "الوقت"].map(
                    (h) => (
                      <th
                        key={h}
                        className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                      >
                        {h}
                      </th>
                    )
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {dashboard.recentMessages.map((msg) => {
                  const statusInfo = STATUS_MAP[msg.status] || STATUS_MAP.pending;
                  const StatusIcon = statusInfo.icon;
                  return (
                    <tr key={msg.id} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">
                        {msg.patientName || "—"}
                      </td>
                      <td className="px-4 py-3 text-gray-500 font-mono text-xs" dir="ltr">
                        {msg.phoneNumber}
                      </td>
                      <td className="px-4 py-3 text-gray-600 max-w-[200px]">
                        <p className="truncate">{msg.messageContent}</p>
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1",
                            statusInfo.color
                          )}
                        >
                          <StatusIcon className="w-2.5 h-2.5" />
                          {statusInfo.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs whitespace-nowrap">
                        {formatRelativeTime(msg.createdAt)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
