"use client";

import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { InventoryItem, CreateInventoryItemRequest } from "@/types/inventory";

interface Props {
  item: InventoryItem | null;
  onClose: () => void;
}

export function InventoryFormModal({ item, onClose }: Props) {
  const queryClient = useQueryClient();
  const isEdit = item !== null;

  const [form, setForm] = useState<CreateInventoryItemRequest>({
    name:        item?.name ?? "",
    category:    item?.category ?? "",
    quantity:    item?.quantity ?? 0,
    minQuantity: item?.minQuantity ?? 0,
    unit:        item?.unit ?? "",
    costPerUnit: item?.costPerUnit ?? undefined,
  });

  const mutation = useMutation({
    mutationFn: async (data: CreateInventoryItemRequest) => {
      if (isEdit) {
        await api.put(`/api/inventory/${item!.id}`, data);
      } else {
        await api.post("/api/inventory", data);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      queryClient.invalidateQueries({ queryKey: ["inventory-categories"] });
      queryClient.invalidateQueries({ queryKey: ["inventory-low-stock-count"] });
      toast.success(isEdit ? "تم تحديث المادة" : "تم إضافة المادة");
      onClose();
    },
    onError: () => toast.error(isEdit ? "فشل التحديث" : "فشل الإضافة"),
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    mutation.mutate({
      ...form,
      category:    form.category    || undefined,
      unit:        form.unit        || undefined,
      costPerUnit: form.costPerUnit || undefined,
    });
  };

  const set = <K extends keyof typeof form>(k: K, v: typeof form[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-md mx-4">
        <div className="flex items-center justify-between p-6 border-b">
          <h2 className="text-lg font-bold text-gray-900">
            {isEdit ? "تعديل المادة" : "إضافة مادة جديدة"}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">اسم المادة *</label>
            <input
              required
              value={form.name}
              onChange={(e) => set("name", e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">الفئة</label>
            <input
              value={form.category ?? ""}
              onChange={(e) => set("category", e.target.value)}
              placeholder="مثال: مواد تعقيم، حشوات، إبر..."
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">الكمية الحالية *</label>
              <input
                type="number"
                min={0}
                required
                value={form.quantity}
                onChange={(e) => set("quantity", Number(e.target.value))}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">الحد الأدنى *</label>
              <input
                type="number"
                min={0}
                required
                value={form.minQuantity}
                onChange={(e) => set("minQuantity", Number(e.target.value))}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">الوحدة</label>
              <input
                value={form.unit ?? ""}
                onChange={(e) => set("unit", e.target.value)}
                placeholder="قطعة، علبة، مل..."
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">التكلفة / وحدة (ر.ي)</label>
              <input
                type="number"
                min={0}
                step="0.01"
                value={form.costPerUnit ?? ""}
                onChange={(e) => set("costPerUnit", e.target.value ? Number(e.target.value) : undefined)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
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
              disabled={mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الحفظ..." : isEdit ? "حفظ التعديلات" : "إضافة"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
