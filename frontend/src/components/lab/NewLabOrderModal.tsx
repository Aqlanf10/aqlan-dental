"use client";

import { useMemo, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, X } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { toast } from "@/stores/toastStore";
import type {
  CreateLabOrderItemDto,
  CreateLabOrderRequest,
  Lab,
  LabOrderPriority,
  LabWorkPrice,
  LabWorkType,
} from "@/types/lab";
import type { PatientListItem } from "@/types/patient";
import { cn } from "@/lib/utils";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

interface Props {
  onClose: () => void;
  initialPatient?: {
    id: string;
    displayName: string;
  };
}

type DraftItem = CreateLabOrderItemDto & { key: string };

const emptyItem = (): DraftItem => ({
  key: crypto.randomUUID(),
  workTypeId: "",
  toothNumber: "",
  arch: "",
  shade: "",
  restorationType: "",
  unitsCount: 1,
  unitPrice: undefined,
  totalPrice: undefined,
  instructions: "",
});

/** Numbered section heading — makes the order of data entry explicit. */
function FormStep({ number, title }: { number: number; title: string }) {
  return (
    <div className="flex items-center gap-2 pt-1">
      <span className="grid place-items-center w-6 h-6 rounded-full bg-cyan-600 text-white text-xs font-bold shrink-0">
        {number}
      </span>
      <h3 className="text-sm font-bold text-gray-900">{title}</h3>
      <div className="flex-1 h-px bg-gray-100" />
    </div>
  );
}

export function NewLabOrderModal({ onClose, initialPatient }: Props) {
  const queryClient = useQueryClient();
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(initialPatient?.id ?? null);
  const [items, setItems] = useState<DraftItem[]>([emptyItem()]);
  const [form, setForm] = useState<Omit<CreateLabOrderRequest, "patientId" | "items">>({
    applianceType: "",
    labName: "",
    labId: "",
    sentDate: "",
    expectedDate: "",
    priority: "normal",
    instructions: "",
    shade: "",
    restorationType: "",
    cost: undefined,
    currency: "YER",
    exchangeRateToYer: undefined,
  });

  const { data: labs = [] } = useQuery({
    queryKey: ["labs", "active"],
    queryFn: async () => {
      const { data } = await api.get<{ data: Lab[] }>("/api/labs", { params: { activeOnly: true, pageSize: 100 } });
      return data.data;
    },
  });

  const { data: workTypes = [] } = useQuery({
    queryKey: ["lab-work-types", "active"],
    queryFn: async () => {
      const { data } = await api.get<{ data: LabWorkType[] }>("/api/lab-work-types", { params: { activeOnly: true } });
      return data.data;
    },
  });

  const { data: prices = [] } = useQuery({
    queryKey: ["lab-work-prices", form.labId],
    enabled: !!form.labId,
    queryFn: async () => {
      const { data } = await api.get<{ data: LabWorkPrice[] }>("/api/lab-work-prices", { params: { labId: form.labId, activeOnly: true, pageSize: 200 } });
      return data.data;
    },
  });

  const priceByWorkType = useMemo(() => new Map(prices.map((p) => [p.workTypeId, p])), [prices]);
  const totalCost = useMemo(() => items.reduce((sum, item) => sum + (Number(item.totalPrice) || 0), 0), [items]);

  const mutation = useMutation({
    mutationFn: async (data: CreateLabOrderRequest) => {
      const res = await api.post<{ id: string; orderNumber: string }>("/api/lab-orders", data);
      return res.data;
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["lab-orders"] });
      queryClient.invalidateQueries({ queryKey: ["lab-orders-pending-count"] });
      queryClient.invalidateQueries({ queryKey: ["lab-dashboard"] });
      toast.success(`تم إنشاء طلب المعمل ${data.orderNumber} بنجاح`);
      onClose();
    },
    onError: (error: unknown) => {
      toast.error(extractErrorMessage(error, "فشل إنشاء طلب المعمل"));
    },
  });

  const set = (k: keyof typeof form, v: string | number | undefined) => setForm((f) => ({ ...f, [k]: v }));

  const updateItem = (key: string, patch: Partial<DraftItem>) => {
    setItems((current) =>
      current.map((item) => {
        if (item.key !== key) return item;
        const next = { ...item, ...patch };
        const units = Math.max(1, Number(next.unitsCount) || 1);
        const unitPrice = Number(next.unitPrice) || 0;
        return { ...next, unitsCount: units, totalPrice: units * unitPrice };
      })
    );
  };

  const handleWorkTypeChange = (key: string, workTypeId: string) => {
    const price = priceByWorkType.get(workTypeId);
    const unitPrice = price?.unitPrice;
    updateItem(key, { workTypeId, unitPrice });
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!selectedPatientId) return;

    const cleanItems = items
      .filter((item) => item.workTypeId)
      .map((item, index) => ({
        workTypeId: item.workTypeId,
        toothNumber: item.toothNumber || undefined,
        arch: item.arch || undefined,
        shade: item.shade || undefined,
        restorationType: item.restorationType || undefined,
        unitsCount: Math.max(1, Number(item.unitsCount) || 1),
        unitPrice: item.unitPrice,
        totalPrice: item.totalPrice,
        instructions: item.instructions || undefined,
        sortOrder: index,
      }));

    mutation.mutate({
      ...form,
      patientId: selectedPatientId,
      labId: form.labId || undefined,
      labName: form.labId ? undefined : form.labName || undefined,
      sentDate: form.sentDate || undefined,
      expectedDate: form.expectedDate || undefined,
      shade: form.shade || undefined,
      restorationType: form.restorationType || undefined,
      cost: cleanItems.length > 0 ? totalCost : form.cost || undefined,
      applianceType: form.applianceType || workTypes.find((w) => w.id === cleanItems[0]?.workTypeId)?.nameAr || workTypes.find((w) => w.id === cleanItems[0]?.workTypeId)?.name || "",
      items: cleanItems.length > 0 ? cleanItems : undefined,
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-xl w-full max-w-4xl mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-6 border-b">
          <div>
            <h2 className="text-lg font-bold text-gray-900">طلب معمل جديد</h2>
            <p className="text-xs text-gray-500 mt-1">اختر المعمل والبنود ليتم حساب التكلفة تلقائياً.</p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-6">
          {/* CORE-LAB-018: the form is filled in a fixed order — who, then what, then when.
              Numbered sections make that order visible instead of leaving the user to infer
              it from a flat wall of inputs. */}
          <FormStep number={1} title="المريض والمعمل" />
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">المريض *</label>
              <PatientCombobox
                defaultDisplayValue={initialPatient?.displayName ?? ""}
                onSelect={(p: PatientListItem) => setSelectedPatientId(p.id)}
              />
            </div>

            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">المعمل</label>
              <select
                value={form.labId ?? ""}
                onChange={(e) => {
                  const lab = labs.find((l) => l.id === e.target.value);
                  setForm((f) => ({ ...f, labId: e.target.value, labName: lab?.name ?? "" }));
                }}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              >
                <option value="">اختر المعمل</option>
                {labs.map((lab) => <option key={lab.id} value={lab.id}>{lab.name}</option>)}
              </select>
            </div>
          </div>

          {!form.labId && (
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">اسم المعمل اليدوي للطلبات القديمة</label>
              <input
                value={form.labName ?? ""}
                onChange={(e) => set("labName", e.target.value)}
                placeholder="اسم المعمل"
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
          )}

          <FormStep number={2} title="بنود العمل والتكلفة" />
          <div className="space-y-3 rounded-xl border border-gray-100 bg-gray-50 p-4">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-gray-900">بنود العمل</h3>
              <button
                type="button"
                onClick={() => setItems((current) => [...current, emptyItem()])}
                className="inline-flex items-center gap-1 rounded-lg bg-white px-3 py-1.5 text-xs font-semibold text-cyan-700 border border-cyan-100"
              >
                <Plus className="w-3.5 h-3.5" /> إضافة بند
              </button>
            </div>

            {/* Placeholders disappear the moment a value is typed, so a filled row became six
                anonymous boxes. A header row keeps every column identifiable. */}
            <div className="hidden md:grid md:grid-cols-12 gap-3 px-3 text-xs font-semibold text-gray-500">
              <span className="md:col-span-3">نوع العمل</span>
              <span className="md:col-span-2">رقم/أرقام الأسنان</span>
              <span className="md:col-span-2">الظل (اللون)</span>
              <span className="md:col-span-1">العدد</span>
              <span className="md:col-span-2">سعر الوحدة</span>
              <span className="md:col-span-1">الإجمالي</span>
              <span className="md:col-span-1" />
            </div>

            {items.map((item, index) => (
              <div key={item.key} className="grid gap-3 rounded-lg bg-white p-3 border border-gray-100 md:grid-cols-12">
                <select
                  value={item.workTypeId}
                  onChange={(e) => handleWorkTypeChange(item.key, e.target.value)}
                  className="md:col-span-3 border border-gray-200 rounded-lg px-3 py-2 text-sm"
                >
                  <option value="">نوع العمل</option>
                  {workTypes.map((type) => <option key={type.id} value={type.id}>{type.nameAr || type.name}</option>)}
                </select>
                <input className="md:col-span-2 border border-gray-200 rounded-lg px-3 py-2 text-sm" placeholder="الأسنان" aria-label="رقم الأسنان" value={item.toothNumber ?? ""} onChange={(e) => updateItem(item.key, { toothNumber: e.target.value })} />
                <input className="md:col-span-2 border border-gray-200 rounded-lg px-3 py-2 text-sm" placeholder="الظل" aria-label="الظل (اللون)" value={item.shade ?? ""} onChange={(e) => updateItem(item.key, { shade: e.target.value })} />
                <input className="md:col-span-1 border border-gray-200 rounded-lg px-3 py-2 text-sm" type="number" min="1" aria-label="العدد" value={item.unitsCount ?? 1} onChange={(e) => updateItem(item.key, { unitsCount: Number(e.target.value) })} />
                <input className="md:col-span-2 border border-gray-200 rounded-lg px-3 py-2 text-sm" type="number" min="0" step="0.01" placeholder="سعر الوحدة" aria-label="سعر الوحدة" value={item.unitPrice ?? ""} onChange={(e) => updateItem(item.key, { unitPrice: e.target.value === "" ? undefined : Number(e.target.value) })} />
                <div className="md:col-span-1 flex items-center text-sm font-bold text-gray-700">{Number(item.totalPrice ?? 0).toLocaleString()}</div>
                <button type="button" disabled={items.length === 1} onClick={() => setItems((current) => current.filter((x) => x.key !== item.key))} className="md:col-span-1 text-red-500 disabled:text-gray-300 flex items-center justify-center">
                  <Trash2 className="w-4 h-4" />
                </button>
                <input className="md:col-span-12 border border-gray-200 rounded-lg px-3 py-2 text-sm" placeholder={`تعليمات البند ${index + 1}`} value={item.instructions ?? ""} onChange={(e) => updateItem(item.key, { instructions: e.target.value })} />
              </div>
            ))}

            <div className="flex items-center justify-between rounded-lg bg-cyan-50 px-4 py-3">
              <span className="text-sm font-medium text-cyan-900">إجمالي تكلفة المعمل</span>
              <span className="text-lg font-black text-cyan-800">{totalCost.toLocaleString()} {form.currency}</span>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">عملة حساب المعمل</label>
                <select value={form.currency} onChange={(event) => { set("currency", event.target.value); set("exchangeRateToYer", undefined); }} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm bg-white">
                  <option value="YER">YER</option><option value="SAR">SAR</option><option value="USD">USD</option>
                </select>
              </div>
              {form.currency !== "YER" && (
                <div className="space-y-1.5">
                  <label className="text-sm font-medium text-gray-700">سعر الصرف الفعلي: 1 {form.currency} = كم YER؟</label>
                  <input type="number" min="0.000001" step="0.000001" value={form.exchangeRateToYer ?? ""} onChange={(event) => set("exchangeRateToYer", event.target.value === "" ? undefined : Number(event.target.value))} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm" dir="ltr" />
                </div>
              )}
            </div>
          </div>

          <FormStep number={3} title="الوصف والمواعيد" />
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">وصف مختصر للعمل</label>
              <input value={form.applianceType} onChange={(e) => set("applianceType", e.target.value)} placeholder="مثال: زركونيا، Emax، جهاز تقويم" className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500" />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">نوع الترميم العام</label>
              <input value={form.restorationType ?? ""} onChange={(e) => set("restorationType", e.target.value)} placeholder="اختياري" className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500" />
            </div>
          </div>

          {/* These were two bare date boxes with no labels — the user had to guess which was
              which, and getting them backwards is what makes an order look overdue on day one. */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">تاريخ الإرسال للمعمل</label>
              <input type="date" value={form.sentDate ?? ""} onChange={(e) => set("sentDate", e.target.value)} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500" />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">التاريخ المتوقع للاستلام</label>
              <input type="date" value={form.expectedDate ?? ""} onChange={(e) => set("expectedDate", e.target.value)} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500" />
              <p className="text-xs text-gray-400">يُحتسب التأخر مقارنة بهذا التاريخ</p>
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">الأولوية</label>
            <div className="flex gap-2">
              {(["urgent", "normal", "low"] as LabOrderPriority[]).map((p) => (
                <button
                  key={p}
                  type="button"
                  onClick={() => set("priority", p)}
                  className={cn(
                    "flex-1 py-2 rounded-lg border text-sm font-medium transition-colors",
                    form.priority === p ? "border-cyan-500 bg-cyan-50 text-cyan-700" : "border-gray-200 text-gray-500 hover:border-gray-300"
                  )}
                >
                  {p === "urgent" ? "عاجل" : p === "normal" ? "عادي" : "منخفض"}
                </button>
              ))}
            </div>
          </div>

          <textarea value={form.instructions ?? ""} onChange={(e) => set("instructions", e.target.value)} rows={3} placeholder="تعليمات إضافية للمعمل..." className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 resize-none" />

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="flex-1 border border-gray-200 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50 transition-colors">
              إلغاء
            </button>
            <button
              type="submit"
              disabled={!selectedPatientId || (!form.applianceType && !items.some((i) => i.workTypeId)) || (form.currency !== "YER" && !form.exchangeRateToYer) || mutation.isPending}
              className="flex-1 bg-cyan-700 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-cyan-800 transition-colors disabled:opacity-50"
            >
              {mutation.isPending ? "جارٍ الإنشاء..." : "إنشاء طلب المعمل"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
