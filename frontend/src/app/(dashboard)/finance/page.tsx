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
const SPECIALTY_COLORS = ["#3d7ab5", "#a855f7", "#ef4444", "#f5922e"];
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
        { label: "محصّل اليوم", value: formatYemeniRiyal(summary.todayCollected), icon: TrendingUp, color: "bg-[#3d7ab518] text-accent-blue border-[#3d7ab530]" },
        { label: "محصّل هذا الشهر", value: formatYemeniRiyal(summary.monthCollected), icon: Wallet, color: "bg-[#22c55e18] text-[#22c55e] border-[#22c55e30]" },
        { label: "المبالغ المستحقة", value: formatYemeniRiyal(summary.totalOutstanding), icon: AlertCircle, color: "bg-[#ef444418] text-[#ef4444] border-[#ef444430]" },
        { label: "العقود النشطة", value: summary.activeContracts.toString(), icon: FileText, color: "bg-[#a855f718] text-[#a855f7] border-[#a855f730]" },
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
          color: "bg-[#3d7ab518] text-accent-blue border-[#3d7ab530]",
        },
        {
          label: "نسبة التحصيل",
          value: summary.collectionRate != null
            ? `${summary.collectionRate.toFixed(1)}%`
            : "—",
          icon: Percent,
          color: summary.collectionRate != null && summary.collectionRate >= 80
            ? "bg-[#22c55e18] text-[#22c55e] border-[#22c55e30]"
            : "bg-[#f59e0b18] text-[#f59e0b] border-[#f59e0b30]",
        },
        {
          label: "نسبة المتأخرات",
          value: summary.overduePercentage != null
            ? `${summary.overduePercentage.toFixed(1)}%`
            : "—",
          icon: AlertCircle,
          color: summary.overduePercentage != null && summary.overduePercentage > 20
            ? "bg-[#ef444418] text-[#ef4444] border-[#ef444430]"
            : "bg-[#f5922e18] text-[#f5922e] border-[#f5922e30]",
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
          <h1 className="text-2xl font-extrabold text-[#0d2137]">المالية</h1>
          <p className="text-sm text-[#64748b] mt-0.5">الملخص المالي والعقود والدفعات</p>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <Link href="/finance/overdue"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-[#ef444430] text-[#ef4444] hover:bg-[#ef444418] transition"
          >
            <AlertCircle className="w-4 h-4" />
            متأخرات
          </Link>
          <Link href="/finance/contracts/new"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-accent-blue text-accent-blue hover:bg-light-blue transition"
          >
            <FileText className="w-4 h-4" />
            عقد جديد
          </Link>
          <Link href="/finance/payments"
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-accent-blue text-white hover:bg-blue-hover transition"
          >
            <Plus className="w-4 h-4" />
            دفعة جديدة
          </Link>
        </div>
      </div>

      {/* Main Stats */}
      {summaryLoading ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => <div key={i} className="h-24 bg-[#eef3f9] rounded-xl" />)}
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
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-bold text-[#0d2137] text-sm">الإيرادات حسب التخصص</h2>
            <Link href="/reports" className="text-xs text-accent-blue hover:underline">
              التقرير المالي
            </Link>
          </div>
          {pieData.length === 0 ? (
            <div className="text-center py-8 text-[#94a3b8] text-sm">لا توجد بيانات</div>
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
                        <span className="text-[#64748b]">{s.name}</span>
                      </div>
                      <span className="font-semibold text-[#0d2137]">{formatYemeniRiyal(s.value)} ({pct}%)</span>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </div>

        {/* Revenue by doctor mini-table */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card">
          <div className="flex items-center justify-between px-5 py-4 border-b border-[#f1f5f9]">
            <h2 className="font-bold text-[#0d2137] text-sm">الإيرادات حسب الطبيب</h2>
            <Link href="/reports" className="text-xs text-accent-blue hover:underline">
              أداء الأطباء
            </Link>
          </div>
          {!doctorData?.length ? (
            <div className="text-center py-8 text-[#94a3b8] text-sm">لا توجد بيانات</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
                  <tr>
                    {["الطبيب", "الدفعات", "الإيرادات"].map((h) => (
                      <th key={h} className="text-start px-4 py-2.5 text-xs font-bold text-[#64748b]">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#f1f5f9]">
                  {doctorData.map((d) => (
                    <tr key={d.doctorId} className="hover:bg-[#f7fafd] transition">
                      <td className="px-4 py-2.5 font-medium text-[#0d2137]">{d.doctorName}</td>
                      <td className="px-4 py-2.5 font-mono text-[#64748b]">{d.count}</td>
                      <td className="px-4 py-2.5 font-mono font-semibold text-[#22c55e]">{formatYemeniRiyal(d.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Quick Links */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
        <h2 className="font-bold text-[#0d2137] text-sm mb-3">روابط سريعة</h2>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <QuickLink href="/finance/contracts" icon={FileText} label="العقود" color="text-accent-blue" />
          <QuickLink href="/finance/payments" icon={Wallet} label="الدفعات" color="text-[#22c55e]" />
          <QuickLink href="/finance/overdue" icon={AlertCircle} label="المتأخرات" color="text-[#ef4444]" />
          <QuickLink href="/reports" icon={BarChart3} label="التقارير" color="text-[#a855f7]" />
        </div>
      </div>

      {/* Recent Payments */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card">
        <div className="flex items-center justify-between px-5 py-4 border-b border-[#f1f5f9]">
          <h2 className="font-bold text-[#0d2137]">آخر الدفعات</h2>
          <Link href="/finance/contracts" className="text-sm text-accent-blue hover:underline">
            عرض العقود
          </Link>
        </div>

        {summaryLoading ? (
          <div className="p-5 space-y-3 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-[#eef3f9] rounded-lg" />)}
          </div>
        ) : !summary?.recentPayments.length ? (
          <div className="text-center py-12 text-[#94a3b8] text-sm">لا توجد دفعات مسجلة</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
                <tr>
                  {["المريض", "المبلغ", "التاريخ", "الطريقة", "رقم السند"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-bold text-[#64748b]">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {summary.recentPayments.map((p) => (
                  <tr key={p.id} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-3 font-medium text-[#0d2137]">{p.patientName}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-[#22c55e]">{formatYemeniRiyal(p.amount)}</td>
                    <td className="px-4 py-3 text-[#64748b]">{formatArabicDate(p.paymentDate)}</td>
                    <td className="px-4 py-3 text-[#64748b]">
                      {METHOD_LABELS[p.paymentMethod ?? ""] ?? p.paymentMethod ?? "—"}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-[#94a3b8]">{p.receiptNumber ?? "—"}</td>
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
      className="flex items-center gap-3 px-4 py-3 rounded-lg border border-[#e8f0f9] hover:border-[#dce8f5] hover:shadow-card transition group"
    >
      <Icon className={`w-5 h-5 ${color} group-hover:scale-110 transition`} />
      <span className="text-sm font-medium text-[#0d2137]">{label}</span>
    </Link>
  );
}
