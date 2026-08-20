"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, FlaskConical, Clock, CheckCircle2, XCircle, Package, Search, FileText, RotateCcw, RefreshCw, Printer, Download, ScanLine } from "lucide-react";
import api from "@/lib/api";
import { downloadPdfFromApi, printPdfFromApi } from "@/lib/pdfDownload";
import { toast } from "@/stores/toastStore";
import { EditLabOrderModal } from "@/components/lab/EditLabOrderModal";
import { RemakeLabOrderModal } from "@/components/lab/RemakeLabOrderModal";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import type { LabOrder, LabOrderStatus } from "@/types/lab";
import { NewLabOrderModal } from "@/components/lab/NewLabOrderModal";
import { ScanOrderDialog } from "@/components/lab/ScanOrderDialog";
import { SendToLabButton } from "@/components/lab/SendToLabButton";
import { cn, localDateString } from "@/lib/utils";
import { extractErrorMessage } from "@/lib/errors";
import { QueryErrorBanner } from "@/components/shared/QueryErrorBanner";

const STATUS_CONFIG: Record<
  LabOrderStatus,
  { label: string; color: string; icon: ReactNode }
> = {
  draft:         { label: "مسودة",      color: "bg-gray-100 text-gray-500",   icon: <FileText className="w-3.5 h-3.5" /> },
  sent:          { label: "تم الإرسال",  color: "bg-blue-100 text-blue-700",   icon: <Clock className="w-3.5 h-3.5" /> },
  manufacturing: { label: "قيد الصنع",   color: "bg-amber-100 text-amber-700", icon: <FlaskConical className="w-3.5 h-3.5" /> },
  tryIn:         { label: "تجربة",       color: "bg-teal-100 text-teal-700",   icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  ready:         { label: "جاهز",        color: "bg-green-100 text-green-700", icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  received:      { label: "تم الاستلام", color: "bg-indigo-100 text-indigo-700", icon: <Package className="w-3.5 h-3.5" /> },
  delivered:     { label: "تم التسليم",  color: "bg-emerald-100 text-emerald-700", icon: <CheckCircle2 className="w-3.5 h-3.5" /> },
  returned:      { label: "مرتجع",      color: "bg-orange-100 text-orange-700", icon: <RotateCcw className="w-3.5 h-3.5" /> },
  remake:        { label: "إعادة صناعة", color: "bg-purple-100 text-purple-700", icon: <RefreshCw className="w-3.5 h-3.5" /> },
  cancelled:     { label: "ملغى",        color: "bg-red-100 text-red-700",     icon: <XCircle className="w-3.5 h-3.5" /> },
};

const PRIORITY_CONFIG = {
  urgent: { label: "عاجل",   color: "text-red-600 font-semibold" },
  normal: { label: "عادي",   color: "text-gray-600" },
  low:    { label: "منخفض", color: "text-gray-400" },
};

const STATUS_FILTERS: Array<{ value: string; label: string }> = [
  { value: "",             label: "الكل" },
  { value: "draft",        label: "مسودة" },
  { value: "sent",         label: "تم الإرسال" },
  { value: "manufacturing",label: "قيد الصنع" },
  { value: "tryIn",        label: "تجربة" },
  { value: "ready",        label: "جاهز" },
  { value: "received",     label: "تم الاستلام" },
  { value: "delivered",    label: "تم التسليم" },
  { value: "returned",     label: "مرتجع" },
  { value: "remake",       label: "إعادة صناعة" },
  { value: "cancelled",    label: "ملغى" },
];

const NEXT_STATUSES: Partial<Record<LabOrderStatus, LabOrderStatus>> = {
  draft:         "sent",
  sent:          "manufacturing",
  manufacturing: "tryIn",
  tryIn:         "ready",
  ready:         "received",
  received:      "delivered",
  returned:      "remake",
  remake:        "sent",
};

const NEXT_STATUS_LABELS: Partial<Record<LabOrderStatus, string>> = {
  sent: "إرسال",
  manufacturing: "بدء التصنيع",
  tryIn: "تجربة",
  ready: "جاهز",
  received: "استلام",
  delivered: "تسليم",
  remake: "إعادة صناعة",
};

export function LabOrdersPanel() {
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage]     = useState(1);
  const [showNew, setShowNew] = useState(false);
  const [showScan, setShowScan] = useState(false);
  // CORE-LAB-003: /lab could advance a draft's status but never edit it, so a draft
  // missing its lab/cost had no way to become complete.
  const [editOrder, setEditOrder] = useState<LabOrder | null>(null);
  // CORE-LAB-007: "returned → remake" needs its own dialog (reason + free/paid + cost)
  // instead of the generic status-advance button, so the /remake endpoint actually
  // receives the data it's built to accept.
  const [remakeOrder, setRemakeOrder] = useState<LabOrder | null>(null);

  const queryClient = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
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
        receivedDate: status === "received" ? localDateString() : undefined,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["lab-orders-pending-count"] });
      toast.success("تم تحديث حالة الطلب");
    },
    onError: (err) => toast.error(extractErrorMessage(err, "فشل تحديث الحالة")),
  });

  const orders = (data?.data ?? []).filter((o) =>
    search.trim() === "" ||
    o.patientName.includes(search) ||
    o.orderNumber.includes(search) ||
                    (o.labEntityName ?? o.labName ?? "").includes(search)
  );

  const totalPages = Math.ceil((data?.total ?? 0) / 20);

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          {/* CORE-LAB-017: the module title lives in the workspace command bar now.
              What stays is the number the user actually came for. */}
          <div>
            {pendingData && pendingData.count > 0 && (
              <p className="text-sm text-amber-600">
                {pendingData.count} طلب معلّق (قيد التصنيع أو تم الإرسال)
              </p>
            )}
          </div>
          <div className="flex items-center gap-2">
            {/* LABINV-REQ-008 — the receiving end of "تراكم التراكيب": a box arrives and
                the fastest way to reach its record is to scan the slip taped to it. */}
            <button
              onClick={() => setShowScan(true)}
              className="flex items-center gap-2 border border-cyan-200 text-cyan-800 px-4 py-2 rounded-lg text-sm font-medium hover:bg-cyan-50 transition-colors"
            >
              <ScanLine className="w-4 h-4" />
              مسح رمز
            </button>
            <button
              onClick={() => setShowNew(true)}
              className="flex items-center gap-2 bg-cyan-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors"
            >
              <Plus className="w-4 h-4" />
              طلب جديد
            </button>
          </div>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-52">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالمريض أو رقم الطلب أو المختبر..."
              className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>
          {/* CORE-LAB-021: eleven status pills in a flex row that neither wrapped nor
              scrolled came to roughly 770px, so on a phone the group pushed the whole page
              into horizontal scrolling and the last statuses — ملغى, إعادة صناعة, مرتجع —
              could not be reached at all. The reports tabs already solved this; the same
              three classes are used here so the module behaves consistently. */}
          <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit max-w-full overflow-x-auto">
            {STATUS_FILTERS.map((f) => (
              <button
                key={f.value}
                onClick={() => { setStatusFilter(f.value); setPage(1); }}
                className={cn(
                  "px-3 py-1.5 rounded-md text-xs font-medium transition-colors",
                  statusFilter === f.value
                    ? "bg-white text-gray-900 shadow-sm"
                    : "text-gray-500 hover:text-gray-700"
                )}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
          {isLoading ? (
            <div className="p-6">
              <TableSkeleton rows={6} cols={6} />
            </div>
          ) : isError ? (
            <div className="p-6">
              <QueryErrorBanner
                text="تعذر تحميل طلبات المختبر — تحقق من الاتصال وحاول مجددًا"
                onRetry={() => refetch()}
              />
            </div>
          ) : orders.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-gray-400">
              <FlaskConical className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد طلبات</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["رقم الطلب", "المريض", "نوع الجهاز", "المختبر", "تاريخ الاستلام المتوقع", "الأولوية", "الحالة", "إجراء"].map((h) => (
                    <th key={h} className="text-right px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {orders.map((order) => {
                  const statusCfg = STATUS_CONFIG[order.status];
                  const nextStatus = NEXT_STATUSES[order.status];
                  const priorityCfg = PRIORITY_CONFIG[order.priority];

                  return (
                    <tr key={order.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3 font-mono text-xs text-gray-500">{order.orderNumber}</td>
                      <td className="px-4 py-3">
                        <p className="font-medium text-gray-900">{order.patientName}</p>
                        <p className="text-xs text-gray-400">{order.patientNumber}</p>
                      </td>
                      <td className="px-4 py-3 text-gray-700">{order.applianceType}</td>
                      <td className="px-4 py-3 text-gray-500">{order.labEntityName ?? order.labName ?? "—"}</td>
                      <td className="px-4 py-3 text-gray-500 text-xs">
                        {order.expectedDate ?? "—"}
                      </td>
                      <td className="px-4 py-3">
                        <span className={cn("text-xs", priorityCfg.color)}>{priorityCfg.label}</span>
                      </td>
                      <td className="px-4 py-3">
                        <span className={cn("inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium", statusCfg.color)}>
                          {statusCfg.icon}
                          {statusCfg.label}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          {order.status === "draft" && (
                            <button
                              type="button"
                              onClick={() => setEditOrder(order)}
                              className="text-xs text-cyan-800 hover:text-cyan-900 font-bold"
                            >
                              إكمال
                            </button>
                          )}
                          {nextStatus && nextStatus === "remake" ? (
                            <button
                              onClick={() => setRemakeOrder(order)}
                              className="text-xs text-purple-700 hover:text-purple-800 font-medium"
                            >
                              {NEXT_STATUS_LABELS[nextStatus] ?? "تقدّم"} ←
                            </button>
                          ) : nextStatus && (
                            <button
                              onClick={() => advanceMutation.mutate({ id: order.id, status: nextStatus })}
                              disabled={advanceMutation.isPending}
                              className="text-xs text-cyan-700 hover:text-cyan-800 font-medium disabled:opacity-50"
                            >
                              {NEXT_STATUS_LABELS[nextStatus] ?? "تقدّم"} ←
                            </button>
                          )}
                          {/* LABINV-REQ-009 — the alternative is retyping the case into
                              WhatsApp by hand, which is where tooth numbers and shades get
                              transposed. Sits next to Print because both are "get this to
                              the lab". */}
                          <SendToLabButton orderId={order.id} />
                          {/* Download PDF button — sends Authorization token */}
                          <button
                            type="button"
                            onClick={async () => {
                              try {
                                const filename = order.orderNumber
                                  ? `lab-order-${order.orderNumber}.pdf`
                                  : `lab-order-${order.id}.pdf`;
                                await downloadPdfFromApi(`/api/lab-orders/${order.id}/print`, filename);
                                toast.success("تم تحميل أمر العمل");
                              } catch (err) {
                                const reason = err instanceof Error ? err.message : "خطأ";
                                toast.error(`فشل تحميل أمر العمل: ${reason}`);
                              }
                            }}
                            className="text-xs text-green-600 hover:text-green-800 font-medium"
                            title="تحميل PDF"
                          >
                            <Download className="w-3.5 h-3.5 inline" /> PDF
                          </button>
                          {/* Print PDF button — prints the PDF itself, not the system page */}
                          <button
                            type="button"
                            onClick={async () => {
                              try {
                                const filename = order.orderNumber
                                  ? `lab-order-${order.orderNumber}.pdf`
                                  : `lab-order-${order.id}.pdf`;
                                await printPdfFromApi(`/api/lab-orders/${order.id}/print`, filename);
                              } catch (err) {
                                const reason = err instanceof Error ? err.message : "خطأ";
                                toast.error(`فشل طباعة أمر العمل: ${reason}`);
                              }
                            }}
                            className="text-xs text-purple-600 hover:text-purple-800 font-medium"
                            title="طباعة مباشرة"
                          >
                            <Printer className="w-3.5 h-3.5 inline" />
                          </button>
                        </div>
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
              className="px-3 py-1.5 text-sm border rounded-lg disabled:opacity-40 hover:bg-gray-50"
            >
              السابق
            </button>
            <span className="text-sm text-gray-500">
              {page} / {totalPages}
            </span>
            <button
              disabled={page === totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="px-3 py-1.5 text-sm border rounded-lg disabled:opacity-40 hover:bg-gray-50"
            >
              التالي
            </button>
          </div>
        )}
      </div>

      {showNew && <NewLabOrderModal onClose={() => setShowNew(false)} />}

      {/* Resolving to the list filtered by the order number — rather than to a detail
          route that does not exist — puts the scanned order in front of the user with
          every status action already attached to its row. */}
      {showScan && (
        <ScanOrderDialog
          onClose={() => setShowScan(false)}
          onResolved={(order) => {
            setShowScan(false);
            setStatusFilter("");
            setPage(1);
            setSearch(order.orderNumber ?? "");
          }}
        />
      )}

      {/* CORE-LAB-003: complete an incomplete draft (lab + cost) and send it. */}
      {editOrder && (
        <EditLabOrderModal order={editOrder} open onClose={() => setEditOrder(null)} />
      )}

      {remakeOrder && (
        <RemakeLabOrderModal order={remakeOrder} onClose={() => setRemakeOrder(null)} />
      )}
    </ErrorBoundary>
  );
}
