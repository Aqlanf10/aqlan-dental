"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { FlaskConical, DollarSign, TrendingUp, AlertTriangle, Clock } from "lucide-react";
import api from "@/lib/api";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { TableSkeleton } from "@/components/ui/skeleton";
import type {
  LabCostReportItem, LabPerformanceItem, LabPerformanceSummary,
  LabPayable, LabDebtSummary, LabCurrencyAmount, LabPerformanceThresholds,
  SupplierAgingReport, SupplierAgingRow, SupplierAgingBuckets,
} from "@/types/lab";
import { QueryErrorBanner } from "@/components/shared/QueryErrorBanner";
import { formatCurrencyAmounts, formatMoney } from "@/app/(dashboard)/finance-v3/components/FinanceHelpers";
import { cn } from "@/lib/utils";

type TabType = "costs" | "performance" | "debts" | "aging";

export function LabReportsPanel() {
  const [tab, setTab] = useState<TabType>("costs");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");

  const { data: costsData, isLoading: costsLoading, isError: costsError, refetch: refetchCosts } = useQuery({
    queryKey: ["lab-costs", dateFrom, dateTo],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (dateFrom) params.set("from", dateFrom);
      if (dateTo) params.set("to", dateTo);
      const res = await api.get<{ data: LabCostReportItem[]; totalsByCurrency: LabCurrencyAmount[] }>(`/api/reports/lab-costs?${params}`);
      return res.data;
    },
    enabled: tab === "costs",
  });

  // CORE-LAB-010: the grand total comes from the server, which grouped by currency at the
  // source. Re-deriving it here by adding up the rows is exactly what produced a number that
  // was not money in any currency.
  const costsRows = costsData?.data ?? [];
  const costsTotals = costsData?.totalsByCurrency ?? [];

  const { data: perfData, isLoading: perfLoading, isError: perfError, refetch: refetchPerf } = useQuery({
    queryKey: ["lab-performance", dateFrom, dateTo],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (dateFrom) params.set("from", dateFrom);
      if (dateTo) params.set("to", dateTo);
      const res = await api.get<{ data: LabPerformanceItem[]; summary: LabPerformanceSummary; thresholds: LabPerformanceThresholds }>(`/api/reports/lab-performance?${params}`);
      return { data: res.data.data, summary: res.data.summary, thresholds: res.data.thresholds };
    },
    enabled: tab === "performance",
  });

  // CORE-LAB-015: the thresholds come from Settings via the report itself, so the summary
  // card and the row badge below can no longer disagree about the same lab. The fallback
  // reproduces the literals these used to be, which is what the server defaults to anyway.
  const sla: LabPerformanceThresholds = perfData?.thresholds ?? {
    remakeRateAlarm: 10, remakeRateWarn: 5,
    overdueRateAlarm: 15, overdueRateWarn: 5,
    turnaroundDaysTarget: 7,
    onTimeRateGood: 90, onTimeRateWarn: 70,
  };


  const { data: debtsData, isLoading: debtsLoading, isError: debtsError, refetch: refetchDebts } = useQuery({
    queryKey: ["lab-debts"],
    queryFn: async () => {
      const res = await api.get<{ data: LabPayable[]; summary: LabDebtSummary }>("/api/reports/lab-debts");
      return { data: res.data.data, summary: res.data.summary };
    },
    enabled: tab === "debts",
  });

  // CORE-LAB-020: "ديون المعامل" answers how much is owed. This answers for how long, which is
  // the figure that decides who gets paid first when the work is piling up.
  const { data: agingData, isLoading: agingLoading, isError: agingError, refetch: refetchAging } = useQuery({
    queryKey: ["lab-aging"],
    queryFn: async () => {
      const res = await api.get<SupplierAgingReport>("/api/reports/lab-aging");
      return res.data;
    },
    enabled: tab === "aging",
  });

  const tabs: Array<{ key: TabType; label: string; icon: React.ReactNode }> = [
    { key: "costs", label: "تكاليف المعامل", icon: <DollarSign className="w-4 h-4" /> },
    { key: "performance", label: "أداء المعامل", icon: <TrendingUp className="w-4 h-4" /> },
    { key: "debts", label: "ديون المعامل", icon: <AlertTriangle className="w-4 h-4" /> },
    { key: "aging", label: "أعمار المستحقات", icon: <Clock className="w-4 h-4" /> },
  ];

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Tabs */}
        <div className="flex gap-1 bg-gray-100 p-1 rounded-lg w-fit max-w-full overflow-x-auto">
          {tabs.map((t) => (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={cn(
                "flex items-center gap-1.5 px-4 py-2 rounded-md text-sm font-medium transition-colors",
                tab === t.key
                  ? "bg-white text-gray-900 shadow-sm"
                  : "text-gray-500 hover:text-gray-700"
              )}
            >
              {t.icon}
              {t.label}
            </button>
          ))}
        </div>

        {/* Date filters */}
        <div className="flex items-center gap-3 flex-wrap">
          <span className="text-sm text-gray-500">الفترة:</span>
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
            className="border border-gray-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
          <span className="text-sm text-gray-400">إلى</span>
          <input
            type="date"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
            className="border border-gray-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
          {(dateFrom || dateTo) && (
            <button
              onClick={() => { setDateFrom(""); setDateTo(""); }}
              className="text-xs text-gray-500 hover:text-gray-700"
            >
              مسح الفلتر
            </button>
          )}
        </div>

        {/* Tab Content */}
        {tab === "costs" && (
          <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
            {costsLoading ? (
              <div className="p-6"><TableSkeleton rows={4} cols={6} /></div>
            ) : costsError ? (
              <div className="p-6">
                <QueryErrorBanner text="تعذر تحميل بيانات التكاليف — تحقق من الاتصال وحاول مجددًا" onRetry={() => refetchCosts()} />
              </div>
            ) : costsRows.length === 0 ? (
              <div className="flex flex-col items-center py-12 text-gray-400">
                <FlaskConical className="w-10 h-10 mb-3" />
                <p className="font-medium">لا توجد بيانات تكاليف</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-100">
                    <tr>
                      {["المختبر", "عدد الطلبات", "التكلفة الإجمالية", "معلّقة", "مرتجعة", "إعادة صناعة"].map((h) => (
                        <th key={h} className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {costsRows.map((item: LabCostReportItem, i: number) => (
                      <tr key={i} className="hover:bg-gray-50">
                        <td className="px-4 py-3 font-medium text-gray-900">{item.labName}</td>
                        <td className="px-4 py-3 text-gray-700">{item.totalOrders}</td>
                        <td className="px-4 py-3 text-gray-700 font-medium whitespace-nowrap">{formatCurrencyAmounts(item.totalCostByCurrency)}</td>
                        <td className="px-4 py-3">
                          <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium", item.pendingOrders > 0 ? "bg-amber-100 text-amber-700" : "bg-gray-100 text-gray-500")}>
                            {item.pendingOrders}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium", item.returnedOrders > 0 ? "bg-orange-100 text-orange-700" : "bg-gray-100 text-gray-500")}>
                            {item.returnedOrders}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium", item.remakeOrders > 0 ? "bg-purple-100 text-purple-700" : "bg-gray-100 text-gray-500")}>
                            {item.remakeOrders}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot className="bg-gray-50 border-t border-gray-200">
                    <tr>
                      <td className="px-4 py-3 font-bold text-gray-900">الإجمالي</td>
                      <td className="px-4 py-3 font-bold text-gray-900">{costsRows.reduce((s: number, i: LabCostReportItem) => s + i.totalOrders, 0)}</td>
                      <td className="px-4 py-3 font-bold text-gray-900 whitespace-nowrap">{formatCurrencyAmounts(costsTotals)}</td>
                      <td className="px-4 py-3 font-bold">{costsRows.reduce((s: number, i: LabCostReportItem) => s + i.pendingOrders, 0)}</td>
                      <td className="px-4 py-3 font-bold">{costsRows.reduce((s: number, i: LabCostReportItem) => s + i.returnedOrders, 0)}</td>
                      <td className="px-4 py-3 font-bold">{costsRows.reduce((s: number, i: LabCostReportItem) => s + i.remakeOrders, 0)}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </div>
        )}

        {tab === "performance" && (
          <div className="space-y-4">
            {/* Summary KPIs */}
            {perfData?.summary && (
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">متوسط أيام التنفيذ</p>
                  <p className="text-xl font-bold text-gray-900 mt-1">{perfData.summary.overallAvgExecutionDays} <span className="text-sm font-normal text-gray-400">يوم</span></p>
                </div>
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">نسبة الإعادة</p>
                  <p className={cn("text-xl font-bold mt-1", perfData.summary.overallRemakeRate > sla.remakeRateAlarm ? "text-red-600" : "text-gray-900")}>
                    {perfData.summary.overallRemakeRate}%
                  </p>
                </div>
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">نسبة التأخير</p>
                  <p className={cn("text-xl font-bold mt-1", perfData.summary.overallOverdueRate > sla.overdueRateAlarm ? "text-red-600" : "text-gray-900")}>
                    {perfData.summary.overallOverdueRate}%
                  </p>
                </div>
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">إجمالي التكاليف</p>
                  <p className="text-lg font-bold text-gray-900 mt-1">{formatCurrencyAmounts(perfData.summary.totalCostByCurrency)}</p>
                </div>
              </div>
            )}

            {/* Performance Table */}
            <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
              {perfLoading ? (
                <div className="p-6"><TableSkeleton rows={4} cols={8} /></div>
              ) : perfError ? (
                <div className="p-6">
                  <QueryErrorBanner text="تعذر تحميل بيانات الأداء — تحقق من الاتصال وحاول مجددًا" onRetry={() => refetchPerf()} />
                </div>
              ) : (perfData?.data ?? []).length === 0 ? (
                <div className="flex flex-col items-center py-12 text-gray-400">
                  <TrendingUp className="w-10 h-10 mb-3" />
                  <p className="font-medium">لا توجد بيانات أداء</p>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-gray-50 border-b border-gray-100">
                      <tr>
                        {["المختبر", "الطلبات", "مسلّمة", "متوسط أيام", "نسبة إعادة", "نسبة تأخير", "نسبة التزام", "التكلفة"].map((h) => (
                          <th key={h} className="text-start px-3 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50">
                      {(perfData?.data ?? []).map((item: LabPerformanceItem, i: number) => (
                        <tr key={i} className="hover:bg-gray-50">
                          <td className="px-3 py-3 font-medium text-gray-900">{item.labName}</td>
                          <td className="px-3 py-3 text-gray-700">{item.totalOrders}</td>
                          <td className="px-3 py-3 text-gray-700">{item.deliveredOrders}</td>
                          <td className="px-3 py-3 text-gray-700">
                            <span className={cn("font-medium", item.avgExecutionDays > sla.turnaroundDaysTarget ? "text-red-600" : "text-green-600")}>
                              {item.avgExecutionDays}
                            </span>
                          </td>
                          <td className="px-3 py-3">
                            <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium",
                              item.remakeRate > sla.remakeRateAlarm ? "bg-red-100 text-red-700" :
                              item.remakeRate > sla.remakeRateWarn ? "bg-amber-100 text-amber-700" :
                              "bg-green-100 text-green-700")}>
                              {item.remakeRate}%
                            </span>
                          </td>
                          <td className="px-3 py-3">
                            <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium",
                              item.overdueRate > sla.overdueRateAlarm ? "bg-red-100 text-red-700" :
                              item.overdueRate > sla.overdueRateWarn ? "bg-amber-100 text-amber-700" :
                              "bg-green-100 text-green-700")}>
                              {item.overdueRate}%
                            </span>
                          </td>
                          <td className="px-3 py-3">
                            <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium",
                              item.onTimeRate >= sla.onTimeRateGood ? "bg-green-100 text-green-700" :
                              item.onTimeRate >= sla.onTimeRateWarn ? "bg-amber-100 text-amber-700" :
                              "bg-red-100 text-red-700")}>
                              {item.onTimeRate}%
                            </span>
                          </td>
                          <td className="px-3 py-3 text-gray-700 whitespace-nowrap">{formatCurrencyAmounts(item.totalCostByCurrency)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        )}

        {tab === "debts" && (
          <div className="space-y-4">
            {/* Summary */}
            {debtsData?.summary && (
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">إجمالي الديون</p>
                  <p className="text-lg font-bold text-red-600 mt-1">{formatCurrencyAmounts(debtsData.summary.totalDebtByCurrency)}</p>
                </div>
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">عدد المستحقات</p>
                  <p className="text-xl font-bold text-gray-900 mt-1">{debtsData.summary.totalPayables}</p>
                </div>
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">قيد الانتظار</p>
                  <p className="text-xl font-bold text-amber-600 mt-1">{debtsData.summary.pendingCount}</p>
                </div>
                <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                  <p className="text-xs text-gray-500">دفعة جزئية</p>
                  <p className="text-xl font-bold text-blue-600 mt-1">{debtsData.summary.partialCount}</p>
                </div>
              </div>
            )}

            {/* Debts Table */}
            <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
              {debtsLoading ? (
                <div className="p-6"><TableSkeleton rows={4} cols={7} /></div>
              ) : debtsError ? (
                <div className="p-6">
                  <QueryErrorBanner text="تعذر تحميل ديون المعامل — تحقق من الاتصال وحاول مجددًا" onRetry={() => refetchDebts()} />
                </div>
              ) : (debtsData?.data ?? []).length === 0 ? (
                <div className="flex flex-col items-center py-12 text-gray-400">
                  <DollarSign className="w-10 h-10 mb-3" />
                  <p className="font-medium">لا توجد ديون مستحقة</p>
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead className="bg-gray-50 border-b border-gray-100">
                      <tr>
                        {["المختبر", "رقم الطلب", "المبلغ", "المدفوع", "الرصيد", "الحالة", "تاريخ الاستحقاق"].map((h) => (
                          <th key={h} className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50">
                      {(debtsData?.data ?? []).map((p: LabPayable) => (
                        <tr key={p.id} className="hover:bg-gray-50">
                          <td className="px-4 py-3 font-medium text-gray-900">{p.labName ?? "—"}</td>
                          <td className="px-4 py-3 font-mono text-xs text-gray-500">{p.orderNumber ?? "—"}</td>
                          <td className="px-4 py-3 text-gray-700 whitespace-nowrap">{formatMoney(p.amount, p.currency)}</td>
                          <td className="px-4 py-3 text-green-600 whitespace-nowrap">{formatMoney(p.paidAmount, p.currency)}</td>
                          <td className="px-4 py-3 text-red-600 font-medium whitespace-nowrap">{formatMoney(p.balance, p.currency)}</td>
                          <td className="px-4 py-3">
                            <span className={cn("px-2 py-0.5 rounded-full text-xs font-medium",
                              p.status === "pending" ? "bg-amber-100 text-amber-700" :
                              p.status === "partial" ? "bg-blue-100 text-blue-700" :
                              "bg-green-100 text-green-700")}>
                              {p.status === "pending" ? "قيد الانتظار" : p.status === "partial" ? "دفعة جزئية" : "مدفوع"}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-gray-500 text-xs">{p.dueDate ?? "—"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        )}

        {tab === "aging" && (
          <div className="space-y-4">
            {agingLoading ? (
              <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-6">
                <TableSkeleton rows={4} cols={8} />
              </div>
            ) : agingError ? (
              <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-6">
                <QueryErrorBanner
                  text="تعذر تحميل أعمار المستحقات — تحقق من الاتصال وحاول مجددًا"
                  onRetry={() => refetchAging()}
                />
              </div>
            ) : (agingData?.data ?? []).length === 0 ? (
              <div className="bg-white rounded-xl border border-gray-100 shadow-sm flex flex-col items-center py-12 text-gray-400">
                <Clock className="w-10 h-10 mb-3" />
                <p className="font-medium">لا توجد مستحقات غير مسددة</p>
              </div>
            ) : (
              <>
                <p className="text-xs text-gray-500">
                  حتى تاريخ {agingData!.asOf} — الفترات محسوبة من تاريخ استحقاق كل فاتورة وتُضبط من الإعدادات.
                </p>

                {/* Totals per currency. Never one figure: a bucket holding riyals and dollars
                    is not a sum of anything. */}
                {(agingData?.totals ?? []).map((t) => (
                  <div key={t.currency} className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
                    <p className="text-xs text-gray-500 mb-3">الإجمالي بعملة {t.currency}</p>
                    <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
                      {agingColumns(agingData!.buckets).map((col) => (
                        <div key={col.key}>
                          <p className="text-[11px] text-gray-500">{col.label}</p>
                          <p className={cn("text-sm font-bold mt-0.5", col.tone)}>
                            {formatMoney(t[col.key], t.currency)}
                          </p>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}

                <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
                  <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead className="bg-gray-50 border-b border-gray-100">
                        <tr>
                          <th className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">المختبر</th>
                          <th className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">العملة</th>
                          {agingColumns(agingData!.buckets).map((col) => (
                            <th key={col.key} className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">
                              {col.label}
                            </th>
                          ))}
                          <th className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">الإجمالي</th>
                          <th className="text-start px-4 py-3 font-medium text-gray-500 text-xs whitespace-nowrap">أقدم تأخر</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50">
                        {(agingData?.data ?? []).map((row: SupplierAgingRow) => (
                          <tr key={`${row.supplierId}-${row.currency}`} className="hover:bg-gray-50">
                            <td className="px-4 py-3 font-medium text-gray-900">{row.supplierName}</td>
                            <td className="px-4 py-3 text-gray-500 text-xs">{row.currency}</td>
                            {agingColumns(agingData!.buckets).map((col) => (
                              <td key={col.key} className={cn("px-4 py-3 whitespace-nowrap", row[col.key] > 0 ? col.tone : "text-gray-300")}>
                                {formatMoney(row[col.key], row.currency)}
                              </td>
                            ))}
                            <td className="px-4 py-3 font-bold text-gray-900 whitespace-nowrap">
                              {formatMoney(row.total, row.currency)}
                            </td>
                            <td className="px-4 py-3 whitespace-nowrap">
                              {row.oldestOverdueDays === null ? (
                                <span className="text-gray-400 text-xs">—</span>
                              ) : (
                                <span className={cn(
                                  "px-2 py-0.5 rounded-full text-xs font-medium",
                                  row.oldestOverdueDays > agingData!.buckets.bucket3Days ? "bg-red-100 text-red-700" :
                                  row.oldestOverdueDays > agingData!.buckets.bucket1Days ? "bg-amber-100 text-amber-700" :
                                  "bg-gray-100 text-gray-600",
                                )}>
                                  {row.oldestOverdueDays} يوم
                                </span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </>
            )}
          </div>
        )}
      </div>
    </ErrorBoundary>
  );
}

/**
 * The ageing columns, labelled from the boundaries the server actually used.
 * <p>
 * Deliberately derived rather than written out: hardcoding "٣١–٦٠ يوم" would keep saying 60
 * after the owner changed the terms to net-45, and a header that contradicts the number under
 * it is worse than no report at all.
 */
export function agingColumns(buckets: SupplierAgingBuckets): Array<{
  key: "notYetDue" | "bucket1" | "bucket2" | "bucket3" | "bucket4Plus" | "noDueDate";
  label: string;
  tone: string;
}> {
  return [
    { key: "notYetDue",   label: "لم يستحق بعد",                                          tone: "text-gray-700" },
    { key: "bucket1",     label: `١–${buckets.bucket1Days} يوم`,                          tone: "text-amber-600" },
    { key: "bucket2",     label: `${buckets.bucket1Days + 1}–${buckets.bucket2Days} يوم`, tone: "text-orange-600" },
    { key: "bucket3",     label: `${buckets.bucket2Days + 1}–${buckets.bucket3Days} يوم`, tone: "text-red-600" },
    { key: "bucket4Plus", label: `أكثر من ${buckets.bucket3Days} يوم`,                    tone: "text-red-700" },
    { key: "noDueDate",   label: "بلا تاريخ استحقاق",                                     tone: "text-gray-500" },
  ];
}
