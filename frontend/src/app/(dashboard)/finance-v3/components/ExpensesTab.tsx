"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Plus,
  RefreshCw,
  TrendingDown,
  ThumbsUp,
  ThumbsDown,
  Trash2,
} from "lucide-react";
import { api } from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { ExpenseListItem, CreateExpenseRequest } from "./types";
import { EXPENSE_CATEGORIES, PAYMENT_METHODS } from "./types";
import { SectionHeader, LoadingSkeleton, EmptyState, DataTable, Modal, ConfirmDialog, StatusBadge, tokens, inputStyle, labelStyle, btnPrimary, btnGhost } from "./FinanceSharedUI";
import { formatYER, extractErrorMessage } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 8: Expenses
   ═══════════════════════════════════════════════════════════════════════════════ */
export function ExpensesTab() {
  const [data, setData] = useState<ExpenseListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; action: "approve" | "reject" | "delete" } | null>(null);

  // Create form
  const [eTitle, setETitle] = useState("");
  const [eCategory, setECategory] = useState("Other");
  const [eAmount, setEAmount] = useState("");
  const [eMethod, setEMethod] = useState("cash");
  const [eDate, setEDate] = useState(new Date().toISOString().slice(0, 10));

  const fetchData = useCallback(async () => {
    try { setLoading(true); const { data: responseData } = await api.get<{ data: ExpenseListItem[]; total: number }>("/api/expenses"); setData(responseData.data); } catch { toast.error("فشل في تحميل المصروفات"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleCreate = async () => {
    if (!eTitle.trim() || !eAmount || Number(eAmount) <= 0) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    try {
      setSubmitting(true);
      const payload: CreateExpenseRequest = {
        title: eTitle,
        category: eCategory,
        amount: Number(eAmount),
        paymentMethod: eMethod,
        expenseDate: eDate,
      };
      await api.post("/api/finance-v3/expenses", payload);
      toast.success("تم إنشاء المصروف بنجاح");
      setShowCreate(false);
      setETitle(""); setECategory("Other"); setEAmount(""); setEMethod("cash"); setEDate(new Date().toISOString().slice(0, 10));
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في إنشاء المصروف")); } finally { setSubmitting(false); }
  };

  const handleAction = async () => {
    if (!confirmAction) return;
    try {
      setSubmitting(true);
      if (confirmAction.action === "approve") {
        await api.post(`/api/finance-v3/expenses/${confirmAction.id}/approve`);
        toast.success("تم اعتماد المصروف");
      } else if (confirmAction.action === "reject") {
        await api.post(`/api/finance-v3/expenses/${confirmAction.id}/reject`);
        toast.success("تم رفض المصروف");
      } else if (confirmAction.action === "delete") {
        await api.delete(`/api/finance-v3/expenses/${confirmAction.id}`);
        toast.success("تم حذف/عكس المصروف");
      }
      setConfirmAction(null);
      fetchData();
    } catch (err) { toast.error(extractErrorMessage(err, "فشل في تنفيذ الإجراء")); } finally { setSubmitting(false); }
  };

  return (
    <div className="p-6 space-y-4">
      <SectionHeader title="المصروفات" action={
        <div className="flex items-center gap-2">
          <button onClick={() => setShowCreate(true)} style={btnPrimary}><Plus className="w-4 h-4" /> مصروف جديد</button>
          <button onClick={fetchData} className="w-8 h-8 rounded-md flex items-center justify-center" style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }} title="تحديث"><RefreshCw className="w-4 h-4" /></button>
        </div>
      } />

      {loading ? <LoadingSkeleton /> : data.length === 0 ? <EmptyState icon={TrendingDown} message="لا توجد مصروفات" /> : (
        <DataTable<ExpenseListItem>
          keyField="id"
          data={data}
          columns={[
            { key: "title", label: "العنوان" },
            { key: "category", label: "الفئة", render: (r) => EXPENSE_CATEGORIES.find((c) => c.value === r.category)?.label ?? r.category },
            { key: "amount", label: "المبلغ", render: (r) => formatYER(r.amount) },
            { key: "paymentMethod", label: "طريقة الدفع", render: (r) => PAYMENT_METHODS.find((m) => m.value === r.paymentMethod)?.label ?? r.paymentMethod },
            { key: "expenseDate", label: "التاريخ", render: (r) => new Date(r.expenseDate).toLocaleDateString("ar-SA") },
            { key: "status", label: "الحالة", render: (r) => <StatusBadge status={r.status} /> },
            { key: "actions", label: "إجراءات", render: (r) => (
              <div className="flex items-center gap-1">
                {r.status === "Pending" && (
                  <>
                    <button onClick={(e) => { e.stopPropagation(); setConfirmAction({ id: r.id, action: "approve" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.successBorder }} title="اعتماد"><ThumbsUp className="w-3.5 h-3.5" /></button>
                    <button onClick={(e) => { e.stopPropagation(); setConfirmAction({ id: r.id, action: "reject" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="رفض"><ThumbsDown className="w-3.5 h-3.5" /></button>
                  </>
                )}
                {!r.isReversal && <button onClick={(e) => { e.stopPropagation(); setConfirmAction({ id: r.id, action: "delete" }); }} className="w-7 h-7 rounded-md flex items-center justify-center" style={{ color: tokens.dangerBorder }} title="حذف/عكس"><Trash2 className="w-3.5 h-3.5" /></button>}
              </div>
            )},
          ]}
        />
      )}

      {/* Create Expense Modal */}
      <Modal open={showCreate} onClose={() => setShowCreate(false)} title="مصروف جديد">
        <div className="space-y-4">
          <div><label style={labelStyle}>العنوان <span style={{ color: tokens.dangerBorder }}>*</span></label><input value={eTitle} onChange={(e) => setETitle(e.target.value)} placeholder="وصف المصروف" style={inputStyle} /></div>
          <div><label style={labelStyle}>الفئة</label><select value={eCategory} onChange={(e) => setECategory(e.target.value)} style={inputStyle}>{EXPENSE_CATEGORIES.map((c) => (<option key={c.value} value={c.value}>{c.label}</option>))}</select></div>
          <div><label style={labelStyle}>المبلغ <span style={{ color: tokens.dangerBorder }}>*</span></label><input type="number" min="0.01" step="0.01" value={eAmount} onChange={(e) => setEAmount(e.target.value)} dir="ltr" style={inputStyle} /></div>
          <div><label style={labelStyle}>طريقة الدفع</label><select value={eMethod} onChange={(e) => setEMethod(e.target.value)} style={inputStyle}>{PAYMENT_METHODS.map((m) => (<option key={m.value} value={m.value}>{m.label}</option>))}</select></div>
          <div><label style={labelStyle}>تاريخ المصروف</label><input type="date" value={eDate} onChange={(e) => setEDate(e.target.value)} style={inputStyle} /></div>
          <div className="flex gap-3 pt-2 border-t" style={{ borderColor: tokens.border }}>
            <button onClick={() => setShowCreate(false)} style={btnGhost}>إلغاء</button>
            <button onClick={handleCreate} disabled={submitting} style={{ ...btnPrimary, opacity: submitting ? 0.6 : 1 }}>{submitting ? "جارٍ الحفظ..." : "إنشاء"}</button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={handleAction}
        title={confirmAction?.action === "approve" ? "اعتماد المصروف" : confirmAction?.action === "reject" ? "رفض المصروف" : "حذف/عكس المصروف"}
        message={confirmAction?.action === "approve" ? "هل أنت متأكد من اعتماد هذا المصروف؟" : confirmAction?.action === "reject" ? "هل أنت متأكد من رفض هذا المصروف؟" : "هل أنت متأكد من حذف/عكس هذا المصروف؟ سيتم إنشاء قيد عكسي."}
        confirmLabel={confirmAction?.action === "approve" ? "اعتماد" : confirmAction?.action === "reject" ? "رفض" : "حذف"}
        danger={confirmAction?.action !== "approve"}
      />
    </div>
  );
}
