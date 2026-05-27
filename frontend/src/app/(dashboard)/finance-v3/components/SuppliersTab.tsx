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
import { formatYER, extractErrorMessage } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 9: Suppliers
   ═══════════════════════════════════════════════════════════════════════════════ */
export function SuppliersTab() {
  const [suppliers, setSuppliers] = useState<SupplierListItem[]>([]);
  const [bills, setBills] = useState<SupplierBill[]>([]);
  const [loading, setLoading] = useState(true);
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
      const [sRes, bRes] = await Promise.all([
        api.get<{ data: SupplierListItem[]; total: number }>("/api/suppliers"),
        api.get<{ data: SupplierBill[]; total: number }>("/api/supplier-bills"),
      ]);
      setSuppliers(sRes.data.data ?? (Array.isArray(sRes.data) ? sRes.data as unknown as SupplierListItem[] : []));
      setBills(bRes.data.data ?? (Array.isArray(bRes.data) ? bRes.data as unknown as SupplierBill[] : []));
    } catch { toast.error("فشل في تحميل بيانات الموردين"); } finally { setLoading(false); }
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
        SupplierId: bSupplier,
        Description: bDesc,
        TotalAmount: Number(bAmount),
        DueDate: bDueDate,
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
    if (Number(payAmount) > showPayBill.Balance) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(showPayBill.Balance)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: PaySupplierBillRequest = {
        Amount: Number(payAmount),
        PaymentMethod: payMethod,
      };
      await api.post(`/api/finance-v3/supplier-bills/${showPayBill.Id}/pay`, payload);
      toast.success("تم سداد القسط بنجاح");
      setShowPayBill(null);
      setPayAmount(""); setPayMethod("cash");
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في سداد القسط")); } finally { setSubmitting(false); }
  };

  return (
    <div className="p-6 space-y-6">
      {/* Suppliers */}
      <div>
        <SectionHeader title="الموردون" action={
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        } />
        {loading ? <LoadingSkeleton /> : suppliers.length === 0 ? <EmptyState icon={Truck} message="لا يوجد موردون" /> : (
          <DataTable<SupplierListItem>
            keyField="Id"
            data={suppliers}
            columns={[
              { key: "Name", label: "الاسم" },
              { key: "ContactPerson", label: "جهة الاتصال", render: (r) => r.ContactPerson ?? "—" },
              { key: "Phone", label: "الهاتف", render: (r) => r.Phone ?? "—" },
              { key: "TotalBilled", label: "إجمالي الفواتير", render: (r) => formatYER(r.TotalBilled) },
              { key: "TotalPaid", label: "المدفوع", render: (r) => formatYER(r.TotalPaid) },
              { key: "Balance", label: "الرصيد", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
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
            keyField="Id"
            data={bills}
            columns={[
              { key: "SupplierName", label: "المورد" },
              { key: "Description", label: "الوصف" },
              { key: "TotalAmount", label: "الإجمالي", render: (r) => formatYER(r.TotalAmount) },
              { key: "PaidAmount", label: "المدفوع", render: (r) => formatYER(r.PaidAmount) },
              { key: "Balance", label: "المتبقي", render: (r) => <span style={{ color: r.Balance > 0 ? tokens.dangerBorder : tokens.successBorder, fontWeight: 700 }}>{formatYER(r.Balance)}</span> },
              { key: "DueDate", label: "تاريخ الاستحقاق", render: (r) => new Date(r.DueDate).toLocaleDateString("ar-SA") },
              { key: "Status", label: "الحالة", render: (r) => <StatusBadge status={r.Status} /> },
              { key: "actions", label: "إجراءات", render: (r) => r.Balance > 0 ? (
                <button onClick={(e) => { e.stopPropagation(); setShowPayBill(r); setPayAmount(""); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.brand }} title="سداد قسط"><DollarSign className="w-3.5 h-3.5" /></button>
              ) : null },
            ]}
          />
        )}
      </div>

      {/* Create Supplier Bill Modal */}
      <Modal open={showCreateBill} onClose={() => setShowCreateBill(false)} title="فاتورة مورد جديدة">
        <div className="space-y-4">
          <div><label style={labelStyle}>المورد <span style={{ color: tokens.dangerBorder }}>*</span></label><select value={bSupplier} onChange={(e) => setBSupplier(e.target.value)} style={inputStyle}><option value="">— اختر —</option>{suppliers.map((s) => (<option key={s.Id} value={s.Id}>{s.Name}</option>))}</select></div>
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
      <Modal open={!!showPayBill} onClose={() => setShowPayBill(null)} title={`سداد قسط — ${showPayBill?.Description ?? ""}`}>
        {showPayBill && (
          <div className="space-y-4">
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
              <p className="text-xs" style={{ color: tokens.infoText }}>المتبقي: <strong>{formatYER(showPayBill.Balance)}</strong></p>
            </div>
            <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0" max={showPayBill.Balance} step="0.01" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} dir="ltr" style={inputStyle} />{Number(payAmount) > showPayBill.Balance && (<p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>⚠ المبلغ يتجاوز المبلغ المتبقي ({formatYER(showPayBill.Balance)})</p>)}</div>
            <div><label style={labelStyle}>طريقة الدفع</label><select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} style={inputStyle}>{PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}</select></div>
            <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setShowPayBill(null)} style={btnGhost}>إلغاء</button>
              <button onClick={handlePayBill} disabled={submitting || Number(payAmount) > showPayBill.Balance} style={{ ...btnPrimary, opacity: submitting || Number(payAmount) > showPayBill.Balance ? 0.6 : 1 }}>{submitting ? "جارٍ السداد..." : "سداد"}</button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
