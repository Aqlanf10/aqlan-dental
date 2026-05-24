"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Wallet,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Loader2,
  Plus,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { useBranches } from "@/hooks/useBranches";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";

interface AdvanceRecord {
  id: string;
  employeeId: string;
  employeeName: string;
  amount: number;
  reason: string | null;
  requestDate: string;
  status: string;
  approvedAt: string | null;
  rejectionReason: string | null;
  deductFromMonth: number | null;
  deductFromYear: number | null;
  isDeducted: boolean;
}

const STATUS_CONFIG: Record<string, { label: string; color: string }> = {
  Pending: { label: "قيد المراجعة", color: "bg-amber-100 text-amber-700" },
  Approved: { label: "موافق عليه", color: "bg-emerald-100 text-emerald-700" },
  Rejected: { label: "مرفوض", color: "bg-red-100 text-red-700" },
};

export default function AdvancesPage() {
  const [statusFilter, setStatusFilter] = useState("");
  const [branchFilter, setBranchFilter] = useState("");
  const [page, setPage] = useState(1);

  const [records, setRecords] = useState<AdvanceRecord[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  // New advance modal
  const [showNew, setShowNew] = useState(false);
  const [employees, setEmployees] = useState<{ id: string; fullName: string }[]>([]);
  const [newForm, setNewForm] = useState({ employeeId: "", amount: "", reason: "", deductFromMonth: "", deductFromYear: "" });
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Approval action
  const [actionId, setActionId] = useState<string | null>(null);
  const [actionType, setActionType] = useState<"approve" | "reject">("approve");
  const [rejectionReason, setRejectionReason] = useState("");

  const { data: branches } = useBranches();

  const fetchRecords = useCallback(async () => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      if (statusFilter) params.set("status", statusFilter);
      if (branchFilter) params.set("branchId", branchFilter);
      params.set("page", String(page));
      params.set("pageSize", "50");
      const { data } = await api.get(`/api/advances?${params.toString()}`);
      setRecords(data.data || []);
      setTotal(data.total || 0);
      setTotalPages(data.totalPages || 1);
    } catch {
      toast.error("حدث خطأ أثناء تحميل البيانات");
    } finally {
      setIsLoading(false);
    }
  }, [statusFilter, branchFilter, page]);

  useEffect(() => { fetchRecords(); }, [fetchRecords]);

  const fetchEmployees = async () => {
    try {
      const { data } = await api.get("/api/employees?pageSize=100");
      setEmployees((data.data || []).map((e: { id: string; fullName: string }) => ({ id: e.id, fullName: e.fullName })));
    } catch { /* ignore */ }
  };

  const handleCreate = async () => {
    if (!newForm.employeeId || !newForm.amount || Number(newForm.amount) <= 0) {
      toast.error("يرجى ملء جميع الحقول المطلوبة");
      return;
    }
    setIsSubmitting(true);
    try {
      await api.post("/api/advances", {
        employeeId: newForm.employeeId,
        amount: Number(newForm.amount),
        reason: newForm.reason || undefined,
        deductFromMonth: newForm.deductFromMonth ? Number(newForm.deductFromMonth) : undefined,
        deductFromYear: newForm.deductFromYear ? Number(newForm.deductFromYear) : undefined,
      });
      toast.success("تم إنشاء طلب السلفة بنجاح");
      setShowNew(false);
      setNewForm({ employeeId: "", amount: "", reason: "", deductFromMonth: "", deductFromYear: "" });
      fetchRecords();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleAction = async () => {
    if (!actionId) return;
    try {
      await api.put(`/api/advances/${actionId}/approve`, {
        approve: actionType === "approve",
        rejectionReason: actionType === "reject" ? rejectionReason : undefined,
      });
      toast.success(actionType === "approve" ? "تم الموافقة على السلفة" : "تم رفض السلفة");
      setActionId(null);
      setRejectionReason("");
      fetchRecords();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    }
  };

  const formatMoney = (val: number) => val.toLocaleString("ar-SA");
  const totalPending = records.filter((r) => r.status === "Pending").reduce((s, r) => s + r.amount, 0);
  const totalApproved = records.filter((r) => r.status === "Approved").reduce((s, r) => s + r.amount, 0);

  return (
    <div className="space-y-6" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">السلف</h1>
          <p className="text-sm text-gray-500 mt-1">إدارة طلبات السلف للموظفين والموافقة عليها</p>
        </div>
        <button onClick={() => { fetchEmployees(); setShowNew(true); }}
          className="flex items-center gap-2 bg-[#f5922e] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm">
          <Plus className="w-4 h-4" /> طلب سلفة
        </button>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 gap-3">
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
          <p className="text-xs text-gray-500 mb-1">قيد المراجعة</p>
          <p className="text-lg font-bold text-amber-600" dir="ltr">{formatMoney(totalPending)} ر.ي</p>
        </div>
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
          <p className="text-xs text-gray-500 mb-1">تمت الموافقة</p>
          <p className="text-lg font-bold text-emerald-600" dir="ltr">{formatMoney(totalApproved)} ر.ي</p>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <select value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
          <option value="">جميع الحالات</option>
          <option value="Pending">قيد المراجعة</option>
          <option value="Approved">موافق عليه</option>
          <option value="Rejected">مرفوض</option>
        </select>
        <select value={branchFilter} onChange={(e) => { setBranchFilter(e.target.value); setPage(1); }}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
          <option value="">جميع الفروع</option>
          {branches?.map((b: { id: string; name: string }) => (<option key={b.id} value={b.id}>{b.name}</option>))}
        </select>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6"><TableSkeleton rows={5} cols={7} /></div>
      ) : records.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <Wallet className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">لا يوجد طلبات سلف</p>
        </div>
      ) : (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/60">
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الموظف</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">المبلغ</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">السبب</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">تاريخ الطلب</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الحالة</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الخصم من</th>
                  <th className="text-center px-4 py-3 font-semibold text-gray-600">إجراء</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {records.map((rec) => {
                  const cfg = STATUS_CONFIG[rec.status] ?? { label: rec.status, color: "bg-gray-100 text-gray-700" };
                  return (
                    <tr key={rec.id} className="hover:bg-gray-50/50 transition">
                      <td className="px-4 py-3 font-medium text-[#0d2137]">{rec.employeeName}</td>
                      <td className="px-4 py-3 font-bold text-[#0d2137]" dir="ltr">{formatMoney(rec.amount)}</td>
                      <td className="px-4 py-3 text-gray-500 text-xs max-w-[200px] truncate">{rec.reason ?? "—"}</td>
                      <td className="px-4 py-3 text-gray-500">{new Date(rec.requestDate).toLocaleDateString("ar-SA")}</td>
                      <td className="px-4 py-3">
                        <span className={cn("inline-flex items-center text-[11px] font-semibold px-2 py-0.5 rounded-full", cfg.color)}>{cfg.label}</span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs">
                        {rec.deductFromMonth && rec.deductFromYear
                          ? `${rec.deductFromMonth}/${rec.deductFromYear}` : "—"}
                        {rec.isDeducted && <span className="text-emerald-600 mr-1">(تم الخصم)</span>}
                      </td>
                      <td className="px-4 py-3 text-center">
                        {rec.status === "Pending" && (
                          <div className="flex items-center justify-center gap-1">
                            <button onClick={() => { setActionId(rec.id); setActionType("approve"); }}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-emerald-600 hover:bg-emerald-50 transition" title="موافقة">
                              <CheckCircle className="w-4 h-4" />
                            </button>
                            <button onClick={() => { setActionId(rec.id); setActionType("reject"); }}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-red-600 hover:bg-red-50 transition" title="رفض">
                              <XCircle className="w-4 h-4" />
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* New Advance Modal */}
      {showNew && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setShowNew(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-md p-6">
            <h3 className="text-lg font-bold text-gray-900 mb-4">طلب سلفة جديدة</h3>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">الموظف <span className="text-red-500">*</span></label>
                <select value={newForm.employeeId} onChange={(e) => setNewForm((p) => ({ ...p, employeeId: e.target.value }))}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white">
                  <option value="">اختر الموظف</option>
                  {employees.map((e) => (<option key={e.id} value={e.id}>{e.fullName}</option>))}
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">المبلغ <span className="text-red-500">*</span></label>
                <input type="number" min="1" step="0.01" value={newForm.amount}
                  onChange={(e) => setNewForm((p) => ({ ...p, amount: e.target.value }))}
                  placeholder="مبلغ السلفة" dir="ltr"
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition" />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1.5">السبب</label>
                <input value={newForm.reason} onChange={(e) => setNewForm((p) => ({ ...p, reason: e.target.value }))}
                  placeholder="سبب طلب السلفة"
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition" />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">شهر الخصم</label>
                  <input type="number" min="1" max="12" value={newForm.deductFromMonth}
                    onChange={(e) => setNewForm((p) => ({ ...p, deductFromMonth: e.target.value }))}
                    placeholder="1-12"
                    className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">سنة الخصم</label>
                  <input type="number" min="2024" max="2030" value={newForm.deductFromYear}
                    onChange={(e) => setNewForm((p) => ({ ...p, deductFromYear: e.target.value }))}
                    placeholder="2026"
                    className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition" />
                </div>
              </div>
            </div>
            <div className="flex items-center gap-3 mt-6">
              <button onClick={() => setShowNew(false)}
                className="flex-1 px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition">إلغاء</button>
              <button onClick={handleCreate} disabled={isSubmitting}
                className="flex-1 flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl text-sm font-semibold text-white bg-[#f5922e] hover:opacity-90 shadow-sm disabled:opacity-50">
                {isSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
                {isSubmitting ? "جارٍ الحفظ..." : "إنشاء الطلب"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Action Modal */}
      {actionId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setActionId(null)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6">
            <h3 className="text-lg font-bold text-gray-900 mb-4">
              {actionType === "approve" ? "الموافقة على السلفة" : "رفض السلفة"}
            </h3>
            {actionType === "reject" && (
              <div className="mb-4">
                <label className="block text-sm font-medium text-gray-700 mb-1.5">سبب الرفض</label>
                <input value={rejectionReason} onChange={(e) => setRejectionReason(e.target.value)}
                  placeholder="سبب رفض الطلب"
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition" />
              </div>
            )}
            <div className="flex items-center gap-3">
              <button onClick={() => setActionId(null)}
                className="flex-1 px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition">إلغاء</button>
              <button onClick={handleAction}
                className={cn("flex-1 px-5 py-2.5 rounded-xl text-sm font-semibold text-white shadow-sm",
                  actionType === "approve" ? "bg-emerald-600 hover:bg-emerald-700" : "bg-red-600 hover:bg-red-700")}>
                {actionType === "approve" ? "موافقة" : "رفض"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
