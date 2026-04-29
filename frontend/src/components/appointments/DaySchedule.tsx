"use client";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { MoreVertical, Pencil } from "lucide-react";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { APPOINTMENT_STATUS_LABELS, formatTime, appointmentStatusColor, badgeColorClass } from "@/lib/utils";

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

const STATUS_BG: Record<string, { bg: string; border: string }> = {
  Scheduled:  { bg: "#3d7ab518", border: "#3d7ab530" },
  Confirmed:  { bg: "#0ea5e918", border: "#0ea5e930" },
  Arrived:    { bg: "#a855f718", border: "#a855f730" },
  InProgress: { bg: "#f5922e18", border: "#f5922e30" },
  Completed:  { bg: "#22c55e18", border: "#22c55e30" },
  Cancelled:  { bg: "#64748b18", border: "#64748b30" },
  NoShow:     { bg: "#ef444418", border: "#ef444430" },
};

const DEFAULT_DOCTOR_COLOR = "#3d7ab5";

interface Props {
  date: string;
  doctorId?: string;
}

export function DaySchedule({ date, doctorId }: Props) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = () => {
    setLoading(true);
    const q = doctorId ? `&doctorId=${doctorId}` : "";
    api
      .get<Appointment[]>(`/api/appointments?from=${date}&to=${date}${q}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => { reload(); }, [date, doctorId]); // eslint-disable-line react-hooks/exhaustive-deps

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
          <div key={i} className="h-16 rounded-xl" style={{ backgroundColor: "#f0f5fb" }} />
        ))}
      </div>
    );
  }

  if (!appointments.length) {
    return (
      <div className="text-center py-16" style={{ color: "#94a3b8" }}>
        <p className="text-sm">لا توجد مواعيد في هذا اليوم</p>
        <Link
          href="/appointments/new"
          className="mt-3 inline-block text-xs font-medium hover:underline"
          style={{ color: "#3d7ab5" }}
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
            <span
              className="text-xs w-12 flex-shrink-0 pt-2 font-mono"
              style={{ color: "#94a3b8" }}
            >
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
                <div className="h-10 border-b border-dashed" style={{ borderColor: "#f1f5f9" }} />
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
  const statusColors = STATUS_BG[a.status] ?? STATUS_BG.Scheduled;
  const doctorColor = a.doctorColor ?? DEFAULT_DOCTOR_COLOR;

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
      className="rounded-lg border px-3 py-2.5 flex items-center gap-3 transition-colors"
      style={{
        backgroundColor: statusColors.bg,
        borderColor: statusColors.border,
      }}
    >
      {/* Doctor color bar */}
      <div
        className="w-1.5 self-stretch rounded-full flex-shrink-0"
        style={{ backgroundColor: doctorColor }}
      />

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <Link
            href={`/patients/${a.patientId}`}
            className="font-semibold text-sm hover:underline"
            style={{ color: "#0d2137" }}
          >
            {a.patientName}
          </Link>
          <span className="text-xs font-mono" style={{ color: "#94a3b8" }}>{a.patientNumber}</span>
        </div>
        <div className="text-xs flex items-center gap-2 mt-0.5 flex-wrap" style={{ color: "#64748b" }}>
          <span
            className="w-1.5 h-1.5 rounded-full inline-block flex-shrink-0"
            style={{ backgroundColor: doctorColor }}
          />
          <span>{a.doctorName}</span>
          <span>·</span>
          <span>{a.appointmentType}</span>
          <span>·</span>
          <span className="font-mono">{formatTime(a.startTime)} – {formatTime(a.endTime)}</span>
        </div>
      </div>

      {/* Status badge */}
      <span className={badgeColorClass(appointmentStatusColor(a.status))}>
        {APPOINTMENT_STATUS_LABELS[a.status] ?? a.status}
      </span>

      {/* Quick actions menu */}
      <div className="relative flex-shrink-0" ref={menuRef}>
        <button
          onClick={() => setMenuOpen(!menuOpen)}
          className="p-1 rounded transition-colors"
          style={{ color: "#64748b" }}
          onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "#0d213710"; }}
          onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
          aria-label="خيارات"
        >
          <MoreVertical className="w-4 h-4" />
        </button>
        {menuOpen && (
          <div
            className="absolute left-0 top-7 z-20 rounded-lg border py-1 min-w-[140px] animate-fade-in"
            style={{
              backgroundColor: "#ffffff",
              borderColor: "#e8f0f9",
              boxShadow: "0 8px 30px rgba(0,0,0,0.12)",
            }}
          >
            <Link
              href={`/appointments/${a.id}/edit`}
              onClick={() => setMenuOpen(false)}
              className="w-full flex items-center gap-2 px-3 py-2 text-sm transition-colors"
              style={{ color: "#0d2137" }}
              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "#f7fafd"; }}
              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل الموعد
            </Link>
            {transitions.length > 0 && (
              <div className="mt-1 pt-1" style={{ borderTop: "1px solid #f1f5f9" }}>
                {transitions.map(({ value, label }) => (
                  <button
                    key={value}
                    onClick={() => {
                      onStatusChange(a.id, value);
                      setMenuOpen(false);
                    }}
                    className="w-full text-start px-3 py-2 text-sm transition-colors"
                    style={{ color: "#0d2137" }}
                    onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "#f7fafd"; }}
                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                  >
                    {label}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
