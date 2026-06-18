"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, FlaskConical } from "lucide-react";
import api from "@/lib/api";
import { LAB_STATUS_LABELS as STATUS_LABELS, LAB_STATUS_COLORS as STATUS_COLORS } from "@/lib/labStatus";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import type { LabOrder, LabOrderStatus } from "@/types/lab";
import { cn, localDateString } from "@/lib/utils";

// FE-08: STATUS_LABELS + STATUS_COLORS now imported from @/lib/labStatus (was re-declared locally).

export default function LabOverduePage() {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["lab-orders-overdue"],
    queryFn: async () => {
      const res = await api.get<{ data: LabOrder[]; count: number }>("/api/lab-orders/overdue");
      return res.data;
    },
  });

  const markReceivedMutation = useMutation({
    mutationFn: async (id: string) => {
      await api.post(`/api/lab-orders/${id}/mark-received`, {
        receivedDate: localDateString(),
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders-overdue"] });
      toast.success("تم تأكيد استلام الطلب");
    },
    onError: () => toast.error("فشل تأكيد الاستلام"),
  });

  const advanceMutation = useMutation({
    mutationFn: async ({ id, status }: { id: string; status: LabOrderStatus }) => {
      await api.put(`/api/lab-orders/${id}/status`, { status });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders-overdue"] });
      toast.success("تم تحديث حالة الطلب");
    },
    onError: () => toast.error("فشل تحديث الحالة"),
  });

  const orders = data?.data ?? [];
  const overdueCount = data?.count ?? 0;

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center gap-3">
          <AlertTriangle className="w-7 h-7 text-red-600" />
          <div>
            <h1 className="text-2xl font-bold text-gray-900">طلبات متأخرة</h1>
            <p className="text-sm text-red-600 mt-0.5">
              {overdueCount} طلب تجاوز تاريخ الاستلام المتوقع
            </p>
          </div>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-red-200 shadow-sm overflow-hidden">
          {isLoading ? (
            <div className="p-6"><TableSkeleton rows={4} cols={8} /></div>
          ) : orders.length === 0 ? (
            <div className="flex flex-col items-center py-16 text-gray-400">
              <FlaskConical className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد طلبات متأخرة</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-red-50 border-b border-red-200">
                  <tr>
                    {["رقم الطلب", "المريض", "المختبر", "نوع الجهاز", "تاريخ الاستلام", "أيام التأخير", "الحالة", "إجراء"].map((h) => (
                      <th key={h} className="text-right px-4 py-3 font-medium text-red-700 text-xs whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {orders.map((order) => {
                    const daysOverdue = order.expectedDate
                      ? Math.floor((Date.now() - new Date(order.expectedDate).getTime()) / (1000 * 60 * 60 * 24))
                      : 0;

                    return (
                      <tr key={order.id} className="hover:bg-red-50/30">
                        <td className="px-4 py-3 font-mono text-xs text-gray-500">{order.orderNumber}</td>
                        <td className="px-4 py-3">
                          <p className="font-medium text-gray-900">{order.patientName}</p>
                          <p className="text-xs text-gray-400">{order.patientNumber}</p>
                        </td>
                        <td className="px-4 py-3 text-gray-500">{order.labEntityName ?? order.labName ?? "—"}</td>
                        <td className="px-4 py-3 text-gray-700">{order.applianceType}</td>
                        <td className="px-4 py-3 text-gray-500 text-xs">{order.expectedDate ?? "—"}</td>
                        <td className="px-4 py-3">
                          <span className={cn("font-bold text-sm",
                            daysOverdue > 7 ? "text-red-700" :
                            daysOverdue > 3 ? "text-red-600" : "text-red-500")}>
                            {daysOverdue}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span className={cn("inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium",
                            STATUS_COLORS[order.status] ?? "bg-gray-100 text-gray-500")}>
                            {STATUS_LABELS[order.status] ?? order.status}
                          </span>
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap">
                          {order.status === "ready" && (
                            <button
                              onClick={() => markReceivedMutation.mutate(order.id)}
                              disabled={markReceivedMutation.isPending}
                              className="text-xs text-indigo-700 hover:text-indigo-800 font-medium disabled:opacity-50"
                            >
                              تأكيد الاستلام
                            </button>
                          )}
                          {order.status === "sent" && (
                            <button
                              onClick={() => advanceMutation.mutate({ id: order.id, status: "manufacturing" as LabOrderStatus })}
                              disabled={advanceMutation.isPending}
                              className="text-xs text-amber-700 hover:text-amber-800 font-medium disabled:opacity-50"
                            >
                              بدء الصنع
                            </button>
                          )}
                          {order.status === "manufacturing" && (
                            <button
                              onClick={() => advanceMutation.mutate({ id: order.id, status: "tryIn" as LabOrderStatus })}
                              disabled={advanceMutation.isPending}
                              className="text-xs text-teal-700 hover:text-teal-800 font-medium disabled:opacity-50"
                            >
                              تجربة
                            </button>
                          )}
                          {order.status === "tryIn" && (
                            <button
                              onClick={() => advanceMutation.mutate({ id: order.id, status: "ready" as LabOrderStatus })}
                              disabled={advanceMutation.isPending}
                              className="text-xs text-green-700 hover:text-green-800 font-medium disabled:opacity-50"
                            >
                              جاهز
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </ErrorBoundary>
  );
}
