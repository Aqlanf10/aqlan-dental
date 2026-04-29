"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { cn, formatTime } from "@/lib/utils";

const HOURS = Array.from({ length: 13 }, (_, i) => i + 8); // 8:00 – 20:00

const DAY_NAMES = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];

function toDateStr(d: Date): string {
  return d.toISOString().split("T")[0];
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
  Confirmed:  "bg-teal-100 border-teal-300 text-teal-800",
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
                    ? "bg-clinic-teal/10 text-clinic-teal"
                    : "text-gray-600"
                )}
              >
                <div className={cn("font-bold", isToday && "text-clinic-teal")}>
                  {DAY_NAMES[dayIdx]}
                </div>
                <div className={cn("text-xs mt-0.5 font-mono", isToday ? "text-clinic-teal" : "text-gray-400")}>
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
              return (
                <Link
                  key={date}
                  href={`/appointments/new?date=${date}&startTime=${String(hour).padStart(2,"0")}:00`}
                  className={cn(
                    "min-h-[52px] rounded border border-dashed p-0.5 transition",
                    isToday
                      ? "border-clinic-teal/30 bg-clinic-teal/5 hover:bg-clinic-teal/10"
                      : "border-gray-100 bg-gray-50/50 hover:bg-gray-100"
                  )}
                  onClick={(e) => {
                    if (slotAppts.length > 0) e.preventDefault();
                  }}
                >
                  <div className="space-y-0.5">
                    {slotAppts.map((a) => (
                      <div
                        key={a.id}
                        className={cn(
                          "rounded px-1.5 py-1 text-xs border truncate",
                          STATUS_COLORS[a.status] ?? "bg-gray-100 border-gray-200"
                        )}
                        title={`${a.patientName} — ${a.appointmentType} — ${formatTime(a.startTime)}`}
                      >
                        <div className="flex items-center gap-1">
                          <span
                            className="w-1.5 h-1.5 rounded-full flex-shrink-0 inline-block"
                            style={{ backgroundColor: a.doctorColor ?? "#0E7490" }}
                          />
                          <span className="font-medium truncate">{a.patientName}</span>
                        </div>
                        <div className="text-[10px] opacity-70 font-mono">
                          {formatTime(a.startTime)} · {a.appointmentType}
                        </div>
                      </div>
                    ))}
                  </div>
                </Link>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
