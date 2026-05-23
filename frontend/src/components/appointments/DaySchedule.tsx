"use client";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { MoreVertical, Pencil, Stethoscope, Send, Trash2, Plus, UserX, Mail } from "lucide-react";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";
import { cn, APPOINTMENT_STATUS_LABELS, formatTime } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { useAuthStore } from "@/stores/authStore";

const HOURS = Array.from({ length: 13 }, (_, i) => i + 8); // 8:00 – 20:00

const STATUS_TRANSITIONS: Record<string, { value: string; label: string }[]> = {
  Scheduled:  [{ value: "Confirmed", label: "تأكيد" }, { value: "Arrived", label: "وصل" }, { value: "Cancelled", label: "إلغاء" }],
  Confirmed:  [{ value: "Arrived",    label: "وصل" },   { value: "Cancelled", label: "إلغاء" }],
  Arrived:    [{ value: "Waiting", label: "في الانتظار" }, { value: "NoShow", label: "غياب" }],
  Waiting:    [{ value: "Called", label: "تم النداء" }, { value: "NoShow", label: "غياب" }],
  Called:     [{ value: "InRoom", label: "داخل الغرفة" }, { value: "NoShow", label: "غياب" }],
  InRoom:     [{ value: "InProgress", label: "بدأ" }, { value: "Cancelled", label: "إلغاء" }],
  InProgress: [{ value: "Completed",  label: "اكتمل" }, { value: "Cancelled", label: "إلغاء" }],
  Completed:  [],
  Cancelled:  [{ value: "Scheduled",  label: "إعادة جدولة" }],
  NoShow:     [{ value: "Scheduled",  label: "إعادة جدولة" }],
};

const STATUS_COLORS: Record<string, string> = {
  Scheduled:  "bg-blue-50 border-blue-200 text-blue-800",
  Confirmed:  "bg-clinic-blue-50 border-clinic-blue-100 text-clinic-navy-700",
  Arrived:    "bg-yellow-50 border-yellow-200 text-yellow-800",
  Waiting:    "bg-amber-50 border-amber-200 text-amber-800",
  Called:     "bg-cyan-50 border-cyan-200 text-cyan-800",
  InRoom:     "bg-purple-50 border-purple-200 text-purple-800",
  InProgress: "bg-purple-50 border-purple-200 text-purple-800",
  Completed:  "bg-green-50 border-green-200 text-green-800",
  Cancelled:  "bg-gray-50 border-gray-200 text-gray-500",
  NoShow:     "bg-red-50 border-red-200 text-red-700",
};

interface Props {
  date: string;
  doctorId?: string;
}

function newApptUrl(date: string, hour: number, doctorId?: string): string {
  const h = String(hour).padStart(2, "0");
  let url = `/appointments/new?date=${date}&startTime=${h}:00`;
  if (doctorId) url += `&doctorId=${doctorId}`;
  return url;
}

export function DaySchedule({ date, doctorId }: Props) {
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true);
  const [noShowLoading, setNoShowLoading] = useState(false);

  // NOTE: This component uses direct api.get() with from/to params instead of useAppointments hook.
  // The useAppointments hook uses startDate/endDate query params, while the backend expects from/to for date-range queries.
  // Until the hook is updated to support from/to params, direct API calls are the correct approach here.
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

  const handleNoShowAll = async () => {
    const remaining = appointments.filter(
      (a) => a.status === "Scheduled" || a.status === "Confirmed"
    );
    if (remaining.length === 0) {
      toast.info("لا توجد مواعيد مجدولة أو مؤكدة لتحويلها");
      return;
    }
    if (!confirm(`هل أنت متأكد من تسجيل غياب ${remaining.length} موعد؟`)) return;
    setNoShowLoading(true);
    try {
      await api.post("/api/appointments/batch-status", {
        appointmentIds: remaining.map((a) => a.id),
        status: "NoShow",
      });
      setAppointments((prev) =>
        prev.map((a) =>
          a.status === "Scheduled" || a.status === "Confirmed"
            ? { ...a, status: "NoShow" }
            : a
        )
      );
      toast.success(`تم تسجيل غياب ${remaining.length} موعد`);
    } catch {
      toast.error("فشل تسجيل الغياب الجماعي");
    } finally {
      setNoShowLoading(false);
    }
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
          className="mt-3 inline-block text-xs text-clinic-blue hover:underline"
        >
          + إضافة موعد
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-1.5">
      {/* No-show all button */}
      {appointments.some((a) => a.status === "Scheduled" || a.status === "Confirmed") && (
        <div className="flex justify-start mb-3">
          <button
            onClick={handleNoShowAll}
            disabled={noShowLoading}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-50 text-red-700 border border-red-200 hover:bg-red-100 transition disabled:opacity-50"
          >
            <UserX className="w-3.5 h-3.5" />
            {noShowLoading ? "جارٍ التحديث..." : "تسجيل غياب الباقين"}
          </button>
        </div>
      )}
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
                <Link
                  href={newApptUrl(date, h, doctorId)}
                  className="flex items-center gap-1.5 h-10 px-2 border-b border-dashed border-gray-100 text-transparent hover:text-clinic-blue hover:border-clinic-blue/30 hover:bg-clinic-blue/5 rounded group transition-colors"
                >
                  <Plus className="w-3 h-3 opacity-0 group-hover:opacity-100 transition-opacity" />
                  <span className="text-xs opacity-0 group-hover:opacity-100 transition-opacity">
                    موعد جديد
                  </span>
                </Link>
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
  const { user } = useAuthStore();
  const [menuOpen, setMenuOpen] = useState(false);
  const [visitExists, setVisitExists] = useState(false);
  const [startingVisit, setStartingVisit] = useState(false);
  const [arrivalLoading, setArrivalLoading] = useState(false);
  const [queueLoading, setQueueLoading] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const transitions = STATUS_TRANSITIONS[a.status] ?? [];

  // Check if a visit already exists for this appointment
  useEffect(() => {
    api.get<{ data: { appointmentId?: string }[] }>(`/api/visits?patientId=${a.patientId}`)
      .then((r) => {
        const hasVisit = (r.data.data ?? []).some((v: { appointmentId?: string }) => v.appointmentId === a.id);
        setVisitExists(hasVisit);
      })
      .catch(() => {});
  }, [a.patientId, a.id]);

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

  const handleStartVisit = async () => {
    setStartingVisit(true);
    try {
      const { data } = await api.post(`/api/appointments/${a.id}/start-visit`);
      toast.success(data.message ?? "تم إنشاء الزيارة بنجاح");
      setVisitExists(true);
      // Update status locally to InProgress
      onStatusChange(a.id, "InProgress");
      setMenuOpen(false);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إنشاء الزيارة");
    } finally {
      setStartingVisit(false);
    }
  };

  // Determine if "بدء الزيارة" should be shown
  const canStartVisit = !visitExists && ["Scheduled", "Confirmed", "Arrived", "Waiting", "Called", "InRoom", "InProgress"].includes(a.status);
  const canDelete = !["InProgress", "Completed"].includes(a.status);

  const canArrive = ["Scheduled", "Confirmed"].includes(a.status) && hasPermission(user, PERMISSION_KEYS.PATIENT_JOURNEY_EDIT);
  const canSendToQueue = a.status === "Arrived" && hasPermission(user, PERMISSION_KEYS.CLINIC_QUEUE_CREATE);

  const handleArrival = async () => {
    setArrivalLoading(true);
    try {
      await api.post(`/api/patient-journey/${a.id}/intake`, {});
      toast.success("تم تسجيل حضور المريض بنجاح");
      onStatusChange(a.id, "Arrived");
      setMenuOpen(false);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل تسجيل الحضور");
    } finally {
      setArrivalLoading(false);
    }
  };

  const handleSendToQueue = async () => {
    setQueueLoading(true);
    try {
      await api.post(`/api/patient-journey/${a.id}/send-to-queue`, {});
      toast.success("تم إرسال المريض إلى الطابور");
      onStatusChange(a.id, "Waiting");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إرسال المريض للطابور");
    } finally {
      setQueueLoading(false);
    }
  };

  const handleSendReminder = async () => {
    try {
      const { data } = await api.post(`/api/appointments/${a.id}/send-reminder`);
      toast.success(data.message ?? "تم إرسال التذكير");
      setMenuOpen(false);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إرسال التذكير");
    }
  };

  const handleSendEmailReminder = async () => {
    try {
      const { data } = await api.post(`/api/appointments/${a.id}/send-email-reminder`);
      toast.success(data.message ?? "تم إرسال تذكير الموعد بنجاح");
      setMenuOpen(false);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "تعذر إرسال التذكير، حاول مرة أخرى");
    }
  };

  const handleDelete = async () => {
    if (!confirm("هل أنت متأكد من حذف هذا الموعد؟")) return;
    try {
      await api.delete(`/api/appointments/${a.id}`);
      toast.success("تم حذف الموعد");
      onStatusChange(a.id, "Cancelled"); // remove from view
      setMenuOpen(false);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حذف الموعد");
    }
  };

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
        style={{ backgroundColor: a.doctorColor ?? "#2563EB" }}
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
          {a.serviceName && (
            <>
              <span>·</span>
              <span className="text-[#3d7ab5]">{a.serviceName}</span>
            </>
          )}
          <span>·</span>
          <span className="font-mono">{formatTime(a.startTime)} – {formatTime(a.endTime)}</span>
          {a.roomName && (
            <>
              <span>·</span>
              <span className="flex items-center gap-0.5 text-purple-600">📍 {a.roomName}</span>
            </>
          )}
        </div>
      </div>

      {/* Status badge + quick arrival action */}
      <div className="flex items-center gap-1.5 flex-shrink-0">
        <span className="text-xs px-2 py-0.5 rounded-full bg-white/60 font-medium">
          {APPOINTMENT_STATUS_LABELS[a.status] ?? a.status}
        </span>
        {canArrive && (
          <button
            onClick={handleArrival}
            disabled={arrivalLoading}
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-semibold bg-amber-500 text-white hover:bg-amber-600 transition disabled:opacity-60"
          >
            {arrivalLoading ? <span className="w-3 h-3 border-2 border-white/40 border-t-white rounded-full animate-spin" /> : null}
            تسجيل حضور
          </button>
        )}
        {canSendToQueue && (
          <button
            onClick={handleSendToQueue}
            disabled={queueLoading}
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-semibold bg-blue-500 text-white hover:bg-blue-600 transition disabled:opacity-60"
          >
            {queueLoading ? <span className="w-3 h-3 border-2 border-white/40 border-t-white rounded-full animate-spin" /> : null}
            إرسال إلى الطابور
          </button>
        )}
      </div>

      {/* Quick actions menu */}
      <div className="relative flex-shrink-0" ref={menuRef}>
        <button
          onClick={() => setMenuOpen(!menuOpen)}
          className="p-1 rounded hover:bg-black/10 transition"
          aria-label="خيارات"
        >
          <MoreVertical className="w-4 h-4" />
        </button>
        {menuOpen && (
          <div className="absolute left-0 top-7 z-20 bg-white rounded-lg shadow-lg border border-gray-200 py-1 min-w-[160px]">
            <Link
              href={`/appointments/${a.id}/edit`}
              onClick={() => setMenuOpen(false)}
              className="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-50 transition text-gray-700"
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل الموعد
            </Link>
            {canStartVisit && (
              <button
                onClick={handleStartVisit}
                disabled={startingVisit}
                className="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-50 transition text-green-700 font-medium disabled:opacity-50"
              >
                <Stethoscope className="w-3.5 h-3.5" />
                {startingVisit ? "جاري الإنشاء..." : "بدء الزيارة"}
              </button>
            )}
            <button
              onClick={handleSendReminder}
              className="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-50 transition text-[#f5922e]"
            >
              <Send className="w-3.5 h-3.5" />
              إرسال تذكير واتساب
            </button>
            <button
              onClick={handleSendEmailReminder}
              className="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-50 transition text-[#0E7490]"
            >
              <Mail className="w-3.5 h-3.5" />
              إرسال تذكير بالإيميل
            </button>
            {canDelete && (
              <button
                onClick={handleDelete}
                className="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-50 transition text-red-600"
              >
                <Trash2 className="w-3.5 h-3.5" />
                حذف الموعد
              </button>
            )}
            {visitExists && (
              <Link
                href={`/patients/${a.patientId}`}
                onClick={() => setMenuOpen(false)}
                className="w-full flex items-center gap-2 px-3 py-2 text-sm hover:bg-gray-50 transition text-[#3d7ab5]"
              >
                <Stethoscope className="w-3.5 h-3.5" />
                فتح ملف المريض
              </Link>
            )}
            {transitions.length > 0 && (
              <div className="border-t border-gray-100 mt-1 pt-1">
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
    </div>
  );
}
