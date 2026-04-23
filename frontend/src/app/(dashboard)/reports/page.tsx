"use client";
import { useEffect, useState } from "react";
import { BarChart2, Users, Calendar, TrendingUp, Stethoscope, Wallet } from "lucide-react";
import api from "@/lib/api";
import { formatYemeniRiyal } from "@/lib/utils";

interface CenterSummary {
  fromDate: string;
  toDate: string;
  totalPatients: number;
  newPatients: number;
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalRevenue: number;
}

interface DoctorPerformance {
  doctorId: string;
  name: string;
  color?: string;
  specialty?: string;
  appointmentCount: number;
  completedCount: number;
  orthoCasesCount: number;
  treatmentsCount: number;
  revenue: number;
}

interface FinancialReport {
  fromDate: string;
  toDate: string;
  totalCollected: number;
  daily: { date: string; total: number; count: number }[];
  bySpecialty: { specialty: string; total: number; count: number }[];
  byMethod: { method: string; total: number }[];
}

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم", general: "عام", surgery: "جراحة", other: "أخرى",
};
const METHOD_LABELS: Record<string, string> = {
  cash: "نقد", bank_transfer: "تحويل", card: "بطاقة",
};

type ReportType = "center" | "doctors" | "financial";

export default function ReportsPage() {
  const [activeReport, setActiveReport] = useState<ReportType>("center");
  const today = new Date().toISOString().slice(0, 10);
  const monthAgo = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);
  const [from, setFrom] = useState(monthAgo);
  const [to, setTo] = useState(today);

  const [summary, setSummary] = useState<CenterSummary | null>(null);
  const [performance, setPerformance] = useState<DoctorPerformance[]>([]);
  const [financial, setFinancial] = useState<FinancialReport | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    const params = `?from=${from}&to=${to}`;

    if (activeReport === "center") {
      api.get<CenterSummary>(`/api/reports/center-summary${params}`)
        .then((r) => setSummary(r.data))
        .catch(() => {})
        .finally(() => setLoading(false));
    } else if (activeReport === "doctors") {
      api.get<DoctorPerformance[]>(`/api/reports/doctor-performance${params}`)
        .then((r) => setPerformance(r.data))
        .catch(() => {})
        .finally(() => setLoading(false));
    } else if (activeReport === "financial") {
      api.get<FinancialReport>(`/api/reports/financial${params}`)
        .then((r) => setFinancial(r.data))
        .catch(() => {})
        .finally(() => setLoading(false));
    }
  }, [activeReport, from, to]);

  return (
    <div className="space-y-5 max-w-6xl">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">التقارير</h1>
        <p className="text-sm text-gray-500 mt-0.5">التقارير والإحصائيات</p>
      </div>

      {/* Report tabs */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-3 flex items-center gap-2 flex-wrap">
        {([
          { key: "center", label: "ملخص المركز", icon: BarChart2 },
          { key: "doctors", label: "أداء الأطباء", icon: Stethoscope },
          { key: "financial", label: "التقرير المالي", icon: Wallet },
        ] as { key: ReportType; label: string; icon: typeof BarChart2 }[]).map(({ key, label, icon: Icon }) => (
          <button key={key} onClick={() => setActiveReport(key)}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition ${
              activeReport === key
                ? "bg-clinic-teal text-white"
                : "text-gray-600 hover:bg-gray-100"
            }`}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}

        {/* Date range */}
        <div className="flex items-center gap-2 md:ms-auto">
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5"
          />
          <span className="text-gray-400">—</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5"
          />
        </div>
      </div>

      {loading ? (
        <div className="h-64 bg-gray-100 rounded-xl animate-pulse" />
      ) : activeReport === "center" && summary ? (
        <div className="space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            <StatCard label="إجمالي المرضى" value={summary.totalPatients.toString()} icon={Users} color="bg-blue-50 text-blue-600 border-blue-200" />
            <StatCard label="مرضى جدد" value={summary.newPatients.toString()} icon={TrendingUp} color="bg-teal-50 text-teal-600 border-teal-200" />
            <StatCard label="إجمالي المواعيد" value={summary.totalAppointments.toString()} icon={Calendar} color="bg-purple-50 text-purple-600 border-purple-200" />
            <StatCard label="مواعيد مكتملة" value={summary.completedAppointments.toString()} icon={Calendar} color="bg-green-50 text-green-600 border-green-200" />
            <StatCard label="حالات تقويم نشطة" value={summary.activeOrthoCases.toString()} icon={Stethoscope} color="bg-yellow-50 text-yellow-600 border-yellow-200" />
            <StatCard label="الإيرادات" value={formatYemeniRiyal(summary.totalRevenue)} icon={Wallet} color="bg-emerald-50 text-emerald-600 border-emerald-200" />
          </div>
        </div>
      ) : activeReport === "doctors" && performance.length > 0 ? (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["الطبيب", "المواعيد", "المكتملة", "حالات تقويم", "معالجات", "الإيرادات"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {performance.map((p) => (
                  <tr key={p.doctorId} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <div className="w-2 h-2 rounded-full" style={{ backgroundColor: p.color ?? "#0E7490" }} />
                        <span className="font-medium text-gray-900">{p.name}</span>
                      </div>
                      {p.specialty && <div className="text-xs text-gray-400 mr-4">{p.specialty}</div>}
                    </td>
                    <td className="px-4 py-3 font-mono text-gray-700">{p.appointmentCount}</td>
                    <td className="px-4 py-3 font-mono text-green-700">{p.completedCount}</td>
                    <td className="px-4 py-3 font-mono text-gray-700">{p.orthoCasesCount}</td>
                    <td className="px-4 py-3 font-mono text-gray-700">{p.treatmentsCount}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-emerald-700">{formatYemeniRiyal(p.revenue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : activeReport === "financial" && financial ? (
        <div className="space-y-4">
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-500">إجمالي المحصّل في الفترة</p>
                <p className="text-3xl font-extrabold text-emerald-700 font-mono mt-1">{formatYemeniRiyal(financial.totalCollected)}</p>
              </div>
              <Wallet className="w-16 h-16 text-emerald-200" />
            </div>
          </div>

          {/* By specialty */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
            <div className="px-5 py-4 border-b border-gray-100 font-bold">حسب التخصص</div>
            <div className="p-5 space-y-2">
              {financial.bySpecialty.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-4">لا توجد بيانات</p>
              ) : financial.bySpecialty.map((s) => {
                const pct = financial.totalCollected > 0 ? (s.total / financial.totalCollected) * 100 : 0;
                return (
                  <div key={s.specialty}>
                    <div className="flex items-center justify-between text-sm mb-1">
                      <span className="text-gray-700 font-medium">{SPECIALTY_LABELS[s.specialty] ?? s.specialty}</span>
                      <span className="font-mono text-gray-900">{formatYemeniRiyal(s.total)} <span className="text-xs text-gray-400">({pct.toFixed(0)}%)</span></span>
                    </div>
                    <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div className="h-full bg-clinic-teal rounded-full" style={{ width: `${pct}%` }} />
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* By method */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
            <div className="px-5 py-4 border-b border-gray-100 font-bold">حسب طريقة الدفع</div>
            <div className="p-5 grid grid-cols-3 gap-3">
              {financial.byMethod.map((m) => (
                <div key={m.method} className="text-center bg-gray-50 rounded-lg p-3">
                  <p className="text-xs text-gray-500">{METHOD_LABELS[m.method] ?? m.method}</p>
                  <p className="font-mono font-bold text-gray-900 mt-1">{formatYemeniRiyal(m.total)}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      ) : (
        <div className="text-center py-16 text-gray-400 text-sm">لا توجد بيانات للعرض</div>
      )}
    </div>
  );
}

function StatCard({ label, value, icon: Icon, color }: { label: string; value: string; icon: typeof BarChart2; color: string }) {
  return (
    <div className={`rounded-xl border p-4 ${color}`}>
      <Icon className="w-5 h-5 mb-2 opacity-80" />
      <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
      <p className="text-xs font-medium mt-1 opacity-70">{label}</p>
    </div>
  );
}
