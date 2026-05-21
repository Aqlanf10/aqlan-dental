"use client";

import { useState, useMemo } from "react";
import {
  BarChart2, Download, Filter, RefreshCw,
  TrendingUp, DollarSign, Percent, CheckCircle2,
  ChevronDown, AlertTriangle,
} from "lucide-react";
import { useCommissionReport } from "@/hooks/useCommissions";
import { useDoctors } from "@/hooks/useDoctors";
import type { CommissionStatus } from "@/types/commission";
import { cn } from "@/lib/utils";

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmt(n: number) {
  return n.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}

function today() { return new Date().toISOString().slice(0, 10); }
function firstOfMonth() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

const STATUS_CONFIG: Record<CommissionStatus, { label: string; color: string }> = {
  Pending:    { label: "قيد الانتظار", color: "bg-gray-100 text-gray-600" },
  Calculated: { label: "محسوبة",        color: "bg-blue-100 text-blue-700" },
  Approved:   { label: "معتمدة",        color: "bg-green-100 text-green-700" },
  Paid:       { label: "مدفوعة",        color: "bg-emerald-100 text-emerald-700" },
};

// ── Summary card ──────────────────────────────────────────────────────────────

function SummaryCard({ label, value, sub, color }: {
  label: string; value: string; sub?: string; color: string;
}) {
  return (
    <div className={cn("rounded-xl p-4 border", color)}>
      <p className="text-xs font-medium opacity-70 mb-1">{label}</p>
      <p className="text-xl font-extrabold">{value}</p>
      {sub && <p className="text-[11px] opacity-60 mt-0.5">{sub}</p>}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CommissionsPage() {
  const [from, setFrom]                   = useState(firstOfMonth());
  const [to, setTo]                       = useState(today());
  const [doctorId, setDoctorId]           = useState("");
  const [commissionStatus, setStatus]     = useState("");
  const [applied, setApplied]             = useState<{ from: string; to: string; doctorId?: string; commissionStatus?: string } | null>(null);

  const { data: doctors } = useDoctors();
  const { data: report, isLoading, error } = useCommissionReport(applied);

  const summary = report?.summary;
  const rows    = report?.rows ?? [];

  function applyFilters() {
    setApplied({
      from,
      to,
      doctorId:         doctorId || undefined,
      commissionStatus: commissionStatus || undefined,
    });
  }

  // ── Export CSV ────────────────────────────────────────────────────────────
  function exportCsv() {
    if (!rows.length) return;
    const headers = [
      "التاريخ","المريض","رقم الفاتورة","الخدمة","الطبيب",
      "الإجمالي","الخصم","تكلفة المواد","أجور المعمل","تكاليف أخرى",
      "الصافي الخاضع","نسبة الطبيب","عمولة الطبيب","المدفوع","المتبقي","الحالة",
    ];
    const csv = [
      headers.join(","),
      ...rows.map(r => [
        r.date.slice(0,10), r.patientName, r.invoiceNumber, r.serviceName,
        r.doctorName ?? "", r.grossAmount, r.discount, r.materialCost,
        r.labCost, r.otherCosts, r.netCommissionableAmount, r.doctorPercentage,
        r.doctorCommission, r.paidCommission, r.remainingCommission, r.status,
      ].join(","))
    ].join("\n");
    const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8" });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement("a");
    a.href = url; a.download = `commissions-${from}-${to}.csv`; a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">تقرير عمولات الأطباء</h1>
          <p className="text-sm text-gray-500 mt-0.5">محاسبة مستحقات الأطباء بعد خصم التكاليف المباشرة</p>
        </div>
        <button
          onClick={exportCsv}
          disabled={!rows.length}
          className="flex items-center gap-2 px-4 py-2 bg-white border border-gray-200 rounded-xl text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-40"
        >
          <Download className="w-4 h-4" /> تصدير CSV
        </button>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-2xl border border-gray-100 p-4">
        <div className="flex items-center gap-2 mb-3">
          <Filter className="w-4 h-4 text-gray-400" />
          <span className="text-sm font-semibold text-gray-700">الفلاتر</span>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <div>
            <label className="block text-xs text-gray-500 mb-1">من تاريخ</label>
            <input type="date" value={from} onChange={e => setFrom(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">إلى تاريخ</label>
            <input type="date" value={to} onChange={e => setTo(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">الطبيب</label>
            <select value={doctorId} onChange={e => setDoctorId(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white">
              <option value="">كل الأطباء</option>
              {doctors?.filter(d => d.isActive !== false).map(d => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">حالة العمولة</label>
            <select value={commissionStatus} onChange={e => setStatus(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-clinic-blue bg-white">
              <option value="">كل الحالات</option>
              <option value="Pending">قيد الانتظار</option>
              <option value="Calculated">محسوبة</option>
              <option value="Approved">معتمدة</option>
              <option value="Paid">مدفوعة</option>
            </select>
          </div>
        </div>
        <div className="mt-3 flex justify-end">
          <button
            onClick={applyFilters}
            className="flex items-center gap-2 px-5 py-2 bg-clinic-blue text-white rounded-xl text-sm font-semibold hover:bg-clinic-blue/90"
          >
            <RefreshCw className="w-4 h-4" /> عرض التقرير
          </button>
        </div>
      </div>

      {/* Summary cards */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
          <SummaryCard label="إجمالي العلاجات"   value={`${fmt(summary.totalGross)} ر.ي`}          color="bg-white border-gray-100 text-gray-800" />
          <SummaryCard label="إجمالي التكاليف"   value={`${fmt(summary.totalMaterialCost + summary.totalLabCost + summary.totalOtherCosts)} ر.ي`} color="bg-red-50 border-red-100 text-red-800" />
          <SummaryCard label="الصافي الخاضع"    value={`${fmt(summary.totalNet)} ر.ي`}             color="bg-blue-50 border-blue-100 text-blue-800" />
          <SummaryCard label="مستحق الأطباء"    value={`${fmt(summary.totalDoctorCommission)} ر.ي`} color="bg-purple-50 border-purple-100 text-purple-800" />
          <SummaryCard label="تم الدفع"          value={`${fmt(summary.totalPaid)} ر.ي`}            color="bg-green-50 border-green-100 text-green-800" />
          <SummaryCard label="المتبقي"            value={`${fmt(summary.totalRemaining)} ر.ي`}       color="bg-amber-50 border-amber-100 text-amber-800" />
        </div>
      )}

      {/* Table */}
      <div className="bg-white rounded-2xl border border-gray-100 overflow-hidden">
        {isLoading && (
          <div className="flex items-center justify-center py-16 text-gray-400 text-sm">
            <RefreshCw className="w-5 h-5 animate-spin ml-2" /> جاري التحميل…
          </div>
        )}

        {error && (
          <div className="flex items-center justify-center py-16 text-red-500 text-sm">
            <AlertTriangle className="w-5 h-5 ml-2" /> تعذّر تحميل التقرير
          </div>
        )}

        {!isLoading && !error && !applied && (
          <div className="flex flex-col items-center justify-center py-16 text-gray-400">
            <BarChart2 className="w-10 h-10 mb-3 opacity-40" />
            <p className="text-sm">اختر الفلاتر واضغط &quot;عرض التقرير&quot;</p>
          </div>
        )}

        {!isLoading && applied && rows.length === 0 && (
          <div className="flex flex-col items-center justify-center py-16 text-gray-400">
            <CheckCircle2 className="w-8 h-8 mb-3 opacity-40" />
            <p className="text-sm">لا توجد بيانات للفترة المحددة</p>
          </div>
        )}

        {!isLoading && rows.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 text-gray-600 text-xs">
                  {[
                    "التاريخ","المريض","رقم الفاتورة","الخدمة","الطبيب",
                    "الإجمالي","الخصم","تكلفة المواد","أجور المعمل","تكاليف أخرى",
                    "الصافي الخاضع","نسبة الطبيب","عمولة الطبيب","المدفوع","المتبقي","الحالة",
                  ].map(h => (
                    <th key={h} className="px-3 py-3 text-right font-semibold whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {rows.map((row, i) => {
                  const st = STATUS_CONFIG[row.status as CommissionStatus] ?? STATUS_CONFIG.Pending;
                  return (
                    <tr key={i} className="hover:bg-gray-50 transition">
                      <td className="px-3 py-2.5 text-gray-500 whitespace-nowrap">{row.date.slice(0,10)}</td>
                      <td className="px-3 py-2.5 font-medium text-gray-900 whitespace-nowrap">{row.patientName}</td>
                      <td className="px-3 py-2.5 text-gray-500 font-mono text-xs">{row.invoiceNumber}</td>
                      <td className="px-3 py-2.5 text-gray-700 max-w-[140px] truncate">{row.serviceName}</td>
                      <td className="px-3 py-2.5 text-gray-700 whitespace-nowrap">{row.doctorName ?? "—"}</td>
                      <td className="px-3 py-2.5 font-medium text-gray-800 whitespace-nowrap">{fmt(row.grossAmount)}</td>
                      <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.discount > 0 ? `(${fmt(row.discount)})` : "—"}</td>
                      <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.materialCost > 0 ? `(${fmt(row.materialCost)})` : "—"}</td>
                      <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.labCost > 0 ? `(${fmt(row.labCost)})` : "—"}</td>
                      <td className="px-3 py-2.5 text-red-600 whitespace-nowrap">{row.otherCosts > 0 ? `(${fmt(row.otherCosts)})` : "—"}</td>
                      <td className="px-3 py-2.5 font-bold text-blue-700 whitespace-nowrap">{fmt(row.netCommissionableAmount)}</td>
                      <td className="px-3 py-2.5 text-gray-600 whitespace-nowrap">{row.doctorPercentage}%</td>
                      <td className="px-3 py-2.5 font-bold text-purple-700 whitespace-nowrap">{fmt(row.doctorCommission)}</td>
                      <td className="px-3 py-2.5 text-green-700 whitespace-nowrap">{fmt(row.paidCommission)}</td>
                      <td className="px-3 py-2.5 text-amber-700 whitespace-nowrap">{fmt(row.remainingCommission)}</td>
                      <td className="px-3 py-2.5">
                        <span className={cn("text-[10px] font-semibold px-2 py-0.5 rounded-full", st.color)}>
                          {st.label}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
