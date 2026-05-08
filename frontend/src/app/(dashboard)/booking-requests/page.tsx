"use client";
import { useEffect, useState, useCallback } from "react";
import { Phone, MessageCircle, CheckCircle, XCircle, CalendarClock } from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";

/* ─── Types ─────────────────────────────────────────────────────────────── */
interface BookingRequest {
  id: string;
  patientName: string;
  phoneNumber: string;
  email?: string;
  serviceType?: string;
  preferredDate?: string;
  preferredTime?: string;
  notes?: string;
  status: "pending" | "confirmed" | "cancelled";
  staffNotes?: string;
  createdAt: string;
  updatedAt?: string;
}

interface ApiResponse {
  data: BookingRequest[];
  total: number;
  page: number;
  pageSize: number;
}

type StatusFilter = "" | "pending" | "confirmed" | "cancelled";

/* ─── Helpers ────────────────────────────────────────────────────────────── */
function buildWhatsAppLink(phone: string): string {
  const digits = phone.replace(/\D/g, "");
  const normalized = digits.startsWith("967") ? digits : `967${digits}`;
  return `https://wa.me/${normalized}`;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "—";
  try {
    return new Date(dateStr).toLocaleDateString("ar-YE", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return dateStr;
  }
}

function StatusBadge({ status }: { status: BookingRequest["status"] }) {
  const map: Record<BookingRequest["status"], { label: string; cls: string }> = {
    pending:   { label: "جديد",   cls: "bg-yellow-100 text-yellow-800" },
    confirmed: { label: "مؤكد",   cls: "bg-green-100 text-green-800" },
    cancelled: { label: "ملغي",   cls: "bg-red-100 text-red-800" },
  };
  const { label, cls } = map[status] ?? { label: status, cls: "bg-gray-100 text-gray-700" };
  return (
    <span className={cn("inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium", cls)}>
      {label}
    </span>
  );
}

/* ─── Skeleton ───────────────────────────────────────────────────────────── */
function SkeletonRow() {
  return (
    <tr className="animate-pulse">
      {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
        <td key={i} className="px-4 py-3">
          <div className="h-4 bg-gray-200 rounded w-full" />
        </td>
      ))}
    </tr>
  );
}

/* ─── Main Page ──────────────────────────────────────────────────────────── */
export default function BookingRequestsPage() {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [page, setPage] = useState(1);
  const [requests, setRequests] = useState<BookingRequest[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [updatingId, setUpdatingId] = useState<string | null>(null);

  const pageSize = 20;

  const fetchRequests = useCallback(async () => {
    setLoading(true);
    try {
      const params: Record<string, string | number> = { page, pageSize };
      if (statusFilter) params.status = statusFilter;
      const { data } = await api.get<ApiResponse>("/api/booking-requests", { params });
      setRequests(data.data);
      setTotal(data.total);
    } catch {
      setRequests([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [statusFilter, page]);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  // Reset to page 1 when filter changes
  useEffect(() => {
    setPage(1);
  }, [statusFilter]);

  async function updateStatus(id: string, status: "confirmed" | "cancelled") {
    setUpdatingId(id);
    try {
      await api.put(`/api/booking-requests/${id}/status`, { status });
      setRequests((prev) =>
        prev.map((r) => (r.id === id ? { ...r, status } : r))
      );
    } catch {
      // silently ignore; could add toast
    } finally {
      setUpdatingId(null);
    }
  }

  const totalPages = Math.ceil(total / pageSize);

  const STATUS_TABS: { value: StatusFilter; label: string }[] = [
    { value: "",          label: "الكل" },
    { value: "pending",   label: "جديد" },
    { value: "confirmed", label: "مؤكد" },
    { value: "cancelled", label: "ملغي" },
  ];

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-teal-50 flex items-center justify-center">
          <CalendarClock className="w-5 h-5 text-clinic-teal" />
        </div>
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">طلبات الحجز الواردة</h1>
          <p className="text-sm text-gray-500 mt-0.5">إدارة طلبات الحجز من الموقع العام</p>
        </div>
      </div>

      {/* Status filter tabs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex border-b border-gray-200">
          {STATUS_TABS.map((tab) => (
            <button
              key={tab.value}
              onClick={() => setStatusFilter(tab.value)}
              className={cn(
                "px-5 py-3 text-sm font-medium transition-colors border-b-2 -mb-px",
                statusFilter === tab.value
                  ? "border-clinic-teal text-clinic-teal"
                  : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300"
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-right">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {["الاسم", "الهاتف", "الخدمة", "التاريخ المفضل", "الوقت", "تاريخ الطلب", "الحالة", "إجراءات"].map((h) => (
                  <th key={h} className="px-4 py-3 text-xs font-semibold text-gray-600 whitespace-nowrap">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)
              ) : requests.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-16 text-center text-gray-400">
                    <CalendarClock className="w-10 h-10 mx-auto mb-3 opacity-30" />
                    <p className="font-medium">لا توجد طلبات حجز</p>
                  </td>
                </tr>
              ) : (
                requests.map((req) => (
                  <tr key={req.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">
                      {req.patientName}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <div className="flex items-center gap-2">
                        <span className="text-gray-700 font-mono text-xs">{req.phoneNumber}</span>
                        <a
                          href={`tel:${req.phoneNumber}`}
                          className="text-clinic-teal hover:opacity-70 transition"
                          title="اتصال"
                        >
                          <Phone className="w-3.5 h-3.5" />
                        </a>
                        <a
                          href={buildWhatsAppLink(req.phoneNumber)}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-green-600 hover:opacity-70 transition"
                          title="واتساب"
                        >
                          <MessageCircle className="w-3.5 h-3.5" />
                        </a>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {req.serviceType ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600 whitespace-nowrap">
                      {req.preferredDate ? formatDate(req.preferredDate + "T00:00:00") : "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {req.preferredTime ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-500 whitespace-nowrap text-xs">
                      {formatDate(req.createdAt)}
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={req.status} />
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5">
                        {req.status !== "confirmed" && (
                          <button
                            onClick={() => updateStatus(req.id, "confirmed")}
                            disabled={updatingId === req.id}
                            className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-green-700 bg-green-50 hover:bg-green-100 rounded-lg transition disabled:opacity-50"
                          >
                            <CheckCircle className="w-3.5 h-3.5" />
                            تأكيد
                          </button>
                        )}
                        {req.status !== "cancelled" && (
                          <button
                            onClick={() => updateStatus(req.id, "cancelled")}
                            disabled={updatingId === req.id}
                            className="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium text-red-700 bg-red-50 hover:bg-red-100 rounded-lg transition disabled:opacity-50"
                          >
                            <XCircle className="w-3.5 h-3.5" />
                            إلغاء
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200">
            <span className="text-sm text-gray-500">
              {total} طلب — صفحة {page} من {totalPages}
            </span>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
                className="px-3 py-1.5 text-sm rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-40 transition"
              >
                السابق
              </button>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
                className="px-3 py-1.5 text-sm rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-40 transition"
              >
                التالي
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
