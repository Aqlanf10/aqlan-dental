/**
 * All modal components for Daily Operations page.
 * QuickPayment, CompleteVisit, BookAppointment, ConfirmDialog, WhatsAppMenu,
 * ChangeRoomModal, WalkInModal, UndoToast, PatientSidePanel, KeyboardShortcutsHelp.
 */

"use client";

import { useState, useEffect, useRef } from "react";
import {
  X, CreditCard, CheckCircle, CalendarPlus, AlertTriangle,
  MessageCircle, Loader2, Send, Printer, Building2,
  UserPlus, Undo2, Keyboard, Clock, ChevronLeft,
  AlertCircle, Wallet, FileText, Stethoscope,
  Search, User,
} from "lucide-react";
import {
  PAYMENT_METHODS, APPOINTMENT_TYPES, inputCls, fmtRial,
  WHATSAPP_TEMPLATES, normalizePhone, fmtDate, fmtTime,
  fmtWaitMinutes, NAVY, BLUE, ORANGE, KEYBOARD_SHORTCUTS,
  type UndoAction,
} from "../_lib/constants";
import type { TodayJourneyItem, DoctorOption, ServiceOption, RoomOption, BranchOption } from "../_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";
import { usePaymentMethodSettings } from "../_lib/hooks";
import api from "@/lib/api";
import axios from "axios";

/* ═══════════════════════════════════════════════════════════════════════════
   Shared overlay wrapper
   ═══════════════════════════════════════════════════════════════════════════ */
function ModalShell({ open, onClose, title, icon: Icon, iconColor, children, wide }: {
  open: boolean; onClose: () => void; title: string;
  icon?: React.ElementType; iconColor?: string; children: React.ReactNode; wide?: boolean;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div
        className={`bg-white rounded-2xl shadow-xl ${wide ? "w-full max-w-2xl" : "w-full max-w-md"} max-h-[90vh] overflow-y-auto`}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          {Icon && (
            <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: (iconColor ?? "#3d7ab5") + "15" }}>
              <Icon className="w-4.5 h-4.5" style={{ color: iconColor ?? "#3d7ab5" }} />
            </div>
          )}
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>{title}</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        {/* Body */}
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Quick Payment Modal — with PDF receipt download
   ═══════════════════════════════════════════════════════════════════════════ */
export function QuickPaymentModal({
  open, onClose, item, summary, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  isPending: boolean;
  onConfirm: (amount: number, method: string, desc: string, notes: string, referenceNumber?: string) => void;
}) {
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState("Cash");
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
    onConfirm(num, method, desc ? voucherPrefix + desc : voucherPrefix.trim(), notes, requiresRef ? referenceNumber.trim() : undefined);
    setAmount(""); setMethod("Cash"); setDesc(""); setNotes(""); setVoucherType("consultation");
    setReferenceNumber(""); setReferenceError("");
  };

  const handleDownloadReceipt = async () => {
    if (!latestPayment?.id) return;
    setDownloadingPdf(true);
    try {
      const { data } = await api.get(`/api/payments/${latestPayment.id}/pdf`, {
        responseType: "blob",
      });
      const url = window.URL.createObjectURL(new Blob([data], { type: "application/pdf" }));
      const link = document.createElement("a");
      link.href = url;
      link.download = `receipt-${latestPayment.receiptNumber || latestPayment.id}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    } catch {
      // Fallback to print
      window.print();
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

/* ═══════════════════════════════════════════════════════════════════════════
   Complete Visit Modal — Enhanced with diagnosis, instructions, follow-up
   ═══════════════════════════════════════════════════════════════════════════ */
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
  const [, setPaymentMethod] = useState("Cash");
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
      const { data } = await api.get(`/api/payments/${latestPayment.id}/pdf`, {
        responseType: "blob",
      });
      const url = window.URL.createObjectURL(new Blob([data], { type: "application/pdf" }));
      const link = document.createElement("a");
      link.href = url;
      link.download = `receipt-${latestPayment.receiptNumber || latestPayment.id}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
    } catch {
      window.print();
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
    setAmountDue(""); setPaymentMethod("Cash"); setIsPaid(false);
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

/* ═══════════════════════════════════════════════════════════════════════════
   Book Appointment Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function BookAppointmentModal({
  open, onClose, item, doctors, services, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  doctors: DoctorOption[];
  services: ServiceOption[];
  isPending: boolean;
  onConfirm: (data: {
    doctorId: string; date: string; startTime: string; endTime: string;
    serviceId: string; type: string; notes: string;
  }) => void;
}) {
  const [doctorId, setDoctorId] = useState(item?.doctorId ?? "");
  const [date, setDate] = useState("");
  const [startTime, setStartTime] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [type, setType] = useState("FollowUp");
  const [notes, setNotes] = useState("");

  const handleSubmit = () => {
    if (!doctorId || !date || !startTime) return;
    const start = startTime;
    const [h, m] = start.split(":").map(Number);
    const endH = h + Math.floor((m + 30) / 60);
    const endM = (m + 30) % 60;
    const endTime = `${String(endH).padStart(2, "0")}:${String(endM).padStart(2, "0")}`;
    onConfirm({ doctorId, date, startTime: start, endTime, serviceId, type, notes });
  };

  return (
    <ModalShell open={open} onClose={onClose} title="حجز موعد متابعة" icon={CalendarPlus} iconColor="#3d7ab5">
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{item?.patientName}</div>
      </div>

      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الطبيب *</label>
          <select value={doctorId} onChange={e => setDoctorId(e.target.value)} className={inputCls()}>
            <option value="">اختر الطبيب</option>
            {doctors.map(d => <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` (${d.specialty})` : ""}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>التاريخ *</label>
            <input type="date" value={date} onChange={e => setDate(e.target.value)} className={inputCls()} />
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الوقت *</label>
            <input type="time" value={startTime} onChange={e => setStartTime(e.target.value)} className={inputCls()} dir="ltr" />
          </div>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الخدمة</label>
          <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
            <option value="">اختر الخدمة</option>
            {services.map(s => <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${s.defaultPrice} ر.ي` : ""}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>نوع الموعد</label>
          <select value={type} onChange={e => setType(e.target.value)} className={inputCls()}>
            {APPOINTMENT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
          <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
        </div>
      </div>

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!doctorId || !date || !startTime || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#3d7ab5", opacity: !doctorId || !date || !startTime || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CalendarPlus className="w-4 h-4" />}
          حجز الموعد
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Walk-In Patient Modal — Quick registration + auto-intake + queue
   ═══════════════════════════════════════════════════════════════════════════ */
export function WalkInModal({
  open, onClose, doctors, services, branches, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  doctors: DoctorOption[];
  services: ServiceOption[];
  branches: BranchOption[];
  isPending: boolean;
  onConfirm: (data: {
    patientName: string; patientPhone: string; doctorId: string;
    serviceId: string; branchId: string; notes: string;
  }) => void;
}) {
  const [patientName, setPatientName] = useState("");
  const [patientPhone, setPatientPhone] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [branchId, setBranchId] = useState("");
  const [notes, setNotes] = useState("");

  const handleSubmit = () => {
    if (!patientName.trim() || !doctorId) return;
    onConfirm({
      patientName: patientName.trim(),
      patientPhone: patientPhone.trim(),
      doctorId,
      serviceId,
      branchId,
      notes: notes.trim(),
    });
    setPatientName(""); setPatientPhone(""); setDoctorId("");
    setServiceId(""); setBranchId(""); setNotes("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="مريض مشي (Walk-in)" icon={UserPlus} iconColor={ORANGE}>
      <div className="mb-3 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#fff7ed" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
        <span className="text-xs font-medium" style={{ color: "#92400e" }}>
          سيتم إنشاء مريض + موعد + تسجيل وصول + إضافة لقائمة الانتظار تلقائياً
        </span>
      </div>

      <div className="space-y-3">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>اسم المريض *</label>
            <input value={patientName} onChange={e => setPatientName(e.target.value)}
              placeholder="الاسم الكامل" className={inputCls()} />
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>رقم الهاتف</label>
            <input value={patientPhone} onChange={e => setPatientPhone(e.target.value)}
              placeholder="7XXXXXXXX" className={inputCls()} dir="ltr" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الطبيب *</label>
            <select value={doctorId} onChange={e => setDoctorId(e.target.value)} className={inputCls()}>
              <option value="">اختر الطبيب</option>
              {doctors.map(d => <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` (${d.specialty})` : ""}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الخدمة</label>
            <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
              <option value="">اختر الخدمة</option>
              {services.map(s => <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${s.defaultPrice} ر.ي` : ""}</option>)}
            </select>
          </div>
        </div>
        {branches.length > 1 && (
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الفرع</label>
            <select value={branchId} onChange={e => setBranchId(e.target.value)} className={inputCls()}>
              <option value="">الفرع الرئيسي</option>
              {branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          </div>
        )}
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
          <input value={notes} onChange={e => setNotes(e.target.value)}
            placeholder="الشكوى الرئيسية..." className={inputCls()} />
        </div>
      </div>

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!patientName.trim() || !doctorId || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: !patientName.trim() || !doctorId || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
          تسجيل وإضافة لقائمة الانتظار
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Confirm Dialog
   ═══════════════════════════════════════════════════════════════════════════ */
export function ConfirmDialog({
  open, onClose, type, patientName, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  type: "Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete";
  patientName: string;
  isPending: boolean;
  onConfirm: () => void;
}) {
  const configs: Record<string, { title: string; body: string; btnColor: string }> = {
    Cancel:      { title: "إلغاء الموعد",     body: `هل أنت متأكد من إلغاء موعد المريض ${patientName}؟`, btnColor: "#ef4444" },
    NoShow:      { title: "لم يحضر",          body: `هل تريد تسجيل المريض ${patientName} كـ "لم يحضر"؟`, btnColor: "#f5922e" },
    CancelQueue: { title: "إلغاء من الانتظار", body: `هل أنت متأكد من إلغاء المريض ${patientName} من قائمة الانتظار؟`, btnColor: "#ef4444" },
    ChangeRoom:  { title: "تغيير الغرفة",     body: `هل تريد تغيير غرفة المريض ${patientName}؟`, btnColor: "#3d7ab5" },
    Complete:    { title: "إنهاء الزيارة",     body: `هل تريد إنهاء زيارة المريض ${patientName}؟`, btnColor: "#16a34a" },
  };

  const cfg = configs[type] ?? configs.Cancel;

  return (
    <ModalShell open={open} onClose={onClose} title={cfg.title} icon={AlertTriangle} iconColor={cfg.btnColor}>
      <p className="text-sm leading-relaxed" style={{ color: "#475569" }}>{cfg.body}</p>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>تراجع</button>
        <button onClick={onConfirm} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: cfg.btnColor, opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
          تأكيد
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Change Room Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function ChangeRoomModal({
  open, onClose, rooms, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  rooms: RoomOption[];
  isPending: boolean;
  onConfirm: (roomName: string) => void;
}) {
  const [roomId, setRoomId] = useState("");

  const handleSubmit = () => {
    if (!roomId) return;
    const selectedRoom = rooms.find(r => r.id === roomId);
    onConfirm(selectedRoom?.arabicName ?? roomId);
    setRoomId("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="تغيير الغرفة" icon={Building2} iconColor="#3d7ab5">
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الغرفة الجديدة *</label>
          <select value={roomId} onChange={e => setRoomId(e.target.value)} className={inputCls()}>
            <option value="">اختر الغرفة</option>
            {rooms.map(r => <option key={r.id} value={r.id}>{r.arabicName}</option>)}
          </select>
        </div>
      </div>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!roomId || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#3d7ab5", opacity: !roomId || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Building2 className="w-4 h-4" />}
          تغيير الغرفة
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   WhatsApp Menu — Opens WhatsApp Web with Arabic templates
   ═══════════════════════════════════════════════════════════════════════════ */
export function WhatsAppMenu({
  open, onClose, item, summary, clinicName,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  clinicName: string;
}) {
  if (!open || !item) return null;

  const phone = normalizePhone(item.patientPhone);
  const patientName = item.patientName;
  const doctorName = item.doctorName;
  const todayDate = new Date();
  const aptDate = fmtDate(todayDate);
  const aptTime = fmtTime(item.appointmentTime);
  const remaining = summary?.financeSummary?.outstandingBalance;

  const handleSend = (template: typeof WHATSAPP_TEMPLATES[number]) => {
    const msg = template.build({ patientName, clinicName, aptDate, aptTime, doctorName, remaining });
    // Use WhatsApp Web (web.whatsapp.com) for desktop, fallback to wa.me for mobile
    const isMobile = /Android|iPhone|iPad/i.test(navigator.userAgent);
    if (isMobile) {
      window.open(`https://wa.me/${phone}?text=${encodeURIComponent(msg)}`, "_blank");
    } else {
      window.open(`https://web.whatsapp.com/send?phone=${phone}&text=${encodeURIComponent(msg)}`, "_blank");
    }
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm" onClick={e => e.stopPropagation()}>
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: "#25D36620" }}>
            <MessageCircle className="w-4.5 h-4.5" style={{ color: "#25D366" }} />
          </div>
          <div className="flex-1">
            <h3 className="font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>واتساب</h3>
            <p className="text-xs" style={{ color: "#64748b" }}>{patientName} — {item.patientPhone ?? "—"}</p>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        <div className="p-3 space-y-1.5">
          {WHATSAPP_TEMPLATES.map(t => (
            <button key={t.key} onClick={() => handleSend(t)}
              className="w-full text-right px-4 py-3 rounded-xl text-sm font-medium flex items-center gap-3 transition hover:bg-[#25D36608]"
              style={{ color: "#1a3a5c" }}>
              <Send className="w-4 h-4 flex-shrink-0" style={{ color: "#25D366" }} />
              {t.label}
            </button>
          ))}
        </div>
        <div className="px-5 pb-4">
          <p className="text-[10px] text-center" style={{ color: "#94a3b8" }}>
            سيتم فتح واتساب ويب لإرسال الرسالة
          </p>
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Undo Toast — Appears for 5 seconds after Cancel/NoShow/CancelQueue
   ═══════════════════════════════════════════════════════════════════════════ */
export function UndoToast({
  action, onUndo, onDismiss,
}: {
  action: UndoAction;
  onUndo: () => void;
  onDismiss: () => void;
}) {
  const [secondsLeft, setSecondsLeft] = useState(5);
  const timerRef = useRef<ReturnType<typeof setInterval>>(undefined);

  useEffect(() => {
    timerRef.current = setInterval(() => {
      setSecondsLeft(prev => {
        if (prev <= 1) {
          if (timerRef.current) clearInterval(timerRef.current);
          onDismiss();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, [onDismiss]);

  const actionLabels: Record<string, string> = {
    Cancel: "إلغاء الموعد",
    NoShow: "لم يحضر",
    CancelQueue: "إلغاء من الانتظار",
  };

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-[60] flex items-center gap-3 px-5 py-3 rounded-xl shadow-xl border"
      style={{ background: "#fff", borderColor: "#e8f0f9", minWidth: 320 }}>
      <Undo2 className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
      <div className="flex-1">
        <div className="text-xs font-bold" style={{ color: NAVY }}>
          {actionLabels[action.type] ?? "إجراء"} — {action.patientName}
        </div>
        <div className="text-[10px]" style={{ color: "#94a3b8" }}>
          تراجع خلال {secondsLeft} ثانية
        </div>
      </div>
      <button onClick={onUndo}
        className="px-3 py-1.5 rounded-lg text-xs font-bold"
        style={{ background: `${ORANGE}15`, color: ORANGE, border: `1px solid ${ORANGE}30` }}>
        تراجع
      </button>
      <button onClick={onDismiss} className="p-1 rounded hover:bg-gray-100">
        <X className="w-3 h-3 text-gray-400" />
      </button>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Patient Side Panel — Slide-in summary without leaving the page
   ═══════════════════════════════════════════════════════════════════════════ */
export function PatientSidePanel({
  open, onClose, item, summary, waitTime,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  waitTime?: { estimatedMinutes: number; patientsAhead: number } | null;
}) {
  if (!open || !item) return null;

  const finance = summary?.financeSummary;
  const medicalAlerts = summary?.medicalAlerts ?? [];
  const activeContract = summary?.activeContract;
  const activeOrtho = summary?.activeOrthoCase;

  return (
    <div className="fixed inset-0 z-50 flex justify-end" onClick={onClose}>
      {/* Overlay */}
      <div className="flex-1 bg-black/20" />
      {/* Panel */}
      <div className="w-full max-w-md bg-white shadow-2xl overflow-y-auto"
        onClick={e => e.stopPropagation()} style={{ borderRight: "none" }}>
        {/* Header */}
        <div className="sticky top-0 bg-white z-10 px-5 py-4 border-b flex items-center gap-3"
          style={{ borderColor: "#e8f0f9" }}>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100">
            <ChevronLeft className="w-5 h-5" style={{ color: NAVY }} />
          </button>
          <div className="flex-1">
            <h3 className="font-extrabold text-sm" style={{ color: NAVY }}>{item.patientName}</h3>
            <p className="text-[11px]" style={{ color: "#64748b" }}>
              {item.doctorName} — {item.serviceName ?? "—"}
            </p>
          </div>
        </div>

        <div className="p-5 space-y-4">
          {/* Medical Alerts */}
          {medicalAlerts.length > 0 && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: "#ef4444" }}>
                <AlertCircle className="w-3.5 h-3.5" /> تنبيهات طبية
              </h4>
              <div className="space-y-1.5">
                {medicalAlerts.map((alert, i) => (
                  <div key={i} className="px-3 py-2 rounded-lg text-xs font-medium"
                    style={{
                      background: alert.severity === "danger" ? "#fef2f2" : alert.severity === "warning" ? "#fff7ed" : "#f0f5fb",
                      color: alert.severity === "danger" ? "#dc2626" : alert.severity === "warning" ? "#d97706" : "#3d7ab5",
                      border: `1px solid ${alert.severity === "danger" ? "#fecaca" : alert.severity === "warning" ? "#fde8d0" : "#dce8f5"}`,
                    }}>
                    {alert.label}: {alert.value}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Finance Summary */}
          {finance && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Wallet className="w-3.5 h-3.5" /> الوضع المالي
              </h4>
              <div className="grid grid-cols-2 gap-2">
                <div className="p-2.5 rounded-lg" style={{ background: "#fff7ed" }}>
                  <div className="text-[10px] font-medium" style={{ color: ORANGE }}>المستحق</div>
                  <div className="text-sm font-bold" style={{ color: NAVY }}>{fmtRial(finance.outstandingBalance)}</div>
                </div>
                <div className="p-2.5 rounded-lg" style={{ background: finance.overdueAmount > 0 ? "#fef2f2" : "#f0fdf4" }}>
                  <div className="text-[10px] font-medium" style={{ color: finance.overdueAmount > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
                  <div className="text-sm font-bold" style={{ color: NAVY }}>{fmtRial(finance.overdueAmount)}</div>
                </div>
                {finance.totalPaid != null && (
                  <div className="p-2.5 rounded-lg" style={{ background: "#f0fdf4" }}>
                    <div className="text-[10px] font-medium" style={{ color: "#16a34a" }}>المدفوع</div>
                    <div className="text-sm font-bold" style={{ color: NAVY }}>{fmtRial(finance.totalPaid)}</div>
                  </div>
                )}
                <div className="p-2.5 rounded-lg" style={{ background: "#f5f5f5" }}>
                  <div className="text-[10px] font-medium" style={{ color: "#64748b" }}>الحالة</div>
                  <div className="text-xs font-bold" style={{ color: NAVY }}>
                    {finance.financialStatus === "paid_full" ? "مكتمل" :
                     finance.financialStatus === "has_balance" ? "عليه رصيد" :
                     finance.financialStatus === "overdue" ? "متأخر" : "لا خطة"}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Active Contract */}
          {activeContract && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <FileText className="w-3.5 h-3.5" /> عقد نشط
              </h4>
              <div className="p-3 rounded-lg" style={{ background: "#f0f5fb", border: "1px solid #dce8f5" }}>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs font-bold" style={{ color: NAVY }}>
                    {activeContract.specialty ?? "عقد"} — {activeContract.status}
                  </span>
                  <span className="text-[10px] font-bold" style={{ color: BLUE }}>
                    {activeContract.installmentsCount} قسط
                  </span>
                </div>
                <div className="w-full rounded-full h-1.5 bg-gray-200 mt-1.5">
                  <div className="h-1.5 rounded-full" style={{
                    width: `${Math.min(100, (activeContract.paidAmount / activeContract.totalAmount) * 100)}%`,
                    background: BLUE,
                  }} />
                </div>
                <div className="flex justify-between mt-1.5 text-[10px]">
                  <span style={{ color: "#16a34a" }}>مدفوع: {fmtRial(activeContract.paidAmount)}</span>
                  <span style={{ color: ORANGE }}>متبقي: {fmtRial(activeContract.remainingAmount)}</span>
                </div>
              </div>
            </div>
          )}

          {/* Active Ortho Case */}
          {activeOrtho && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Stethoscope className="w-3.5 h-3.5" /> حالة تقويم
              </h4>
              <div className="p-3 rounded-lg" style={{ background: "#faf5ff", border: "1px solid #e9d5ff" }}>
                <div className="flex justify-between items-center mb-1">
                  <span className="text-xs font-bold" style={{ color: "#9333ea" }}>
                    {activeOrtho.applianceType ?? "تقويم"} — {activeOrtho.status}
                  </span>
                  <span className="text-[10px] font-bold" style={{ color: "#9333ea" }}>
                    {activeOrtho.stagePercentage ?? 0}%
                  </span>
                </div>
                <div className="w-full rounded-full h-1.5 bg-gray-200 mt-1.5">
                  <div className="h-1.5 rounded-full" style={{
                    width: `${activeOrtho.stagePercentage ?? 0}%`,
                    background: "#9333ea",
                  }} />
                </div>
              </div>
            </div>
          )}

          {/* Queue Wait Time */}
          {waitTime && waitTime.estimatedMinutes > 0 && (
            <div>
              <h4 className="text-xs font-bold mb-2 flex items-center gap-1.5" style={{ color: NAVY }}>
                <Clock className="w-3.5 h-3.5" /> وقت الانتظار المتوقع
              </h4>
              <div className="p-3 rounded-lg flex items-center gap-3"
                style={{ background: "#fff7ed", border: "1px solid #fde8d0" }}>
                <Clock className="w-5 h-5" style={{ color: ORANGE }} />
                <div>
                  <div className="text-sm font-bold" style={{ color: NAVY }}>
                    {fmtWaitMinutes(waitTime.estimatedMinutes)}
                  </div>
                  <div className="text-[10px]" style={{ color: "#94a3b8" }}>
                    {waitTime.patientsAhead} مرضى قبله
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Recent Visits */}
          {summary?.recentVisits && summary.recentVisits.length > 0 && (
            <div>
              <h4 className="text-xs font-bold mb-2" style={{ color: NAVY }}>آخر الزيارات</h4>
              <div className="space-y-1.5">
                {summary.recentVisits.slice(0, 3).map((v, i) => (
                  <div key={i} className="px-3 py-2 rounded-lg text-xs"
                    style={{ background: "#f8fafc", border: "1px solid #f1f5f9" }}>
                    <div className="font-semibold" style={{ color: NAVY }}>
                      {v.treatmentDone || v.chiefComplaint || "زيارة"}
                    </div>
                    <div className="text-[10px] mt-0.5" style={{ color: "#94a3b8" }}>
                      {fmtDate(v.visitDate)} {v.cost ? `— ${fmtRial(v.cost)}` : ""}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Keyboard Shortcuts Help — Popup showing available shortcuts
   ═══════════════════════════════════════════════════════════════════════════ */
export function KeyboardShortcutsHelp({ open, onClose }: {
  open: boolean; onClose: () => void;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm" onClick={e => e.stopPropagation()}>
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: `${BLUE}15` }}>
            <Keyboard className="w-4.5 h-4.5" style={{ color: BLUE }} />
          </div>
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: NAVY }}>اختصارات لوحة المفاتيح</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        <div className="p-4 space-y-2">
          {KEYBOARD_SHORTCUTS.map(s => (
            <div key={s.keys} className="flex items-center justify-between py-1.5">
              <span className="text-xs font-medium" style={{ color: "#475569" }}>{s.description}</span>
              <kbd className="px-2 py-1 rounded text-[11px] font-bold"
                style={{ background: "#f1f5f9", color: NAVY, border: "1px solid #e2e8f0" }}>
                {s.keys}
              </kbd>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Bulk SMS Reminders Modal — Send reminders to tomorrow's patients
   ═══════════════════════════════════════════════════════════════════════════ */
export function BulkSmsModal({
  open, onClose, tomorrowItems, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  tomorrowItems: TodayJourneyItem[];
  isPending: boolean;
  onConfirm: (appointmentIds: string[]) => void;
}) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (open && tomorrowItems.length > 0) {
      setSelectedIds(new Set(tomorrowItems.map(i => i.appointmentId).filter((id): id is string => !!id)));
    }
  }, [open, tomorrowItems]);

  const toggleItem = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    if (selectedIds.size === tomorrowItems.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(tomorrowItems.map(i => i.appointmentId).filter((id): id is string => !!id)));
    }
  };

  const handleSubmit = () => {
    if (selectedIds.size === 0) return;
    onConfirm(Array.from(selectedIds));
  };

  return (
    <ModalShell open={open} onClose={onClose} title="تذكيرات مواعيد الغد" icon={Send} iconColor="#3d7ab5" wide>
      <div className="mb-3 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#f0f5fb" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
        <span className="text-xs font-medium" style={{ color: NAVY }}>
          سيتم إرسال رسالة تذكير لكل مريض له موعد غداً عبر SMS
        </span>
      </div>

      {tomorrowItems.length === 0 ? (
        <div className="text-center py-8" style={{ color: "#94a3b8" }}>
          <CalendarPlus className="w-8 h-8 mx-auto mb-2 opacity-30" />
          <p className="text-sm font-bold">لا توجد مواعيد غداً</p>
        </div>
      ) : (
        <>
          <div className="flex items-center gap-2 mb-3">
            <input type="checkbox" checked={selectedIds.size === tomorrowItems.length}
              onChange={toggleAll}
              className="w-4 h-4 rounded border-gray-300 accent-[#3d7ab5]" />
            <span className="text-xs font-bold" style={{ color: NAVY }}>
              تحديد الكل ({tomorrowItems.length} مريض)
            </span>
            <span className="text-[10px] mr-auto" style={{ color: "#94a3b8" }}>
              تم تحديد {selectedIds.size}
            </span>
          </div>

          <div className="max-h-[300px] overflow-y-auto space-y-1.5">
            {tomorrowItems.map(item => (
              <label key={item.appointmentId ?? item.patientId}
                className="flex items-center gap-2.5 px-3 py-2 rounded-lg cursor-pointer hover:bg-gray-50"
                style={{ background: item.appointmentId && selectedIds.has(item.appointmentId) ? "#f0f5fb" : undefined }}>
                <input type="checkbox" checked={!!item.appointmentId && selectedIds.has(item.appointmentId)}
                  onChange={() => item.appointmentId && toggleItem(item.appointmentId)}
                  className="w-4 h-4 rounded border-gray-300 accent-[#3d7ab5]" />
                <div className="flex-1">
                  <span className="text-xs font-bold" style={{ color: NAVY }}>{item.patientName}</span>
                  <span className="text-[10px] mx-1.5" style={{ color: "#94a3b8" }}>—</span>
                  <span className="text-[10px]" style={{ color: "#64748b" }}>
                    {item.doctorName} — {fmtTime(item.appointmentTime)}
                  </span>
                </div>
                {item.patientPhone && (
                  <span className="text-[10px]" style={{ color: "#94a3b8" }}>{item.patientPhone}</span>
                )}
              </label>
            ))}
          </div>
        </>
      )}

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={selectedIds.size === 0 || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#3d7ab5", opacity: selectedIds.size === 0 || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
          إرسال {selectedIds.size} تذكير
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Direct Payment Modal — Pay for any patient (even without appointment)
   Allows searching for a patient by name/phone and making a direct payment.
   ═══════════════════════════════════════════════════════════════════════════ */
interface SearchedPatient {
  id: string;
  name: string;
  phone?: string;
  patientNumber?: string;
}

export function DirectPaymentModal({
  open, onClose, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  isPending: boolean;
  onConfirm: (data: {
    patientId: string; patientName: string;
    amount: number; paymentMethod: string;
    serviceDescription: string; notes: string;
    referenceNumber?: string;
  }) => void;
}) {
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState<SearchedPatient[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [selectedPatient, setSelectedPatient] = useState<SearchedPatient | null>(null);
  const searchTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Payment form state
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState("Cash");
  const [desc, setDesc] = useState("");
  const [notes, setNotes] = useState("");
  const [voucherType, setVoucherType] = useState<"consultation" | "procedure">("consultation");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [referenceError, setReferenceError] = useState("");

  // Dynamic payment methods from API
  const { data: paymentMethodSettings = [] } = usePaymentMethodSettings();
  const activePaymentMethods = paymentMethodSettings.filter(m => m.isActive);
  const selectedMethodSetting = activePaymentMethods.find(m => m.code.toLowerCase() === method.toLowerCase());
  const requiresRef = selectedMethodSetting?.requiresReferenceNumber ?? false;

  // Search patients with debounce
  useEffect(() => {
    if (!searchQuery.trim() || searchQuery.trim().length < 2) {
      setSearchResults([]);
      return;
    }
    if (searchTimeoutRef.current) clearTimeout(searchTimeoutRef.current);
    searchTimeoutRef.current = setTimeout(async () => {
      setIsSearching(true);
      try {
        const { data } = await api.get(`/api/patients?search=${encodeURIComponent(searchQuery.trim())}&pageSize=10`);
        const patients = Array.isArray(data) ? data : (data as { data?: SearchedPatient[]; items?: SearchedPatient[] }).data ?? (data as { items?: SearchedPatient[] }).items ?? [];
        setSearchResults(patients.map((p: SearchedPatient & { fullName?: string; arabicName?: string }) => ({
          id: p.id,
          name: p.name || p.fullName || p.arabicName || "—",
          phone: p.phone,
          patientNumber: p.patientNumber,
        })));
      } catch {
        setSearchResults([]);
      }
      setIsSearching(false);
    }, 350);
    return () => { if (searchTimeoutRef.current) clearTimeout(searchTimeoutRef.current); };
  }, [searchQuery]);

  // Reset on close
  useEffect(() => {
    if (!open) {
      setSearchQuery(""); setSearchResults([]); setSelectedPatient(null);
      setAmount(""); setMethod("Cash"); setDesc(""); setNotes(""); setVoucherType("consultation");
      setReferenceNumber(""); setReferenceError("");
    }
  }, [open]);

  const handleSubmit = () => {
    if (!selectedPatient) return;
    const num = parseFloat(amount);
    if (!num || num <= 0) return;
    // Validate reference number if required
    if (requiresRef && !referenceNumber.trim()) {
      setReferenceError("الرقم المرجعي مطلوب لطريقة الدفع هذه");
      return;
    }
    setReferenceError("");
    const voucherPrefix = voucherType === "consultation" ? "[سند معاينة] " : "[إجراءات شغل/خدمة مقدمة] ";
    onConfirm({
      patientId: selectedPatient.id,
      patientName: selectedPatient.name,
      amount: num,
      paymentMethod: method,
      serviceDescription: desc ? voucherPrefix + desc : voucherPrefix.trim(),
      notes: notes,
      referenceNumber: requiresRef ? referenceNumber.trim() : undefined,
    });
  };

  return (
    <ModalShell open={open} onClose={onClose} title="دفع لمريض (بدون موعد)" icon={CreditCard} iconColor="#22c55e" wide>
      {/* Step 1: Search Patient */}
      {!selectedPatient && (
        <div className="space-y-3">
          <div className="p-2.5 rounded-xl flex items-center gap-2" style={{ background: "#f0f5fb", border: "1px solid #3d7ab520" }}>
            <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
            <span className="text-[11px] font-medium" style={{ color: NAVY }}>
              ابحث عن مريض لتسجيل دفعة مباشرة بدون الحاجة لموعد محجوز
            </span>
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>بحث عن مريض *</label>
            <div className="relative">
              <Search className="w-4 h-4 absolute top-1/2 right-3 -translate-y-1/2" style={{ color: "#94a3b8" }} />
              <input
                type="text"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="اسم المريض أو رقم الهاتف..."
                className={inputCls() + " pr-9"}
                autoFocus
              />
              {isSearching && (
                <Loader2 className="w-4 h-4 absolute top-1/2 left-3 -translate-y-1/2 animate-spin" style={{ color: BLUE }} />
              )}
            </div>
          </div>

          {/* Search Results */}
          {searchResults.length > 0 && (
            <div className="border rounded-xl overflow-hidden" style={{ borderColor: "#e5e7eb" }}>
              <div className="max-h-[200px] overflow-y-auto">
                {searchResults.map(p => (
                  <button
                    key={p.id}
                    onClick={() => setSelectedPatient(p)}
                    className="w-full text-right px-4 py-3 flex items-center gap-3 transition hover:bg-[#f0f5fb] border-b last:border-0"
                    style={{ borderColor: "#f1f5f9" }}
                  >
                    <div className="w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0"
                      style={{ background: NAVY + "12" }}>
                      <User className="w-4 h-4" style={{ color: NAVY }} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="text-sm font-bold truncate" style={{ color: NAVY }}>{p.name}</div>
                      <div className="text-[10px]" style={{ color: "#94a3b8" }}>
                        {p.phone && <span>{p.phone}</span>}
                        {p.patientNumber && <span className="mr-2">#{p.patientNumber}</span>}
                      </div>
                    </div>
                    <CreditCard className="w-4 h-4 flex-shrink-0" style={{ color: "#22c55e" }} />
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* No results */}
          {searchQuery.trim().length >= 2 && !isSearching && searchResults.length === 0 && (
            <div className="text-center py-6">
              <User className="w-8 h-8 mx-auto mb-2 opacity-20" style={{ color: "#94a3b8" }} />
              <p className="text-xs font-bold" style={{ color: "#94a3b8" }}>لم يتم العثور على مريض</p>
              <p className="text-[10px] mt-1" style={{ color: "#94a3b8" }}>جرّب بحثاً مختلفاً أو سجّل مريض مشي جديد</p>
            </div>
          )}
        </div>
      )}

      {/* Step 2: Payment Form */}
      {selectedPatient && (
        <div className="space-y-3">
          {/* Selected patient chip */}
          <div className="p-3 rounded-xl flex items-center gap-3" style={{ background: "#f0f5fb" }}>
            <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0"
              style={{ background: "#22c55e15" }}>
              <User className="w-4 h-4" style={{ color: "#22c55e" }} />
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-sm font-bold truncate" style={{ color: NAVY }}>{selectedPatient.name}</div>
              <div className="text-[10px]" style={{ color: "#94a3b8" }}>
                {selectedPatient.phone && <span>{selectedPatient.phone}</span>}
                {selectedPatient.patientNumber && <span className="mr-2">#{selectedPatient.patientNumber}</span>}
              </div>
            </div>
            <button onClick={() => setSelectedPatient(null)}
              className="px-2 py-1 rounded-lg text-[10px] font-bold"
              style={{ background: "#ef444415", color: "#ef4444", border: "1px solid #ef444430" }}>
              تغيير
            </button>
          </div>

          {/* Voucher Type Selector */}
          <div>
            <label className="text-xs font-semibold block mb-2" style={{ color: NAVY }}>نوع السند *</label>
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
                <span className="text-xs font-bold" style={{ color: voucherType === "consultation" ? NAVY : "#94a3b8" }}>سند معاينة</span>
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
                <span className="text-xs font-bold" style={{ color: voucherType === "procedure" ? NAVY : "#94a3b8" }}>إجراءات شغل</span>
              </button>
            </div>
          </div>

          {/* Amount */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المبلغ *</label>
            <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
              placeholder="0" className={inputCls()} min={0} step={0.01} dir="ltr" />
          </div>

          {/* Payment Method */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>طريقة الدفع</label>
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
              <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الرقم المرجعي <span className="text-red-500">*</span></label>
              <input value={referenceNumber} onChange={e => { setReferenceNumber(e.target.value); setReferenceError(""); }}
                placeholder="أدخل الرقم المرجعي" className={inputCls(!!referenceError)} dir="ltr" />
              {referenceError && <p className="text-[10px] text-red-500 mt-1">{referenceError}</p>}
            </div>
          )}

          {/* Service Description */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>وصف الخدمة</label>
            <input value={desc} onChange={e => setDesc(e.target.value)}
              placeholder={voucherType === "consultation" ? "مثال: كشف + فحص أشعة" : "مثال: حشوة تجميلية + تنظيف"}
              className={inputCls()} />
          </div>

          {/* Notes */}
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
            <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        {selectedPatient && (
          <button onClick={handleSubmit} disabled={!amount || isPending}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
            style={{ background: "#22c55a", opacity: !amount || isPending ? 0.5 : 1 }}>
            {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
            تسجيل الدفع
          </button>
        )}
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Manager Override Dialog — for patients with overdue balance
   ═══════════════════════════════════════════════════════════════════════════ */
export function OverrideDialog({
  open,
  onClose,
  patientName,
  overdueAmount,
  onConfirm,
}: {
  open: boolean;
  onClose: () => void;
  patientName: string;
  overdueAmount: number;
  onConfirm: (managerUsername: string) => void;
}) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleVerify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password) return;
    setLoading(true);
    setError("");
    try {
      const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";
      const { data } = await axios.post(`${API_URL}/api/auth/login`, {
        username: username.trim(),
        password,
      });

      const userRole = data?.user?.role;
      if (userRole === "Admin" || userRole === "BranchManager") {
        onConfirm(username.trim());
        setUsername("");
        setPassword("");
      } else {
        setError("عذراً، هذا الحساب لا يملك صلاحية تجاوز (يجب أن يكون مدير فرع أو مسؤول)");
      }
    } catch (err: unknown) {
      let errorMsg = "فشل التحقق من اسم المستخدم أو كلمة المرور";
      if (err && typeof err === "object" && "response" in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) errorMsg = resp.data.message;
      }
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <ModalShell open={open} onClose={onClose} title="موافقة المدير المطلوبة (تجاوز متأخرات)" icon={AlertTriangle} iconColor="#ef4444">
      <div className="mb-4 p-3.5 rounded-xl border border-red-100 bg-red-50 text-red-800 text-xs">
        <div className="font-extrabold text-sm mb-1">تنبيه وجود متأخرات مالية!</div>
        لا يمكن تسجيل وصول أو إدخال المريض <span className="font-extrabold">{patientName}</span> للانتظار نظراً لوجود أقساط متأخرة بقيمة <span className="font-extrabold">{fmtRial(overdueAmount)}</span>.
        لتجاوز هذا المنع، يجب إدخال اسم مستخدم وكلمة مرور لمدير الفرع أو المسؤول.
      </div>

      <form onSubmit={handleVerify} className="space-y-3.5">
        <div>
          <label className="text-xs font-semibold block mb-1 text-gray-700">اسم مستخدم المدير</label>
          <input
            type="text"
            value={username}
            onChange={e => setUsername(e.target.value)}
            placeholder="Username"
            className={inputCls()}
            required
            autoFocus
          />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1 text-gray-700">كلمة المرور</label>
          <input
            type="password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            placeholder="••••••••"
            className={inputCls()}
            required
          />
        </div>

        {error && (
          <div className="text-xs font-semibold text-red-600 bg-red-50 p-2 rounded-lg border border-red-200">
            {error}
          </div>
        )}

        <div className="flex gap-2 pt-2">
          <button type="button" onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold bg-gray-100 text-gray-600">
            إلغاء
          </button>
          <button
            type="submit"
            disabled={loading || !username.trim() || !password}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white bg-red-600 hover:bg-red-700 flex items-center justify-center gap-2 disabled:opacity-50"
          >
            {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
            تأكيد التجاوز
          </button>
        </div>
      </form>
    </ModalShell>
  );
}
