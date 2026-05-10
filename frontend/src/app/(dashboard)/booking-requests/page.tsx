"use client";

import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import {
  Clock, CheckCircle2, XCircle, Eye, Loader2, RefreshCw,
  Phone, Mail, Calendar, MessageSquare, Filter, Globe, CalendarPlus,
} from "lucide-react";

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
}

const STATUS_CONFIG: Record<BookingStatus, { label: string; color: string; bg: string; icon: React.ReactNode }> = {
  Pending:   { label: "قيد الانتظار",   color: "text-amber-700",  bg: "bg-amber-50 border-amber-200",  icon: <Clock className="w-3.5 h-3.5" /> },
  Reviewed:  { label: "تمت المراجعة",   color: "text-blue-700",   bg: "bg-blue-50 border-blue-200",    icon: <Eye className="w-3.5 h-3.5" /> },
  Confirmed: { label: "مؤكد",           color: "text-green-700",  bg: "bg-green-50 border-green-200",  icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  Rejected:  { label: "مرفوض",          color: "text-red-700",    bg: "bg-red-50 border-red-200",      icon: <XCircle className="w-3.5 h-3.5" /> },
};

const NEXT_STATUSES: Record<BookingStatus, { status: BookingStatus; label: string; color: string }[]> = {
  Pending:   [
    { status: "Reviewed",  label: "وضع علامة مراجعة", color: "bg-blue-500 hover:bg-blue-600" },
    { status: "Confirmed", label: "تأكيد",             color: "bg-green-500 hover:bg-green-600" },
    { status: "Rejected",  label: "رفض",               color: "bg-red-500 hover:bg-red-600" },
  ],
  Reviewed:  [
    { status: "Confirmed", label: "تأكيد",             color: "bg-green-500 hover:bg-green-600" },
    { status: "Rejected",  label: "رفض",               color: "bg-red-500 hover:bg-red-600" },
  ],
  Confirmed: [],
  Rejected:  [],
};

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("ar-YE", {
    year: "numeric", month: "short", day: "numeric",
    hour: "2-digit", minute: "2-digit",
  });
}

function parseTimeToISO(t: string | null): string | undefined {
  if (!t) return undefined;
  // Already 24h format "HH:MM"
  if (/^\d{1,2}:\d{2}$/.test(t.trim())) {
    const [h, m] = t.trim().split(":");
    return `${h.padStart(2, "0")}:${m}`;
  }
  // Arabic 12h like "9:00 ص" or "3:00 م"
  const m12 = t.match(/(\d{1,2}):(\d{2})\s*(ص|م|AM|PM)/i);
  if (m12) {
    let h = parseInt(m12[1], 10);
    const min = m12[2];
    const isPm = m12[3] === "م" || m12[3].toUpperCase() === "PM";
    if (isPm && h < 12) h += 12;
    if (!isPm && h === 12) h = 0;
    return `${h.toString().padStart(2, "0")}:${min}`;
  }
  return undefined;
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

interface DetailModalProps {
  item: BookingRequest;
  onClose: () => void;
  onStatusChange: (id: string, status: BookingStatus, notes: string) => Promise<void>;
  onCreateAppointment: (item: BookingRequest) => void;
}

function DetailModal({ item, onClose, onStatusChange, onCreateAppointment }: DetailModalProps) {
  const [staffNotes, setStaffNotes] = useState(item.staffNotes ?? "");
  const [loading, setLoading] = useState(false);
  const nextStatuses = NEXT_STATUSES[item.status];

  async function handleStatusChange(status: BookingStatus) {
    setLoading(true);
    try {
      await onStatusChange(item.id, status, staffNotes);
      onClose();
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" onClick={onClose}>
      <div
        className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="p-6 border-b border-gray-100 flex items-start justify-between">
          <div>
            <h2 className="text-lg font-bold text-gray-900">{item.patientName}</h2>
            <StatusBadge status={item.status} />
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

          {/* Service & Date */}
          <div className="grid grid-cols-2 gap-3">
            {item.serviceType && (
              <div className="bg-clinic-blue-50 rounded-xl p-3">
                <div className="text-xs text-gray-500 mb-1">الخدمة المطلوبة</div>
                <div className="font-semibold text-clinic-navy text-sm">{item.serviceType}</div>
              </div>
            )}
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
          </div>

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
          {nextStatuses.length > 0 && (
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
          {/* Create Appointment — shown for confirmed requests */}
          {item.status === "Confirmed" && (
            <button
              onClick={() => { onCreateAppointment(item); onClose(); }}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-white text-sm font-semibold bg-clinic-blue hover:bg-clinic-navy transition-colors"
            >
              <CalendarPlus className="w-3.5 h-3.5" />
              إنشاء موعد
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

const FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "جميع الطلبات" },
  { value: "Pending", label: "قيد الانتظار" },
  { value: "Reviewed", label: "تمت المراجعة" },
  { value: "Confirmed", label: "مؤكد" },
  { value: "Rejected", label: "مرفوض" },
];

export default function BookingRequestsPage() {
  const router = useRouter();
  const [items, setItems] = useState<BookingRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("");
  const [selected, setSelected] = useState<BookingRequest | null>(null);
  const [error, setError] = useState("");

  const fetchItems = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const params = statusFilter ? `?status=${statusFilter}` : "";
      const res = await api.get<BookingRequest[]>(`/api/booking-requests${params}`);
      setItems(res.data);
    } catch {
      setError("تعذّر تحميل طلبات الحجز");
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => { fetchItems(); }, [fetchItems]);

  async function handleStatusChange(id: string, status: BookingStatus, staffNotes: string) {
    await api.patch(`/api/booking-requests/${id}/status`, { status, staffNotes });
    await fetchItems();
  }

  function handleCreateAppointment(item: BookingRequest) {
    const params = new URLSearchParams();
    params.set("patientName", item.patientName);
    if (item.preferredDate) params.set("date", item.preferredDate);
    const t24 = parseTimeToISO(item.preferredTime);
    if (t24) params.set("startTime", t24);
    router.push(`/appointments/new?${params.toString()}`);
  }

  const pendingCount = items.filter((i) => i.status === "Pending").length;

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
          <button
            onClick={fetchItems}
            disabled={loading}
            className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 hover:text-gray-900 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
            تحديث
          </button>
        </div>
      </div>

      {/* Filter */}
      <div className="flex items-center gap-2 flex-wrap">
        <Filter className="w-4 h-4 text-gray-400 flex-shrink-0" />
        {FILTER_OPTIONS.map((opt) => (
          <button
            key={opt.value}
            onClick={() => setStatusFilter(opt.value)}
            className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${
              statusFilter === opt.value
                ? "bg-clinic-blue text-white"
                : "bg-white text-gray-600 border border-gray-200 hover:bg-gray-50"
            }`}
          >
            {opt.label}
          </button>
        ))}
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

      {!loading && !error && items.length === 0 && (
        <div className="text-center py-20 text-gray-400">
          <Globe className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="font-medium">لا توجد طلبات حجز</p>
          <p className="text-sm mt-1">ستظهر هنا طلبات الحجز الواردة من الموقع الإلكتروني</p>
        </div>
      )}

      {!loading && items.length > 0 && (
        <div className="grid gap-3">
          {items.map((item) => (
            <div
              key={item.id}
              className="bg-white rounded-2xl border border-gray-100 shadow-card hover:shadow-card-hover transition-all p-4 cursor-pointer"
              onClick={() => setSelected(item)}
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap mb-1">
                    <span className="font-bold text-clinic-navy">{item.patientName}</span>
                    <StatusBadge status={item.status} />
                  </div>
                  <div className="flex items-center gap-3 text-sm text-gray-500 flex-wrap">
                    <span className="flex items-center gap-1 font-mono" dir="ltr">
                      <Phone className="w-3.5 h-3.5" />
                      {item.phoneNumber}
                    </span>
                    {item.serviceType && (
                      <span className="bg-gray-100 rounded px-2 py-0.5 text-xs">{item.serviceType}</span>
                    )}
                    {item.preferredDate && (
                      <span className="flex items-center gap-1 text-xs">
                        <Calendar className="w-3 h-3" />
                        {item.preferredDate}
                        {item.preferredTime && ` ${item.preferredTime}`}
                      </span>
                    )}
                  </div>
                  {item.notes && (
                    <p className="text-xs text-gray-400 mt-1 truncate max-w-md">{item.notes}</p>
                  )}
                </div>
                <div className="flex flex-col items-end gap-1.5 flex-shrink-0">
                  <span className="text-xs text-gray-400" dir="ltr">{formatDate(item.createdAt)}</span>
                  {item.status === "Confirmed" && (
                    <button
                      onClick={(e) => { e.stopPropagation(); handleCreateAppointment(item); }}
                      className="flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded-lg bg-clinic-blue/10 text-clinic-blue hover:bg-clinic-blue/20 transition-colors"
                    >
                      <CalendarPlus className="w-3 h-3" />
                      إنشاء موعد
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {selected && (
        <DetailModal
          item={selected}
          onClose={() => setSelected(null)}
          onStatusChange={handleStatusChange}
          onCreateAppointment={handleCreateAppointment}
        />
      )}
    </div>
  );
}

