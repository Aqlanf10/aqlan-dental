"use client";
import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import type { Appointment } from "@/types/appointment";

const DAYS_AR = ["أحد", "اثنين", "ثلاثاء", "أربعاء", "خميس", "جمعة", "سبت"];

const STATUS_DOT: Record<string, string> = {
  Scheduled:  "bg-blue-400",
  Confirmed:  "bg-teal-400",
  Arrived:    "bg-yellow-400",
  InProgress: "bg-purple-400",
  Completed:  "bg-green-400",
  Cancelled:  "bg-gray-300",
  NoShow:     "bg-red-400",
};

interface Props {
  anchor: string;           // any date in the month (YYYY-MM-DD)
  doctorId?: string;
  onDateClick: (date: string) => void;
}

function toStr(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export function MonthCalendar({ anchor, doctorId, onDateClick }: Props) {
  const anchorDate  = new Date(anchor + "T12:00:00");
  const year        = anchorDate.getFullYear();
  const month       = anchorDate.getMonth(); // 0-based
  const todayStr    = toStr(new Date());

  const firstDay  = new Date(year, month, 1);
  const lastDay   = new Date(year, month + 1, 0);
  const startPad  = firstDay.getDay(); // 0=Sun
  const totalCells = startPad + lastDay.getDate();
  const rows = Math.ceil(totalCells / 7);

  const fromStr = toStr(firstDay);
  const toStr2  = toStr(lastDay);

  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    const q = doctorId ? `&doctorId=${doctorId}` : "";
    api.get<Appointment[]>(`/api/appointments?from=${fromStr}&to=${toStr2}${q}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [fromStr, toStr2, doctorId]);

  // Group by date string
  const byDate: Record<string, Appointment[]> = {};
  for (const a of appointments) {
    const key = a.appointmentDate;
    if (!byDate[key]) byDate[key] = [];
    byDate[key].push(a);
  }

  return (
    <div>
      {loading && (
        <div className="text-center py-3 text-xs text-gray-400 animate-pulse">جارٍ التحميل…</div>
      )}
      {/* Day headers */}
      <div className="grid grid-cols-7 mb-1">
        {DAYS_AR.map((d) => (
          <div key={d} className="text-center text-xs font-semibold text-gray-400 py-2">
            {d}
          </div>
        ))}
      </div>

      {/* Calendar grid */}
      <div className="grid grid-cols-7 gap-px bg-gray-100 rounded-xl overflow-hidden border border-gray-100">
        {Array.from({ length: rows * 7 }).map((_, i) => {
          const dayNum = i - startPad + 1;
          const isValid = dayNum >= 1 && dayNum <= lastDay.getDate();
          if (!isValid) {
            return <div key={i} className="bg-gray-50 min-h-[80px]" />;
          }

          const dateStr = `${year}-${String(month + 1).padStart(2, "0")}-${String(dayNum).padStart(2, "0")}`;
          const dayAppts = byDate[dateStr] ?? [];
          const isToday  = dateStr === todayStr;
          const isAnchor = dateStr === anchor;

          return (
            <button
              key={i}
              onClick={() => onDateClick(dateStr)}
              className={cn(
                "bg-white min-h-[80px] p-1.5 text-start align-top transition hover:bg-teal-50 focus:outline-none",
                isAnchor && "ring-2 ring-inset ring-clinic-teal"
              )}
            >
              {/* Day number */}
              <span
                className={cn(
                  "inline-flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold mb-1",
                  isToday
                    ? "bg-clinic-teal text-white"
                    : "text-gray-700"
                )}
              >
                {dayNum}
              </span>

              {/* Appointments */}
              {dayAppts.length > 0 && (
                <div className="space-y-0.5">
                  {dayAppts.slice(0, 3).map((a) => (
                    <div
                      key={a.id}
                      className="flex items-center gap-1 text-[10px] text-gray-700 truncate leading-tight"
                    >
                      <span
                        className={cn("w-1.5 h-1.5 rounded-full flex-shrink-0", STATUS_DOT[a.status] ?? "bg-gray-400")}
                      />
                      <span className="truncate">{a.patientName}</span>
                    </div>
                  ))}
                  {dayAppts.length > 3 && (
                    <div className="text-[10px] text-gray-400 font-medium">
                      +{dayAppts.length - 3} أخرى
                    </div>
                  )}
                </div>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
