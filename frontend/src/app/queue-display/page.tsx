"use client";

import { useState, useEffect, useCallback } from "react";
import { QueueDisplay } from "@/types/appointment";

const API_BASE = process.env.NEXT_PUBLIC_API_URL || "";

/* ─── TV Display Page — No sidebar, large text, auto-refresh ──────────────── */
export default function QueueDisplayPage() {
  const [data, setData] = useState<QueueDisplay | null>(null);
  const [loading, setLoading] = useState(true);
  const [currentTime, setCurrentTime] = useState(new Date());

  /* ── Fetch display data ──────────────────────────────────────────────────── */
  const fetchDisplay = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/api/clinic-queue/display`);
      if (res.ok) {
        const json = await res.json();
        setData(json);
      }
    } catch {
      /* silent — TV display retries automatically */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchDisplay();
    const interval = setInterval(fetchDisplay, 8000); // refresh every 8 seconds
    return () => clearInterval(interval);
  }, [fetchDisplay]);

  /* ── Update clock ────────────────────────────────────────────────────────── */
  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const formatTime = (d: Date) =>
    d.toLocaleTimeString("ar-SA", { hour: "2-digit", minute: "2-digit" });

  const formatDate = (d: Date) =>
    d.toLocaleDateString("ar-SA", {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    });

  /* ── Render ──────────────────────────────────────────────────────────────── */
  return (
    <div
      className="min-h-screen flex flex-col"
      style={{
        background: "linear-gradient(135deg, #0F1B2D 0%, #1a3a5c 50%, #0F1B2D 100%)",
        direction: "rtl",
        color: "#fff",
      }}
    >
      {/* ── Top bar ──────────────────────────────────────────────────────── */}
      <header
        className="flex items-center justify-between px-8 py-4"
        style={{ borderBottom: "1px solid rgba(255,255,255,0.1)" }}
      >
        <div className="flex items-center gap-3">
          <div
            className="w-10 h-10 rounded-lg flex items-center justify-center"
            style={{ background: "#3d7ab5" }}
          >
            <svg className="w-5 h-5 text-white" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
            </svg>
          </div>
          <span className="text-xl font-bold">مركز د. عقلان</span>
        </div>
        <div className="text-left">
          <div className="text-3xl font-bold">{formatTime(currentTime)}</div>
          <div className="text-sm opacity-60">{formatDate(currentTime)}</div>
        </div>
      </header>

      {/* ── Main content ─────────────────────────────────────────────────── */}
      <main className="flex-1 flex flex-col px-8 py-6 gap-6 overflow-hidden">
        {loading ? (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-2xl opacity-60">جارٍ تحميل شاشة العرض...</div>
          </div>
        ) : (
          <>
            {/* ── Latest called — hero section ─────────────────────────────── */}
            <section
              className="rounded-3xl p-8 text-center"
              style={{
                background: data?.latestCalled
                  ? "linear-gradient(135deg, #3d7ab5 0%, #2563eb 100%)"
                  : "rgba(255,255,255,0.05)",
                minHeight: "200px",
                display: "flex",
                flexDirection: "column",
                justifyContent: "center",
              }}
            >
              {data?.latestCalled ? (
                <>
                  <div className="text-lg opacity-80 mb-2">يُرجى التوجه إلى</div>
                  <div
                    className="text-5xl font-black mb-3"
                    style={{ color: "#f5922e" }}
                  >
                    {data.latestCalled.roomName}
                  </div>
                  <div className="text-3xl font-bold mb-1">
                    المريض رقم {data.latestCalled.patientNumber}
                  </div>
                  <div className="text-2xl opacity-90">
                    {data.latestCalled.patientName}
                  </div>
                  <div className="text-base opacity-70 mt-1">
                    الدكتور: {data.latestCalled.doctorName}
                  </div>
                </>
              ) : (
                <div className="text-2xl opacity-50">لا يوجد نداء حالي</div>
              )}
            </section>

            {/* ── Bottom row: Waiting + Recently Called ──────────────────────── */}
            <div className="flex-1 grid grid-cols-1 md:grid-cols-2 gap-6 min-h-0">
              {/* Waiting list */}
              <section
                className="rounded-2xl p-5 overflow-hidden flex flex-col"
                style={{ background: "rgba(255,255,255,0.07)" }}
              >
                <div className="flex items-center justify-between mb-3">
                  <h2 className="text-lg font-bold">في الانتظار</h2>
                  <span
                    className="px-3 py-1 rounded-full text-sm font-bold"
                    style={{ background: "#f5922e", color: "#fff" }}
                  >
                    {data?.waitingCount ?? 0}
                  </span>
                </div>
                <div className="flex-1 overflow-y-auto space-y-2">
                  {data?.waitingList && data.waitingList.length > 0 ? (
                    data.waitingList.map((w, i) => (
                      <div
                        key={w.patientId + i}
                        className="flex items-center justify-between px-4 py-2.5 rounded-xl"
                        style={{ background: "rgba(255,255,255,0.08)" }}
                      >
                        <div className="flex items-center gap-3">
                          <span
                            className="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold"
                            style={{ background: "#3d7ab5" }}
                          >
                            {i + 1}
                          </span>
                          <div>
                            <div className="text-sm font-bold">{w.patientName}</div>
                            <div className="text-xs opacity-60">
                              رقم {w.patientNumber} • {w.appointmentTime}
                            </div>
                          </div>
                        </div>
                        <div className="text-xs opacity-70">{w.doctorName}</div>
                      </div>
                    ))
                  ) : (
                    <div className="text-center opacity-40 py-4 text-sm">
                      لا يوجد مرضى في الانتظار
                    </div>
                  )}
                </div>
              </section>

              {/* Recently called */}
              <section
                className="rounded-2xl p-5 overflow-hidden flex flex-col"
                style={{ background: "rgba(255,255,255,0.07)" }}
              >
                <h2 className="text-lg font-bold mb-3">تم النداء مؤخرًا</h2>
                <div className="flex-1 overflow-y-auto space-y-2">
                  {data?.recentlyCalled && data.recentlyCalled.length > 0 ? (
                    data.recentlyCalled.map((r, i) => (
                      <div
                        key={r.patientId + i}
                        className="flex items-center justify-between px-4 py-2.5 rounded-xl"
                        style={{ background: "rgba(255,255,255,0.08)" }}
                      >
                        <div>
                          <div className="text-sm font-bold">{r.patientName}</div>
                          <div className="text-xs opacity-60">
                            رقم {r.patientNumber}
                          </div>
                        </div>
                        <div className="flex items-center gap-2">
                          <span
                            className="text-xs px-2 py-0.5 rounded-md font-bold"
                            style={{ background: "#3d7ab5", color: "#fff" }}
                          >
                            {r.roomName}
                          </span>
                          <span
                            className="text-xs px-2 py-0.5 rounded-md font-medium"
                            style={{
                              background:
                                r.status === "InRoom"
                                  ? "rgba(124,58,237,0.3)"
                                  : "rgba(249,115,22,0.3)",
                              color:
                                r.status === "InRoom" ? "#c4b5fd" : "#fed7aa",
                            }}
                          >
                            {r.status === "InRoom" ? "داخل الغرفة" : "تم النداء"}
                          </span>
                        </div>
                      </div>
                    ))
                  ) : (
                    <div className="text-center opacity-40 py-4 text-sm">
                      لا يوجد نداءات سابقة
                    </div>
                  )}
                </div>
              </section>
            </div>
          </>
        )}
      </main>

      {/* ── Footer ─────────────────────────────────────────────────────────── */}
      <footer
        className="px-8 py-3 text-center text-xs opacity-40"
        style={{ borderTop: "1px solid rgba(255,255,255,0.1)" }}
      >
        شاشة عرض الطابور — مركز د. عقلان لطب وتقويم الأسنان
      </footer>
    </div>
  );
}
