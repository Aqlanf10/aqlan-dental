"use client";

import React, { useState, useEffect, useCallback } from "react";
import { 
  Filter, Users, CreditCard, TrendingUp, Wallet, Banknote, 
  Download, RefreshCw 
} from "lucide-react";
import { api } from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";
import { localDateString } from "@/lib/utils";
import {
  tokens, KpiCard, SectionHeader, LoadingSkeleton, EmptyState,
  inputStyle, labelStyle, btnPrimary, btnGhost 
} from "./FinanceSharedUI";
import { formatMoney, formatNumber } from "./FinanceHelpers";
import { toast } from "@/stores/toastStore";
import { CommissionAdjustmentsPanel } from "./CommissionAdjustmentsPanel";
import { PartyAccountStatementPanel } from "./PartyAccountStatementPanel";

interface DoctorCommissionSummary {
  doctorId: string;
  branchId: string;
  currency: string;
  doctorName: string;
  casesCount: number;
  totalServiceValue: number;
  commissionPercentage: number;
  commissionDue: number;
  commissionPaid: number;
  commissionRemaining: number;
}

export function CommissionsTab() {
  // FE-13: useDoctors() replaces useState + fetch.
  const { data: allDoctors = [], isLoading: doctorsLoading } = useDoctors();
  const doctors = allDoctors.filter((d) => d.isActive !== false);
  const [data, setData] = useState<DoctorCommissionSummary[]>([]);
  const [loading, setLoading] = useState(false);

  // Filters state
  const [selectedDoctorId, setSelectedDoctorId] = useState<string>("");
  const [startDate, setStartDate] = useState<string>(() => {
    const d = new Date();
    return `${d.getFullYear()}-01-01`;
  });
  const [endDate, setEndDate] = useState<string>(() => {
    return localDateString();
  });

  // Fetch aggregated commissions data
  const fetchCommissions = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (selectedDoctorId) params.set("doctorId", selectedDoctorId);
      if (startDate) params.set("from", startDate);
      if (endDate) params.set("to", endDate);

      const res = await api.get<DoctorCommissionSummary[]>(
        `/api/finance-v3/doctor-commissions?${params.toString()}`
      );
      setData(res.data ?? []);
    } catch (err) {
      console.error("Failed to fetch doctor commissions", err);
      toast.error("تعذر تحميل بيانات العمولات");
    } finally {
      setLoading(false);
    }
  }, [selectedDoctorId, startDate, endDate]);

  useEffect(() => {
    fetchCommissions();
  }, [fetchCommissions]);

  const handleApplyFilters = (e: React.FormEvent) => {
    e.preventDefault();
    fetchCommissions();
  };

  // Aggregated totals for the KPI cards
  const totalCases = data.reduce((sum, item) => sum + item.casesCount, 0);
  const totalsByCurrency = Array.from(
    data.reduce((totals, item) => {
      const current = totals.get(item.currency) ?? {
        currency: item.currency,
        totalServiceValue: 0,
        commissionDue: 0,
        commissionPaid: 0,
        commissionRemaining: 0,
      };
      current.totalServiceValue += item.totalServiceValue;
      current.commissionDue += item.commissionDue;
      current.commissionPaid += item.commissionPaid;
      current.commissionRemaining += item.commissionRemaining;
      totals.set(item.currency, current);
      return totals;
    }, new Map<string, { currency: string; totalServiceValue: number; commissionDue: number; commissionPaid: number; commissionRemaining: number }>()).values(),
  );

  // CSV Export function
  const exportToCsv = () => {
    if (data.length === 0) return;
    const headers = [
      "اسم الطبيب",
      "الفرع",
      "العملة",
      "عدد الحالات",
      "إجمالي الخدمات",
      "نسبة العمولة (%)",
      "العمولة المستحقة",
      "العمولة المدفوعة",
      "الرصيد المتبقي"
    ];

    const rows = data.map(item => [
      item.doctorName,
      item.branchId,
      item.currency,
      item.casesCount,
      item.totalServiceValue,
      item.commissionPercentage,
      item.commissionDue,
      item.commissionPaid,
      item.commissionRemaining
    ]);

    const csvContent = [
      headers.join(","),
      ...rows.map(row => row.join(","))
    ].join("\n");

    const blob = new Blob(["\ufeff" + csvContent], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `doctor-commissions-${startDate}-to-${endDate}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="p-5 space-y-6">
      {selectedDoctorId && (
        <section className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <h3 className="mb-3 text-sm font-bold" style={{ color: tokens.textPrimary }}>كشف حساب الطبيب — مدين / دائن حسب العملة</h3>
          <PartyAccountStatementPanel partyType="doctor" partyId={selectedDoctorId} />
        </section>
      )}
      {/* ─── Filter Form ─── */}
      <form onSubmit={handleApplyFilters} className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border, boxShadow: tokens.shadow2 }}>
        <div className="flex items-center gap-2 mb-3">
          <Filter className="w-4 h-4" style={{ color: tokens.textSecondary }} />
          <span className="text-xs font-bold" style={{ color: tokens.textPrimary }}>فلاتر البحث</span>
        </div>
        
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div>
            <label style={labelStyle}>الطبيب</label>
            <select
              value={selectedDoctorId}
              onChange={(e) => setSelectedDoctorId(e.target.value)}
              style={inputStyle}
              disabled={doctorsLoading}
            >
              <option value="">كل الأطباء</option>
              {doctors.map((doc) => (
                <option key={doc.id} value={doc.id}>{doc.name}</option>
              ))}
            </select>
          </div>

          <div>
            <label style={labelStyle}>من تاريخ</label>
            <input
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              style={inputStyle}
            />
          </div>

          <div>
            <label style={labelStyle}>إلى تاريخ</label>
            <input
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              style={inputStyle}
            />
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 mt-4 pt-3 border-t" style={{ borderColor: tokens.border }}>
          <button
            type="button"
            onClick={exportToCsv}
            disabled={data.length === 0}
            style={btnGhost}
            className="disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-50"
          >
            <Download className="w-3.5 h-3.5" />
            <span>تصدير CSV</span>
          </button>
          
          <button
            type="submit"
            style={btnPrimary}
            className="hover:opacity-90"
            disabled={loading}
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? "animate-spin" : ""}`} />
            <span>عرض التقرير</span>
          </button>
        </div>
      </form>

      {/* ─── Summary Cards ─── */}
      <div className="grid grid-cols-1 gap-3">
        <KpiCard
          label="عدد الحالات"
          value={formatNumber(totalCases)}
          icon={<Users className="w-3.5 h-3.5" style={{ color: tokens.textSecondary }} />}
          color={tokens.textPrimary}
        />
        {totalsByCurrency.map((total) => (
          <div key={total.currency} className="grid grid-cols-2 sm:grid-cols-4 gap-3 rounded-lg border p-3" style={{ borderColor: tokens.border }}>
            <KpiCard label={`إجمالي الخدمات — ${total.currency}`} value={formatMoney(total.totalServiceValue, total.currency)} icon={<Wallet className="w-3.5 h-3.5" />} color={tokens.brand} />
            <KpiCard label={`المستحقة — ${total.currency}`} value={formatMoney(total.commissionDue, total.currency)} icon={<TrendingUp className="w-3.5 h-3.5" />} color={tokens.warningBorder} />
            <KpiCard label={`المدفوعة — ${total.currency}`} value={formatMoney(total.commissionPaid, total.currency)} icon={<CreditCard className="w-3.5 h-3.5" />} color={tokens.successBorder} />
            <KpiCard label={`المتبقي — ${total.currency}`} value={formatMoney(total.commissionRemaining, total.currency)} icon={<Banknote className="w-3.5 h-3.5" />} color={tokens.dangerBorder} />
          </div>
        ))}
      </div>

      {/* ─── Table Section ─── */}
      <div className="rounded-lg border" style={{ backgroundColor: tokens.card, borderColor: tokens.border, boxShadow: tokens.shadow2 }}>
        <div className="p-4 border-b flex items-center justify-between" style={{ borderColor: tokens.border }}>
          <SectionHeader title="جدول عمولات الأطباء" />
        </div>

        {loading ? (
          <div className="p-6">
            <LoadingSkeleton rows={5} />
          </div>
        ) : data.length === 0 ? (
          <EmptyState icon={Banknote} message="لا توجد بيانات عمولات للمحددات المذكورة" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr style={{ backgroundColor: tokens.cardHover }}>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>اسم الطبيب</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>العملة</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>عدد الحالات</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>إجمالي الخدمات</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>نسبة العمولة</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>العمولة المستحقة</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>العمولة المدفوعة</th>
                  <th className="text-start px-4 py-3 font-semibold text-xs" style={{ color: tokens.textSecondary }}>المتبقي</th>
                </tr>
              </thead>
              <tbody>
                {data.map((row) => (
                  <tr
                    key={`${row.doctorId}-${row.branchId}-${row.currency}`}
                    style={{ borderBottom: `1px solid ${tokens.border}` }}
                    className="transition-colors hover:bg-slate-50"
                  >
                    <td className="px-4 py-3 font-medium" style={{ color: tokens.textPrimary }}>{row.doctorName}</td>
                    <td className="px-4 py-3 font-semibold" style={{ color: tokens.brand }}>{row.currency}</td>
                    <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatNumber(row.casesCount)}</td>
                    <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatMoney(row.totalServiceValue, row.currency)}</td>
                    <td className="px-4 py-3" style={{ color: tokens.textSecondary }}>{formatNumber(row.commissionPercentage)}%</td>
                    <td className="px-4 py-3 font-semibold text-purple-700">{formatMoney(row.commissionDue, row.currency)}</td>
                    <td className="px-4 py-3 text-green-700">{formatMoney(row.commissionPaid, row.currency)}</td>
                    <td className="px-4 py-3 font-bold" style={{ color: row.commissionRemaining > 0 ? tokens.dangerBorder : tokens.textSecondary }}>{formatMoney(row.commissionRemaining, row.currency)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* CORE-FIN-LAB-ADJ — corrections on commissions that were already paid out. The table
          above shows what each doctor accrued; these are the differences that appeared after
          the money moved, and they are invisible anywhere else because a paid commission line
          is deliberately never rewritten. */}
      <CommissionAdjustmentsPanel doctorId={selectedDoctorId || undefined} />
    </div>
  );
}
