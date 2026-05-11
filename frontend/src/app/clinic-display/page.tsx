"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import {
  Users,
  Volume2,
  MapPin,
  Stethoscope,
  CheckCircle2,
  PhoneCall,
  Clock,
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

const REFRESH_INTERVAL = 20_000; // 20 seconds

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
  const prevLatestCalledRef = useRef<string | null>(null);

  const fetchDisplay = useCallback(async () => {
    try {
      const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
      const res = await fetch(`${apiBase}/api/clinic-queue/display`, {
        cache: "no-store",
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: DisplayData = await res.json();

      // Only trigger pulse animation when the latest called patient actually changes
      const newKey = json.latestCalled?.patientNumber ?? null;
      if (newKey !== prevLatestCalledRef.current) {
        prevLatestCalledRef.current = newKey;
        setPulseKey((k) => k + 1);
      }

      setData(json);
      setLastUpdated(new Date());
      setError(null);
    } catch {
      setError("تعذّر تحميل البيانات");
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial fetch + auto-refresh
  useEffect(() => {
    fetchDisplay();
    const refreshInterval = setInterval(fetchDisplay, REFRESH_INTERVAL);
    return () => clearInterval(refreshInterval);
  }, [fetchDisplay]);

  // Live clock every second
  useEffect(() => {
    const clockInterval = setInterval(() => setNow(new Date()), 1_000);
    return () => clearInterval(clockInterval);
  }, []);

  const isFullyEmpty = data && data.waitingCount === 0 && !data.latestCalled && data.recentlyCalled.length === 0;

  return (
    <div
      dir="rtl"
      className="min-h-screen bg-[#0F172A] text-white flex flex-col"
      style={{ fontFamily: "'Segoe UI', Tahoma, Arial, sans-serif" }}
    >
      {/* ── Header ─────────────────────────────────────────────── */}
      <header className="flex items-center justify-between px-8 md:px-16 py-6 border-b border-white/10 bg-[#0c1322]">
        <div className="flex items-center gap-5">
          <div className="w-14 h-14 rounded-full bg-[#0E7490] flex items-center justify-center text-2xl font-bold shadow-lg shadow-cyan-900/40">
            ع
          </div>
          <div>
            <h1 className="text-3xl md:text-4xl font-bold text-white leading-tight">
              مركز الدكتور عقلان الكامل
            </h1>
            <p className="text-lg md:text-xl text-teal-300 mt-1">
              شاشة الطابور
            </p>
          </div>
        </div>

        {/* Live clock */}
        <div className="text-left">
          <p className="text-5xl md:text-6xl font-mono font-bold text-teal-300 tabular-nums">
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
      <main className="flex-1 px-8 md:px-16 py-8 overflow-auto">
        {loading && !data ? (
          <div className="flex flex-col items-center justify-center h-64 gap-4">
            <div className="w-12 h-12 border-4 border-teal-500 border-t-transparent rounded-full animate-spin" />
            <p className="text-2xl text-gray-400">جاري التحميل…</p>
          </div>
        ) : error && !data ? (
          <div className="flex flex-col items-center justify-center h-64 gap-4">
            <div className="text-5xl text-red-400 mb-2">⚠</div>
            <p className="text-2xl text-red-400">{error}</p>
            <button
              onClick={fetchDisplay}
              className="mt-4 px-6 py-3 rounded-xl bg-teal-700 text-white font-bold hover:bg-teal-600 transition"
            >
              إعادة المحاولة
            </button>
          </div>
        ) : isFullyEmpty ? (
          /* Fully empty clinic — friendly message */
          <div className="flex flex-col items-center justify-center h-full gap-4 py-20">
            <div className="w-24 h-24 rounded-full bg-white/5 flex items-center justify-center mb-4">
              <CheckCircle2 className="w-14 h-14 text-teal-500/60" />
            </div>
            <p className="text-3xl font-bold text-gray-300">لا يوجد مرضى في الطابور حالياً</p>
            <p className="text-lg text-gray-500 mt-2">سيتم تحديث الشاشة تلقائياً عند إضافة مرضى</p>
          </div>
        ) : data ? (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 h-full">
            {/* ── Left Column: Latest Called + Waiting Count ── */}
            <div className="lg:col-span-1 space-y-8">
              {/* Latest Called — Prominent */}
              {data.latestCalled ? (
                <div
                  key={pulseKey}
                  className="rounded-3xl bg-gradient-to-br from-teal-900/70 to-cyan-900/50 border border-teal-600/40 p-8 md:p-10 shadow-xl shadow-teal-900/20 animate-[fadeIn_0.6s_ease]"
                >
                  <div className="flex items-center gap-3 mb-6">
                    <Volume2 className="w-8 h-8 text-teal-300 animate-pulse" />
                    <span className="text-2xl font-bold text-teal-300">النداء الأخير</span>
                  </div>
                  <div className="text-center space-y-4">
                    <div className="text-5xl md:text-7xl font-extrabold text-white leading-tight">
                      {data.latestCalled.patientName}
                    </div>
                    <div className="text-2xl md:text-3xl text-teal-200 font-mono">
                      رقم الملف: {data.latestCalled.patientNumber}
                    </div>
                    <div className="flex items-center justify-center gap-5 mt-6">
                      <div className="flex items-center gap-3 bg-teal-800/60 px-6 py-3 rounded-2xl">
                        <MapPin className="w-6 h-6 text-teal-300" />
                        <span className="text-2xl font-bold text-teal-200">{data.latestCalled.roomName}</span>
                      </div>
                      {data.latestCalled.doctorName && (
                        <div className="flex items-center gap-3 bg-teal-800/60 px-6 py-3 rounded-2xl">
                          <Stethoscope className="w-6 h-6 text-teal-300" />
                          <span className="text-xl text-teal-200">{data.latestCalled.doctorName}</span>
                        </div>
                      )}
                    </div>
                    <div className="flex items-center justify-center gap-2 text-base text-gray-400 mt-3">
                      <Clock className="w-4 h-4" />
                      {formatTimeAgo(data.latestCalled.calledAt)}
                    </div>
                  </div>
                </div>
              ) : (
                <div className="rounded-3xl bg-white/5 border border-white/10 p-8 md:p-10 text-center">
                  <PhoneCall className="w-12 h-12 text-gray-500 mx-auto mb-4" />
                  <p className="text-2xl text-gray-400">لم يتم نداء أحد بعد</p>
                </div>
              )}

              {/* Waiting Count */}
              <div className="rounded-3xl bg-white/5 border border-white/10 p-6">
                <div className="flex items-center gap-4 mb-5">
                  <Users className="w-8 h-8 text-amber-400" />
                  <span className="text-2xl font-bold text-amber-300">قائمة الانتظار</span>
                </div>
                <div className="text-center">
                  <span className="text-7xl font-extrabold text-amber-300">{data.waitingCount}</span>
                  <p className="text-xl text-gray-400 mt-2">مريض في الانتظار</p>
                </div>
                {data.waitingList.length > 0 && (
                  <div className="mt-5 space-y-3 max-h-60 overflow-y-auto">
                    {data.waitingList.map((w, i) => (
                      <div
                        key={i}
                        className="flex items-center justify-between px-4 py-3 rounded-xl bg-white/5"
                      >
                        <div className="flex items-center gap-3">
                          <span className="text-sm text-gray-500 w-7 text-center font-bold">{i + 1}</span>
                          <span className="text-lg font-medium text-gray-200">{w.patientName}</span>
                          <span className="text-sm text-gray-500 font-mono">{w.patientNumber}</span>
                        </div>
                        {w.doctorName && (
                          <span className="text-sm text-gray-400 flex items-center gap-1">
                            <Stethoscope className="w-3 h-3" />
                            {w.doctorName}
                          </span>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* ── Right Column: Recently Called ── */}
            <div className="lg:col-span-2">
              <div className="rounded-3xl bg-white/5 border border-white/10 p-6 h-full">
                <div className="flex items-center gap-4 mb-6">
                  <PhoneCall className="w-8 h-8 text-blue-400" />
                  <span className="text-2xl font-bold text-blue-300">تم النداء مؤخراً</span>
                </div>
                {data.recentlyCalled.length === 0 ? (
                  <div className="text-center py-20">
                    <CheckCircle2 className="w-16 h-16 text-gray-600 mx-auto mb-4" />
                    <p className="text-2xl text-gray-500">لا يوجد نداءات حديثة</p>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {data.recentlyCalled.map((item, i) => {
                      const cfg = getStatusDisplay(item.status);
                      return (
                        <div
                          key={i}
                          className="flex items-center gap-5 px-6 py-5 rounded-2xl bg-white/5 border border-white/5 hover:bg-white/10 transition-colors"
                        >
                          {/* Room */}
                          <div className="flex items-center justify-center w-20 h-14 rounded-xl bg-cyan-900/50 border border-cyan-700/30">
                            <span className="text-xl font-bold text-cyan-300">{item.roomName}</span>
                          </div>

                          {/* Patient info */}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-3 flex-wrap">
                              <span className="text-2xl font-bold text-white">{item.patientName}</span>
                              <span className="text-base text-gray-500 font-mono">{item.patientNumber}</span>
                            </div>
                            <div className="flex items-center gap-4 mt-2">
                              {item.doctorName && (
                                <span className="flex items-center gap-1.5 text-base text-gray-400">
                                  <Stethoscope className="w-4 h-4" />
                                  {item.doctorName}
                                </span>
                              )}
                              <span className="text-sm text-gray-500 flex items-center gap-1">
                                <Clock className="w-3.5 h-3.5" />
                                {formatTimeAgo(item.calledAt)}
                              </span>
                            </div>
                          </div>

                          {/* Status badge */}
                          <div className={`inline-flex items-center gap-2 px-4 py-2 rounded-full text-base font-medium ${cfg.bg} ${cfg.color}`}>
                            <span className={`w-3 h-3 rounded-full ${cfg.dotColor} ${(item.status === "Called" || item.status === "InProgress") ? "animate-pulse" : ""}`} />
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
      <footer className="border-t border-white/10 px-8 md:px-16 py-4 flex items-center justify-between text-gray-500 text-base bg-[#0c1322]">
        <span>
          {lastUpdated
            ? `آخر تحديث: ${formatClock(lastUpdated)}`
            : "جاري التحميل…"}
        </span>
        <span>
          يتحدث تلقائياً كل ٢٠ ثانية — مركز الدكتور عقلان الكامل
        </span>
      </footer>

      {/* Global CSS for fadeIn animation */}
      <style jsx global>{`
        @keyframes fadeIn {
          from { opacity: 0; transform: scale(0.97); }
          to   { opacity: 1; transform: scale(1); }
        }
      `}</style>
    </div>
  );
}
