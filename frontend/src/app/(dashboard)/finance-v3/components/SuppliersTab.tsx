"use client";

import { useState, useEffect, useCallback } from "react";
import {
  RefreshCw,
  Truck,
  Plus,
  FileText,
  DollarSign,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { SupplierListItem, SupplierBill, CreateSupplierBillRequest, PaySupplierBillRequest } from "./types";
import { PAYMENT_METHODS } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, StatusBadge, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate, safeArray } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 9: Suppliers
   Zero-State Resiliency: Safe array extraction, null-safe rendering
   ═══════════════════════════════════════════════════════════════════════════════ */
export function SuppliersTab() {
  const [suppliers, setSuppliers] = useState<SupplierListItem[]>([]);
  const [bills, setBills] = useState<SupplierBill[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreateBill, setShowCreateBill] = useState(false);
  const [showPayBill, setShowPayBill] = useState<SupplierBill | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Create bill form
  const [bSupplier, setBSupplier] = useState("");
  const [bDesc, setBDesc] = useState("");
  const [bAmount, setBAmount] = useState("");
  const [bDueDate, setBDueDate] = useState("");

  // Pay installment form
  const [payAmount, setPayAmount] = useState("");
  const [payMethod, setPayMethod] = useState("cash");

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [sRes, bRes] = await Promise.all([
        api.get<{ data: SupplierListItem[]; total: number }>("/api/finance-v3/suppliers"),
        api.get<{ data: SupplierBill[]; total: number }>("/api/finance-v3/supplier-bills"),
      ]);
      setSuppliers(safeArray(sRes.data?.data ?? (Array.isArray(sRes.data) ? sRes.data as unknown as SupplierListItem[] : undefined)));
      setBills(safeArray(bRes.data?.data ?? (Array.isArray(bRes.data) ? bRes.data as unknown as SupplierBill[] : undefined)));
    } catch (err) {
      setError(extractErrorMessage(err, "فشل في تحميل بيانات الموردين"));
      toast.error("فشل في تحميل بيانات الموردين");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreateBill = async () => {
    if (!bSupplier || !bDesc.trim() || !bAmount || Number(bAmount) <= 0 || !bDueDate) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateSupplierBillRequest = {
        supplierId: bSupplier,
        description: bDesc,
        totalAmount: Number(bAmount),
        dueDate: bDueDate,
      };
      await api.post("/api/finance-v3/supplier-bills", payload);
      toast.success("تم إنشاء فاتورة المورد بنجاح");
      setShowCreateBill(false);
      setBSupplier(""); setBDesc(""); setBAmount(""); setBDueDate("");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء الفاتورة")); } finally { setSubmitting(false); }
  };

  const handlePayBill = async () => {
    if (!showPayBill || !payAmount || Number(payAmount) <= 0) {
      toast.error("يرجى إدخال المبلغ");
      return;
    }
    if (Number(payAmount) > showPayBill.balance) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(showPayBill.balance)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: PaySupplierBillRequest = {
        amount: Number(payAmount),
        paymentMethod: payMethod,
      };
      await api.post(`/api/finance-v3/supplier-bills/${showPayBill.id}/pay`, payload);
      toast.success("تم سداد القسط بنجاح");
      setShowPayBill(null);
      setPayAmount(""); setPayMethod("cash");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في سداد القسط")); } finally { setSubmitting(false); }
  };

  return (
    <div className="p-6 space-y-6">
      {error && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
          <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
          <button onClick={fetchData} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
        </div>
      )}
      {/* Suppliers */}
      <div>
        <SectionHeader title="الموردون" action={
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        } />
        {loading ? <LoadingSkeleton /> : suppliers.length === 0 ? <EmptyState icon={Truck} message="لا يوجد موردون" /> : (
          <DataTable<SupplierListItem>
            keyField="id"
            data={suppliers}
            columns={[
              { key: "name", label: "الاسم" },
              { key: "contactPerson", label: "جهة الاتصال", render: (r) => r.contactPerson ?? "—" },
              { key: "phone", label: "الهاتف", render: (r) => r.phone ?? "—" },
              { key: "totalBilled", label: "إجمالي الفواتير", render: (r) => formatYER(r.totalBilled) },
              { key: "totalPaid", label: "المدفوع", render: (r) => formatYER(r.totalPaid) },
              { key: "balance", label: "الرصيد", render: (r) => <span style={{ color: r.balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.balance)}</span> },
            ]}
          />
        )}
      </div>

      {/* Supplier Bills */}
      <div>
        <SectionHeader title="فواتير الموردين" action={
          <button onClick={() => setShowCreateBill(true)} style={btnPrimary}><Plus className="w-4 h-4" /> فاتورة جديدة</button>
        } />
        {loading ? <LoadingSkeleton /> : bills.length === 0 ? <EmptyState icon={FileText} message="لا توجد فواتير موردين" /> : (
          <DataTable<SupplierBill>
            keyField="id"
            data={bills}
            columns={[
              { key: "supplierName", label: "المورد" },
              { key: "description", label: "الوصف" },
              { key: "totalAmount", label: "الإجمالي", render: (r) => formatYER(r.totalAmount) },
              { key: "paidAmount", label: "المدفوع", render: (r) => formatYER(r.paidAmount) },
              { key: "balance", label: "المتبقي", render: (r) => <span style={{ color: r.balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.balance)}</span> },
              { key: "dueDate", label: "تاريخ الاستحقاق", render: (r) => safeFormatDate(r.dueDate) },
              { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
              { key: "actions", label: "إجراءات", render: (r) => r.balance > 0 ? (
                <button onClick={(e) => { e.stopPropagation(); setShowPayBill(r); setPayAmount(""); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.brand }} title="سداد قسط"><DollarSign className="w-3.5 h-3.5" /></button>
              ) : null },
            ]}
          />
        )}
      </div>

      {/* Create Supplier Bill Modal */}
      <Modal open={showCreateBill} onClose={() => setShowCreateBill(false)} title="فاتورة مورد جديدة">
        <div className="space-y-4">
          <div><label style={labelStyle}>المورد <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={bSupplier} onChange={(e) => setBSupplier(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{suppliers.map((s) => (<option key={s.id} value={s.id}>{s.name}</option>))}</select></div>
          <div><label style={labelStyle}>الوصف <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={bDesc} onChange={(e) => setBDesc(e.target.value)} placeholder="وصف الفاتورة" style={inputStyle} /></div>
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0.01" step="0.01" value={bAmount} onChange={(e) => setBAmount(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div><label style={labelStyle}>تاريخ الاستحقاق <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="date" value={bDueDate} onChange={(e) => setBDueDate(e.target.value)} style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateBill(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreateBill} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء"}</button>
          </div>
        </div>
      </Modal>

      {/* Pay Installment Modal */}
      <Modal open={!!showPayBill} onClose={() => setShowPayBill(null)} title={`سداد قسط — ${showPayBill?.description ?? ""}`}>
        {showPayBill && (
          <div className="space-y-4">
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <p className="text-xs" style={{ color: tokens.infoText }}>المتبقي: <strong>{formatYER(showPayBill.balance)}</strong></p>
            </div>
            <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0" max={showPayBill.balance} step="0.01" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} dir="ltr" style={inputStyle} />{Number(payAmount) > showPayBill.balance && (<p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز المبلغ المتبقي ({formatYER(showPayBill.balance)})</p>)}</div>
            <div><label style={labelStyle}>طريقة الدفع</label><select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} style={inputStyle}>{PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}</select></div>
            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setShowPayBill(null)} style={btnGhost}>إلغاء</button>
              <button onClick={handlePayBill} disabled={submitting || Number(payAmount) > showPayBill.balance} style={{ ...btnPrimary, opacity: submitting || Number(payAmount) > showPayBill.balance ? 0.6 : 1 }}>{submitting ? "جارٍ السداد..." : "سداد"}</button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
