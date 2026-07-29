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
    name:             item?.name ?? "",
    category:         item?.category ?? "",
    quantity:         item?.quantity ?? 0,
    minQuantity:      item?.minQuantity ?? 0,
    unit:             item?.unit ?? "",
    costPerUnit:      item?.costPerUnit ?? undefined,
    // ── YOLO-S4 enhancements (optional) ────────────────────────────────────
    batchNumber:      item?.batchNumber ?? "",
    expiryDate:       item?.expiryDate ?? "",
    defaultSupplierId:item?.defaultSupplierId ?? undefined,
    minStockLevel:    item?.minStockLevel ? Number(item.minStockLevel) : undefined,
    purchaseUnit:     item?.purchaseUnit ?? "",
    consumptionUnit:  item?.consumptionUnit ?? "",
    imageUrl:         item?.imageUrl ?? "",
    warehouseLocation:item?.warehouseLocation ?? "",
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
      category:         form.category         || undefined,
      unit:             form.unit             || undefined,
      costPerUnit:      form.costPerUnit      || undefined,
      batchNumber:      form.batchNumber      || undefined,
      expiryDate:       form.expiryDate       || undefined,
      defaultSupplierId:form.defaultSupplierId || undefined,
      minStockLevel:    form.minStockLevel    || undefined,
      purchaseUnit:     form.purchaseUnit     || undefined,
      consumptionUnit:  form.consumptionUnit  || undefined,
      imageUrl:         form.imageUrl         || undefined,
      warehouseLocation:form.warehouseLocation|| undefined,
    });
  };

  const set = <K extends keyof typeof form>(k: K, v: typeof form[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const inputClass =
    "w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500";
  const labelClass = "text-sm font-medium text-gray-700";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <h2 className="text-lg font-bold text-gray-900">
            {isEdit ? "تعديل المادة" : "إضافة مادة جديدة"}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="space-y-1.5">
            <label className={labelClass}>اسم المادة *</label>
            <input
              required
              value={form.name}
              onChange={(e) => set("name", e.target.value)}
              className={inputClass}
            />
          </div>

          <div className="space-y-1.5">
            <label className={labelClass}>الفئة</label>
            <input
              value={form.category ?? ""}
              onChange={(e) => set("category", e.target.value)}
              placeholder="مثال: مواد تعقيم، حشوات، إبر..."
              className={inputClass}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className={labelClass}>الكمية الحالية *</label>
              <input
                type="number"
                min={0}
                required
                value={form.quantity}
                onChange={(e) => set("quantity", Number(e.target.value))}
                className={inputClass}
              />
            </div>
            <div className="space-y-1.5">
              <label className={labelClass}>الحد الأدنى *</label>
              <input
                type="number"
                min={0}
                required
                value={form.minQuantity}
                onChange={(e) => set("minQuantity", Number(e.target.value))}
                className={inputClass}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className={labelClass}>الوحدة</label>
              <input
                value={form.unit ?? ""}
                onChange={(e) => set("unit", e.target.value)}
                placeholder="قطعة، علبة، مل..."
                className={inputClass}
              />
            </div>
            <div className="space-y-1.5">
              <label className={labelClass}>التكلفة / وحدة (ر.ي)</label>
              <input
                type="number"
                min={0}
                step="0.01"
                value={form.costPerUnit ?? ""}
                onChange={(e) => set("costPerUnit", e.target.value ? Number(e.target.value) : undefined)}
                className={inputClass}
              />
            </div>
          </div>

          {/* ── YOLO-S4: Inventory enhancements ─────────────────────────────── */}
          <div className="pt-2 border-t border-gray-100">
            <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">
              تفاصيل إضافية
            </p>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className={labelClass}>رقم الدفعة</label>
              <input
                value={form.batchNumber ?? ""}
                onChange={(e) => set("batchNumber", e.target.value)}
                placeholder="LOT-2026-001"
                className={inputClass}
              />
            </div>
            <div className="space-y-1.5">
              <label className={labelClass}>تاريخ الانتهاء</label>
              <input
                type="date"
                value={form.expiryDate ?? ""}
                onChange={(e) => set("expiryDate", e.target.value)}
                className={inputClass}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className={labelClass}>الحد الأدنى للمخزون (عشري)</label>
              <input
                type="number"
                min={0}
                step="0.01"
                value={form.minStockLevel ?? ""}
                onChange={(e) => set("minStockLevel", e.target.value ? Number(e.target.value) : undefined)}
                placeholder="مثال: 2.5"
                className={inputClass}
              />
              <p className="text-[11px] text-gray-400">
                عتبة عشرية مستقلة عن «الحد الأدنى» أعلاه — للأصناف المُتعامل بها بالوزن أو الحجم.
              </p>
            </div>
            <div className="space-y-1.5">
              <label className={labelClass}>موقع المستودع</label>
              <input
                value={form.warehouseLocation ?? ""}
                onChange={(e) => set("warehouseLocation", e.target.value)}
                placeholder="مثال: رف A-3، ثلاجة 2"
                className={inputClass}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className={labelClass}>وحدة الشراء</label>
              <input
                value={form.purchaseUnit ?? ""}
                onChange={(e) => set("purchaseUnit", e.target.value)}
                placeholder="كرتون، عبوة، كيلو..."
                className={inputClass}
              />
            </div>
            <div className="space-y-1.5">
              <label className={labelClass}>وحدة الصرف</label>
              <input
                value={form.consumptionUnit ?? ""}
                onChange={(e) => set("consumptionUnit", e.target.value)}
                placeholder="قطعة، مل، جرام..."
                className={inputClass}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className={labelClass}>رابط الصورة</label>
            <input
              value={form.imageUrl ?? ""}
              onChange={(e) => set("imageUrl", e.target.value)}
              placeholder="https://..."
              className={inputClass}
              dir="ltr"
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
