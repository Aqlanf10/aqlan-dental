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

const STATUS_COLORS: Record<string, { bg: string; border: string; text: string }> = {
  Scheduled:  { bg: "#3d7ab518", border: "#3d7ab530", text: "#3d7ab5" },
  Confirmed:  { bg: "#0ea5e918", border: "#0ea5e930", text: "#0ea5e9" },
  Arrived:    { bg: "#a855f718", border: "#a855f730", text: "#a855f7" },
  InProgress: { bg: "#f5922e18", border: "#f5922e30", text: "#f5922e" },
  Completed:  { bg: "#22c55e18", border: "#22c55e30", text: "#16a34a" },
  Cancelled:  { bg: "#64748b18", border: "#64748b30", text: "#64748b" },
  NoShow:     { bg: "#ef444418", border: "#ef444430", text: "#ef4444" },
};

const DEFAULT_DOCTOR_COLOR = "#3d7ab5";

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
            <div key={i} className="h-8 rounded" style={{ backgroundColor: "#f0f5fb" }} />
          ))}
        </div>
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="grid grid-cols-8 gap-0.5 mb-0.5">
            {Array.from({ length: 8 }).map((_, j) => (
              <div key={j} className="h-12 rounded" style={{ backgroundColor: "#f7fafd" }} />
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
        <div className="grid grid-cols-8 gap-0.5 mb-1 sticky top-0 z-10 pb-1" style={{ backgroundColor: "#ffffff" }}>
          {/* Time column header */}
          <div className="text-xs text-center py-2" style={{ color: "#94a3b8" }} />
          {dates.map((date) => {
            const dayIdx = new Date(date).getDay();
            const isToday = date === today;
            return (
              <button
                key={date}
                onClick={() => onDateClick?.(date)}
                className={cn(
                  "text-center py-2 rounded-lg text-xs font-medium transition-colors",
                )}
                style={isToday ? { color: "#3d7ab5" } : { color: "#64748b" }}
                onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "#f0f5fb"; }}
                onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
              >
                <div className={cn("font-bold", isToday && "text-[#3d7ab5]")}>
                  {DAY_NAMES[dayIdx]}
                </div>
                <div className="text-xs mt-0.5 font-mono" style={{ color: isToday ? "#3d7ab5" : "#94a3b8" }}>
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
            <div className="text-xs font-mono text-center py-2 pt-2.5" style={{ color: "#94a3b8" }}>
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
                    "min-h-[52px] rounded-lg border border-dashed p-0.5 transition-colors",
                  )}
                  style={{
                    borderColor: isToday ? "#3d7ab540" : "#f1f5f9",
                    backgroundColor: isToday ? "#3d7ab510" : "#f7fafd",
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = isToday ? "#3d7ab520" : "#f0f5fb"; }}
                  onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = isToday ? "#3d7ab510" : "#f7fafd"; }}
                  onClick={(e) => {
                    if (slotAppts.length > 0) e.preventDefault();
                  }}
                >
                  <div className="space-y-0.5">
                    {slotAppts.map((a) => {
                      const sc = STATUS_COLORS[a.status] ?? STATUS_COLORS.Scheduled;
                      return (
                        <div
                          key={a.id}
                          className="rounded px-1.5 py-1 text-xs border truncate"
                          style={{
                            backgroundColor: sc.bg,
                            borderColor: sc.border,
                            color: sc.text,
                          }}
                          title={`${a.patientName} — ${a.appointmentType} — ${formatTime(a.startTime)}`}
                        >
                          <div className="flex items-center gap-1">
                            <span
                              className="w-1.5 h-1.5 rounded-full flex-shrink-0 inline-block"
                              style={{ backgroundColor: a.doctorColor ?? DEFAULT_DOCTOR_COLOR }}
                            />
                            <span className="font-medium truncate">{a.patientName}</span>
                          </div>
                          <div className="text-[10px] opacity-70 font-mono">
                            {formatTime(a.startTime)} · {a.appointmentType}
                          </div>
                        </div>
                      );
                    })}
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
