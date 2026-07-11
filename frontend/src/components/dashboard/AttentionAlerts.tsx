"use client";
import { useEffect } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  AlertTriangle,
  BellRing,
  CalendarClock,
  CheckCircle2,
  FlaskConical,
  Hourglass,
  RefreshCw,
  UserX,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";

interface DashboardAlerts {
  overdueLabOrdersCount: number;
  maxLabDaysOverdue: number;
  todayNoShowCount: number;
  longWaitingCount: number;
  unconfirmedTomorrowCount: number;
  recallCandidatesCount: number;
}

interface AlertItem {
  key: string;
  label: string;
  count: number;
  color: string;
  href: string;
  icon: LucideIcon;
}

/* ZIP-matched card style (same as dashboard cards) */
const cardBaseStyle: React.CSSProperties = {
  background: "#fff",
  borderRadius: 12,
  boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
};

function buildAlerts(data: DashboardAlerts): AlertItem[] {
  return [
    {
      key: "lab-overdue",
      label: `تراكيب متأخرة في المختبر (${data.overdueLabOrdersCount}) — أقدمها متأخر ${data.maxLabDaysOverdue} يوم`,
      count: data.overdueLabOrdersCount,
      color: "#ef4444", // red
      href: "/lab/overdue",
      icon: FlaskConical,
    },
    {
      key: "long-waiting",
      label: `مرضى انتظروا أكثر من 30 دقيقة (${data.longWaitingCount})`,
      count: data.longWaitingCount,
      color: "#f5922e", // orange
      href: "/daily-operations",
      icon: Hourglass,
    },
    {
      key: "no-show",
      label: `غياب اليوم (${data.todayNoShowCount})`,
      count: data.todayNoShowCount,
      color: "#f59e0b", // amber
      href: "/daily-operations",
      icon: UserX,
    },
    {
      key: "unconfirmed-tomorrow",
      label: `مواعيد الغد غير مؤكدة (${data.unconfirmedTomorrowCount})`,
      count: data.unconfirmedTomorrowCount,
      color: "#3d7ab5", // blue
      href: "/appointments",
      icon: CalendarClock,
    },
    {
      key: "recall",
      label: `مرضى بحاجة لإعادة استدعاء (${data.recallCandidatesCount})`,
      count: data.recallCandidatesCount,
      color: "#a855f7", // purple
      href: "/appointments/recall",
      icon: BellRing,
    },
  ];
}

export function AttentionAlerts() {
  const { data, isLoading, isError, error, isFetching, refetch } = useQuery({
    queryKey: ["dashboard-alerts"],
    queryFn: async () =>
      (await api.get<DashboardAlerts>("/api/dashboard/alerts")).data,
    refetchInterval: 120_000,
  });

  // Silent fail: render nothing on error, but log a warning
  useEffect(() => {
    if (isError) {
      console.warn("فشل تحميل تنبيهات لوحة التحكم (dashboard alerts):", error);
    }
  }, [isError, error]);

  if (isLoading) {
    return (
      <div className="flex flex-wrap gap-3 animate-pulse">
        {[0, 1, 2].map((i) => (
          <div
            key={i}
            className="h-[52px] flex-1 min-w-[220px] rounded-xl"
            style={{ background: "#eef3f9", border: "1px solid #e8f0f9" }}
          />
        ))}
      </div>
    );
  }

  const errorState = isError ? (
    <div
      role="alert"
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800"
    >
      <div className="flex min-w-0 items-center gap-2">
        <AlertTriangle className="h-4 w-4 flex-shrink-0" />
        <span className="font-semibold">
          {extractErrorMessage(error, "تعذر تحميل تنبيهات لوحة التحكم")}
        </span>
      </div>
      <button
        type="button"
        onClick={() => void refetch()}
        disabled={isFetching}
        className="inline-flex items-center gap-1.5 rounded-md border border-red-300 px-3 py-1.5 font-semibold text-red-700 transition hover:bg-red-100 disabled:cursor-wait disabled:opacity-60"
      >
        <RefreshCw className={`h-4 w-4 ${isFetching ? "animate-spin" : ""}`} />
        إعادة المحاولة
      </button>
    </div>
  ) : null;

  if (!data) return errorState;

  const alerts = buildAlerts(data).filter((a) => a.count > 0);

  if (alerts.length === 0) {
    if (errorState) return errorState;

    return (
      <div
        className="flex items-center gap-2 rounded-xl px-4 py-2.5 text-[13px] font-semibold"
        style={{
          ...cardBaseStyle,
          background: "#22c55e0d",
          border: "1px solid #22c55e30",
          color: "#16a34a",
        }}
       
      >
        <CheckCircle2 className="h-4 w-4 flex-shrink-0" />
        لا توجد تنبيهات — كل شيء تحت السيطرة ✅
      </div>
    );
  }

  return (
    <section aria-label="يحتاج انتباهك">
      {errorState && <div className="mb-3">{errorState}</div>}
      <div className="mb-2 flex items-center gap-2">
        <AlertTriangle className="h-4 w-4" style={{ color: "#f5922e" }} />
        <h2 className="text-sm font-extrabold" style={{ color: "#0d2137" }}>
          يحتاج انتباهك
        </h2>
      </div>
      <div className="flex flex-wrap gap-3">
        {alerts.map((alert) => (
          <Link
            key={alert.key}
            href={alert.href}
            className="flex flex-1 min-w-[220px] items-center gap-3 rounded-xl px-4 py-3 transition hover:shadow-md"
            style={{
              ...cardBaseStyle,
              background: alert.color + "0d",
              border: `1.5px solid ${alert.color}30`,
            }}
          >
            <div
              className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg"
              style={{ background: alert.color + "18" }}
            >
              <alert.icon className="h-4 w-4" style={{ color: alert.color }} />
            </div>
            <span
              className="text-[13px] font-bold leading-snug"
              style={{ color: alert.color }}
            >
              {alert.label}
            </span>
          </Link>
        ))}
      </div>
    </section>
  );
}

export default AttentionAlerts;
