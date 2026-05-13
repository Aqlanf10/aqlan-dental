"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowRight, Printer, Plus, FileText, CheckCircle, Clock, AlertCircle, Pencil, XCircle, BadgeCheck } from "lucide-react";
import type { Contract, Payment } from "@/types/finance";
import api from "@/lib/api";
import { cn, formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { ReceiptView } from "@/components/finance/ReceiptView";

const STATUS_LABELS: Record<string, string> = {
  active: "نشط", completed: "مكتمل", cancelled: "ملغى",
};
const METHOD_LABELS: Record<string, string> = {
  cash: "نقداً", bank_transfer: "تحويل بنكي", card: "بطاقة",
};
const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم", general: "أسنان عام", surgery: "جراحة", other: "أخرى",
};

// ─── Installment Schedule ────────────────────────────────────────────────────
function InstallmentSchedule({ contract }: { contract: Contract }) {
  const totalPaid    = contract.paidAmount;
  const installAmt   = contract.installmentAmount ?? 0;
  const downPayment  = contract.downPayment ?? 0;
  const startDate    = contract.startDate ? new Date(contract.startDate) : null;
  if (!startDate || installAmt === 0) return null;

  const installments = Array.from({ length: contract.installmentsCount }, (_, i) => {
    const due = new Date(startDate);
    due.setMonth(due.getMonth() + i + 1);
    const dueStr = due.toISOString().slice(0, 10);
    const cumulative = downPayment + (i + 1) * installAmt;
    const isPaid = totalPaid >= cumulative;
    const today = new Date().toISOString().slice(0, 10);
    const isOverdue = !isPaid && dueStr < today;
    return { num: i + 1, dueStr, amount: installAmt, isPaid, isOverdue };
  });

  const paidCount    = installments.filter((i) => i.isPaid).length;
  const overdueCount = installments.filter((i) => i.isOverdue).length;

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
      <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
        <h2 className="font-bold text-gray-900">جدول الأقساط</h2>
        <div className="flex items-center gap-3 text-xs">
          <span className="flex items-center gap-1 text-green-600">
            <CheckCircle className="w-3.5 h-3.5" /> {paidCount} مسدّد
          </span>
          {overdueCount > 0 && (
            <span className="flex items-center gap-1 text-red-600">
              <AlertCircle className="w-3.5 h-3.5" /> {overdueCount} متأخر
            </span>
          )}
          <span className="flex items-center gap-1 text-gray-400">
            <Clock className="w-3.5 h-3.5" /> {contract.installmentsCount - paidCount} متبقي
          </span>
        </div>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 border-b border-gray-100">
            <tr>
              {["#", "تاريخ الاستحقاق", "المبلغ", "الحالة"].map((h) => (
                <th key={h} className="text-start px-4 py-2.5 text-xs font-semibold text-gray-500">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {installments.map((inst) => (
              <tr key={inst.num}
                className={cn("transition", inst.isPaid ? "bg-green-50/40" : inst.isOverdue ? "bg-red-50/40" : "hover:bg-gray-50")}
              >
                <td className="px-4 py-2.5 font-mono text-xs text-gray-500">{inst.num}</td>
                <td className="px-4 py-2.5 text-gray-700">{formatArabicDate(inst.dueStr)}</td>
                <td className="px-4 py-2.5 font-mono font-semibold text-gray-900">{formatYemeniRiyal(inst.amount)}</td>
                <td className="px-4 py-2.5">
                  {inst.isPaid ? (
                    <span className="flex items-center gap-1 text-xs text-green-700 font-medium">
                      <CheckCircle className="w-3.5 h-3.5" /> مسدّد
                    </span>
                  ) : inst.isOverdue ? (
                    <span className="flex items-center gap-1 text-xs text-red-600 font-medium">
                      <AlertCircle className="w-3.5 h-3.5" /> متأخر
                    </span>
                  ) : (
                    <span className="flex items-center gap-1 text-xs text-gray-400">
                      <Clock className="w-3.5 h-3.5" /> قادم
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── Edit Contract Modal ─────────────────────────────────────────────────────
interface EditForm {
  specialty: string;
  totalAmount: number;
  installmentsCount: number;
  installmentAmount: number;
  startDate: string;
  discountAmount: number;
  discountReason: string;
  notes: string;
}

function EditModal({
  contract, onClose, onSaved,
}: { contract: Contract; onClose: () => void; onSaved: (c: Contract) => void }) {
  const [form, setForm] = useState<EditForm>({
    specialty: contract.specialty ?? "",
    totalAmount: contract.totalAmount,
    installmentsCount: contract.installmentsCount,
    installmentAmount: contract.installmentAmount ?? 0,
    startDate: contract.startDate ?? "",
    discountAmount: contract.discountAmount ?? 0,
    discountReason: contract.discountReason ?? "",
    notes: contract.notes ?? "",
  });
  const [saving, setSaving] = useState(false);
  const [error, setError]   = useState<string | null>(null);

  const set = (k: keyof EditForm, v: string | number) =>
    setForm((prev) => ({ ...prev, [k]: v }));

  const validate = (): string | null => {
    if (form.totalAmount < 0) return "إجمالي العقد يجب أن يكون صفراً أو أكثر";
    if (form.discountAmount < 0) return "قيمة الخصم يجب أن تكون صفراً أو أكثر";
    if (form.discountAmount > form.totalAmount) return "قيمة الخصم لا يمكن أن تتجاوز إجمالي العقد";
    if (form.installmentsCount < 0) return "عدد الأقساط يجب أن يكون صفراً أو أكثر";
    if (form.installmentAmount < 0) return "قيمة القسط يجب أن تكون صفراً أو أكثر";
    return null;
  };

  const save = async () => {
    const validationError = validate();
    if (validationError) { setError(validationError); return; }

    setSaving(true); setError(null);
    try {
      const { data } = await api.put<Contract>(`/api/contracts/${contract.id}`, {
        specialty:         form.specialty || null,
        totalAmount:       form.totalAmount,
        installmentsCount: form.installmentsCount,
        installmentAmount: form.installmentAmount || null,
        startDate:         form.startDate || null,
        discountAmount:    form.discountAmount,
        discountReason:    form.discountReason || null,
        notes:             form.notes || null,
      });
      onSaved(data);
      onClose();
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "حدث خطأ أثناء الحفظ");
    } finally {
      setSaving(false);
    }
  };

  const inputCls = "w-full rounded-lg border border-gray-200 px-3 py-2 text-sm focus:outline-none focus:border-clinic-blue";

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 space-y-4" onClick={(e) => e.stopPropagation()}>
        <h2 className="text-lg font-bold text-gray-900">تعديل العقد</h2>

        <div className="grid grid-cols-2 gap-3">
          <label className="col-span-2 space-y-1">
            <span className="text-xs text-gray-500">التخصص</span>
            <select value={form.specialty} onChange={(e) => set("specialty", e.target.value)} className={inputCls}>
              <option value="">-- اختر --</option>
              <option value="orthodontics">تقويم</option>
              <option value="general">أسنان عام</option>
              <option value="surgery">جراحة</option>
              <option value="other">أخرى</option>
            </select>
          </label>

          <label className="space-y-1">
            <span className="text-xs text-gray-500">إجمالي العقد (ر.ي)</span>
            <input type="number" min="0" value={form.totalAmount}
              onChange={(e) => set("totalAmount", +e.target.value)} className={inputCls} dir="ltr" />
          </label>

          <label className="space-y-1">
            <span className="text-xs text-gray-500">الخصم (ر.ي)</span>
            <input type="number" min="0" value={form.discountAmount}
              onChange={(e) => set("discountAmount", +e.target.value)} className={inputCls} dir="ltr" />
          </label>

          <label className="space-y-1">
            <span className="text-xs text-gray-500">عدد الأقساط</span>
            <input type="number" min="1" value={form.installmentsCount}
              onChange={(e) => set("installmentsCount", +e.target.value)} className={inputCls} dir="ltr" />
          </label>

          <label className="space-y-1">
            <span className="text-xs text-gray-500">قيمة القسط (ر.ي)</span>
            <input type="number" min="0" value={form.installmentAmount}
              onChange={(e) => set("installmentAmount", +e.target.value)} className={inputCls} dir="ltr" />
          </label>

          <label className="space-y-1">
            <span className="text-xs text-gray-500">تاريخ البدء</span>
            <input type="date" value={form.startDate}
              onChange={(e) => set("startDate", e.target.value)} className={inputCls} dir="ltr" />
          </label>

          <label className="space-y-1">
            <span className="text-xs text-gray-500">سبب الخصم</span>
            <input type="text" value={form.discountReason}
              onChange={(e) => set("discountReason", e.target.value)} className={inputCls} />
          </label>

          <label className="col-span-2 space-y-1">
            <span className="text-xs text-gray-500">ملاحظات</span>
            <textarea rows={2} value={form.notes}
              onChange={(e) => set("notes", e.target.value)} className={cn(inputCls, "resize-none")} />
          </label>
        </div>

        {error && <p className="text-xs text-red-600">{error}</p>}

        <div className="flex gap-2 pt-1">
          <button onClick={save} disabled={saving}
            className="flex-1 py-2 text-sm font-semibold rounded-xl bg-clinic-blue text-white hover:opacity-90 disabled:opacity-50 transition">
            {saving ? "جارٍ الحفظ..." : "حفظ التعديلات"}
          </button>
          <button onClick={onClose} disabled={saving}
            className="px-4 py-2 text-sm rounded-xl border border-gray-200 hover:bg-gray-50 transition">
            إلغاء
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Main Page ───────────────────────────────────────────────────────────────
export default function ContractDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [contract, setContract]       = useState<Contract | null>(null);
  const [loading, setLoading]         = useState(true);
  const [printPayment, setPrintPayment] = useState<Payment | null>(null);
  const [showEdit, setShowEdit]       = useState(false);
  const [statusLoading, setStatusLoading] = useState(false);
  const [statusError, setStatusError]     = useState<string | null>(null);

  useEffect(() => {
    api.get<Contract>(`/api/contracts/${id}`)
      .then((r) => setContract(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const changeStatus = async (status: "completed" | "cancelled") => {
    if (!contract) return;
    const label = status === "completed" ? "إتمام" : "إلغاء";
    if (!confirm(`هل تريد ${label} هذا العقد؟`)) return;
    setStatusLoading(true); setStatusError(null);
    try {
      const { data } = await api.patch<Contract>(`/api/contracts/${id}/status`, { status });
      setContract(data);
    } catch {
      setStatusError("حدث خطأ أثناء تغيير الحالة");
    } finally {
      setStatusLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-3xl space-y-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-24 bg-gray-100 rounded-xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (!contract) {
    return (
      <div className="text-center py-20 text-gray-400">
        <FileText className="w-12 h-12 mx-auto mb-3 opacity-30" />
        <p className="text-sm">العقد غير موجود</p>
      </div>
    );
  }

  const pctPaid = contract.totalAmount > 0
    ? Math.min(100, (contract.paidAmount / (contract.totalAmount - (contract.discountAmount ?? 0))) * 100)
    : 0;

  return (
    <div className="space-y-5 max-w-3xl">
      {showEdit && (
        <EditModal
          contract={contract}
          onClose={() => setShowEdit(false)}
          onSaved={setContract}
        />
      )}

      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/finance" className="hover:text-clinic-blue transition">المالية</Link>
        <span>/</span>
        <Link href="/finance/contracts" className="hover:text-clinic-blue transition">العقود</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{contract.patientName}</span>
      </div>

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/finance/contracts" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-gray-900">تفاصيل العقد</h1>
        </div>

        {/* Actions */}
        {contract.status === "active" && (
          <div className="flex items-center gap-2">
            <button
              onClick={() => setShowEdit(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-gray-200 hover:bg-gray-50 transition"
            >
              <Pencil className="w-3.5 h-3.5" />
              تعديل
            </button>
            <button
              onClick={() => changeStatus("completed")}
              disabled={statusLoading}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-green-300 text-green-700 hover:bg-green-50 disabled:opacity-50 transition"
            >
              <BadgeCheck className="w-3.5 h-3.5" />
              إتمام
            </button>
            <button
              onClick={() => changeStatus("cancelled")}
              disabled={statusLoading}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-red-300 text-red-600 hover:bg-red-50 disabled:opacity-50 transition"
            >
              <XCircle className="w-3.5 h-3.5" />
              إلغاء
            </button>
          </div>
        )}
      </div>

      {statusError && (
        <p className="text-sm text-red-600 bg-red-50 rounded-lg px-4 py-2">{statusError}</p>
      )}

      {/* Contract summary card */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-start justify-between mb-4">
          <div>
            <Link href={`/patients/${contract.patientId}`} className="text-lg font-bold text-gray-900 hover:text-clinic-blue transition">
              {contract.patientName}
            </Link>
            <p className="text-xs text-gray-400 font-mono mt-0.5">{contract.patientNumber}</p>
          </div>
          <span className={cn(
            "text-xs px-3 py-1 rounded-full font-medium",
            contract.status === "active"    ? "bg-green-50 text-green-700"  :
            contract.status === "completed" ? "bg-blue-50 text-blue-700"    :
            "bg-gray-100 text-gray-500"
          )}>
            {STATUS_LABELS[contract.status] ?? contract.status}
          </span>
        </div>

        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-5">
          <div className="bg-gray-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">إجمالي العقد</p>
            <p className="font-bold font-mono text-gray-900 mt-1">{formatYemeniRiyal(contract.totalAmount)}</p>
          </div>
          {(contract.discountAmount ?? 0) > 0 && (
            <div className="bg-orange-50 rounded-lg p-3 text-center">
              <p className="text-xs text-gray-500">الخصم</p>
              <p className="font-bold font-mono text-orange-700 mt-1">{formatYemeniRiyal(contract.discountAmount ?? 0)}</p>
            </div>
          )}
          <div className="bg-green-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">المدفوع</p>
            <p className="font-bold font-mono text-green-700 mt-1">{formatYemeniRiyal(contract.paidAmount)}</p>
          </div>
          <div className="bg-red-50 rounded-lg p-3 text-center">
            <p className="text-xs text-gray-500">المتبقي</p>
            <p className="font-bold font-mono text-red-600 mt-1">{formatYemeniRiyal(contract.remainingAmount)}</p>
          </div>
        </div>

        {/* Progress bar */}
        <div>
          <div className="flex justify-between text-xs text-gray-500 mb-1">
            <span>نسبة السداد</span>
            <span>{pctPaid.toFixed(0)}%</span>
          </div>
          <div className="h-2.5 bg-gray-100 rounded-full overflow-hidden">
            <div
              className={cn("h-full rounded-full transition-all", pctPaid >= 100 ? "bg-green-500" : "bg-clinic-blue")}
              style={{ width: `${pctPaid}%` }}
            />
          </div>
        </div>

        <div className="flex items-center gap-4 mt-4 text-xs text-gray-500 pt-4 border-t border-gray-100 flex-wrap">
          {contract.specialty && <span>التخصص: <span className="font-medium text-gray-700">{SPECIALTY_LABELS[contract.specialty] ?? contract.specialty}</span></span>}
          {contract.startDate && <span>تاريخ البدء: <span className="font-medium text-gray-700">{formatArabicDate(contract.startDate)}</span></span>}
          <span>الأقساط: <span className="font-medium text-gray-700">{contract.installmentsCount} قسط × {formatYemeniRiyal(contract.installmentAmount ?? 0)}</span></span>
          {contract.discountReason && <span>سبب الخصم: <span className="font-medium text-gray-700">{contract.discountReason}</span></span>}
        </div>
        {contract.notes && (
          <p className="mt-3 text-sm text-gray-600 bg-gray-50 rounded-lg p-3">{contract.notes}</p>
        )}
      </div>

      {/* Installment Schedule */}
      {contract.installmentsCount > 1 && contract.startDate && (
        <InstallmentSchedule contract={contract} />
      )}

      {/* Print receipt if selected */}
      {printPayment && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold text-gray-700">معاينة السند</p>
            <div className="flex gap-2">
              <button onClick={() => window.print()}
                className="flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border border-gray-300 hover:bg-gray-50 transition"
              >
                <Printer className="w-3.5 h-3.5" />
                طباعة
              </button>
              <button onClick={() => setPrintPayment(null)}
                className="px-3 py-1.5 text-sm rounded-lg border border-gray-300 hover:bg-gray-50 transition"
              >
                إغلاق
              </button>
            </div>
          </div>
          <ReceiptView payment={printPayment} />
        </div>
      )}

      {/* Payments list */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">سجل الدفعات</h2>
          {contract.status === "active" && (
            <Link
              href={`/finance/payments?contractId=${id}&patientId=${contract.patientId}&patientName=${encodeURIComponent(contract.patientName ?? "")}`}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
            >
              <Plus className="w-3.5 h-3.5" />
              دفعة جديدة
            </Link>
          )}
        </div>

        {!contract.payments?.length ? (
          <div className="text-center py-10 text-gray-400 text-sm">لا توجد دفعات مسجلة</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["رقم السند", "المبلغ", "التاريخ", "الطريقة", "الوصف", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {contract.payments.map((p) => (
                  <tr key={p.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-mono text-xs text-gray-400">{p.receiptNumber ?? "—"}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-green-700">{formatYemeniRiyal(p.amount)}</td>
                    <td className="px-4 py-3 text-gray-600">{formatArabicDate(p.paymentDate)}</td>
                    <td className="px-4 py-3 text-gray-600">{METHOD_LABELS[p.paymentMethod ?? ""] ?? p.paymentMethod ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600">{p.serviceDescription ?? "—"}</td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => setPrintPayment(p)}
                        className="text-xs text-gray-400 hover:text-clinic-blue transition"
                        title="طباعة السند"
                      >
                        <Printer className="w-3.5 h-3.5" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
