"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Clock,
  LogIn,
  LogOut,
  CalendarDays,
  AlertTriangle,
  FileSpreadsheet,
} from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { useBranches } from "@/hooks/useBranches";
import { toast } from "@/stores/toastStore";
import { TableSkeleton } from "@/components/ui/skeleton";

// ─── Types ──────────────────────────────────────────────────────────────

interface AttendanceRecord {
  id: string;
  employeeId: string;
  employeeName: string;
  date: string;
  checkIn: string | null;
  checkOut: string | null;
  status: string;
  notes: string | null;
}

interface AttendanceSummary {
  employeeId: string;
  employeeName: string;
  totalDays: number;
  presentDays: number;
  absentDays: number;
  lateDays: number;
  halfDays: number;
  leaveDays: number;
  holidayDays: number;
  totalWorkHours: number;
}

const STATUS_CONFIG: Record<string, { label: string; color: string }> = {
  Present: { label: "حاضر", color: "bg-emerald-100 text-emerald-700" },
  Absent: { label: "غائب", color: "bg-red-100 text-red-700" },
  Late: { label: "متأخر", color: "bg-amber-100 text-amber-700" },
  HalfDay: { label: "نصف يوم", color: "bg-sky-100 text-sky-700" },
  Leave: { label: "إجازة", color: "bg-purple-100 text-purple-700" },
  Holiday: { label: "عطلة", color: "bg-gray-100 text-gray-700" },
};

export default function AttendancePage() {
  const [view, setView] = useState<"daily" | "summary">("daily");
  const [date, setDate] = useState(() => new Date().toISOString().split("T")[0]);
  const [year, setYear] = useState(() => new Date().getFullYear());
  const [month, setMonth] = useState(() => new Date().getMonth() + 1);
  const [branchFilter, setBranchFilter] = useState("");
  const [page, setPage] = useState(1);

  const [records, setRecords] = useState<AttendanceRecord[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  const [summary, setSummary] = useState<AttendanceSummary[]>([]);
  const [summaryLoading, setSummaryLoading] = useState(false);

  const { data: branches } = useBranches();

  const fetchRecords = useCallback(async () => {
    setIsLoading(true);
    try {
      const params = new URLSearchParams();
      if (date) params.set("date", date);
      if (branchFilter) params.set("branchId", branchFilter);
      params.set("page", String(page));
      params.set("pageSize", "50");
      const { data } = await api.get(`/api/attendance?${params.toString()}`);
      setRecords(data.data || []);
      setTotal(data.total || 0);
      setTotalPages(data.totalPages || 1);
    } catch {
      toast.error("حدث خطأ أثناء تحميل البيانات");
    } finally {
      setIsLoading(false);
    }
  }, [date, branchFilter, page]);

  const fetchSummary = useCallback(async () => {
    setSummaryLoading(true);
    try {
      const params = new URLSearchParams();
      params.set("year", String(year));
      params.set("month", String(month));
      if (branchFilter) params.set("branchId", branchFilter);
      const { data } = await api.get(`/api/attendance/summary?${params.toString()}`);
      setSummary(Array.isArray(data) ? data : []);
    } catch {
      toast.error("حدث خطأ أثناء تحميل الملخص");
    } finally {
      setSummaryLoading(false);
    }
  }, [year, month, branchFilter]);

  useEffect(() => {
    if (view === "daily") fetchRecords();
    else fetchSummary();
  }, [view, fetchRecords, fetchSummary]);

  const handleCheckIn = async () => {
    try {
      const { data } = await api.post("/api/attendance/check-in", {});
      toast.success(data.message || "تم تسجيل الحضور");
      fetchRecords();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    }
  };

  const handleCheckOut = async () => {
    try {
      const { data } = await api.post("/api/attendance/check-out", {});
      toast.success(data.message || "تم تسجيل الانصراف");
      fetchRecords();
    } catch (err: unknown) {
      const msg = err && typeof err === "object" && "response" in err
        ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? "حدث خطأ")
        : "حدث خطأ";
      toast.error(msg);
    }
  };

  const formatDate = (dateStr: string) => {
    try { return new Date(dateStr).toLocaleDateString("ar-SA"); } catch { return dateStr; }
  };

  return (
    <div className="space-y-6" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-bold text-[#0d2137]">سجل الحضور والانصراف</h1>
          <p className="text-sm text-gray-500 mt-1">تتبع حضور الموظفين وانصرافهم يومياً وشهرياً</p>
        </div>
        <div className="flex items-center gap-3">
          <button onClick={handleCheckIn}
            className="flex items-center gap-2 bg-emerald-600 text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:bg-emerald-700 transition shadow-sm">
            <LogIn className="w-4 h-4" /> تسجيل حضور
          </button>
          <button onClick={handleCheckOut}
            className="flex items-center gap-2 bg-[#1a3a5c] text-white px-4 py-2.5 rounded-xl text-sm font-semibold hover:opacity-90 transition shadow-sm">
            <LogOut className="w-4 h-4" /> تسجيل انصراف
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="flex rounded-xl overflow-hidden border border-gray-200">
          <button onClick={() => setView("daily")}
            className={cn("px-4 py-2 text-sm font-medium transition", view === "daily" ? "bg-[#1a3a5c] text-white" : "bg-white text-gray-600 hover:bg-gray-50")}>
            <CalendarDays className="w-4 h-4 inline ml-1" /> يومي
          </button>
          <button onClick={() => setView("summary")}
            className={cn("px-4 py-2 text-sm font-medium transition", view === "summary" ? "bg-[#1a3a5c] text-white" : "bg-white text-gray-600 hover:bg-gray-50")}>
            <FileSpreadsheet className="w-4 h-4 inline ml-1" /> ملخص شهري
          </button>
        </div>

        {view === "daily" ? (
          <input type="date" value={date} onChange={(e) => { setDate(e.target.value); setPage(1); }}
            className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition" />
        ) : (
          <>
            <select value={month} onChange={(e) => setMonth(Number(e.target.value))}
              className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
              {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                <option key={m} value={m}>{new Date(2000, m - 1, 1).toLocaleDateString("ar-SA", { month: "long" })}</option>
              ))}
            </select>
            <select value={year} onChange={(e) => setYear(Number(e.target.value))}
              className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition">
              {[2024, 2025, 2026].map((y) => (<option key={y} value={y}>{y}</option>))}
            </select>
          </>
        )}

        <select value={branchFilter} onChange={(e) => { setBranchFilter(e.target.value); setPage(1); }}
          className="border border-gray-200 rounded-xl px-3 py-2.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-[#1a3a5c]/40 transition min-w-[140px]">
          <option value="">جميع الفروع</option>
          {branches?.map((b: { id: string; name: string }) => (<option key={b.id} value={b.id}>{b.name}</option>))}
        </select>
      </div>

      {/* Content */}
      {view === "daily" ? (
        isLoading ? (
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6"><TableSkeleton rows={5} cols={6} /></div>
        ) : records.length === 0 ? (
          <div className="text-center py-24 text-gray-400">
            <Clock className="w-12 h-12 mx-auto mb-3 opacity-40" />
            <p className="font-medium">لا يوجد سجلات حضور لهذا التاريخ</p>
          </div>
        ) : (
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 bg-gray-50/60">
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">الموظف</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">التاريخ</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">الحضور</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">الانصراف</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">الحالة</th>
                    <th className="text-right px-4 py-3 font-semibold text-gray-600">ملاحظات</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {records.map((rec) => {
                    const cfg = STATUS_CONFIG[rec.status] ?? { label: rec.status, color: "bg-gray-100 text-gray-700" };
                    return (
                      <tr key={rec.id} className="hover:bg-gray-50/50 transition">
                        <td className="px-4 py-3 font-medium text-[#0d2137]">{rec.employeeName}</td>
                        <td className="px-4 py-3 text-gray-500">{formatDate(rec.date)}</td>
                        <td className="px-4 py-3 font-mono text-sm" dir="ltr">{rec.checkIn ?? "—"}</td>
                        <td className="px-4 py-3 font-mono text-sm" dir="ltr">{rec.checkOut ?? "—"}</td>
                        <td className="px-4 py-3">
                          <span className={cn("inline-flex items-center text-[11px] font-semibold px-2 py-0.5 rounded-full", cfg.color)}>{cfg.label}</span>
                        </td>
                        <td className="px-4 py-3 text-gray-400 text-xs max-w-[200px] truncate">{rec.notes ?? "—"}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100 bg-gray-50/40">
                <p className="text-xs text-gray-500">عرض {records.length} من {total}</p>
                <div className="flex items-center gap-2">
                  <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}
                    className="px-3 py-1 rounded-lg text-sm text-gray-500 hover:bg-gray-200 disabled:opacity-40">السابق</button>
                  <span className="text-sm text-gray-600">{page} / {totalPages}</span>
                  <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages}
                    className="px-3 py-1 rounded-lg text-sm text-gray-500 hover:bg-gray-200 disabled:opacity-40">التالي</button>
                </div>
              </div>
            )}
          </div>
        )
      ) : summaryLoading ? (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6"><TableSkeleton rows={5} cols={7} /></div>
      ) : summary.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <FileSpreadsheet className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">لا يوجد بيانات حضور لهذا الشهر</p>
        </div>
      ) : (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/60">
                  <th className="text-right px-4 py-3 font-semibold text-gray-600">الموظف</th>
                  <th className="text-center px-4 py-3 font-semibold text-emerald-600">حاضر</th>
                  <th className="text-center px-4 py-3 font-semibold text-red-600">غائب</th>
                  <th className="text-center px-4 py-3 font-semibold text-amber-600">متأخر</th>
                  <th className="text-center px-4 py-3 font-semibold text-sky-600">نصف يوم</th>
                  <th className="text-center px-4 py-3 font-semibold text-purple-600">إجازة</th>
                  <th className="text-center px-4 py-3 font-semibold text-gray-600">ساعات العمل</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {summary.map((s) => (
                  <tr key={s.employeeId} className="hover:bg-gray-50/50 transition">
                    <td className="px-4 py-3 font-medium text-[#0d2137]">{s.employeeName}</td>
                    <td className="px-4 py-3 text-center text-emerald-600 font-semibold">{s.presentDays}</td>
                    <td className="px-4 py-3 text-center text-red-600 font-semibold">{s.absentDays}</td>
                    <td className="px-4 py-3 text-center text-amber-600 font-semibold">{s.lateDays}</td>
                    <td className="px-4 py-3 text-center text-sky-600 font-semibold">{s.halfDays}</td>
                    <td className="px-4 py-3 text-center text-purple-600 font-semibold">{s.leaveDays}</td>
                    <td className="px-4 py-3 text-center text-gray-600">
                      {s.totalWorkHours > 0 ? `${s.totalWorkHours.toFixed(1)} ساعة` : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
