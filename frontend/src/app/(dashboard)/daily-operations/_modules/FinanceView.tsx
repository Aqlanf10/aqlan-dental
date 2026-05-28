"use client";

import { useState, useCallback } from "react";
import {
  CreditCard, TrendingUp, AlertTriangle,
  RefreshCw, Loader2, FileText, CheckCircle,
  DollarSign, ArrowUpRight, ArrowDownRight, Users,
  Receipt, IndianRupee, X, UserPlus, Search,
  Vault, CircleDot,
} from "lucide-react";
import { NAVY, BLUE, ORANGE, fmtRial, PAYMENT_METHODS } from "../_lib/constants";
import type { FinanceSummaryData } from "../_lib/constants";
import { useFinanceSummary, useTodayJourneyItems, useCreatePayment, useCheckout, useCreateDraftInvoice, useActiveCashierSession } from "../_lib/hooks";
import { toast } from "@/stores/toastStore";
import { api } from "@/lib/api";

/* ─── Types ────────────────────────────────────────────────────────────────── */
interface ReadyForCheckoutPatient {
  visitId?: string | null;
  appointmentId: string;
  patientId: string;
  patientName: string;
  doctorName: string;
  serviceName?: string;
  amountDue?: number;
  amountDueReference?: number;
  checkoutStatus?: string;
  nextAction: string;
}

interface PaymentItem {
  id: string;
  amount: number;
  paymentDate: string;
  patientName?: string;
  paymentMethod?: string;
}

interface InvoiceItem {
  id: string;
  invoiceNumber: string;
  totalAmount: number;
  status: string;
  patientName?: string;
}

/* ─── Status colors ────────────────────────────────────────────────────────── */
const INVOICE_STATUS: Record<string, { label: string; color: string; bg: string }> = {
  Draft:     { label: "مسودة",     color: "#6b7280", bg: "#f3f4f6" },
  Pending:   { label: "معلّقة",     color: "#d97706", bg: "#fffbeb" },
  Paid:      { label: "مدفوعة",     color: "#16a34a", bg: "#f0fdf4" },
  Partially: { label: "مدفوعة جزئياً", color: "#2563eb", bg: "#eff6ff" },
  Overdue:   { label: "متأخرة",     color: "#dc2626", bg: "#fef2f2" },
  Cancelled: { label: "ملغاة",     color: "#6b7280", bg: "#f9fafb" },
};

const METHOD_LABELS: Record<string, string> = {
  Cash: "نقدي",
  Card: "بطاقة",
  BankTransfer: "تحويل بنكي",
  MobileWallet: "محفظة إلكترونية",
};

/* ─── Component ────────────────────────────────────────────────────────────── */
import type { TodayJourneyItem } from "../_lib/constants";

interface FinanceViewProps {
  onContextMenu?: (e: React.MouseEvent, item: TodayJourneyItem) => void;
}

export default function FinanceView({ onContextMenu }: FinanceViewProps) {
  // ── Data hooks ──
  const { data: summaryRaw, isLoading: summaryLoading, refetch: refetchSummary } = useFinanceSummary();
  const { data: journeyItems, isLoading: journeyLoading, refetch: refetchJourney } = useTodayJourneyItems({});
  const { data: cashierSession } = useActiveCashierSession();

  // ── Mutations ──
  const createPaymentMutation = useCreatePayment();
  const checkoutMutation = useCheckout();
  const draftInvoiceMutation = useCreateDraftInvoice();

  // ── Local UI state ──
  const [paymentPatient, setPaymentPatient] = useState<ReadyForCheckoutPatient | null>(null);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("Cash");
  const [paymentNotes, setPaymentNotes] = useState("");
  const [checkoutPatient, setCheckoutPatient] = useState<ReadyForCheckoutPatient | null>(null);
  const [checkoutNotes, setCheckoutNotes] = useState("");
  const [draftPendingId, setDraftPendingId] = useState<string | null>(null);

  // Walk-in payment state
  const [showWalkInPayment, setShowWalkInPayment] = useState(false);
  const [walkInSearch, setWalkInSearch] = useState("");
  const [walkInPatientOptions, setWalkInPatientOptions] = useState<{ id: string; fullName: string; patientNumber: string }[]>([]);
  const [walkInPatientId, setWalkInPatientId] = useState("");
  const [, setWalkInPatientName] = useState("");
  const [walkInAmount, setWalkInAmount] = useState("");
  const [walkInMethod, setWalkInMethod] = useState("Cash");
  const [walkInDesc, setWalkInDesc] = useState("");
  const [walkInNotes, setWalkInNotes] = useState("");

  // ── Derived data ──
  const summary: FinanceSummaryData | undefined = summaryRaw;
  const readyPatients: ReadyForCheckoutPatient[] = (journeyItems ?? []).filter(
    (p) =>
      p.checkoutStatus === "ReadyForCheckout" ||
      p.nextAction === "Checkout"
  );

  const recentPayments: PaymentItem[] = (summary?.recentPayments ?? []).map((p) => ({
    id: p.id,
    amount: p.amount,
    paymentDate: p.paymentDate,
    patientName: p.patientName,
    paymentMethod: undefined,
  }));

  const recentInvoices: InvoiceItem[] = (summary?.recentInvoices ?? []).map((inv) => ({
    id: inv.id,
    invoiceNumber: inv.invoiceNumber,
    totalAmount: inv.totalAmount,
    status: inv.status,
    patientName: undefined,
  }));

  // ── Handlers ──

  /** Draft invoice: uses visitId */
  const handleDraftInvoice = useCallback(async (patient: ReadyForCheckoutPatient) => {
    if (!patient.visitId) {
      toast.error("لا يمكن إنشاء فاتورة: رقم الزيارة غير متوفر");
      return;
    }
    setDraftPendingId(patient.appointmentId);
    try {
      await draftInvoiceMutation.mutateAsync(patient.visitId);
      toast.success("تم إنشاء فاتورة مسودة بنجاح");
      refetchSummary();
      refetchJourney();
    } catch {
      toast.error("فشل إنشاء الفاتورة المسودة");
    } finally {
      setDraftPendingId(null);
    }
  }, [draftInvoiceMutation, refetchSummary, refetchJourney]);

  /** Quick payment: opens in-page payment form */
  const handleOpenPayment = useCallback((patient: ReadyForCheckoutPatient) => {
    const amount = patient.amountDueReference ?? patient.amountDue ?? 0;
    setPaymentPatient(patient);
    setPaymentAmount(amount > 0 ? String(amount) : "");
    setPaymentMethod("Cash");
    setPaymentNotes("");
  }, []);

  /** Submit quick payment */
  const handleSubmitPayment = useCallback(async () => {
    if (!paymentPatient) return;
    const amount = parseFloat(paymentAmount);
    if (!amount || amount <= 0) {
      toast.error("يرجى إدخال مبلغ صحيح");
      return;
    }
    try {
      await createPaymentMutation.mutateAsync({
        patientId: paymentPatient.patientId,
        amount,
        paymentMethod,
        serviceDescription: `دفع سريع — ${paymentPatient.serviceName ?? "خدمة"}`,
        doctorId: undefined,
        notes: paymentNotes || undefined,
      });
      toast.success(`تم تسجيل الدفع بنجاح — ${fmtRial(amount)}`);
      setPaymentPatient(null);
      refetchSummary();
      refetchJourney();
    } catch {
      toast.error("فشل تسجيل الدفع");
    }
  }, [paymentPatient, paymentAmount, paymentMethod, paymentNotes, createPaymentMutation, refetchSummary, refetchJourney]);

  /** Complete checkout */
  const handleOpenCheckout = useCallback((patient: ReadyForCheckoutPatient) => {
    setCheckoutPatient(patient);
    setCheckoutNotes("");
  }, []);

  const handleSubmitCheckout = useCallback(async () => {
    if (!checkoutPatient) return;
    try {
      await checkoutMutation.mutateAsync({
        appointmentId: checkoutPatient.appointmentId,
        body: {
          notes: checkoutNotes || undefined,
        },
      });
      toast.success(`تم إكمال خروج ${checkoutPatient.patientName} بنجاح`);
      setCheckoutPatient(null);
      refetchSummary();
      refetchJourney();
    } catch {
      toast.error("فشل إكمال الخروج");
    }
  }, [checkoutPatient, checkoutNotes, checkoutMutation, refetchSummary, refetchJourney]);

  // ── Walk-in Payment Handlers ──
  const searchWalkInPatient = useCallback(async (q: string) => {
    setWalkInSearch(q);
    if (q.length < 2) { setWalkInPatientOptions([]); return; }
    try {
      const { data } = await api.get<{ data: { id: string; fullName: string; patientNumber: string }[] }>("/api/patients", { params: { search: q, pageSize: 10 } });
      setWalkInPatientOptions(data?.data ?? []);
    } catch { /* ignore */ }
  }, []);

  const handleWalkInPayment = useCallback(async () => {
    if (!walkInPatientId || !walkInAmount || Number(walkInAmount) <= 0) {
      toast.error("يرجى اختيار المريض وإدخال المبلغ");
      return;
    }
    try {
      await createPaymentMutation.mutateAsync({
        patientId: walkInPatientId,
        amount: Number(walkInAmount),
        paymentMethod: walkInMethod,
        serviceDescription: walkInDesc?.trim() || "دفعة مباشرة — بدون حجز",
        doctorId: undefined,
        notes: walkInNotes?.trim() || undefined,
      });
      toast.success(`تم تسجيل الدفعة بنجاح — ${fmtRial(Number(walkInAmount))}`);
      resetWalkInForm();
      refetchSummary();
    } catch {
      toast.error("فشل تسجيل الدفعة");
    }
  }, [walkInPatientId, walkInAmount, walkInMethod, walkInDesc, walkInNotes, createPaymentMutation, refetchSummary]);

  const resetWalkInForm = () => {
    setShowWalkInPayment(false);
    setWalkInSearch("");
    setWalkInPatientOptions([]);
    setWalkInPatientId("");
    setWalkInPatientName("");
    setWalkInAmount("");
    setWalkInMethod("Cash");
    setWalkInDesc("");
    setWalkInNotes("");
  };

  // ── Loading ──
  if (summaryLoading && journeyLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <Loader2 className="w-8 h-8 animate-spin" style={{ color: BLUE }} />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full overflow-auto p-4 gap-4" style={{ background: "#f8fafc" }}>
      {/* ── Summary Cards ── */}
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
        <SummaryCard
          icon={DollarSign}
          label="تحصيل اليوم"
          value={fmtRial(summary?.todayCollected)}
          color="#16a34a"
          bg="#f0fdf4"
          trend="up"
        />
        <SummaryCard
          icon={TrendingUp}
          label="تحصيل الشهر"
          value={fmtRial(summary?.monthCollected)}
          color="#2563eb"
          bg="#eff6ff"
          trend="up"
        />
        <SummaryCard
          icon={AlertTriangle}
          label="مبالغ متأخرة"
          value={fmtRial(summary?.overdueAmount)}
          color="#dc2626"
          bg="#fef2f2"
          trend="down"
        />
        <SummaryCard
          icon={Users}
          label="عقود نشطة"
          value={String(summary?.activeContracts ?? 0)}
          color="#7c3aed"
          bg="#f5f3ff"
        />
        <button
          onClick={() => { resetWalkInForm(); setShowWalkInPayment(true); }}
          className="rounded-xl border p-4 flex items-center gap-3 cursor-pointer transition hover:shadow-md"
          style={{ background: "#fff", borderColor: "#16a34a40" }}
        >
          <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: "#16a34a15" }}>
            <UserPlus className="w-4 h-4" style={{ color: "#16a34a" }} />
          </div>
          <div className="text-right">
            <p className="text-[10px] font-medium" style={{ color: "#94a3b8" }}>دفعة مباشرة</p>
            <p className="text-sm font-bold" style={{ color: "#16a34a" }}>مريض بدون حجز</p>
          </div>
        </button>
      </div>

      {/* ── Cashier Shift Status ── */}
      <div
        className="rounded-xl border p-3 flex items-center justify-between"
        style={{
          background: cashierSession?.hasActiveSession
            ? "linear-gradient(135deg, #f0fdf4, #dcfce7)"
            : "linear-gradient(135deg, #fef2f2, #fee2e2)",
          borderColor: cashierSession?.hasActiveSession ? "#16a34a30" : "#dc262630",
        }}
      >
        <div className="flex items-center gap-3">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center"
            style={{
              background: cashierSession?.hasActiveSession ? "#16a34a15" : "#dc262615",
            }}
          >
            {cashierSession?.hasActiveSession ? (
              <CircleDot className="w-4 h-4" style={{ color: "#16a34a" }} />
            ) : (
              <Vault className="w-4 h-4" style={{ color: "#dc2626" }} />
            )}
          </div>
          <div>
            {cashierSession?.hasActiveSession ? (
              <>
                <p className="text-xs font-bold" style={{ color: NAVY }}>
                  وردية مفتوحة: {cashierSession.sessionNumber ?? ""}
                </p>
                <p className="text-[10px]" style={{ color: "#64748b" }}>
                  أمين الصندوق: {cashierSession.cashierName ?? ""} · التحصيلات: {fmtRial(cashierSession.totalCollections ?? 0)}
                </p>
              </>
            ) : (
              <>
                <p className="text-xs font-bold" style={{ color: "#dc2626" }}>
                  لا توجد وردية صندوق مفتوحة
                </p>
                <p className="text-[10px]" style={{ color: "#991b1b" }}>
                  يجب فتح وردية قبل تسجيل التحصيلات النقدية
                </p>
              </>
            )}
          </div>
        </div>
        {!cashierSession?.hasActiveSession && (
          <a
            href="/finance-v3"
            className="px-3 py-1.5 rounded-lg text-[10px] font-bold text-white no-underline"
            style={{ background: "#dc2626" }}
          >
            فتح وردية صندوق
          </a>
        )}
      </div>

      {/* ── Ready for Checkout Banner ── */}
      {readyPatients.length > 0 && (
        <div
          className="rounded-xl border p-4"
          style={{
            background: "linear-gradient(135deg, #fff7ed, #fef3c7)",
            borderColor: "#f5922e40",
            boxShadow: "0 2px 8px rgba(245,146,46,0.1)",
          }}
        >
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <div
                className="w-8 h-8 rounded-lg flex items-center justify-center"
                style={{ background: ORANGE + "20" }}
              >
                <CreditCard className="w-4 h-4" style={{ color: ORANGE }} />
              </div>
              <h3 className="text-sm font-extrabold" style={{ color: NAVY }}>
                جاهزون للدفع من الطبيب ({readyPatients.length})
              </h3>
            </div>
            <button onClick={() => { refetchSummary(); refetchJourney(); }} className="p-1 rounded-lg hover:bg-white/60 transition">
              <RefreshCw className="w-3.5 h-3.5" style={{ color: ORANGE }} />
            </button>
          </div>
          <div className="space-y-2">
            {readyPatients.map((p) => (
              <div
                key={p.appointmentId}
                className="flex items-center justify-between bg-white rounded-lg border px-3 py-2.5 cursor-context-menu"
                style={{ borderColor: "#e5e7eb" }}
                onContextMenu={(e) => {
                  e.preventDefault();
                  onContextMenu?.(e, p as TodayJourneyItem);
                }}
              >
                <div className="flex items-center gap-3">
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold"
                    style={{ background: NAVY + "15", color: NAVY }}
                  >
                    {p.patientName.split(" ").filter(Boolean).length >= 2
                      ? p.patientName.split(" ")[0][0] + p.patientName.split(" ")[1][0]
                      : p.patientName[0]}
                  </div>
                  <div>
                    <p className="text-xs font-bold" style={{ color: NAVY }}>
                      {p.patientName}
                    </p>
                    <p className="text-[10px]" style={{ color: "#94a3b8" }}>
                      {p.serviceName ?? "—"} · {p.doctorName}
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {(p.amountDueReference ?? p.amountDue ?? 0) > 0 && (
                    <span className="text-xs font-bold" style={{ color: ORANGE }}>
                      {fmtRial(p.amountDueReference ?? p.amountDue)}
                    </span>
                  )}
                  {/* Draft Invoice */}
                  <button
                    onClick={() => handleDraftInvoice(p)}
                    disabled={draftPendingId === p.appointmentId || draftInvoiceMutation.isPending}
                    className="px-2 py-1.5 rounded-lg text-[10px] font-bold transition hover:opacity-80"
                    style={{ background: NAVY + "10", color: NAVY, border: `1px solid ${NAVY}30` }}
                    title="إنشاء فاتورة مسودة"
                  >
                    {draftPendingId === p.appointmentId ? (
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      "فاتورة مسودة"
                    )}
                  </button>
                  {/* Quick Payment */}
                  <button
                    onClick={() => handleOpenPayment(p)}
                    disabled={createPaymentMutation.isPending}
                    className="px-2 py-1.5 rounded-lg text-[10px] font-bold text-white transition hover:opacity-80"
                    style={{ background: "#16a34a" }}
                    title="دفع سريع"
                  >
                    دفع سريع
                  </button>
                  {/* Complete Checkout */}
                  <button
                    onClick={() => handleOpenCheckout(p)}
                    disabled={checkoutMutation.isPending}
                    className="px-2 py-1.5 rounded-lg text-[10px] font-bold text-white transition hover:opacity-80"
                    style={{ background: BLUE }}
                    title="إكمال الخروج"
                  >
                    إكمال الخروج
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ── Quick Payment Modal (In-Page) ── */}
      {paymentPatient && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => setPaymentPatient(null)}>
          <div
            className="bg-white rounded-2xl shadow-xl w-full max-w-md"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="flex items-center gap-3 px-5 py-4 border-b" style={{ borderColor: "#e8f0f9" }}>
              <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: "#16a34a15" }}>
                <CreditCard className="w-4 h-4" style={{ color: "#16a34a" }} />
              </div>
              <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: NAVY }}>دفع سريع</h3>
              <button onClick={() => setPaymentPatient(null)} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-400" />
              </button>
            </div>
            {/* Body */}
            <div className="p-5 space-y-3">
              <div className="p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
                <div className="font-bold text-sm" style={{ color: NAVY }}>{paymentPatient.patientName}</div>
                <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
                  الطبيب: {paymentPatient.doctorName} — الخدمة: {paymentPatient.serviceName ?? "—"}
                </div>
              </div>
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المبلغ *</label>
                <div className="flex gap-2">
                  <input
                    type="number"
                    value={paymentAmount}
                    onChange={(e) => setPaymentAmount(e.target.value)}
                    placeholder="0"
                    className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                    min={0}
                    step={0.01}
                    dir="ltr"
                  />
                  {(paymentPatient.amountDueReference ?? paymentPatient.amountDue ?? 0) > 0 && (
                    <button
                      onClick={() => setPaymentAmount(String(paymentPatient.amountDueReference ?? paymentPatient.amountDue))}
                      className="px-3 rounded-lg text-xs font-semibold whitespace-nowrap"
                      style={{ background: "#f5922e15", color: ORANGE, border: "1px solid #f5922e30" }}
                    >
                      الكل
                    </button>
                  )}
                </div>
              </div>
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>طريقة الدفع</label>
                <select
                  value={paymentMethod}
                  onChange={(e) => setPaymentMethod(e.target.value)}
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                >
                  {PAYMENT_METHODS.map((m) => (
                    <option key={m.value} value={m.value}>{m.label}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
                <input
                  value={paymentNotes}
                  onChange={(e) => setPaymentNotes(e.target.value)}
                  placeholder="اختياري"
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                />
              </div>
              <div className="flex gap-2 mt-4">
                <button
                  onClick={() => setPaymentPatient(null)}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold"
                  style={{ background: "#f1f5f9", color: "#64748b" }}
                >
                  إلغاء
                </button>
                <button
                  onClick={handleSubmitPayment}
                  disabled={!paymentAmount || createPaymentMutation.isPending}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
                  style={{ background: "#22c55a", opacity: !paymentAmount || createPaymentMutation.isPending ? 0.5 : 1 }}
                >
                  {createPaymentMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
                  تسجيل الدفع
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── Complete Checkout Modal (In-Page) ── */}
      {checkoutPatient && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => setCheckoutPatient(null)}>
          <div
            className="bg-white rounded-2xl shadow-xl w-full max-w-md"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="flex items-center gap-3 px-5 py-4 border-b" style={{ borderColor: "#e8f0f9" }}>
              <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: "#16a34a15" }}>
                <CheckCircle className="w-4 h-4" style={{ color: "#16a34a" }} />
              </div>
              <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: NAVY }}>إكمال الخروج</h3>
              <button onClick={() => setCheckoutPatient(null)} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-400" />
              </button>
            </div>
            {/* Body */}
            <div className="p-5 space-y-3">
              <div className="p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
                <div className="font-bold text-sm" style={{ color: NAVY }}>{checkoutPatient.patientName}</div>
                <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>
                  الطبيب: {checkoutPatient.doctorName} — الخدمة: {checkoutPatient.serviceName ?? "—"}
                </div>
              </div>

              <div className="p-2.5 rounded-xl flex items-center gap-2" style={{ background: "#f0fdf4", border: "1px solid #16a34a30" }}>
                <CheckCircle className="w-4 h-4 flex-shrink-0" style={{ color: "#16a34a" }} />
                <span className="text-[11px] font-medium" style={{ color: "#166534" }}>
                  سيتم إنهاء الزيارة وإكمال خروج المريض
                </span>
              </div>

              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
                <textarea
                  value={checkoutNotes}
                  onChange={(e) => setCheckoutNotes(e.target.value)}
                  rows={2}
                  placeholder="ملاحظات على إنهاء الزيارة (اختياري)"
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                />
              </div>

              <div className="flex gap-2 mt-4">
                <button
                  onClick={() => setCheckoutPatient(null)}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold"
                  style={{ background: "#f1f5f9", color: "#64748b" }}
                >
                  إلغاء
                </button>
                <button
                  onClick={handleSubmitCheckout}
                  disabled={checkoutMutation.isPending}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
                  style={{ background: "#16a34a", opacity: checkoutMutation.isPending ? 0.5 : 1 }}
                >
                  {checkoutMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
                  تأكيد الخروج
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── Two-column: Recent Payments + Recent Invoices ── */}

      {/* ── Walk-in Payment Modal ── */}
      {showWalkInPayment && (
        <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={() => resetWalkInForm()}>
          <div
            className="bg-white rounded-2xl shadow-xl w-full max-w-md"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="flex items-center gap-3 px-5 py-4 border-b" style={{ borderColor: "#e8f0f9" }}>
              <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: "#16a34a15" }}>
                <UserPlus className="w-4 h-4" style={{ color: "#16a34a" }} />
              </div>
              <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: NAVY }}>دفعة مباشرة — مريض بدون حجز</h3>
              <button onClick={() => resetWalkInForm()} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
                <X className="w-4 h-4 text-gray-400" />
              </button>
            </div>
            {/* Body */}
            <div className="p-5 space-y-3">
              <div className="p-2.5 rounded-xl flex items-center gap-2" style={{ background: "#f0fdf4", border: "1px solid #16a34a30" }}>
                <UserPlus className="w-4 h-4 flex-shrink-0" style={{ color: "#16a34a" }} />
                <span className="text-[11px] font-medium" style={{ color: "#166534" }}>
                  لهذا المريض حجز أو لا — يمكن تسجيل الدفعة مباشرة
                </span>
              </div>

              {/* Patient search */}
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المريض *</label>
                <div className="relative">
                  <input
                    value={walkInSearch}
                    onChange={(e) => searchWalkInPatient(e.target.value)}
                    placeholder="ابحث بالاسم أو الرقم..."
                    className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20 pr-9"
                  />
                  <Search className="w-4 h-4 absolute right-3 top-2.5" style={{ color: "#94a3b8" }} />
                </div>
                {walkInPatientOptions.length > 0 && !walkInPatientId && (
                  <div className="mt-1 rounded-md border max-h-40 overflow-y-auto" style={{ borderColor: "#e2e8f0" }}>
                    {walkInPatientOptions.map((p) => (
                      <button
                        key={p.id}
                        onClick={() => {
                          setWalkInSearch(`${p.fullName} (${p.patientNumber})`);
                          setWalkInPatientId(p.id);
                          setWalkInPatientName(p.fullName);
                          setWalkInPatientOptions([]);
                        }}
                        className="w-full text-right px-3 py-2 text-sm transition-colors hover:bg-gray-50"
                        style={{ color: NAVY }}
                      >
                        {p.fullName} ({p.patientNumber})
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* Amount */}
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>المبلغ *</label>
                <input
                  type="number"
                  value={walkInAmount}
                  onChange={(e) => setWalkInAmount(e.target.value)}
                  placeholder="0"
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                  min={0}
                  step={0.01}
                  dir="ltr"
                />
              </div>

              {/* Payment method */}
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>طريقة الدفع</label>
                <select
                  value={walkInMethod}
                  onChange={(e) => setWalkInMethod(e.target.value)}
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                >
                  {PAYMENT_METHODS.map((m) => (
                    <option key={m.value} value={m.value}>{m.label}</option>
                  ))}
                </select>
              </div>

              {/* Description */}
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>وصف الخدمة</label>
                <input
                  value={walkInDesc}
                  onChange={(e) => setWalkInDesc(e.target.value)}
                  placeholder="مثال: استشارة، حشو، تنظيف..."
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                />
              </div>

              {/* Notes */}
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
                <input
                  value={walkInNotes}
                  onChange={(e) => setWalkInNotes(e.target.value)}
                  placeholder="اختياري"
                  className="w-full rounded-lg border px-3 py-2 text-sm outline-none border-[#e2e8f0] bg-white focus:border-[#3d7ab5] focus:ring-1 focus:ring-[#3d7ab5]/20"
                />
              </div>

              <div className="flex gap-2 mt-4">
                <button
                  onClick={() => resetWalkInForm()}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold"
                  style={{ background: "#f1f5f9", color: "#64748b" }}
                >
                  إلغاء
                </button>
                <button
                  onClick={handleWalkInPayment}
                  disabled={!walkInPatientId || !walkInAmount || createPaymentMutation.isPending}
                  className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
                  style={{ background: "#22c55a", opacity: !walkInPatientId || !walkInAmount || createPaymentMutation.isPending ? 0.5 : 1 }}
                >
                  {createPaymentMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
                  تسجيل الدفعة
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── Two-column: Recent Payments + Recent Invoices ── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Recent Payments */}
        <div
          className="rounded-xl border p-4"
          style={{ background: "#fff", borderColor: "#e5e7eb" }}
        >
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <Receipt className="w-4 h-4" style={{ color: BLUE }} />
              <h3 className="text-xs font-bold" style={{ color: NAVY }}>
                آخر المدفوعات
              </h3>
            </div>
            <button onClick={() => { refetchSummary(); refetchJourney(); }} className="p-1 rounded-lg hover:bg-gray-100">
              <RefreshCw className="w-3.5 h-3.5" style={{ color: "#94a3b8" }} />
            </button>
          </div>
          {recentPayments.length > 0 ? (
            <div className="space-y-2">
              {recentPayments.map((p) => (
                <div
                  key={p.id}
                  className="flex items-center justify-between py-2 border-b last:border-0"
                  style={{ borderColor: "#f1f5f9" }}
                >
                  <div>
                    <p className="text-xs font-bold" style={{ color: NAVY }}>
                      {p.patientName ?? "—"}
                    </p>
                    <p className="text-[10px]" style={{ color: "#94a3b8" }}>
                      {p.paymentDate ? new Date(p.paymentDate).toLocaleDateString("ar-YE") : "—"}
                      {p.paymentMethod ? ` · ${METHOD_LABELS[p.paymentMethod] ?? p.paymentMethod}` : ""}
                    </p>
                  </div>
                  <span className="text-xs font-extrabold" style={{ color: "#16a34a" }}>
                    {fmtRial(p.amount)}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-center py-6 text-xs" style={{ color: "#94a3b8" }}>
              لا توجد مدفوعات اليوم
            </div>
          )}
        </div>

        {/* Recent Invoices */}
        <div
          className="rounded-xl border p-4"
          style={{ background: "#fff", borderColor: "#e5e7eb" }}
        >
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <FileText className="w-4 h-4" style={{ color: NAVY }} />
              <h3 className="text-xs font-bold" style={{ color: NAVY }}>
                آخر الفواتير
              </h3>
            </div>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full" style={{ background: "#fef3c7", color: "#d97706" }}>
              {summary?.draftInvoicesCount ?? 0} مسودة
            </span>
          </div>
          {recentInvoices.length > 0 ? (
            <div className="space-y-2">
              {recentInvoices.map((inv) => {
                const st = INVOICE_STATUS[inv.status] ?? INVOICE_STATUS.Draft;
                return (
                  <div
                    key={inv.id}
                    className="flex items-center justify-between py-2 border-b last:border-0"
                    style={{ borderColor: "#f1f5f9" }}
                  >
                    <div>
                      <p className="text-xs font-bold" style={{ color: NAVY }}>
                        {inv.invoiceNumber}
                      </p>
                      <p className="text-[10px]" style={{ color: "#94a3b8" }}>
                        {inv.patientName ?? "—"}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-bold" style={{ color: NAVY }}>
                        {fmtRial(inv.totalAmount)}
                      </span>
                      <span
                        className="text-[10px] font-bold px-2 py-0.5 rounded-full"
                        style={{ background: st.bg, color: st.color }}
                      >
                        {st.label}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="text-center py-6 text-xs" style={{ color: "#94a3b8" }}>
              لا توجد فواتير حديثة
            </div>
          )}
        </div>
      </div>

      {/* ── Quick Stats Row ── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <QuickStat
          icon={IndianRupee}
          label="إجمالي مستحق"
          value={fmtRial(summary?.totalOutstanding)}
          color="#b91c1c"
        />
        <QuickStat
          icon={FileText}
          label="فواتير معلّقة"
          value={String(summary?.unpaidInvoicesCount ?? 0)}
          color="#d97706"
        />
        <QuickStat
          icon={FileText}
          label="فواتير مسودة"
          value={String(summary?.draftInvoicesCount ?? 0)}
          color="#6b7280"
        />
        <QuickStat
          icon={TrendingUp}
          label="عمولات معلّقة"
          value={fmtRial(summary?.pendingCommissionsAmount)}
          color="#7c3aed"
        />
      </div>
    </div>
  );
}

/* ─── Sub-components ────────────────────────────────────────────────────────── */

function SummaryCard({
  icon: Icon,
  label,
  value,
  color,
  bg,
  trend,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  color: string;
  bg: string;
  trend?: "up" | "down";
}) {
  return (
    <div className="rounded-xl border p-4" style={{ background: "#fff", borderColor: "#e5e7eb" }}>
      <div className="flex items-center gap-2 mb-2">
        <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: bg }}>
          <Icon className="w-4 h-4" style={{ color }} />
        </div>
        {trend === "up" && <ArrowUpRight className="w-3 h-3" style={{ color: "#16a34a" }} />}
        {trend === "down" && <ArrowDownRight className="w-3 h-3" style={{ color: "#dc2626" }} />}
      </div>
      <p className="text-[10px] font-medium mb-0.5" style={{ color: "#94a3b8" }}>
        {label}
      </p>
      <p className="text-sm font-extrabold" style={{ color: NAVY }}>
        {value}
      </p>
    </div>
  );
}

function QuickStat({
  icon: Icon,
  label,
  value,
  color,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div
      className="rounded-lg border px-3 py-2.5 flex items-center gap-2"
      style={{ background: "#fff", borderColor: "#e5e7eb" }}
    >
      <Icon className="w-4 h-4 flex-shrink-0" style={{ color }} />
      <div>
        <p className="text-[10px]" style={{ color: "#94a3b8" }}>
          {label}
        </p>
        <p className="text-xs font-bold" style={{ color: NAVY }}>
          {value}
        </p>
      </div>
    </div>
  );
}
