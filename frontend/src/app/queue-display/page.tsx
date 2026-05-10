"use client";

import { useEffect, useState, useCallback } from "react";

interface QueueItem {
  id: string;
  patientDisplayName: string;
  appointmentType: string;
  startTime: string;
  endTime: string | null;
  doctorName: string | null;
  doctorColor: string | null;
  status: string;
}

const STATUS_CONFIG: Record<
  string,
  { label: string; rowClass: string; badgeClass: string; dot?: boolean }
> = {
  Scheduled: {
    label: "في الانتظار",
    rowClass: "opacity-70",
    badgeClass: "text-gray-400 bg-white/5",
  },
  Confirmed: {
    label: "في الانتظار",
    rowClass: "opacity-70",
    badgeClass: "text-gray-400 bg-white/5",
  },
  Arrived: {
    label: "في الانتظار",
    rowClass: "opacity-80",
    badgeClass: "text-blue-300 bg-blue-900/30",
  },
  InProgress: {
    label: "جارٍ الآن",
    rowClass:
      "border-l-4 border-teal-400 bg-teal-900/20 brightness-110",
    badgeClass: "text-teal-300 bg-teal-900/40",
    dot: true,
  },
  Completed: {
    label: "مكتمل",
    rowClass: "opacity-50",
    badgeClass: "text-green-400 bg-green-900/30",
  },
  NoShow: {
    label: "لم يحضر",
    rowClass: "opacity-40",
    badgeClass: "text-red-400 bg-red-900/30",
  },
};

function getStatusConfig(status: string) {
  return (
    STATUS_CONFIG[status] ?? {
      label: status,
      rowClass: "opacity-60",
      badgeClass: "text-gray-400 bg-white/5",
    }
  );
}

function formatClock(date: Date): string {
  return date.toLocaleTimeString("ar-SA", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

export default function QueueDisplayPage() {
  const [queue, setQueue] = useState<QueueItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [now, setNow] = useState<Date>(new Date());
  const [error, setError] = useState<string | null>(null);

  const fetchQueue = useCallback(async () => {
    try {
      const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
      const res = await fetch(`${apiBase}/api/public/queue`, {
        cache: "no-store",
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: QueueItem[] = await res.json();
      setQueue(data);
      setLastUpdated(new Date());
      setError(null);
    } catch (err) {
      setError("تعذّر تحميل البيانات");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial fetch + auto-refresh every 30 s
  useEffect(() => {
    fetchQueue();
    const refreshInterval = setInterval(fetchQueue, 30_000);
    return () => clearInterval(refreshInterval);
  }, [fetchQueue]);

  // Live clock every second
  useEffect(() => {
    const clockInterval = setInterval(() => setNow(new Date()), 1_000);
    return () => clearInterval(clockInterval);
  }, []);

  return (
    <div
      dir="rtl"
      className="min-h-screen bg-[#0F172A] text-white flex flex-col font-sans"
      style={{ fontFamily: "'Segoe UI', Tahoma, Arial, sans-serif" }}
    >
      {/* ── Header ─────────────────────────────────────────────── */}
      <header className="flex items-center justify-between px-10 py-6 border-b border-white/10">
        {/* Clinic icon + name */}
        <div className="flex items-center gap-4">
          <div className="w-14 h-14 rounded-full bg-[#0E7490] flex items-center justify-center text-2xl font-bold">
            ع
          </div>
          <div>
            <h1 className="text-3xl font-bold text-white leading-tight">
              مركز الدكتور عقلان الكامل
            </h1>
            <p className="text-lg text-teal-300 mt-0.5">
              قائمة المرضى اليوم
            </p>
          </div>
        </div>

        {/* Live clock */}
        <div className="text-right">
          <p className="text-5xl font-mono font-bold text-teal-300 tabular-nums">
            {formatClock(now)}
          </p>
          <p className="text-sm text-gray-400 mt-1">
            {now.toLocaleDateString("ar-SA", {
              weekday: "long",
              year: "numeric",
              month: "long",
              day: "numeric",
            })}
          </p>
        </div>
      </header>

      {/* ── Main content ───────────────────────────────────────── */}
      <main className="flex-1 px-10 py-8 overflow-auto">
        {loading ? (
          <div className="flex items-center justify-center h-64">
            <div className="text-3xl text-gray-400 animate-pulse">
              جاري التحميل…
            </div>
          </div>
        ) : error ? (
          <div className="flex items-center justify-center h-64">
            <div className="text-3xl text-red-400">{error}</div>
          </div>
        ) : queue.length === 0 ? (
          <div className="flex items-center justify-center h-64">
            <div className="text-center">
              <div className="text-6xl mb-4">📅</div>
              <p className="text-4xl text-gray-400">لا توجد مواعيد اليوم</p>
            </div>
          </div>
        ) : (
          <table className="w-full border-collapse">
            <thead>
              <tr className="text-teal-300 text-xl border-b-2 border-teal-800/50">
                <th className="py-4 px-4 text-center w-16">#</th>
                <th className="py-4 px-6 text-right">اسم المريض</th>
                <th className="py-4 px-6 text-right">نوع الزيارة</th>
                <th className="py-4 px-6 text-center">الوقت</th>
                <th className="py-4 px-6 text-center">الحالة</th>
              </tr>
            </thead>
            <tbody>
              {queue.map((item, index) => {
                const cfg = getStatusConfig(item.status);
                return (
                  <tr
                    key={item.id}
                    className={`border-b border-white/5 transition-all ${cfg.rowClass}`}
                  >
                    {/* Row number */}
                    <td className="py-5 px-4 text-center">
                      <span className="text-2xl font-bold text-gray-500">
                        {index + 1}
                      </span>
                    </td>

                    {/* Patient name */}
                    <td className="py-5 px-6">
                      <span className="text-3xl font-semibold">
                        {item.patientDisplayName}
                      </span>
                    </td>

                    {/* Appointment type */}
                    <td className="py-5 px-6">
                      <span className="text-2xl text-gray-200">
                        {item.appointmentType}
                      </span>
                    </td>

                    {/* Time */}
                    <td className="py-5 px-6 text-center">
                      <span className="text-2xl font-mono tabular-nums text-gray-200">
                        {item.startTime}
                      </span>
                    </td>

                    {/* Status badge */}
                    <td className="py-5 px-6 text-center">
                      <span
                        className={`inline-flex items-center gap-2 px-4 py-1.5 rounded-full text-xl font-medium ${cfg.badgeClass}`}
                      >
                        {cfg.dot && (
                          <span className="w-3 h-3 rounded-full bg-teal-400 animate-pulse inline-block" />
                        )}
                        {cfg.label}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </main>

      {/* ── Footer ─────────────────────────────────────────────── */}
      <footer className="border-t border-white/10 px-10 py-4 flex items-center justify-between text-gray-500 text-base">
        <span>
          {lastUpdated
            ? `آخر تحديث: ${formatClock(lastUpdated)}`
            : "جاري التحميل…"}
        </span>
        <span>
          يتحدث تلقائياً كل 30 ثانية — مركز الدكتور عقلان الكامل
        </span>
      </footer>
    </div>
  );
}
