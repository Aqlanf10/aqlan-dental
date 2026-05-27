"use client";

import { useState, useEffect, useCallback } from "react";
import {
  Receipt,
  TrendingDown,
  Vault,
  HandCoins,
  RefreshCw,
  CheckCircle2,
  Clock,
  BarChart3,
} from "lucide-react";
import { api } from "@/lib/api";
import type { DashboardData, ProfitLossData } from "./types";
import { KpiCard, tokens } from "./FinanceSharedUI";
import { formatYER } from "./FinanceHelpers";

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 1: Overview
   ═══════════════════════════════════════════════════════════════════════════════ */
export function OverviewTab() {
  const [data, setData] = useState<DashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [plData, setPlData] = useState<ProfitLossData | null>(null);
  const [plLoading, setPlLoading] = useState(false);

  const fetchDashboard = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { data } = await api.get<DashboardData>("/api/finance-v3/dashboard");
      setData(data);
    } catch (err: unknown) {
      if (err && typeof err === "object" && "response" in err) {
        const status = (err as { response?: { status?: number } }).response?.status;
        if (status === 401 || status === 403) {
          setError("ليس لديك صلاحية الوصول. يرجى تسجيل الدخول مجدداً أو التواصل مع المسؤول.");
        } else {
          setError("فشل في تحميل البيانات. يرجى المحاولة لاحقاً.");
        }
      } else {
        setError("فشل في الاتصال بالخادم. تحقق من اتصال الإنترنت وحاول مجدداً.");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchPL = useCallback(async () => {
    try {
      setPlLoading(true);
      const now = new Date();
      const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
      const to = now.toISOString().slice(0, 10);
      const { data } = await api.get<ProfitLossData>("/api/finance-v3/profit-loss", { params: { from, to } });
      setPlData(data);
    } catch {
      // P&L is supplementary — don't block the page
    } finally {
      setPlLoading(false);
    }
  }, []);

  useEffect(() => { fetchDashboard(); fetchPL(); }, [fetchDashboard, fetchPL]);

  return (
    <div className="p-6 space-y-6">
      {/* Header with refresh */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold mb-1" style={{ color: tokens.textPrimary }}>المركز المالي</h2>
          <p className="text-sm leading-relaxed" style={{ color: tokens.textSecondary }}>
            مرحباً بك في المركز المالي. يتم تسجيل تحصيل المرضى من شاشة التشغيل اليومي، بينما هذه الشاشة مخصصة للمراجعة والتسوية والتقارير.
          </p>
        </div>
        <button
          onClick={() => { fetchDashboard(); fetchPL(); }}
          className="w-8 h-8 rounded-md flex items-center justify-center transition-colors"
          style={{ color: tokens.brand, border: `1px solid ${tokens.border}` }}
          onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = tokens.brandLight; }}
          onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
          title="تحديث البيانات"
        >
          <RefreshCw className="w-4 h-4" />
        </button>
      </div>

      {/* Live KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {loading ? (
          Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="rounded-lg border p-4 animate-pulse" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <div className="h-3 w-20 rounded mb-2" style={{ backgroundColor: tokens.cardHover }} />
              <div className="h-6 w-32 rounded" style={{ backgroundColor: tokens.cardHover }} />
            </div>
          ))
        ) : error ? (
          <div className="col-span-full rounded-lg border p-4" style={{ backgroundColor: tokens.dangerBg, borderColor: tokens.dangerBorder }}>
            <p className="text-sm" style={{ color: tokens.dangerText }}>{error}</p>
            <button onClick={fetchDashboard} className="text-xs font-medium mt-2 underline" style={{ color: tokens.brand }}>إعادة المحاولة</button>
          </div>
        ) : data ? (
          <>
            <KpiCard label="إيراد اليوم (مستحق)" value={formatYER(data.TodayAccruedRevenue)} sublabel={`التدفقات الداخلة: ${formatYER(data.TodayInflow)}`} color={tokens.successBorder} icon={<Receipt className="w-4 h-4" />} />
            <KpiCard label="التدفقات الخارجة اليوم" value={formatYER(data.TodayOutflow)} sublabel={`شهري: ${formatYER(data.MonthOutflow)}`} color={tokens.dangerBorder} icon={<TrendingDown className="w-4 h-4" />} />
            <KpiCard label="رصيد الخزائن" value={formatYER(data.TotalTreasuryBalance)} sublabel={`${data.JournalEntryCount} قيد محاسبي`} color={tokens.brand} icon={<Vault className="w-4 h-4" />} />
            <KpiCard label="المستحقات المعلقة" value={formatYER(data.TotalOutstanding)} sublabel={`عقود: ${formatYER(data.ContractOutstanding)}`} color={tokens.warningBorder} icon={<HandCoins className="w-4 h-4" />} />
          </>
        ) : null}
      </div>

      {/* Dual-write health + Pending actions */}
      {data && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center gap-2 mb-3">
              <CheckCircle2 className="w-4 h-4" style={{ color: tokens.successBorder }} />
              <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>حالة الكتابة المزدوجة</h4>
            </div>
            <div className="space-y-2">
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>إجمالي القيود</span><span className="font-bold" style={{ color: tokens.textPrimary }}>{data.JournalEntryCount}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>قيود مرحّلة</span><span className="font-bold" style={{ color: tokens.successBorder }}>{data.PostedEntryCount}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>قيود عكسية</span><span className="font-bold" style={{ color: tokens.warningText }}>{data.ReversalEntryCount}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>نسبة التغطية</span><span className="font-bold" style={{ color: tokens.brand }}>{data.DualWriteCoverage}</span></div>
            </div>
          </div>
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center gap-2 mb-3">
              <Clock className="w-4 h-4" style={{ color: tokens.warningBorder }} />
              <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>إجراءات معلقة</h4>
            </div>
            <div className="space-y-2">
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>مصروفات بانتظار الاعتماد</span><span className="font-bold" style={{ color: data.PendingExpenses > 0 ? tokens.warningText : tokens.textPrimary }}>{data.PendingExpenses}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>تحويلات معلقة</span><span className="font-bold" style={{ color: data.PendingTransfers > 0 ? tokens.warningText : tokens.textPrimary }}>{data.PendingTransfers}</span></div>
              <div className="flex justify-between text-xs"><span style={{ color: tokens.textSecondary }}>فواتير غير مدفوعة</span><span className="font-bold" style={{ color: tokens.textPrimary }}>{formatYER(data.InvoiceOutstanding)}</span></div>
            </div>
          </div>
        </div>
      )}

      {/* P&L Summary */}
      <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
        <div className="flex items-center gap-2 mb-3">
          <BarChart3 className="w-4 h-4" style={{ color: tokens.brand }} />
          <h4 className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>ملخص الأرباح والخسائر (الشهر الحالي)</h4>
        </div>
        {plLoading ? (
          <div className="animate-pulse space-y-2">
            <div className="h-4 w-40 rounded" style={{ backgroundColor: tokens.cardHover }} />
            <div className="h-4 w-60 rounded" style={{ backgroundColor: tokens.cardHover }} />
          </div>
        ) : plData ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(plData.AccruedRevenue)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المصروفات المستحقة</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(plData.AccruedExpenses)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح المستحق</p><p className="text-sm font-bold" style={{ color: plData.AccruedNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(plData.AccruedNetProfit)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح النقدي</p><p className="text-sm font-bold" style={{ color: plData.CashNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(plData.CashNetProfit)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>التحصيلات النقدية</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(plData.CashCollections)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات النقدية</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(plData.CashRefunds)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي التكاليف</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(plData.TotalCosts)}</p></div>
            <div><p className="text-[11px]" style={{ color: tokens.textTertiary }}>هامش الربح</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{plData.ProfitMargin.toFixed(1)}%</p></div>
          </div>
        ) : (
          <p className="text-xs" style={{ color: tokens.textTertiary }}>لم يتم تحميل بيانات الأرباح والخسائر</p>
        )}
      </div>

      {/* Monthly summary */}
      {data && (
        <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
          <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>ملخص الشهر</h4>
          <div className="grid grid-cols-3 gap-4 text-center">
            <div><p className="text-xs" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p><p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.MonthAccruedRevenue)}</p></div>
            <div><p className="text-xs" style={{ color: tokens.textTertiary }}>التدفقات الخارجة</p><p className="text-lg font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.MonthOutflow)}</p></div>
            <div><p className="text-xs" style={{ color: tokens.textTertiary }}>صافي التدفق</p><p className="text-lg font-bold" style={{ color: data.MonthNet >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.MonthNet)}</p></div>
          </div>
        </div>
      )}
    </div>
  );
}
