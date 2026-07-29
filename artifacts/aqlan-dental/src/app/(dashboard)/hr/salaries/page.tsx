
import { useState, useEffect, useCallback } from "react";
import {
  Banknote,
  CheckCircle,
  Loader2,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { useBranches } from "@/hooks/useBranches";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";

interface SalaryRecord {
  id: string;
  employeeId: string;
  employeeName: string;
  year: number;
  month: number;
  baseSalary: number;
  deductions: number;
  advances: number;
  bonuses: number;
  netSalary: number;
  paidAt: string | null;
  paymentMethod: string | null;
  notes: string | null;
}

const MONTH_NAMES = Array.from({ length: 12 }, (_, i) =>
  new Date(2000, i, 1).toLocaleDateString("ar-SA", { month: "long" })
);

export default function SalariesPage() {
  const [year, setYear] = useState(() => new Date().getFullYear());
  const [month, setMonth] = useState(() => new Date().getMonth() + 1);
  const [branchFilter, setBranchFilter] = useState("");
  const [paidFilter, setPaidFilter] = useState<"" | "true" | "false">("");
  const [page, setPage] = useState(1);

  const [records, setRecords] = useState<SalaryRecord[]>([]);
  const [, setTotal] = useState(0);
  const [, setTotalPages] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  // Generate all modal
  const [showGenerateAll, setShowGenerateAll] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);

  // Pay salary modal
  const [payingId, setPayingId] = useState<string | null>(null);
  const [paymentMethod, setPaymentMethod] = useState("نقدي");

  const { data: branches } = useBranches();

  const fetchRecords = useCallback(async () => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      params.set("year", String(year));
      params.set("month", String(month));
      if (branchFilter) params.set("branchId", branchFilter);
      if (paidFilter) params.set("paid", paidFilter);
      params.set("page", String(page));
      params.set("pageSize", "50");
      const { data } = await api.get(`/api/salaries?${params.toString()}`);
      setRecords(data.data || []);
      setTotal(data.total || 0);
      setTotalPages(data.totalPages || 1);
    } catch {
      toast.error("حدث خطأ أثناء تحميل البيانات");
    } finally {
      setIsLoading(false);
    }
  }, [year, month, branchFilter, paidFilter, page]);

  useEffect(() => { fetchRecords(); }, [fetchRecords]);

  const handleGenerateAll = async () => {
    setIsGenerating(true);
    try {
      const { data } = await api.post("/api/salaries/generate-all", { year, month });
      toast.success(data.message || "تم إنشاء الكشوف بنجاح");
      setShowGenerateAll(false);
      fetchRecords();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    } finally {
      setIsGenerating(false);
    }
  };

  const handlePay = async (id: string) => {
    try {
      await api.put(`/api/salaries/${id}/pay`, { paymentMethod });
      toast.success("تم تسجيل صرف الراتب بنجاح");
      setPayingId(null);
      fetchRecords();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    }
  };

  const formatMoney = (val: number) => val.toLocaleString("ar-SA");

  // Totals
  const totalBase = records.reduce((s, r) => s + r.baseSalary, 0);
  const totalDeductions = records.reduce((s, r) => s + r.deductions, 0);
  const totalAdvances = records.reduce((s, r) => s + r.advances, 0);
  const totalBonuses = records.reduce((s, r) => s + r.bonuses, 0);
  const totalNet = records.reduce((s, r) => s + r.netSalary, 0);
  const paidCount = records.filter((r) => r.paidAt).length;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">إدارة الرواتب</h1>
          <p className="text-sm text-gray-500 mt-1">إنشاء كشوف الرواتب الشهرية وإدارة الصرف</p>
        </div>
        <button onClick={() => setShowGenerateAll(true)}
          className="flex items-center gap-2 bg-[#f5922e] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm">
          <Banknote className="w-4 h-4" /> إنشاء كشوف الشهر
        </button>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        {[
          { label: "إجمالي الرواتب", value: formatMoney(totalBase), color: "text-[#1a3a5c]" },
          { label: "الخصومات", value: formatMoney(totalDeductions), color: "text-red-600" },
          { label: "السلف", value: formatMoney(totalAdvances), color: "text-amber-600" },
          { label: "البدلات", value: formatMoney(totalBonuses), color: "text-emerald-600" },
          { label: "الصافي", value: formatMoney(totalNet), color: "text-[#0d2137] font-bold" },
        ].map((card) => (
          <div key={card.label} className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
            <p className="text-xs text-gray-500 mb-1">{card.label}</p>
            <p className={cn("text-lg font-bold", card.color)} dir="ltr">{card.value} ر.ي</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <select value={month} onChange={(e) => { setMonth(Number(e.target.value)); setPage(1); }}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
          {MONTH_NAMES.map((name, i) => (<option key={i} value={i + 1}>{name}</option>))}
        </select>
        <select value={year} onChange={(e) => setYear(Number(e.target.value))}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
          {[2024, 2025, 2026].map((y) => (<option key={y} value={y}>{y}</option>))}
        </select>
        <select value={branchFilter} onChange={(e) => { setBranchFilter(e.target.value); setPage(1); }}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
          <option value="">جميع الفروع</option>
          {branches?.map((b: { id: string; name: string }) => (<option key={b.id} value={b.id}>{b.name}</option>))}
        </select>
        <select value={paidFilter} onChange={(e) => { setPaidFilter(e.target.value as "" | "true" | "false"); setPage(1); }}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
          <option value="">الكل</option>
          <option value="true">تم الصرف</option>
          <option value="false">لم يُصرف</option>
        </select>
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6"><TableSkeleton rows={5} cols={8} /></div>
      ) : records.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <Banknote className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">لا يوجد كشوف رواتب لهذا الشهر</p>
          <p className="text-sm mt-1 text-gray-300">اضغط &quot;إنشاء كشوف الشهر&quot; للبدء</p>
        </div>
      ) : (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/60">
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الموظف</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الراتب الأساسي</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الخصومات</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">السلف</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">البدلات</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الصافي</th>
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الحالة</th>
                  <th className="text-center px-4 py-3 font-semibold text-gray-600">إجراء</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {records.map((rec) => (
                  <tr key={rec.id} className="hover:bg-gray-50/50 transition">
                    <td className="px-4 py-3 font-medium text-[#0d2137]">{rec.employeeName}</td>
                    <td className="px-4 py-3 text-gray-600" dir="ltr">{formatMoney(rec.baseSalary)}</td>
                    <td className="px-4 py-3 text-red-600" dir="ltr">{formatMoney(rec.deductions)}</td>
                    <td className="px-4 py-3 text-amber-600" dir="ltr">{formatMoney(rec.advances)}</td>
                    <td className="px-4 py-3 text-emerald-600" dir="ltr">{formatMoney(rec.bonuses)}</td>
                    <td className="px-4 py-3 font-bold text-[#0d2137]" dir="ltr">{formatMoney(rec.netSalary)}</td>
                    <td className="px-4 py-3">
                      {rec.paidAt ? (
                        <span className="inline-flex items-center gap-1 text-[11px] font-semibold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700">
                          <CheckCircle className="w-3 h-3" /> تم الصرف
                        </span>
                      ) : (
                        <span className="inline-flex items-center text-[11px] font-semibold px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">
                          لم يُصرف
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center">
                      {!rec.paidAt && (
                        <button onClick={() => setPayingId(rec.id)}
                          className="text-xs font-semibold text-[#1a3a5c] hover:text-[#f5922e] transition">
                          صرف الراتب
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="px-4 py-3 border-t border-gray-100 bg-gray-50/40 text-xs text-gray-500">
            {paidCount} من {records.length} تم صرفهم — إجمالي الصافي: {formatMoney(totalNet)} ر.ي
          </div>
        </div>
      )}

      {/* Generate All Modal */}
      {showGenerateAll && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setShowGenerateAll(false)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6">
            <h3 className="text-lg font-bold text-gray-900 mb-2">إنشاء كشوف رواتب</h3>
            <p className="text-sm text-gray-500 mb-4">
              سيتم إنشاء كشف راتب لجميع الموظفين النشطين لشهر {MONTH_NAMES[month - 1]} {year}
            </p>
            <div className="flex items-center gap-3">
              <button onClick={() => setShowGenerateAll(false)}
                className="flex-1 px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition">إلغاء</button>
              <button onClick={handleGenerateAll} disabled={isGenerating}
                className="flex-1 flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl text-sm font-semibold text-white bg-[#f5922e] hover:opacity-90 shadow-sm disabled:opacity-50">
                {isGenerating && <Loader2 className="w-4 h-4 animate-spin" />}
                {isGenerating ? "جارٍ الإنشاء..." : "إنشاء"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Pay Modal */}
      {payingId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setPayingId(null)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6">
            <h3 className="text-lg font-bold text-gray-900 mb-4">تسجيل صرف الراتب</h3>
            <div className="mb-4">
              <label className="block text-sm font-medium text-gray-700 mb-1.5">طريقة الدفع</label>
              <select value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white">
                <option value="نقدي">نقدي</option>
                <option value="تحويل بنكي">تحويل بنكي</option>
                <option value="شيك">شيك</option>
              </select>
            </div>
            <div className="flex items-center gap-3">
              <button onClick={() => setPayingId(null)}
                className="flex-1 px-5 py-2.5 rounded-xl text-sm font-medium text-gray-600 hover:bg-gray-100 transition">إلغاء</button>
              <button onClick={() => handlePay(payingId)}
                className="flex-1 px-5 py-2.5 rounded-xl text-sm font-semibold text-white bg-emerald-600 hover:bg-emerald-700 shadow-sm">
                تأكيد الصرف
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
