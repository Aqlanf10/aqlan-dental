"use client";
import { useState } from "react";
import Link from "next/link";
import {
  Plus, Receipt, TrendingDown, Calendar, Trash2, Edit2,
  Wallet,
} from "lucide-react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { useExpenses, useCreateExpense, useUpdateExpense, useDeleteExpense, useExpenseSummary } from "@/hooks/useFinance";
import type { Expense, CreateExpenseRequest, UpdateExpenseRequest } from "@/types/finance";
import { formatYemeniRiyal, formatArabicDate, cn } from "@/lib/utils";

const CATEGORY_LABELS: Record<string, string> = {
  rent: "إيجار", utilities: "خدمات", salaries: "رواتب", supplies: "مستلزمات طبية",
  equipment: "أجهزة", maintenance: "صيانة", marketing: "تسويق", transport: "مواصلات",
  lab_fees: "رسوم مختبر", insurance: "تأمين", taxes: "ضرائب", other: "أخرى",
};
const CATEGORY_OPTIONS = Object.entries(CATEGORY_LABELS);
const METHOD_LABELS: Record<string, string> = {
  cash: "نقداً", bank_transfer: "تحويل بنكي", card: "بطاقة", check: "شيك",
};

const expenseSchema = z.object({
  description: z.string().min(1, "الوصف مطلوب"),
  amount: z.number().min(1, "المبلغ مطلوب"),
  expenseDate: z.string().optional(),
  category: z.string().optional(),
  paymentMethod: z.string().optional(),
  recipient: z.string().optional(),
  notes: z.string().optional(),
});
type ExpenseFormData = z.infer<typeof expenseSchema>;

const inputCls = (err?: string) => cn(
  "w-full px-3 py-2 text-sm rounded-lg border bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal",
  err ? "border-red-400" : "border-gray-300"
);

export default function ExpensesPage() {
  const [categoryFilter, setCategoryFilter] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [showModal, setShowModal] = useState(false);
  const [editExpense, setEditExpense] = useState<Expense | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  const { data: expensesData, isLoading } = useExpenses({
    page: 1,
    pageSize: 50,
    category: categoryFilter || undefined,
    from: from || undefined,
    to: to || undefined,
  });
  const { data: summary } = useExpenseSummary();
  const createMutation = useCreateExpense();
  const updateMutation = useUpdateExpense();
  const deleteMutation = useDeleteExpense();

  const { register, handleSubmit, reset, formState: { errors } } = useForm<ExpenseFormData>({
    resolver: zodResolver(expenseSchema),
    defaultValues: { amount: 0, category: "other", paymentMethod: "cash" },
  });

  const openNew = () => {
    setEditExpense(null);
    reset({ description: "", amount: 0, category: "other", paymentMethod: "cash", expenseDate: new Date().toISOString().slice(0, 10) });
    setShowModal(true);
  };

  const openEdit = (expense: Expense) => {
    setEditExpense(expense);
    reset({
      description: expense.description,
      amount: expense.amount,
      expenseDate: expense.expenseDate,
      category: expense.category ?? "other",
      paymentMethod: expense.paymentMethod ?? "cash",
      recipient: expense.recipient,
      notes: expense.notes,
    });
    setShowModal(true);
  };

  const onSubmit = async (data: ExpenseFormData) => {
    if (editExpense) {
      await updateMutation.mutateAsync({ id: editExpense.id, ...data } as UpdateExpenseRequest & { id: string });
    } else {
      await createMutation.mutateAsync(data as CreateExpenseRequest);
    }
    setShowModal(false);
    reset();
  };

  const handleDelete = async (id: string) => {
    await deleteMutation.mutateAsync(id);
    setDeleteConfirm(null);
  };

  const expenses = expensesData?.items ?? [];

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <div className="flex items-center gap-2 text-sm text-gray-500 mb-1">
            <Link href="/finance" className="hover:text-clinic-teal transition">المالية</Link>
            <span>/</span>
            <span className="text-gray-900 font-medium">المصروفات</span>
          </div>
          <h1 className="text-2xl font-extrabold text-gray-900">المصروفات</h1>
          <p className="text-sm text-gray-500 mt-0.5">تتبع مصروفات المركز</p>
        </div>
        <button onClick={openNew}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          إضافة مصروف
        </button>
      </div>

      {/* Summary Cards */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <SummaryCard label="مصروفات اليوم" value={formatYemeniRiyal(summary.todayExpenses)} icon={Receipt} color="bg-red-50 text-red-600 border-red-200" />
          <SummaryCard label="مصروفات الشهر" value={formatYemeniRiyal(summary.monthExpenses)} icon={TrendingDown} color="bg-orange-50 text-orange-600 border-orange-200" />
          <SummaryCard label="إجمالي المصروفات" value={formatYemeniRiyal(summary.totalExpenses)} icon={Wallet} color="bg-amber-50 text-amber-600 border-amber-200" />
          <SummaryCard label="عدد المصروفات" value={summary.expenseCount.toString()} icon={Calendar} color="bg-gray-50 text-gray-600 border-gray-200" />
        </div>
      )}

      {/* Category breakdown mini-bar */}
      {summary && summary.byCategory.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
          <h3 className="font-bold text-gray-900 text-sm mb-3">التوزيع حسب الفئة</h3>
          <div className="space-y-2">
            {summary.byCategory.slice(0, 5).map((cat) => {
              const pct = summary.totalExpenses > 0 ? Math.round((cat.total / summary.totalExpenses) * 100) : 0;
              return (
                <div key={cat.category}>
                  <div className="flex items-center justify-between text-sm mb-1">
                    <span className="text-gray-700 font-medium">{cat.categoryAr}</span>
                    <span className="font-mono text-gray-900">
                      {formatYemeniRiyal(cat.total)}{" "}
                      <span className="text-xs text-gray-400">({pct}%)</span>
                    </span>
                  </div>
                  <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div className="h-full bg-red-400 rounded-full transition-all" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Filters */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="flex items-center gap-1 flex-wrap">
          <button onClick={() => setCategoryFilter("")}
            className={cn("px-3 py-1.5 text-sm rounded-lg border transition font-medium whitespace-nowrap",
              categoryFilter === "" ? "bg-clinic-teal text-white border-clinic-teal" : "border-gray-200 text-gray-600 hover:bg-gray-50"
            )}
          >الكل</button>
          {CATEGORY_OPTIONS.slice(0, 6).map(([key, label]) => (
            <button key={key} onClick={() => setCategoryFilter(key)}
              className={cn("px-3 py-1.5 text-sm rounded-lg border transition font-medium whitespace-nowrap",
                categoryFilter === key ? "bg-clinic-teal text-white border-clinic-teal" : "border-gray-200 text-gray-600 hover:bg-gray-50"
              )}
            >{label}</button>
          ))}
        </div>
        <div className="flex items-center gap-2 md:ms-auto">
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5" placeholder="من" />
          <span className="text-gray-400">—</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5" placeholder="إلى" />
        </div>
      </div>

      {/* Expenses Table */}
      {isLoading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-14 bg-gray-100 rounded-xl" />)}
        </div>
      ) : expenses.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <Receipt className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد مصروفات</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["الوصف", "المبلغ", "التاريخ", "الفئة", "الطريقة", "المستلم", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {expenses.map((e) => (
                  <tr key={e.id} className="hover:bg-gray-50 transition group">
                    <td className="px-4 py-3 font-medium text-gray-900 max-w-48 truncate">{e.description}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-red-600">{formatYemeniRiyal(e.amount)}</td>
                    <td className="px-4 py-3 text-gray-600 text-xs">{formatArabicDate(e.expenseDate)}</td>
                    <td className="px-4 py-3">
                      <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-700">
                        {e.categoryAr ?? CATEGORY_LABELS[e.category ?? ""] ?? e.category ?? "—"}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-600 text-xs">
                      {e.paymentMethodAr ?? METHOD_LABELS[e.paymentMethod ?? ""] ?? e.paymentMethod ?? "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-600 text-xs">{e.recipient ?? "—"}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
                        <button onClick={() => openEdit(e)} className="p-1 text-gray-400 hover:text-clinic-teal transition">
                          <Edit2 className="w-3.5 h-3.5" />
                        </button>
                        <button onClick={() => setDeleteConfirm(e.id)} className="p-1 text-gray-400 hover:text-red-500 transition">
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {expensesData && expensesData.totalCount > 0 && (
            <div className="px-4 py-2 border-t border-gray-100 bg-gray-50 text-xs text-gray-500">
              {expensesData.totalCount} مصروف — صفحة {expensesData.page} من {expensesData.totalPages}
            </div>
          )}
        </div>
      )}

      {/* Add/Edit Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={() => setShowModal(false)}>
          <div className="bg-white rounded-xl shadow-xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="font-bold text-gray-900">{editExpense ? "تعديل المصروف" : "إضافة مصروف"}</h2>
              <button onClick={() => setShowModal(false)} className="text-gray-400 hover:text-gray-600 text-xl">&times;</button>
            </div>
            <form onSubmit={handleSubmit(onSubmit)} className="p-5 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">الوصف <span className="text-red-500">*</span></label>
                <input {...register("description")} className={inputCls(errors.description?.message)} placeholder="إيجار المركز، مستلزمات..." />
                {errors.description && <p className="mt-1 text-xs text-red-600">{errors.description.message}</p>}
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">المبلغ (ريال) <span className="text-red-500">*</span></label>
                  <input {...register("amount", { valueAsNumber: true })} type="number" min={1} className={inputCls(errors.amount?.message)} dir="ltr" />
                  {errors.amount && <p className="mt-1 text-xs text-red-600">{errors.amount.message}</p>}
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">التاريخ</label>
                  <input {...register("expenseDate")} type="date" className={inputCls()} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">الفئة</label>
                  <select {...register("category")} className={inputCls()}>
                    {CATEGORY_OPTIONS.map(([key, label]) => (
                      <option key={key} value={key}>{label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">طريقة الدفع</label>
                  <select {...register("paymentMethod")} className={inputCls()}>
                    <option value="cash">نقداً</option>
                    <option value="bank_transfer">تحويل بنكي</option>
                    <option value="card">بطاقة</option>
                    <option value="check">شيك</option>
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">المستلم</label>
                <input {...register("recipient")} className={inputCls()} placeholder="اسم المستلم أو الجهة" />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
                <textarea {...register("notes")} rows={2} className={inputCls()} />
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setShowModal(false)}
                  className="px-4 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
                >إلغاء</button>
                <button type="submit" disabled={createMutation.isPending || updateMutation.isPending}
                  className="flex items-center gap-2 px-5 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
                >
                  <Plus className="w-4 h-4" />
                  {createMutation.isPending || updateMutation.isPending ? "جارٍ الحفظ..." : editExpense ? "تحديث" : "إضافة"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Confirmation */}
      {deleteConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={() => setDeleteConfirm(null)}>
          <div className="bg-white rounded-xl shadow-xl w-full max-w-sm mx-4 p-5" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-bold text-gray-900 mb-2">تأكيد الحذف</h3>
            <p className="text-sm text-gray-600 mb-4">هل أنت متأكد من حذف هذا المصروف؟ لا يمكن التراجع.</p>
            <div className="flex justify-end gap-3">
              <button onClick={() => setDeleteConfirm(null)}
                className="px-4 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >إلغاء</button>
              <button onClick={() => handleDelete(deleteConfirm)} disabled={deleteMutation.isPending}
                className="px-4 py-2 text-sm font-medium rounded-lg bg-red-600 text-white hover:bg-red-700 disabled:opacity-60 transition"
              >
                {deleteMutation.isPending ? "جارٍ الحذف..." : "حذف"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function SummaryCard({ label, value, icon: Icon, color }: {
  label: string; value: string; icon: typeof Receipt; color: string;
}) {
  return (
    <div className={`rounded-xl border p-4 ${color}`}>
      <Icon className="w-5 h-5 mb-2 opacity-80" />
      <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
      <p className="text-xs font-medium mt-1 opacity-70">{label}</p>
    </div>
  );
}
