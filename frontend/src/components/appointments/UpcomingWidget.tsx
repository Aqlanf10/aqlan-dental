"use client";
import { useEffect, useState } from "react";
import { Clock, Stethoscope, CalendarDays } from "lucide-react";
import api from "@/lib/api";
import type { UpcomingAppointment } from "@/types/appointment";
import { formatTime, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";
import { cn } from "@/lib/utils";

const STATUS_BADGE_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-50 text-blue-700",
  Confirmed: "bg-sky-50 text-sky-700",
  Arrived: "bg-yellow-50 text-yellow-700",
  Waiting: "bg-amber-50 text-amber-700",
  Called: "bg-cyan-50 text-cyan-700",
  InRoom: "bg-purple-50 text-purple-700",
  InProgress: "bg-green-50 text-green-700",
};

export function UpcomingWidget() {
  const [appointments, setAppointments] = useState<UpcomingAppointment[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchUpcoming() {
      try {
        const { data } = await api.get<UpcomingAppointment[]>(
          "/api/appointments/upcoming?hours=2"
        );
        setAppointments(data ?? []);
      } catch {
        setAppointments([]);
      } finally {
        setLoading(false);
      }
    }
    fetchUpcoming();
    const interval = setInterval(fetchUpcoming, 60_000);
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5">
        <h3 className="font-bold text-sm text-gray-900 mb-4 flex items-center gap-2">
          <CalendarDays className="w-4 h-4 text-clinic-blue" />
          المواعيد القادمة
        </h3>
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 rounded-lg" />
          ))}
        </div>
      </div>
    );
  }

  if (appointments.length === 0) {
    return (
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5">
        <h3 className="font-bold text-sm text-gray-900 mb-4 flex items-center gap-2">
          <CalendarDays className="w-4 h-4 text-clinic-blue" />
          المواعيد القادمة
        </h3>
        <div className="text-center py-6 text-gray-400">
          <Clock className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-xs">لا توجد مواعيد في الساعتين القادمتين</p>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 flex items-center justify-between">
        <h3 className="font-bold text-sm text-gray-900 flex items-center gap-2">
          <CalendarDays className="w-4 h-4 text-clinic-blue" />
          المواعيد القادمة
        </h3>
        <span className="text-xs text-gray-400">{appointments.length} موعد</span>
      </div>
      <div className="max-h-80 overflow-y-auto">
        {appointments.map((a, i) => {
          const badgeColor = STATUS_BADGE_COLORS[a.status] ?? "bg-gray-50 text-gray-600";
          return (
            <div
              key={a.id}
              className={cn(
                "flex items-center gap-3 px-5 py-3 transition hover:bg-gray-50 cursor-pointer",
                i < appointments.length - 1 ? "border-b border-gray-50" : ""
              )}
              onClick={() => window.location.href = `/patients/${a.patientId}`}
            >
              {/* Time */}
              <div className="text-xs font-bold flex-shrink-0 text-center min-w-[42px]" style={{ color: "#64748b" }}>
                {formatTime(a.startTime)}
              </div>
              {/* Doctor color bar */}
              <div
                className="w-[3px] h-8 rounded-full flex-shrink-0"
                style={{ backgroundColor: a.doctorColor ?? "#3d7ab5" }}
              />
              {/* Info */}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-bold truncate" style={{ color: "#0d2137" }}>
                  {a.patientName}
                </p>
                <div className="flex items-center gap-1.5 text-xs text-gray-400 mt-0.5">
                  <Stethoscope className="w-3 h-3" />
                  <span>{a.doctorName}</span>
                  {a.serviceName && (
                    <>
                      <span>·</span>
                      <span className="text-[#3d7ab5]">{a.serviceName}</span>
                    </>
                  )}
                </div>
              </div>
              {/* Status */}
              <span className={cn("text-[11px] px-2 py-0.5 rounded-full font-semibold flex-shrink-0", badgeColor)}>
                {APPOINTMENT_STATUS_LABELS[a.status] ?? a.status}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
