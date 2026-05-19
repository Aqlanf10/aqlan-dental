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
  CreditCard,
  CheckCircle2,
  Receipt,
} from "lucide-react";
import type {
  InvoiceDetail,
  InvoiceLineItem,
  UpdateInvoiceRequest,
  UpdateInvoiceLineItemRequest,
  InvoicePayment,
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

  // Payment dialog state
  const [showPaymentDialog, setShowPaymentDialog] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState(0);
  const [paymentMethod, setPaymentMethod] = useState("cash");
  const [paymentNotes, setPaymentNotes] = useState("");
  const [paymentLoading, setPaymentLoading] = useState(false);

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
        setPaymentAmount(r.data.remainingAmount ?? r.data.totalAmount);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(loadInvoice, [loadInvoice]);

  const isDraft = invoice?.status === "Draft";
  const isIssued = invoice?.status === "Issued";
  const isPaid = invoice?.status === "Paid";

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

  const handleRecordPayment = async () => {
    if (!invoice || paymentAmount <= 0) return;
    setPaymentLoading(true);
    try {
      await api.post("/api/payments", {
        patientId: invoice.patientId,
        invoiceId: invoice.id,
        amount: paymentAmount,
        paymentMethod,
        serviceDescription: `دفعة فاتورة ${invoice.invoiceNumber}`,
        notes: paymentNotes || undefined,
      });
      toast.success("تم تسجيل الدفعة بنجاح");
      setShowPaymentDialog(false);
      setPaymentNotes("");
      loadInvoice(); // Reload to reflect paid amount / status change
    } catch {
      toast.error("فشل تسجيل الدفعة");
    } finally {
      setPaymentLoading(false);
    }
  };

  // ─── Computed values ─────────────────────────────────────────────────────

  const displayItems: EditLineItem[] = editMode
    ? editLineItems
    : toEditLineItems(invoice?.lineItems ?? []);
  const subtotal = editMode
    ? calcSubtotal(editLineItems)
    : invoice?.subtotal ?? 0;
  const discountAmt = editMode ? editDiscount : (invoice?.discountAmount ?? 0);
  const taxAmt = editMode ? editTax : (invoice?.taxAmount ?? 0);
  const total = subtotal - discountAmt + taxAmt;
  const paidAmount = invoice?.paidAmount ?? 0;
  const remainingAmount = invoice?.remainingAmount ?? total;
  const payments: InvoicePayment[] = invoice?.payments ?? [];

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
          {isIssued && remainingAmount > 0 && (
            <button
              onClick={() => {
                setPaymentAmount(remainingAmount);
                setShowPaymentDialog(true);
              }}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
            >
              <CreditCard className="w-3.5 h-3.5" />
              تسجيل دفعة
            </button>
          )}
        </div>
      </div>

      {/* Payment status banner for Issued/Paid invoices */}
      {(isIssued || isPaid) && (
        <div
          className={cn(
            "rounded-xl border shadow-sm p-4",
            isPaid
              ? "bg-emerald-50 border-emerald-200"
              : remainingAmount > 0
                ? "bg-amber-50 border-amber-200"
                : "bg-emerald-50 border-emerald-200"
          )}
        >
          <div className="flex items-center gap-3">
            {isPaid || remainingAmount <= 0 ? (
              <CheckCircle2 className="w-5 h-5 text-emerald-600 flex-shrink-0" />
            ) : (
              <CreditCard className="w-5 h-5 text-amber-600 flex-shrink-0" />
            )}
            <div className="flex-1">
              <div className="flex items-center justify-between">
                <span
                  className={cn(
                    "text-sm font-medium",
                    isPaid || remainingAmount <= 0
                      ? "text-emerald-800"
                      : "text-amber-800"
                  )}
                >
                  {isPaid || remainingAmount <= 0
                    ? "تم سداد الفاتورة بالكامل"
                    : `المبلغ المتبقي: ${formatYemeniRiyal(remainingAmount)}`}
                </span>
                <span className="text-xs text-gray-500">
                  المدفوع: {formatYemeniRiyal(paidAmount)} / {formatYemeniRiyal(total)}
                </span>
              </div>
              {/* Progress bar */}
              <div className="mt-2 h-2 bg-white/60 rounded-full overflow-hidden">
                <div
                  className={cn(
                    "h-full rounded-full transition-all duration-500",
                    isPaid || remainingAmount <= 0
                      ? "bg-emerald-500"
                      : "bg-amber-500"
                  )}
                  style={{
                    width: `${Math.min(100, total > 0 ? (paidAmount / total) * 100 : 0)}%`,
                  }}
                />
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Draft info banner */}
      {isDraft && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-2.5 text-xs text-blue-700 flex items-center gap-2">
          <LinkIcon className="w-3.5 h-3.5 flex-shrink-0" />
          الفاتورة مسودة — قم بإصدارها أولاً قبل تسجيل الدفعات
        </div>
      )}

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
                  const itemTotal = li.quantity * li.unitPrice;

                  return (
                    <tr key={li.tempId ?? idx} className="hover:bg-gray-50 transition">
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

      {/* Linked Payments */}
      {payments.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
          <div className="flex items-center gap-2 px-5 py-4 border-b border-gray-100">
            <Receipt className="w-4 h-4 text-gray-500" />
            <h2 className="font-bold text-gray-900">المدفوعات المرتبطة</h2>
            <span className="text-xs text-gray-400 mr-auto">
              {payments.length} دفعة
            </span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["التاريخ", "المبلغ", "طريقة الدفع", "رقم الإيصال", "ملاحظات"].map(
                    (h) => (
                      <th
                        key={h}
                        className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap"
                      >
                        {h}
                      </th>
                    )
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {payments.map((p) => (
                  <tr key={p.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 text-gray-700">
                      {p.paymentDate || "—"}
                    </td>
                    <td className="px-4 py-3 font-mono font-semibold text-gray-900">
                      {formatYemeniRiyal(p.amount)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {p.paymentMethod === "cash"
                        ? "نقدي"
                        : p.paymentMethod === "card"
                          ? "بطاقة"
                          : p.paymentMethod === "bank_transfer"
                            ? "تحويل بنكي"
                            : p.paymentMethod ?? "—"}
                    </td>
                    <td className="px-4 py-3">
                      {p.receiptNumber ? (
                        <Link
                          href={`/finance/payments/${p.id}`}
                          className="text-clinic-blue hover:underline font-mono text-xs"
                        >
                          {p.receiptNumber}
                        </Link>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs max-w-[200px] truncate">
                      {p.notes || "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Link to payments page (for Issued with no payments yet) */}
      {isIssued && payments.length === 0 && (
        <div className="flex items-center justify-center gap-2 text-sm text-gray-500">
          <LinkIcon className="w-3.5 h-3.5" />
          <span>لإجراء الدفع، استخدم زر &quot;تسجيل دفعة&quot; أعلاه</span>
        </div>
      )}

      {/* ─── Payment Dialog ──────────────────────────────────────────────── */}
      {showPaymentDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 p-6">
            <h3 className="text-lg font-bold text-gray-900 mb-4">
              تسجيل دفعة — فاتورة {invoice.invoiceNumber}
            </h3>

            <div className="space-y-4">
              <div className="bg-gray-50 rounded-lg p-3 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-500">الإجمالي</span>
                  <span className="font-mono font-bold">
                    {formatYemeniRiyal(total)}
                  </span>
                </div>
                <div className="flex justify-between mt-1">
                  <span className="text-gray-500">المدفوع سابقاً</span>
                  <span className="font-mono">{formatYemeniRiyal(paidAmount)}</span>
                </div>
                <div className="flex justify-between mt-1 border-t pt-1">
                  <span className="text-gray-700 font-medium">المتبقي</span>
                  <span className="font-mono font-bold text-amber-700">
                    {formatYemeniRiyal(remainingAmount)}
                  </span>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  مبلغ الدفعة (ر.ي)
                </label>
                <input
                  type="number"
                  min={0.01}
                  max={remainingAmount}
                  step={0.01}
                  value={paymentAmount}
                  onChange={(e) => setPaymentAmount(+e.target.value || 0)}
                  className={inputCls}
                  dir="ltr"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  طريقة الدفع
                </label>
                <select
                  value={paymentMethod}
                  onChange={(e) => setPaymentMethod(e.target.value)}
                  className={inputCls}
                >
                  <option value="cash">نقدي</option>
                  <option value="card">بطاقة</option>
                  <option value="bank_transfer">تحويل بنكي</option>
                  <option value="check">شيك</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">
                  ملاحظات (اختياري)
                </label>
                <input
                  type="text"
                  value={paymentNotes}
                  onChange={(e) => setPaymentNotes(e.target.value)}
                  className={inputCls}
                  placeholder="ملاحظات إضافية"
                />
              </div>
            </div>

            <div className="flex items-center gap-3 mt-6">
              <button
                onClick={handleRecordPayment}
                disabled={paymentLoading || paymentAmount <= 0 || paymentAmount > remainingAmount}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50 transition"
              >
                <CreditCard className="w-4 h-4" />
                {paymentLoading ? "جارٍ التسجيل..." : "تسجيل الدفعة"}
              </button>
              <button
                onClick={() => setShowPaymentDialog(false)}
                className="px-4 py-2.5 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 transition"
              >
                إلغاء
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
