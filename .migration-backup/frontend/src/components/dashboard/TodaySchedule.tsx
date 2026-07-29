"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Clock, Calendar } from "lucide-react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import type { Appointment } from "@/types/appointment";
import { formatTime, localDateString } from "@/lib/utils";
// FE-09: centralized appointment status labels
import { APPOINTMENT_STATUS_LABELS as STATUS_LABELS } from "@/lib/statusStyles";

/* ZIP-matched status badge colors */
const STATUS_STYLES: Record<string, { color: string; bg: string }> = {
  Scheduled:  { color: "#64748b",  bg: "#64748b18" },
  Confirmed:  { color: "#3d7ab5",  bg: "#3d7ab518" },
  Arrived:    { color: "#f5922e",  bg: "#f5922e18" },
  InProgress: { color: "#3d7ab5",  bg: "#3d7ab518" },
  Completed:  { color: "#22c55e",  bg: "#22c55e18" },
  Cancelled:  { color: "#94a3b8",  bg: "#94a3b818" },
  NoShow:     { color: "#ef4444",  bg: "#ef444418" },
};

export function TodaySchedule() {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const router = useRouter();

  const today = localDateString();

  useEffect(() => {
    setLoading(true);
    setLoadError(false);
    api.get<Appointment[]>(`/api/appointments?from=${today}&to=${today}`)
      .then((r) => setAppointments(r.data))
      // A failed fetch must not read as "لا توجد مواعيد اليوم".
      .catch(() => { setAppointments([]); setLoadError(true); })
      .finally(() => setLoading(false));
  }, [today, reloadKey]);

  const active = appointments.filter((a) => a.status !== "Cancelled" && a.status !== "NoShow");
  const completed = appointments.filter((a) => a.status === "Completed").length;

  return (
    <div
      className="overflow-hidden"
      style={{
        background: "#fff",
        borderRadius: 12,
        boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
        border: "1px solid #e8f0f9",
      }}
    >
      {/* Header — matches ZIP */}
      <div className="flex items-center justify-between px-5 py-4" style={{ borderBottom: "1px solid #f1f5f9" }}>
        <div className="flex items-center gap-2">
          <Calendar className="w-4 h-4" style={{ color: "#3d7ab5" }} />
          <h3 className="font-extrabold text-sm" style={{ color: "#0d2137" }}>مواعيد اليوم</h3>
          {!loading && (
            <span className="text-xs" style={{ color: "#94a3b8" }}>
              {completed}/{active.length} مكتمل
            </span>
          )}
        </div>
        <Link
          href="/appointments"
          className="text-xs font-semibold transition"
          style={{ color: "#3d7ab5" }}
          onMouseEnter={(e) => (e.currentTarget.style.color = "#2d5e8e")}
          onMouseLeave={(e) => (e.currentTarget.style.color = "#3d7ab5")}
        >
          عرض الجدول
        </Link>
      </div>

      {loading ? (
        <div className="p-4 space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-12 rounded-lg" style={{ background: "#f1f5f9" }} />
          ))}
        </div>
      ) : loadError ? (
        <div className="text-center py-10" style={{ background: "#fef2f2" }}>
          <p className="text-xs font-medium" style={{ color: "#b91c1c" }}>
            تعذر تحميل مواعيد اليوم — تحقق من الاتصال
          </p>
          <button
            type="button"
            onClick={() => setReloadKey((k) => k + 1)}
            className="mt-2 rounded-lg border border-red-300 px-3 py-1 text-xs font-medium text-red-700 transition hover:bg-red-100"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : appointments.length === 0 ? (
        <div className="text-center py-10" style={{ color: "#94a3b8" }}>
          <Clock className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-xs">لا توجد مواعيد اليوم</p>
        </div>
      ) : (
        <div className="max-h-80 overflow-y-auto">
          {appointments
            .sort((a, b) => a.startTime.localeCompare(b.startTime))
            .map((appt, i) => {
              const st = STATUS_STYLES[appt.status] ?? STATUS_STYLES.Scheduled;
              return (
                <div
                  key={appt.id}
                  className="flex items-center gap-3.5 px-5 py-3 cursor-pointer transition"
                  style={{ borderBottom: i < appointments.length - 1 ? "1px solid #f8fafc" : "none" }}
                  onClick={() => router.push(`/appointments/${appt.id}`)}
                  onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
                >
                  {/* Time */}
                  <div className="text-xs font-bold flex-shrink-0 text-center" style={{ color: "#64748b", width: 38 }}>
                    {formatTime(appt.startTime)}
                  </div>
                  {/* Doctor color bar — matches ZIP */}
                  <div
                    className="w-[3px] h-8 rounded-full flex-shrink-0"
                    style={{ backgroundColor: appt.doctorColor ?? "#3d7ab5" }}
                  />
                  {/* Patient & type */}
                  <div className="flex-1 min-w-0">
                    <Link
                      href={`/patients/${appt.patientId}`}
                      className="text-sm font-bold block truncate transition"
                      style={{ color: "#0d2137" }}
                      onMouseEnter={(e) => (e.currentTarget.style.color = "#3d7ab5")}
                      onMouseLeave={(e) => (e.currentTarget.style.color = "#0d2137")}
                    >
                      {appt.patientName}
                    </Link>
                    <p className="text-xs mt-0.5 truncate" style={{ color: "#94a3b8" }}>{appt.appointmentType} · {appt.doctorName}</p>
                  </div>
                  {/* Status badge — matches ZIP Badge component */}
                  <span
                    className="text-xs px-2.5 py-0.5 rounded-full font-semibold flex-shrink-0"
                    style={{ color: st.color, background: st.bg }}
                  >
                    {STATUS_LABELS[appt.status] ?? appt.status}
                  </span>
                  <div className="text-xs flex-shrink-0" style={{ color: "#cbd5e1" }}>
                    {appt.durationMinutes ?? 30} دق
                  </div>
                </div>
              );
            })}
        </div>
      )}
    </div>
  );
}
