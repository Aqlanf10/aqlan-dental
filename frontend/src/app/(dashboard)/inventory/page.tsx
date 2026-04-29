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
            <h1 className="text-2xl font-bold text-[#0d2137]">المخزون</h1>
            {lowStockCount > 0 && (
              <p className="text-sm text-[#ef4444] mt-0.5 flex items-center gap-1">
                <AlertTriangle className="w-3.5 h-3.5" />
                {lowStockCount} مادة وصلت للحد الأدنى
              </p>
            )}
          </div>
          <button
            onClick={() => { setEditItem(null); setShowForm(true); }}
            className="flex items-center gap-2 bg-accent-blue text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-blue-hover transition-colors"
          >
            <Plus className="w-4 h-4" />
            إضافة مادة
          </button>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-52">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#94a3b8]" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث بالاسم أو الفئة..."
              className="w-full border border-[#e8f0f9] rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-accent-blue"
            />
          </div>
          {categoriesData && categoriesData.length > 0 && (
            <select
              value={category}
              onChange={(e) => { setCategory(e.target.value); setPage(1); }}
              className="border border-[#e8f0f9] rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-accent-blue"
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
              className="rounded border-[#dce8f5] text-accent-blue focus:ring-accent-blue"
            />
            <span className="text-sm text-[#64748b]">الحد الأدنى فقط</span>
          </label>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-[#f1f5f9] shadow-card overflow-hidden">
          {isLoading ? (
            <div className="p-6">
              <TableSkeleton rows={6} cols={6} />
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-[#94a3b8]">
              <Package className="w-10 h-10 mb-3" />
              <p className="font-medium">لا توجد مواد</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] border-b border-[#f1f5f9]">
                <tr>
                  {["المادة", "الفئة", "الكمية", "الحد الأدنى", "الوحدة", "التكلفة / وحدة", ""].map((h) => (
                    <th key={h} className="text-right px-4 py-3 font-medium text-[#64748b] text-xs">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {items.map((item) => (
                  <tr
                    key={item.id}
                    className={cn(
                      "hover:bg-[#f7fafd] transition-colors",
                      item.isLowStock && "bg-[#ef444418]/40"
                    )}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {item.isLowStock && (
                          <AlertTriangle className="w-3.5 h-3.5 text-[#ef4444] flex-shrink-0" />
                        )}
                        <span className="font-medium text-[#0d2137]">{item.name}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      {item.category ? (
                        <span className="bg-[#eef3f9] text-[#64748b] text-xs px-[10px] py-[2px] rounded-full">
                          {item.category}
                        </span>
                      ) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <span className={cn(
                        "font-semibold",
                        item.isLowStock ? "text-[#ef4444]" : "text-[#0d2137]"
                      )}>
                        {item.quantity}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-[#64748b]">{item.minQuantity}</td>
                    <td className="px-4 py-3 text-[#64748b]">{item.unit ?? "—"}</td>
                    <td className="px-4 py-3 text-[#64748b]">
                      {item.costPerUnit != null
                        ? `${item.costPerUnit.toLocaleString("ar-YE")} ر.ي`
                        : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2 justify-end">
                        <button
                          onClick={() => setAdjustItem(item)}
                          className="text-xs text-accent-blue hover:text-blue-hover font-medium flex items-center gap-0.5"
                          title="تعديل الكمية"
                        >
                          <Minus className="w-3 h-3" />/<Plus className="w-3 h-3" />
                        </button>
                        <button
                          onClick={() => { setEditItem(item); setShowForm(true); }}
                          className="text-xs text-[#64748b] hover:text-[#64748b] font-medium"
                        >
                          تعديل
                        </button>
                        <button
                          onClick={() => {
                            if (confirm(`هل تريد حذف "${item.name}"؟`))
                              deleteMutation.mutate(item.id);
                          }}
                          className="text-xs text-[#ef4444] hover:text-[#ef4444] font-medium"
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
              className="px-3 py-1.5 text-sm border rounded-lg disabled:opacity-40 hover:bg-[#f7fafd]"
            >
              السابق
            </button>
            <span className="text-sm text-[#64748b]">{page} / {totalPages}</span>
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
