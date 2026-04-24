"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X, Plus, Minus } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InventoryItem } from "@/types/inventory";
import { cn } from "@/lib/utils";

interface Props {
  item: InventoryItem;
  onClose: () => void;
}

export function AdjustQuantityModal({ item, onClose }: Props) {
  const queryClient = useQueryClient();
  const [delta, setDelta]   = useState(1);
  const [reason, setReason] = useState("");
  const [mode, setMode]     = useState<"add" | "remove">("add");

  const actualDelta = mode === "add" ? Math.abs(delta) : -Math.abs(delta);
  const newQty      = item.quantity + actualDelta;
  const isValid     = newQty >= 0 && delta > 0;

  const mutation = useMutation({
    mutationFn: async () => {
      const res = await api.put<{ newQuantity: number; isLowStock: boolean }>(
        `/api/inventory/${item.id}/adjust`,
        { delta: actualDelta, reason: reason || undefined }
      );
      return res.data;
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      queryClient.invalidateQueries({ queryKey: ["inventory-low-stock-count"] });
      toast.success(`تم تحديث الكمية: ${item.name} — الكمية الجديدة: ${data.newQuantity}`);
      if (data.isLowStock) toast.info("تنبيه: الكمية وصلت إلى الحد الأدنى");
      onClose();
    },
    onError: (error: { response?: { data?: { message?: string } } }) => {
      toast.error(error.response?.data?.message ?? "فشل تعديل الكمية");
    },
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-sm mx-4">
        <div className="flex items-center justify-between p-6 border-b">
          <h2 className="text-lg font-bold text-gray-900">تعديل الكمية</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-4">
          <div className="bg-gray-50 rounded-xl p-4">
            <p className="font-semibold text-gray-900">{item.name}</p>
            <p className="text-sm text-gray-500 mt-0.5">
              الكمية الحالية: <span className="font-bold text-gray-800">{item.quantity}</span>
              {item.unit ? ` ${item.unit}` : ""}
            </p>
          </div>

          {/* Mode toggle */}
          <div className="flex gap-2">
            <button
              onClick={() => setMode("add")}
              className={cn(
                "flex-1 flex items-center justify-center gap-1.5 py-2 rounded-lg border text-sm font-medium transition-colors",
                mode === "add"
                  ? "border-green-500 bg-green-50 text-green-700"
                  : "border-gray-200 text-gray-500 hover:border-gray-300"
              )}
            >
              <Plus className="w-4 h-4" />
              إضافة
            </button>
            <button
              onClick={() => setMode("remove")}
              className={cn(
                "flex-1 flex items-center justify-center gap-1.5 py-2 rounded-lg border text-sm font-medium transition-colors",
                mode === "remove"
                  ? "border-red-500 bg-red-50 text-red-700"
                  : "border-gray-200 text-gray-500 hover:border-gray-300"
              )}
            >
              <Minus className="w-4 h-4" />
              سحب
            </button>
          </div>

          {/* Quantity input */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">الكمية *</label>
            <input
              type="number"
              min={1}
              value={delta}
              onChange={(e) => setDelta(Math.max(1, Number(e.target.value)))}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          {/* Preview */}
          <div className={cn(
            "rounded-lg px-4 py-3 text-sm font-medium",
            newQty < 0 ? "bg-red-50 text-red-700" :
            newQty <= item.minQuantity ? "bg-amber-50 text-amber-700" :
            "bg-green-50 text-green-700"
          )}>
            {newQty < 0
              ? "⚠️ الكمية ستصبح سالبة — غير مسموح"
              : `الكمية الجديدة: ${newQty}${item.unit ? ` ${item.unit}` : ""}`
            }
          </div>

          {/* Reason */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">السبب (اختياري)</label>
            <input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="مثال: استخدام في المريض، استلام شحنة..."
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
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
              onClick={() => mutation.mutate()}
              disabled={!isValid || mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الحفظ..." : "تأكيد"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
