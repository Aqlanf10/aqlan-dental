"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { ChevronRight, ChevronLeft, CalendarDays, Plus, LayoutGrid, List, Calendar, Stethoscope, Printer } from "lucide-react";
import { DaySchedule } from "@/components/appointments/DaySchedule";
import { WeekCalendar } from "@/components/appointments/WeekCalendar";
import { MonthCalendar } from "@/components/appointments/MonthCalendar";
import { UpcomingWidget } from "@/components/appointments/UpcomingWidget";
import { formatArabicDate, cn } from "@/lib/utils";
import api from "@/lib/api";

interface Doctor { id: string; name: string; color?: string; specialty?: string; }

const MONTHS_AR = [
  "يناير","فبراير","مارس","أبريل","مايو","يونيو",
  "يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر",
];

function toDateStr(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

type ViewMode = "day" | "week" | "month";

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
    <div className="space-y-5">
      {/* Print header — only visible when printing */}
      <div className="hidden print-only mb-4">
        <h1 className="text-2xl font-bold">جدول مواعيد يوم {date}</h1>
        <p className="text-sm text-gray-500 mt-1">مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
      </div>

      {/* Header */}
      <div className="no-print flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">المواعيد</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {view === "day" ? "جدول المواعيد اليومي" : view === "week" ? "عرض الأسبوع" : "عرض الشهر"}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Doctor filter */}
          {doctors.length > 0 && (
            <div className="relative flex items-center">
              <Stethoscope className="absolute end-2.5 w-3.5 h-3.5 text-gray-400 pointer-events-none" />
              <select
                value={doctorId}
                onChange={(e) => setDoctorId(e.target.value)}
                className="pe-8 ps-3 py-2 text-sm rounded-lg border border-gray-200 bg-white text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-blue appearance-none"
              >
                <option value="">كل الأطباء</option>
                {doctors.map((d) => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            </div>
          )}

          {/* View toggle */}
          <div className="flex items-center rounded-lg border border-gray-200 overflow-hidden">
            <button
              onClick={() => setView("day")}
              className={cn(
                "flex items-center gap-1.5 px-3 py-2 text-sm font-medium transition",
                view === "day" ? "bg-clinic-blue text-white" : "text-gray-600 hover:bg-gray-50"
              )}
            >
              <List className="w-3.5 h-3.5" />
              يومي
            </button>
            <button
              onClick={() => setView("week")}
              className={cn(
                "flex items-center gap-1.5 px-3 py-2 text-sm font-medium transition border-r border-l border-gray-200",
                view === "week" ? "bg-clinic-blue text-white" : "text-gray-600 hover:bg-gray-50"
              )}
            >
              <LayoutGrid className="w-3.5 h-3.5" />
              أسبوعي
            </button>
            <button
              onClick={() => setView("month")}
              className={cn(
                "flex items-center gap-1.5 px-3 py-2 text-sm font-medium transition",
                view === "month" ? "bg-clinic-blue text-white" : "text-gray-600 hover:bg-gray-50"
              )}
            >
              <Calendar className="w-3.5 h-3.5" />
              شهري
            </button>
          </div>
          {view === "day" && (
            <button
              onClick={() => window.print()}
              className="no-print flex items-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition"
              title="طباعة جدول اليوم"
            >
              <Printer className="w-4 h-4" />
              طباعة
            </button>
          )}
          <Link
            href="/appointments/new"
            className="no-print flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Plus className="w-4 h-4" />
            موعد جديد
          </Link>
        </div>
      </div>

      {/* Date Navigator */}
      <div className="no-print bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <div className="flex items-center gap-3">
          <button
            onClick={navigatePrev}
            className="p-1.5 rounded-lg hover:bg-gray-100 transition text-gray-500"
            aria-label="السابق"
          >
            <ChevronRight className="w-5 h-5" />
          </button>

          <div className="flex-1 flex items-center gap-3">
            <CalendarDays className="w-5 h-5 text-clinic-blue flex-shrink-0" />
            <div>
              <div className="font-bold text-gray-900">
                {view === "month" ? monthLabel : formatArabicDate(date)}
              </div>
              {isToday && view !== "month" && (
                <span className="text-xs text-clinic-blue font-medium">اليوم</span>
              )}
            </div>
          </div>

          {view !== "month" && (
            <input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-blue"
            />
          )}

          <button
            onClick={navigateNext}
            className="p-1.5 rounded-lg hover:bg-gray-100 transition text-gray-500"
            aria-label="التالي"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>

          {(!isToday || view === "month") && (
            <button
              onClick={() => setDate(toDateStr(new Date()))}
              className="text-xs px-3 py-1.5 rounded-lg border border-clinic-blue text-clinic-blue hover:bg-clinic-blue-50 transition font-medium"
            >
              اليوم
            </button>
          )}
        </div>
      </div>

      {/* Schedule */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div className="xl:col-span-2 bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          {view === "day" ? (
            <DaySchedule date={date} doctorId={doctorId || undefined} />
          ) : view === "week" ? (
            <WeekCalendar anchor={date} doctorId={doctorId || undefined} onDateClick={(d) => { setDate(d); setView("day"); }} />
          ) : (
            <MonthCalendar anchor={date} doctorId={doctorId || undefined} onDateClick={(d) => { setDate(d); setView("day"); }} />
          )}
        </div>
        {/* Upcoming sidebar */}
        <UpcomingWidget />
      </div>
    </div>
  );
}
