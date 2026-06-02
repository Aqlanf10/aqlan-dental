"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  useLabOrders,
  useMarkLabReceived,
  useMarkLabDelivered,
  useCancelLabOrder,
  useCreateLabOrder,
  type LabOrderRow,
} from "../_lib/hooks";
import { toast } from "@/stores/toastStore";
import {
  Search, Activity, CheckCircle2, Clock, Check, AlertTriangle,
  Plus, Loader2, XCircle, Package
} from "lucide-react";
import { NAVY, BLUE, fmtRial, fmtDate } from "../_lib/constants";

// Status labels and colors
const LAB_STATUS_MAP: Record<string, { label: string; color: string; bgColor: string; borderColor: string; icon: React.ElementType }> = {
  sent: { label: "تم الإرسال", color: "#d97706", bgColor: "#fff7ed", borderColor: "#fde68a", icon: Clock },
  manufacturing: { label: "قيد العمل", color: "#7c3aed", bgColor: "#faf5ff", borderColor: "#e9d5ff", icon: Activity },
  ready: { label: "جاهز للتسليم", color: "#2563eb", bgColor: "#eff6ff", borderColor: "#bfdbfe", icon: Package },
  received: { label: "جاهز بالعيادة", color: "#0891b2", bgColor: "#ecfeff", borderColor: "#a5f3fc", icon: CheckCircle2 },
  delivered: { label: "تم التسليم", color: "#16a34a", bgColor: "#f0fdf4", borderColor: "#bbf7d0", icon: Check },
  cancelled: { label: "ملغي", color: "#6b7280", bgColor: "#f9fafb", borderColor: "#e5e7eb", icon: XCircle },
};

// Ready lab orders alert query
function useReadyLabOrders() {
  return useQueryClient().getQueryData<LabOrderRow[]>(["daily-ops", "lab-orders-ready"]) ?? [];
}

export default function LabView() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("All");
  const [page, setPage] = useState(1);
  const [cancelModalOpen, setCancelModalOpen] = useState(false);
  const [cancelOrderId, setCancelOrderId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [newOrderModalOpen, setNewOrderModalOpen] = useState(false);
  // New lab order form state
  const [newPatientId, setNewPatientId] = useState("");
  const [newApplianceType, setNewApplianceType] = useState("");
  const [newLabName, setNewLabName] = useState("");
  const [newCost, setNewCost] = useState("");

  // Fetch lab orders
  const { data, isLoading, isError } = useLabOrders({
    status: statusFilter !== "All" ? statusFilter : undefined,
    page,
  });

  const orders = data?.data ?? [];

  // Filter by search on frontend (API doesn't support text search)
  const filteredOrders = orders.filter(o => {
    if (!search.trim()) return true;
    const q = search.trim().toLowerCase();
    return o.patientName.toLowerCase().includes(q) ||
      o.patientNumber?.toLowerCase().includes(q) ||
      o.doctorName?.toLowerCase().includes(q) ||
      o.applianceType?.toLowerCase().includes(q);
  });

  // Check for ready lab orders (alert badge)
  const readyOrders = useReadyLabOrders();

  // Mark received mutation
  const markReceivedMutation = useMarkLabReceived();

  // Mark delivered mutation
  const markDeliveredMutation = useMarkLabDelivered();

  // Cancel mutation
  const cancelMutation = useCancelLabOrder();

  // Create lab order mutation
  const createMutation = useCreateLabOrder();

  const getStatusBadge = (status: string) => {
    const info = LAB_STATUS_MAP[status] || LAB_STATUS_MAP.sent;
    const Icon = info.icon;
    return (
      <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold border`}
        style={{ background: info.bgColor, color: info.color, borderColor: info.borderColor }}>
        <Icon className="w-3 h-3" /> {info.label}
      </span>
    );
  };

  const handleCreateOrder = () => {
    if (!newPatientId.trim() || !newApplianceType.trim()) {
      toast.error("المريض ونوع التركيبة مطلوبان");
      return;
    }
    createMutation.mutate(
      {
        patientId: newPatientId.trim(),
        applianceType: newApplianceType.trim(),
        labName: newLabName.trim() || undefined,
        cost: newCost ? parseFloat(newCost) : undefined,
        sentDate: new Date().toISOString().split("T")[0],
      },
      {
        onSuccess: () => {
          setNewOrderModalOpen(false);
          setNewPatientId("");
          setNewApplianceType("");
          setNewLabName("");
          setNewCost("");
        },
      }
    );
  };

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 space-y-4">
      {/* Ready Lab Orders Alert */}
      {readyOrders.length > 0 && (
        <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl border bg-blue-50 border-blue-200">
          <Package className="w-4 h-4 text-blue-600 flex-shrink-0" />
          <span className="text-xs font-bold text-blue-800">
            {readyOrders.length} طلب معمل جاهز بالعيادة بانتظار التسليم للمريض
          </span>
        </div>
      )}

      {/* Top Filter Bar */}
      <div className="flex items-center justify-between gap-3 bg-white p-3 rounded-2xl border border-gray-100 shadow-sm flex-wrap">
        <div className="flex items-center gap-2 flex-1 max-w-md relative">
          <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder="البحث باسم المريض، رقم الملف، الطبيب، أو نوع التركيبة..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full text-xs rounded-xl border border-gray-200 pl-10 pr-9 py-2 outline-none focus:ring-2 focus:ring-[#3d7ab5]/20"
          />
        </div>

        <div className="flex items-center gap-2">
          <select
            value={statusFilter}
            onChange={e => { setStatusFilter(e.target.value); setPage(1); }}
            className="text-xs font-semibold rounded-xl px-3 py-2 border border-gray-200 outline-none"
            style={{ color: NAVY }}
          >
            <option value="All">كل حالات المعمل</option>
            <option value="sent">المرسلة</option>
            <option value="manufacturing">قيد العمل</option>
            <option value="ready">جاهز للتسليم</option>
            <option value="received">جاهز بالعيادة</option>
            <option value="delivered">تم التسليم</option>
            <option value="cancelled">ملغي</option>
          </select>

          <button
            onClick={() => setNewOrderModalOpen(true)}
            className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-bold text-white transition hover:opacity-90"
            style={{ background: BLUE }}
          >
            <Plus className="w-3.5 h-3.5" /> طلب جديد
          </button>
        </div>
      </div>

      {/* Loading / Error / Empty states */}
      {isLoading ? (
        <div className="flex-1 flex items-center justify-center">
          <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
        </div>
      ) : isError ? (
        <div className="flex-1 flex flex-col items-center justify-center text-red-400 py-20">
          <AlertTriangle className="w-12 h-12 mb-3 opacity-40" />
          <p className="text-sm font-bold">فشل تحميل بيانات المعمل</p>
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ["daily-ops", "lab-orders"] })}
            className="mt-2 text-xs text-blue-500 hover:underline"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : filteredOrders.length === 0 ? (
        <div className="flex-1 flex flex-col items-center justify-center text-gray-400 py-20">
          <Activity className="w-12 h-12 mb-3 opacity-20" />
          <p className="text-sm font-bold">لا توجد حالات معمل مطابقة</p>
        </div>
      ) : (
        /* Table */
        <div className="flex-1 overflow-auto bg-white rounded-2xl border border-gray-100 shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-right" style={{ borderCollapse: "separate", borderSpacing: 0 }}>
              <thead>
                <tr className="text-[11px] font-bold text-gray-400 uppercase tracking-wider bg-gray-50/50">
                  <th className="py-3 px-4 border-b">المريض</th>
                  <th className="py-3 px-4 border-b">نوع التركيبة</th>
                  <th className="py-3 px-4 border-b">الطبيب / المعمل</th>
                  <th className="py-3 px-4 border-b">التواريخ</th>
                  <th className="py-3 px-4 border-b">التكلفة</th>
                  <th className="py-3 px-4 border-b">الحالة</th>
                  <th className="py-3 px-4 border-b text-left">إجراءات</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filteredOrders.map(c => (
                  <tr key={c.id} className="hover:bg-gray-50/30 text-xs">
                    <td className="py-3.5 px-4 font-bold" style={{ color: NAVY }}>
                      <div>{c.patientName}</div>
                      <div className="text-[10px] text-gray-400 font-medium font-sans mt-0.5">{c.patientNumber}</div>
                    </td>
                    <td className="py-3.5 px-4 font-semibold text-gray-700">
                      <div>{c.applianceType}</div>
                    </td>
                    <td className="py-3.5 px-4 text-gray-600">
                      <div className="font-semibold">{c.doctorName || "—"}</div>
                      <div className="text-[10px] text-gray-400 mt-0.5">{c.labName || "—"}</div>
                    </td>
                    <td className="py-3.5 px-4 text-gray-500 font-medium">
                      <div>أُرسل: {c.sentDate ? fmtDate(c.sentDate) : "—"}</div>
                      <div className="text-[10px] text-orange-600 font-bold mt-0.5">متوقع: {c.expectedDate ? fmtDate(c.expectedDate) : "—"}</div>
                    </td>
                    <td className="py-3.5 px-4 font-bold text-gray-900 font-sans">{c.cost ? fmtRial(c.cost) : "—"}</td>
                    <td className="py-3.5 px-4">{getStatusBadge(c.status)}</td>
                    <td className="py-3.5 px-4 text-left">
                      <div className="flex items-center justify-end gap-1.5">
                        {(c.status === "sent" || c.status === "manufacturing") && (
                          <button
                            onClick={() => markReceivedMutation.mutate(c.id)}
                            disabled={markReceivedMutation.isPending}
                            className="px-2.5 py-1 rounded-lg text-[10px] font-bold text-white bg-blue-600 hover:bg-blue-700 transition disabled:opacity-50"
                          >
                            تأكيد الوصول
                          </button>
                        )}
                        {(c.status === "ready" || c.status === "received") && (
                          <button
                            onClick={() => markDeliveredMutation.mutate(c.id)}
                            disabled={markDeliveredMutation.isPending}
                            className="px-2.5 py-1 rounded-lg text-[10px] font-bold text-white bg-emerald-600 hover:bg-emerald-700 transition disabled:opacity-50"
                          >
                            تسليم للمريض
                          </button>
                        )}
                        {c.status !== "delivered" && c.status !== "cancelled" && (
                          <button
                            onClick={() => { setCancelOrderId(c.id); setCancelModalOpen(true); }}
                            className="px-2.5 py-1 rounded-lg text-[10px] font-bold text-red-600 border border-red-200 hover:bg-red-50 transition"
                          >
                            إلغاء
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {data && data.total > 50 && (
            <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100">
              <span className="text-[10px] text-gray-400">
                عرض {((page - 1) * 50) + 1} - {Math.min(page * 50, data.total)} من {data.total}
              </span>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-3 py-1.5 rounded-lg text-xs font-bold border border-gray-200 hover:bg-gray-50 disabled:opacity-40"
                >
                  السابق
                </button>
                <span className="text-xs font-bold" style={{ color: NAVY }}>{page}</span>
                <button
                  onClick={() => setPage(p => p + 1)}
                  disabled={page * 50 >= data.total}
                  className="px-3 py-1.5 rounded-lg text-xs font-bold border border-gray-200 hover:bg-gray-50 disabled:opacity-40"
                >
                  التالي
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Cancel Reason Modal */}
      {cancelModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => setCancelModalOpen(false)}>
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-5" onClick={e => e.stopPropagation()}>
            <h3 className="font-extrabold text-[15px] mb-3" style={{ color: NAVY }}>إلغاء طلب المعمل</h3>
            <p className="text-xs text-gray-500 mb-3">يرجى إدخال سبب الإلغاء</p>
            <textarea
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              placeholder="سبب الإلغاء..."
              rows={3}
              className="w-full text-xs rounded-lg border border-gray-200 px-3 py-2 outline-none focus:border-red-400"
            />
            <div className="flex gap-2 mt-4">
              <button
                onClick={() => { setCancelModalOpen(false); setCancelOrderId(null); setCancelReason(""); }}
                className="flex-1 py-2 rounded-xl text-sm font-bold bg-gray-100 text-gray-500 hover:bg-gray-200 transition"
              >
                تراجع
              </button>
              <button
                onClick={() => {
                  if (cancelOrderId) {
                    cancelMutation.mutate(
                      { id: cancelOrderId, reason: cancelReason.trim() || "بدون سبب" },
                      {
                        onSuccess: () => {
                          setCancelModalOpen(false);
                          setCancelOrderId(null);
                          setCancelReason("");
                        },
                      }
                    );
                  }
                }}
                disabled={!cancelReason.trim() || cancelMutation.isPending}
                className="flex-1 py-2 rounded-xl text-sm font-bold text-white bg-red-600 hover:bg-red-700 disabled:opacity-50 transition"
              >
                {cancelMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : "تأكيد الإلغاء"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* New Lab Order Modal */}
      {newOrderModalOpen && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => setNewOrderModalOpen(false)}>
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto p-5" onClick={e => e.stopPropagation()}>
            <h3 className="font-extrabold text-[15px] mb-1" style={{ color: NAVY }}>طلب معمل جديد</h3>
            <p className="text-xs text-gray-400 mb-4">سيتم إنشاء طلب معمل جديد وإضافته للتتبع</p>

            <div className="space-y-3">
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>معرف المريض (GUID) *</label>
                <input
                  value={newPatientId}
                  onChange={e => setNewPatientId(e.target.value)}
                  className="w-full text-xs rounded-lg border border-gray-200 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="أدخل معرف المريض أو ابحث"
                  dir="ltr"
                />
              </div>
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>نوع التركيبة *</label>
                <input
                  value={newApplianceType}
                  onChange={e => setNewApplianceType(e.target.value)}
                  className="w-full text-xs rounded-lg border border-gray-200 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="مثال: تاج زركونيا"
                />
              </div>
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>اسم المعمل</label>
                <input
                  value={newLabName}
                  onChange={e => setNewLabName(e.target.value)}
                  className="w-full text-xs rounded-lg border border-gray-200 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="اسم المختبر"
                />
              </div>
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>التكلفة</label>
                <input
                  type="number"
                  value={newCost}
                  onChange={e => setNewCost(e.target.value)}
                  className="w-full text-xs rounded-lg border border-gray-200 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="0"
                  dir="ltr"
                />
              </div>
            </div>

            <div className="flex gap-2 mt-5">
              <button
                onClick={() => {
                  setNewOrderModalOpen(false);
                  setNewPatientId("");
                  setNewApplianceType("");
                  setNewLabName("");
                  setNewCost("");
                }}
                className="flex-1 py-2 rounded-xl text-sm font-bold bg-gray-100 text-gray-500 hover:bg-gray-200 transition"
              >
                إلغاء
              </button>
              <button
                onClick={handleCreateOrder}
                disabled={createMutation.isPending}
                className="flex-1 py-2 rounded-xl text-sm font-bold text-white hover:opacity-90 transition disabled:opacity-50"
                style={{ background: BLUE }}
              >
                {createMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : "إنشاء الطلب"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
