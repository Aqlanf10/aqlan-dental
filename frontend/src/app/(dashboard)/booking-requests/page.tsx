"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { normalizePhone } from "@/lib/utils";
import type { CreatePatientRequest, PatientProfile } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import {
  Clock, CheckCircle2, XCircle, Eye, Loader2, RefreshCw,
  Phone, Mail, Calendar, MessageSquare, Filter, Globe, CalendarPlus,
  Search, User, Stethoscope, ExternalLink, MessageCircle,
  ChevronLeft, ChevronRight, Ban, X,
} from "lucide-react";
import { WorkflowNav, WORKFLOW_LINKS } from "@/components/shared/WorkflowNav";
import { toast } from "@/stores/toastStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { useAuthStore } from "@/stores/authStore";

type BookingStatus = "Pending" | "Reviewed" | "Confirmed" | "Rejected";

interface BookingRequest {
  id: string;
  patientName: string;
  phoneNumber: string;
  email: string | null;
  serviceType: string | null;
  preferredDate: string | null;
  preferredTime: string | null;
  notes: string | null;
  status: BookingStatus;
  staffNotes: string | null;
  createdAt: string;
  reviewedAt: string | null;
  doctorId: string | null;
  doctorName: string | null;
  convertedToAppointmentId: string | null;
}

const STATUS_CONFIG: Record<BookingStatus, { label: string; color: string; bg: string; icon: React.ReactNode }> = {
  Pending:   { label: "قيد الانتظار",   color: "text-amber-700",  bg: "bg-amber-50 border-amber-200",  icon: <Clock className="w-3.5 h-3.5" /> },
  Reviewed:  { label: "تمت المراجعة",   color: "text-blue-700",   bg: "bg-blue-50 border-blue-200",    icon: <Eye className="w-3.5 h-3.5" /> },
  Confirmed: { label: "مؤكد",           color: "text-green-700",  bg: "bg-green-50 border-green-200",  icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  Rejected:  { label: "مرفوض",          color: "text-red-700",    bg: "bg-red-50 border-red-200",      icon: <XCircle className="w-3.5 h-3.5" /> },
};

const NEXT_STATUSES: Record<BookingStatus, { status: BookingStatus; label: string; color: string }[]> = {
  Pending:   [
    { status: "Reviewed",  label: "تمت المراجعة",    color: "bg-blue-500 hover:bg-blue-600" },
    { status: "Confirmed", label: "تأكيد",            color: "bg-green-500 hover:bg-green-600" },
    { status: "Rejected",  label: "رفض الطلب",       color: "bg-red-500 hover:bg-red-600" },
  ],
  Reviewed:  [
    { status: "Confirmed", label: "تأكيد",            color: "bg-green-500 hover:bg-green-600" },
    { status: "Rejected",  label: "رفض الطلب",       color: "bg-red-500 hover:bg-red-600" },
  ],
  Confirmed: [
    { status: "Rejected",  label: "رفض / إلغاء التأكيد", color: "bg-red-500 hover:bg-red-600" },
  ],
  Rejected:  [
    { status: "Pending",   label: "إعادة فتح الطلب", color: "bg-amber-500 hover:bg-amber-600" },
    { status: "Confirmed", label: "تأكيد الطلب",      color: "bg-green-500 hover:bg-green-600" },
  ],
};

type FilterKey = "all" | "today" | "week" | "Pending" | "Reviewed" | "Confirmed" | "Rejected" | "notConverted";

const FILTER_OPTIONS: { value: FilterKey; label: string; icon: React.ReactNode }[] = [
  { value: "all",         label: "جميع الطلبات",           icon: <Filter className="w-3.5 h-3.5" /> },
  { value: "today",       label: "اليوم",                  icon: <Calendar className="w-3.5 h-3.5" /> },
  { value: "week",        label: "هذا الأسبوع",            icon: <Calendar className="w-3.5 h-3.5" /> },
  { value: "Pending",     label: "قيد الانتظار",          icon: <Clock className="w-3.5 h-3.5" /> },
  { value: "Reviewed",    label: "تمت المراجعة",          icon: <Eye className="w-3.5 h-3.5" /> },
  { value: "Confirmed",   label: "مؤكد",                  icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  { value: "Rejected",    label: "مرفوضة",                icon: <XCircle className="w-3.5 h-3.5" /> },
  { value: "notConverted", label: "غير محولة إلى موعد",   icon: <CalendarPlus className="w-3.5 h-3.5" /> },
];

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("ar-YE", {
    year: "numeric", month: "short", day: "numeric",
    hour: "2-digit", minute: "2-digit",
  });
}

function formatShortDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("ar-YE", {
    month: "short", day: "numeric",
  });
}

function parseTimeToISO(t: string | null): string | undefined {
  if (!t) return undefined;
  // Already 24h format "HH:MM"
  if (/^\d{1,2}:\d{2}$/.test(t.trim())) {
    const [h, m] = t.trim().split(":");
    return `${h.padStart(2, "0")}:${m}`;
  }
  // 12h format with AM/PM — handles "9:00 AM", "12:00 PM", "3:00 م", "12:30 ص"
  const m12 = t.match(/(\d{1,2}):(\d{2})\s*(ص|م|AM|PM)/i);
  if (m12) {
    let h = parseInt(m12[1], 10);
    const min = m12[2];
    const isPm = m12[3] === "م" || m12[3].toUpperCase() === "PM";
    if (isPm && h < 12) h += 12;
    if (!isPm && h === 12) h = 0;
    return `${h.toString().padStart(2, "0")}:${min}`;
  }
  // Try just a number like "9" or "14"
  const numMatch = t.match(/^(\d{1,2})$/);
  if (numMatch) {
    const h = parseInt(numMatch[1], 10);
    if (h >= 0 && h <= 23) {
      return `${h.toString().padStart(2, "0")}:00`;
    }
  }
  return undefined;
}

function splitPatientName(fullName: string): Pick<CreatePatientRequest, "firstName" | "middleName" | "lastName"> {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return { firstName: "Patient", lastName: "File" };
  }
  if (parts.length === 1) {
    return { firstName: parts[0], lastName: "غير محدد" };
  }
  return {
    firstName: parts[0],
    middleName: parts.length > 2 ? parts.slice(1, -1).join(" ") : undefined,
    lastName: parts[parts.length - 1],
  };
}

function buildPatientPayload(item: BookingRequest): CreatePatientRequest {
  const noteParts = [
    item.serviceType ? `الخدمة المطلوبة: ${item.serviceType}` : null,
    item.preferredDate || item.preferredTime
      ? `موعد الحجز المفضل: ${[item.preferredDate, item.preferredTime].filter(Boolean).join(" ")}`
      : null,
    item.notes ? `ملاحظات طلب الحجز: ${item.notes}` : null,
  ].filter(Boolean);

  return {
    ...splitPatientName(item.patientName),
    phone: item.phoneNumber,
    whatsApp: item.phoneNumber,
    referralSource: "طلب حجز من الموقع",
    primaryDoctorId: item.doctorId ?? undefined,
    dentalHistory: noteParts.length > 0 ? { notes: noteParts.join("\n") } : undefined,
  };
}

function isToday(dateStr: string | null): boolean {
  if (!dateStr) return false;
  const d = new Date(dateStr);
  const now = new Date();
  return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth() && d.getDate() === now.getDate();
}

function isThisWeek(dateStr: string | null): boolean {
  if (!dateStr) return false;
  const d = new Date(dateStr);
  const now = new Date();
  const startOfWeek = new Date(now);
  // Week starts on Saturday in Yemen
  const day = startOfWeek.getDay();
  const diff = day === 6 ? 0 : day + 1; // Saturday = 0 offset, Sunday = 1, ..., Friday = 6
  startOfWeek.setDate(startOfWeek.getDate() - diff);
  startOfWeek.setHours(0, 0, 0, 0);
  const endOfWeek = new Date(startOfWeek);
  endOfWeek.setDate(endOfWeek.getDate() + 7);
  return d >= startOfWeek && d < endOfWeek;
}

function StatusBadge({ status }: { status: BookingStatus }) {
  const cfg = STATUS_CONFIG[status];
  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold border ${cfg.bg} ${cfg.color}`}>
      {cfg.icon}
      {cfg.label}
    </span>
  );
}

function ConvertedBadge() {
  return (
    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold border bg-emerald-50 border-emerald-200 text-emerald-700">
      <CheckCircle2 className="w-3.5 h-3.5" />
      تم التحويل إلى موعد
    </span>
  );
}

interface DetailModalProps {
  item: BookingRequest;
  onClose: () => void;
  onStatusChange: (item: BookingRequest, status: BookingStatus, notes: string) => Promise<{ success: boolean; patientId?: string }>;
  onCreateAppointment: (item: BookingRequest) => void;
  onViewAppointment: (appointmentId: string) => void;
  onConfirmAndConvert: (item: BookingRequest, notes: string) => Promise<void>;
}

function DetailModal({ item, onClose, onStatusChange, onCreateAppointment, onViewAppointment, onConfirmAndConvert }: DetailModalProps) {
  const { user } = useAuthStore();
  const [staffNotes, setStaffNotes] = useState(item.staffNotes ?? "");
  const [loading, setLoading] = useState(false);
  const nextStatuses = NEXT_STATUSES[item.status];
  const isConverted = item.convertedToAppointmentId !== null;
  const canCreateAppointment = item.status === "Confirmed" && !isConverted;
  // Show "Confirm & Convert" for Pending/Reviewed items that aren't converted yet
  const canConfirmAndConvert = (item.status === "Pending" || item.status === "Reviewed") && !isConverted;
  // Always show staff notes for statuses that have actions or can be transitioned
  const showStaffNotes = nextStatuses.length > 0 || canCreateAppointment;

  async function handleStatusChange(status: BookingStatus) {
    setLoading(true);
    try {
      const result = await onStatusChange(item, status, staffNotes);
      // Only close modal for non-Confirmed statuses or failed confirms
      if (status !== "Confirmed" || !result.success) {
        onClose();
      }
      // For successful Confirmed: keep modal open so user can convert
    } finally {
      setLoading(false);
    }
  }

  async function handleConfirmAndConvert() {
    setLoading(true);
    try {
      await onConfirmAndConvert(item, staffNotes);
      onClose();
    } finally {
      setLoading(false);
    }
  }

  const waPhone = normalizePhone(item.phoneNumber);
  const waMessage = encodeURIComponent("مرحباً، معك مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان بخصوص طلب الحجز الخاص بك.");

  return (
    <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" onClick={onClose}>
      <div
        className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="p-6 border-b border-gray-100 flex items-start justify-between">
          <div className="space-y-2">
            <h2 className="text-lg font-bold text-gray-900">{item.patientName}</h2>
            <div className="flex items-center gap-2 flex-wrap">
              <StatusBadge status={item.status} />
              {isConverted && <ConvertedBadge />}
            </div>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 transition-colors p-1 rounded-lg hover:bg-gray-100"
          >
            ✕
          </button>
        </div>

        <div className="p-6 space-y-4">
          {/* Contact info */}
          <div className="grid grid-cols-2 gap-3">
            <div className="bg-gray-50 rounded-xl p-3">
              <div className="flex items-center gap-1.5 text-xs text-gray-500 mb-1">
                <Phone className="w-3 h-3" /> رقم الهاتف
              </div>
              <a href={`tel:${item.phoneNumber}`} className="font-semibold text-clinic-blue text-sm hover:underline" dir="ltr">
                {item.phoneNumber}
              </a>
            </div>
            {item.email && (
              <div className="bg-gray-50 rounded-xl p-3">
                <div className="flex items-center gap-1.5 text-xs text-gray-500 mb-1">
                  <Mail className="w-3 h-3" /> البريد الإلكتروني
                </div>
                <div className="font-medium text-sm text-gray-700 truncate">{item.email}</div>
              </div>
            )}
          </div>

          {/* Quick contact actions */}
          <div className="flex gap-2">
            <a
              href={`tel:${item.phoneNumber}`}
              className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium bg-clinic-blue-50 text-clinic-blue hover:bg-clinic-blue-100 transition-colors"
            >
              <Phone className="w-3.5 h-3.5" />
              اتصال
            </a>
            <a
              href={`https://wa.me/${waPhone}?text=${waMessage}`}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium bg-green-50 text-green-700 hover:bg-green-100 transition-colors"
            >
              <MessageCircle className="w-3.5 h-3.5" />
              واتساب
            </a>
          </div>

          {/* Service, Doctor & Date */}
          <div className="grid grid-cols-2 gap-3">
            {item.serviceType && (
              <div className="bg-clinic-blue-50 rounded-xl p-3">
                <div className="text-xs text-gray-500 mb-1">الخدمة المطلوبة</div>
                <div className="font-semibold text-clinic-navy text-sm">{item.serviceType}</div>
              </div>
            )}
            {item.doctorName && (
              <div className="bg-clinic-blue-50 rounded-xl p-3">
                <div className="flex items-center gap-1.5 text-xs text-gray-500 mb-1">
                  <Stethoscope className="w-3 h-3" /> الطبيب المفضل
                </div>
                <div className="font-semibold text-clinic-navy text-sm">{item.doctorName}</div>
              </div>
            )}
          </div>

          {(item.preferredDate || item.preferredTime) && (
            <div className="bg-clinic-blue-50 rounded-xl p-3">
              <div className="flex items-center gap-1.5 text-xs text-gray-500 mb-1">
                <Calendar className="w-3 h-3" /> الموعد المفضل
              </div>
              <div className="font-semibold text-clinic-navy text-sm">
                {item.preferredDate} {item.preferredTime && `— ${item.preferredTime}`}
              </div>
            </div>
          )}

          {/* Notes */}
          {item.notes && (
            <div className="bg-gray-50 rounded-xl p-3">
              <div className="flex items-center gap-1.5 text-xs text-gray-500 mb-2">
                <MessageSquare className="w-3 h-3" /> ملاحظات المريض
              </div>
              <p className="text-sm text-gray-700 leading-relaxed">{item.notes}</p>
            </div>
          )}

          <div className="text-xs text-gray-400">
            تاريخ الطلب: {formatDate(item.createdAt)}
            {item.reviewedAt && ` · تمت المراجعة: ${formatDate(item.reviewedAt)}`}
          </div>

          {/* Staff notes */}
          {showStaffNotes && (
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                ملاحظات الموظف <span className="font-normal text-gray-400">(اختياري)</span>
              </label>
              <textarea
                value={staffNotes}
                onChange={(e) => setStaffNotes(e.target.value)}
                rows={2}
                placeholder="أضف ملاحظة للمريض أو الفريق..."
                className="w-full px-3 py-2 rounded-xl border border-gray-200 focus:border-clinic-blue outline-none focus:ring-2 focus:ring-clinic-blue/20 text-sm resize-none text-right"
              />
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="p-6 pt-0 flex flex-wrap gap-2">
          {/* View Appointment — shown if already converted */}
          {isConverted && item.convertedToAppointmentId && (
            <button
              onClick={() => { onViewAppointment(item.convertedToAppointmentId!); onClose(); }}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-white text-sm font-semibold bg-emerald-600 hover:bg-emerald-700 transition-colors"
            >
              <ExternalLink className="w-3.5 h-3.5" />
              فتح الموعد
            </button>
          )}
          {/* Confirm & Convert — combined action for Pending/Reviewed */}
          {canConfirmAndConvert && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
            <button
              disabled={loading}
              onClick={handleConfirmAndConvert}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-white text-sm font-semibold bg-gradient-to-l from-clinic-blue to-clinic-navy hover:opacity-90 transition-colors disabled:opacity-50"
            >
              {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CalendarPlus className="w-3.5 h-3.5" />}
              تأكيد وتحويل إلى موعد
            </button>
          )}
          {/* Create Appointment — shown for confirmed & not converted */}
          {canCreateAppointment && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
            <button
              onClick={() => { onCreateAppointment(item); onClose(); }}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-white text-sm font-semibold bg-clinic-blue hover:bg-clinic-navy transition-colors"
            >
              <CalendarPlus className="w-3.5 h-3.5" />
              تحويل إلى موعد
            </button>
          )}
          {nextStatuses.map((ns) => (
            <button
              key={ns.status}
              disabled={loading}
              onClick={() => handleStatusChange(ns.status)}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-white text-sm font-semibold transition-colors disabled:opacity-50 ${ns.color}`}
            >
              {loading && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
              {ns.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

// ── Convert to Appointment Modal ─────────────────────────────────────────────
interface ConvertModalProps {
  item: BookingRequest;
  onClose: () => void;
  onConverted: (appointmentId?: string) => void;
}

function ConvertModal({ item, onClose, onConverted }: ConvertModalProps) {
  // FE-13: useDoctors() replaces useState + fetch.
  const { data: doctors = [] } = useDoctors();
  const [doctorId, setDoctorId] = useState(item.doctorId ?? "");
  const [date, setDate] = useState(item.preferredDate ?? "");
  const [startTime, setStartTime] = useState(() => {
    const t = parseTimeToISO(item.preferredTime);
    return t ?? "";
  });
  const [duration, setDuration] = useState(30);
  const [converting, setConverting] = useState(false);
  const [err, setErr] = useState("");

  async function handleConvert() {
    if (!doctorId) { setErr("اختر الطبيب"); return; }
    if (!date)     { setErr("حدد تاريخ الموعد"); return; }
    if (!startTime){ setErr("حدد وقت الموعد"); return; }

    // Guard: prevent double conversion
    if (item.convertedToAppointmentId) {
      setErr("تم تحويل هذا الطلب بالفعل إلى موعد");
      return;
    }

    const [h, m] = startTime.split(":").map(Number);
    const endTotal = h * 60 + m + duration;
    const endH = Math.floor(endTotal / 60).toString().padStart(2, "0");
    const endM = (endTotal % 60).toString().padStart(2, "0");

    setConverting(true);
    setErr("");
    try {
      const res = await api.post(`/api/booking-requests/${item.id}/convert-to-appointment`, {
        patientId: "00000000-0000-0000-0000-000000000000",
        doctorId,
        appointmentDate: date,
        startTime: `${startTime}:00`,
        endTime: `${endH}:${endM}:00`,
        durationMinutes: duration,
        appointmentType: item.serviceType ?? "عام",
      });
      const convertedToAppointmentId = res.data?.convertedToAppointmentId || res.data?.appointmentId || res.data?.id;
      toast.success("تم تحويل طلب الحجز إلى موعد بنجاح");
      onConverted(convertedToAppointmentId);
      onClose();
    } catch (e: unknown) {
      const axiosErr = e as { response?: { data?: { message?: string } }; message?: string };
      const msg = axiosErr?.response?.data?.message || axiosErr?.message;
      // Show real backend error if available, otherwise generic message
      setErr(msg ?? "فشل تحويل الطلب — حاول مرة أخرى");
    } finally {
      setConverting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[60] bg-black/50 backdrop-blur-sm flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={(e) => e.stopPropagation()} dir="rtl">
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <div>
            <h3 className="font-bold text-gray-900">تحويل إلى موعد</h3>
            <p className="text-xs text-gray-500 mt-0.5">{item.patientName} — {item.phoneNumber}</p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 p-1 rounded-lg hover:bg-gray-100">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-5 space-y-4">
          {/* Doctor */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الطبيب *</label>
            <select
              value={doctorId}
              onChange={(e) => setDoctorId(e.target.value)}
              className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-clinic-blue focus:ring-2 focus:ring-clinic-blue/20 outline-none bg-white"
            >
              <option value="">— اختر طبيباً —</option>
              {doctors.filter((d) => d.isActive !== false).map((d) => (
                <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` — ${d.specialty}` : ""}</option>
              ))}
            </select>
          </div>

          {/* Date + Time */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">التاريخ *</label>
              <input
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
                className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-clinic-blue focus:ring-2 focus:ring-clinic-blue/20 outline-none"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">الوقت *</label>
              <input
                type="time"
                value={startTime}
                onChange={(e) => setStartTime(e.target.value)}
                className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-clinic-blue focus:ring-2 focus:ring-clinic-blue/20 outline-none"
              />
            </div>
          </div>

          {/* Duration */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">مدة الموعد</label>
            <select
              value={duration}
              onChange={(e) => setDuration(Number(e.target.value))}
              className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-clinic-blue focus:ring-2 focus:ring-clinic-blue/20 outline-none bg-white"
            >
              {[15, 20, 30, 45, 60, 90].map((d) => (
                <option key={d} value={d}>{d} دقيقة</option>
              ))}
            </select>
          </div>

          {/* Service type (read-only info) */}
          {item.serviceType && (
            <div className="bg-clinic-blue-50 rounded-xl px-3 py-2 text-sm text-clinic-navy">
              <span className="font-medium">الخدمة: </span>{item.serviceType}
            </div>
          )}

          {err && (
            <p className="text-sm text-red-600 bg-red-50 rounded-xl px-3 py-2">{err}</p>
          )}
        </div>

        <div className="p-5 pt-0 flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 rounded-xl border border-gray-200 text-sm font-medium text-gray-700 hover:bg-gray-50 transition"
          >
            إلغاء
          </button>
          <button
            onClick={handleConvert}
            disabled={converting}
            className="flex-1 py-2.5 rounded-xl bg-clinic-blue text-white text-sm font-semibold hover:bg-clinic-navy transition disabled:opacity-60 flex items-center justify-center gap-2"
          >
            {converting && <Loader2 className="w-4 h-4 animate-spin" />}
            {converting ? "جاري التحويل..." : "تحويل إلى موعد"}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function BookingRequestsPage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const [items, setItems] = useState<BookingRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeFilter, setActiveFilter] = useState<FilterKey>("all");
  const [searchQuery, setSearchQuery] = useState("");
  const [selected, setSelected] = useState<BookingRequest | null>(null);
  const [convertItem, setConvertItem] = useState<BookingRequest | null>(null);
  const [error, setError] = useState("");

  // Pagination state
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const fetchItems = useCallback(async (pageNum: number = 1) => {
    setLoading(true);
    setError("");
    try {
      // Try paginated endpoint first
      const res = await api.get<BookingRequest[] | PaginatedResponse<BookingRequest>>(
        `/api/booking-requests?page=${pageNum}&pageSize=${pageSize}`
      );
      const data = res.data;
      if (Array.isArray(data)) {
        // Non-paginated response — fallback to client-side
        setItems(data);
        setTotalCount(data.length);
        setTotalPages(1);
      } else {
        // Paginated response
        setItems(data.data ?? []);
        setTotalCount(data.totalCount ?? 0);
        setTotalPages(data.totalPages ?? 1);
        setPage(data.page ?? pageNum);
      }
    } catch {
      setError("تعذّر تحميل طلبات الحجز");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchItems(1); }, [fetchItems]);

  async function ensurePatientFile(item: BookingRequest): Promise<string> {
    const normalizedPhone = normalizePhone(item.phoneNumber);
    if (normalizedPhone) {
      const params = new URLSearchParams({ phone: normalizedPhone });
      const { data } = await api.get<{
        isDuplicate: boolean;
        matches: Array<{ id: string; matchType: string }>;
      }>(`/api/patients/check-duplicate?${params.toString()}`);
      const existing = data.matches.find((match) =>
        match.matchType === "phone" || match.matchType === "whatsapp"
      );
      if (existing) return existing.id;
    }

    const { data: created } = await api.post<PatientProfile>(
      "/api/patients",
      buildPatientPayload(item)
    );
    return created.id;
  }

  async function handleStatusChange(item: BookingRequest, status: BookingStatus, staffNotes: string): Promise<{ success: boolean; patientId?: string }> {
    if (status === "Confirmed") {
      // F2 FIX: Create/find patient file FIRST — only confirm booking if patient creation succeeds.
      // Previously, if patient creation failed, the booking was still confirmed (creating an orphan
      // confirmed booking with no patient file). Now we DO NOT confirm the booking on failure.
      // FIX: No longer navigates away — stays on booking requests page.
      try {
        const patientId = await ensurePatientFile(item);
        // Patient file created/found successfully — now confirm the booking
        await api.patch(`/api/booking-requests/${item.id}/status`, { status, staffNotes });
        toast.success("تم تأكيد الطلب — يمكنك الآن تحويله إلى موعد");
        await fetchItems(page);
        return { success: true, patientId };
      } catch (err: unknown) {
        const axiosErr = err as { response?: { data?: { message?: string } }; message?: string };
        const msg = axiosErr?.response?.data?.message || axiosErr?.message || "تعذّر إنشاء ملف المريض";
        // F2 FIX: Do NOT confirm the booking. Show error and keep in current status.
        setError(`فشل فتح ملف المريض: ${msg}. لم يتم تأكيد الطلب — حاول مرة أخرى.`);
        await fetchItems(page);
        return { success: false };
      }
    }

    await api.patch(`/api/booking-requests/${item.id}/status`, { status, staffNotes });
    await fetchItems(page);
    return { success: true };
  }

  /** Confirm a booking request AND immediately open the convert-to-appointment modal. */
  async function handleConfirmAndConvert(item: BookingRequest, staffNotes: string) {
    const result = await handleStatusChange(item, "Confirmed", staffNotes);
    if (result.success) {
      // Refresh will update the item to Confirmed, then open convert modal
      // Find the refreshed item (it now has Confirmed status)
      const refreshedItem = { ...item, status: "Confirmed" as BookingStatus };
      setConvertItem(refreshedItem);
    }
  }

  /** Guard: prevent converting an already-converted booking request. */
  function openConvertWithGuard(item: BookingRequest) {
    if (item.convertedToAppointmentId) {
      toast.info("تم تحويل هذا الطلب بالفعل إلى موعد");
      return;
    }
    if (item.status !== "Confirmed") {
      toast.info("يجب تأكيد الطلب أولاً قبل التحويل إلى موعد");
      return;
    }
    setConvertItem(item);
  }

  function handleCreateAppointment(item: BookingRequest) {
    openConvertWithGuard(item);
  }

  function handleViewAppointment(appointmentId: string) {
    router.push(`/appointments?highlight=${appointmentId}`);
  }

  // Client-side filtering
  const filteredItems = useMemo(() => {
    let result = items;

    // Apply filter
    switch (activeFilter) {
      case "today":
        result = result.filter(
          (i) => isToday(i.preferredDate) || isToday(i.createdAt)
        );
        break;
      case "week":
        result = result.filter((i) => isThisWeek(i.preferredDate));
        break;
      case "Pending":
      case "Reviewed":
      case "Confirmed":
      case "Rejected":
        result = result.filter((i) => i.status === activeFilter);
        break;
      case "notConverted":
        result = result.filter(
          (i) => i.convertedToAppointmentId === null && i.status === "Confirmed"
        );
        break;
      case "all":
      default:
        break;
    }

    // Apply search
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (i) =>
          i.patientName.toLowerCase().includes(q) ||
          i.phoneNumber.replace(/[\s\-\(\)]/g, "").includes(q.replace(/[\s\-\(\)]/g, ""))
      );
    }

    return result;
  }, [items, activeFilter, searchQuery]);

  const pendingCount = items.filter((i) => i.status === "Pending").length;
  const notConvertedCount = items.filter(
    (i) => i.convertedToAppointmentId === null && i.status === "Confirmed"
  ).length;

  return (
    <div dir="rtl" className="p-4 md:p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-clinic-navy">طلبات الحجز</h1>
          <p className="text-gray-500 text-sm mt-0.5">طلبات الحجز الواردة من الموقع الإلكتروني</p>
        </div>
        <div className="flex items-center gap-2">
          {pendingCount > 0 && (
            <span className="bg-amber-100 text-amber-800 text-xs font-bold px-3 py-1 rounded-full border border-amber-200">
              {pendingCount} طلب جديد
            </span>
          )}
          {notConvertedCount > 0 && (
            <span className="bg-orange-100 text-orange-800 text-xs font-bold px-3 py-1 rounded-full border border-orange-200">
              {notConvertedCount} غير محولة
            </span>
          )}
          <button
            onClick={() => fetchItems(page)}
            disabled={loading}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
            تحديث
          </button>
        </div>
      </div>

      {/* Workflow Navigation */}
      <WorkflowNav
        links={[
          WORKFLOW_LINKS.backToDailyOps(),
          WORKFLOW_LINKS.appointments(),
          WORKFLOW_LINKS.newAppointment(),
          WORKFLOW_LINKS.clinicQueue(),
        ]}
        currentPage="/booking-requests"
      />

      {/* Search */}
      <div className="relative">
        <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="بحث باسم المريض أو رقم الهاتف..."
          className="w-full pr-10 pl-4 py-2.5 rounded-xl border border-gray-200 focus:border-clinic-blue outline-none focus:ring-2 focus:ring-clinic-blue/20 text-sm bg-white"
        />
        {searchQuery && (
          <button
            onClick={() => setSearchQuery("")}
            className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-sm"
          >
            ✕
          </button>
        )}
      </div>

      {/* Filters */}
      <div className="flex items-center gap-2 flex-wrap">
        {FILTER_OPTIONS.map((opt) => {
          const isActive = activeFilter === opt.value;
          let count = 0;
          if (opt.value === "all") count = items.length;
          else if (opt.value === "today") count = items.filter((i) => isToday(i.preferredDate) || isToday(i.createdAt)).length;
          else if (opt.value === "week") count = items.filter((i) => isThisWeek(i.preferredDate)).length;
          else if (opt.value === "notConverted") count = notConvertedCount;
          else count = items.filter((i) => i.status === opt.value).length;

          return (
            <button
              key={opt.value}
              onClick={() => setActiveFilter(opt.value)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                isActive
                  ? "bg-clinic-blue text-white shadow-sm"
                  : "bg-white text-gray-600 border border-gray-200 hover:bg-gray-50"
              }`}
            >
              {opt.icon}
              {opt.label}
              {count > 0 && (
                <span className={`text-xs font-bold ${isActive ? "text-white/80" : "text-gray-400"}`}>
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Content */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-red-600 text-sm text-center">
          {error}
        </div>
      )}

      {loading && !error && (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin text-clinic-blue" />
        </div>
      )}

      {!loading && !error && filteredItems.length === 0 && (
        <div className="text-center py-20 text-gray-400">
          <Globe className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="font-medium">
            {searchQuery ? "لا توجد نتائج مطابقة للبحث" : "لا توجد طلبات حجز"}
          </p>
          <p className="text-sm mt-1">
            {searchQuery
              ? "جرّب تعديل كلمة البحث أو تغيير الفلتر"
              : "ستظهر هنا طلبات الحجز الواردة من الموقع الإلكتروني"}
          </p>
        </div>
      )}

      {!loading && filteredItems.length > 0 && (
        <div className="grid gap-3">
          {filteredItems.map((item) => {
            const isConverted = item.convertedToAppointmentId !== null;
            const waPhone = normalizePhone(item.phoneNumber);
            const waMessage = encodeURIComponent("مرحباً، معك مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان بخصوص طلب الحجز الخاص بك.");

            return (
              <div
                key={item.id}
                className="bg-white rounded-2xl border border-gray-100 shadow-card hover:shadow-card-hover transition-all p-4 cursor-pointer"
                onClick={() => setSelected(item)}
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="flex-1 min-w-0">
                    {/* Top row: name + badges */}
                    <div className="flex items-center gap-2 flex-wrap mb-2">
                      <span className="font-bold text-clinic-navy flex items-center gap-1.5">
                        <User className="w-4 h-4 text-clinic-blue" />
                        {item.patientName}
                      </span>
                      <StatusBadge status={item.status} />
                      {isConverted && <ConvertedBadge />}
                      {item.staffNotes?.includes("إلغاء من قبل المريض") && (
                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold border bg-orange-50 border-orange-200 text-orange-700">
                          <Ban className="w-3.5 h-3.5" />
                          إلغاء من قبل المريض
                        </span>
                      )}
                    </div>

                    {/* Details grid */}
                    <div className="flex items-center gap-3 text-sm text-gray-500 flex-wrap">
                      <span className="flex items-center gap-1 font-mono" dir="ltr">
                        <Phone className="w-3.5 h-3.5" />
                        {item.phoneNumber}
                      </span>
                      {item.serviceType && (
                        <span className="bg-clinic-blue-50 text-clinic-navy rounded px-2 py-0.5 text-xs font-medium">
                          {item.serviceType}
                        </span>
                      )}
                      {item.doctorName && (
                        <span className="flex items-center gap-1 text-xs text-clinic-blue">
                          <Stethoscope className="w-3 h-3" />
                          {item.doctorName}
                        </span>
                      )}
                      {(item.preferredDate || item.preferredTime) && (
                        <span className="flex items-center gap-1 text-xs">
                          <Calendar className="w-3 h-3" />
                          {item.preferredDate}
                          {item.preferredTime && ` ${item.preferredTime}`}
                        </span>
                      )}
                    </div>

                    {item.notes && (
                      <p className="text-xs text-gray-400 mt-1.5 truncate max-w-md">{item.notes}</p>
                    )}

                    {/* Quick actions row */}
                    <div className="flex items-center gap-2 mt-2.5" onClick={(e) => e.stopPropagation()}>
                      <a
                        href={`tel:${item.phoneNumber}`}
                        className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium text-clinic-blue bg-clinic-blue-50 hover:bg-clinic-blue-100 transition-colors"
                      >
                        <Phone className="w-3 h-3" />
                        اتصال
                      </a>
                      <a
                        href={`https://wa.me/${waPhone}?text=${waMessage}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium text-green-700 bg-green-50 hover:bg-green-100 transition-colors"
                      >
                        <MessageCircle className="w-3 h-3" />
                        واتساب
                      </a>
                    </div>
                  </div>

                  {/* Right side: time + action */}
                  <div className="flex flex-col items-end gap-2 flex-shrink-0">
                    <span className="text-xs text-gray-400" dir="ltr">{formatShortDate(item.createdAt)}</span>
                    {isConverted && item.convertedToAppointmentId && (
                      <button
                        onClick={(e) => { e.stopPropagation(); handleViewAppointment(item.convertedToAppointmentId!); }}
                        className="flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded-lg bg-emerald-50 text-emerald-700 hover:bg-emerald-100 transition-colors"
                      >
                        <ExternalLink className="w-3 h-3" />
                        فتح الموعد
                      </button>
                    )}
                    {!isConverted && item.status === "Confirmed" && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
                      <button
                        onClick={(e) => { e.stopPropagation(); handleCreateAppointment(item); }}
                        className="flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded-lg bg-clinic-blue/10 text-clinic-blue hover:bg-clinic-blue/20 transition-colors"
                      >
                        <CalendarPlus className="w-3 h-3" />
                        تحويل إلى موعد
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {selected && (
        <DetailModal
          item={selected}
          onClose={() => setSelected(null)}
          onStatusChange={handleStatusChange}
          onCreateAppointment={handleCreateAppointment}
          onViewAppointment={handleViewAppointment}
          onConfirmAndConvert={handleConfirmAndConvert}
        />
      )}

      {convertItem && (
        <ConvertModal
          item={convertItem}
          onClose={() => setConvertItem(null)}
          onConverted={(appointmentId?: string) => {
            fetchItems(page);
            if (appointmentId) {
              toast.success("تم تحويل الطلب — يمكنك فتح الموعد الآن");
            }
          }}
        />
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 pt-4">
          <button
            onClick={() => fetchItems(page - 1)}
            disabled={page <= 1 || loading}
            className="flex items-center gap-1 px-3 py-2 text-sm font-medium rounded-lg border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <ChevronRight className="w-4 h-4" />
            السابق
          </button>
          <div className="flex items-center gap-1">
            {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
              let pageNum: number;
              if (totalPages <= 5) {
                pageNum = i + 1;
              } else if (page <= 3) {
                pageNum = i + 1;
              } else if (page >= totalPages - 2) {
                pageNum = totalPages - 4 + i;
              } else {
                pageNum = page - 2 + i;
              }
              return (
                <button
                  key={pageNum}
                  onClick={() => fetchItems(pageNum)}
                  className={`w-9 h-9 rounded-lg text-sm font-semibold transition ${
                    pageNum === page
                      ? "bg-clinic-blue text-white"
                      : "bg-white text-gray-600 border border-gray-200 hover:bg-gray-50"
                  }`}
                >
                  {pageNum}
                </button>
              );
            })}
          </div>
          <button
            onClick={() => fetchItems(page + 1)}
            disabled={page >= totalPages || loading}
            className="flex items-center gap-1 px-3 py-2 text-sm font-medium rounded-lg border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 transition disabled:opacity-40 disabled:cursor-not-allowed"
          >
            التالي
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="text-xs text-gray-400 mr-2">
            {totalCount} طلب
          </span>
        </div>
      )}
    </div>
  );
}
