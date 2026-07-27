"use client";
import { useEffect, useState, useCallback, useRef } from "react";
import Link from "next/link";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { cn, formatTime, localDateString } from "@/lib/utils";
// FE-09: centralized appointment status colors
import { APPOINTMENT_STATUS_COLORS as STATUS_COLORS } from "@/lib/statusStyles";
// YOLO-S1: resolve appointment color (explicit pick → package color → doctor color)
import { resolveAppointmentColor } from "@/lib/appointmentColors";

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

interface Props {
  anchor: string; // any date in the target week (yyyy-MM-dd)
  doctorId?: string;
  onDateClick?: (date: string) => void;
}

export function WeekCalendar({ anchor, doctorId, onDateClick }: Props) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  const dates = getWeekDates(anchor);
  const today = toDateStr(new Date());

  // NOTE: This component uses direct api.get() with from/to params instead of useAppointments hook.
  // The useAppointments hook uses startDate/endDate query params, while the backend expects from/to for date-range queries.
  // Until the hook is updated to support from/to params, direct API calls are the correct approach here.
  // CORE-APPT-014: stepping quickly between weeks fires overlapping requests with
  // no ordering guarantee, so a slower earlier response could land last and paint a
  // different week than the header shows. Each load takes a sequence number and
  // only the newest one is allowed to write state.
  const reqSeq = useRef(0);

  const load = useCallback(() => {
    const seq = ++reqSeq.current;
    setLoading(true);
    setLoadError(false);
    const q = doctorId ? `&doctorId=${doctorId}` : "";
    api
      .get<Appointment[]>(`/api/appointments?from=${dates[0]}&to=${dates[6]}${q}`)
      .then((r) => { if (seq === reqSeq.current) setAppointments(r.data); })
      // A failed fetch must not render an empty (appointment-free) week grid.
      // A STALE failure must not blank out the week that is actually displayed.
      .catch(() => { if (seq === reqSeq.current) { setAppointments([]); setLoadError(true); } })
      .finally(() => { if (seq === reqSeq.current) setLoading(false); });
  }, [dates[0], dates[6], doctorId]); // eslint-disable-line react-hooks/exhaustive-deps

  // The cleanup bump invalidates the in-flight request when the week/doctor
  // changes or the component unmounts, so a late reply can never apply.
  // Reading reqSeq.current at cleanup time is deliberate — the lint rule assumes a
  // DOM-node ref, but here we want the LATEST counter so the in-flight request is
  // invalidated. Copying it into a variable would defeat the guard.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { load(); return () => { reqSeq.current++; }; }, [load]);

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

  if (loadError) {
    return (
      <div className="rounded-xl border border-red-200 py-10 text-center" style={{ background: "#fef2f2" }}>
        <p className="text-sm font-medium" style={{ color: "#b91c1c" }}>
          تعذر تحميل المواعيد — تحقق من الاتصال وحاول مجددًا
        </p>
        <button
          type="button"
          onClick={load}
          className="mt-3 rounded-lg border border-red-300 px-4 py-1.5 text-sm font-medium text-red-700 transition hover:bg-red-100"
        >
          إعادة المحاولة
        </button>
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
                    {slotAppts.map((a) => {
                      // YOLO-S1: resolve the appointment color (explicit → package → doctor → null).
                      // Applied as a 4px left border so the calendar entry is visually
                      // differentiated by type while preserving the status background color.
                      const resolvedColor = resolveAppointmentColor(
                        a.appointmentColor,
                        a.packageColor,
                        a.doctorColor,
                      );
                      return (
                      <Link
                        key={a.id}
                        href={`/appointments/${a.id}`}
                        className={cn(
                          "block rounded px-1.5 py-1 text-xs border truncate hover:brightness-95 transition",
                          STATUS_COLORS[a.status] ?? "bg-gray-100 border-gray-200"
                        )}
                        style={resolvedColor ? { borderRight: `4px solid ${resolvedColor}` } : undefined}
                        title={`${a.patientName} — ${a.appointmentType} — ${formatTime(a.startTime)}${a.companionName ? ` — مرافق: ${a.companionName}` : ""}`}
                      >
                        <div className="flex items-center gap-1">
                          <span
                            className="w-1.5 h-1.5 rounded-full flex-shrink-0 inline-block"
                            style={{ backgroundColor: resolvedColor ?? "#2563EB" }}
                          />
                          <span className="font-medium truncate">{a.patientName}</span>
                          {a.companionName && (
                            <span className="text-[9px] text-emerald-700 bg-emerald-50 rounded-full px-1 py-px flex-shrink-0">
                              👤
                            </span>
                          )}
                        </div>
                        <div className="text-[10px] opacity-70 font-mono">
                          {formatTime(a.startTime)} · {a.appointmentType}
                          {a.packageName && <span className="opacity-80"> · 📦 {a.packageName}</span>}
                        </div>
                      </Link>
                      );
                    })}
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
