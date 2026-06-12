"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { cn, formatTime, localDateString } from "@/lib/utils";

const HOURS = Array.from({ length: 13 }, (_, i) => i + 8); // 8:00 – 20:00

const DAY_NAMES = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];

function toDateStr(d: Date): string {
  return localDateString(d);
}

function getWeekDates(anchor: string): string[] {
  const d = new Date(anchor);
  const day = d.getDay(); // 0=Sun
  const sunday = new Date(d);
  sunday.setDate(d.getDate() - day);
  return Array.from({ length: 7 }, (_, i) => {
    const dd = new Date(sunday);
    dd.setDate(sunday.getDate() + i);
    return toDateStr(dd);
  });
}

function formatShortDate(dateStr: string): string {
  const d = new Date(dateStr);
  return `${d.getDate()}/${d.getMonth() + 1}`;
}

const STATUS_COLORS: Record<string, string> = {
  Scheduled:  "bg-blue-100 border-blue-300 text-blue-800",
  Confirmed:  "bg-clinic-blue-50 border-clinic-blue-100 text-clinic-navy-700",
  Arrived:    "bg-yellow-100 border-yellow-300 text-yellow-800",
  InProgress: "bg-purple-100 border-purple-300 text-purple-800",
  Completed:  "bg-green-100 border-green-300 text-green-800",
  Cancelled:  "bg-gray-100 border-gray-200 text-gray-400 line-through",
  NoShow:     "bg-red-100 border-red-300 text-red-700",
};

interface Props {
  anchor: string; // any date in the target week (yyyy-MM-dd)
  doctorId?: string;
  onDateClick?: (date: string) => void;
}

export function WeekCalendar({ anchor, doctorId, onDateClick }: Props) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);

  const dates = getWeekDates(anchor);
  const today = toDateStr(new Date());

  // NOTE: This component uses direct api.get() with from/to params instead of useAppointments hook.
  // The useAppointments hook uses startDate/endDate query params, while the backend expects from/to for date-range queries.
  // Until the hook is updated to support from/to params, direct API calls are the correct approach here.
  const load = useCallback(() => {
    setLoading(true);
    const q = doctorId ? `&doctorId=${doctorId}` : "";
    api
      .get<Appointment[]>(`/api/appointments?from=${dates[0]}&to=${dates[6]}${q}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [dates[0], dates[6], doctorId]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => { load(); }, [load]);

  const getSlotAppts = (date: string, hour: number) =>
    appointments.filter(
      (a) =>
        a.appointmentDate === date &&
        a.startTime.startsWith(String(hour).padStart(2, "0"))
    );

  if (loading) {
    return (
      <div className="animate-pulse">
        <div className="grid grid-cols-8 gap-0.5 mb-1">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="h-8 bg-gray-100 rounded" />
          ))}
        </div>
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="grid grid-cols-8 gap-0.5 mb-0.5">
            {Array.from({ length: 8 }).map((_, j) => (
              <div key={j} className="h-12 bg-gray-50 rounded" />
            ))}
          </div>
        ))}
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <div className="min-w-[700px]">
        {/* Day headers */}
        <div className="grid grid-cols-8 gap-0.5 mb-1 sticky top-0 z-10 bg-white pb-1">
          {/* Time column header */}
          <div className="text-xs text-gray-400 text-center py-2" />
          {dates.map((date) => {
            const dayIdx = new Date(date).getDay();
            const isToday = date === today;
            return (
              <button
                key={date}
                onClick={() => onDateClick?.(date)}
                className={cn(
                  "text-center py-2 rounded-lg text-xs font-medium transition hover:bg-gray-50",
                  isToday
                    ? "bg-clinic-blue/10 text-clinic-blue"
                    : "text-gray-600"
                )}
              >
                <div className={cn("font-bold", isToday && "text-clinic-blue")}>
                  {DAY_NAMES[dayIdx]}
                </div>
                <div className={cn("text-xs mt-0.5 font-mono", isToday ? "text-clinic-blue" : "text-gray-400")}>
                  {formatShortDate(date)}
                </div>
              </button>
            );
          })}
        </div>

        {/* Time rows */}
        {HOURS.map((hour) => (
          <div key={hour} className="grid grid-cols-8 gap-0.5 mb-0.5">
            {/* Hour label */}
            <div className="text-xs text-gray-400 font-mono text-center py-2 pt-2.5">
              {String(hour).padStart(2, "0")}:00
            </div>
            {/* Day cells */}
            {dates.map((date) => {
              const slotAppts = getSlotAppts(date, hour);
              const isToday = date === today;
              const h24 = String(hour).padStart(2, "0");
              const newUrl = `/appointments/new?date=${date}&startTime=${h24}:00${doctorId ? `&doctorId=${doctorId}` : ""}`;
              return (
                <div
                  key={date}
                  className={cn(
                    "min-h-[52px] rounded border border-dashed p-0.5 transition group relative",
                    isToday
                      ? "border-clinic-blue/30 bg-clinic-blue/5"
                      : "border-gray-100 bg-gray-50/50"
                  )}
                >
                  <div className="space-y-0.5">
                    {slotAppts.map((a) => (
                      <Link
                        key={a.id}
                        href={`/appointments/${a.id}`}
                        className={cn(
                          "block rounded px-1.5 py-1 text-xs border truncate hover:brightness-95 transition",
                          STATUS_COLORS[a.status] ?? "bg-gray-100 border-gray-200"
                        )}
                        title={`${a.patientName} — ${a.appointmentType} — ${formatTime(a.startTime)}`}
                      >
                        <div className="flex items-center gap-1">
                          <span
                            className="w-1.5 h-1.5 rounded-full flex-shrink-0 inline-block"
                            style={{ backgroundColor: a.doctorColor ?? "#2563EB" }}
                          />
                          <span className="font-medium truncate">{a.patientName}</span>
                        </div>
                        <div className="text-[10px] opacity-70 font-mono">
                          {formatTime(a.startTime)} · {a.appointmentType}
                        </div>
                      </Link>
                    ))}
                  </div>
                  {/* Click empty area to add appointment */}
                  {slotAppts.length === 0 && (
                    <Link
                      href={newUrl}
                      className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity text-clinic-blue"
                      title="إضافة موعد"
                    >
                      <span className="text-lg font-light">+</span>
                    </Link>
                  )}
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
