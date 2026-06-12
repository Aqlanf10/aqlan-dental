"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import {
  CreditCard, Pencil, Trash2, X, AlertCircle,
  Search, FileText, Printer,
} from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate, localDateString } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { useDoctors } from "@/hooks/useDoctors";
import { useAuthStore } from "@/stores/authStore";
import { isAdminRole, isAccountantRole } from "@/lib/roles";
import { useActiveCashierSession } from "@/hooks/useCashierSession";
import type { Payment, Contract, CreatePaymentRequest, UpdatePaymentRequest } from "@/types/finance";
import { PAYMENT_METHODS, PAYMENT_METHOD_OPTIONS, SPECIALTY_OPTIONS } from "@/types/finance";

// ─── Component ────────────────────────────────────────────────────────────────

interface PaymentsTabProps {
  patientId: string;
  onPaymentChanged?: () => void;
}

interface PaymentForm {
  amount: string;
  paymentDate: string;
  paymentMethod: string;
  serviceDescription: string;
  specialty: string;
  doctorId: string;
  contractId: string;
  notes: string;
}

const EMPTY_FORM: PaymentForm = {
  amount: "",
  paymentDate: localDateString(),
  paymentMethod: "Cash",
  serviceDescription: "",
  specialty: "",
  doctorId: "",
  contractId: "",
  notes: "",
};

export function PaymentsTab({ patientId, onPaymentChanged }: PaymentsTabProps) {
  const { user } = useAuthStore();
  const canModifyPayment = isAdminRole(user?.role) || isAccountantRole(user?.role);
  const { data: activeCashierSession } = useActiveCashierSession();

  const [payments, setPayments] = useState<Payment[]>([]);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [searchQuery, setSearchQuery] = useState("");

  // Modal
  const [showModal, setShowModal] = useState(false);
  const [editingPayment, setEditingPayment] = useState<Payment | null>(null);
  const [form, setForm] = useState<PaymentForm>({ ...EMPTY_FORM });
  const [saving, setSaving] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  // Doctors
  const { data: doctors = [] } = useDoctors();

  const fetchPayments = useCallback(() => {
    setLoading(true);
    setError("");
    Promise.all([
      api.get<Payment[]>(`/api/payments?patientId=${patientId}`).then((r) => r.data).catch(() => []),
      api.get<Contract[]>(`/api/contracts?patientId=${patientId}&status=active`).then((r) => r.data).catch(() => []),
    ]).then(([p, c]) => {
      setPayments(p);
      setContracts(c);
    }).catch(() => {
      setError("فشل تحميل المدفوعات");
    }).finally(() => setLoading(false));
  }, [patientId]);

  useEffect(() => {
    fetchPayments();
  }, [fetchPayments]);

  // Filtered
  const filteredPayments = searchQuery.trim()
    ? payments.filter((p) => {
        const q = searchQuery.toLowerCase();
        return (p.serviceDescription && p.serviceDescription.toLowerCase().includes(q)) ||
               (p.notes && p.notes.toLowerCase().includes(q)) ||
               (p.doctorName && p.doctorName.toLowerCase().includes(q)) ||
               (p.receiptNumber && p.receiptNumber.toLowerCase().includes(q));
      })
    : payments;

  const totalPaid = payments
    .filter((p) => p.isActive !== false)
    .reduce((sum, p) => sum + (p.amount ?? 0), 0);

  // ─── Handlers ──────────────────────────────────────────────────────────────

  const openAddModal = () => {
    if (!activeCashierSession) {
      toast.error("يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل أي مدفوعات");
      return;
    }
    setEditingPayment(null);
    setForm({ ...EMPTY_FORM });
    setShowModal(true);
  };

  const openEditModal = (payment: Payment) => {
    setEditingPayment(payment);
    setForm({
      amount: payment.amount.toString(),
      paymentDate: payment.paymentDate,
      paymentMethod: payment.paymentMethod ?? "cash",
      serviceDescription: payment.serviceDescription ?? "",
      specialty: payment.specialty ?? "",
      doctorId: payment.doctorId ?? "",
      contractId: payment.contractId ?? "",
      notes: payment.notes ?? "",
    });
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!form.amount || parseFloat(form.amount) <= 0) {
      toast.error("المبلغ مطلوب");
      return;
    }
    setSaving(true);
    try {
      if (editingPayment) {
        const payload: UpdatePaymentRequest = {
          amount: parseFloat(form.amount),
          paymentDate: form.paymentDate || undefined,
          paymentMethod: form.paymentMethod || undefined,
          serviceDescription: form.serviceDescription || undefined,
          specialty: form.specialty || undefined,
          doctorId: form.doctorId || undefined,
          notes: form.notes || undefined,
        };
        await api.put(`/api/payments/${editingPayment.id}`, payload);
        toast.success("تم تحديث الدفعة بنجاح");
      } else {
        const payload: CreatePaymentRequest = {
          patientId,
          contractId: form.contractId || undefined,
          amount: parseFloat(form.amount),
          paymentMethod: form.paymentMethod || "cash",
          serviceDescription: form.serviceDescription || undefined,
          specialty: form.specialty || undefined,
          doctorId: form.doctorId || undefined,
          notes: form.notes || undefined,
        };
        await api.post("/api/payments", payload);
        toast.success("تم إضافة الدفعة بنجاح");
      }
      setShowModal(false);
      fetchPayments();
      onPaymentChanged?.();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل حفظ الدفعة");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await api.delete(`/api/payments/${id}`);
      toast.success("تم حذف الدفعة بنجاح");
      setDeleteConfirm(null);
      fetchPayments();
      onPaymentChanged?.();
    } catch {
      toast.error("فشل حذف الدفعة");
    }
  };

  // ─── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-bold text-[#0d2137]">المدفوعات</h3>
        <button
          onClick={openAddModal}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg text-white hover:opacity-90 transition"
          style={{
            background: activeCashierSession ? "#22c55e" : "#94a3b8",
            cursor: activeCashierSession ? undefined : "not-allowed",
          }}
          title={!activeCashierSession ? "يجب فتح صندوق الكاشير أولاً" : undefined}
        >
          <span
            className="w-2 h-2 rounded-full inline-block"
            style={{ background: activeCashierSession ? "#86efac" : "#ef4444" }}
          />
          إضافة دفعة
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-3">
        <div className="rounded-xl px-3 py-2 bg-green-50 flex items-center gap-2">
          <CreditCard className="w-4 h-4 text-green-600 flex-shrink-0" />
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">إجمالي المدفوعات</p>
            <p className="text-sm font-bold text-green-600">{totalPaid.toLocaleString()} ر.ي</p>
          </div>
        </div>
        <div className="rounded-xl px-3 py-2 bg-[#3d7ab518] flex items-center gap-2">
          <FileText className="w-4 h-4 text-[#3d7ab5] flex-shrink-0" />
          <div className="min-w-0">
            <p className="text-xs text-[#94a3b8]">عدد المدفوعات</p>
            <p className="text-sm font-bold text-[#3d7ab5]">{payments.length}</p>
          </div>
        </div>
      </div>

      {/* Search */}
      <div className="relative">
        <Search className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#94a3b8]" />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="بحث بالوصف أو الملاحظات أو رقم الإيصال..."
          className="w-full text-sm border border-[#e8f0f9] rounded-lg pr-9 pl-3 py-2 focus:outline-none focus:border-[#3d7ab5] placeholder:text-[#94a3b8]"
        />
      </div>

      {/* Loading */}
      {loading ? (
        <div className="space-y-3 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-16 bg-[#f1f5f9] rounded-lg" />
          ))}
        </div>
      ) : error ? (
        <div className="text-center py-10">
          <AlertCircle className="w-10 h-10 mx-auto mb-2 text-red-300" />
          <p className="text-sm text-red-500">{error}</p>
          <button onClick={fetchPayments} className="mt-2 text-xs text-[#3d7ab5] hover:underline">إعادة المحاولة</button>
        </div>
      ) : filteredPayments.length === 0 ? (
        <EmptyState
          icon={CreditCard}
          title="لا توجد مدفوعات"
          description={searchQuery ? "لا توجد مدفوعات تطابق البحث" : "لم يتم تسجيل أي مدفوعات لهذا المريض"}
        />
      ) : (
        <div className="space-y-2">
          {filteredPayments.map((p) => (
            <div key={p.id} className="flex items-center justify-between p-3 bg-white border border-[#e8f0f9] rounded-xl hover:border-[#d6e8fa] transition">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg bg-green-50 flex items-center justify-center flex-shrink-0">
                  <CreditCard className="w-5 h-5 text-green-600" />
                </div>
                <div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-semibold text-[#0d2137]">{p.amount.toLocaleString()} ر.ي</span>
                    {p.paymentMethod && (
                      <span className="text-xs bg-[#f1f5f9] text-[#64748b] px-1.5 py-0.5 rounded font-medium">
                        {PAYMENT_METHODS[p.paymentMethod as keyof typeof PAYMENT_METHODS] ?? p.paymentMethod}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-[#94a3b8]">{formatArabicDate(p.paymentDate)}</p>
                  {p.serviceDescription && <p className="text-xs text-[#64748b] line-clamp-1">{p.serviceDescription}</p>}
                  {p.doctorName && <p className="text-[10px] text-[#94a3b8]">د. {p.doctorName}</p>}
                </div>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                <Link
                  href={`/patients/${p.patientId ?? patientId}/payments/${p.id}/receipt`}
                  className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                >
                  <Printer className="w-3 h-3" />
                  سند قبض
                </Link>
                {canModifyPayment && (
                  <button
                    onClick={() => openEditModal(p)}
                    className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg border border-[#3d7ab5] text-[#3d7ab5] hover:bg-[#eef3f9] transition"
                  >
                    <Pencil className="w-3 h-3" />
                    تعديل
                  </button>
                )}
                {canModifyPayment && (
                  deleteConfirm === p.id ? (
                    <div className="flex items-center gap-1">
                      <button onClick={() => handleDelete(p.id)} className="px-2 py-1 text-xs font-medium rounded-lg bg-red-500 text-white hover:bg-red-600 transition">تأكيد</button>
                      <button onClick={() => setDeleteConfirm(null)} className="px-2 py-1 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 transition">إلغاء</button>
                    </div>
                  ) : (
                    <button
                      onClick={() => setDeleteConfirm(p.id)}
                      className="flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-lg text-red-500 hover:bg-red-50 transition"
                    >
                      <Trash2 className="w-3 h-3" />
                      حذف
                    </button>
                  )
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ─── Add/Edit Modal ──────────────────────────────────────────────────── */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4" onClick={() => setShowModal(false)}>
          <div className="bg-white rounded-2xl w-full max-w-lg shadow-2xl max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <div className="p-6 space-y-4" dir="rtl">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-bold text-[#0d2137]">
                  {editingPayment ? "تعديل الدفعة" : "إضافة دفعة جديدة"}
                </h3>
                <button onClick={() => setShowModal(false)} className="text-[#94a3b8] hover:text-[#0d2137]">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="space-y-3">
                {/* Amount and Date */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">المبلغ (ر.ي) *</label>
                    <input
                      type="number"
                      value={form.amount}
                      onChange={(e) => setForm({ ...form, amount: e.target.value })}
                      placeholder="0"
                      min="0"
                      step="0.01"
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">تاريخ الدفع</label>
                    <input
                      type="date"
                      value={form.paymentDate}
                      onChange={(e) => setForm({ ...form, paymentDate: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                    />
                  </div>
                </div>

                {/* Method and Contract */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">طريقة الدفع</label>
                    <select
                      value={form.paymentMethod}
                      onChange={(e) => setForm({ ...form, paymentMethod: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      {PAYMENT_METHOD_OPTIONS.map((m) => (
                        <option key={m.value} value={m.value}>{m.label}</option>
                      ))}
                    </select>
                  </div>
                  {!editingPayment && contracts.length > 0 && (
                    <div>
                      <label className="text-xs text-[#64748b] block mb-1">عقد مرتبط</label>
                      <select
                        value={form.contractId}
                        onChange={(e) => setForm({ ...form, contractId: e.target.value })}
                        className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                      >
                        <option value="">— بدون عقد —</option>
                        {contracts.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.specialty ? `${SPECIALTY_OPTIONS[c.specialty as keyof typeof SPECIALTY_OPTIONS] ?? c.specialty} — ` : ""}{c.totalAmount.toLocaleString()} ر.ي
                          </option>
                        ))}
                      </select>
                    </div>
                  )}
                </div>

                {/* Service Description */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">وصف الخدمة</label>
                  <input
                    type="text"
                    value={form.serviceDescription}
                    onChange={(e) => setForm({ ...form, serviceDescription: e.target.value })}
                    placeholder="مثل: تنظيف أسنان، حشو ضرس"
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5]"
                  />
                </div>

                {/* Specialty and Doctor */}
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">التخصص</label>
                    <select
                      value={form.specialty}
                      onChange={(e) => setForm({ ...form, specialty: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {Object.entries(SPECIALTY_OPTIONS).filter(([k]) => k[0] === k[0].toLowerCase()).map(([k, v]) => (
                        <option key={k} value={k}>{v}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-xs text-[#64748b] block mb-1">الطبيب</label>
                    <select
                      value={form.doctorId}
                      onChange={(e) => setForm({ ...form, doctorId: e.target.value })}
                      className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] bg-white"
                    >
                      <option value="">— اختر —</option>
                      {doctors.filter((d) => d.isActive).map((d) => (
                        <option key={d.id} value={d.id}>{d.name}</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Notes */}
                <div>
                  <label className="text-xs text-[#64748b] block mb-1">ملاحظات</label>
                  <textarea
                    value={form.notes}
                    onChange={(e) => setForm({ ...form, notes: e.target.value })}
                    placeholder="ملاحظات إضافية"
                    rows={2}
                    className="w-full text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-[#3d7ab5] resize-none"
                  />
                </div>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-2">
                <button
                  onClick={() => setShowModal(false)}
                  className="flex-1 py-2.5 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >
                  إلغاء
                </button>
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className={cn(
                    "flex-1 py-2.5 text-sm font-medium rounded-lg text-white transition",
                    saving ? "bg-[#22c55e]/60 cursor-not-allowed" : "bg-[#22c55e] hover:bg-[#16a34a]"
                  )}
                >
                  {saving ? "جاري الحفظ..." : editingPayment ? "تحديث" : "إضافة"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
