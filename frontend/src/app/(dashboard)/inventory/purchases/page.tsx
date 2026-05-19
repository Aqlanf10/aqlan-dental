"use client";

import { useState, useMemo, type FormEvent } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import {
  ShoppingCart, Plus, Search, Eye, PackageOpen, X, FileText, Truck,
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { formatYemeniRiyal, formatArabicDate, cn } from "@/lib/utils";
import type {
  PurchaseOrder, PurchaseOrderStatus, CreatePurchaseOrderRequest,
  Supplier, InventoryItem, ReceivePurchaseOrderRequest,
} from "@/types/inventory";

/* ─── Status helpers ────────────────────────────────────────────────────────── */
const STATUS_LABELS: Record<PurchaseOrderStatus, string> = {
  Draft: "مسودة",
  Submitted: "مُرسل",
  PartiallyReceived: "مستلم جزئياً",
  Received: "مستلم",
  Cancelled: "ملغي",
};

const STATUS_BADGE_CLASSES: Record<PurchaseOrderStatus, string> = {
  Draft: "bg-blue-50 text-blue-700 border-blue-200",
  Submitted: "bg-amber-50 text-amber-700 border-amber-200",
  PartiallyReceived: "bg-orange-50 text-orange-700 border-orange-200",
  Received: "bg-green-50 text-green-700 border-green-200",
  Cancelled: "bg-gray-100 text-gray-500 border-gray-200",
};

const STATUS_TABS: { value: PurchaseOrderStatus | "All"; label: string }[] = [
  { value: "All", label: "الكل" },
  { value: "Draft", label: "مسودة" },
  { value: "Submitted", label: "مُرسل" },
  { value: "PartiallyReceived", label: "مستلم جزئياً" },
  { value: "Received", label: "مستلم" },
  { value: "Cancelled", label: "ملغي" },
];

/* ─── Create PO Modal ──────────────────────────────────────────────────────── */
interface LineItemDraft {
  key: string;
  inventoryItemId?: string;
  itemName: string;
  quantity: number;
  unitCost: number;
}

function CreatePOModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const [supplierId, setSupplierId] = useState("");
  const [expectedDate, setExpectedDate] = useState("");
  const [notes, setNotes] = useState("");
  const [lineItems, setLineItems] = useState<LineItemDraft[]>([]);
  const [itemSearch, setItemSearch] = useState("");
  const [showItemSearch, setShowItemSearch] = useState(false);

  const { data: suppliers } = useQuery({
    queryKey: ["suppliers"],
    queryFn: async () => {
      const res = await api.get<Supplier[]>("/api/suppliers");
      return res.data;
    },
  });

  const { data: inventoryItems } = useQuery({
    queryKey: ["inventory-all"],
    queryFn: async () => {
      const res = await api.get<{ data: InventoryItem[] }>("/api/inventory?pageSize=200");
      return res.data.data;
    },
  });

  const filteredItems = useMemo(() => {
    if (!inventoryItems) return [];
    if (!itemSearch.trim()) return inventoryItems.slice(0, 20);
    return inventoryItems.filter((i) =>
      i.name.toLowerCase().includes(itemSearch.toLowerCase())
    ).slice(0, 20);
  }, [inventoryItems, itemSearch]);

  const addLineItem = (invItem?: InventoryItem) => {
    setLineItems((prev) => [
      ...prev,
      {
        key: crypto.randomUUID(),
        inventoryItemId: invItem?.id,
        itemName: invItem?.name ?? "",
        quantity: 1,
        unitCost: invItem?.costPerUnit ?? 0,
      },
    ]);
    setShowItemSearch(false);
    setItemSearch("");
  };

  const removeLineItem = (key: string) =>
    setLineItems((prev) => prev.filter((li) => li.key !== key));

  const updateLineItem = (key: string, field: keyof LineItemDraft, value: string | number) =>
    setLineItems((prev) =>
      prev.map((li) => (li.key === key ? { ...li, [field]: value } : li))
    );

  const subtotal = lineItems.reduce((sum, li) => sum + li.quantity * li.unitCost, 0);

  const mutation = useMutation({
    mutationFn: async (data: CreatePurchaseOrderRequest) => {
      await api.post("/api/purchase-orders", data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
      toast.success("تم إنشاء أمر الشراء");
      onClose();
    },
    onError: () => toast.error("فشل إنشاء أمر الشراء"),
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!supplierId) {
      toast.error("يرجى اختيار المورد");
      return;
    }
    if (lineItems.length === 0) {
      toast.error("يرجى إضافة عنصر واحد على الأقل");
      return;
    }
    const hasEmptyName = lineItems.some((li) => !li.itemName.trim());
    if (hasEmptyName) {
      toast.error("يرجى إدخال اسم العنصر لجميع البنود");
      return;
    }
    mutation.mutate({
      supplierId,
      expectedDate: expectedDate || undefined,
      notes: notes || undefined,
      lineItems: lineItems.map((li) => ({
        inventoryItemId: li.inventoryItemId,
        itemName: li.itemName,
        quantity: li.quantity,
        unitCost: li.unitCost,
      })),
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-2xl mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <h2 className="text-lg font-bold text-gray-900">أمر شراء جديد</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          {/* Supplier */}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">المورد *</label>
            <select
              value={supplierId}
              onChange={(e) => setSupplierId(e.target.value)}
              required
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            >
              <option value="">اختر المورد...</option>
              {suppliers?.filter((s) => s.isActive).map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
          </div>

          {/* Expected Date */}
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">تاريخ الاستلام المتوقع</label>
              <input
                type="date"
                value={expectedDate}
                onChange={(e) => setExpectedDate(e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">ملاحظات</label>
              <input
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="ملاحظات إضافية..."
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
          </div>

          {/* Line Items */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <label className="text-sm font-medium text-gray-700">بنود الأمر</label>
              <button
                type="button"
                onClick={() => setShowItemSearch(true)}
                className="text-xs text-cyan-700 hover:text-cyan-800 font-medium flex items-center gap-1"
              >
                <Plus className="w-3 h-3" />
                إضافة من المخزون
              </button>
            </div>

            {/* Item Search Dropdown */}
            {showItemSearch && (
              <div className="border border-gray-200 rounded-lg p-3 bg-gray-50 space-y-2">
                <div className="relative">
                  <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    value={itemSearch}
                    onChange={(e) => setItemSearch(e.target.value)}
                    placeholder="بحث في المواد..."
                    autoFocus
                    className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                  />
                </div>
                <div className="max-h-40 overflow-y-auto space-y-1">
                  {filteredItems.map((inv) => (
                    <button
                      key={inv.id}
                      type="button"
                      onClick={() => addLineItem(inv)}
                      className="w-full text-right px-3 py-2 text-sm hover:bg-white rounded-lg transition-colors flex items-center justify-between"
                    >
                      <span className="font-medium text-gray-900">{inv.name}</span>
                      <span className="text-gray-400 text-xs">
                        {inv.costPerUnit ? formatYemeniRiyal(inv.costPerUnit) : "—"} / وحدة
                      </span>
                    </button>
                  ))}
                  {filteredItems.length === 0 && (
                    <p className="text-xs text-gray-400 text-center py-2">لا توجد نتائج</p>
                  )}
                </div>
                <button
                  type="button"
                  onClick={() => {
                    addLineItem();
                  }}
                  className="text-xs text-cyan-700 hover:text-cyan-800 font-medium"
                >
                  + إضافة عنصر مخصص
                </button>
              </div>
            )}

            {/* Line items list */}
            {lineItems.length === 0 ? (
              <div className="text-center py-8 text-gray-400 text-sm border border-dashed border-gray-200 rounded-lg">
                لم يتم إضافة بنود بعد
              </div>
            ) : (
              <div className="space-y-2">
                {lineItems.map((li) => (
                  <div
                    key={li.key}
                    className="flex items-end gap-2 border border-gray-100 rounded-lg p-3 bg-gray-50/50"
                  >
                    <div className="flex-1 min-w-0">
                      <label className="text-[11px] text-gray-500">اسم العنصر</label>
                      <input
                        value={li.itemName}
                        onChange={(e) => updateLineItem(li.key, "itemName", e.target.value)}
                        className="w-full border border-gray-200 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                      />
                    </div>
                    <div className="w-20">
                      <label className="text-[11px] text-gray-500">الكمية</label>
                      <input
                        type="number"
                        min={1}
                        value={li.quantity}
                        onChange={(e) => updateLineItem(li.key, "quantity", Math.max(1, Number(e.target.value)))}
                        className="w-full border border-gray-200 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                      />
                    </div>
                    <div className="w-28">
                      <label className="text-[11px] text-gray-500">التكلفة / وحدة</label>
                      <input
                        type="number"
                        min={0}
                        step="0.01"
                        value={li.unitCost}
                        onChange={(e) => updateLineItem(li.key, "unitCost", Number(e.target.value))}
                        className="w-full border border-gray-200 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                      />
                    </div>
                    <div className="w-24 text-left">
                      <label className="text-[11px] text-gray-500">الإجمالي</label>
                      <p className="text-sm font-mono font-semibold text-gray-700 py-1.5">
                        {formatYemeniRiyal(li.quantity * li.unitCost)}
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => removeLineItem(li.key)}
                      className="text-red-400 hover:text-red-600 pb-1.5"
                    >
                      <X className="w-4 h-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}

            {/* Subtotal */}
            {lineItems.length > 0 && (
              <div className="flex items-center justify-between border-t border-gray-100 pt-3">
                <span className="text-sm font-medium text-gray-700">المجموع الفرعي</span>
                <span className="text-lg font-bold font-mono text-gray-900">
                  {formatYemeniRiyal(subtotal)}
                </span>
              </div>
            )}
          </div>

          {/* Actions */}
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
              {mutation.isPending ? "جارٍ الإنشاء..." : "إنشاء أمر الشراء"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ─── Receive PO Modal ─────────────────────────────────────────────────────── */
function ReceivePOModal({
  po,
  onClose,
}: {
  po: PurchaseOrder;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [receivedQtys, setReceivedQtys] = useState<Record<string, number>>(
    Object.fromEntries(po.lineItems.map((li) => [li.id, li.receivedQuantity]))
  );

  const mutation = useMutation({
    mutationFn: async (data: ReceivePurchaseOrderRequest) => {
      await api.patch(`/api/purchase-orders/${po.id}/receive`, data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
      queryClient.invalidateQueries({ queryKey: ["inventory"] });
      toast.success("تم تحديث الاستلام");
      onClose();
    },
    onError: () => toast.error("فشل تحديث الاستلام"),
  });

  const handleSubmit = () => {
    const lineItems = po.lineItems.map((li) => ({
      id: li.id,
      receivedQuantity: receivedQtys[li.id] ?? li.receivedQuantity,
    }));
    mutation.mutate({ lineItems });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-xl mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <h2 className="text-lg font-bold text-gray-900">
            استلام أمر الشراء — {po.orderNumber}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            ×
          </button>
        </div>

        <div className="p-6 space-y-4">
          {/* PO Info */}
          <div className="bg-gray-50 rounded-xl p-4 space-y-1 text-sm">
            <p>
              <span className="text-gray-500">المورد:</span>{" "}
              <span className="font-medium">{po.supplierName ?? "—"}</span>
            </p>
            <p>
              <span className="text-gray-500">تاريخ الطلب:</span>{" "}
              <span className="font-medium">{formatArabicDate(po.orderDate)}</span>
            </p>
          </div>

          {/* Line Items */}
          <div className="space-y-3">
            {po.lineItems.map((li) => (
              <div key={li.id} className="border border-gray-100 rounded-lg p-3 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="font-medium text-gray-900 text-sm">{li.itemName}</span>
                  <span className="text-xs text-gray-400">
                    المطلوب: {li.quantity} — المستلم سابقاً: {li.receivedQuantity}
                  </span>
                </div>
                <div className="flex items-center gap-3">
                  <label className="text-xs text-gray-500">الكمية المستلمة:</label>
                  <input
                    type="number"
                    min={0}
                    max={li.quantity}
                    value={receivedQtys[li.id] ?? 0}
                    onChange={(e) =>
                      setReceivedQtys((prev) => ({
                        ...prev,
                        [li.id]: Math.min(li.quantity, Math.max(0, Number(e.target.value))),
                      }))
                    }
                    className="w-24 border border-gray-200 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
                  />
                  <span className="text-xs text-gray-400">من أصل {li.quantity}</span>
                </div>
              </div>
            ))}
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 border border-gray-200 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors"
            >
              إلغاء
            </button>
            <button
              onClick={handleSubmit}
              disabled={mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الحفظ..." : "تأكيد الاستلام"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ─── View PO Detail Modal ─────────────────────────────────────────────────── */
function ViewPOModal({
  po,
  onClose,
  onReceive,
  onSubmit,
  onCancel,
}: {
  po: PurchaseOrder;
  onClose: () => void;
  onReceive: (po: PurchaseOrder) => void;
  onSubmit: (po: PurchaseOrder) => void;
  onCancel: (po: PurchaseOrder) => void;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-2xl mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b sticky top-0 bg-white z-10">
          <div className="flex items-center gap-3">
            <h2 className="text-lg font-bold text-gray-900">
              أمر الشراء — {po.orderNumber}
            </h2>
            <span
              className={cn(
                "text-xs px-2.5 py-1 rounded-full border font-medium",
                STATUS_BADGE_CLASSES[po.status]
              )}
            >
              {STATUS_LABELS[po.status]}
            </span>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            ×
          </button>
        </div>

        <div className="p-6 space-y-5">
          {/* PO Info Grid */}
          <div className="grid grid-cols-2 gap-4 text-sm">
            <div className="bg-gray-50 rounded-lg p-3">
              <p className="text-gray-500 text-xs">المورد</p>
              <p className="font-medium text-gray-900">{po.supplierName ?? "—"}</p>
            </div>
            <div className="bg-gray-50 rounded-lg p-3">
              <p className="text-gray-500 text-xs">تاريخ الطلب</p>
              <p className="font-medium text-gray-900">{formatArabicDate(po.orderDate)}</p>
            </div>
            {po.expectedDate && (
              <div className="bg-gray-50 rounded-lg p-3">
                <p className="text-gray-500 text-xs">تاريخ الاستلام المتوقع</p>
                <p className="font-medium text-gray-900">{formatArabicDate(po.expectedDate)}</p>
              </div>
            )}
            {po.receivedDate && (
              <div className="bg-gray-50 rounded-lg p-3">
                <p className="text-gray-500 text-xs">تاريخ الاستلام الفعلي</p>
                <p className="font-medium text-gray-900">{formatArabicDate(po.receivedDate)}</p>
              </div>
            )}
            {po.notes && (
              <div className="bg-gray-50 rounded-lg p-3 col-span-2">
                <p className="text-gray-500 text-xs">ملاحظات</p>
                <p className="font-medium text-gray-900">{po.notes}</p>
              </div>
            )}
          </div>

          {/* Line Items Table */}
          <div className="border border-gray-100 rounded-xl overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["العنصر", "الكمية", "المستلمة", "تكلفة الوحدة", "الإجمالي"].map((h) => (
                    <th key={h} className="text-right px-4 py-3 font-medium text-gray-500 text-xs">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {po.lineItems.map((li) => (
                  <tr key={li.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900">{li.itemName}</td>
                    <td className="px-4 py-3 text-gray-700">{li.quantity}</td>
                    <td className="px-4 py-3">
                      <span className={cn(
                        "font-medium",
                        li.receivedQuantity >= li.quantity
                          ? "text-green-600"
                          : li.receivedQuantity > 0
                            ? "text-orange-600"
                            : "text-gray-400"
                      )}>
                        {li.receivedQuantity}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-600 font-mono text-xs">
                      {formatYemeniRiyal(li.unitCost)}
                    </td>
                    <td className="px-4 py-3 font-mono text-gray-800">
                      {formatYemeniRiyal(li.totalPrice)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Totals */}
          <div className="bg-gray-50 rounded-xl p-4 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">المجموع الفرعي</span>
              <span className="font-mono">{formatYemeniRiyal(po.subtotal)}</span>
            </div>
            {po.taxAmount > 0 && (
              <div className="flex justify-between">
                <span className="text-gray-500">الضريبة</span>
                <span className="font-mono">{formatYemeniRiyal(po.taxAmount)}</span>
              </div>
            )}
            <div className="flex justify-between border-t border-gray-200 pt-2">
              <span className="font-bold text-gray-900">الإجمالي</span>
              <span className="font-bold font-mono text-gray-900">
                {formatYemeniRiyal(po.totalAmount)}
              </span>
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-3">
            {po.status === "Draft" && (
              <button
                onClick={() => onSubmit(po)}
                className="flex-1 flex items-center justify-center gap-2 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors"
              >
                <FileText className="w-4 h-4" />
                إرسال الأمر
              </button>
            )}
            {(po.status === "Submitted" || po.status === "PartiallyReceived") && (
              <button
                onClick={() => onReceive(po)}
                className="flex-1 flex items-center justify-center gap-2 bg-green-600 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-green-700 transition-colors"
              >
                <PackageOpen className="w-4 h-4" />
                استلام
              </button>
            )}
            {(po.status === "Draft" || po.status === "Submitted") && (
              <button
                onClick={() => onCancel(po)}
                className="flex items-center justify-center gap-2 border border-red-200 text-red-600 px-4 py-2.5 rounded-lg text-sm font-medium hover:bg-red-50 transition-colors"
              >
                إلغاء الأمر
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="flex items-center justify-center border border-gray-200 text-gray-600 px-4 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors"
            >
              إغلاق
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ─── Main Purchase Orders Page ────────────────────────────────────────────── */
export default function PurchaseOrdersPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<PurchaseOrderStatus | "All">("All");
  const [showCreate, setShowCreate] = useState(false);
  const [viewPO, setViewPO] = useState<PurchaseOrder | null>(null);
  const [receivePO, setReceivePO] = useState<PurchaseOrder | null>(null);
  const [cancelPO, setCancelPO] = useState<PurchaseOrder | null>(null);

  const queryClient = useQueryClient();

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ["purchase-orders"],
    queryFn: async () => {
      const res = await api.get<PurchaseOrder[]>("/api/purchase-orders");
      return res.data;
    },
  });

  const submitMutation = useMutation({
    mutationFn: async (id: string) => api.patch(`/api/purchase-orders/${id}/submit`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
      toast.success("تم إرسال أمر الشراء");
      setViewPO(null);
    },
    onError: () => toast.error("فشل إرسال أمر الشراء"),
  });

  const cancelMutation = useMutation({
    mutationFn: async (id: string) => api.patch(`/api/purchase-orders/${id}/cancel`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
      toast.success("تم إلغاء أمر الشراء");
      setCancelPO(null);
      setViewPO(null);
    },
    onError: () => toast.error("فشل إلغاء أمر الشراء"),
  });

  const orders = (data ?? []).filter((po) => {
    const matchesSearch =
      search.trim() === "" ||
      po.orderNumber.toLowerCase().includes(search.toLowerCase()) ||
      (po.supplierName ?? "").toLowerCase().includes(search.toLowerCase());
    const matchesStatus = statusFilter === "All" || po.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const handleSubmitPO = (po: PurchaseOrder) => {
    submitMutation.mutate(po.id);
  };

  const handleCancelPO = (po: PurchaseOrder) => {
    setCancelPO(po);
  };

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Navigation Tabs */}
        <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit">
          <Link
            href="/inventory"
            className="px-4 py-2 text-sm font-medium rounded-md text-gray-500 hover:text-gray-700 transition-colors"
          >
            المخزون
          </Link>
          <Link
            href="/inventory/suppliers"
            className="px-4 py-2 text-sm font-medium rounded-md text-gray-500 hover:text-gray-700 transition-colors"
          >
            الموردين
          </Link>
          <span className="px-4 py-2 text-sm font-medium rounded-md bg-white shadow-sm text-gray-900">
            أوامر الشراء
          </span>
        </div>

        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-clinic-blue-50 flex items-center justify-center">
              <ShoppingCart className="w-5 h-5 text-clinic-blue" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-gray-900">أوامر الشراء</h1>
              <p className="text-sm text-gray-500">
                إدارة أوامر الشراء والاستلام
              </p>
            </div>
          </div>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-2 bg-cyan-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors"
          >
            <Plus className="w-4 h-4" />
            أمر شراء جديد
          </button>
        </div>

        {/* Filters */}
        <div className="flex flex-wrap gap-3 items-center">
          <div className="relative flex-1 min-w-52">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="بحث برقم الأمر أو المورد..."
              className="w-full border border-gray-200 rounded-lg pr-9 pl-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
            />
          </div>
        </div>

        {/* Status Tabs */}
        <div className="flex gap-1 overflow-x-auto pb-1">
          {STATUS_TABS.map((tab) => (
            <button
              key={tab.value}
              onClick={() => setStatusFilter(tab.value)}
              className={cn(
                "px-3 py-1.5 text-xs font-medium rounded-full border transition-colors whitespace-nowrap",
                statusFilter === tab.value
                  ? "bg-clinic-blue text-white border-clinic-blue"
                  : "bg-white text-gray-600 border-gray-200 hover:border-gray-300"
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Error State */}
        {error && (
          <div className="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
            <p className="text-red-700 font-medium">فشل تحميل أوامر الشراء</p>
            <button
              onClick={() => refetch()}
              className="mt-2 text-sm text-red-600 hover:text-red-800 underline"
            >
              إعادة المحاولة
            </button>
          </div>
        )}

        {/* Table */}
        {!error && (
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
            {isLoading ? (
              <div className="p-6">
                <TableSkeleton rows={6} cols={6} />
              </div>
            ) : orders.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-gray-400">
                <ShoppingCart className="w-10 h-10 mb-3" />
                <p className="font-medium">لا توجد أوامر شراء</p>
                <p className="text-sm mt-1">أنشئ أمر شراء جديد للبدء</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-100">
                    <tr>
                      {[
                        "رقم الأمر",
                        "المورد",
                        "التاريخ",
                        "المبلغ",
                        "الحالة",
                        "إجراءات",
                      ].map((h) => (
                        <th
                          key={h}
                          className="text-right px-4 py-3 font-medium text-gray-500 text-xs"
                        >
                          {h}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {orders.map((po) => (
                      <tr
                        key={po.id}
                        className="hover:bg-gray-50 transition-colors"
                      >
                        <td className="px-4 py-3">
                          <span className="font-mono font-semibold text-gray-900">
                            {po.orderNumber}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <Truck className="w-3.5 h-3.5 text-gray-400 flex-shrink-0" />
                            <span className="text-gray-900">{po.supplierName ?? "—"}</span>
                          </div>
                        </td>
                        <td className="px-4 py-3 text-gray-600">
                          {formatArabicDate(po.orderDate)}
                        </td>
                        <td className="px-4 py-3 font-mono font-semibold">
                          {formatYemeniRiyal(po.totalAmount)}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={cn(
                              "text-xs px-2.5 py-1 rounded-full border font-medium",
                              STATUS_BADGE_CLASSES[po.status]
                            )}
                          >
                            {STATUS_LABELS[po.status]}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2 justify-end">
                            <button
                              onClick={() => setViewPO(po)}
                              className="text-xs text-clinic-blue hover:text-clinic-blue-600 font-medium flex items-center gap-0.5"
                            >
                              <Eye className="w-3 h-3" />
                              عرض
                            </button>
                            {(po.status === "Submitted" || po.status === "PartiallyReceived") && (
                              <button
                                onClick={() => setReceivePO(po)}
                                className="text-xs text-green-600 hover:text-green-700 font-medium flex items-center gap-0.5"
                              >
                                <PackageOpen className="w-3 h-3" />
                                استلام
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Summary */}
        {!error && !isLoading && orders.length > 0 && (
          <div className="text-sm text-gray-500 text-center">
            إجمالي الأوامر: {orders.length}
          </div>
        )}
      </div>

      {/* Create PO Modal */}
      {showCreate && <CreatePOModal onClose={() => setShowCreate(false)} />}

      {/* View PO Detail Modal */}
      {viewPO && (
        <ViewPOModal
          po={viewPO}
          onClose={() => setViewPO(null)}
          onReceive={(po) => {
            setViewPO(null);
            setReceivePO(po);
          }}
          onSubmit={handleSubmitPO}
          onCancel={handleCancelPO}
        />
      )}

      {/* Receive PO Modal */}
      {receivePO && (
        <ReceivePOModal
          po={receivePO}
          onClose={() => setReceivePO(null)}
        />
      )}

      {/* Cancel PO Confirmation */}
      <ConfirmDialog
        open={!!cancelPO}
        title="إلغاء أمر الشراء"
        message={`هل أنت متأكد من إلغاء أمر الشراء "${cancelPO?.orderNumber}"؟`}
        confirmLabel="إلغاء الأمر"
        variant="danger"
        onConfirm={() => cancelPO && cancelMutation.mutate(cancelPO.id)}
        onCancel={() => setCancelPO(null)}
      />
    </ErrorBoundary>
  );
}
