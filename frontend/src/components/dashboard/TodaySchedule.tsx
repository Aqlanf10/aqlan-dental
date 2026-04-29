"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Clock, Calendar } from "lucide-react";
import api from "@/lib/api";
import type { Appointment } from "@/types/appointment";
import { cn, formatTime } from "@/lib/utils";

const STATUS_COLORS: Record<string, string> = {
  Scheduled:  "bg-blue-100 text-blue-700",
  Confirmed:  "bg-teal-100 text-teal-700",
  Arrived:    "bg-yellow-100 text-yellow-700",
  InProgress: "bg-purple-100 text-purple-700",
  Completed:  "bg-green-100 text-green-700",
  Cancelled:  "bg-gray-100 text-gray-400 line-through",
  NoShow:     "bg-red-100 text-red-700",
};
const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول", Confirmed: "مؤكد", Arrived: "وصل",
  InProgress: "جارٍ", Completed: "مكتمل", Cancelled: "ملغى", NoShow: "غياب",
};

export function TodaySchedule() {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);

  const today = new Date().toISOString().slice(0, 10);

  useEffect(() => {
    api.get<Appointment[]>(`/api/appointments?from=${today}&to=${today}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [today]);

  const active = appointments.filter((a) => a.status !== "Cancelled" && a.status !== "NoShow");
  const completed = appointments.filter((a) => a.status === "Completed").length;

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <Calendar className="w-4 h-4 text-clinic-teal" />
          <h3 className="font-bold text-gray-900 text-sm">مواعيد اليوم</h3>
          {!loading && (
            <span className="text-xs text-gray-400">
              {completed}/{active.length} مكتمل
            </span>
          )}
        </div>
        <Link href="/appointments" className="text-xs text-clinic-teal hover:underline">
          عرض الجدول
        </Link>
      </div>

      {loading ? (
        <div className="p-4 space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      ) : appointments.length === 0 ? (
        <div className="text-center py-10 text-gray-400">
          <Clock className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-xs">لا توجد مواعيد اليوم</p>
        </div>
      ) : (
        <div className="divide-y divide-gray-50 max-h-80 overflow-y-auto">
          {appointments
            .sort((a, b) => a.startTime.localeCompare(b.startTime))
            .map((appt) => (
              <div key={appt.id} className="flex items-center gap-3 px-5 py-3 hover:bg-gray-50 transition">
                {/* Doctor color dot */}
                <div
                  className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                  style={{ backgroundColor: appt.doctorColor ?? "#0E7490" }}
                />
                {/* Time */}
                <div className="text-xs font-mono text-gray-500 w-12 flex-shrink-0" dir="ltr">
                  {formatTime(appt.startTime)}
                </div>
                {/* Patient & type */}
                <div className="flex-1 min-w-0">
                  <Link
                    href={`/patients/${appt.patientId}`}
                    className="text-sm font-semibold text-gray-900 hover:text-clinic-teal transition truncate block"
                  >
                    {appt.patientName}
                  </Link>
                  <p className="text-xs text-gray-400 truncate">{appt.appointmentType} · {appt.doctorName}</p>
                </div>
                {/* Status badge */}
                <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium flex-shrink-0", STATUS_COLORS[appt.status] ?? "bg-gray-100 text-gray-600")}>
                  {STATUS_LABELS[appt.status] ?? appt.status}
                </span>
              </div>
            ))}
        </div>
      )}
    </div>
  );
}
