"use client";

import { useEffect, useState, useCallback, useMemo } from "react";
import api from "@/lib/api";
import { normalizePhone } from "@/lib/utils";
import type { CreatePatientRequest, PatientProfile } from "@/types/patient";
import type { PaginatedResponse } from "@/types/api";
import type { DoctorDto } from "@/hooks/useDoctors";
import {
  Clock, CheckCircle2, XCircle, Eye, Loader2, RefreshCw,
  Phone, Mail, Calendar, MessageSquare, Filter, Globe, CalendarPlus,
  Search, User, Stethoscope, ExternalLink, MessageCircle,
  Ban, X,
} from "lucide-react";
import { toast } from "@/stores/toastStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { useAuthStore } from "@/stores/authStore";
import { NAVY, BLUE, ORANGE } from "../_lib/constants";

/* ─── Types ────────────────────────────────────────────────────────────────── */
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

const STATUS_CONFIG: Record<BookingStatus, { label: string; color: string; bg: string }> = {
  Pending:   { label: "قيد الانتظار",   color: "#b45309",  bg: "#fffbeb" },
  Reviewed:  { label: "تمت المراجعة",   color: "#1d4ed8",  bg: "#eff6ff" },
  Confirmed: { label: "مؤكد",           color: "#15803d",  bg: "#f0fdf4" },
  Rejected:  { label: "مرفوض",          color: "#dc2626",  bg: "#fef2f2" },
};

type FilterKey = "all" | "today" | "Pending" | "Reviewed" | "Confirmed" | "Rejected";

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString("ar-YE", {
    year: "numeric", month: "short", day: "numeric",
    hour: "2-digit", minute: "2-digit",
  });
}

function splitPatientName(fullName: string): Pick<CreatePatientRequest, "firstName" | "middleName" | "lastName"> {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return { firstName: "Patient", lastName: "File" };
  if (parts.length === 1) return { firstName: parts[0], lastName: "غير محدد" };
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

/* ─── Component ────────────────────────────────────────────────────────────── */
export default function BookingRequestsView({ searchQuery }: { searchQuery: string }) {
  const { user } = useAuthStore();
  const [items, setItems] = useState<BookingRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeFilter, setActiveFilter] = useState<FilterKey>("all");
  const [selected, setSelected] = useState<BookingRequest | null>(null);
  const [staffNotes, setStaffNotes] = useState("");
  const [actionLoading, setActionLoading] = useState(false);

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<BookingRequest[] | PaginatedResponse<BookingRequest>>("/api/booking-requests?pageSize=50");
      const data = res.data;
      if (Array.isArray(data)) setItems(data);
      else setItems(data.data ?? []);
    } catch { /* ignore */ } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchItems(); }, [fetchItems]);

  async function ensurePatientFile(item: BookingRequest): Promise<string> {
    const normalizedPhone = normalizePhone(item.phoneNumber);
    if (normalizedPhone) {
      const params = new URLSearchParams({ phone: normalizedPhone });
      const { data } = await api.get(`/api/patients/check-duplicate?${params.toString()}`);
      const existing = data.matches?.find((m: { matchType: string }) => m.matchType === "phone" || m.matchType === "whatsapp");
      if (existing) return existing.id;
    }
    const { data: created } = await api.post<PatientProfile>("/api/patients", buildPatientPayload(item));
    return created.id;
  }

  async function handleStatusChange(item: BookingRequest, status: BookingStatus) {
    setActionLoading(true);
    try {
      if (status === "Confirmed") {
        await ensurePatientFile(item);
      }
      await api.patch(`/api/booking-requests/${item.id}/status`, { status, staffNotes });
      toast.success(status === "Confirmed" ? "تم تأكيد الطلب" : status === "Rejected" ? "تم رفض الطلب" : "تم تحديث الحالة");
      setSelected(null);
      setStaffNotes("");
      fetchItems();
    } catch { toast.error("فشل تحديث الحالة"); }
    finally { setActionLoading(false); }
  }

  const filteredItems = useMemo(() => {
    let result = items;
    if (activeFilter !== "all") result = result.filter(i => i.status === activeFilter);
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(i => i.patientName.toLowerCase().includes(q) || i.phoneNumber.includes(q));
    }
    return result;
  }, [items, activeFilter, searchQuery]);

  const pendingCount = items.filter(i => i.status === "Pending").length;

  return (
    <div className="flex flex-col h-full">
      {/* Filter bar */}
      <div className="flex-shrink-0 flex items-center gap-2 px-3 py-2 border-b" style={{ borderColor: "#f1f5f9", background: "#fff" }}>
        {(["all", "Pending", "Reviewed", "Confirmed", "Rejected"] as FilterKey[]).map(f => (
          <button key={f} onClick={() => setActiveFilter(f)}
            className="px-3 py-1.5 rounded-lg text-xs font-bold transition-all"
            style={{
              background: activeFilter === f ? BLUE : "#f5f7fa",
              color: activeFilter === f ? "#fff" : "#64748b",
            }}>
            {f === "all" ? "الكل" : (f in STATUS_CONFIG ? STATUS_CONFIG[f as BookingStatus]?.label : f)}
            {f === "Pending" && pendingCount > 0 && (
              <span className="mr-1 px-1.5 py-0.5 rounded-full text-[10px] font-bold"
                style={{ background: activeFilter === f ? "rgba(255,255,255,0.25)" : ORANGE + "15", color: activeFilter === f ? "#fff" : ORANGE }}>
                {pendingCount}
              </span>
            )}
          </button>
        ))}
        <div className="flex-1" />
        <button onClick={fetchItems} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
          <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} style={{ color: "#64748b" }} />
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto p-3 space-y-2">
        {loading ? (
          <div className="flex items-center justify-center py-20">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="text-center py-20" style={{ color: "#94a3b8" }}>
            <Globe className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="font-medium">{searchQuery ? "لا توجد نتائج" : "لا توجد طلبات حجز"}</p>
          </div>
        ) : (
          filteredItems.map(item => {
            const cfg = STATUS_CONFIG[item.status];
            const isConverted = item.convertedToAppointmentId !== null;
            const waPhone = normalizePhone(item.phoneNumber);
            const waMsg = encodeURIComponent("مرحباً، معك مركز الدكتور عقلان الكامل بخصوص طلب الحجز.");
            return (
              <div key={item.id} className="rounded-xl border p-3 hover:shadow-sm transition"
                style={{ background: "#fff", borderColor: "#e5e7eb" }}>
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1 flex-wrap">
                      <span className="font-bold text-sm" style={{ color: NAVY }}>{item.patientName}</span>
                      <span className="text-[11px] px-2 py-0.5 rounded-full font-bold" style={{ background: cfg.bg, color: cfg.color }}>{cfg.label}</span>
                      {isConverted && <span className="text-[11px] px-2 py-0.5 rounded-full font-bold bg-emerald-50 text-emerald-700">تم التحويل</span>}
                    </div>
                    <div className="flex items-center gap-3 text-xs flex-wrap" style={{ color: "#64748b" }}>
                      <span className="font-mono" dir="ltr">{item.phoneNumber}</span>
                      {item.serviceType && <span className="px-1.5 py-0.5 rounded text-[10px] font-medium" style={{ background: BLUE + "10", color: BLUE }}>{item.serviceType}</span>}
                      {item.doctorName && <span className="flex items-center gap-1"><Stethoscope className="w-3 h-3" />{item.doctorName}</span>}
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 flex-shrink-0">
                    {item.status === "Pending" && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
                      <>
                        <button onClick={() => handleStatusChange(item, "Confirmed")} disabled={actionLoading}
                          className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#16a34a" }}>تأكيد</button>
                        <button onClick={() => handleStatusChange(item, "Rejected")} disabled={actionLoading}
                          className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#ef4444" }}>رفض</button>
                      </>
                    )}
                    {item.status === "Reviewed" && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
                      <button onClick={() => handleStatusChange(item, "Confirmed")} disabled={actionLoading}
                        className="px-2.5 py-1 rounded-lg text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#16a34a" }}>تأكيد</button>
                    )}
                    <a href={`https://wa.me/${waPhone}?text=${waMsg}`} target="_blank" rel="noopener noreferrer"
                      className="w-7 h-7 rounded-lg flex items-center justify-center transition hover:bg-green-50" title="واتساب">
                      <MessageCircle className="w-3.5 h-3.5" style={{ color: "#25D366" }} />
                    </a>
                    <a href={`tel:${item.phoneNumber}`}
                      className="w-7 h-7 rounded-lg flex items-center justify-center transition hover:bg-blue-50" title="اتصال">
                      <Phone className="w-3.5 h-3.5" style={{ color: BLUE }} />
                    </a>
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Detail modal */}
      {selected && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm flex items-center justify-center p-4" onClick={() => setSelected(null)}>
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
            <div className="p-5 border-b flex items-start justify-between">
              <div>
                <h2 className="text-lg font-bold" style={{ color: NAVY }}>{selected.patientName}</h2>
                <span className="text-[11px] px-2 py-0.5 rounded-full font-bold inline-block mt-1"
                  style={{ background: STATUS_CONFIG[selected.status].bg, color: STATUS_CONFIG[selected.status].color }}>
                  {STATUS_CONFIG[selected.status].label}
                </span>
              </div>
              <button onClick={() => setSelected(null)} className="text-gray-400 hover:text-gray-600 p-1 rounded-lg hover:bg-gray-100">
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="p-5 space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div className="bg-gray-50 rounded-xl p-3">
                  <div className="text-xs text-gray-500 mb-1 flex items-center gap-1"><Phone className="w-3 h-3" /> الهاتف</div>
                  <a href={`tel:${selected.phoneNumber}`} className="font-semibold text-sm" style={{ color: BLUE }} dir="ltr">{selected.phoneNumber}</a>
                </div>
                {selected.serviceType && (
                  <div className="bg-gray-50 rounded-xl p-3">
                    <div className="text-xs text-gray-500 mb-1">الخدمة</div>
                    <div className="font-semibold text-sm" style={{ color: NAVY }}>{selected.serviceType}</div>
                  </div>
                )}
              </div>
              <div className="text-xs text-gray-400">تاريخ الطلب: {formatDate(selected.createdAt)}</div>
              <div>
                <label className="block text-sm font-semibold mb-1" style={{ color: NAVY }}>ملاحظات الموظف</label>
                <textarea value={staffNotes} onChange={e => setStaffNotes(e.target.value)} rows={2}
                  className="w-full px-3 py-2 rounded-xl border border-gray-200 focus:border-[#3d7ab5] outline-none text-sm resize-none" placeholder="ملاحظات اختيارية..." />
              </div>
            </div>
            <div className="p-5 pt-0 flex gap-2">
              {selected.status !== "Confirmed" && selected.status !== "Rejected" && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
                <button onClick={() => handleStatusChange(selected, "Confirmed")} disabled={actionLoading}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white transition disabled:opacity-50" style={{ background: "#16a34a" }}>
                  {actionLoading ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : "تأكيد"}
                </button>
              )}
              {selected.status !== "Rejected" && hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT) && (
                <button onClick={() => handleStatusChange(selected, "Rejected")} disabled={actionLoading}
                  className="px-4 py-2.5 rounded-xl text-sm font-bold text-white bg-red-500 transition hover:opacity-80 disabled:opacity-50">
                  رفض
                </button>
              )}
              <button onClick={() => setSelected(null)} className="px-4 py-2.5 rounded-xl text-sm font-medium border border-gray-200 hover:bg-gray-50" style={{ color: "#64748b" }}>إغلاق</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
