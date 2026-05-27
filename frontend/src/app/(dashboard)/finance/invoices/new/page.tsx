"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Plus, Trash2, ArrowRight, ChevronLeft } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import { useDoctors } from "@/hooks/useDoctors";
import type { PatientListItem } from "@/types/patient";
import { toast } from "@/stores/toastStore";

interface LineItemForm {
  serviceId?: string;
  serviceNameSnapshot: string;
  description: string;
  quantity: number;
  unitPrice: number;
  doctorId: string;
}

const EMPTY_LINE_ITEM: LineItemForm = {
  serviceNameSnapshot: "",
  description: "",
  quantity: 1,
  unitPrice: 0,
  doctorId: "",
};

export default function NewInvoicePage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [patientId, setPatientId] = useState("");
  const [patientName, setPatientName] = useState("");
  const [notes, setNotes] = useState("");
  const [discountAmount, setDiscountAmount] = useState(0);
  const [lineItems, setLineItems] = useState<LineItemForm[]>([{ ...EMPTY_LINE_ITEM }]);

  const { data: doctors = [] } = useDoctors();

  const subtotal = lineItems.reduce((sum, li) => sum + li.quantity * li.unitPrice, 0);
  const totalAmount = subtotal - discountAmount;

  const addLineItem = () => {
    setLineItems([...lineItems, { ...EMPTY_LINE_ITEM }]);
  };

  const removeLineItem = (index: number) => {
    if (lineItems.length <= 1) return;
    setLineItems(lineItems.filter((_, i) => i !== index));
  };

  const updateLineItem = (index: number, field: keyof LineItemForm, value: string | number) => {
    const updated = [...lineItems];
    updated[index] = { ...updated[index], [field]: value };
    setLineItems(updated);
  };

  const handleSubmit = async () => {
    if (!patientId) {
      setError("اختر المريض أولاً");
      return;
    }
    if (lineItems.some((li) => !li.serviceNameSnapshot.trim())) {
      setError("أدخل اسم الخدمة لكل بند");
      return;
    }
    if (lineItems.some((li) => li.unitPrice <= 0)) {
      setError("سعر الوحدة يجب أن يكون أكبر من صفر");
      return;
    }

    setSaving(true);
    setError("");
    try {
      const payload = {
        patientId,
        discountAmount: discountAmount > 0 ? discountAmount : undefined,
        notes: notes || undefined,
        lineItems: lineItems.map((li) => ({
          serviceNameSnapshot: li.serviceNameSnapshot,
          description: li.description || li.serviceNameSnapshot,
          quantity: li.quantity,
          unitPrice: li.unitPrice,
          doctorId: li.doctorId || undefined,
        })),
      };
      const { data: invoice } = await api.post("/api/invoices", payload);
      toast.success("تم إنشاء الفاتورة بنجاح");
      router.push(`/finance/invoices/${invoice.id}`);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "فشل إنشاء الفاتورة");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-5 max-w-4xl" dir="rtl">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/finance/invoices" className="hover:text-clinic-blue transition">الفواتير</Link>
        <ChevronLeft className="w-3.5 h-3.5" />
        <span className="text-gray-900 font-medium">فاتورة جديدة</span>
      </nav>

      <h1 className="text-2xl font-extrabold text-gray-900">إنشاء فاتورة مسودة</h1>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-3 text-sm">{error}</div>
      )}

      {/* Patient Selection */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <label className="block text-sm font-medium text-gray-700 mb-1.5">المريض <span className="text-red-500">*</span></label>
        <PatientCombobox
          defaultDisplayValue={patientName}
          onSelect={(p: PatientListItem) => {
            setPatientId(p.id);
            setPatientName(p.fullName);
          }}
        />
      </div>

      {/* Line Items */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-bold text-gray-900">بنود الفاتورة</h2>
          <button
            type="button"
            onClick={addLineItem}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
          >
            <Plus className="w-3.5 h-3.5" />
            إضافة بند
          </button>
        </div>

        {lineItems.map((li, i) => (
          <div key={i} className="grid grid-cols-12 gap-3 items-end border-b border-gray-100 pb-4">
            {/* Service Name */}
            <div className="col-span-4">
              <label className="text-xs text-gray-500 block mb-1">اسم الخدمة *</label>
              <input
                type="text"
                value={li.serviceNameSnapshot}
                onChange={(e) => updateLineItem(i, "serviceNameSnapshot", e.target.value)}
                placeholder="مثل: تنظيف أسنان"
                className="w-full text-sm border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-blue"
              />
            </div>
            {/* Description */}
            <div className="col-span-3">
              <label className="text-xs text-gray-500 block mb-1">الوصف</label>
              <input
                type="text"
                value={li.description}
                onChange={(e) => updateLineItem(i, "description", e.target.value)}
                placeholder="وصف تفصيلي"
                className="w-full text-sm border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-blue"
              />
            </div>
            {/* Quantity */}
            <div className="col-span-1">
              <label className="text-xs text-gray-500 block mb-1">الكمية</label>
              <input
                type="number"
                min={1}
                value={li.quantity}
                onChange={(e) => updateLineItem(i, "quantity", parseInt(e.target.value) || 1)}
                className="w-full text-sm border border-gray-300 rounded-lg px-2 py-2 text-center focus:outline-none focus:ring-2 focus:ring-clinic-blue"
              />
            </div>
            {/* Unit Price */}
            <div className="col-span-2">
              <label className="text-xs text-gray-500 block mb-1">سعر الوحدة</label>
              <input
                type="number"
                min={0}
                step="0.01"
                value={li.unitPrice || ""}
                onChange={(e) => updateLineItem(i, "unitPrice", parseFloat(e.target.value) || 0)}
                className="w-full text-sm border border-gray-300 rounded-lg px-2 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-blue"
                dir="ltr"
              />
            </div>
            {/* Doctor */}
            <div className="col-span-1">
              <label className="text-xs text-gray-500 block mb-1">الطبيب</label>
              <select
                value={li.doctorId}
                onChange={(e) => updateLineItem(i, "doctorId", e.target.value)}
                className="w-full text-sm border border-gray-300 rounded-lg px-1 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-blue bg-white"
              >
                <option value="">—</option>
                {doctors.filter((d) => d.isActive).map((d) => (
                  <option key={d.id} value={d.id}>{d.name}</option>
                ))}
              </select>
            </div>
            {/* Remove */}
            <div className="col-span-1 flex justify-center">
              {lineItems.length > 1 && (
                <button
                  onClick={() => removeLineItem(i)}
                  className="p-2 text-red-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              )}
            </div>
          </div>
        ))}

        {/* Totals */}
        <div className="border-t border-gray-200 pt-4 space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-gray-500">المجموع الفرعي:</span>
            <span className="font-semibold font-mono">{subtotal.toLocaleString()} ر.ي</span>
          </div>
          <div className="flex items-center justify-between text-sm gap-3">
            <span className="text-gray-500">الخصم:</span>
            <input
              type="number"
              min={0}
              step="0.01"
              value={discountAmount || ""}
              onChange={(e) => setDiscountAmount(parseFloat(e.target.value) || 0)}
              className="w-32 text-sm border border-gray-300 rounded-lg px-2 py-1 text-center focus:outline-none focus:ring-2 focus:ring-clinic-blue"
              dir="ltr"
              placeholder="0"
            />
          </div>
          <div className="flex justify-between text-lg font-bold">
            <span>الإجمالي:</span>
            <span className={cn("font-mono", totalAmount >= 0 ? "text-green-700" : "text-red-600")}>
              {totalAmount.toLocaleString()} ر.ي
            </span>
          </div>
        </div>
      </div>

      {/* Notes */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={2}
          className="w-full text-sm border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-clinic-blue resize-none"
          placeholder="ملاحظات إضافية على الفاتورة..."
        />
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 pb-4">
        <button
          type="button"
          onClick={() => router.push("/finance/invoices")}
          className="px-5 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
        >
          إلغاء
        </button>
        <button
          onClick={handleSubmit}
          disabled={saving || !patientId}
          className="flex items-center gap-2 px-6 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition"
        >
          <ArrowRight className="w-4 h-4" />
          {saving ? "جارٍ الإنشاء..." : "إنشاء الفاتورة"}
        </button>
      </div>
    </div>
  );
}
