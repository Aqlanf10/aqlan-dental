"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { DollarSign, FlaskConical, X } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { TableSkeleton } from "@/components/ui/skeleton";
import { useHasPermission } from "@/hooks/usePermissions";
import { PERMISSION_KEYS } from "@/hooks/usePermissions";
import type { LabPayable } from "@/types/lab";
import { cn } from "@/lib/utils";

/* ─── Status helpers ────────────────────────────────────────────────────────── */
const statusLabel: Record<string, string> = {
  pending: "قيد الانتظار",
  partial: "دفعة جزئية",
  paid: "مدفوع",
};

const statusClasses: Record<string, string> = {
  pending: "bg-amber-100 text-amber-700",
  partial: "bg-blue-100 text-blue-700",
  paid: "bg-green-100 text-green-700",
};

/* ─── Record Payment Modal ───────────────────────────────────────────────────── */
function RecordPaymentModal({
  payable,
  onClose,
}: {
  payable: LabPayable;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [amount, setAmount] = useState("");
  const [notes, setNotes] = useState("");
  const balance = payable.amount - payable.paidAmount;

  const mutation = useMutation({
    mutationFn: async () => {
      const res = await api.post(`/api/lab-payables/${payable.id}/record-payment`, {
        amount: Number(amount),
        notes: notes || undefined,
      });
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["lab-payables"] });
      toast.success("تم تسجيل الدفعة بنجاح");
      onClose();
    },
    onError: (error: unknown) => {
      const apiError = error as { response?: { data?: { message?: string } } };
      toast.error(apiError.response?.data?.message ?? "فشل تسجيل الدفعة");
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!amount || Number(amount) <= 0) return;
    mutation.mutate();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />
      {/* Dialog */}
      <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6 space-y-5">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-bold text-gray-900">تسجيل دفعة</h3>
            <p className="text-xs text-gray-500 mt-1">
              {payable.labName ?? "—"} — طلب #{payable.orderNumber ?? "—"}
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Payable summary */}
        <div className="rounded-xl border border-gray-100 bg-gray-50 p-4 space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-gray-500">المبلغ الإجمالي</span>
            <span className="font-medium text-gray-900">
              {payable.amount.toLocaleString()}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-gray-500">المدفوع</span>
            <span className="font-medium text-green-600">
              {payable.paidAmount.toLocaleString()}
            </span>
          </div>
          <div className="flex justify-between border-t border-gray-200 pt-2">
            <span className="text-gray-500 font-medium">الرصيد المتبقي</span>
            <span className="font-bold text-red-600">
              {balance.toLocaleString()}
            </span>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">
              مبلغ الدفعة *
            </label>
            <input
              type="number"
              min="1"
              max={balance}
              step="0.01"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder={`الحد الأقصى: ${balance.toLocaleString()}`}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              required
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">ملاحظات</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              placeholder="ملاحظات اختيارية..."
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 resize-none"
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 border border-gray-200 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={!amount || Number(amount) <= 0 || mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ التسجيل..." : "تسجيل الدفعة"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ─── Main Page ──────────────────────────────────────────────────────────────── */
export default function LabPayablesPage() {
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [selectedPayable, setSelectedPayable] = useState<LabPayable | null>(
    null
  );

  const canView = useHasPermission(PERMISSION_KEYS.LAB_PAYABLES_VIEW);
  const canEdit = useHasPermission(PERMISSION_KEYS.LAB_PAYABLES_MANAGE);

  const { data, isLoading } = useQuery({
    queryKey: ["lab-payables", statusFilter, page],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (statusFilter) params.set("status", statusFilter);
      params.set("page", String(page));
      params.set("pageSize", "20");
      const res = await api.get<{
        data: LabPayable[];
        total: number;
        page: number;
        pageSize: number;
      }>(`/api/lab-payables?${params}`);
      return res.data;
    },
    enabled: canView,
  });

  const payables = data?.data ?? [];
  const totalPages = Math.ceil((data?.total ?? 0) / (data?.pageSize ?? 20));

  if (!canView) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] gap-3 text-gray-400">
        <DollarSign className="w-10 h-10" />
        <p className="font-medium">ليس لديك صلاحية الوصول لهذه الصفحة</p>
      </div>
    );
  }

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Header */}
        <div>
          <h1 className="text-2xl font-bold text-gray-900">
            مستحقات المعامل
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            إدارة ومتابعة ديون ومستحقات المعامل
          </p>
        </div>

        {/* Summary KPIs */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">إجمالي المستحقات</p>
            <p className="text-xl font-bold text-gray-900 mt-1">
              {data?.total ?? 0}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">قيد الانتظار</p>
            <p className="text-xl font-bold text-amber-600 mt-1">
              {payables.filter((p) => p.status === "pending").length}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">دفعة جزئية</p>
            <p className="text-xl font-bold text-blue-600 mt-1">
              {payables.filter((p) => p.status === "partial").length}
            </p>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500">مدفوع</p>
            <p className="text-xl font-bold text-green-600 mt-1">
              {payables.filter((p) => p.status === "paid").length}
            </p>
          </div>
        </div>

        {/* Filter bar */}
        <div className="flex items-center gap-3">
          <span className="text-sm text-gray-500">الحالة:</span>
          <div className="flex gap-1 bg-gray-100 p-1 rounded-lg">
            {[
              { value: "", label: "الكل" },
              { value: "pending", label: "قيد الانتظار" },
              { value: "partial", label: "دفعة جزئية" },
              { value: "paid", label: "مدفوع" },
            ].map((opt) => (
              <button
                key={opt.value}
                onClick={() => {
                  setStatusFilter(opt.value);
                  setPage(1);
                }}
                className={cn(
                  "px-3 py-1.5 rounded-md text-xs font-medium transition-colors",
                  statusFilter === opt.value
                    ? "bg-white text-gray-900 shadow-sm"
                    : "text-gray-500 hover:text-gray-700"
                )}
              >
                {opt.label}
              </button>
            ))}
          </div>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
          {isLoading ? (
            <div className="p-6">
              <TableSkeleton rows={5} cols={6} />
            </div>
          ) : payables.length === 0 ? (
            <div className="flex flex-col items-center py-12 text-gray-400">
              <FlaskConical className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد مستحقات</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 border-b border-gray-100">
                  <tr>
                    {[
                      "المختبر",
                      "رقم الطلب",
                      "المبلغ",
                      "المدفوع",
                      "الحالة",
                      "تاريخ الاستحقاق",
                      "",
                    ].map((h) => (
                      <th
                        key={h}
                        className="text-right px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap"
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {payables.map((p: LabPayable) => (
                    <tr key={p.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 font-medium text-gray-900">
                        {p.labName ?? "—"}
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-gray-500">
                        {p.orderNumber ?? "—"}
                      </td>
                      <td className="px-4 py-3 text-gray-700">
                        {p.amount.toLocaleString()}
                      </td>
                      <td className="px-4 py-3 text-green-600">
                        {p.paidAmount.toLocaleString()}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={cn(
                            "px-2 py-0.5 rounded-full text-xs font-medium",
                            statusClasses[p.status] ??
                              "bg-gray-100 text-gray-500"
                          )}
                        >
                          {statusLabel[p.status] ?? p.status}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs">
                        {p.dueDate ?? "—"}
                      </td>
                      <td className="px-4 py-3">
                        {p.status !== "paid" && canEdit && (
                          <button
                            onClick={() => setSelectedPayable(p)}
                            className="px-3 py-1.5 bg-cyan-700 text-white text-xs font-medium rounded-lg hover:bg-cyan-800 transition-colors"
                          >
                            تسجيل دفعة
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 transition-colors"
            >
              السابق
            </button>
            <span className="text-sm text-gray-500">
              {page} / {totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 transition-colors"
            >
              التالي
            </button>
          </div>
        )}

        {/* Record Payment Modal */}
        {selectedPayable && (
          <RecordPaymentModal
            payable={selectedPayable}
            onClose={() => setSelectedPayable(null)}
          />
        )}
      </div>
    </ErrorBoundary>
  );
}
