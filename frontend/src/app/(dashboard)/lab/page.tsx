"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, FlaskConical, Clock, CheckCircle2, XCircle, Package, Search } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import type { LabOrder, LabOrderStatus } from "@/types/lab";
import { NewLabOrderModal } from "@/components/lab/NewLabOrderModal";
import { cn } from "@/lib/utils";

const STATUS_CONFIG: Record<
  LabOrderStatus,
  { label: string; color: string; icon: ReactNode }
> = {
  sent:          { label: "تم الإرسال",  color: "bg-[#3d7ab518] text-accent-blue",   icon: <Clock className="w-3.5 h-3.5" /> },
  manufacturing: { label: "قيد الصنع",   color: "bg-[#f59e0b18] text-[#f59e0b]", icon: <FlaskConical className="w-3.5 h-3.5" /> },
  ready:         { label: "جاهز",        color: "bg-green-100 text-[#22c55e]", icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  received:      { label: "تم الاستلام", color: "bg-[#eef3f9] text-[#64748b]",   icon: <Package className="w-3.5 h-3.5" /> },
  cancelled:     { label: "ملغى",        color: "bg-red-100 text-[#ef4444]",     icon: <XCircle className="w-3.5 h-3.5" /> },
};

const PRIORITY_CONFIG = {
  urgent: { label: "عاجل",   color: "text-[#ef4444] font-semibold" },
  normal: { label: "عادي",   color: "text-[#64748b]" },
  low:    { label: "منخفض", color: "text-[#94a3b8]" },
};

const STATUS_FILTERS: Array<{ value: string; label: string }> = [
  { value: "",             label: "الكل" },
  { value: "sent",         label: "تم الإرسال" },
  { value: "manufacturing",label: "قيد الصنع" },
  { value: "ready",        label: "جاهز" },
  { value: "received",     label: "تم الاستلام" },
  { value: "cancelled",    label: "ملغى" },
];

const NEXT_STATUSES: Partial<Record<LabOrderStatus, LabOrderStatus>> = {
  sent:          "manufacturing",
  manufacturing: "ready",
  ready:         "received",
};

export default function LabPage() {
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage]     = useState(1);
  const [showNew, setShowNew] = useState(false);

  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["lab-orders", statusFilter, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "20" });
      if (statusFilter) params.set("status", statusFilter);
      const res = await api.get<{ data: LabOrder[]; total: number; page: number; pageSize: number }>(
        `/api/lab-orders?${params}`
      );
      return res.data;
    },
  });

  const { data: pendingData } = useQuery({
    queryKey: ["lab-orders-pending-count"],
    queryFn: async () => {
      const res = await api.get<{ count: number }>("/api/lab-orders/pending-count");
      return res.data;
    },
  });

  const advanceMutation = useMutation({
    mutationFn: async ({ id, status }: { id: string; status: LabOrderStatus }) => {
      await api.put(`/api/lab-orders/${id}/status`, {
        status,
        receivedDate: status === "received" ? new Date().toISOString().split("T")[0] : undefined,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["lab-orders-pending-count"] });
      toast.success("تم تحديث حالة الطلب");
    },
    onError: () => toast.error("فشل تحديث الحالة"),
  });

  const orders = (data?.data ?? []).filter((o) =>
    search.trim() === "" ||
    o.patientName.includes(search) ||
    o.orderNumber.includes(search) ||
    (o.labName ?? "").includes(search)
  );

  const totalPages = Math.ceil((data?.total ?? 0) / 20);

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-[#0d2137]">طلبات المختبر</h1>
            {pendingData && pendingData.count > 0 && (
              <p className="text-sm text-[#f59e0b] mt-0.5">
                {pendingData.count} طلب معلّق (قيد التصنيع أو تم الإرسال)
              </p>
            )}
          </div>
          <button
            onClick={() => setShowNew(true)}
            className="flex items-center gap-2 bg-accent-blue text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-blue-hover transition-colors"
          >
            <Plus className="w-4 h-4" />
            طلب جديد
          </button>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-52">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#94a3b8]" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالمريض أو رقم الطلب أو المختبر..."
              className="w-full border border-[#e8f0f9] rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-accent-blue"
            />
          </div>
          <div className="flex gap-1 bg-[#eef3f9] p-1 rounded-lg">
            {STATUS_FILTERS.map((f) => (
              <button
                key={f.value}
                onClick={() => { setStatusFilter(f.value); setPage(1); }}
                className={cn(
                  "px-3 py-1.5 rounded-md text-xs font-medium transition-colors",
                  statusFilter === f.value
                    ? "bg-white text-[#0d2137] shadow-card"
                    : "text-[#64748b] hover:text-[#64748b]"
                )}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-[#f1f5f9] shadow-card overflow-hidden">
          {isLoading ? (
            <div className="p-6">
              <TableSkeleton rows={6} cols={6} />
            </div>
          ) : orders.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-[#94a3b8]">
              <FlaskConical className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد طلبات</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] border-b border-[#f1f5f9]">
                <tr>
                  {["رقم الطلب", "المريض", "نوع الجهاز", "المختبر", "تاريخ الاستلام المتوقع", "الأولوية", "الحالة", ""].map((h) => (
                    <th key={h} className="text-right px-4 py-3 font-medium text-[#64748b] text-xs whitespace-nowrap">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {orders.map((order) => {
                  const statusCfg = STATUS_CONFIG[order.status];
                  const nextStatus = NEXT_STATUSES[order.status];
                  const priorityCfg = PRIORITY_CONFIG[order.priority];

                  return (
                    <tr key={order.id} className="hover:bg-[#f7fafd] transition-colors">
                      <td className="px-4 py-3 font-mono text-xs text-[#64748b]">{order.orderNumber}</td>
                      <td className="px-4 py-3">
                        <p className="font-medium text-[#0d2137]">{order.patientName}</p>
                        <p className="text-xs text-[#94a3b8]">{order.patientNumber}</p>
                      </td>
                      <td className="px-4 py-3 text-[#64748b]">{order.applianceType}</td>
                      <td className="px-4 py-3 text-[#64748b]">{order.labName ?? "—"}</td>
                      <td className="px-4 py-3 text-[#64748b] text-xs">
                        {order.expectedDate ?? "—"}
                      </td>
                      <td className="px-4 py-3">
                        <span className={cn("text-xs", priorityCfg.color)}>{priorityCfg.label}</span>
                      </td>
                      <td className="px-4 py-3">
                        <span className={cn("inline-flex items-center gap-1 px-[10px] py-[2px] rounded-full text-xs font-medium", statusCfg.color)}>
                          {statusCfg.icon}
                          {statusCfg.label}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        {nextStatus && (
                          <button
                            onClick={() => advanceMutation.mutate({ id: order.id, status: nextStatus })}
                            disabled={advanceMutation.isPending}
                            className="text-xs text-accent-blue hover:text-blue-hover font-medium disabled:opacity-50"
                          >
                            تقدّم ←
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2">
            <button
              disabled={page === 1}
              onClick={() => setPage((p) => p - 1)}
              className="px-3 py-1.5 text-sm border rounded-lg disabled:opacity-40 hover:bg-[#f7fafd]"
            >
              السابق
            </button>
            <span className="text-sm text-[#64748b]">
              {page} / {totalPages}
            </span>
            <button
              disabled={page === totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="px-3 py-1.5 text-sm border rounded-lg disabled:opacity-40 hover:bg-[#f7fafd]"
            >
              التالي
            </button>
          </div>
        )}
      </div>

      {showNew && <NewLabOrderModal onClose={() => setShowNew(false)} />}
    </ErrorBoundary>
  );
}
