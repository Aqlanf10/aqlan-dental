"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  ArrowRight,
  FileText,
  Plus,
  Trash2,
  Send,
  XCircle,
  Save,
  Link as LinkIcon,
} from "lucide-react";
import type {
  InvoiceDetail,
  InvoiceLineItem,
  UpdateInvoiceRequest,
  UpdateInvoiceLineItemRequest,
} from "@/types/finance";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { toast } from "@/stores/toastStore";

const STATUS_LABELS: Record<string, string> = {
  Draft: "مسودة",
  Issued: "مصدرة",
  Cancelled: "ملغاة",
  Paid: "مدفوعة",
};

const STATUS_COLORS: Record<string, string> = {
  Draft: "bg-blue-50 text-blue-700",
  Issued: "bg-green-50 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  Paid: "bg-emerald-50 text-emerald-700",
};

const inputCls =
  "w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-blue";

interface EditLineItem {
  tempId: string;
  serviceId?: string;
  serviceNameSnapshot: string;
  description: string;
  quantity: number;
  unitPrice: number;
  relatedTreatmentPlanStepId?: string;
  relatedVisitId?: string;
}

function toEditLineItems(items: InvoiceLineItem[]): EditLineItem[] {
  return items.map((li, idx) => ({
    tempId: `edit-${idx}-${li.id}`,
    serviceId: li.serviceId,
    serviceNameSnapshot: li.serviceNameSnapshot,
    description: li.description,
    quantity: li.quantity,
    unitPrice: li.unitPrice,
    relatedTreatmentPlanStepId: li.relatedTreatmentPlanStepId,
    relatedVisitId: li.relatedVisitId,
  }));
}

function calcSubtotal(items: EditLineItem[]): number {
  return items.reduce((sum, li) => sum + li.quantity * li.unitPrice, 0);
}

export default function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>();

  const [invoice, setInvoice] = useState<InvoiceDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);

  // Editable state (only used for Draft invoices)
  const [editMode, setEditMode] = useState(false);
  const [editLineItems, setEditLineItems] = useState<EditLineItem[]>([]);
  const [editDiscount, setEditDiscount] = useState(0);
  const [editTax, setEditTax] = useState(0);
  const [editNotes, setEditNotes] = useState("");

  const loadInvoice = useCallback(() => {
    setLoading(true);
    api
      .get<InvoiceDetail>(`/api/invoices/${id}`)
      .then((r) => {
        setInvoice(r.data);
        setEditLineItems(toEditLineItems(r.data.lineItems ?? []));
        setEditDiscount(r.data.discountAmount ?? 0);
        setEditTax(r.data.taxAmount ?? 0);
        setEditNotes(r.data.notes ?? "");
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(loadInvoice, [loadInvoice]);

  const isDraft = invoice?.status === "Draft";

  // ─── Edit handlers ───────────────────────────────────────────────────────

  const addLineItem = () => {
    setEditLineItems((prev) => [
      ...prev,
      {
        tempId: `new-${Date.now()}`,
        serviceNameSnapshot: "",
        description: "",
        quantity: 1,
        unitPrice: 0,
      },
    ]);
  };

  const removeLineItem = (tempId: string) => {
    setEditLineItems((prev) => prev.filter((li) => li.tempId !== tempId));
  };

  const updateLineItem = (
    tempId: string,
    field: keyof EditLineItem,
    value: string | number
  ) => {
    setEditLineItems((prev) =>
      prev.map((li) => (li.tempId === tempId ? { ...li, [field]: value } : li))
    );
  };

  const handleSave = async () => {
    if (!invoice) return;
    setSaving(true);
    try {
      const lineItems: UpdateInvoiceLineItemRequest[] = editLineItems.map(
        (li) => ({
          serviceId: li.serviceId,
          serviceNameSnapshot: li.serviceNameSnapshot || undefined,
          description: li.description,
          quantity: li.quantity,
          unitPrice: li.unitPrice,
          relatedTreatmentPlanStepId: li.relatedTreatmentPlanStepId,
          relatedVisitId: li.relatedVisitId,
        })
      );
      const payload: UpdateInvoiceRequest = {
        lineItems,
        discountAmount: editDiscount,
        taxAmount: editTax,
        notes: editNotes || undefined,
      };
      const { data } = await api.put<InvoiceDetail>(
        `/api/invoices/${id}`,
        payload
      );
      setInvoice(data);
      setEditLineItems(toEditLineItems(data.lineItems ?? []));
      setEditMode(false);
      toast.success("تم حفظ التعديلات بنجاح");
    } catch {
      toast.error("فشل حفظ التعديلات");
    } finally {
      setSaving(false);
    }
  };

  const handleIssue = async () => {
    if (!invoice) return;
    if (!confirm("هل تريد إصدار هذه الفاتورة؟")) return;
    setActionLoading(true);
    try {
      const { data } = await api.patch<InvoiceDetail>(
        `/api/invoices/${id}/issue`
      );
      setInvoice(data);
      setEditMode(false);
      toast.success("تم إصدار الفاتورة بنجاح");
    } catch {
      toast.error("فشل إصدار الفاتورة");
    } finally {
      setActionLoading(false);
    }
  };

  const handleCancel = async () => {
    if (!invoice) return;
    if (!confirm("هل تريد إلغاء هذه الفاتورة؟")) return;
    setActionLoading(true);
    try {
      const { data } = await api.patch<InvoiceDetail>(
        `/api/invoices/${id}/cancel`
      );
      setInvoice(data);
      setEditMode(false);
      toast.success("تم إلغاء الفاتورة");
    } catch {
      toast.error("فشل إلغاء الفاتورة");
    } finally {
      setActionLoading(false);
    }
  };

  // ─── Computed values ─────────────────────────────────────────────────────

  const displayItems = editMode ? editLineItems : (invoice?.lineItems ?? []);
  const subtotal = editMode
    ? calcSubtotal(editLineItems)
    : invoice?.subtotal ?? 0;
  const discountAmt = editMode ? editDiscount : (invoice?.discountAmount ?? 0);
  const taxAmt = editMode ? editTax : (invoice?.taxAmount ?? 0);
  const total = subtotal - discountAmt + taxAmt;

  // ─── Render ──────────────────────────────────────────────────────────────

  if (loading) {
    return (
      <div className="max-w-3xl space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-24 bg-gray-100 rounded-xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (!invoice) {
    return (
      <div className="text-center py-20 text-gray-400">
        <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
        <p className="text-sm">الفاتورة غير موجودة</p>
      </div>
    );
  }

  return (
    <div className="space-y-5 max-w-4xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/finance" className="hover:text-clinic-blue transition">
          المالية
        </Link>
        <span>/</span>
        <Link
          href="/finance/invoices"
          className="hover:text-clinic-blue transition"
        >
          الفواتير
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">
          {invoice.invoiceNumber}
        </span>
      </div>

      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link
            href="/finance/invoices"
            className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
          >
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div>
            <h1 className="text-2xl font-extrabold text-gray-900">
              فاتورة {invoice.invoiceNumber}
            </h1>
            <p className="text-sm text-gray-500 mt-0.5">
              <Link
                href={`/patients/${invoice.patientId}`}
                className="text-clinic-blue hover:underline"
              >
                {invoice.patientName ?? "المريض"}
              </Link>
              {" · "}
              {formatArabicDate(invoice.createdAt)}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span
            className={cn(
              "text-xs px-3 py-1 rounded-full font-medium",
              STATUS_COLORS[invoice.status] ?? "bg-gray-100 text-gray-600"
            )}
          >
            {STATUS_LABELS[invoice.status] ?? invoice.status}
          </span>

          {isDraft && !editMode && (
            <button
              onClick={() => setEditMode(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 transition"
            >
              <Save className="w-3.5 h-3.5" />
              تعديل
            </button>
          )}
          {isDraft && editMode && (
            <button
              onClick={handleSave}
              disabled={saving}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50 transition"
            >
              <Save className="w-3.5 h-3.5" />
              {saving ? "جارٍ الحفظ..." : "حفظ"}
            </button>
          )}
          {isDraft && editMode && (
            <button
              onClick={() => {
                setEditMode(false);
                setEditLineItems(toEditLineItems(invoice.lineItems ?? []));
                setEditDiscount(invoice.discountAmount ?? 0);
                setEditTax(invoice.taxAmount ?? 0);
                setEditNotes(invoice.notes ?? "");
              }}
              className="px-3 py-1.5 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 transition"
            >
              تراجع
            </button>
          )}
          {isDraft && (
            <button
              onClick={handleIssue}
              disabled={actionLoading}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-green-300 text-green-700 hover:bg-green-50 disabled:opacity-50 transition"
            >
              <Send className="w-3.5 h-3.5" />
              إصدار
            </button>
          )}
          {isDraft && (
            <button
              onClick={handleCancel}
              disabled={actionLoading}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-red-300 text-red-600 hover:bg-red-50 disabled:opacity-50 transition"
            >
              <XCircle className="w-3.5 h-3.5" />
              إلغاء
            </button>
          )}
        </div>
      </div>

      {/* Payment reminder */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-2.5 text-xs text-blue-700 flex items-center gap-2">
        <LinkIcon className="w-3.5 h-3.5 flex-shrink-0" />
        الدفع يتم عبر صفحة المالية
      </div>

      {/* Invoice summary card */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
          <div className="bg-gray-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">المجموع الفرعي</p>
            <p className="font-bold font-mono text-gray-900 mt-1">
              {formatYemeniRiyal(subtotal)}
            </p>
          </div>
          <div className="bg-orange-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">الخصم</p>
            <p className="font-bold font-mono text-orange-700 mt-1">
              {formatYemeniRiyal(discountAmt)}
            </p>
          </div>
          <div className="bg-purple-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">الضريبة</p>
            <p className="font-bold font-mono text-purple-700 mt-1">
              {formatYemeniRiyal(taxAmt)}
            </p>
          </div>
          <div className="bg-green-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">الإجمالي</p>
            <p className="font-bold font-mono text-green-700 mt-1">
              {formatYemeniRiyal(total)}
            </p>
          </div>
        </div>

        {/* Editable discount/tax/notes for Draft in edit mode */}
        {isDraft && editMode && (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3 pt-4 border-t border-gray-100">
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الخصم (ر.ي)
              </label>
              <input
                type="number"
                min={0}
                value={editDiscount}
                onChange={(e) => setEditDiscount(+e.target.value || 0)}
                className={inputCls}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                الضريبة (ر.ي)
              </label>
              <input
                type="number"
                min={0}
                value={editTax}
                onChange={(e) => setEditTax(+e.target.value || 0)}
                className={inputCls}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-600 mb-1">
                ملاحظات
              </label>
              <input
                type="text"
                value={editNotes}
                onChange={(e) => setEditNotes(e.target.value)}
                className={inputCls}
                placeholder="ملاحظات إضافية"
              />
            </div>
          </div>
        )}

        {invoice.notes && !editMode && (
          <div className="mt-3 text-sm text-gray-600 bg-gray-50 rounded-lg p-3">
            {invoice.notes}
          </div>
        )}

        <div className="flex items-center gap-4 mt-4 text-xs text-gray-500 pt-4 border-t border-gray-100 flex-wrap">
          <span>
            تاريخ الإنشاء:{" "}
            <span className="font-medium text-gray-700">
              {formatArabicDate(invoice.createdAt)}
            </span>
          </span>
          <span>
            آخر تحديث:{" "}
            <span className="font-medium text-gray-700">
              {formatArabicDate(invoice.updatedAt)}
            </span>
          </span>
          {invoice.createdBy && (
            <span>
              أنشئ بواسطة:{" "}
              <span className="font-medium text-gray-700">
                {invoice.createdBy}
              </span>
            </span>
          )}
        </div>
      </div>

      {/* Line Items */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">بنود الفاتورة</h2>
          {isDraft && editMode && (
            <button
              onClick={addLineItem}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
            >
              <Plus className="w-3.5 h-3.5" />
              إضافة بند
            </button>
          )}
        </div>

        {displayItems.length === 0 ? (
          <div className="text-center py-10 text-gray-400 text-sm">
            لا توجد بنود في الفاتورة
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {[
                    "الخدمة",
                    "الوصف",
                    "الكمية",
                    "سعر الوحدة",
                    "الإجمالي",
                    isDraft && editMode ? "" : "",
                  ]
                    .filter(Boolean)
                    .map((h) => (
                      <th
                        key={h}
                        className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                      >
                        {h}
                      </th>
                    ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {displayItems.map((li, idx) => {
                  const itemTotal = editMode
                    ? li.quantity * li.unitPrice
                    : li.totalPrice;

                  return (
                    <tr key={li.tempId ?? li.id ?? idx} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-3">
                        {isDraft && editMode ? (
                          <input
                            type="text"
                            value={li.serviceNameSnapshot}
                            onChange={(e) =>
                              updateLineItem(
                                li.tempId,
                                "serviceNameSnapshot",
                                e.target.value
                              )
                            }
                            className={cn(inputCls, "min-w-[120px]")}
                            placeholder="اسم الخدمة"
                          />
                        ) : (
                          <span className="font-medium text-gray-900">
                            {li.serviceNameSnapshot || "—"}
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        {isDraft && editMode ? (
                          <input
                            type="text"
                            value={li.description}
                            onChange={(e) =>
                              updateLineItem(
                                li.tempId,
                                "description",
                                e.target.value
                              )
                            }
                            className={cn(inputCls, "min-w-[120px]")}
                            placeholder="الوصف"
                          />
                        ) : (
                          <span className="text-gray-600">
                            {li.description || "—"}
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        {isDraft && editMode ? (
                          <input
                            type="number"
                            min={1}
                            value={li.quantity}
                            onChange={(e) =>
                              updateLineItem(
                                li.tempId,
                                "quantity",
                                +e.target.value || 1
                              )
                            }
                            className={cn(inputCls, "w-20")}
                            dir="ltr"
                          />
                        ) : (
                          <span className="text-gray-700">{li.quantity}</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        {isDraft && editMode ? (
                          <input
                            type="number"
                            min={0}
                            value={li.unitPrice}
                            onChange={(e) =>
                              updateLineItem(
                                li.tempId,
                                "unitPrice",
                                +e.target.value || 0
                              )
                            }
                            className={cn(inputCls, "w-28")}
                            dir="ltr"
                          />
                        ) : (
                          <span className="font-mono text-gray-700">
                            {formatYemeniRiyal(li.unitPrice)}
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3 font-mono font-semibold text-gray-900">
                        {formatYemeniRiyal(itemTotal)}
                      </td>
                      {isDraft && editMode && (
                        <td className="px-4 py-3">
                          <button
                            onClick={() => removeLineItem(li.tempId)}
                            className="text-red-400 hover:text-red-600 transition"
                            title="حذف البند"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Link to payments page */}
      <div className="flex items-center justify-center gap-2 text-sm text-gray-500">
        <LinkIcon className="w-3.5 h-3.5" />
        <span>لإجراء الدفع، انتقل إلى</span>
        <Link
          href="/finance/payments"
          className="text-clinic-blue hover:underline font-medium"
        >
          صفحة الدفعات
        </Link>
      </div>
    </div>
  );
}
