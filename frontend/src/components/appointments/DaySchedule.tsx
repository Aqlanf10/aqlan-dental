"use client";
import { useEffect, useState } from "react";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { cn, APPOINTMENT_STATUS_LABELS, formatTime } from "@/lib/utils";

const HOURS = Array.from({ length: 13 }, (_, i) => i + 8); // 8:00 – 20:00

interface Props {
  date: string; // yyyy-MM-dd
}

export function DaySchedule({ date }: Props) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    api
      .get<Appointment[]>(`/api/appointments?from=${date}&to=${date}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [date]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-16 bg-gray-100 rounded-xl" />
        ))}
      </div>
    );
  }

  if (!appointments.length) {
    return (
      <div className="text-center py-16 text-gray-400">
        <p className="text-sm">لا توجد مواعيد في هذا اليوم</p>
      </div>
    );
  }

  return (
    <div className="space-y-1.5">
      {HOURS.map((h) => {
        const hourStr = `${String(h).padStart(2, "0")}:00`;
        const slotAppts = appointments.filter((a) => a.startTime.startsWith(String(h).padStart(2, "0")));

        return (
          <div key={h} className="flex items-start gap-3 min-h-[52px]">
            <span className="text-xs text-gray-400 w-12 flex-shrink-0 pt-2 font-mono">
              {formatTime(hourStr)}
            </span>
            <div className="flex-1 space-y-1.5">
              {slotAppts.map((a) => (
                <AppointmentCard key={a.id} appointment={a} />
              ))}
              {!slotAppts.length && (
                <div className="h-10 border-b border-dashed border-gray-100" />
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

function AppointmentCard({ appointment: a }: { appointment: Appointment }) {
  const statusColors: Record<string, string> = {
    Scheduled:  "bg-blue-50 border-blue-200 text-blue-800",
    Confirmed:  "bg-teal-50 border-teal-200 text-teal-800",
    Arrived:    "bg-yellow-50 border-yellow-200 text-yellow-800",
    InProgress: "bg-purple-50 border-purple-200 text-purple-800",
    Completed:  "bg-green-50 border-green-200 text-green-800",
    Cancelled:  "bg-gray-50 border-gray-200 text-gray-500",
    NoShow:     "bg-red-50 border-red-200 text-red-700",
  };

  return (
    <div
      className={cn(
        "rounded-lg border px-3 py-2 flex items-center gap-3",
        statusColors[a.status] ?? "bg-gray-50 border-gray-200"
      )}
    >
      {/* Doctor color indicator */}
      <div
        className="w-1 self-stretch rounded-full flex-shrink-0"
        style={{ backgroundColor: a.doctorColor ?? "#0E7490" }}
      />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-semibold text-sm">{a.patientName}</span>
          <span className="text-xs opacity-70 font-mono">{a.patientNumber}</span>
        </div>
        <div className="text-xs opacity-70 flex items-center gap-2 mt-0.5 flex-wrap">
          <span>{a.doctorName}</span>
          <span>·</span>
          <span>{a.appointmentType}</span>
          <span>·</span>
          <span>{formatTime(a.startTime)} – {formatTime(a.endTime)}</span>
        </div>
      </div>
      <span className="text-xs px-2 py-0.5 rounded-full bg-white/50 flex-shrink-0">
        {APPOINTMENT_STATUS_LABELS[a.status] ?? a.status}
      </span>
    </div>
  );
}
