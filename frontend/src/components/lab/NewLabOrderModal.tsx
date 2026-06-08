"use client";

import { useMemo, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, X } from "lucide-react";
import api from "@/lib/api";
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
      const apiError = error as { response?: { data?: { message?: string } } };
      toast.error(apiError.response?.data?.message ?? "فشل إنشاء طلب المعمل");
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

        <form onSubmit={handleSubmit} className="p-6 space-y-5">
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
                <input className="md:col-span-2 border border-gray-200 rounded-lg px-3 py-2 text-sm" placeholder="الأسنان" value={item.toothNumber ?? ""} onChange={(e) => updateItem(item.key, { toothNumber: e.target.value })} />
                <input className="md:col-span-2 border border-gray-200 rounded-lg px-3 py-2 text-sm" placeholder="الظل" value={item.shade ?? ""} onChange={(e) => updateItem(item.key, { shade: e.target.value })} />
                <input className="md:col-span-1 border border-gray-200 rounded-lg px-3 py-2 text-sm" type="number" min="1" value={item.unitsCount ?? 1} onChange={(e) => updateItem(item.key, { unitsCount: Number(e.target.value) })} />
                <input className="md:col-span-2 border border-gray-200 rounded-lg px-3 py-2 text-sm" type="number" min="0" step="0.01" placeholder="سعر الوحدة" value={item.unitPrice ?? ""} onChange={(e) => updateItem(item.key, { unitPrice: e.target.value === "" ? undefined : Number(e.target.value) })} />
                <div className="md:col-span-1 flex items-center text-sm font-bold text-gray-700">{Number(item.totalPrice ?? 0).toLocaleString()}</div>
                <button type="button" disabled={items.length === 1} onClick={() => setItems((current) => current.filter((x) => x.key !== item.key))} className="md:col-span-1 text-red-500 disabled:text-gray-300 flex items-center justify-center">
                  <Trash2 className="w-4 h-4" />
                </button>
                <input className="md:col-span-12 border border-gray-200 rounded-lg px-3 py-2 text-sm" placeholder={`تعليمات البند ${index + 1}`} value={item.instructions ?? ""} onChange={(e) => updateItem(item.key, { instructions: e.target.value })} />
              </div>
            ))}

            <div className="flex items-center justify-between rounded-lg bg-cyan-50 px-4 py-3">
              <span className="text-sm font-medium text-cyan-900">إجمالي تكلفة المعمل</span>
              <span className="text-lg font-black text-cyan-800">{totalCost.toLocaleString()}</span>
            </div>
          </div>

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

          <div className="grid grid-cols-2 gap-3">
            <input type="date" value={form.sentDate ?? ""} onChange={(e) => set("sentDate", e.target.value)} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500" />
            <input type="date" value={form.expectedDate ?? ""} onChange={(e) => set("expectedDate", e.target.value)} className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500" />
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
              disabled={!selectedPatientId || (!form.applianceType && !items.some((i) => i.workTypeId)) || mutation.isPending}
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
