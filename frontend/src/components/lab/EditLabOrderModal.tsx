"use client";
import { useEffect, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, Save, Send } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";
import type { LabOrder } from "@/types/lab";

interface LabOption {
  id: string;
  name: string;
  isActive?: boolean;
}

interface Props {
  order: LabOrder;
  open: boolean;
  onClose: () => void;
  /** Invalidated after a successful save/send so every lab surface refreshes. */
  invalidateKeys?: unknown[][];
}

const CURRENCIES = ["YER", "SAR", "USD"] as const;

/**
 * CORE-LAB-003: completing and sending a draft lab order.
 *
 * A draft could be created with no lab and no cost, but neither the main lab screen
 * nor daily-operations offered any way to fill those in — the only action shown on a
 * draft row was "إلغاء". The order was a dead end: it could not be completed, could
 * not be sent, and (because the backend only ever built the supplier bill during
 * create) would never reach the books even if a lab were attached some other way.
 */
export function EditLabOrderModal({ order, open, onClose, invalidateKeys = [] }: Props) {
  const queryClient = useQueryClient();
  const [labId, setLabId] = useState(order.labId ?? "");
  const [applianceType, setApplianceType] = useState(order.applianceType ?? "");
  const [expectedDate, setExpectedDate] = useState(order.expectedDate ?? "");
  const [priority, setPriority] = useState(order.priority ?? "normal");
  const [instructions, setInstructions] = useState(order.instructions ?? "");
  const [cost, setCost] = useState<string>(
    order.totalCost != null ? String(order.totalCost) : order.cost != null ? String(order.cost) : "",
  );
  const [currency, setCurrency] = useState<string>(order.currency ?? "YER");
  const [rate, setRate] = useState<string>(order.exchangeRateToYer ? String(order.exchangeRateToYer) : "");

  // Re-seed when a different order is opened in the same mounted modal.
  useEffect(() => {
    setLabId(order.labId ?? "");
    setApplianceType(order.applianceType ?? "");
    setExpectedDate(order.expectedDate ?? "");
    setPriority(order.priority ?? "normal");
    setInstructions(order.instructions ?? "");
    setCost(order.totalCost != null ? String(order.totalCost) : order.cost != null ? String(order.cost) : "");
    setCurrency(order.currency ?? "YER");
    setRate(order.exchangeRateToYer ? String(order.exchangeRateToYer) : "");
  }, [order]);

  const labsQuery = useQuery({
    queryKey: ["labs", "active"],
    enabled: open,
    queryFn: async () => {
      const { data } = await api.get<{ data: LabOption[] } | LabOption[]>("/api/labs", {
        params: { activeOnly: true, pageSize: 200 },
      });
      return Array.isArray(data) ? data : data.data ?? [];
    },
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
    queryClient.invalidateQueries({ queryKey: ["daily-ops", "lab-orders"] });
    for (const key of invalidateKeys) queryClient.invalidateQueries({ queryKey: key });
  };

  const buildPayload = () => {
    const payload: Record<string, unknown> = {
      applianceType: applianceType || undefined,
      labId: labId || undefined,
      expectedDate: expectedDate || undefined,
      priority: priority || undefined,
      instructions: instructions || undefined,
    };
    if (cost !== "") payload.cost = Number(cost);
    if (currency) payload.currency = currency;
    // Only meaningful for a foreign currency; YER is pinned to 1 server-side.
    if (currency !== "YER" && rate !== "") payload.exchangeRateToYer = Number(rate);
    return payload;
  };

  const saveMutation = useMutation({
    mutationFn: async () => api.put(`/api/lab-orders/${order.id}`, buildPayload()),
    onSuccess: () => {
      toast.success("تم حفظ تعديلات طلب المعمل");
      invalidateAll();
      onClose();
    },
    onError: (err) => toast.error(extractErrorMessage(err, "تعذر حفظ تعديلات طلب المعمل")),
  });

  // Save first, then send: the server rejects an incomplete send, so sending without
  // persisting the edits the user just made would fail on stale data.
  const sendMutation = useMutation({
    mutationFn: async () => {
      await api.put(`/api/lab-orders/${order.id}`, buildPayload());
      await api.put(`/api/lab-orders/${order.id}/status`, { status: "sent" });
    },
    onSuccess: () => {
      toast.success("تم إرسال طلب المعمل");
      invalidateAll();
      onClose();
    },
    onError: (err) => toast.error(extractErrorMessage(err, "تعذر إرسال طلب المعمل")),
  });

  if (!open) return null;

  const busy = saveMutation.isPending || sendMutation.isPending;
  const needsRate = currency !== "YER";
  // Mirrors the server's ValidateReadyToSend so the reason is visible before the click.
  const missingForSend = !labId
    ? "حدد المعمل أولًا"
    : !cost || Number(cost) <= 0
      ? "أدخل تكلفة أكبر من صفر"
      : needsRate && (!rate || Number(rate) <= 0)
        ? "أدخل سعر الصرف إلى الريال اليمني"
        : !applianceType
          ? "حدد نوع العمل"
          : null;

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div
        className="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <form
          onSubmit={(e: FormEvent) => {
            e.preventDefault();
            saveMutation.mutate();
          }}
          className="p-6 space-y-4"
        >
          <div>
            <h3 className="font-extrabold text-[15px] text-gray-800">إكمال طلب المعمل {order.orderNumber}</h3>
            <p className="text-xs text-gray-500 mt-1">
              أكمل بيانات المعمل والتكلفة ليتم تسجيل المستحق على المورد وربطه بالمصروفات والعمولة.
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">المعمل</label>
            {labsQuery.isError ? (
              <div className="flex items-center justify-between gap-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2">
                <span className="text-xs text-amber-800">تعذر تحميل قائمة المعامل</span>
                <button
                  type="button"
                  onClick={() => labsQuery.refetch()}
                  className="shrink-0 rounded-md border border-amber-400 px-2 py-1 text-xs font-medium text-amber-900 hover:bg-amber-100"
                >
                  إعادة المحاولة
                </button>
              </div>
            ) : (
              <select
                value={labId}
                onChange={(e) => setLabId(e.target.value)}
                disabled={labsQuery.isLoading}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white"
              >
                <option value="">{labsQuery.isLoading ? "جارٍ التحميل…" : "— اختر المعمل —"}</option>
                {(labsQuery.data ?? []).map((lab) => (
                  <option key={lab.id} value={lab.id}>{lab.name}</option>
                ))}
              </select>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">نوع العمل</label>
            <input
              value={applianceType}
              onChange={(e) => setApplianceType(e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">التكلفة</label>
              <input
                type="number"
                min="0"
                step="0.01"
                value={cost}
                onChange={(e) => setCost(e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">العملة</label>
              <select
                value={currency}
                onChange={(e) => { setCurrency(e.target.value); if (e.target.value === "YER") setRate(""); }}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white"
              >
                {CURRENCIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
          </div>

          {needsRate && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                سعر الصرف الفعلي: 1 {currency} = كم YER؟
              </label>
              <input
                type="number"
                min="0.000001"
                step="0.000001"
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
                dir="ltr"
              />
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">تاريخ الاستلام المتوقع</label>
              <input
                type="date"
                value={expectedDate}
                onChange={(e) => setExpectedDate(e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">الأولوية</label>
              <select
                value={priority}
                onChange={(e) => setPriority(e.target.value as LabOrder["priority"])}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white"
              >
                <option value="normal">عادي</option>
                <option value="urgent">مستعجل</option>
                <option value="low">منخفض</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">تعليمات</label>
            <textarea
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
              rows={2}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm"
            />
          </div>

          {missingForSend && (
            <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
              لإرسال الطلب: {missingForSend}
            </p>
          )}

          <div className="flex items-center justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={onClose}
              disabled={busy}
              className="px-4 py-2 rounded-lg text-sm border border-gray-200 text-gray-600 disabled:opacity-50"
            >
              إغلاق
            </button>
            <button
              type="submit"
              disabled={busy}
              className="px-4 py-2 rounded-lg text-sm font-medium border border-gray-300 text-gray-700 inline-flex items-center gap-1.5 disabled:opacity-50"
            >
              {saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              حفظ
            </button>
            <button
              type="button"
              onClick={() => sendMutation.mutate()}
              disabled={busy || !!missingForSend}
              title={missingForSend ?? "إرسال الطلب إلى المعمل"}
              className="px-4 py-2 rounded-lg text-sm font-medium text-white inline-flex items-center gap-1.5 disabled:opacity-50"
              style={{ background: "#0E7490" }}
            >
              {sendMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              إرسال
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
