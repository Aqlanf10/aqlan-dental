"use client";

import { useState, useEffect, useCallback } from "react";
import {
  RefreshCw,
  Truck,
  Plus,
  FileText,
  DollarSign,
  Users,
  CircleDollarSign,
  Search,
  Eye,
  Loader2,
  Wallet,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type {
  SupplierDto,
  SupplierStatement,
  SupplierBillDto,
  CreateSupplierRequest,
  PaySupplierBillRequest,
  SupplierType,
  Treasury,
} from "./types";
import { SUPPLIER_TYPE_LABELS, SUPPLIER_TYPE_OPTIONS, PAYMENT_METHODS } from "./types";
import {
  LoadingSkeleton,
  EmptyState,
  DataTable,
  Modal,
  StatusBadge,
  KpiCard,
  tokens,
  inputStyle,
  labelStyle,
  btnPrimary,
  btnGhost,
} from "./FinanceSharedUI";
import { formatYER, extractErrorMessage, safeFormatDate } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 9: Suppliers — الموردون والمعامل
   ═══════════════════════════════════════════════════════════════════════════════
   شاشة شاملة تعرض:
   - بطاقات ملخص مالي (عدد الموردين، إجمالي الديون)
   - جدول الموردين مع أزرار كشف الحساب وسداد دفعة
   - نوافذ منبثقة: إضافة مورد، كشف حساب، سداد قسط، فاتورة جديدة
   ═══════════════════════════════════════════════════════════════════════════════ */

export function SuppliersTab() {
  /* ── State ── */
  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");

  // Modal states
  const [showCreateSupplier, setShowCreateSupplier] = useState(false);
  const [showStatement, setShowStatement] = useState<SupplierDto | null>(null);
  const [showCreateBill, setShowCreateBill] = useState<SupplierDto | null>(null);
  const [showPayBill, setShowPayBill] = useState<SupplierBillDto | null>(null);

  // Form: Create Supplier
  const [newName, setNewName] = useState("");
  const [newPhone, setNewPhone] = useState("");
  const [newContact, setNewContact] = useState("");
  const [newType, setNewType] = useState<SupplierType>("DentalLab");

  // Form: Create Bill
  const [billDesc, setBillDesc] = useState("");
  const [billAmount, setBillAmount] = useState("");
  const [billDueDate, setBillDueDate] = useState("");

  // Form: Pay Bill
  const [payAmount, setPayAmount] = useState("");
  const [payMethod, setPayMethod] = useState("cash");
  const [payTreasuryId, setPayTreasuryId] = useState<string>("");
  const [payNotes, setPayNotes] = useState("");
  const [payRefNumber, setPayRefNumber] = useState("");
  const [treasuries, setTreasuries] = useState<Treasury[]>([]);
  const [treasuriesLoading, setTreasuriesLoading] = useState(false);

  const [submitting, setSubmitting] = useState(false);

  /* ── Fetch suppliers from Finance V3 API ── */
  const fetchSuppliers = useCallback(async () => {
    try {
      setLoading(true);
      const res = await api.get<{ data: SupplierDto[]; total: number }>(
        "/api/finance-v3/suppliers"
      );
      const list = res.data.data ?? (Array.isArray(res.data) ? (res.data as unknown as SupplierDto[]) : []);
      setSuppliers(list);
    } catch {
      toast.error("فشل في تحميل بيانات الموردين");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSuppliers();
  }, [fetchSuppliers]);

  /* ── Computed values ── */
  const filteredSuppliers = suppliers.filter((s) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.trim().toLowerCase();
    return (
      s.name.toLowerCase().includes(q) ||
      (s.contactPerson && s.contactPerson.toLowerCase().includes(q)) ||
      (s.phone && s.phone.includes(q))
    );
  });

  const totalDebt = suppliers.reduce((sum, s) => sum + (s.balance ?? 0), 0);
  const suppliersWithDebt = suppliers.filter((s) => (s.balance ?? 0) > 0).length;

  /* ── Handlers ── */

  // Create Supplier
  const handleCreateSupplier = async () => {
    if (!newName.trim()) {
      toast.error("اسم المورد مطلوب");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateSupplierRequest = {
        name: newName.trim(),
        contactPerson: newContact.trim() || undefined,
        phone: newPhone.trim() || undefined,
        type: newType,
      };
      await api.post("/api/finance-v3/suppliers", payload);
      toast.success("تم إنشاء المورد/المعمل بنجاح");
      setShowCreateSupplier(false);
      resetCreateSupplierForm();
      fetchSuppliers();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في إنشاء المورد"));
    } finally {
      setSubmitting(false);
    }
  };

  // Create Supplier Bill
  const handleCreateBill = async () => {
    if (!showCreateBill) return;
    if (!billDesc.trim() || !billAmount || Number(billAmount) <= 0 || !billDueDate) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      setSubmitting(true);
      await api.post(`/api/finance-v3/suppliers/${showCreateBill.id}/bills`, {
        description: billDesc.trim(),
        totalAmount: Number(billAmount),
        dueDate: billDueDate,
      });
      toast.success("تم تسجيل فاتورة المورد بنجاح");
      setShowCreateBill(null);
      resetBillForm();
      fetchSuppliers();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في تسجيل الفاتورة"));
    } finally {
      setSubmitting(false);
    }
  };

  // Pay Bill Installment
  const handlePayBill = async () => {
    if (!showPayBill) return;
    if (!payAmount || Number(payAmount) <= 0) {
      toast.error("يرجى إدخال مبلغ صحيح");
      return;
    }
    if (Number(payAmount) > showPayBill.remainingAmount) {
      toast.error(`المبلغ يتجاوز المستحق (${formatYER(showPayBill.remainingAmount)})`);
      return;
    }
    try {
      setSubmitting(true);
      const payload: PaySupplierBillRequest = {
        amount: Number(payAmount),
        paymentMethod: payMethod,
        treasuryId: payTreasuryId || undefined,
        notes: payNotes.trim() || undefined,
        referenceNumber: payRefNumber.trim() || undefined,
      };
      await api.post(`/api/finance-v3/suppliers/bills/${showPayBill.id}/pay`, payload);
      toast.success("تم سداد القسط بنجاح وترحيل القيد المحاسبي");
      setShowPayBill(null);
      resetPayForm();
      fetchSuppliers();
      // Refresh statement if open
      if (showStatement) {
        loadStatement(showStatement.id);
      }
      // Refresh treasuries in case balance changed
      fetchTreasuries();
    } catch (err) {
      toast.error(extractErrorMessage(err, "فشل في سداد القسط"));
    } finally {
      setSubmitting(false);
    }
  };

  // Load supplier account statement
  const [statementData, setStatementData] = useState<SupplierStatement | null>(null);
  const [statementLoading, setStatementLoading] = useState(false);

  const loadStatement = async (supplierId: string) => {
    try {
      setStatementLoading(true);
      const res = await api.get<SupplierStatement>(
        `/api/finance-v3/suppliers/${supplierId}/bills`
      );
      setStatementData(res.data);
    } catch {
      toast.error("فشل في تحميل كشف الحساب");
    } finally {
      setStatementLoading(false);
    }
  };

  const openStatement = (supplier: SupplierDto) => {
    setShowStatement(supplier);
    loadStatement(supplier.id);
  };

  /* ── Form reset helpers ── */
  const resetCreateSupplierForm = () => {
    setNewName("");
    setNewPhone("");
    setNewContact("");
    setNewType("DentalLab");
  };

  const resetBillForm = () => {
    setBillDesc("");
    setBillAmount("");
    setBillDueDate("");
  };

  const resetPayForm = () => {
    setPayAmount("");
    setPayMethod("cash");
    setPayTreasuryId("");
    setPayNotes("");
    setPayRefNumber("");
  };

  /* ── Fetch treasuries for payment modal ── */
  const fetchTreasuries = useCallback(async () => {
    try {
      setTreasuriesLoading(true);
      const res = await api.get<{ data: Treasury[]; total: number }>("/api/finance-v3/treasuries");
      const list = res.data?.data ?? (Array.isArray(res.data) ? (res.data as unknown as Treasury[]) : []);
      setTreasuries(list);
    } catch {
      setTreasuries([]);
    } finally {
      setTreasuriesLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTreasuries();
  }, [fetchTreasuries]);

  /* ── Supplier type badge renderer ── */
  const renderTypeBadge = (type: SupplierType) => {
    const label = SUPPLIER_TYPE_LABELS[type] ?? type;
    const colorMap: Record<SupplierType, { bg: string; text: string }> = {
      DentalLab: { bg: "#deecf9", text: "#0078d4" },
      MedicalVendor: { bg: "#dff6dd", text: "#107c10" },
      GeneralService: { bg: "#fff4ce", text: "#8a6914" },
    };
    const c = colorMap[type] ?? { bg: tokens.cardHover, text: tokens.textSecondary };
    return (
      <span
        className="inline-flex text-[11px] font-semibold px-2 py-0.5 rounded-full"
        style={{ backgroundColor: c.bg, color: c.text }}
      >
        {label}
      </span>
    );
  };

  /* ═══════════════════════════════════════════════════════════════════════════
     Render
     ═══════════════════════════════════════════════════════════════════════════ */
  return (
    <div className="p-6 space-y-6">
      {/* ── KPI Summary Cards ── */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <KpiCard
          label="إجمالي الموردين والمعامل"
          value={String(suppliers.length)}
          sublabel={suppliersWithDebt > 0 ? `${suppliersWithDebt} لديهم ديون مستحقة` : "لا توجد ديون"}
          color={tokens.brand}
          icon={<Users className="w-4 h-4" style={{ color: tokens.brand }} />}
        />
        <KpiCard
          label="إجمالي الديون المستحقة"
          value={formatYER(totalDebt)}
          sublabel="لنا ديون على الموردين"
          color={totalDebt > 0 ? "#d13438" : "#107c10"}
          icon={<CircleDollarSign className="w-4 h-4" style={{ color: totalDebt > 0 ? "#d13438" : "#107c10" }} />}
        />
        <KpiCard
          label="إجمالي المبالغ المدفوعة"
          value={formatYER(suppliers.reduce((s, x) => s + (x.totalPaid ?? 0), 0))}
          sublabel="للموردين والمعامل"
          color="#107c10"
          icon={<DollarSign className="w-4 h-4" style={{ color: "#107c10" }} />}
        />
      </div>

      {/* ── Search & Action Bar ── */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        <div className="flex items-center gap-3 flex-1 w-full sm:w-auto">
          <h2 className="text-sm font-bold whitespace-nowrap" style={{ color: tokens.textPrimary }}>
            سجل المعامل والموردين
          </h2>
          <div className="relative flex-1 max-w-xs">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5" style={{ color: tokens.textTertiary }} />
            <input
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="بحث بالاسم أو الهاتف..."
              className="w-full pr-9 pl-3 py-2 rounded-md text-xs"
              style={{ ...inputStyle, fontSize: 12 }}
            />
          </div>
        </div>
        <div className="flex gap-2">
          <button onClick={fetchSuppliers} className="w-8 h-8 rounded-md flex items-center justify-center transition-colors" style={{ color: tokens.textSecondary, border: `1px solid ${tokens.border}` }} title="تحديث">
            <RefreshCw className="w-3.5 h-3.5" />
          </button>
          <button onClick={() => setShowCreateSupplier(true)} style={btnPrimary}>
            <Plus className="w-4 h-4" /> إضافة مورد جديد
          </button>
        </div>
      </div>

      {/* ── Suppliers Data Table ── */}
      {loading ? (
        <LoadingSkeleton rows={5} />
      ) : filteredSuppliers.length === 0 ? (
        <EmptyState icon={Truck} message={searchQuery ? "لا توجد نتائج مطابقة" : "لا يوجد موردون مسجلين"} />
      ) : (
        <DataTable<SupplierDto>
          keyField="id"
          data={filteredSuppliers}
          columns={[
            {
              key: "name",
              label: "اسم الجهة",
              render: (r) => (
                <div>
                  <span className="font-semibold" style={{ color: tokens.textPrimary }}>{r.name}</span>
                  {r.contactPerson && (
                    <p className="text-[11px]" style={{ color: tokens.textTertiary }}>{r.contactPerson}</p>
                  )}
                </div>
              ),
            },
            {
              key: "type",
              label: "النوع",
              render: (r) => renderTypeBadge(r.type ?? "MedicalVendor"),
            },
            {
              key: "phone",
              label: "الهاتف",
              render: (r) => r.phone ?? "—",
            },
            {
              key: "totalBilled",
              label: "إجمالي الفواتير",
              render: (r) => formatYER(r.totalBilled ?? 0),
            },
            {
              key: "totalPaid",
              label: "المدفوع",
              render: (r) => formatYER(r.totalPaid ?? 0),
            },
            {
              key: "balance",
              label: "الرصيد المستحق",
              render: (r) => (
                <span style={{ color: (r.balance ?? 0) > 0 ? "#d13438" : "#107c10", fontWeight: 700 }}>
                  {formatYER(r.balance ?? 0)}
                </span>
              ),
            },
            {
              key: "actions",
              label: "الإجراءات",
              render: (r) => (
                <div className="flex items-center gap-1">
                  <button
                    onClick={(e) => { e.stopPropagation(); openStatement(r); }}
                    className="text-[11px] font-medium px-2.5 py-1 rounded-md transition-colors"
                    style={{ color: tokens.brand, backgroundColor: `${tokens.brand}10` }}
                    title="كشف الحساب"
                  >
                    <Eye className="w-3 h-3 inline ml-1" />
                    كشف الحساب
                  </button>
                  {(r.balance ?? 0) > 0 && (
                    <button
                      onClick={(e) => { e.stopPropagation(); setShowCreateBill(r); resetBillForm(); }}
                      className="text-[11px] font-medium px-2.5 py-1 rounded-md transition-colors"
                      style={{ color: "#8a6914", backgroundColor: "#fff4ce" }}
                      title="فاتورة جديدة"
                    >
                      <FileText className="w-3 h-3 inline ml-1" />
                      فاتورة
                    </button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {/* ═══════════════════════════════════════════════════════════════════════════
          MODALS
          ═══════════════════════════════════════════════════════════════════════════ */}

      {/* ── Modal: Create Supplier ── */}
      <Modal open={showCreateSupplier} onClose={() => setShowCreateSupplier(false)} title="إضافة مورد/معمل جديد">
        <div className="space-y-4">
          <div>
            <label style={labelStyle}>الاسم <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="مثال: معمل الابتسامة الرقمية"
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>النوع</label>
            <select
              value={newType}
              onChange={(e) => setNewType(e.target.value as SupplierType)}
              style={inputStyle}
            >
              {SUPPLIER_TYPE_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={labelStyle}>رقم الهاتف</label>
            <input
              value={newPhone}
              onChange={(e) => setNewPhone(e.target.value)}
              placeholder="777123456"
              dir="ltr"
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>جهة الاتصال</label>
            <input
              value={newContact}
              onChange={(e) => setNewContact(e.target.value)}
              placeholder="اسم الشخص المسؤول"
              style={inputStyle}
            />
          </div>
          <div className="flex gap-3 pt-3 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateSupplier(false)} style={btnGhost}>إلغاء</button>
            <button
              onClick={handleCreateSupplier}
              disabled={submitting || !newName.trim()}
              style={{ ...btnPrimary, opacity: submitting || !newName.trim() ? 0.6 : 1 }}
            >
              {submitting ? "جارٍ الحفظ..." : "إنشاء المورد"}
            </button>
          </div>
        </div>
      </Modal>

      {/* ── Modal: Supplier Account Statement (كشف الحساب) ── */}
      <Modal open={!!showStatement} onClose={() => { setShowStatement(null); setStatementData(null); }} title={`كشف حساب — ${showStatement?.name ?? ""}`} wide>
        {statementLoading ? (
          <LoadingSkeleton rows={4} />
        ) : statementData ? (
          <div className="space-y-4">
            {/* Statement summary */}
            <div className="rounded-md p-4 grid grid-cols-2 sm:grid-cols-3 gap-4" style={{ backgroundColor: tokens.cardHover, border: `1px solid ${tokens.border}` }}>
              <div>
                <p className="text-[11px]" style={{ color: tokens.textTertiary }}>اسم الجهة</p>
                <p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{statementData.supplierName}</p>
              </div>
              <div>
                <p className="text-[11px]" style={{ color: tokens.textTertiary }}>الرصيد الحالي</p>
                <p className="text-sm font-bold" style={{ color: statementData.balance > 0 ? "#d13438" : "#107c10" }}>
                  {formatYER(statementData.balance)}
                </p>
              </div>
              <div>
                <p className="text-[11px]" style={{ color: tokens.textTertiary }}>عدد الفواتير</p>
                <p className="text-sm font-bold" style={{ color: tokens.textPrimary }}>{statementData.bills?.length ?? 0}</p>
              </div>
            </div>

            {/* Bills table */}
            {!statementData.bills || statementData.bills.length === 0 ? (
              <EmptyState icon={FileText} message="لا توجد فواتير مسجلة" />
            ) : (
              <DataTable<SupplierBillDto>
                keyField="id"
                data={statementData.bills}
                columns={[
                  { key: "billNumber", label: "رقم الفاتورة" },
                  { key: "description", label: "الوصف", render: (r) => r.description || "—" },
                  { key: "totalAmount", label: "الإجمالي", render: (r) => formatYER(r.totalAmount) },
                  { key: "paidAmount", label: "المدفوع", render: (r) => formatYER(r.paidAmount) },
                  {
                    key: "remainingAmount",
                    label: "المتبقي",
                    render: (r) => (
                      <span style={{ color: r.remainingAmount > 0 ? "#d13438" : "#107c10", fontWeight: 700 }}>
                        {formatYER(r.remainingAmount)}
                      </span>
                    ),
                  },
                  {
                    key: "dueDate",
                    label: "تاريخ الاستحقاق",
                    render: (r) => safeFormatDate(r.dueDate),
                  },
                  {
                    key: "status",
                    label: "الحالة",
                    render: (r) => <StatusBadge status={r.status} />,
                  },
                  {
                    key: "actions",
                    label: "إجراء",
                    render: (r) =>
                      r.remainingAmount > 0 ? (
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            setShowPayBill(r);
                            setPayAmount("");
                            setPayMethod("cash");
                          }}
                          className="text-[11px] font-medium px-3 py-1 rounded-md transition-colors"
                          style={{ color: "#d13438", backgroundColor: "#fde7e9" }}
                        >
                          <DollarSign className="w-3 h-3 inline ml-1" />
                          سداد دفعة
                        </button>
                      ) : null,
                  },
                ]}
              />
            )}

            <div className="flex justify-start pt-2">
              <button onClick={() => { setShowStatement(null); setStatementData(null); }} style={btnGhost}>
                إغلاق
              </button>
            </div>
          </div>
        ) : null}
      </Modal>

      {/* ── Modal: Create Supplier Bill ── */}
      <Modal open={!!showCreateBill} onClose={() => setShowCreateBill(null)} title={`فاتورة جديدة — ${showCreateBill?.name ?? ""}`}>
        <div className="space-y-4">
          <div className="rounded-md p-3" style={{ backgroundColor: tokens.infoBg, border: `1px solid ${tokens.infoBorder}` }}>
            <p className="text-xs" style={{ color: tokens.infoText }}>
              المورد: <strong>{showCreateBill?.name}</strong> — الرصيد الحالي: <strong>{formatYER(showCreateBill?.balance ?? 0)}</strong>
            </p>
          </div>
          <div>
            <label style={labelStyle}>وصف الفاتورة <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              value={billDesc}
              onChange={(e) => setBillDesc(e.target.value)}
              placeholder="مثال: تركة زركونيا - 12 وحدة"
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>المبلغ الإجمالي <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              type="number"
              min="0.01"
              step="0.01"
              value={billAmount}
              onChange={(e) => setBillAmount(e.target.value)}
              dir="ltr"
              style={inputStyle}
            />
          </div>
          <div>
            <label style={labelStyle}>تاريخ الاستحقاق <span style={{ color: tokens.dangerBorder }}>*</span></label>
            <input
              type="date"
              value={billDueDate}
              onChange={(e) => setBillDueDate(e.target.value)}
              style={inputStyle}
            />
          </div>
          <div className="flex gap-3 pt-3 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreateBill(null)} style={btnGhost}>إلغاء</button>
            <button
              onClick={handleCreateBill}
              disabled={submitting || !billDesc.trim() || !billAmount || !billDueDate}
              style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}
            >
              {submitting ? "جارٍ التسجيل..." : "تسجيل الفاتورة"}
            </button>
          </div>
        </div>
      </Modal>

      {/* ── Modal: Pay Bill Installment (Advanced — with Treasury selection) ── */}
      <Modal open={!!showPayBill} onClose={() => setShowPayBill(null)} title={`سداد قسط — ${showPayBill?.billNumber ?? ""}`}>
        {showPayBill && (
          <div className="space-y-4">
            {/* Bill info banner */}
            <div className="rounded-md p-3" style={{ backgroundColor: tokens.warningBg, border: `1px solid ${tokens.warningBorder}` }}>
              <p className="text-xs" style={{ color: tokens.warningText }}>
                الفاتورة: <strong>{showPayBill.billNumber}</strong> — المتبقي: <strong>{formatYER(showPayBill.remainingAmount)}</strong>
              </p>
            </div>

            {/* Amount */}
            <div>
              <label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label>
              <input
                type="number"
                min="0"
                max={showPayBill.remainingAmount}
                step="0.01"
                value={payAmount}
                onChange={(e) => setPayAmount(e.target.value)}
                dir="ltr"
                style={inputStyle}
              />
              {Number(payAmount) > showPayBill.remainingAmount && (
                <p className="text-xs mt-1" style={{ color: tokens.dangerBorder }}>
                  المبلغ يتجاوز المتبقي ({formatYER(showPayBill.remainingAmount)})
                </p>
              )}
            </div>

            {/* Payment method */}
            <div>
              <label style={labelStyle}>طريقة الدفع</label>
              <select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} style={inputStyle}>
                {PAYMENT_METHODS.map((m) => (
                  <option key={m.value} value={m.value}>{m.label}</option>
                ))}
              </select>
            </div>

            {/* Treasury selector */}
            <div>
              <label style={labelStyle}>السحب من الخزينة <span style={{ color: tokens.dangerBorder }}>*</span></label>
              {treasuriesLoading ? (
                <div className="flex items-center gap-2 text-xs" style={{ color: tokens.textTertiary }}>
                  <Loader2 className="w-3.5 h-3.5 animate-spin" /> جاري تحميل الخزائن...
                </div>
              ) : treasuries.length === 0 ? (
                <div
                  className="rounded-md p-3 text-xs"
                  style={{ backgroundColor: tokens.dangerBg, color: tokens.dangerText, border: `1px solid ${tokens.dangerBorder}` }}
                >
                  لا توجد خزائن نشطة. يرجى إنشاء خزينة أولاً.
                </div>
              ) : (
                <select
                  value={payTreasuryId}
                  onChange={(e) => setPayTreasuryId(e.target.value)}
                  style={inputStyle}
                >
                  <option value="">— اختر الخزينة —</option>
                  {treasuries.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name} ({formatYER(t.balance)})
                    </option>
                  ))}
                </select>
              )}
            </div>

            {/* Selected treasury balance hint */}
            {payTreasuryId && (() => {
              const selected = treasuries.find((t) => t.id === payTreasuryId);
              if (!selected) return null;
              const payNum = Number(payAmount) || 0;
              return (
                <div
                  className="rounded-md p-2 flex items-center gap-2"
                  style={{ backgroundColor: tokens.brandLight, border: `1px solid ${tokens.infoBorder}` }}
                >
                  <Wallet className="w-3.5 h-3.5 flex-shrink-0" style={{ color: tokens.brand }} />
                  <span className="text-xs" style={{ color: tokens.infoText }}>
                    رصيد {selected.name}: <strong>{formatYER(selected.balance)}</strong>
                    {payNum > 0 && selected.balance < payNum && (
                      <span style={{ color: tokens.dangerText, marginRight: 8 }}>
                        ⚠ الرصيد غير كافٍ
                      </span>
                    )}
                  </span>
                </div>
              );
            })()}

            {/* Reference number */}
            <div>
              <label style={labelStyle}>رقم الإشعار / المرجع (اختياري)</label>
              <input
                type="text"
                value={payRefNumber}
                onChange={(e) => setPayRefNumber(e.target.value)}
                placeholder="مثال: CHK-00123"
                dir="ltr"
                style={inputStyle}
              />
            </div>

            {/* Notes */}
            <div>
              <label style={labelStyle}>ملاحظات (اختياري)</label>
              <input
                type="text"
                value={payNotes}
                onChange={(e) => setPayNotes(e.target.value)}
                placeholder="مثال: دفعة جزئية من إجمالي الفاتورة..."
                style={inputStyle}
              />
            </div>

            {/* Actions */}
            <div className="flex gap-3 pt-3 border-t" style={{ borderColor: tokens.border }}>
              <button onClick={() => setShowPayBill(null)} style={btnGhost}>إلغاء</button>
              <button
                onClick={handlePayBill}
                disabled={submitting || !payAmount || Number(payAmount) <= 0 || Number(payAmount) > showPayBill.remainingAmount || (!payTreasuryId && treasuries.length > 0)}
                style={{
                  ...btnPrimary,
                  backgroundColor: "#d13438",
                  opacity: submitting || !payAmount || Number(payAmount) <= 0 || Number(payAmount) > showPayBill.remainingAmount || (!payTreasuryId && treasuries.length > 0) ? 0.6 : 1,
                }}
              >
                {submitting ? <><Loader2 className="w-4 h-4 animate-spin" /> جارٍ السداد...</> : "سداد القسط"}
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
