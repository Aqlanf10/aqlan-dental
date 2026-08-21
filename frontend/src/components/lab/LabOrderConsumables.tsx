"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Boxes, Loader2, Plus, Trash2, X } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import { isAdminRole } from "@/lib/roles";

/**
 * LABINV-REQ-011 — the materials a case actually consumed, shown beside its lab order.
 *
 * The problem this addresses is the third of the clinic's standing ones: the materials for a
 * crown or a retainer leave the shelf and nothing records it against the case. Stock drifts
 * away from reality until someone reaches for a box that is empty on the day it is needed.
 *
 * Two constraints shape this component:
 *
 * 1. **Cost is displayed, never submitted.** What the clinic consumes is its own cost, not
 *    part of what it owes the lab. The server refuses to fold it into `LabOrder.TotalCost`,
 *    and nothing here sends a cost — the numbers below are read back from the server, which
 *    prices them from the current inventory record.
 *
 * 2. **Recording is admin-only, reading is not.** Moving stock goes through the inventory
 *    owner API, which is `AdminOnly`. Everyone who can see the order can see what it
 *    consumed; only an administrator can record more. Non-admins are simply not shown the
 *    form rather than being shown one that fails on submit.
 */

interface ConsumedLine {
  id: string;
  inventoryItemId: string;
  itemName: string;
  unit: string | null;
  consumedQuantity: number;
  costPerUnit: number | null;
  reason: string | null;
  createdAt: string;
}

interface ConsumablesResponse {
  labOrderId: string;
  orderNumber: string | null;
  lines: ConsumedLine[];
  materialCost: number;
  currency: string;
  unpricedLineCount: number;
}

interface InventoryOption {
  id: string;
  name: string;
  quantity: number;
  unit: string | null;
  costPerUnit: number | null;
}

interface DraftLine {
  key: string;
  inventoryItemId: string;
  quantity: string;
}

interface Props {
  orderId: string;
  orderNumber?: string | null;
  onClose: () => void;
}

const money = (value: number) => value.toLocaleString("en-US", { maximumFractionDigits: 2 });

function newDraft(): DraftLine {
  return {
    // crypto.randomUUID is unavailable on http:// origins in some browsers, and this key
    // only has to be unique within one open dialog.
    key: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
    inventoryItemId: "",
    quantity: "1",
  };
}

export function LabOrderConsumables({ orderId, orderNumber, onClose }: Props) {
  const { user } = useAuthStore();
  const isAdmin = isAdminRole(user?.role);
  const queryClient = useQueryClient();

  const [drafts, setDrafts] = useState<DraftLine[]>([]);
  const [notes, setNotes] = useState("");

  const consumables = useQuery({
    queryKey: ["lab-order-consumables", orderId],
    queryFn: async () => {
      const { data } = await api.get<ConsumablesResponse>(`/api/lab-orders/${orderId}/consumables`);
      return data;
    },
  });

  // Only an administrator can post a consumption, so only an administrator needs the item
  // list. Fetching it for everyone would send a 403 to every other role on open.
  const inventory = useQuery({
    queryKey: ["inventory-options"],
    enabled: isAdmin,
    queryFn: async () => {
      const { data } = await api.get<{ data: InventoryOption[] }>("/api/inventory?pageSize=200");
      return data.data ?? [];
    },
  });

  const itemsById = useMemo(() => {
    const map = new Map<string, InventoryOption>();
    for (const item of inventory.data ?? []) map.set(item.id, item);
    return map;
  }, [inventory.data]);

  const record = useMutation({
    mutationFn: async () => {
      const items = drafts
        .filter((d) => d.inventoryItemId && Number(d.quantity) > 0)
        .map((d) => ({ inventoryItemId: d.inventoryItemId, quantity: Number(d.quantity) }));

      await api.post("/api/inventory/consume-lab-order", {
        labOrderId: orderId,
        items,
        notes: notes.trim() || undefined,
      });
    },
    onSuccess: async () => {
      toast.success("تم صرف المواد من المخزون");
      setDrafts([]);
      setNotes("");
      await queryClient.invalidateQueries({ queryKey: ["lab-order-consumables", orderId] });
      await queryClient.invalidateQueries({ queryKey: ["inventory-options"] });
    },
    onError: (err) => toast.error(extractErrorMessage(err)),
  });

  const filled = drafts.filter((d) => d.inventoryItemId && Number(d.quantity) > 0);

  // Checked here as well as on the server: the server refuses a duplicated item rather than
  // merging it, and saying so before submitting is friendlier than an error afterwards.
  const duplicated = useMemo(() => {
    const seen = new Set<string>();
    return filled.some((d) => {
      if (seen.has(d.inventoryItemId)) return true;
      seen.add(d.inventoryItemId);
      return false;
    });
  }, [filled]);

  const overdrawn = filled.filter((d) => {
    const item = itemsById.get(d.inventoryItemId);
    return item ? Number(d.quantity) > item.quantity : false;
  });

  const draftCost = filled.reduce((sum, d) => {
    const item = itemsById.get(d.inventoryItemId);
    return sum + (item?.costPerUnit ?? 0) * Number(d.quantity);
  }, 0);

  const canSubmit = filled.length > 0 && !duplicated && overdrawn.length === 0 && !record.isPending;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="flex max-h-[90vh] w-full max-w-2xl flex-col rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4">
          <div className="flex items-center gap-2">
            <Boxes className="h-5 w-5 text-cyan-700" aria-hidden />
            <h2 className="text-base font-bold text-gray-900">
              مواد أمر المختبر{orderNumber ? ` — ${orderNumber}` : ""}
            </h2>
          </div>
          <button type="button" onClick={onClose} aria-label="إغلاق" className="text-gray-400 hover:text-gray-700">
            <X className="h-5 w-5" aria-hidden />
          </button>
        </div>

        <div className="flex-1 space-y-5 overflow-y-auto px-5 py-4">
          <section>
            <h3 className="mb-2 text-sm font-semibold text-gray-800">المصروف حتى الآن</h3>

            {consumables.isLoading && (
              <p className="flex items-center gap-2 text-xs text-gray-500">
                <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> جارٍ التحميل…
              </p>
            )}

            {consumables.isError && (
              <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-800">
                {extractErrorMessage(consumables.error)}
              </p>
            )}

            {consumables.data && consumables.data.lines.length === 0 && (
              <p className="rounded-lg bg-gray-50 px-3 py-2 text-xs text-gray-600">
                لم تُصرف أي مواد لهذا الأمر بعد.
              </p>
            )}

            {consumables.data && consumables.data.lines.length > 0 && (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[32rem] text-right text-xs">
                  <thead className="bg-gray-50 text-gray-600">
                    <tr>
                      <th className="px-3 py-2 font-medium">المادة</th>
                      <th className="px-3 py-2 font-medium">الكمية</th>
                      <th className="px-3 py-2 font-medium">تكلفة الوحدة</th>
                      <th className="px-3 py-2 font-medium">الإجمالي</th>
                      <th className="px-3 py-2 font-medium">التاريخ</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {consumables.data.lines.map((line) => (
                      <tr key={line.id}>
                        <td className="px-3 py-2 text-gray-900">{line.itemName}</td>
                        <td className="px-3 py-2 text-gray-700">
                          {line.consumedQuantity}
                          {line.unit ? ` ${line.unit}` : ""}
                        </td>
                        <td className="px-3 py-2 text-gray-700">
                          {line.costPerUnit === null ? "—" : money(line.costPerUnit)}
                        </td>
                        <td className="px-3 py-2 text-gray-700">
                          {line.costPerUnit === null ? "—" : money(line.costPerUnit * line.consumedQuantity)}
                        </td>
                        <td className="px-3 py-2 text-gray-500" dir="ltr">{line.createdAt}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {consumables.data && consumables.data.lines.length > 0 && (
              <div className="mt-3 rounded-lg border border-cyan-100 bg-cyan-50 px-3 py-2">
                <p className="text-xs font-semibold text-cyan-900">
                  تكلفة المواد على المركز: {money(consumables.data.materialCost)} {consumables.data.currency}
                </p>
                {/* Stated, not hidden: this is a clinic-side cost that deliberately stays out
                    of the order total, and it is priced at today's rates. */}
                <p className="mt-1 text-[11px] leading-relaxed text-cyan-800">
                  هذه تكلفة المركز وليست جزءًا من مستحقات المعمل — لا تُضاف إلى تكلفة الأمر ولا
                  تدخل في خصم تكلفة المختبر من عمولة الطبيب. تُحتسب بسعر المادة الحالي.
                </p>
                {consumables.data.unpricedLineCount > 0 && (
                  <p className="mt-1 text-[11px] font-medium text-amber-800">
                    {consumables.data.unpricedLineCount} مادة بلا سعر وحدة — الإجمالي أعلاه ناقص.
                  </p>
                )}
              </div>
            )}
          </section>

          {isAdmin && (
            <section className="border-t border-gray-100 pt-4">
              <h3 className="mb-2 text-sm font-semibold text-gray-800">صرف مواد جديدة</h3>

              <div className="space-y-2">
                {drafts.map((draft, index) => {
                  const item = itemsById.get(draft.inventoryItemId);
                  return (
                    <div key={draft.key} className="flex items-start gap-2">
                      <div className="flex-1">
                        <label className="sr-only" htmlFor={`consumable-item-${draft.key}`}>
                          المادة {index + 1}
                        </label>
                        <select
                          id={`consumable-item-${draft.key}`}
                          value={draft.inventoryItemId}
                          onChange={(e) =>
                            setDrafts((prev) =>
                              prev.map((d) =>
                                d.key === draft.key ? { ...d, inventoryItemId: e.target.value } : d,
                              ),
                            )
                          }
                          className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-cyan-500 focus:outline-none"
                        >
                          <option value="">اختر المادة…</option>
                          {(inventory.data ?? []).map((option) => (
                            <option key={option.id} value={option.id}>
                              {option.name} — المتاح {option.quantity}
                              {option.unit ? ` ${option.unit}` : ""}
                            </option>
                          ))}
                        </select>
                      </div>

                      <div className="w-24">
                        <label className="sr-only" htmlFor={`consumable-qty-${draft.key}`}>
                          الكمية {index + 1}
                        </label>
                        <input
                          id={`consumable-qty-${draft.key}`}
                          type="number"
                          min={1}
                          value={draft.quantity}
                          onChange={(e) =>
                            setDrafts((prev) =>
                              prev.map((d) => (d.key === draft.key ? { ...d, quantity: e.target.value } : d)),
                            )
                          }
                          className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-cyan-500 focus:outline-none"
                        />
                        {item && Number(draft.quantity) > item.quantity && (
                          <p className="mt-1 text-[11px] text-red-700">المتاح {item.quantity} فقط</p>
                        )}
                      </div>

                      <button
                        type="button"
                        onClick={() => setDrafts((prev) => prev.filter((d) => d.key !== draft.key))}
                        aria-label={`حذف السطر ${index + 1}`}
                        className="mt-2 text-gray-400 hover:text-red-600"
                      >
                        <Trash2 className="h-4 w-4" aria-hidden />
                      </button>
                    </div>
                  );
                })}
              </div>

              <button
                type="button"
                onClick={() => setDrafts((prev) => [...prev, newDraft()])}
                className="mt-2 flex items-center gap-1 text-xs font-semibold text-cyan-700 hover:text-cyan-900"
              >
                <Plus className="h-4 w-4" aria-hidden /> إضافة مادة
              </button>

              {drafts.length > 0 && (
                <>
                  <div className="mt-3">
                    <label htmlFor="consumable-notes" className="mb-1 block text-xs font-medium text-gray-700">
                      ملاحظة (اختياري)
                    </label>
                    <input
                      id="consumable-notes"
                      type="text"
                      value={notes}
                      maxLength={300}
                      onChange={(e) => setNotes(e.target.value)}
                      className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-cyan-500 focus:outline-none"
                    />
                  </div>

                  {duplicated && (
                    <p role="alert" className="mt-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                      المادة مكرّرة في أكثر من سطر — ادمج الكمية في سطر واحد.
                    </p>
                  )}

                  {filled.length > 0 && (
                    <p className="mt-2 text-xs text-gray-600">
                      تكلفة تقديرية: {money(draftCost)} YER
                    </p>
                  )}
                </>
              )}
            </section>
          )}
        </div>

        <div className="flex items-center justify-end gap-2 border-t border-gray-100 px-5 py-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50"
          >
            إغلاق
          </button>
          {isAdmin && (
            <button
              type="button"
              onClick={() => record.mutate()}
              disabled={!canSubmit}
              className="flex items-center gap-2 rounded-lg bg-cyan-700 px-4 py-2 text-sm font-semibold text-white hover:bg-cyan-800 disabled:opacity-50"
            >
              {record.isPending && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
              صرف من المخزون
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
