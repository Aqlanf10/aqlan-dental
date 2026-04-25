"use client";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { MoreVertical } from "lucide-react";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { cn, APPOINTMENT_STATUS_LABELS, formatTime } from "@/lib/utils";

const HOURS = Array.from({ length: 13 }, (_, i) => i + 8); // 8:00 – 20:00

const STATUS_TRANSITIONS: Record<string, { value: string; label: string }[]> = {
  Scheduled:  [{ value: "Confirmed", label: "تأكيد" }, { value: "Arrived", label: "وصل" }, { value: "Cancelled", label: "إلغاء" }],
  Confirmed:  [{ value: "Arrived",    label: "وصل" },   { value: "Cancelled", label: "إلغاء" }],
  Arrived:    [{ value: "InProgress", label: "بدأ" },   { value: "NoShow", label: "غياب" }],
  InProgress: [{ value: "Completed",  label: "اكتمل" }, { value: "Cancelled", label: "إلغاء" }],
  Completed:  [],
  Cancelled:  [{ value: "Scheduled",  label: "إعادة جدولة" }],
  NoShow:     [{ value: "Scheduled",  label: "إعادة جدولة" }],
};

const STATUS_COLORS: Record<string, string> = {
  Scheduled:  "bg-blue-50 border-blue-200 text-blue-800",
  Confirmed:  "bg-teal-50 border-teal-200 text-teal-800",
  Arrived:    "bg-yellow-50 border-yellow-200 text-yellow-800",
  InProgress: "bg-purple-50 border-purple-200 text-purple-800",
  Completed:  "bg-green-50 border-green-200 text-green-800",
  Cancelled:  "bg-gray-50 border-gray-200 text-gray-500",
  NoShow:     "bg-red-50 border-red-200 text-red-700",
};

interface Props {
  date: string;
}

export function DaySchedule({ date }: Props) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = () => {
    setLoading(true);
    api
      .get<Appointment[]>(`/api/appointments?from=${date}&to=${date}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => { reload(); }, [date]); // eslint-disable-line react-hooks/exhaustive-deps

  const updateStatus = async (id: string, status: string) => {
    await api.put(`/api/appointments/${id}/status`, { status }).catch(() => {});
    setAppointments((prev) =>
      prev.map((a) => (a.id === id ? { ...a, status } : a))
    );
  };

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
        <Link
          href="/appointments/new"
          className="mt-3 inline-block text-xs text-clinic-teal hover:underline"
        >
          + إضافة موعد
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-1.5">
      {HOURS.map((h) => {
        const slotAppts = appointments.filter((a) =>
          a.startTime.startsWith(String(h).padStart(2, "0"))
        );

        return (
          <div key={h} className="flex items-start gap-3 min-h-[52px]">
            <span className="text-xs text-gray-400 w-12 flex-shrink-0 pt-2 font-mono">
              {formatTime(`${String(h).padStart(2, "0")}:00`)}
            </span>
            <div className="flex-1 space-y-1.5">
              {slotAppts.map((a) => (
                <AppointmentCard
                  key={a.id}
                  appointment={a}
                  onStatusChange={updateStatus}
                />
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

function AppointmentCard({
  appointment: a,
  onStatusChange,
}: {
  appointment: Appointment;
  onStatusChange: (id: string, status: string) => void;
}) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const transitions = STATUS_TRANSITIONS[a.status] ?? [];

  // Close menu on outside click
  useEffect(() => {
    if (!menuOpen) return;
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [menuOpen]);

  return (
    <div
      className={cn(
        "rounded-lg border px-3 py-2 flex items-center gap-3",
        STATUS_COLORS[a.status] ?? "bg-gray-50 border-gray-200"
      )}
    >
      {/* Doctor color bar */}
      <div
        className="w-1 self-stretch rounded-full flex-shrink-0"
        style={{ backgroundColor: a.doctorColor ?? "#0E7490" }}
      />

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <Link
            href={`/patients/${a.patientId}`}
            className="font-semibold text-sm hover:underline"
          >
            {a.patientName}
          </Link>
          <span className="text-xs opacity-60 font-mono">{a.patientNumber}</span>
        </div>
        <div className="text-xs opacity-70 flex items-center gap-2 mt-0.5 flex-wrap">
          <span>{a.doctorName}</span>
          <span>·</span>
          <span>{a.appointmentType}</span>
          <span>·</span>
          <span className="font-mono">{formatTime(a.startTime)} – {formatTime(a.endTime)}</span>
        </div>
      </div>

      {/* Status badge */}
      <span className="text-xs px-2 py-0.5 rounded-full bg-white/60 flex-shrink-0 font-medium">
        {APPOINTMENT_STATUS_LABELS[a.status] ?? a.status}
      </span>

      {/* Quick actions menu */}
      {transitions.length > 0 && (
        <div className="relative flex-shrink-0" ref={menuRef}>
          <button
            onClick={() => setMenuOpen(!menuOpen)}
            className="p-1 rounded hover:bg-black/10 transition"
            aria-label="خيارات"
          >
            <MoreVertical className="w-4 h-4" />
          </button>
          {menuOpen && (
            <div className="absolute left-0 top-7 z-20 bg-white rounded-lg shadow-lg border border-gray-200 py-1 min-w-[130px]">
              {transitions.map(({ value, label }) => (
                <button
                  key={value}
                  onClick={() => {
                    onStatusChange(a.id, value);
                    setMenuOpen(false);
                  }}
                  className="w-full text-start px-3 py-2 text-sm hover:bg-gray-50 transition text-gray-700"
                >
                  {label}
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
