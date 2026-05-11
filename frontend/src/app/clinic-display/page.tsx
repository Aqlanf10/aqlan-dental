"use client";

import { useEffect, useState, useCallback } from "react";
import {
  Users,
  Volume2,
  MapPin,
  Stethoscope,
  CheckCircle2,
  PhoneCall,
} from "lucide-react";

/* ─── Types ────────────────────────────────────────────────────────────────── */
interface DisplayData {
  latestCalled: {
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    calledAt: string;
  } | null;
  waitingCount: number;
  waitingList: {
    patientNumber: string;
    patientName: string;
    doctorName: string;
    status: string;
  }[];
  recentlyCalled: {
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    statusArabic: string;
    status: string;
    calledAt: string;
  }[];
}

/* ─── Constants ────────────────────────────────────────────────────────────── */
const STATUS_DISPLAY: Record<string, { label: string; color: string; bg: string; dotColor: string }> = {
  Waiting:    { label: "في الانتظار",  color: "text-amber-300",  bg: "bg-amber-900/30",  dotColor: "bg-amber-400" },
  Called:     { label: "تم النداء",    color: "text-blue-300",   bg: "bg-blue-900/30",   dotColor: "bg-blue-400" },
  InRoom:     { label: "داخل الغرفة",  color: "text-purple-300", bg: "bg-purple-900/30", dotColor: "bg-purple-400" },
  InProgress: { label: "قيد المعالجة", color: "text-teal-300",   bg: "bg-teal-900/30",   dotColor: "bg-teal-400" },
  Completed:  { label: "مكتمل",        color: "text-gray-400",   bg: "bg-gray-800/30",   dotColor: "bg-gray-500" },
  Cancelled:  { label: "ملغى",         color: "text-red-400",    bg: "bg-red-900/30",    dotColor: "bg-red-400" },
};

/* ─── Helpers ──────────────────────────────────────────────────────────────── */
function formatClock(date: Date): string {
  return date.toLocaleTimeString("ar-SA", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

function formatTimeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "الآن";
  if (mins < 60) return `منذ ${mins} دقيقة`;
  const hours = Math.floor(mins / 60);
  return `منذ ${hours} ساعة`;
}

function getStatusDisplay(status: string) {
  return STATUS_DISPLAY[status] ?? { label: status, color: "text-gray-400", bg: "bg-gray-800/30", dotColor: "bg-gray-500" };
}

/* ─── Main Page ────────────────────────────────────────────────────────────── */
export default function ClinicDisplayPage() {
  const [data, setData] = useState<DisplayData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [now, setNow] = useState<Date>(new Date());
  const [pulseKey, setPulseKey] = useState(0);

  const fetchDisplay = useCallback(async () => {
    try {
      const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
      const res = await fetch(`${apiBase}/api/clinic-queue/display`, {
        cache: "no-store",
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: DisplayData = await res.json();
      setData(json);
      setLastUpdated(new Date());
      setError(null);
      // Trigger pulse animation when latest called changes
      setPulseKey((k) => k + 1);
    } catch (err) {
      setError("تعذّر تحميل البيانات");
      console.error(err);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial fetch + auto-refresh every 30 s
  useEffect(() => {
    fetchDisplay();
    const refreshInterval = setInterval(fetchDisplay, 30_000);
    return () => clearInterval(refreshInterval);
  }, [fetchDisplay]);

  // Live clock every second
  useEffect(() => {
    const clockInterval = setInterval(() => setNow(new Date()), 1_000);
    return () => clearInterval(clockInterval);
  }, []);

  return (
    <div
      dir="rtl"
      className="min-h-screen bg-[#0F172A] text-white flex flex-col"
      style={{ fontFamily: "'Segoe UI', Tahoma, Arial, sans-serif" }}
    >
      {/* ── Header ─────────────────────────────────────────────── */}
      <header className="flex items-center justify-between px-8 md:px-12 py-5 border-b border-white/10">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-full bg-[#0E7490] flex items-center justify-center text-xl font-bold">
            ع
          </div>
          <div>
            <h1 className="text-2xl md:text-3xl font-bold text-white leading-tight">
              مركز الدكتور عقلان الكامل
            </h1>
            <p className="text-base md:text-lg text-teal-300 mt-0.5">
              شاشة الطابور
            </p>
          </div>
        </div>

        {/* Live clock */}
        <div className="text-left">
          <p className="text-4xl md:text-5xl font-mono font-bold text-teal-300 tabular-nums">
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
      <main className="flex-1 px-8 md:px-12 py-6 overflow-auto">
        {loading && !data ? (
          <div className="flex items-center justify-center h-64">
            <div className="text-3xl text-gray-400 animate-pulse">
              جاري التحميل…
            </div>
          </div>
        ) : error && !data ? (
          <div className="flex items-center justify-center h-64">
            <div className="text-3xl text-red-400">{error}</div>
          </div>
        ) : data ? (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 h-full">
            {/* ── Left Column: Latest Called + Waiting Count ── */}
            <div className="lg:col-span-1 space-y-6">
              {/* Latest Called — Prominent */}
              {data.latestCalled ? (
                <div
                  key={pulseKey}
                  className="rounded-2xl bg-gradient-to-br from-teal-900/60 to-cyan-900/40 border border-teal-700/40 p-6 md:p-8 animate-[fadeIn_0.5s_ease]"
                >
                  <div className="flex items-center gap-2 mb-4">
                    <Volume2 className="w-6 h-6 text-teal-300 animate-pulse" />
                    <span className="text-lg font-bold text-teal-300">النداء الأخير</span>
                  </div>
                  <div className="text-center space-y-3">
                    <div className="text-5xl md:text-6xl font-extrabold text-white leading-tight">
                      {data.latestCalled.patientName}
                    </div>
                    <div className="text-xl md:text-2xl text-teal-200 font-mono">
                      رقم الملف: {data.latestCalled.patientNumber}
                    </div>
                    <div className="flex items-center justify-center gap-4 mt-4">
                      <div className="flex items-center gap-2 bg-teal-800/50 px-4 py-2 rounded-xl">
                        <MapPin className="w-5 h-5 text-teal-300" />
                        <span className="text-xl font-bold text-teal-200">{data.latestCalled.roomName}</span>
                      </div>
                      <div className="flex items-center gap-2 bg-teal-800/50 px-4 py-2 rounded-xl">
                        <Stethoscope className="w-5 h-5 text-teal-300" />
                        <span className="text-lg text-teal-200">{data.latestCalled.doctorName}</span>
                      </div>
                    </div>
                    <div className="text-sm text-gray-400 mt-2">
                      {formatTimeAgo(data.latestCalled.calledAt)}
                    </div>
                  </div>
                </div>
              ) : (
                <div className="rounded-2xl bg-white/5 border border-white/10 p-6 md:p-8 text-center">
                  <PhoneCall className="w-10 h-10 text-gray-500 mx-auto mb-3" />
                  <p className="text-xl text-gray-400">لم يتم نداء أحد بعد</p>
                </div>
              )}

              {/* Waiting Count */}
              <div className="rounded-2xl bg-white/5 border border-white/10 p-5">
                <div className="flex items-center gap-3 mb-4">
                  <Users className="w-6 h-6 text-amber-400" />
                  <span className="text-lg font-bold text-amber-300">قائمة الانتظار</span>
                </div>
                <div className="text-center">
                  <span className="text-6xl font-extrabold text-amber-300">{data.waitingCount}</span>
                  <p className="text-lg text-gray-400 mt-2">مريض في الانتظار</p>
                </div>
                {data.waitingList.length > 0 && (
                  <div className="mt-4 space-y-2 max-h-48 overflow-y-auto">
                    {data.waitingList.map((w, i) => (
                      <div
                        key={i}
                        className="flex items-center justify-between px-3 py-2 rounded-lg bg-white/5"
                      >
                        <div className="flex items-center gap-2">
                          <span className="text-xs text-gray-500 w-5">{i + 1}</span>
                          <span className="text-sm font-medium text-gray-200">{w.patientName}</span>
                          <span className="text-xs text-gray-500 font-mono">{w.patientNumber}</span>
                        </div>
                        <span className="text-xs text-gray-400">{w.doctorName}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* ── Right Column: Recently Called ── */}
            <div className="lg:col-span-2">
              <div className="rounded-2xl bg-white/5 border border-white/10 p-5 h-full">
                <div className="flex items-center gap-3 mb-5">
                  <PhoneCall className="w-6 h-6 text-blue-400" />
                  <span className="text-lg font-bold text-blue-300">تم النداء مؤخراً</span>
                </div>
                {data.recentlyCalled.length === 0 ? (
                  <div className="text-center py-16">
                    <CheckCircle2 className="w-12 h-12 text-gray-600 mx-auto mb-3" />
                    <p className="text-xl text-gray-500">لا يوجد نداءات حديثة</p>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {data.recentlyCalled.map((item, i) => {
                      const cfg = getStatusDisplay(item.status);
                      return (
                        <div
                          key={i}
                          className="flex items-center gap-4 px-5 py-4 rounded-xl bg-white/5 border border-white/5 hover:bg-white/10 transition-colors"
                        >
                          {/* Room */}
                          <div className="flex items-center justify-center w-16 h-12 rounded-lg bg-cyan-900/50 border border-cyan-700/30">
                            <span className="text-lg font-bold text-cyan-300">{item.roomName}</span>
                          </div>

                          {/* Patient info */}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className="text-xl font-bold text-white">{item.patientName}</span>
                              <span className="text-sm text-gray-500 font-mono">{item.patientNumber}</span>
                            </div>
                            <div className="flex items-center gap-3 mt-1">
                              <span className="flex items-center gap-1 text-sm text-gray-400">
                                <Stethoscope className="w-3.5 h-3.5" />
                                {item.doctorName}
                              </span>
                              <span className="text-xs text-gray-500">{formatTimeAgo(item.calledAt)}</span>
                            </div>
                          </div>

                          {/* Status badge */}
                          <div className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium ${cfg.bg} ${cfg.color}`}>
                            <span className={`w-2.5 h-2.5 rounded-full ${cfg.dotColor} ${(item.status === "Called" || item.status === "InProgress") ? "animate-pulse" : ""}`} />
                            {item.statusArabic || cfg.label}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          </div>
        ) : null}
      </main>

      {/* ── Footer ─────────────────────────────────────────────── */}
      <footer className="border-t border-white/10 px-8 md:px-12 py-3 flex items-center justify-between text-gray-500 text-sm">
        <span>
          {lastUpdated
            ? `آخر تحديث: ${formatClock(lastUpdated)}`
            : "جاري التحميل…"}
        </span>
        <span>
          يتحدث تلقائياً كل ٣٠ ثانية — مركز الدكتور عقلان الكامل
        </span>
      </footer>
    </div>
  );
}
