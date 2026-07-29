
import { useState, useEffect, useCallback } from "react";
import {
  BarChart3,
  Receipt,
  Landmark,
  Eye,
  RefreshCw,
  TrendingDown,
  Vault,
  ClipboardCheck,
  AlertTriangle,
  FileText,
  HandCoins,
  Truck,
} from "lucide-react";
import { api } from "@/lib/api";
import { localDateString } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import type { ProfitLossData, DailyCashSummary, AccountBalancesData } from "./types";
import { PAYMENT_METHODS } from "./types";
import { KpiCard, LoadingSkeleton, EmptyState, tokens, inputStyle, labelStyle, btnPrimary } from "./FinanceSharedUI";
import { formatCurrencyAmounts, formatMoney, formatYER, formatNumber } from "./FinanceHelpers";

type AccountTotalKey = "totalAssets" | "totalRevenue" | "totalExpenses" | "totalReceivables" | "totalPayables";

function formatAccountTotals(data: AccountBalancesData, key: AccountTotalKey): string {
  const legacyYerTotals = {
    currency: "YER",
    totalAssets: data.totalAssets,
    totalRevenue: data.totalRevenue,
    totalExpenses: data.totalExpenses,
    totalReceivables: data.totalReceivables,
    totalPayables: data.totalPayables,
  };
  return formatCurrencyAmounts(
    (data.totalsByCurrency ?? [legacyYerTotals]).map((total) => ({
      currency: total.currency,
      amount: total[key],
    })),
  );
}

/* ── P&L Sub-tab ── */
function PLSubTab() {
  const [data, setData] = useState<ProfitLossData | null>(null);
  const [loading, setLoading] = useState(false);
  const [from, setFrom] = useState(() => { const d = new Date(); return localDateString(new Date(d.getFullYear(), d.getMonth(), 1)); });
  const [to, setTo] = useState(() => localDateString());

  const fetchPL = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await api.get<ProfitLossData>("/api/finance-v3/profit-loss", { params: { from, to } });
      setData(data);
    } catch { toast.error("فشل في تحميل تقرير الأرباح والخسائر"); } finally { setLoading(false); }
  }, [from, to]);

  useEffect(() => { fetchPL(); }, [fetchPL]);

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <div><label style={labelStyle}>من</label><input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ ...inputStyle, width: 160 }} /></div>
        <div><label style={labelStyle}>إلى</label><input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ ...inputStyle, width: 160 }} /></div>
        <button onClick={fetchPL} style={{ ...btnPrimary, marginTop: 18 }}><Eye className="w-4 h-4" /> عرض</button>
      </div>

      {loading ? <LoadingSkeleton rows={4} /> : data ? (
        <div className="space-y-4">
          {/* Accrued section */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>الأساس المستحق</h4>
            <div className="grid grid-cols-3 gap-4">
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>الإيرادات المستحقة</p><p className="text-lg font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.accruedRevenue)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المصروفات المستحقة</p><p className="text-lg font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.accruedExpenses)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح المستحق</p><p className="text-lg font-bold" style={{ color: data.accruedNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.accruedNetProfit)}</p></div>
            </div>
          </div>

          {/* Cash section */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>الأساس النقدي</h4>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>التحصيلات</p><p className="text-sm font-bold" style={{ color: tokens.successBorder }}>{formatYER(data.cashCollections)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>المرتجعات</p><p className="text-sm font-bold" style={{ color: tokens.warningText }}>{formatYER(data.cashRefunds)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي التحصيل</p><p className="text-sm font-bold" style={{ color: tokens.brand }}>{formatYER(data.netCashCollections)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>صافي الربح النقدي</p><p className="text-sm font-bold" style={{ color: data.cashNetProfit >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatYER(data.cashNetProfit)}</p></div>
            </div>
          </div>

          {/* Cost breakdown */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>تفصيل التكاليف</h4>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مصروفات تشغيلية</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.operatingExpenses)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>رواتب</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.salaryPayments)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>عمولات أطباء</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.doctorCommissions)}</p></div>
              <div className="text-center"><p className="text-[11px]" style={{ color: tokens.textTertiary }}>مدفوعات موردين</p><p className="text-sm font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.supplierPayments)}</p></div>
            </div>
            <div className="mt-3 pt-3 border-t text-center" style={{ borderColor: tokens.border }}>
              <p className="text-[11px]" style={{ color: tokens.textTertiary }}>إجمالي التكاليف</p>
              <p className="text-base font-bold" style={{ color: tokens.dangerBorder }}>{formatYER(data.totalCosts)}</p>
            </div>
          </div>

          {/* Margin */}
          <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold" style={{ color: tokens.textPrimary }}>هامش الربح</span>
              <span className="text-xl font-bold" style={{ color: (data.profitMargin ?? 0) >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{(data.profitMargin ?? 0).toFixed(1)}%</span>
            </div>
          </div>
        </div>
      ) : (
        <EmptyState icon={BarChart3} message="اختر فترة واضغط عرض لتحميل التقرير" />
      )}
    </div>
  );
}

/* ── Daily Cash Sub-tab ── */
function DailyCashSubTab() {
  const [data, setData] = useState<DailyCashSummary | null>(null);
  const [loading, setLoading] = useState(false);
  const [date, setDate] = useState(() => localDateString());

  const fetchDaily = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await api.get<DailyCashSummary>("/api/finance-v3/daily-cash-summary", { params: { date } });
      setData(data);
    } catch { toast.error("فشل في تحميل ملخص الكاش اليومي"); } finally { setLoading(false); }
  }, [date]);

  useEffect(() => { fetchDaily(); }, [fetchDaily]);

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <div><label style={labelStyle}>التاريخ</label><input type="date" value={date} onChange={(e) => setDate(e.target.value)} style={{ ...inputStyle, width: 180 }} /></div>
        <button onClick={fetchDaily} style={{ ...btnPrimary, marginTop: 18 }}><Eye className="w-4 h-4" /> عرض</button>
      </div>

      {loading ? <LoadingSkeleton rows={4} /> : data ? (
        <div className="space-y-4">
          {/* Summary cards */}
          <div className="grid grid-cols-3 gap-4">
            <KpiCard label="التدفقات الداخلة" value={formatYER(data.totalInflow)} color={tokens.successBorder} icon={<Receipt className="w-4 h-4" />} />
            <KpiCard label="التدفقات الخارجة" value={formatYER(data.totalOutflow)} color={tokens.dangerBorder} icon={<TrendingDown className="w-4 h-4" />} />
            <KpiCard label="صافي الكاش" value={formatYER(data.netCash)} color={data.netCash >= 0 ? tokens.successBorder : tokens.dangerBorder} icon={<Vault className="w-4 h-4" />} />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <KpiCard label="عدد المعاملات" value={formatNumber(data.transactionCount)} color={tokens.brand} icon={<ClipboardCheck className="w-4 h-4" />} />
            <KpiCard label="معاملات عكسية" value={formatNumber(data.reversalCount)} color={tokens.warningBorder} icon={<AlertTriangle className="w-4 h-4" />} />
            <KpiCard label="قيود محاسبية" value={formatNumber(data.journalEntryCount)} color={tokens.brand} icon={<FileText className="w-4 h-4" />} />
          </div>

          {/* By category */}
          {data.byCategory && data.byCategory.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>حسب الفئة</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-3 py-2">النوع</th>
                    <th className="text-right px-3 py-2">الفئة</th>
                    <th className="text-right px-3 py-2">عكسي</th>
                    <th className="text-right px-3 py-2">العدد</th>
                    <th className="text-right px-3 py-2">الإجمالي</th>
                  </tr></thead>
                  <tbody>
                    {data.byCategory.map((cat, idx) => (
                      <tr key={idx} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                        <td className="px-3 py-2">{cat.type}</td>
                        <td className="px-3 py-2">{cat.category}</td>
                        <td className="px-3 py-2">{cat.isReversal ? "نعم" : "—"}</td>
                        <td className="px-3 py-2">{cat.count}</td>
                        <td className="px-3 py-2 font-bold" style={{ color: cat.isReversal ? tokens.dangerBorder : tokens.successBorder }}>{formatYER(cat.total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* By payment method */}
          {data.byPaymentMethod && data.byPaymentMethod.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>حسب طريقة الدفع</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-3 py-2">طريقة الدفع</th>
                    <th className="text-right px-3 py-2">العدد</th>
                    <th className="text-right px-3 py-2">الإجمالي</th>
                  </tr></thead>
                  <tbody>
                    {data.byPaymentMethod.map((pm, idx) => (
                      <tr key={idx} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                        <td className="px-3 py-2">{PAYMENT_METHODS.find((m) => m.value === pm.paymentMethod)?.label ?? pm.paymentMethod}</td>
                        <td className="px-3 py-2">{pm.count}</td>
                        <td className="px-3 py-2 font-bold">{formatYER(pm.total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      ) : (
        <EmptyState icon={Receipt} message="اختر تاريخاً واضغط عرض" />
      )}
    </div>
  );
}

/* ── Account Balances Sub-tab ── */
function AccountBalancesSubTab() {
  const [data, setData] = useState<AccountBalancesData | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchBalances = useCallback(async () => {
    try { setLoading(true); const { data } = await api.get<AccountBalancesData>("/api/finance-v3/account-balances"); setData(data); } catch { toast.error("فشل في تحميل أرصدة الحسابات"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { fetchBalances(); }, [fetchBalances]);

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button onClick={fetchBalances} style={btnPrimary}><RefreshCw className="w-4 h-4" /> تحديث</button>
      </div>

      {loading ? <LoadingSkeleton rows={4} /> : data ? (
        <div className="space-y-4">
          {/* Summary cards */}
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
            <KpiCard label="إجمالي الأصول" value={formatAccountTotals(data, "totalAssets")} color={tokens.brand} icon={<Vault className="w-4 h-4" />} />
            <KpiCard label="إجمالي الإيرادات" value={formatAccountTotals(data, "totalRevenue")} color={tokens.successBorder} icon={<Receipt className="w-4 h-4" />} />
            <KpiCard label="إجمالي المصروفات" value={formatAccountTotals(data, "totalExpenses")} color={tokens.dangerBorder} icon={<TrendingDown className="w-4 h-4" />} />
            <KpiCard label="إجمالي المستحقات" value={formatAccountTotals(data, "totalReceivables")} color={tokens.warningBorder} icon={<HandCoins className="w-4 h-4" />} />
            <KpiCard label="إجمالي الالتزامات" value={formatAccountTotals(data, "totalPayables")} color={tokens.dangerBorder} icon={<Truck className="w-4 h-4" />} />
          </div>

          {/* Account balances table */}
          {data.accountBalances && data.accountBalances.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>أرصدة الحسابات</h4>
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead><tr style={{ backgroundColor: tokens.cardHover }}>
                    <th className="text-right px-3 py-2">نوع الحساب</th>
                    <th className="text-right px-3 py-2">العملة</th>
                    <th className="text-right px-3 py-2">مدين</th>
                    <th className="text-right px-3 py-2">دائن</th>
                    <th className="text-right px-3 py-2">صافي الرصيد</th>
                    <th className="text-right px-3 py-2">عدد القيود</th>
                  </tr></thead>
                  <tbody>
                    {data.accountBalances.map((ab, idx) => (
                      <tr key={idx} style={{ borderBottom: `1px solid ${tokens.border}` }}>
                        <td className="px-3 py-2 font-medium">{ab.accountType}</td>
                        <td className="px-3 py-2 font-semibold" dir="ltr">{ab.currency}</td>
                        <td className="px-3 py-2">{formatMoney(ab.totalDebit, ab.currency)}</td>
                        <td className="px-3 py-2">{formatMoney(ab.totalCredit, ab.currency)}</td>
                        <td className="px-3 py-2 font-bold" style={{ color: ab.netBalance >= 0 ? tokens.successBorder : tokens.dangerBorder }}>{formatMoney(ab.netBalance, ab.currency)}</td>
                        <td className="px-3 py-2">{ab.entryCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Treasuries */}
          {data.treasuries && data.treasuries.length > 0 && (
            <div className="rounded-lg border p-4" style={{ backgroundColor: tokens.card, borderColor: tokens.border }}>
              <h4 className="text-sm font-semibold mb-3" style={{ color: tokens.textPrimary }}>الخزائن</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                {data.treasuries.map((t) => (
                  <div key={t.id} className="rounded-md border p-3" style={{ borderColor: tokens.border }}>
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-sm font-medium" style={{ color: tokens.textPrimary }}>{t.name}</span>
                      <span className="text-[11px] px-2 py-0.5 rounded-full" style={{ backgroundColor: tokens.brandLight, color: tokens.brand }}>{t.type}</span>
                    </div>
                    <p className="text-base font-bold" style={{ color: tokens.successBorder }}>{formatMoney(t.balance, t.currency)}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      ) : (
        <EmptyState icon={Landmark} message="اضغط تحديث لعرض أرصدة الحسابات" />
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════════
   Tab 10: Audit (with sub-tabs)
   ═══════════════════════════════════════════════════════════════════════════════ */
export function AuditTab() {
  const [subTab, setSubTab] = useState<"pl" | "daily" | "balances">("pl");

  const subTabs = [
    { key: "pl" as const, label: "الأرباح والخسائر", icon: BarChart3 },
    { key: "daily" as const, label: "ملخص الكاش اليومي", icon: Receipt },
    { key: "balances" as const, label: "أرصدة الحسابات", icon: Landmark },
  ];

  return (
    <div className="p-6 space-y-4">
      {/* Sub-tab bar */}
      <div className="flex items-center gap-1 border-b" style={{ borderColor: tokens.border }}>
        {subTabs.map((st) => {
          const Icon = st.icon;
          const isActive = subTab === st.key;
          return (
            <button key={st.key} onClick={() => setSubTab(st.key)} className="flex items-center gap-1.5 px-3 py-2 text-xs font-medium transition-colors relative" style={{ color: isActive ? tokens.brand : tokens.textSecondary, borderBottom: isActive ? `2px solid ${tokens.brand}` : "2px solid transparent" }}>
              <Icon className="w-3.5 h-3.5" /><span>{st.label}</span>
            </button>
          );
        })}
      </div>

      {subTab === "pl" && <PLSubTab />}
      {subTab === "daily" && <DailyCashSubTab />}
      {subTab === "balances" && <AccountBalancesSubTab />}
    </div>
  );
}
