"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import {
  Plus, Package, AlertTriangle, Search, Minus, Clock, X,
  TrendingDown, BarChart3, Truck, CalendarClock, MapPin, ImageIcon,
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { formatYemeniRiyal, formatArabicDate, cn } from "@/lib/utils";
import type { InventoryItem, InventoryAdjustment } from "@/types/inventory";
import { InventoryFormModal } from "@/components/inventory/InventoryFormModal";
import { AdjustQuantityModal } from "@/components/inventory/AdjustQuantityModal";

/* ─── Adjustment History Modal ─────────────────────────────────────────────── */
function AdjustmentHistoryModal({
  item,
  onClose,
}: {
  item: InventoryItem;
  onClose: () => void;
}) {
  const { data: adjustments, isLoading } = useQuery({
    queryKey: ["inventory-adjustments", item.id],
    queryFn: async () => {
      const res = await api.get<InventoryAdjustment[]>(
        `/api/inventory/${item.id}/adjustments`
      );
      return res.data;
    },
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-lg mx-4 max-h-[80vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <h2 className="text-lg font-bold text-gray-900">
            تاريخ التعديلات — {item.name}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6">
          {isLoading ? (
            <div className="space-y-3 animate-pulse">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-14 bg-gray-100 rounded-lg" />
              ))}
            </div>
          ) : !adjustments || adjustments.length === 0 ? (
            <div className="text-center py-10 text-gray-400">
              <Clock className="w-8 h-8 mx-auto mb-2" />
              <p className="font-medium">لا يوجد تاريخ تعديلات</p>
            </div>
          ) : (
            <div className="space-y-2 max-h-96 overflow-y-auto">
              {adjustments.map((adj) => (
                <div
                  key={adj.id}
                  className="flex items-center justify-between border border-gray-100 rounded-lg p-3"
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span
                        className={cn(
                          "text-xs px-2 py-0.5 rounded-full font-medium",
                          adj.delta > 0
                            ? "bg-green-50 text-green-700"
                            : "bg-red-50 text-red-700"
                        )}
                      >
                        {adj.delta > 0 ? "+" : ""}
                        {adj.delta}
                      </span>
                      <span className="text-sm text-gray-700">
                        {adj.previousQuantity} → {adj.newQuantity}
                      </span>
                    </div>
                    {adj.reason && (
                      <p className="text-xs text-gray-400 mt-0.5 truncate">
                        {adj.reason}
                      </p>
                    )}
                  </div>
                  <div className="text-left flex-shrink-0 mr-3">
                    <p className="text-[11px] text-gray-400">
                      {formatArabicDate(adj.createdAt)}
                    </p>
                    <p className="text-[10px] text-gray-300">
                      {adj.adjustmentType}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ─── Main Inventory Page ──────────────────────────────────────────────────── */
export default function InventoryPage() {
  const [search, setSearch]         = useState("");
  const [category, setCategory]     = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [page, setPage]             = useState(1);
  const [showForm, setShowForm]     = useState(false);
  const [editItem, setEditItem]     = useState<InventoryItem | null>(null);
  const [adjustItem, setAdjustItem] = useState<InventoryItem | null>(null);
  const [historyItem, setHistoryItem] = useState<InventoryItem | null>(null);

  const queryClient = useQueryClient();

  const { data: categoriesData } = useQuery({
    queryKey: ["inventory-categories"],
    queryFn: async () => {
      const res = await api.get<string[]>("/api/inventory/categories");
      return res.data;
    },
  });

  const { data, isLoading } = useQuery({
    queryKey: ["inventory", category, lowStockOnly, page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: "25" });
      if (category) params.set("category", category);
      if (lowStockOnly) params.set("lowStock", "true");
      const res = await api.get<{ data: InventoryItem[]; total: number; page: number; pageSize: number }>(
        `/api/inventory?${params}`
      );
      return res.data;
    },
  });

  const { data: lowStockData } = useQuery({
    queryKey: ["inventory-low-stock-count"],
    queryFn: async () => {
      const res = await api.get<InventoryItem[]>("/api/inventory/low-stock");
      return res.data;
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => api.delete(`/api/inventory/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      queryClient.invalidateQueries({ queryKey: ["inventory-low-stock-count"] });
      toast.success("تم حذف المادة");
    },
    onError: () => toast.error("فشل الحذف"),
  });

  const items = (data?.data ?? []).filter((i) =>
    search.trim() === "" ||
    i.name.toLowerCase().includes(search.toLowerCase()) ||
    (i.category ?? "").toLowerCase().includes(search.toLowerCase())
  );

  const totalPages = Math.ceil((data?.total ?? 0) / 25);
  const lowStockCount = lowStockData?.length ?? 0;

  /* ── Valuation summary ──────────────────────────────────────────────────── */
  const allItems = data?.data ?? [];
  const totalItems = data?.total ?? 0;
  const totalValue = allItems.reduce(
    (sum, i) => sum + (i.costPerUnit ?? 0) * i.quantity,
    0
  );

  /* ── Expiring soon (within 30 days) ─────────────────────────────────────── */
  const now = new Date();
  const thirtyDays = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);
  const expiringItems = allItems.filter((i) => {
    if (!i.expiryDate) return false;
    const exp = new Date(i.expiryDate);
    return exp <= thirtyDays;
  });

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Navigation Tabs */}
        <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit">
          <span className="px-4 py-2 text-sm font-medium rounded-md bg-white shadow-sm text-gray-900">
            المخزون
          </span>
          <Link
            href="/inventory/suppliers"
            className="px-4 py-2 text-sm font-medium rounded-md text-gray-500 hover:text-gray-700 transition-colors"
          >
            الموردين
          </Link>
          <Link
            href="/inventory/purchases"
            className="px-4 py-2 text-sm font-medium rounded-md text-gray-500 hover:text-gray-700 transition-colors"
          >
            أوامر الشراء
          </Link>
        </div>

        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">المخزون</h1>
            {lowStockCount > 0 && (
              <p className="text-sm text-red-600 mt-0.5 flex items-center gap-1">
                <AlertTriangle className="w-3.5 h-3.5" />
                {lowStockCount} مادة وصلت للحد الأدنى
              </p>
            )}
          </div>
          <button
            onClick={() => { setEditItem(null); setShowForm(true); }}
            className="flex items-center gap-2 bg-cyan-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors"
          >
            <Plus className="w-4 h-4" />
            إضافة مادة
          </button>
        </div>

        {/* Expiry Alert Banner */}
        {expiringItems.length > 0 && (
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 flex items-start gap-3">
            <CalendarClock className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
            <div>
              <p className="font-medium text-amber-800">
                تنبيه: {expiringItems.length} مادة قاربت على الانتهاء
              </p>
              <p className="text-sm text-amber-700 mt-0.5">
                {expiringItems.map((i) => i.name).join("، ")}
              </p>
            </div>
          </div>
        )}

        {/* Valuation Summary Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-clinic-blue-50 flex items-center justify-center flex-shrink-0">
              <Package className="w-5 h-5 text-clinic-blue" />
            </div>
            <div>
              <p className="text-xs text-gray-500">إجمالي المواد</p>
              <p className="text-xl font-bold text-gray-900">{totalItems}</p>
            </div>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-green-50 flex items-center justify-center flex-shrink-0">
              <BarChart3 className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">القيمة الإجمالية</p>
              <p className="text-xl font-bold text-gray-900 font-mono">
                {formatYemeniRiyal(totalValue)}
              </p>
            </div>
          </div>
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4 flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-red-50 flex items-center justify-center flex-shrink-0">
              <TrendingDown className="w-5 h-5 text-red-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">مواد منخفضة المخزون</p>
              <p className="text-xl font-bold text-red-600">{lowStockCount}</p>
            </div>
          </div>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-52">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالاسم أو الفئة..."
              className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>
          {categoriesData && categoriesData.length > 0 && (
            <select
              value={category}
              onChange={(e) => { setCategory(e.target.value); setPage(1); }}
              className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            >
              <option value="">جميع الفئات</option>
              {categoriesData.map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
          )}
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={lowStockOnly}
              onChange={(e) => { setLowStockOnly(e.target.checked); setPage(1); }}
              className="rounded border-gray-300 text-cyan-600 focus:ring-cyan-500"
            />
            <span className="text-sm text-gray-700">الحد الأدنى فقط</span>
          </label>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
          {isLoading ? (
            <div className="p-6">
              <TableSkeleton rows={6} cols={9} />
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-gray-400">
              <Package className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد مواد</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 border-b border-gray-100">
                  <tr>
                    {[
                      "المادة",
                      "الفئة",
                      "الكمية",
                      "الحد الأدنى",
                      "الوحدة",
                      "التكلفة / وحدة",
                      "رقم الدفعة",
                      "تاريخ الانتهاء",
                      "موقع المستودع",
                      "المورد الافتراضي",
                      "",
                    ].map((h) => (
                      <th key={h} className="text-right px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {items.map((item) => {
                    const isExpiringSoon =
                      item.expiryDate &&
                      new Date(item.expiryDate) <= thirtyDays;
                    const isExpired =
                      item.expiryDate &&
                      new Date(item.expiryDate) <= now;

                    return (
                      <tr
                        key={item.id}
                        className={cn(
                          "hover:bg-gray-50 transition-colors",
                          item.isLowStock && "bg-red-50/40",
                          isExpired && "bg-red-50/60"
                        )}
                      >
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            {item.imageUrl ? (
                              // eslint-disable-next-line @next/next/no-img-element
                              <img
                                src={item.imageUrl}
                                alt={item.name}
                                className="w-8 h-8 rounded-md object-cover border border-gray-200 flex-shrink-0"
                                onError={(e) => {
                                  (e.currentTarget as HTMLImageElement).style.display = "none";
                                }}
                              />
                            ) : (
                              <div className="w-8 h-8 rounded-md bg-gray-100 flex items-center justify-center flex-shrink-0">
                                <ImageIcon className="w-4 h-4 text-gray-400" />
                              </div>
                            )}
                            <div className="flex flex-col">
                              <div className="flex items-center gap-1.5">
                                {item.isLowStock && (
                                  <AlertTriangle className="w-3.5 h-3.5 text-red-500 flex-shrink-0" />
                                )}
                                <span className="font-medium text-gray-900">{item.name}</span>
                              </div>
                              {item.isBelowMinStockLevel && (
                                <span className="text-[10px] text-amber-700 bg-amber-50 px-1.5 py-0.5 rounded-full w-fit mt-0.5">
                                  أقل من حد المخزون ({item.minStockLevel})
                                </span>
                              )}
                              {(item.purchaseUnit || item.consumptionUnit) && (
                                <span className="text-[10px] text-gray-400 mt-0.5">
                                  {item.purchaseUnit && `شراء: ${item.purchaseUnit}`}
                                  {item.purchaseUnit && item.consumptionUnit && " · "}
                                  {item.consumptionUnit && `صرف: ${item.consumptionUnit}`}
                                </span>
                              )}
                            </div>
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          {item.category ? (
                            <span className="bg-gray-100 text-gray-600 text-xs px-2 py-0.5 rounded-full">
                              {item.category}
                            </span>
                          ) : "—"}
                        </td>
                        <td className="px-4 py-3">
                          <span className={cn(
                            "font-semibold",
                            item.isLowStock ? "text-red-600" : "text-gray-900"
                          )}>
                            {item.quantity}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-gray-500">{item.minQuantity}</td>
                        <td className="px-4 py-3 text-gray-500">{item.unit ?? "—"}</td>
                        <td className="px-4 py-3 text-gray-500">
                          {item.costPerUnit != null
                            ? formatYemeniRiyal(item.costPerUnit)
                            : "—"}
                        </td>
                        <td className="px-4 py-3 text-gray-500 font-mono text-xs">
                          {item.batchNumber ?? "—"}
                        </td>
                        <td className="px-4 py-3">
                          {item.expiryDate ? (
                            <span
                              className={cn(
                                "text-xs font-medium",
                                isExpired
                                  ? "text-red-600"
                                  : isExpiringSoon
                                    ? "text-amber-600"
                                    : "text-gray-500"
                              )}
                            >
                              {formatArabicDate(item.expiryDate)}
                              {isExpired && " (منتهي)"}
                              {!isExpired && isExpiringSoon && " (قريب)"}
                            </span>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {item.warehouseLocation ? (
                            <span className="flex items-center gap-1 text-xs text-gray-600">
                              <MapPin className="w-3 h-3 text-gray-400" />
                              {item.warehouseLocation}
                            </span>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {item.defaultSupplierName ? (
                            <span className="flex items-center gap-1 text-xs text-clinic-blue">
                              <Truck className="w-3 h-3" />
                              {item.defaultSupplierName}
                            </span>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2 justify-end whitespace-nowrap">
                            <button
                              onClick={() => setHistoryItem(item)}
                              className="text-xs text-gray-500 hover:text-gray-700 font-medium flex items-center gap-0.5"
                              title="تاريخ التعديلات"
                            >
                              <Clock className="w-3 h-3" />
                              السجل
                            </button>
                            <button
                              onClick={() => setAdjustItem(item)}
                              className="text-xs text-cyan-700 hover:text-cyan-800 font-medium flex items-center gap-0.5"
                              title="تعديل الكمية"
                            >
                              <Minus className="w-3 h-3" />/<Plus className="w-3 h-3" />
                            </button>
                            <button
                              onClick={() => { setEditItem(item); setShowForm(true); }}
                              className="text-xs text-gray-500 hover:text-gray-700 font-medium"
                            >
                              تعديل
                            </button>
                            <button
                              onClick={() => {
                                if (confirm(`هل تريد حذف "${item.name}"؟`))
                                  deleteMutation.mutate(item.id);
                              }}
                              className="text-xs text-red-500 hover:text-red-700 font-medium"
                            >
                              حذف
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
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
            <span className="text-sm text-gray-500">{page} / {totalPages}</span>
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

      {showForm && (
        <InventoryFormModal
          item={editItem}
          onClose={() => { setShowForm(false); setEditItem(null); }}
        />
      )}
      {adjustItem && (
        <AdjustQuantityModal
          item={adjustItem}
          onClose={() => setAdjustItem(null)}
        />
      )}
      {historyItem && (
        <AdjustmentHistoryModal
          item={historyItem}
          onClose={() => setHistoryItem(null)}
        />
      )}
    </ErrorBoundary>
  );
}
