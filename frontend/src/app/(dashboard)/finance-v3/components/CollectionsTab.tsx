"use client";

import { useState, useEffect, useCallback } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Receipt,
  Plus,
  RefreshCw,
  Trash2,
  Printer,
  Download,
  Loader2,
  AlertTriangle,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { useAuthStore } from "@/stores/authStore";
import { useActiveCashierSession } from "@/hooks/useCashierSession";
import { downloadPdfFromApi, printPdfFromApi } from "@/lib/pdfDownload";
import type { PaymentListItem, RegisterPaymentRequest } from "./types";
import { PAYMENT_METHODS } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate } from "./FinanceHelpers";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

/* ── Inline types for API responses ──────────────────────────────────────────── */
interface PatientInvoice {
  id: string;
  invoiceNumber: string;
  status: string;
  totalAmount: number;
  paidAmount: number;
  balance: number;
}

interface FinanceV3Contract {
  id: string;
  contractNumber: string;
  specialty?: string;
  outstandingAmount: number;
  totalAmount: number;
  status: string;
}

/* ── FE-30: zod schema for the inline "register payment" modal ────────────────
   Mirrors the prior ad-hoc validation:
     - patientId required (set via PatientCombobox → setValue)
     - amount required, > 0, ≤ maxAmount (maxAmount is dynamic — enforced in
       onSubmit + inline UI warning, NOT in zod, since it depends on the
       invoice/contract selection state outside the schema).
     - paymentMethod required (select with PAYMENT_METHODS default)
     - invoiceId / contractId mutually exclusive (cleared via onChange)
     - notes optional ──────────────────────────────────────────────────────── */
const registerPaymentSchema = z.object({
  patientId: z.string().min(1, { message: "يرجى اختيار المريض" }),
  invoiceId: z.string().optional(),
  contractId: z.string().optional(),
  amount: z
    .string()
    .min(1, { message: "يرجى إدخال المبلغ" })
    .refine((v) => Number(v) > 0, { message: "المبلغ يجب أن يكون أكبر من صفر" }),
  paymentMethod: z.string().min(1, { message: "طريقة الدفع مطلوبة" }),
  currency: z.enum(["YER", "SAR", "USD"]).optional(),
  accountCurrency: z.enum(["YER", "SAR", "USD"]).optional(),
  exchangeRateToAccountCurrency: z.string().optional(),
  notes: z.string().optional(),
});
type RegisterPaymentFormData = z.infer<typeof registerPaymentSchema>;

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 4: Collections

   Fix 1: Contract linking now uses GET /api/finance-v3/contracts?patientId=
          instead of the non-existent GET /api/patients/{id}/contracts.
          The FinanceV3 endpoint wraps in { data, total, page, pageSize } and
          returns contractNumber + outstandingAmount.

   Fix 2: Receipt number column now reads receiptNumber from backend response.
          The backend also aliases ReceiptNumber as PaymentNumber for compat.

   Fix 3: Delete/reverse button is hidden for non-Admin users since the
          DELETE endpoint requires AdminOnly policy.
   ═══════════════════════════════════════════════════════════════════════════════ */
export function CollectionsTab() {
  const [payments, setPayments] = useState<PaymentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showRegister, setShowRegister] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Fix 3: Get current user role for permission checks
  const { user } = useAuthStore();
  const isAdmin = user?.role === "Admin";
  const { data: activeCashierSession } = useActiveCashierSession();

  // Linked invoice/contract option lists (populated on patient select).
  const [invoiceOptions, setInvoiceOptions] = useState<{ id: string; invoiceNumber: string; balance: number }[]>([]);
  const [contractOptions, setContractOptions] = useState<{ id: string; contractNumber: string; outstandingAmount: number }[]>([]);

  // FE-30: react-hook-form + zod for the register-payment modal.
  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors },
  } = useForm<RegisterPaymentFormData>({
    resolver: zodResolver(registerPaymentSchema),
    defaultValues: {
      patientId: "",
      invoiceId: "",
      contractId: "",
      amount: "",
      paymentMethod: "cash",
      currency: "YER",
      accountCurrency: "YER",
      exchangeRateToAccountCurrency: "",
      notes: "",
    },
  });

  const fetchPayments = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: PaymentListItem[]; total: number }>("/api/finance-v3/payments"); setPayments(responseData?.data ?? []); } catch { toast.error("فشل في تحميل التحصيلات"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchPayments(); }, [fetchPayments]);

  const onPatientSelect = async (patientId: string) => {
    setValue("patientId", patientId);
    setValue("invoiceId", "");
    setValue("contractId", "");
    try {
      // Fix 1: Use real backend routes
      // Invoices: GET /api/patients/{patientId}/invoices (exists in InvoicesController)
      // Contracts: GET /api/finance-v3/contracts?patientId= (FinanceV3Controller, returns { data, total, page, pageSize })
      const [invRes, conRes] = await Promise.all([
        api.get<PatientInvoice[] | { data: PatientInvoice[] }>(`/api/patients/${patientId}/invoices`),
        api.get<{ data: FinanceV3Contract[]; total: number }>(`/api/finance-v3/contracts`, { params: { patientId, status: "active", pageSize: 100 } }),
      ]);

      // Parse invoices — endpoint returns flat array
      const invRaw = invRes.data;
      const invData: PatientInvoice[] = Array.isArray(invRaw)
        ? invRaw
        : (invRaw as { data?: PatientInvoice[] })?.data ?? [];
      setInvoiceOptions(
        invData
          .filter((i) => i.balance > 0)
          .map((i) => ({ id: i.id, invoiceNumber: i.invoiceNumber, balance: i.balance }))
      );

      // Parse contracts — FinanceV3 wraps in { data: [...] }
      const conData: FinanceV3Contract[] = conRes.data?.data ?? [];
      setContractOptions(
        conData
          .filter((c) => c.outstandingAmount > 0)
          .map((c) => ({
            id: c.id,
            contractNumber: c.contractNumber ?? c.specialty ?? `عقد ${c.id.substring(0, 8)}`,
            outstandingAmount: c.outstandingAmount,
          }))
      );
    } catch { /* ignore */ }
  };

  // Overpayment guard — depends on the currently selected invoice/contract.
  const selectedInvoice = watch("invoiceId");
  const selectedContract = watch("contractId");
  const payAmount = watch("amount");

  const maxAmount = (() => {
    if (selectedInvoice) { const inv = invoiceOptions.find((i) => i.id === selectedInvoice); return inv?.balance ?? 0; }
    if (selectedContract) { const con = contractOptions.find((c) => c.id === selectedContract); return con?.outstandingAmount ?? 0; }
    return 0;
  })();

  const onSubmit = handleSubmit(async (formData) => {
    if (!activeCashierSession) {
      toast.error("يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل أي مدفوعات");
      return;
    }
    // Overpayment guard (dynamic — cannot live in zod).
    if (maxAmount > 0 && Number(formData.amount) > maxAmount) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(maxAmount)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: RegisterPaymentRequest = {
        patientId: formData.patientId,
        amount: Number(formData.amount),
        paymentMethod: formData.paymentMethod,
        currency: formData.currency ?? "YER",
        accountCurrency: formData.accountCurrency ?? "YER",
        exchangeRateToAccountCurrency: formData.exchangeRateToAccountCurrency
          ? Number(formData.exchangeRateToAccountCurrency)
          : null,
        exchangeRateSource: formData.exchangeRateToAccountCurrency ? "manual" : null,
        notes: formData.notes || undefined,
      };
      if (formData.invoiceId) payload.invoiceId = formData.invoiceId;
      if (formData.contractId) payload.contractId = formData.contractId;

      // Part C: Capture created payment id + receiptNumber for immediate receipt download
      const { data: created } = await api.post<{ id?: string; receiptNumber?: string }>("/api/finance-v3/payments", payload);
      resetForm();
      setShowRegister(false);
      fetchPayments();

      // Attempt immediate PDF download — if it fails, still show payment success
      const paymentId = created?.id;
      const receiptNum = created?.receiptNumber;
      if (paymentId) {
        const filename = receiptNum ? `receipt-${receiptNum}.pdf` : `receipt-${paymentId}.pdf`;
        try {
          await downloadPdfFromApi(`/api/payments/${paymentId}/pdf`, filename);
          toast.success("تم تسجيل الدفعة وتحميل سند القبض");
        } catch (pdfErr) {
          const reason = pdfErr instanceof Error ? pdfErr.message : "خطأ";
          toast.success("تم تسجيل الدفعة بنجاح");
          toast.error(`فشل تحميل سند القبض: ${reason}`);
        }
      } else {
        toast.success("تم تسجيل الدفعة بنجاح");
      }
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تسجيل الدفعة")); } finally { setSubmitting(false); }
  });

  const handleDelete = async () => {
    if (!confirmDelete) return;
    try {
      setSubmitting(true);
      await api.delete(`/api/finance-v3/payments/${confirmDelete}`);
      toast.success("تم عكس الدفعة بنجاح");
      setConfirmDelete(null);
      fetchPayments();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في عكس الدفعة")); } finally { setSubmitting(false); }
  };

  const resetForm = () => {
    reset({
      patientId: "",
      invoiceId: "",
      contractId: "",
      amount: "",
      paymentMethod: "cash",
      notes: "",
    });
    setInvoiceOptions([]);
    setContractOptions([]);
  };

  // Fix 2: Prefer receiptNumber from backend; paymentNumber is an alias for the same value
  const getReceiptNumber = (r: PaymentListItem) =>
    r.receiptNumber ?? r.paymentNumber ?? "—";

  const overpaid = maxAmount > 0 && Number(payAmount) > maxAmount;

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="التحصيل" action={
        <div className="flex items-center gap-2">
          <button
            onClick={() => {
              if (!activeCashierSession) {
                toast.error("يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل أي مدفوعات");
                return;
              }
              resetForm();
              setShowRegister(true);
            }}
            style={{ ...btnPrimary, opacity: activeCashierSession ? 1 : 0.6, cursor: activeCashierSession ? undefined : "not-allowed" }}
            title={!activeCashierSession ? "يجب فتح صندوق الكاشير أولاً" : undefined}
          >
            <span className="w-2 h-2 rounded-full inline-block" style={{ background: activeCashierSession ? "#86efac" : "#ef4444" }} />
            <Plus className="w-4 h-4" /> تسجيل دفعة
          </button>
          <button onClick={fetchPayments} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : payments.length === 0 ? <EmptyState icon={Receipt} message="لا توجد تحصيلات" /> : (
        <DataTable<PaymentListItem>
          keyField="id"
          data={payments}
          columns={[
            { key: "paymentNumber", label: "رقم الإيصال", render: (r) => getReceiptNumber(r) },
            { key: "patientName", label: "المريض" },
            { key: "amount", label: "المبلغ", render: (r) => {
              const sym = r.currency === "SAR" ? "ر.س" : r.currency === "USD" ? "$" : "ر.ي";
              return `${r.amount.toLocaleString("en-US")} ${sym}`;
            } },
            { key: "paymentMethod", label: "طريقة الدفع", render: (r) => PAYMENT_METHODS.find((m) => m.value === r.paymentMethod)?.label ?? r.paymentMethod },
            { key: "paymentDate", label: "التاريخ", render: (r) => safeFormatDate(r.paymentDate) },
            { key: "isReversal", label: "عكسي", render: (r) => r.isReversal ? <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>نعم</span> : "—" },
            { key: "hasJournalEntry", label: "قيود", render: (r) => r.hasJournalEntry === false ? <span title="لا يوجد قيد محاسبي"><AlertTriangle className="w-4 h-4" style={{ color: tokens.warningBorder }} /></span> : <span style={{ color: tokens.successBorder }}>✓</span> },
            { key: "actions", label: "إجراءات", render: (r) => !r.isReversal && !r.reversedById ? (
              <div className="flex items-center gap-1">
                {/* Fix 3: Only show reverse/delete to Admin (DELETE /payments/{id} is AdminOnly) */}
                {isAdmin && (
                  <button onClick={(e) => { e.stopPropagation(); setConfirmDelete(r.id); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="عكس الدفعة"><Trash2 className="w-3.5 h-3.5" /></button>
                )}
                {/* Download PDF button */}
                <button
                  onClick={async (e) => {
                    e.stopPropagation();
                    try {
                      const filename = r.receiptNumber ? `receipt-${r.receiptNumber}.pdf` : `receipt-${r.id}.pdf`;
                      await downloadPdfFromApi(`/api/payments/${r.id}/pdf`, filename);
                      toast.success("تم تحميل سند القبض");
                    } catch (err) {
                      const reason = err instanceof Error ? err.message : "خطأ";
                      toast.error(`فشل تحميل سند القبض: ${reason}`);
                    }
                  }}
                  className="w-7 h-7 rounded-md flex items-center justify-center"
                  style={{ color: tokens.successBorder }}
                  title="تحميل PDF"
                >
                  <Download className="w-3.5 h-3.5" />
                </button>
                {/* Print PDF button — prints the PDF itself, not the system page */}
                <button
                  onClick={async (e) => {
                    e.stopPropagation();
                    try {
                      const filename = r.receiptNumber ? `receipt-${r.receiptNumber}.pdf` : `receipt-${r.id}.pdf`;
                      await printPdfFromApi(`/api/payments/${r.id}/pdf`, filename);
                    } catch (err) {
                      const reason = err instanceof Error ? err.message : "خطأ";
                      toast.error(`فشل طباعة سند القبض: ${reason}`);
                    }
                  }}
                  className="w-7 h-7 rounded-md flex items-center justify-center"
                  style={{ color: tokens.brand }}
                  title="طباعة مباشرة"
                >
                  <Printer className="w-3.5 h-3.5" />
                </button>
              </div>
            ) : null },
          ]}
        />
      )}

      {/* Register Payment Modal */}
      <Modal open={showRegister} onClose={() => setShowRegister(false)} title="تسجيل دفعة جديدة" wide>
        <form className="space-y-4" onSubmit={onSubmit}>
          {/* Patient search */}
          <div>
            <label style={labelStyle}>المريض <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <PatientCombobox onSelect={(p) => onPatientSelect(p.id)} placeholder="ابحث بالاسم أو الرقم..." />
            {errors.patientId && (
              <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>{errors.patientId.message}</p>
            )}
          </div>

          {/* Select invoice or contract */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label style={labelStyle}>فاتورة</label>
              <select
                {...register("invoiceId")}
                onChange={(e) => {
                  setValue("invoiceId", e.target.value, { shouldValidate: false });
                  if (e.target.value) setValue("contractId", "");
                }}
                style={inputStyle}
              >
                <option value="">— اختر فاتورة —</option>
                {invoiceOptions.map((i) => (<option key={i.id} value={i.id}>{i.invoiceNumber} ({formatYER(i.balance)})</option>))}
              </select>
            </div>
            <div>
              <label style={labelStyle}>عقد</label>
              <select
                {...register("contractId")}
                onChange={(e) => {
                  setValue("contractId", e.target.value, { shouldValidate: false });
                  if (e.target.value) setValue("invoiceId", "");
                }}
                style={inputStyle}
              >
                <option value="">— اختر عقد —</option>
                {contractOptions.map((c) => (<option key={c.id} value={c.id}>{c.contractNumber} ({formatYER(c.outstandingAmount)})</option>))}
              </select>
            </div>
          </div>

          {/* Amount */}
          <div>
            <label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              {...register("amount")}
              type="number"
              min="0"
              max={maxAmount || undefined}
              step="0.01"
              placeholder="0"
              dir="ltr"
              style={inputStyle}
            />
            {errors.amount && (
              <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>{errors.amount.message}</p>
            )}
            {overpaid && (
              <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز المستحق ({formatYER(maxAmount)})</p>
            )}
          </div>

          {/* Payment method */}
          <div>
            <label style={labelStyle}>طريقة الدفع</label>
            <select {...register("paymentMethod")} style={inputStyle}>
              {PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}
            </select>
            {errors.paymentMethod && (
              <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>{errors.paymentMethod.message}</p>
            )}
          </div>

          {/* Currency — MULTI-CURRENCY: patients may pay in SAR/USD (ortho patients often pay SAR) */}
          <div>
            <label style={labelStyle}>العملة</label>
            <select {...register("currency")} style={inputStyle}>
              <option value="YER">ر.ي يمني</option>
              <option value="SAR">ر.س سعودي</option>
              <option value="USD">$ دولار</option>
            </select>
          </div>

          <div>
            <label style={labelStyle}>عملة حساب المريض</label>
            <select {...register("accountCurrency")} style={inputStyle}>
              <option value="YER">حساب بالريال اليمني</option>
              <option value="SAR">حساب بالسعودي</option>
              <option value="USD">حساب بالدولار</option>
            </select>
          </div>

          <div>
            <label style={labelStyle}>سعر الصرف للحساب</label>
            <input
              {...register("exchangeRateToAccountCurrency")}
              type="number"
              min="0"
              step="0.000001"
              dir="ltr"
              placeholder="مثال: 665 عند دفع سعودي لحساب يمني"
              style={inputStyle}
            />
            <p className="text-[11px] mt-1" style={{ color: tokens.textTertiary }}>
              اتركه فارغاً لاستخدام آخر سعر معتمد من إعدادات المالية.
            </p>
          </div>

          {/* Notes */}
          <div>
            <label style={labelStyle}>ملاحظات</label>
            <input {...register("notes")} placeholder="ملاحظات اختيارية..." style={inputStyle} />
          </div>

          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button type="button" onClick={() => setShowRegister(false)} style={btnGhost}>إلغاء</button>
            <button type="submit" disabled={submitting || overpaid} style={{ ...btnPrimary, opacity: submitting || overpaid ? 0.6 : 1 }}>
              {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
              {submitting ? "جارٍ الحفظ..." : "تسجيل الدفعة"}
            </button>
          </div>
        </form>
      </Modal>

      {/* Fix 3: Confirm dialog for delete — only reachable by Admin */}
      <ConfirmDialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)} onConfirm={handleDelete} title="عكس الدفعة" message="هل أنت متأكد من عكس هذه الدفعة؟ سيتم إنشاء قيد عكسي." confirmLabel="عكس الدفعة" danger />
    </div>
  );
}
