"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { ChevronRight, ChevronLeft, CalendarDays, Plus, Stethoscope } from "lucide-react";
import { DaySchedule } from "@/components/appointments/DaySchedule";
import { WeekCalendar } from "@/components/appointments/WeekCalendar";
import { MonthCalendar } from "@/components/appointments/MonthCalendar";
import { formatArabicDate, cn } from "@/lib/utils";
import api from "@/lib/api";

interface Doctor { id: string; name: string; color?: string; specialty?: string; }

const MONTHS_AR = [
  "يناير","فبراير","مارس","أبريل","مايو","يونيو",
  "يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر",
];

const DOCTOR_COLORS = ["#3d7ab5", "#f5922e", "#22c55e", "#a855f7", "#ef4444"];

function toDateStr(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

type ViewMode = "day" | "week" | "month";

const VIEW_TABS: { key: ViewMode; label: string }[] = [
  { key: "day",   label: "يومي" },
  { key: "week",  label: "أسبوعي" },
  { key: "month", label: "شهري" },
];

export default function AppointmentsPage() {
  const [date, setDate]       = useState(() => toDateStr(new Date()));
  const [view, setView]       = useState<ViewMode>("day");
  const [doctorId, setDoctorId] = useState<string>("");
  const [doctors, setDoctors] = useState<Doctor[]>([]);

  useEffect(() => {
    api.get<Doctor[]>("/api/doctors").then((r) => setDoctors(r.data)).catch(() => {});
  }, []);

  const anchorDate = new Date(date + "T12:00:00");

  const shift = (days: number) => {
    const d = new Date(date + "T12:00:00");
    d.setDate(d.getDate() + days);
    setDate(toDateStr(d));
  };

  const shiftWeek = (weeks: number) => {
    const d = new Date(date + "T12:00:00");
    d.setDate(d.getDate() + weeks * 7);
    setDate(toDateStr(d));
  };

  const shiftMonth = (months: number) => {
    const d = new Date(date + "T12:00:00");
    d.setMonth(d.getMonth() + months);
    setDate(toDateStr(d));
  };

  const isToday = date === toDateStr(new Date());

  const monthLabel = `${MONTHS_AR[anchorDate.getMonth()]} ${anchorDate.getFullYear()}`;

  const navigatePrev = () => {
    if (view === "day") shift(-1);
    else if (view === "week") shiftWeek(-1);
    else shiftMonth(-1);
  };
  const navigateNext = () => {
    if (view === "day") shift(1);
    else if (view === "week") shiftWeek(1);
    else shiftMonth(1);
  };

  return (
    <div className="space-y-5" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold" style={{ color: "#0d2137" }}>
            المواعيد
          </h1>
          <p className="text-sm mt-0.5" style={{ color: "#64748b" }}>
            {view === "day" ? "جدول المواعيد اليومي" : view === "week" ? "عرض الأسبوع" : "عرض الشهر"}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* Doctor filter */}
          {doctors.length > 0 && (
            <div className="relative flex items-center">
              <Stethoscope
                className="absolute end-2.5 w-3.5 h-3.5 pointer-events-none"
                style={{ color: "#94a3b8" }}
              />
              <select
                value={doctorId}
                onChange={(e) => setDoctorId(e.target.value)}
                className="aqlan-select pe-8 ps-3 py-2 text-sm min-w-[150px]"
              >
                <option value="">كل الأطباء</option>
                {doctors.map((d) => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            </div>
          )}

          {/* Doctor color indicators */}
          {doctors.length > 0 && (
            <div className="hidden md:flex items-center gap-2">
              {doctors.slice(0, 5).map((d, i) => (
                <div key={d.id} className="flex items-center gap-1.5">
                  <span
                    className="w-2.5 h-2.5 rounded-full"
                    style={{ backgroundColor: DOCTOR_COLORS[i % DOCTOR_COLORS.length] }}
                  />
                  <span className="text-xs" style={{ color: "#64748b" }}>{d.name}</span>
                </div>
              ))}
            </div>
          )}

          {/* Segmented View Toggle */}
          <div
            className="flex items-center p-1 rounded-[10px]"
            style={{ backgroundColor: "#f0f5fb" }}
          >
            {VIEW_TABS.map((tab) => (
              <button
                key={tab.key}
                onClick={() => setView(tab.key)}
                className={cn(
                  "px-4 py-1.5 text-sm rounded-[8px] transition-all duration-200",
                )}
                style={
                  view === tab.key
                    ? {
                        backgroundColor: "#ffffff",
                        color: "#0d2137",
                        fontWeight: 700,
                        boxShadow: "0 1px 3px rgba(13,33,55,0.1)",
                      }
                    : {
                        backgroundColor: "transparent",
                        color: "#64748b",
                        fontWeight: 400,
                      }
                }
              >
                {tab.label}
              </button>
            ))}
          </div>

          {/* Add Appointment */}
          <Link
            href="/appointments/new"
            className="btn-blue flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            موعد جديد
          </Link>
        </div>
      </div>

      {/* Date Navigator */}
      <div
        className="rounded-xl border p-4"
        style={{
          backgroundColor: "#ffffff",
          borderColor: "#e8f0f9",
          boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
        }}
      >
        <div className="flex items-center gap-3">
          <button
            onClick={navigatePrev}
            className="p-1.5 rounded-lg transition-colors"
            style={{ color: "#64748b" }}
            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "#f0f5fb"; }}
            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
            aria-label="السابق"
          >
            <ChevronRight className="w-5 h-5" />
          </button>

          <div className="flex-1 flex items-center gap-3">
            <CalendarDays className="w-5 h-5 flex-shrink-0" style={{ color: "#3d7ab5" }} />
            <div>
              <div className="font-bold" style={{ color: "#0d2137" }}>
                {view === "month" ? monthLabel : formatArabicDate(date)}
              </div>
              {isToday && view !== "month" && (
                <span className="text-xs font-medium" style={{ color: "#3d7ab5" }}>اليوم</span>
              )}
            </div>
          </div>

          {view !== "month" && (
            <input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="aqlan-input px-2 py-1.5 text-sm"
              style={{ borderRadius: "0.5rem" }}
            />
          )}

          <button
            onClick={navigateNext}
            className="p-1.5 rounded-lg transition-colors"
            style={{ color: "#64748b" }}
            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "#f0f5fb"; }}
            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
            aria-label="التالي"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>

          {(!isToday || view === "month") && (
            <button
              onClick={() => setDate(toDateStr(new Date()))}
              className="text-xs px-3 py-1.5 rounded-lg border font-medium transition-colors"
              style={{
                borderColor: "#3d7ab5",
                color: "#3d7ab5",
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.backgroundColor = "#3d7ab518";
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.backgroundColor = "transparent";
              }}
            >
              اليوم
            </button>
          )}
        </div>
      </div>

      {/* Schedule */}
      <div
        className="rounded-xl border p-5"
        style={{
          backgroundColor: "#ffffff",
          borderColor: "#e8f0f9",
          boxShadow: "0 1px 3px rgba(13,33,55,0.06)",
        }}
      >
        {view === "day" ? (
          <DaySchedule date={date} doctorId={doctorId || undefined} />
        ) : view === "week" ? (
          <WeekCalendar anchor={date} doctorId={doctorId || undefined} onDateClick={(d) => { setDate(d); setView("day"); }} />
        ) : (
          <MonthCalendar anchor={date} doctorId={doctorId || undefined} onDateClick={(d) => { setDate(d); setView("day"); }} />
        )}
      </div>
    </div>
  );
}
