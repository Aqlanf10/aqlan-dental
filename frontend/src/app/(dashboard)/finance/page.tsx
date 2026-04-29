"use client";
import Link from "next/link";
import {
  TrendingUp, Wallet, AlertCircle, FileText, Plus,
  BarChart3, Percent,
} from "lucide-react";
import {
  ResponsiveContainer, PieChart, Pie, Cell, Tooltip,
} from "recharts";
import { useFinanceSummary, useFinanceBySpecialty, useFinanceByDoctor } from "@/hooks/useFinance";
import { formatYemeniRiyal, formatArabicDate } from "@/lib/utils";

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم", general: "عام", surgery: "جراحة", other: "أخرى",
  ortho: "تقويم",
};
const SPECIALTY_COLORS = ["#0E7490", "#7C3AED", "#DC2626", "#D97706"];
const METHOD_LABELS: Record<string, string> = {
  cash: "نقداً", bank_transfer: "تحويل بنكي", card: "بطاقة",
};

export default function FinancePage() {
  const today = new Date().toISOString().slice(0, 10);
  const monthAgo = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);

  const { data: summary, isLoading: summaryLoading } = useFinanceSummary();
  const { data: specialtyData } = useFinanceBySpecialty(monthAgo, today);
  const { data: doctorData } = useFinanceByDoctor(monthAgo, today);

  const stats = summary
    ? [
        { label: "محصّل اليوم", value: formatYemeniRiyal(summary.todayCollected), icon: TrendingUp, color: "bg-teal-50 text-teal-600 border-teal-200" },
        { label: "محصّل هذا الشهر", value: formatYemeniRiyal(summary.monthCollected), icon: Wallet, color: "bg-emerald-50 text-emerald-600 border-emerald-200" },
        { label: "المبالغ المستحقة", value: formatYemeniRiyal(summary.totalOutstanding), icon: AlertCircle, color: "bg-red-50 text-red-600 border-red-200" },
        { label: "العقود النشطة", value: summary.activeContracts.toString(), icon: FileText, color: "bg-purple-50 text-purple-600 border-purple-200" },
      ]
    : [];

  // KPI cards
  const kpis = summary
    ? [
        {
          label: "متوسط قيمة العقد",
          value: summary.averageContractValue
            ? formatYemeniRiyal(summary.averageContractValue)
            : "—",
          icon: BarChart3,
          color: "bg-cyan-50 text-cyan-700 border-cyan-200",
        },
        {
          label: "نسبة التحصيل",
          value: summary.collectionRate != null
            ? `${summary.collectionRate.toFixed(1)}%`
            : "—",
          icon: Percent,
          color: summary.collectionRate != null && summary.collectionRate >= 80
            ? "bg-green-50 text-green-700 border-green-200"
            : "bg-amber-50 text-amber-700 border-amber-200",
        },
        {
          label: "نسبة المتأخرات",
          value: summary.overduePercentage != null
            ? `${summary.overduePercentage.toFixed(1)}%`
            : "—",
          icon: AlertCircle,
          color: summary.overduePercentage != null && summary.overduePercentage > 20
            ? "bg-red-50 text-red-700 border-red-200"
            : "bg-orange-50 text-orange-700 border-orange-200",
        },
      ]
    : [];

  // Pie chart data for revenue by specialty
  const pieData = (specialtyData ?? []).map((s) => ({
    name: SPECIALTY_LABELS[s.specialty] ?? s.specialty,
    value: s.total,
  }));

  const totalSpecialtyRevenue = pieData.reduce((s, d) => s + d.value, 0);

  return (
    <div className="space-y-5 max-w-6xl">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">المالية</h1>
          <p className="text-sm text-gray-500 mt-0.5">الملخص المالي والعقود والدفعات</p>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <Link href="/finance/overdue"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-red-200 text-red-600 hover:bg-red-50 transition"
          >
            <AlertCircle className="w-4 h-4" />
            متأخرات
          </Link>
          <Link href="/finance/contracts/new"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-clinic-teal text-clinic-teal hover:bg-teal-50 transition"
          >
            <FileText className="w-4 h-4" />
            عقد جديد
          </Link>
          <Link href="/finance/payments"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
          >
            <Plus className="w-4 h-4" />
            دفعة جديدة
          </Link>
        </div>
      </div>

      {/* Main Stats */}
      {summaryLoading ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-24 bg-gray-100 rounded-xl" />)}
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {stats.map(({ label, value, icon: Icon, color }) => (
            <div key={label} className={`rounded-xl border p-4 ${color}`}>
              <Icon className="w-5 h-5 mb-2 opacity-80" />
              <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
              <p className="text-xs font-medium mt-1 opacity-70">{label}</p>
            </div>
          ))}
        </div>
      )}

      {/* KPI Cards */}
      {kpis.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {kpis.map(({ label, value, icon: Icon, color }) => (
            <div key={label} className={`rounded-xl border p-4 flex items-center gap-4 ${color}`}>
              <Icon className="w-8 h-8 opacity-70" />
              <div>
                <p className="text-xs font-medium opacity-70">{label}</p>
                <p className="text-xl font-extrabold font-mono">{value}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Revenue by Specialty & Doctor Table */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Revenue by specialty mini-chart */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-bold text-gray-900 text-sm">الإيرادات حسب التخصص</h2>
            <Link href="/reports" className="text-xs text-clinic-teal hover:underline">
              التقرير المالي
            </Link>
          </div>
          {pieData.length === 0 ? (
            <div className="text-center py-8 text-gray-400 text-sm">لا توجد بيانات</div>
          ) : (
            <>
              <ResponsiveContainer width="100%" height={160}>
                <PieChart>
                  <Pie data={pieData} cx="50%" cy="50%" innerRadius={40} outerRadius={65}
                    paddingAngle={3} dataKey="value" nameKey="name">
                    {pieData.map((_, i) => (
                      <Cell key={i} fill={SPECIALTY_COLORS[i % SPECIALTY_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, ""]} />
                </PieChart>
              </ResponsiveContainer>
              <div className="mt-3 space-y-1.5">
                {pieData.map((s, i) => {
                  const pct = totalSpecialtyRevenue > 0 ? Math.round((s.value / totalSpecialtyRevenue) * 100) : 0;
                  return (
                    <div key={s.name} className="flex items-center justify-between text-xs">
                      <div className="flex items-center gap-1.5">
                        <span className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                          style={{ backgroundColor: SPECIALTY_COLORS[i % SPECIALTY_COLORS.length] }} />
                        <span className="text-gray-600">{s.name}</span>
                      </div>
                      <span className="font-semibold text-gray-900">{formatYemeniRiyal(s.value)} ({pct}%)</span>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </div>

        {/* Revenue by doctor mini-table */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h2 className="font-bold text-gray-900 text-sm">الإيرادات حسب الطبيب</h2>
            <Link href="/reports" className="text-xs text-clinic-teal hover:underline">
              أداء الأطباء
            </Link>
          </div>
          {!doctorData?.length ? (
            <div className="text-center py-8 text-gray-400 text-sm">لا توجد بيانات</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 border-b border-gray-100">
                  <tr>
                    {["الطبيب", "الدفعات", "الإيرادات"].map((h) => (
                      <th key={h} className="text-start px-4 py-2.5 text-xs font-semibold text-gray-500">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {doctorData.map((d) => (
                    <tr key={d.doctorId} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-2.5 font-medium text-gray-900">{d.doctorName}</td>
                      <td className="px-4 py-2.5 font-mono text-gray-600">{d.count}</td>
                      <td className="px-4 py-2.5 font-mono font-semibold text-emerald-700">{formatYemeniRiyal(d.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Quick Links */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <h2 className="font-bold text-gray-900 text-sm mb-3">روابط سريعة</h2>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <QuickLink href="/finance/contracts" icon={FileText} label="العقود" color="text-clinic-teal" />
          <QuickLink href="/finance/payments" icon={Wallet} label="الدفعات" color="text-emerald-600" />
          <QuickLink href="/finance/overdue" icon={AlertCircle} label="المتأخرات" color="text-red-600" />
          <QuickLink href="/reports" icon={BarChart3} label="التقارير" color="text-purple-600" />
        </div>
      </div>

      {/* Recent Payments */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر الدفعات</h2>
          <Link href="/finance/contracts" className="text-sm text-clinic-teal hover:underline">
            عرض العقود
          </Link>
        </div>

        {summaryLoading ? (
          <div className="p-5 space-y-3 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}
          </div>
        ) : !summary?.recentPayments.length ? (
          <div className="text-center py-12 text-gray-400 text-sm">لا توجد دفعات مسجلة</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["المريض", "المبلغ", "التاريخ", "الطريقة", "رقم السند"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {summary.recentPayments.map((p) => (
                  <tr key={p.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-medium text-gray-900">{p.patientName}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-green-700">{formatYemeniRiyal(p.amount)}</td>
                    <td className="px-4 py-3 text-gray-600">{formatArabicDate(p.paymentDate)}</td>
                    <td className="px-4 py-3 text-gray-600">
                      {METHOD_LABELS[p.paymentMethod ?? ""] ?? p.paymentMethod ?? "—"}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-400">{p.receiptNumber ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Quick Link Card ────────────────────────────────────────────────────────

function QuickLink({ href, icon: Icon, label, color }: {
  href: string; icon: typeof FileText; label: string; color: string;
}) {
  return (
    <Link href={href}
      className="flex items-center gap-3 px-4 py-3 rounded-lg border border-gray-200 hover:border-gray-300 hover:shadow-sm transition group"
    >
      <Icon className={`w-5 h-5 ${color} group-hover:scale-110 transition`} />
      <span className="text-sm font-medium text-gray-700">{label}</span>
    </Link>
  );
}
