"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Receipt,
  Plus,
  RefreshCw,
  Trash2,
  Printer,
  Loader2,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { PaymentListItem, RegisterPaymentRequest, InvoiceListItem, ContractListItem } from "./types";
import { PAYMENT_METHODS } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate, safeArray } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 4: Collections
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function CollectionsTab() {
  const [payments, setPayments] = useState<PaymentListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showRegister, setShowRegister] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Register payment form state
  const [patientSearch, setPatientSearch] = useState("");
  const [patientOptions, setPatientOptions] = useState<{ id: string; fullName: string; patientNumber: string }[]>([]);
  const [selectedPatient, setSelectedPatient] = useState("");
  const [invoiceOptions, setInvoiceOptions] = useState<{ id: string; invoiceNumber: string; balance: number }[]>([]);
  const [contractOptions, setContractOptions] = useState<{ id: string; contractNumber: string; outstandingAmount: number }[]>([]);
  const [selectedInvoice, setSelectedInvoice] = useState("");
  const [selectedContract, setSelectedContract] = useState("");
  const [payAmount, setPayAmount] = useState("");
  const [payMethod, setPayMethod] = useState("cash");
  const [payNotes, setPayNotes] = useState("");

  const fetchPayments = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data: responseData } = await api.get<{ data: PaymentListItem[]; total: number }>("/api/finance-v3/payments");
      setPayments(safeArray(responseData?.data));
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل التحصيلات"));
      toast.error("فشل في تحميل التحصيلات");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchPayments(); }, [fetchPayments]);

  // Patient search
  const searchPatients = useCallback(async (q: string) => {
    if (q.length < 2) { setPatientOptions([]); return; }
    try {
      const { data } = await api.get<{ data: { id: string; fullName: string; patientNumber: string }[] }>("/api/patients", { params: { search: q, pageSize: 10 } });
      setPatientOptions(data.data ?? data as unknown as { id: string; fullName: string; patientNumber: string }[] ?? []);
    } catch { /* ignore */ }
  }, []);

  const onPatientSelect = async (patientId: string) => {
    setSelectedPatient(patientId);
    setSelectedInvoice("");
    setSelectedContract("");
    try {
      const [invRes, conRes] = await Promise.all([
        api.get<{ data: InvoiceListItem[] }>(`/api/patients/${patientId}/invoices`),
        api.get<ContractListItem[]>(`/api/patients/${patientId}/contracts`),
      ]);
      const invData = invRes.data?.data ?? invRes.data as unknown as InvoiceListItem[];
      setInvoiceOptions((Array.isArray(invData) ? invData : []).filter((i) => i.balance > 0).map((i) => ({ id: i.id, invoiceNumber: i.invoiceNumber, balance: i.balance })));
      const conData = Array.isArray(conRes.data) ? conRes.data : (conRes.data as { data?: ContractListItem[] }).data ?? [];
      setContractOptions(conData.filter((c) => c.outstandingAmount > 0).map((c) => ({ id: c.id, contractNumber: c.contractNumber, outstandingAmount: c.outstandingAmount })));
    } catch { /* ignore */ }
  };

  // Overpayment guard
  const maxAmount = (() => {
    if (selectedInvoice) { const inv = invoiceOptions.find((i) => i.id === selectedInvoice); return inv?.balance ?? 0; }
    if (selectedContract) { const con = contractOptions.find((c) => c.id === selectedContract); return con?.outstandingAmount ?? 0; }
    return 0;
  })();

  const handleRegister = async () => {
    if (!selectedPatient || !payAmount || Number(payAmount) <= 0) {
      toast.error("يرجى اختيار المريض وإدخال المبلغ");
      return;
    }
    if (maxAmount > 0 && Number(payAmount) > maxAmount) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(maxAmount)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: RegisterPaymentRequest = {
        patientId: selectedPatient,
        amount: Number(payAmount),
        paymentMethod: payMethod,
        notes: payNotes || undefined,
      };
      // Only include invoiceId/contractId when actually selected (not empty string)
      if (selectedInvoice) payload.invoiceId = selectedInvoice;
      if (selectedContract) payload.contractId = selectedContract;
      // Debug-safe console log in development only
      if (process.env.NODE_ENV === "development") {
        // eslint-disable-next-line no-console
        console.debug("[CollectionsTab] payment payload:", { ...payload, amount: payload.amount });
      }
      await api.post("/api/finance-v3/payments", payload);
      toast.success("تم تسجيل الدفعة بنجاح");
      resetForm();
      setShowRegister(false);
      fetchPayments();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تسجيل الدفعة")); } finally { setSubmitting(false); }
  };

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
    setPatientSearch(""); setSelectedPatient(""); setPatientOptions([]);
    setInvoiceOptions([]); setContractOptions([]);
    setSelectedInvoice(""); setSelectedContract("");
    setPayAmount(""); setPayMethod("cash"); setPayNotes("");
  };

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="التحصيل" action={
        <div className="flex items-center gap-2">
          <button onClick={() => { resetForm(); setShowRegister(true); }} style={btnPrimary}><Plus className="w-4 h-4" /> تسجيل دفعة</button>
          <button onClick={fetchPayments} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : error ? (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
          <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
          <button onClick={fetchPayments} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
        </div>
      ) : payments.length === 0 ? <EmptyState icon={Receipt} message="لا توجد تحصيلات" /> : (
        <DataTable<PaymentListItem>
          keyField="id"
          data={payments}
          columns={[
            { key: "paymentNumber", label: "رقم الإيصال", render: (r) => r.paymentNumber ?? "" },
            { key: "patientName", label: "المريض" },
            { key: "amount", label: "المبلغ", render: (r) => formatYER(r.amount ?? 0) },
            { key: "paymentMethod", label: "طريقة الدفع", render: (r) => PAYMENT_METHODS.find((m) => m.value === (r.paymentMethod ?? ""))?.label ?? r.paymentMethod },
            { key: "paymentDate", label: "التاريخ", render: (r) => safeFormatDate(r.paymentDate ?? "") },
            { key: "isReversal", label: "عكسي", render: (r) => r.isReversal ? <span style={{ color: tokens.dangerBorder, fontWeight: 700 }}>نعم</span> : "—" },
            { key: "actions", label: "إجراءات", render: (r) => !r.isReversal && !r.reversedById ? (
              <div className="flex items-center gap-1">
                <button onClick={(e) => { e.stopPropagation(); setConfirmDelete(r.id); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="عكس الدفعة"><Trash2 className="w-3.5 h-3.5" /></button>
                <button onClick={(e) => { e.stopPropagation(); window.print(); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.brand }} title="طباعة إيصال"><Printer className="w-3.5 h-3.5" /></button>
              </div>
            ) : null },
          ]}
        />
      )}

      {/* Register Payment Modal */}
      <Modal open={showRegister} onClose={() => setShowRegister(false)} title="تسجيل دفعة جديدة" wide>
        <div className="space-y-4">
          {/* Patient search */}
          <div>
            <label style={labelStyle}>المريض <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              value={patientSearch}
              onChange={(e) => { setPatientSearch(e.target.value); searchPatients(e.target.value); }}
              placeholder="ابحث بالاسم أو الرقم..."
              style={inputStyle}
            />
            {patientOptions.length > 0 && !selectedPatient && (
              <div className="mt-1 rounded-md border max-h-40 overflow-y-auto" style={{ borderColor: tokens.border }}>
                {patientOptions.map((p) => (
                  <button key={p.id} onClick={() => { setPatientSearch(`${p.fullName} (${p.patientNumber})`); onPatientSelect(p.id); }} className="w-full text-right px-3 py-2 text-sm transition-colors" style={{ color: tokens.textPrimary }} onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.cardHover; }} onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}>
                    {p.fullName} ({p.patientNumber})
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Select invoice or contract */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label style={labelStyle}>فاتورة</label>
              <select value={selectedInvoice} onChange={(e) => { setSelectedInvoice(e.target.value); setSelectedContract(""); }} style={inputStyle}>
                <option value="">— اختر فاتورة —</option>
                {invoiceOptions.map((i) => (<option key={i.id} value={i.id}>{i.invoiceNumber} ({formatYER(i.balance)})</option>))}
              </select>
            </div>
            <div>
              <label style={labelStyle}>عقد</label>
              <select value={selectedContract} onChange={(e) => { setSelectedContract(e.target.value); setSelectedInvoice(""); }} style={inputStyle}>
                <option value="">— اختر عقد —</option>
                {contractOptions.map((c) => (<option key={c.id} value={c.id}>{c.contractNumber} ({formatYER(c.outstandingAmount)})</option>))}
              </select>
            </div>
          </div>

          {/* Amount */}
          <div>
            <label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input type="number" min="0" max={maxAmount || undefined} step="0.01" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} placeholder="0" dir="ltr" style={inputStyle} />
            {maxAmount > 0 && Number(payAmount) > maxAmount && (
              <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز المستحق ({formatYER(maxAmount)})</p>
            )}
          </div>

          {/* Payment method */}
          <div>
            <label style={labelStyle}>طريقة الدفع</label>
            <select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} style={inputStyle}>
              {PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}
            </select>
          </div>

          {/* Notes */}
          <div>
            <label style={labelStyle}>ملاحظات</label>
            <input value={payNotes} onChange={(e) => setPayNotes(e.target.value)} placeholder="ملاحظات اختيارية..." style={inputStyle} />
          </div>

          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowRegister(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleRegister} disabled={submitting || (maxAmount > 0 && Number(payAmount) > maxAmount)} style={{ ...btnPrimary, opacity: submitting || (maxAmount > 0 && Number(payAmount) > maxAmount) ? 0.6 : 1 }}>
              {submitting && <Loader2 className="w-4 h-4 animate-spin" />}
              {submitting ? "جارٍ الحفظ..." : "تسجيل الدفعة"}
            </button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)} onConfirm={handleDelete} title="عكس الدفعة" message="هل أنت متأكد من عكس هذه الدفعة؟ سيتم إنشاء قيد عكسي." confirmLabel="عكس الدفعة" danger />
    </div>
  );
}
