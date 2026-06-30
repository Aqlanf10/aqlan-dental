/**
 * QuickPaymentModal — quick payment collection with PDF receipt download.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { useState } from "react";
import { CreditCard, Loader2, Printer, FileText, Stethoscope } from "lucide-react";
import {
  PAYMENT_METHODS, inputCls, fmtRial,
} from "../../_lib/constants";
import type { TodayJourneyItem } from "../../_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";
import { usePaymentMethodSettings } from "../../_lib/hooks";
import { toast } from "@/stores/toastStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { ModalShell } from "./ModalShell";

export function QuickPaymentModal({
  open, onClose, item, summary, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  isPending: boolean;
  onConfirm: (amount: number, method: string, desc: string, notes: string, referenceNumber?: string, currency?: string, accountCurrency?: string, exchangeRateToAccountCurrency?: number) => void;
}) {
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState("cash");
  const [currency, setCurrency] = useState("YER");
  const [accountCurrency, setAccountCurrency] = useState("YER");
  const [exchangeRate, setExchangeRate] = useState("");
  const [desc, setDesc] = useState("");
  const [notes, setNotes] = useState("");
  const [voucherType, setVoucherType] = useState<"consultation" | "procedure">("consultation");
  const [downloadingPdf, setDownloadingPdf] = useState(false);
  const [referenceNumber, setReferenceNumber] = useState("");
  const [referenceError, setReferenceError] = useState("");

  // Dynamic payment methods from API
  const { data: paymentMethodSettings = [] } = usePaymentMethodSettings();
  const activePaymentMethods = paymentMethodSettings.filter(m => m.isActive);
  const selectedMethodSetting = activePaymentMethods.find(m => m.code.toLowerCase() === method.toLowerCase());
  const requiresRef = selectedMethodSetting?.requiresReferenceNumber ?? false;

  const outstanding = summary?.financeSummary?.outstandingBalance ?? 0;
  const overdue = summary?.financeSummary?.overdueAmount ?? 0;
  const totalPaid = summary?.financeSummary?.totalPaid;
  const latestPayment = summary?.financeSummary?.latestPayment;

  const handleSubmit = () => {
    const num = parseFloat(amount);
    if (!num || num <= 0) return;
    // Validate reference number if required
    if (requiresRef && !referenceNumber.trim()) {
      setReferenceError("الرقم المرجعي مطلوب لطريقة الدفع هذه");
      return;
    }
    setReferenceError("");
    const voucherPrefix = voucherType === "consultation" ? "[سند معاينة] " : "[إجراءات شغل/خدمة مقدمة] ";
    onConfirm(
      num,
      method,
      desc ? voucherPrefix + desc : voucherPrefix.trim(),
      notes,
      requiresRef ? referenceNumber.trim() : undefined,
      currency,
      accountCurrency,
      exchangeRate ? Number(exchangeRate) : undefined,
    );
    setAmount(""); setMethod("cash"); setCurrency("YER"); setAccountCurrency("YER"); setExchangeRate(""); setDesc(""); setNotes(""); setVoucherType("consultation");
    setReferenceNumber(""); setReferenceError("");
  };

  const handleDownloadReceipt = async () => {
    if (!latestPayment?.id) return;
    setDownloadingPdf(true);
    try {
      const filename = latestPayment.receiptNumber
        ? `receipt-${latestPayment.receiptNumber}.pdf`
        : `receipt-${latestPayment.id}.pdf`;
      await downloadPdfFromApi(`/api/payments/${latestPayment.id}/pdf`, filename);
      toast.success("تم تحميل السند بنجاح");
    } catch {
      toast.error("فشل تحميل السند المالي");
    }
    setDownloadingPdf(false);
  };

  return (
    <ModalShell open={open} onClose={onClose} title="دفع سريع" icon={CreditCard} iconColor="#22c55e">
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{item?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
          الطبيب: {item?.doctorName} — الخدمة: {item?.serviceName ?? "—"}
        </div>
      </div>

      {/* Finance summary */}
      {summary?.financeSummary && (
        <div className="grid grid-cols-2 gap-2 mb-4">
          <div className="p-2.5 rounded-lg" style={{ background: "#fff7ed" }}>
            <div className="text-[11px] font-medium" style={{ color: "#f5922e" }}>المستحق</div>
            <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(outstanding)}</div>
          </div>
          <div className="p-2.5 rounded-lg" style={{ background: overdue > 0 ? "#fef2f2" : "#f0fdf4" }}>
            <div className="text-[11px] font-medium" style={{ color: overdue > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
            <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(overdue)}</div>
          </div>
          {totalPaid != null && (
            <div className="p-2.5 rounded-lg" style={{ background: "#f0fdf4" }}>
              <div className="text-[11px] font-medium" style={{ color: "#16a34a" }}>المدفوع</div>
              <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(totalPaid)}</div>
            </div>
          )}
          {latestPayment && (
            <div className="p-2.5 rounded-lg" style={{ background: "#f5f5f5" }}>
              <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>آخر دفعة</div>
              <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(latestPayment.amount)}</div>
              {latestPayment.receiptNumber && (
                <div className="text-[10px] mt-0.5" style={{ color: "#94a3b8" }}>إيصال: {latestPayment.receiptNumber}</div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Voucher Type Selector */}
      <div className="mb-4">
        <label className="text-xs font-semibold block mb-2" style={{ color: "#1a3a5c" }}>نوع السند *</label>
        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={() => setVoucherType("consultation")}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl border-2 transition-all text-center"
            style={{
              background: voucherType === "consultation" ? "#f0f5fb" : "#fff",
              borderColor: voucherType === "consultation" ? "#3d7ab5" : "#e5e7eb",
              boxShadow: voucherType === "consultation" ? "0 2px 8px rgba(61,122,181,0.15)" : "none",
            }}>
            <Stethoscope className="w-5 h-5" style={{ color: voucherType === "consultation" ? "#3d7ab5" : "#94a3b8" }} />
            <span className="text-xs font-bold" style={{ color: voucherType === "consultation" ? "#1a3a5c" : "#94a3b8" }}>سند معاينة</span>
            <span className="text-[9px] font-medium" style={{ color: "#94a3b8" }}>كشف وفحص واستشارة</span>
          </button>
          <button
            type="button"
            onClick={() => setVoucherType("procedure")}
            className="flex flex-col items-center gap-1.5 p-3 rounded-xl border-2 transition-all text-center"
            style={{
              background: voucherType === "procedure" ? "#faf5ff" : "#fff",
              borderColor: voucherType === "procedure" ? "#9333ea" : "#e5e7eb",
              boxShadow: voucherType === "procedure" ? "0 2px 8px rgba(147,51,234,0.15)" : "none",
            }}>
            <FileText className="w-5 h-5" style={{ color: voucherType === "procedure" ? "#9333ea" : "#94a3b8" }} />
            <span className="text-xs font-bold" style={{ color: voucherType === "procedure" ? "#1a3a5c" : "#94a3b8" }}>إجراءات شغل / خدمة مقدمة</span>
            <span className="text-[9px] font-medium" style={{ color: "#94a3b8" }}>علاج وتقويم وجراحة</span>
          </button>
        </div>
      </div>

      {/* Form */}
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>المبلغ *</label>
          <div className="flex gap-2">
            <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
              placeholder="0" className={inputCls()} min={0} step={0.01} dir="ltr" />
            {outstanding > 0 && (
              <button onClick={() => setAmount(String(outstanding))}
                className="px-3 rounded-lg text-xs font-semibold whitespace-nowrap"
                style={{ background: "#f5922e15", color: "#f5922e", border: "1px solid #f5922e30" }}>
                الكل ({fmtRial(outstanding)})
              </button>
            )}
          </div>
        </div>
        <div className="grid grid-cols-3 gap-2">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>عملة الدفع</label>
            <select value={currency} onChange={e => setCurrency(e.target.value)} className={inputCls()}>
              <option value="YER">يمني</option>
              <option value="SAR">سعودي</option>
              <option value="USD">دولار</option>
            </select>
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>عملة الحساب</label>
            <select value={accountCurrency} onChange={e => setAccountCurrency(e.target.value)} className={inputCls()}>
              <option value="YER">يمني</option>
              <option value="SAR">سعودي</option>
              <option value="USD">دولار</option>
            </select>
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الصرف</label>
            <input type="number" value={exchangeRate} onChange={e => setExchangeRate(e.target.value)} placeholder="تلقائي" min="0" step="0.000001" dir="ltr" className={inputCls()} />
          </div>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>طريقة الدفع</label>
          <select value={method} onChange={e => { setMethod(e.target.value); setReferenceError(""); }} className={inputCls()}>
            {activePaymentMethods.length > 0 ? (
              activePaymentMethods.map(m => <option key={m.id} value={m.code}>{m.name}</option>)
            ) : (
              PAYMENT_METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)
            )}
          </select>
        </div>
        {/* Reference Number (shown when required) */}
        {requiresRef && (
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الرقم المرجعي <span className="text-red-500">*</span></label>
            <input value={referenceNumber} onChange={e => { setReferenceNumber(e.target.value); setReferenceError(""); }}
              placeholder="أدخل الرقم المرجعي" className={inputCls(!!referenceError)} dir="ltr" />
            {referenceError && <p className="text-[10px] text-red-500 mt-1">{referenceError}</p>}
          </div>
        )}
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>وصف الخدمة</label>
          <input value={desc} onChange={e => setDesc(e.target.value)} placeholder={voucherType === "consultation" ? "مثال: كشف + فحص أشعة" : "مثال: حشوة تجميلية + تنظيف"} className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
          <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
        </div>
      </div>

      {/* Actions */}
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!amount || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#22c55a", opacity: !amount || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
          تسجيل الدفع
        </button>
        {latestPayment?.id && (
          <button onClick={handleDownloadReceipt} disabled={downloadingPdf} title="تحميل إيصال PDF"
            className="w-10 py-2.5 rounded-xl flex items-center justify-center"
            style={{ background: "#3d7ab515", color: "#3d7ab5", border: "1px solid #3d7ab530" }}>
            {downloadingPdf ? <Loader2 className="w-4 h-4 animate-spin" /> : <Printer className="w-4 h-4" />}
          </button>
        )}
      </div>
    </ModalShell>
  );
}
