"use client";

import { useQuery } from "@tanstack/react-query";
import {
  FlaskConical, Clock, Package, CheckCircle2, AlertTriangle,
  RotateCcw, RefreshCw, DollarSign, TrendingUp, Truck,
} from "lucide-react";
import api from "@/lib/api";
import { LAB_STATUS_LABELS as STATUS_LABELS, LAB_STATUS_COLORS as STATUS_COLORS } from "@/lib/labStatus";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { TableSkeleton } from "@/components/ui/skeleton";
import type {
  LabDashboardData, StatusDistributionItem,
  TopLabItem, OverdueOrderItem, MonthlyTrendItem, LabAccount,
} from "@/types/lab";
import { cn } from "@/lib/utils";
import { QueryErrorBanner } from "@/components/shared/QueryErrorBanner";
import { formatCurrencyAmounts } from "@/app/(dashboard)/finance-v3/components/FinanceHelpers";

// FE-08: STATUS_LABELS + STATUS_COLORS now imported from @/lib/labStatus (was re-declared locally).

const ARABIC_MONTHS = [
  "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
  "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر",
];

function KPICard({
  label, value, icon, color, sub,
}: {
  label: string; value: string | number; icon: React.ReactNode;
  color: string; sub?: string;
}) {
  return (
    <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4 flex items-start gap-3">
      <div className={cn("p-2 rounded-lg", color)}>{icon}</div>
      <div className="flex-1 min-w-0">
        <p className="text-xs text-gray-500 font-medium">{label}</p>
        <p className="text-xl font-bold text-gray-900 mt-0.5">{value}</p>
        {sub && <p className="text-xs text-gray-400 mt-0.5">{sub}</p>}
      </div>
    </div>
  );
}

function SimpleBarChart({ data, maxVal, labelFn, valueFn, colorFn }: {
  data: Array<Record<string, unknown>>;
  maxVal: number;
  labelFn: (item: Record<string, unknown>) => string;
  valueFn: (item: Record<string, unknown>) => number;
  colorFn: (item: Record<string, unknown>) => string;
}) {
  return (
    <div className="space-y-2">
      {data.map((item, i) => {
        const val = valueFn(item);
        const pct = maxVal > 0 ? (val / maxVal) * 100 : 0;
        return (
          <div key={i} className="flex items-center gap-2">
            <span className="text-xs text-gray-600 w-24 truncate text-start">{labelFn(item)}</span>
            <div className="flex-1 bg-gray-100 rounded-full h-5 relative overflow-hidden">
              <div
                className={cn("h-full rounded-full transition-all duration-500", colorFn(item))}
                style={{ width: `${Math.max(pct, 2)}%` }}
              />
              <span className="absolute inset-0 flex items-center justify-center text-xs font-medium text-gray-700">
                {val}
              </span>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function LabOverviewPanel() {
  const { data: dashboardData, isLoading, isError, refetch } = useQuery({
    queryKey: ["lab-dashboard"],
    queryFn: async () => {
      const res = await api.get<{ data: LabDashboardData }>("/api/reports/lab-dashboard");
      return res.data.data;
    },
    refetchInterval: 60_000,
  });

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <TableSkeleton rows={4} cols={4} />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="space-y-6 p-6">
        <QueryErrorBanner
          text="تعذر تحميل ملخص المختبر — الأرقام غير متاحة وليست أصفارًا حقيقية"
          onRetry={() => refetch()}
        />
      </div>
    );
  }

  const kpis = dashboardData?.kpis;
  const statusDist = dashboardData?.statusDistribution ?? [];
  const topLabs = dashboardData?.topLabs ?? [];
  const recentOverdue = dashboardData?.recentOverdue ?? [];
  const monthlyTrend = dashboardData?.monthlyTrend ?? [];
  const labAccounts = dashboardData?.labAccounts ?? [];

  const maxStatusCount = Math.max(...statusDist.map((s: StatusDistributionItem) => s.count), 1);
  const maxLabOrders = Math.max(...topLabs.map((l: TopLabItem) => l.orderCount), 1);
  const maxTrendOrders = Math.max(...monthlyTrend.map((m: MonthlyTrendItem) => m.totalOrders), 1);

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* KPI Cards */}
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-3">
          <KPICard
            label="إجمالي الطلبات"
            value={kpis?.totalOrders ?? 0}
            icon={<FlaskConical className="w-5 h-5 text-cyan-700" />}
            color="bg-cyan-50"
          />
          <KPICard
            label="طلبات معلّقة"
            value={kpis?.pendingOrders ?? 0}
            icon={<Clock className="w-5 h-5 text-amber-700" />}
            color="bg-amber-50"
            sub="قيد الإرسال أو الصنع"
          />
          <KPICard
            label="جاهز للاستلام"
            value={kpis?.readyOrders ?? 0}
            icon={<Package className="w-5 h-5 text-green-700" />}
            color="bg-green-50"
          />
          <KPICard
            label="متأخرة"
            value={kpis?.overdueOrders ?? 0}
            icon={<AlertTriangle className="w-5 h-5 text-red-700" />}
            color="bg-red-50"
            sub="تجاوزت تاريخ الاستلام"
          />
          <KPICard
            label="تم التسليم (30 يوم)"
            value={kpis?.deliveredLast30Days ?? 0}
            icon={<Truck className="w-5 h-5 text-emerald-700" />}
            color="bg-emerald-50"
          />
          <KPICard
            label="تم الاستلام"
            value={kpis?.receivedOrders ?? 0}
            icon={<CheckCircle2 className="w-5 h-5 text-indigo-700" />}
            color="bg-indigo-50"
            sub="بانتظار التسليم للمريض"
          />
          <KPICard
            label="مرتجعة"
            value={kpis?.returnedOrders ?? 0}
            icon={<RotateCcw className="w-5 h-5 text-orange-700" />}
            color="bg-orange-50"
          />
          <KPICard
            label="إعادة صناعة"
            value={kpis?.remakeOrders ?? 0}
            icon={<RefreshCw className="w-5 h-5 text-purple-700" />}
            color="bg-purple-50"
          />
          <KPICard
            label="إجمالي التكاليف الملتزم بها"
            value={formatCurrencyAmounts(kpis?.totalLabCostsByCurrency)}
            icon={<DollarSign className="w-5 h-5 text-blue-700" />}
            color="bg-blue-50"
          />
          <KPICard
            label="ديون المعامل"
            value={formatCurrencyAmounts(kpis?.totalDebtByCurrency)}
            icon={<TrendingUp className="w-5 h-5 text-red-700" />}
            color="bg-red-50"
            sub="غير مدفوعة"
          />
        </div>

        {/* ── Lab accounts, per currency (CORE-LAB-016) ────────────────────────
            The clinic deals with labs that invoice in Yemeni riyals, Saudi riyals and
            dollars. A single "total owed" would be meaningless, so each lab's account is
            shown in the currencies it actually bills in — never converted, never summed. */}
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm">
          <div className="p-4 border-b border-gray-100">
            <h2 className="text-sm font-semibold text-gray-900">حسابات المعامل حسب العملة</h2>
            <p className="text-xs text-gray-500 mt-0.5">
              كل معمل بعملته التي يحاسب بها — لا تُجمع العملات ولا تُحوَّل
            </p>
          </div>
          {labAccounts.length === 0 ? (
            <p className="text-sm text-gray-400 text-center py-8">لا توجد حسابات معامل بعد</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-gray-50">
                    {["المعمل", "فواتير مفتوحة", "إجمالي المفوتر", "المدفوع", "الرصيد المستحق"].map((h) => (
                      <th key={h} className="text-start px-4 py-2.5 font-semibold text-xs text-gray-500 whitespace-nowrap">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {labAccounts.map((a: LabAccount) => (
                    <tr key={a.supplierId} className="border-t border-gray-100 hover:bg-gray-50">
                      <td className="px-4 py-3 font-medium text-gray-900 whitespace-nowrap">{a.labName}</td>
                      <td className="px-4 py-3 text-gray-600">{a.openBills}</td>
                      <td className="px-4 py-3 text-gray-700 whitespace-nowrap">
                        {formatCurrencyAmounts(a.billedByCurrency)}
                      </td>
                      <td className="px-4 py-3 text-green-700 whitespace-nowrap">
                        {formatCurrencyAmounts(a.paidByCurrency)}
                      </td>
                      <td className="px-4 py-3 font-bold text-red-600 whitespace-nowrap">
                        {formatCurrencyAmounts(a.balanceByCurrency)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Status Distribution */}
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-5">
            <h2 className="text-sm font-semibold text-gray-900 mb-4">توزيع الحالات</h2>
            <SimpleBarChart
              data={statusDist as unknown as Array<Record<string, unknown>>}
              maxVal={maxStatusCount}
              labelFn={(item) => STATUS_LABELS[(item as unknown as StatusDistributionItem).status] ?? (item as unknown as StatusDistributionItem).status}
              valueFn={(item) => (item as unknown as StatusDistributionItem).count}
              colorFn={(item) => {
                const status = (item as unknown as StatusDistributionItem).status;
                const colorMap: Record<string, string> = {
                  draft: "bg-gray-400",
                  sent: "bg-blue-500",
                  manufacturing: "bg-amber-500",
                  tryIn: "bg-teal-500",
                  ready: "bg-green-500",
                  received: "bg-indigo-500",
                  delivered: "bg-emerald-500",
                  returned: "bg-orange-500",
                  remake: "bg-purple-500",
                  cancelled: "bg-red-500",
                };
                return colorMap[status] ?? "bg-gray-400";
              }}
            />
          </div>

          {/* Top Labs */}
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-5">
            <h2 className="text-sm font-semibold text-gray-900 mb-4">أكثر المعامل طلبات</h2>
            {topLabs.length === 0 ? (
              <p className="text-sm text-gray-400 text-center py-6">لا توجد بيانات</p>
            ) : (
              <SimpleBarChart
                data={topLabs as unknown as Array<Record<string, unknown>>}
                maxVal={maxLabOrders}
                labelFn={(item) => (item as unknown as TopLabItem).labName}
                valueFn={(item) => (item as unknown as TopLabItem).orderCount}
                colorFn={() => "bg-cyan-500"}
              />
            )}
          </div>
        </div>

        {/* Monthly Trend */}
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-5">
          <h2 className="text-sm font-semibold text-gray-900 mb-4">الاتجاه الشهري (آخر 6 أشهر)</h2>
          {monthlyTrend.length === 0 ? (
            <p className="text-sm text-gray-400 text-center py-6">لا توجد بيانات</p>
          ) : (
            <div className="flex items-end gap-2 h-40">
              {monthlyTrend.map((m: MonthlyTrendItem, i: number) => {
                const pct = maxTrendOrders > 0 ? (m.totalOrders / maxTrendOrders) * 100 : 0;
                const deliveredPct = maxTrendOrders > 0 ? (m.deliveredOrders / maxTrendOrders) * 100 : 0;
                return (
                  <div key={i} className="flex-1 flex flex-col items-center gap-1">
                    <div className="w-full relative" style={{ height: "120px" }}>
                      <div
                        className="absolute bottom-0 w-full bg-cyan-200 rounded-t transition-all duration-500"
                        style={{ height: `${pct}%` }}
                      />
                      <div
                        className="absolute bottom-0 w-full bg-cyan-600 rounded-t transition-all duration-500"
                        style={{ height: `${deliveredPct}%` }}
                      />
                    </div>
                    <span className="text-xs text-gray-500 whitespace-nowrap">
                      {ARABIC_MONTHS[m.month - 1] ?? m.month}
                    </span>
                    <span className="text-xs font-medium text-gray-700">{m.totalOrders}</span>
                  </div>
                );
              })}
            </div>
          )}
          <div className="flex items-center gap-4 mt-3 text-xs text-gray-500">
            <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-cyan-600 inline-block" /> مسلّمة</span>
            <span className="flex items-center gap-1"><span className="w-3 h-3 rounded bg-cyan-200 inline-block" /> إجمالي</span>
          </div>
        </div>

        {/* Overdue Orders Alert */}
        {recentOverdue.length > 0 && (
          <div className="bg-red-50 border border-red-200 rounded-xl p-5">
            <div className="flex items-center gap-2 mb-3">
              <AlertTriangle className="w-5 h-5 text-red-600" />
              <h2 className="text-sm font-semibold text-red-800">
                طلبات متأخرة ({recentOverdue.length})
              </h2>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-red-200">
                    {["رقم الطلب", "المريض", "المختبر", "نوع الجهاز", "تاريخ الاستلام", "أيام التأخير", "الحالة"].map((h) => (
                      <th key={h} className="text-start px-3 py-2 font-medium text-red-700 text-xs whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-red-100">
                  {recentOverdue.map((o: OverdueOrderItem) => (
                    <tr key={o.id} className="hover:bg-red-100/50">
                      <td className="px-3 py-2 font-mono text-xs text-gray-700">{o.orderNumber}</td>
                      <td className="px-3 py-2 text-gray-700">{o.patientName}</td>
                      <td className="px-3 py-2 text-gray-500">{o.labEntityName ?? o.labName ?? "—"}</td>
                      <td className="px-3 py-2 text-gray-500">{o.applianceType}</td>
                      <td className="px-3 py-2 text-gray-500 text-xs">{o.expectedDate ?? "—"}</td>
                      <td className="px-3 py-2">
                        <span className="text-red-700 font-bold">{o.daysOverdue}</span>
                      </td>
                      <td className="px-3 py-2">
                        <span className={cn("inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium", STATUS_COLORS[o.status] ?? "bg-gray-100 text-gray-500")}>
                          {STATUS_LABELS[o.status] ?? o.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </ErrorBoundary>
  );
}
