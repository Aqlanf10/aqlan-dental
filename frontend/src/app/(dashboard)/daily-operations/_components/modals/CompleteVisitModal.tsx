/**
 * CompleteVisitModal — doctor handoff / reception checkout.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { useState, useEffect } from "react";
import {
  CreditCard, CheckCircle, Loader2, Send, Printer,
  AlertCircle, Wallet, FileText,
} from "lucide-react";
import { inputCls, fmtRial, ORANGE } from "../../_lib/constants";
import type { TodayJourneyItem } from "../../_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";
import { toast } from "@/stores/toastStore";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { ModalShell } from "./ModalShell";

export function CompleteVisitModal({
  open, onClose, item, summary, isPending, onConfirm, onCheckout,
  onQuickPayment, onCreateDraftInvoice, createDraftInvoicePending,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  isPending: boolean;
  onConfirm: (data: {
    serviceDesc: string; amountDue: number; isPaid: boolean;
    needsFollowUp: boolean; nextDate: string; notes: string;
    diagnosis: string; instructions: string;
    proposedProcedure?: string;
  }) => void;
  onCheckout: (data: { paymentAmount: number; paymentMethod: string; notes: string; nextDate?: string; nextServiceId?: string }) => void;
  onQuickPayment?: (item: TodayJourneyItem) => void;
  onCreateDraftInvoice?: (item: TodayJourneyItem) => void;
  createDraftInvoicePending?: boolean;
}) {
  const [serviceDesc, setServiceDesc] = useState("");
  const [diagnosis, setDiagnosis] = useState("");
  const [instructions, setInstructions] = useState("");
  const [amountDue, setAmountDue] = useState("");
  const [, setPaymentMethod] = useState("cash");
  const [isPaid, setIsPaid] = useState(false);
  const [needsFollowUp, setNeedsFollowUp] = useState(false);
  const [nextDate, setNextDate] = useState("");
  const [notes, setNotes] = useState("");
  const [proposedProcedure, setProposedProcedure] = useState("");

  useEffect(() => {
    if (item) {
      setProposedProcedure(item.proposedProcedure ?? "");
    }
  }, [item]);

  const [downloadingPdf, setDownloadingPdf] = useState(false);
  const latestPayment = summary?.financeSummary?.latestPayment;

  // Handler to download the latest PDF receipt inside the checkout modal
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

  // Determine mode: reception checkout vs doctor handoff
  const isReceptionMode = item?.checkoutStatus === "ReadyForCheckout" || item?.nextAction === "Checkout";

  const handleSubmit = () => {
    const num = parseFloat(amountDue) || 0;
    if (isReceptionMode) {
      // Reception mode: only checkout, no payment fields (payment handled separately)
      onCheckout({
        paymentAmount: 0,
        paymentMethod: "",
        notes,
        nextDate: needsFollowUp ? nextDate : undefined,
      });
    } else {
      // Doctor mode: handoff with treatment details
      onConfirm({
        serviceDesc, amountDue: num, isPaid, needsFollowUp, nextDate, notes,
        diagnosis, instructions, proposedProcedure,
      });
    }
    setServiceDesc(""); setDiagnosis(""); setInstructions("");
    setAmountDue(""); setPaymentMethod("cash"); setIsPaid(false);
    setNeedsFollowUp(false); setNextDate(""); setNotes(""); setProposedProcedure("");
  };

  const outstanding = summary?.financeSummary?.outstandingBalance ?? 0;
  const isPaidFully = outstanding === 0;

  // Modal title and icon change based on mode
  const modalTitle = isReceptionMode ? "التحصيل والخروج (الاستقبال)" : "تسليم للاستقبال";
  const ModalIcon = isReceptionMode ? CreditCard : Send;
  const modalIconColor = isReceptionMode ? "#16a34a" : ORANGE;

  // Compute Invoice Status Badge details
  let invoiceBadgeColor = "#64748b";
  let invoiceBadgeBg = "#f1f5f9";
  let invoiceStatusLabel = "لا توجد فواتير معلقة";
  if (summary?.unpaidInvoicesCount && summary.unpaidInvoicesCount > 0) {
    invoiceBadgeColor = "#dc2626";
    invoiceBadgeBg = "#fef2f2";
    invoiceStatusLabel = `توجد فواتير معلقة (${summary.unpaidInvoicesCount})`;
  }

  return (
    <ModalShell open={open} onClose={onClose} title={modalTitle} icon={ModalIcon} iconColor={modalIconColor} wide>
      {/* Basic Info Container */}
      <div className="mb-4 p-3.5 rounded-xl grid grid-cols-1 sm:grid-cols-2 gap-2" style={{ background: "#f0f5fb" }}>
        <div>
          <div className="text-[10px] text-gray-400 font-medium">اسم المريض</div>
          <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{item?.patientName}</div>
        </div>
        <div>
          <div className="text-[10px] text-gray-400 font-medium">الطبيب المعالج</div>
          <div className="font-semibold text-xs text-gray-700">{item?.doctorName}</div>
        </div>
        {item?.serviceName && (
          <div className="mt-1">
            <div className="text-[10px] text-gray-400 font-medium">الخدمة المطلوبة</div>
            <div className="text-xs font-semibold text-gray-600">{item.serviceName}</div>
          </div>
        )}
        {item?.roomName && (
          <div className="mt-1">
            <div className="text-[10px] text-gray-400 font-medium">غرفة العلاج</div>
            <div className="text-xs font-semibold text-gray-600">{item.roomName}</div>
          </div>
        )}
      </div>

      {/* Form — Doctor Handoff Mode */}
      {!isReceptionMode && (
        <div className="space-y-3">
          {/* Finance info */}
          {summary?.financeSummary && (
            <div className="mb-4 grid grid-cols-3 gap-2">
              <div className="p-2.5 rounded-lg" style={{ background: "#fff7ed" }}>
                <div className="text-[11px] font-medium" style={{ color: "#f5922e" }}>المتبقي</div>
                <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(outstanding)}</div>
              </div>
              <div className="p-2.5 rounded-lg" style={{ background: summary.financeSummary.overdueAmount > 0 ? "#fef2f2" : "#f0fdf4" }}>
                <div className="text-[11px] font-medium" style={{ color: summary.financeSummary.overdueAmount > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
                <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(summary.financeSummary.overdueAmount)}</div>
              </div>
              <div className="p-2.5 rounded-lg" style={{ background: "#f0fdf4" }}>
                <div className="text-[11px] font-medium" style={{ color: "#16a34a" }}>الحالة المالية</div>
                <div className="text-xs font-bold" style={{ color: "#1a3a5c" }}>
                  {summary.financeSummary.financialStatus === "paid_full" ? "مكتمل الدفع" :
                   summary.financeSummary.financialStatus === "has_balance" ? "عليه رصيد" :
                   summary.financeSummary.financialStatus === "overdue" ? "متأخر" : "لا خطة"}
                </div>
              </div>
            </div>
          )}

          {/* Info banner */}
          <div className="p-2.5 rounded-xl flex items-center gap-2" style={{ background: "#fff7ed", border: "1px solid #f5922e30" }}>
            <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
            <span className="text-[11px] font-medium" style={{ color: "#92400e" }}>
              سيتم إرسال المريض للاستقبال للتحصيل والخروج
            </span>
          </div>

          {/* Row 1: Service + ProposedProcedure + Diagnosis */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملخص الإجراء / الخدمة</label>
              <input value={serviceDesc} onChange={e => setServiceDesc(e.target.value)}
                placeholder="مثال: حشو + تنظيف" className={inputCls()} />
            </div>
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الإجراء المقترح للمحاسبة *</label>
              <input value={proposedProcedure} onChange={e => setProposedProcedure(e.target.value)}
                placeholder="مثال: خلع ضرس عقل علوي أيسر" className={inputCls()} />
            </div>
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>التشخيص</label>
              <input value={diagnosis} onChange={e => setDiagnosis(e.target.value)}
                placeholder="مثال: تسوس سطحي" className={inputCls()} />
            </div>
          </div>

          {/* Instructions */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>التعليمات للمريض</label>
            <input value={instructions} onChange={e => setInstructions(e.target.value)}
              placeholder="مثال: عدم أكل الأطعمة الصلبة لمدة 24 ساعة" className={inputCls()} />
          </div>

          {/* Amount Due */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>المبلغ المستحق (مرجعي)</label>
              <input type="number" value={amountDue} onChange={e => setAmountDue(e.target.value)}
                placeholder="0" className={inputCls()} min={0} step={0.01} dir="ltr" />
            </div>
            {outstanding > 0 && (
              <div className="flex items-center gap-2 px-3 py-2 rounded-lg" style={{ background: "#fff7ed" }}>
                <Wallet className="w-4 h-4" style={{ color: ORANGE }} />
                <span className="text-xs font-bold" style={{ color: "#92400e" }}>رصيد سابق: {fmtRial(outstanding)}</span>
              </div>
            )}
          </div>

          {/* Follow-up */}
          <div className="flex items-center gap-4 flex-wrap">
            <div className="flex items-center gap-2">
              <input type="checkbox" id="needsFollowUp" checked={needsFollowUp} onChange={e => setNeedsFollowUp(e.target.checked)}
                className="w-4 h-4 rounded border-gray-300 accent-[#3d7ab5]" />
              <label htmlFor="needsFollowUp" className="text-sm font-medium" style={{ color: "#1a3a5c" }}>يحتاج موعد متابعة</label>
            </div>
          </div>
          {needsFollowUp && (
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>تاريخ الموعد القادم</label>
              <input type="date" value={nextDate} onChange={e => setNextDate(e.target.value)} className={inputCls()} />
            </div>
          )}

          {/* Notes */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={2}
              placeholder="ملاحظة للاستقبال" className={inputCls()} />
          </div>
        </div>
      )}

      {/* Form — Reception Operational Checkout Panel */}
      {isReceptionMode && (
        <div className="space-y-4">
          {/* 1. Patient State Overview */}
          <div className="p-3.5 rounded-xl border border-gray-100 grid grid-cols-2 gap-4 bg-gray-50/50">
            <div>
              <div className="text-[10px] text-gray-400 font-semibold mb-1">المستحق الحالي لهذه الزيارة</div>
              <div className="text-base font-extrabold flex items-center gap-1.5" style={{ color: "#2563eb" }}>
                <Wallet className="w-4 h-4" />
                {summary?.todayVisit?.amountDueReference ? fmtRial(summary.todayVisit.amountDueReference) : "لم يحدد بعد"}
              </div>
            </div>
            <div>
              <div className="text-[10px] text-gray-400 font-semibold mb-1">الرصيد المتبقي الإجمالي</div>
              <div className="text-base font-extrabold flex items-center gap-1.5" style={{ color: isPaidFully ? "#16a34a" : "#ea580c" }}>
                <CreditCard className="w-4 h-4" />
                {fmtRial(outstanding)}
              </div>
            </div>
            <div className="mt-1">
              <div className="text-[10px] text-gray-400 font-semibold mb-1">حالة الفاتورة</div>
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold"
                style={{ color: invoiceBadgeColor, background: invoiceBadgeBg }}>
                {invoiceStatusLabel}
              </span>
            </div>
            <div className="mt-1">
              <div className="text-[10px] text-gray-400 font-semibold mb-1">حالة السداد</div>
              <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold"
                style={{ color: isPaidFully ? "#16a34a" : "#f5922e", background: isPaidFully ? "#f0fdf4" : "#fff7ed" }}>
                {isPaidFully ? "مسدد بالكامل" : "متبقي دفعات"}
              </span>
            </div>
          </div>

          {/* 2. Latest Receipt summary (if available) */}
          {latestPayment && (
            <div className="p-2.5 rounded-lg border border-dashed flex items-center justify-between" style={{ borderColor: "#bfdbfe", background: "#f0f7ff" }}>
              <div className="text-xs" style={{ color: "#1e40af" }}>
                <CheckCircle className="w-3.5 h-3.5 inline ml-1.5 text-blue-500" />
                آخر دفعة محصلة: <span className="font-bold">{fmtRial(latestPayment.amount)}</span>
                {latestPayment.receiptNumber && ` (إيصال: ${latestPayment.receiptNumber})`}
              </div>
              <button
                onClick={handleDownloadReceipt}
                disabled={downloadingPdf}
                className="px-2.5 py-1 rounded text-[10px] font-bold transition-all hover:bg-blue-100 flex items-center gap-1 text-blue-700 bg-blue-50"
              >
                {downloadingPdf ? <Loader2 className="w-3 h-3 animate-spin" /> : <Printer className="w-3 h-3" />}
                طباعة الإيصال
              </button>
            </div>
          )}

          {/* 3. Operational Quick Actions */}
          <div>
            <div className="text-[11px] font-bold text-gray-400 mb-2">إجراءات تحصيل سريعة</div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
              {/* Draft Invoice Action */}
              <button
                type="button"
                onClick={() => { if (item) onCreateDraftInvoice?.(item); }}
                disabled={createDraftInvoicePending || !item?.visitId}
                className="flex items-center justify-center gap-2 p-3 rounded-xl border border-gray-200 transition hover:bg-gray-50 text-right disabled:opacity-40"
              >
                {createDraftInvoicePending ? <Loader2 className="w-4 h-4 animate-spin text-gray-400" /> : <FileText className="w-4 h-4 text-blue-500" />}
                <div className="text-right">
                  <div className="text-xs font-bold text-gray-700">إنشاء فاتورة مسودة</div>
                  <div className="text-[9px] text-gray-400 mt-0.5">توليد مسودة كشف حساب للاستقبال</div>
                </div>
              </button>

              {/* Quick Payment Action */}
              <button
                type="button"
                onClick={() => { if (item) { onQuickPayment?.(item); onClose(); } }}
                disabled={!item}
                className="flex items-center justify-center gap-2 p-3 rounded-xl border border-gray-200 transition hover:bg-gray-50 text-right"
              >
                <CreditCard className="w-4 h-4 text-emerald-500" />
                <div className="text-right">
                  <div className="text-xs font-bold text-gray-700">تسجيل دفعة (سند قبض)</div>
                  <div className="text-[9px] text-gray-400 mt-0.5">تحصيل نقدي أو بطاقة إصدار إيصال</div>
                </div>
              </button>
            </div>
          </div>

          {/* Follow-up & Final Notes */}
          <div className="pt-2 border-t" style={{ borderColor: "#f1f5f9" }}>
            <div className="flex items-center gap-2 mb-3">
              <input type="checkbox" id="checkoutFollowUp" checked={needsFollowUp} onChange={e => setNeedsFollowUp(e.target.checked)}
                className="w-4 h-4 rounded border-gray-300 accent-[#16a34a]" />
              <label htmlFor="checkoutFollowUp" className="text-sm font-medium text-gray-700">حجز موعد متابعة قادمة لهذا المريض</label>
            </div>
            {needsFollowUp && (
              <div className="mb-3">
                <label className="text-xs font-semibold block mb-1 text-gray-500">تاريخ الموعد القادم</label>
                <input type="date" value={nextDate} onChange={e => setNextDate(e.target.value)} className={inputCls()} />
              </div>
            )}
            <div>
              <label className="text-xs font-semibold block mb-1 text-gray-500">ملاحظات تسريح المريض</label>
              <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={2}
                placeholder="أضف أي ملاحظات ختامية للزيارة..." className={inputCls()} />
            </div>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: isReceptionMode ? "#10b981" : ORANGE, opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> :
           isReceptionMode ? <CheckCircle className="w-4 h-4" /> : <Send className="w-4 h-4" />}
          {isReceptionMode ? "تأكيد إنهاء الزيارة والخروج" : "تسليم للاستقبال"}
        </button>
      </div>
    </ModalShell>
  );
}
