
import { useCallback, useEffect, useMemo, useState } from "react";
import api from "@/lib/api";
import type { PaginatedResponse } from "@/types/api";
import { toast } from "@/stores/toastStore";
import { extractErrorMessage } from "@/lib/errors";
import { useAuthStore } from "@/stores/authStore";
import { hasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";
import { Loader2, RefreshCw, Globe, Stethoscope, Phone, MessageCircle } from "lucide-react";
import { NAVY, BLUE, ORANGE } from "../_lib/constants";
import { canConvertBookingToAppointment, confirmBookingAndCreateAppointment } from "../_lib/bookingConversion";
import { useClinicBranding } from "@/hooks/useClinicBranding";

type BookingStatus = "Pending" | "Reviewed" | "Confirmed" | "Rejected";
type FilterKey = "all" | BookingStatus;

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
  Pending: { label: "قيد الانتظار", color: "#b45309", bg: "#fffbeb" },
  Reviewed: { label: "تمت المراجعة", color: "#1d4ed8", bg: "#eff6ff" },
  Confirmed: { label: "مؤكد", color: "#15803d", bg: "#f0fdf4" },
  Rejected: { label: "مرفوض", color: "#dc2626", bg: "#fef2f2" },
};

function normalizeWaPhone(phone: string): string {
  const digits = phone.replace(/\D/g, "");
  if (digits.startsWith("967")) return digits;
  if (digits.startsWith("0")) return "967" + digits.slice(1);
  return "967" + digits;
}

export default function BookingRequestsView({ searchQuery }: { searchQuery: string }) {
  const { user } = useAuthStore();
  const branding = useClinicBranding();
  const [items, setItems] = useState<BookingRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeFilter, setActiveFilter] = useState<FilterKey>("all");
  const [actionLoadingId, setActionLoadingId] = useState<string | null>(null);
  const [staffNotes] = useState("");

  const fetchItems = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<BookingRequest[] | PaginatedResponse<BookingRequest>>("/api/booking-requests?pageSize=50");
      const data = res.data;
      setItems(Array.isArray(data) ? data : data.data ?? []);
    } catch (err) {
      // CORE-APPT-015: the catch used to discard the error and show a fixed
      // string, hiding the server's own Arabic message.
      toast.error(extractErrorMessage(err, "فشل تحميل طلبات الحجز"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchItems(); }, [fetchItems]);

  async function handleConfirm(item: BookingRequest) {
    setActionLoadingId(item.id);
    try {
      const result = await confirmBookingAndCreateAppointment(item, staffNotes);
      if (result.appointmentCreated) {
        toast.success("تم تأكيد الطلب وإنشاء موعد يظهر في التشغيل اليومي");
      } else if (result.reason === "missing_doctor_date_or_time") {
        toast.success("تم تأكيد الطلب، لكنه يحتاج طبيب وتاريخ ووقت لإنشاء الموعد");
      } else {
        toast.success("تم تأكيد الطلب");
      }
      await fetchItems();
    } catch (err) {
      // CORE-APPT-015: this is the one that mattered most. The backend sends
      // precise reasons here — "يوجد موعد آخر في نفس الوقت لهذا الطبيب",
      // "الغرفة محجوزة في هذا الوقت", "تم تحويل هذا الطلب بالفعل إلى موعد" —
      // and bookingConversion rethrows the original error so they survive. The
      // bare catch replaced all of them with one generic line, so reception
      // staff could not tell a real booking clash from a server fault.
      toast.error(extractErrorMessage(err, "فشل تأكيد الطلب أو إنشاء الموعد"));
    } finally {
      setActionLoadingId(null);
    }
  }

  async function handleReject(item: BookingRequest) {
    setActionLoadingId(item.id);
    try {
      await api.patch(`/api/booking-requests/${item.id}/status`, { status: "Rejected", staffNotes });
      toast.success("تم رفض الطلب");
      await fetchItems();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل رفض الطلب"));
    } finally {
      setActionLoadingId(null);
    }
  }

  const filteredItems = useMemo(() => {
    let result = items;
    if (activeFilter !== "all") result = result.filter(i => i.status === activeFilter);
    const q = searchQuery.trim().toLowerCase();
    if (q) result = result.filter(i => i.patientName.toLowerCase().includes(q) || i.phoneNumber.includes(q));
    return result;
  }, [items, activeFilter, searchQuery]);

  const pendingCount = items.filter(i => i.status === "Pending").length;
  const canEdit = hasPermission(user, PERMISSION_KEYS.BOOKING_REQUESTS_EDIT);

  return (
    <div className="flex h-full flex-col">
      <div className="flex flex-shrink-0 items-center gap-2 border-b bg-white px-3 py-2" style={{ borderColor: "#f1f5f9" }}>
        {(["all", "Pending", "Reviewed", "Confirmed", "Rejected"] as FilterKey[]).map(filter => (
          <button key={filter} onClick={() => setActiveFilter(filter)} className="rounded-lg px-3 py-1.5 text-xs font-bold transition-all" style={{ background: activeFilter === filter ? BLUE : "#f5f7fa", color: activeFilter === filter ? "#fff" : "#64748b" }}>
            {filter === "all" ? "الكل" : STATUS_CONFIG[filter].label}
            {filter === "Pending" && pendingCount > 0 ? <span className="mr-1 rounded-full px-1.5 py-0.5 text-[10px] font-bold" style={{ background: activeFilter === filter ? "rgba(255,255,255,.25)" : ORANGE + "15", color: activeFilter === filter ? "#fff" : ORANGE }}>{pendingCount}</span> : null}
          </button>
        ))}
        <div className="flex-1" />
        <button onClick={fetchItems} className="flex h-8 w-8 items-center justify-center rounded-lg transition hover:bg-gray-100">
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} style={{ color: "#64748b" }} />
        </button>
      </div>

      <div className="flex-1 space-y-2 overflow-auto p-3">
        {loading ? <div className="flex justify-center py-20"><Loader2 className="h-8 w-8 animate-spin" style={{ color: BLUE }} /></div> : null}
        {!loading && filteredItems.length === 0 ? <div className="py-20 text-center text-gray-400"><Globe className="mx-auto mb-3 h-12 w-12 opacity-30" /><p className="font-medium">{searchQuery ? "لا توجد نتائج" : "لا توجد طلبات حجز"}</p></div> : null}
        {!loading && filteredItems.map(item => {
          const cfg = STATUS_CONFIG[item.status];
          const isConverted = Boolean(item.convertedToAppointmentId);
          const canConvert = canConvertBookingToAppointment(item);
          const isBusy = actionLoadingId === item.id;
          const waPhone = normalizeWaPhone(item.phoneNumber);
          const waText = encodeURIComponent(`مرحباً، معك ${branding.clinicName} بخصوص طلب الحجز.`);
          return (
            <div key={item.id} className="rounded-xl border bg-white p-3 transition hover:shadow-sm" style={{ borderColor: "#e5e7eb" }}>
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                  <div className="mb-1 flex flex-wrap items-center gap-2">
                    <span className="text-sm font-bold" style={{ color: NAVY }}>{item.patientName}</span>
                    <span className="rounded-full px-2 py-0.5 text-[11px] font-bold" style={{ background: cfg.bg, color: cfg.color }}>{cfg.label}</span>
                    {isConverted ? <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-700">تم التحويل لموعد</span> : null}
                    {!isConverted && item.status !== "Rejected" && !canConvert ? <span className="rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-bold text-amber-700">ينقص طبيب/تاريخ/وقت</span> : null}
                  </div>
                  <div className="flex flex-wrap items-center gap-3 text-xs" style={{ color: "#64748b" }}>
                    <span dir="ltr" className="font-mono">{item.phoneNumber}</span>
                    {item.serviceType ? <span className="rounded px-1.5 py-0.5 text-[10px] font-medium" style={{ background: BLUE + "10", color: BLUE }}>{item.serviceType}</span> : null}
                    {item.preferredDate ? <span>{item.preferredDate}</span> : null}
                    {item.preferredTime ? <span>{item.preferredTime}</span> : null}
                    {item.doctorName ? <span className="flex items-center gap-1"><Stethoscope className="h-3 w-3" />{item.doctorName}</span> : null}
                  </div>
                </div>
                <div className="flex flex-shrink-0 items-center gap-1.5">
                  {(item.status === "Pending" || item.status === "Reviewed") && canEdit ? (
                    <>
                      <button onClick={() => handleConfirm(item)} disabled={isBusy || isConverted} className="rounded-lg px-2.5 py-1 text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50" style={{ background: "#16a34a" }}>{isBusy ? "..." : canConvert ? "تأكيد وإنشاء موعد" : "تأكيد"}</button>
                      <button onClick={() => handleReject(item)} disabled={isBusy} className="rounded-lg bg-red-500 px-2.5 py-1 text-[11px] font-bold text-white transition hover:opacity-80 disabled:opacity-50">رفض</button>
                    </>
                  ) : null}
                  <a href={`https://wa.me/${waPhone}?text=${waText}`} target="_blank" rel="noreferrer" className="flex h-7 w-7 items-center justify-center rounded-lg transition hover:bg-green-50" title="واتساب"><MessageCircle className="h-3.5 w-3.5" style={{ color: "#25D366" }} /></a>
                  <a href={`tel:${item.phoneNumber}`} className="flex h-7 w-7 items-center justify-center rounded-lg transition hover:bg-blue-50" title="اتصال"><Phone className="h-3.5 w-3.5" style={{ color: BLUE }} /></a>
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
