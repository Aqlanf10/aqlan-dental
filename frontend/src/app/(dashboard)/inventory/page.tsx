"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Package, AlertTriangle, Search, Minus } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import type { InventoryItem } from "@/types/inventory";
import { InventoryFormModal } from "@/components/inventory/InventoryFormModal";
import { AdjustQuantityModal } from "@/components/inventory/AdjustQuantityModal";
import { cn } from "@/lib/utils";

export default function InventoryPage() {
  const [search, setSearch]         = useState("");
  const [category, setCategory]     = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [page, setPage]             = useState(1);
  const [showForm, setShowForm]     = useState(false);
  const [editItem, setEditItem]     = useState<InventoryItem | null>(null);
  const [adjustItem, setAdjustItem] = useState<InventoryItem | null>(null);

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

  return (
    <ErrorBoundary>
      <div className="space-y-6">
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
              <TableSkeleton rows={6} cols={6} />
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-gray-400">
              <Package className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد مواد</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["المادة", "الفئة", "الكمية", "الحد الأدنى", "الوحدة", "التكلفة / وحدة", ""].map((h) => (
                    <th key={h} className="text-right px-4 py-3 font-medium text-gray-500 text-xs">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {items.map((item) => (
                  <tr
                    key={item.id}
                    className={cn(
                      "hover:bg-gray-50 transition-colors",
                      item.isLowStock && "bg-red-50/40"
                    )}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {item.isLowStock && (
                          <AlertTriangle className="w-3.5 h-3.5 text-red-500 flex-shrink-0" />
                        )}
                        <span className="font-medium text-gray-900">{item.name}</span>
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
                        ? `${item.costPerUnit.toLocaleString("ar-YE")} ر.ي`
                        : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 justify-end">
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
                ))}
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
    </ErrorBoundary>
  );
}
