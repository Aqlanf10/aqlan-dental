/**
 * PaymentModal — Unified payment-collection modal (FE-07).
 *
 * Replaces the 5 duplicated payment-collection modals identified in the audit:
 *   1. QuickPaymentModal      (daily-operations/_components/Modals.tsx)
 *   2. DirectPaymentModal     (daily-operations/_components/Modals.tsx)
 *   3. RecordPaymentModal     (patient-journey/_components/Modals.tsx)
 *   4. PaymentsTab inline form (components/patient/tabs/PaymentsTab.tsx)
 *   5. CollectionsTab inline "register payment" form
 *      (finance-v3/components/CollectionsTab.tsx)
 *
 * This file ONLY introduces the new shared component. The existing modals are
 * intentionally left untouched — migrating call sites happens in a follow-up PR
 * so this change stays safe and reviewable.
 *
 * Design notes:
 *  - Uses react-hook-form + zod (consistent with PatientForm, AppointmentForm, etc.).
 *  - Posts to POST /api/payments via the shared `api` axios client (JWT + refresh
 *    handled by the interceptor).
 *  - Enforces the "active cashier session required" business rule via the shared
 *    `useActiveCashierSession` hook. When no session is open the submit button is
 *    disabled and a warning banner is shown.
 *  - On success: invalidates react-query caches for ["payments"],
 *    ["patient-finance"], ["daily-ops"], triggers an immediate PDF receipt
 *    download via downloadPdfFromApi, fires onSuccess(id), and closes.
 *  - Preserves the daily-ops voucher-prefix convention: when voucherType is
 *    "consultation" or "procedure", the serviceDescription is prefixed with
 *    "[سند معاينة] " / "[إجراءات شغل/خدمة مقدمة] " before being sent.
 *
 * NOTE for the follow-up migration PR: the existing daily-ops modals also support
 * a dynamic reference-number field (driven by /api/settings/payment-methods
 * `requiresReferenceNumber`). That support is intentionally NOT included here to
 * keep this first cut within the FE-07 spec field list; it can be layered on when
 * wiring up QuickPaymentModal/DirectPaymentModal call sites.
 */

"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  X,
  CreditCard,
  Loader2,
  AlertTriangle,
  FileText,
} from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { useActiveCashierSession } from "@/hooks/useCashierSession";
import {
  PAYMENT_METHODS,
  inputCls,
  fmtRial,
} from "@/components/shared/journey/constants";
import type { Contract } from "@/types/finance";

// ─── Brand palette (clinic navy / blue) ─────────────────────────────────────
const NAVY = "#1a3a5c";
const BLUE = "#3d7ab5";
const GREEN = "#22c55e";

// ─── Public types ───────────────────────────────────────────────────────────
export type PaymentVoucherType = "consultation" | "procedure" | "general";
export type PaymentModalMode = "quick" | "full";

export interface PaymentModalProps {
  /** Patient the payment is recorded against. Required. */
  patientId: string;
  open: boolean;
  onClose: () => void;
  /** Fired after a successful payment (and receipt download attempt). */
  onSuccess?: (paymentId: string) => void;
  /** Pre-link the payment to a specific invoice. */
  invoiceId?: string;
  /** Pre-link the payment to a specific contract. */
  contractId?: string;
  /**
   * Voucher type tag. "consultation"/"procedure" prefix the serviceDescription
   * with the daily-ops voucher tag; "general" (default) sends no prefix.
   */
  voucherType?: PaymentVoucherType;
  /** Preset amount (e.g. outstanding balance). */
  amount?: number;
  /** "quick" = amount + method only; "full" (default) = all fields. */
  mode?: PaymentModalMode;
}

// ─── Voucher prefixes (preserve existing daily-ops convention) ──────────────
const VOUCHER_PREFIX: Record<Exclude<PaymentVoucherType, "general">, string> = {
  consultation: "[سند معاينة] ",
  procedure: "[إجراءات شغل/خدمة مقدمة] ",
};

// ─── Linked-invoice option (fetched per patient in full mode) ───────────────
interface InvoiceOption {
  id: string;
  invoiceNumber: string;
  balance: number;
}

// ─── Validation schema ──────────────────────────────────────────────────────
const schema = z.object({
  amount: z
    .string()
    .min(1, { message: "المبلغ مطلوب" })
    .refine((v) => parseFloat(v) > 0, {
      message: "المبلغ يجب أن يكون أكبر من صفر",
    }),
  paymentMethod: z.string().min(1, { message: "طريقة الدفع مطلوبة" }),
  serviceDescription: z.string().optional(),
  notes: z.string().optional(),
  contractId: z.string().optional(),
  invoiceId: z.string().optional(),
});

type FormData = z.infer<typeof schema>;

// ─── Component ──────────────────────────────────────────────────────────────
export function PaymentModal({
  patientId,
  open,
  onClose,
  onSuccess,
  invoiceId,
  contractId,
  voucherType = "general",
  amount,
  mode = "full",
}: PaymentModalProps) {
  const isFull = mode === "full";

  // Active cashier session — required to record a payment.
  const { data: activeCashierSession, isLoading: sessionLoading } =
    useActiveCashierSession();
  const hasOpenSession = !!activeCashierSession;

  const queryClient = useQueryClient();

  // ─── Linked contracts / invoices (full mode only) ───────────────────────
  const { data: contracts = [] } = useQuery<Contract[]>({
    queryKey: ["payments", "contracts", patientId],
    queryFn: async () => {
      const { data } = await api.get<Contract[] | { data?: Contract[] }>(
        `/api/contracts?patientId=${patientId}&status=active`,
      );
      return Array.isArray(data) ? data : (data?.data ?? []);
    },
    enabled: open && isFull && !!patientId,
    staleTime: 30_000,
  });

  const { data: invoices = [] } = useQuery<InvoiceOption[]>({
    queryKey: ["payments", "invoices", patientId],
    queryFn: async () => {
      const { data } = await api.get<
        InvoiceOption[] | { data?: InvoiceOption[] }
      >(`/api/patients/${patientId}/invoices`);
      const list = Array.isArray(data) ? data : (data?.data ?? []);
      return list.filter((i) => i.balance > 0);
    },
    enabled: open && isFull && !!patientId,
    staleTime: 30_000,
  });

  // ─── Form ───────────────────────────────────────────────────────────────
  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      amount: amount ? String(amount) : "",
      paymentMethod: "cash",
      serviceDescription: "",
      notes: "",
      contractId: contractId ?? "",
      invoiceId: invoiceId ?? "",
    },
  });

  // QA-594: watch link fields to surface a warning when the payment is
  // recorded without an invoice or contract. Unlinked payments are allowed
  // (transitional) but they will not reduce any tracked debt — staff must
  // know this before submitting.
  const watchedInvoiceId = watch("invoiceId");
  const watchedContractId = watch("contractId");
  const isUnlinkedPayment = isFull && !watchedInvoiceId && !watchedContractId;

  // Reset whenever the modal opens or the presets change.
  useEffect(() => {
    if (open) {
      reset({
        amount: amount ? String(amount) : "",
        paymentMethod: "cash",
        serviceDescription: "",
        notes: "",
        contractId: contractId ?? "",
        invoiceId: invoiceId ?? "",
      });
    }
  }, [open, amount, contractId, invoiceId, reset]);

  // Close on Escape.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  const [submitting, setSubmitting] = useState(false);
  const [downloadingPdf, setDownloadingPdf] = useState(false);

  const onSubmit = handleSubmit(async (formData) => {
    if (!hasOpenSession) {
      toast.error(
        "يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل أي مدفوعات",
      );
      return;
    }
    const numAmount = parseFloat(formData.amount);
    if (!numAmount || numAmount <= 0) return;

    // Apply the voucher prefix (preserve daily-ops convention).
    let serviceDescription = formData.serviceDescription?.trim() ?? "";
    if (voucherType !== "general") {
      const prefix = VOUCHER_PREFIX[voucherType];
      serviceDescription = serviceDescription
        ? `${prefix}${serviceDescription}`
        : prefix.trim();
    }

    // invoiceId and contractId are mutually exclusive.
    const invoiceIdVal = formData.invoiceId || undefined;
    const contractIdVal = invoiceIdVal
      ? undefined
      : formData.contractId || undefined;

    const payload = {
      patientId,
      amount: numAmount,
      paymentMethod: formData.paymentMethod,
      serviceDescription: serviceDescription || undefined,
      notes: formData.notes?.trim() || undefined,
      contractId: contractIdVal,
      invoiceId: invoiceIdVal,
    };

    setSubmitting(true);
    try {
      const { data: created } = await api.post<{
        id?: string;
        receiptNumber?: string;
      }>("/api/payments", payload);

      const paymentId = created?.id;
      if (!paymentId) {
        throw new Error("لم يتم استقبال معرف الدفعة من الخادم");
      }

      // Invalidate react-query caches so lists refresh everywhere.
      queryClient.invalidateQueries({ queryKey: ["payments"] });
      queryClient.invalidateQueries({ queryKey: ["patient-finance"] });
      queryClient.invalidateQueries({ queryKey: ["daily-ops"] });

      // Best-effort immediate receipt download.
      setDownloadingPdf(true);
      const filename = created.receiptNumber
        ? `receipt-${created.receiptNumber}.pdf`
        : `receipt-${paymentId}.pdf`;
      try {
        await downloadPdfFromApi(`/api/payments/${paymentId}/pdf`, filename);
        toast.success("تم تسجيل الدفعة وتحميل سند القبض");
      } catch (pdfErr) {
        const reason =
          pdfErr instanceof Error ? pdfErr.message : "خطأ غير متوقع";
        toast.success("تم تسجيل الدفعة بنجاح");
        toast.error(`فشل تحميل سند القبض: ${reason}`);
      } finally {
        setDownloadingPdf(false);
      }

      onSuccess?.(paymentId);
      onClose();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data
          ?.message ??
        (err instanceof Error ? err.message : "فشل تسجيل الدفعة");
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  });

  if (!open) return null;

  const title = isFull ? "تسجيل دفعة" : "تحصيل سريع";
  const submitLabel = isFull ? "تسجيل الدفعة" : "تحصيل سريع";
  const busy = submitting || downloadingPdf;

  return (
    <div
      className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <div
        className="bg-white rounded-2xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div
            className="w-9 h-9 rounded-lg flex items-center justify-center"
            style={{ background: GREEN + "15" }}
          >
            <CreditCard className="w-4.5 h-4.5" style={{ color: GREEN }} />
          </div>
          <h3
            className="flex-1 font-extrabold text-[15px]"
            style={{ color: NAVY }}
          >
            {title}
          </h3>
          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition"
            aria-label="إغلاق"
          >
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={onSubmit} className="p-5 space-y-3">
          {/* Cashier session warning */}
          {!hasOpenSession && (
            <div
              className="flex items-start gap-2 p-3 rounded-xl"
              style={{
                background: "#fff7ed",
                border: "1px solid #fdba74",
              }}
            >
              <AlertTriangle
                className="w-4 h-4 flex-shrink-0 mt-0.5"
                style={{ color: "#f5922e" }}
              />
              <div className="text-[11px] font-medium" style={{ color: NAVY }}>
                {sessionLoading ? (
                  <span>جارٍ التحقق من الوردية...</span>
                ) : (
                  <>
                    <div className="font-bold mb-0.5">لا توجد وردية مفتوحة</div>
                    <span style={{ color: "#64748b" }}>
                      يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل
                      أي مدفوعات.
                    </span>
                  </>
                )}
              </div>
            </div>
          )}

          {/* Amount */}
          <div>
            <label
              className="text-xs font-semibold block mb-1"
              style={{ color: NAVY }}
            >
              المبلغ *
            </label>
            <input
              type="number"
              step="0.01"
              min="0"
              dir="ltr"
              placeholder="0"
              className={inputCls(!!errors.amount)}
              {...register("amount")}
            />
            {errors.amount && (
              <p className="text-[10px] text-red-500 mt-1 font-medium">
                {errors.amount.message}
              </p>
            )}
          </div>

          {/* Payment method */}
          <div>
            <label
              className="text-xs font-semibold block mb-1"
              style={{ color: NAVY }}
            >
              طريقة الدفع
            </label>
            <select
              className={inputCls(!!errors.paymentMethod)}
              {...register("paymentMethod")}
            >
              {PAYMENT_METHODS.map((m) => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
            {errors.paymentMethod && (
              <p className="text-[10px] text-red-500 mt-1 font-medium">
                {errors.paymentMethod.message}
              </p>
            )}
          </div>

          {/* Full-mode only: invoice + contract linking */}
          {isFull && (
            <>
              <div>
                <label
                  className="text-xs font-semibold block mb-1"
                  style={{ color: NAVY }}
                >
                  فاتورة مرتبطة
                </label>
                <select
                  className={inputCls()}
                  {...register("invoiceId")}
                  onChange={(e) => {
                    setValue("invoiceId", e.target.value, {
                      shouldValidate: false,
                    });
                    if (e.target.value) setValue("contractId", "");
                  }}
                >
                  <option value="">— بدون فاتورة —</option>
                  {invoices.map((i) => (
                    <option key={i.id} value={i.id}>
                      {i.invoiceNumber} ({fmtRial(i.balance)})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label
                  className="text-xs font-semibold block mb-1"
                  style={{ color: NAVY }}
                >
                  عقد مرتبط
                </label>
                <select
                  className={inputCls()}
                  {...register("contractId")}
                  onChange={(e) => {
                    setValue("contractId", e.target.value, {
                      shouldValidate: false,
                    });
                    if (e.target.value) setValue("invoiceId", "");
                  }}
                >
                  <option value="">— بدون عقد —</option>
                  {contracts.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.specialty ? `${c.specialty} — ` : ""}
                      {fmtRial(c.remainingAmount ?? c.totalAmount)}
                    </option>
                  ))}
                </select>
              </div>

              {/* Service description */}
              <div>
                <label
                  className="text-xs font-semibold block mb-1"
                  style={{ color: NAVY }}
                >
                  وصف الخدمة
                </label>
                <input
                  type="text"
                  placeholder="مثال: كشف + فحص أشعة"
                  className={inputCls()}
                  {...register("serviceDescription")}
                />
              </div>

              {/* Notes */}
              <div>
                <label
                  className="text-xs font-semibold block mb-1"
                  style={{ color: NAVY }}
                >
                  ملاحظات
                </label>
                <textarea
                  rows={2}
                  placeholder="اختياري"
                  className={`${inputCls()} resize-none`}
                  {...register("notes")}
                />
              </div>

              {/* QA-594: unlinked-payment warning */}
              {isUnlinkedPayment && (
                <div
                  className="flex items-start gap-2 p-2.5 rounded-xl text-[11px] leading-relaxed"
                  style={{
                    background: "#fef3c7",
                    border: "1px solid #f59e0b",
                    color: "#78350f",
                  }}
                >
                  <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>
                    <strong>تنبيه:</strong> هذه الدفعة بدون ربط بفاتورة أو عقد.
                    سيُسجَّل المبلغ في الخزينة، لكن لن يُخصم من أي دين متتبَّع —
                    قد يظهر رصيد المريض غير صحيح. يُنصح بإنشاء فاتورة للجلسة أولاً
                    ثم ربط الدفعة بها.
                  </span>
                </div>
              )}
            </>
          )}

          {/* Actions */}
          <div className="flex gap-2 mt-5">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 py-2.5 rounded-xl text-sm font-bold"
              style={{ background: "#f1f5f9", color: "#64748b" }}
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={busy || !hasOpenSession}
              className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2 disabled:cursor-not-allowed"
              style={{
                background: GREEN,
                opacity: busy || !hasOpenSession ? 0.5 : 1,
              }}
            >
              {busy ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <CreditCard className="w-4 h-4" />
              )}
              {submitting
                ? "جارٍ التسجيل..."
                : downloadingPdf
                  ? "جارٍ تحميل السند..."
                  : submitLabel}
            </button>
          </div>

          {/* Hint: receipt auto-downloads on success */}
          <div
            className="flex items-center gap-1.5 text-[10px] pt-1"
            style={{ color: BLUE }}
          >
            <FileText className="w-3 h-3" />
            <span>سيتم تحميل سند القبض (PDF) تلقائياً بعد التسجيل.</span>
          </div>
        </form>
      </div>
    </div>
  );
}

export default PaymentModal;
