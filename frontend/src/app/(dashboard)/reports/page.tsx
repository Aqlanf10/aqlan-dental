"use client";
import { useEffect, useState } from "react";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, LineChart, Line, PieChart, Pie, Cell,
} from "recharts";
import { BarChart2, Users, Calendar, TrendingUp, Stethoscope, Wallet, Download } from "lucide-react";
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
const SPECIALTY_COLORS = ["#0E7490", "#7C3AED", "#DC2626", "#D97706"];

function formatYER(val: number) {
  if (val >= 1_000_000) return `${(val / 1_000_000).toFixed(1)}م`;
  if (val >= 1_000)     return `${(val / 1_000).toFixed(0)}ك`;
  return String(val);
}

interface TooltipProps { active?: boolean; payload?: { value: number }[]; label?: string; }

const TooltipRevenue = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-gray-500 mb-1">{label}</p>
      <p className="font-bold text-clinic-teal">{payload[0].value.toLocaleString()} ر.ي</p>
    </div>
  );
};

const TooltipAppt = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-gray-500 mb-1">{label}</p>
      <p className="font-bold text-purple-600">{payload[0].value} موعد</p>
    </div>
  );
};

type ReportType = "center" | "doctors" | "financial";

function downloadCsv(url: string, filename: string) {
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
}

export default function ReportsPage() {
  const [activeReport, setActiveReport] = useState<ReportType>("center");
  const [exporting, setExporting] = useState(false);
  const today    = new Date().toISOString().slice(0, 10);
  const monthAgo = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);
  const [from, setFrom] = useState(monthAgo);
  const [to,   setTo]   = useState(today);

  const handleExport = async (type: "patients" | "payments" | "appointments") => {
    setExporting(true);
    try {
      const token = sessionStorage.getItem("accessToken") ?? "";
      const params = type === "patients" ? "" : `?from=${from}&to=${to}`;
      const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? ""}/api/reports/export/${type}${params}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      downloadCsv(url, `${type}_${today}.csv`);
      URL.revokeObjectURL(url);
    } catch {
      alert("فشل التصدير");
    } finally {
      setExporting(false);
    }
  };

  const [summary,     setSummary]     = useState<CenterSummary | null>(null);
  const [performance, setPerformance] = useState<DoctorPerformance[]>([]);
  const [financial,   setFinancial]   = useState<FinancialReport | null>(null);
  const [loading,     setLoading]     = useState(false);

  useEffect(() => {
    setLoading(true);
    const params = `?from=${from}&to=${to}`;
    if (activeReport === "center") {
      api.get<CenterSummary>(`/api/reports/center-summary${params}`)
        .then((r) => setSummary(r.data)).catch(() => {}).finally(() => setLoading(false));
    } else if (activeReport === "doctors") {
      api.get<DoctorPerformance[]>(`/api/reports/doctor-performance${params}`)
        .then((r) => setPerformance(r.data)).catch(() => {}).finally(() => setLoading(false));
    } else {
      api.get<FinancialReport>(`/api/reports/financial${params}`)
        .then((r) => setFinancial(r.data)).catch(() => {}).finally(() => setLoading(false));
    }
  }, [activeReport, from, to]);

  return (
    <div className="space-y-5 max-w-6xl">
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">التقارير</h1>
        <p className="text-sm text-gray-500 mt-0.5">التقارير والإحصائيات التفصيلية</p>
      </div>

      {/* Tabs + date range */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-3 flex items-center gap-2 flex-wrap">
        {([
          { key: "center",    label: "ملخص المركز",    icon: BarChart2 },
          { key: "doctors",   label: "أداء الأطباء",   icon: Stethoscope },
          { key: "financial", label: "التقرير المالي", icon: Wallet },
        ] as { key: ReportType; label: string; icon: typeof BarChart2 }[]).map(({ key, label, icon: Icon }) => (
          <button key={key} onClick={() => setActiveReport(key)}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition ${
              activeReport === key ? "bg-clinic-teal text-white" : "text-gray-600 hover:bg-gray-100"
            }`}
          >
            <Icon className="w-4 h-4" />
            {label}
          </button>
        ))}
        <div className="flex items-center gap-2 md:ms-auto flex-wrap">
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5" />
          <span className="text-gray-400">—</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5" />
          {/* Export buttons */}
          <div className="flex items-center gap-1 border-s border-gray-200 ps-2">
            <span className="text-xs text-gray-400 me-1">تصدير:</span>
            {(["patients", "payments", "appointments"] as const).map(type => (
              <button
                key={type}
                onClick={() => handleExport(type)}
                disabled={exporting}
                title={type === "patients" ? "تصدير المرضى" : type === "payments" ? "تصدير الدفعات" : "تصدير المواعيد"}
                className="flex items-center gap-1 px-2 py-1.5 text-xs rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-50 transition"
              >
                <Download className="w-3 h-3" />
                {type === "patients" ? "المرضى" : type === "payments" ? "الدفعات" : "المواعيد"}
              </button>
            ))}
          </div>
        </div>
      </div>

      {loading ? (
        <div className="h-64 bg-gray-100 rounded-xl animate-pulse" />
      ) : activeReport === "center" && summary ? (
        <div className="space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            <StatCard label="إجمالي المرضى"       value={summary.totalPatients.toString()}        icon={Users}       color="bg-blue-50 text-blue-600 border-blue-200" />
            <StatCard label="مرضى جدد"             value={summary.newPatients.toString()}          icon={TrendingUp}  color="bg-teal-50 text-teal-600 border-teal-200" />
            <StatCard label="إجمالي المواعيد"      value={summary.totalAppointments.toString()}    icon={Calendar}    color="bg-purple-50 text-purple-600 border-purple-200" />
            <StatCard label="مواعيد مكتملة"        value={summary.completedAppointments.toString()} icon={Calendar}   color="bg-green-50 text-green-600 border-green-200" />
            <StatCard label="حالات تقويم نشطة"    value={summary.activeOrthoCases.toString()}     icon={Stethoscope} color="bg-yellow-50 text-yellow-600 border-yellow-200" />
            <StatCard label="الإيرادات"            value={formatYemeniRiyal(summary.totalRevenue)} icon={Wallet}      color="bg-emerald-50 text-emerald-600 border-emerald-200" />
          </div>

          {/* Completion rate bar */}
          {summary.totalAppointments > 0 && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
              <p className="text-sm font-semibold text-gray-700 mb-3">نسبة إكمال المواعيد</p>
              <div className="flex items-center gap-3">
                <div className="flex-1 h-3 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-clinic-teal rounded-full transition-all"
                    style={{ width: `${Math.round((summary.completedAppointments / summary.totalAppointments) * 100)}%` }}
                  />
                </div>
                <span className="text-sm font-bold text-gray-900 flex-shrink-0">
                  {Math.round((summary.completedAppointments / summary.totalAppointments) * 100)}%
                </span>
              </div>
            </div>
          )}
        </div>

      ) : activeReport === "doctors" && performance.length > 0 ? (
        <div className="space-y-4">
          {/* Revenue bar chart by doctor */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
            <h3 className="font-bold text-gray-900 text-sm mb-4 flex items-center gap-2">
              <Wallet className="w-4 h-4 text-emerald-600" />
              الإيرادات حسب الطبيب
            </h3>
            <ResponsiveContainer width="100%" height={180}>
              <BarChart data={performance} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                <XAxis dataKey="name" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
                <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
                <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, "الإيرادات"]} />
                <Bar dataKey="revenue" fill="#0E7490" radius={[4, 4, 0, 0]} maxBarSize={32} />
              </BarChart>
            </ResponsiveContainer>
          </div>

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
                          <div className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: p.color ?? "#0E7490" }} />
                          <span className="font-medium text-gray-900">{p.name}</span>
                        </div>
                        {p.specialty && <div className="text-xs text-gray-400 mr-4.5">{p.specialty}</div>}
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
        </div>

      ) : activeReport === "financial" && financial ? (
        <div className="space-y-4">
          {/* Total */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-500">إجمالي المحصّل في الفترة</p>
                <p className="text-3xl font-extrabold text-emerald-700 font-mono mt-1">
                  {formatYemeniRiyal(financial.totalCollected)}
                </p>
              </div>
              <Wallet className="w-14 h-14 text-emerald-100" />
            </div>
          </div>

          {/* Daily revenue chart */}
          {financial.daily.length > 0 && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
              <h3 className="font-bold text-gray-900 text-sm mb-4 flex items-center gap-2">
                <TrendingUp className="w-4 h-4 text-clinic-teal" />
                الإيرادات اليومية
              </h3>
              <ResponsiveContainer width="100%" height={180}>
                <BarChart data={financial.daily} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                  <XAxis
                    dataKey="date"
                    tick={{ fontSize: 9, fill: "#9CA3AF" }}
                    tickLine={false}
                    axisLine={false}
                    interval={Math.ceil(financial.daily.length / 8)}
                  />
                  <YAxis
                    tickFormatter={formatYER}
                    tick={{ fontSize: 9, fill: "#9CA3AF" }}
                    tickLine={false}
                    axisLine={false}
                  />
                  <Tooltip content={<TooltipRevenue />} />
                  <Bar dataKey="total" fill="#0E7490" radius={[3, 3, 0, 0]} maxBarSize={20} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}

          {/* Daily appointments chart */}
          {financial.daily.length > 0 && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
              <h3 className="font-bold text-gray-900 text-sm mb-4 flex items-center gap-2">
                <Calendar className="w-4 h-4 text-purple-600" />
                دفعات يومية (عدد)
              </h3>
              <ResponsiveContainer width="100%" height={140}>
                <LineChart data={financial.daily} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                  <XAxis
                    dataKey="date"
                    tick={{ fontSize: 9, fill: "#9CA3AF" }}
                    tickLine={false}
                    axisLine={false}
                    interval={Math.ceil(financial.daily.length / 8)}
                  />
                  <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
                  <Tooltip content={<TooltipAppt />} />
                  <Line type="monotone" dataKey="count" stroke="#7C3AED" strokeWidth={2} dot={false} activeDot={{ r: 4, fill: "#7C3AED" }} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* By specialty with pie chart */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
              <h3 className="font-bold text-gray-900 text-sm mb-4">حسب التخصص</h3>
              {financial.bySpecialty.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-4">لا توجد بيانات</p>
              ) : (
                <>
                  <ResponsiveContainer width="100%" height={140}>
                    <PieChart>
                      <Pie data={financial.bySpecialty} cx="50%" cy="50%" innerRadius={40} outerRadius={65}
                        paddingAngle={3} dataKey="total" nameKey="specialty">
                        {financial.bySpecialty.map((_, i) => (
                          <Cell key={i} fill={SPECIALTY_COLORS[i % SPECIALTY_COLORS.length]} />
                        ))}
                      </Pie>
                      <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, ""]} />
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="mt-3 space-y-2">
                    {financial.bySpecialty.map((s, i) => {
                      const pct = financial.totalCollected > 0
                        ? Math.round((s.total / financial.totalCollected) * 100)
                        : 0;
                      return (
                        <div key={s.specialty} className="flex items-center justify-between text-xs">
                          <div className="flex items-center gap-1.5">
                            <span className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                              style={{ backgroundColor: SPECIALTY_COLORS[i % SPECIALTY_COLORS.length] }} />
                            <span className="text-gray-600">{SPECIALTY_LABELS[s.specialty] ?? s.specialty}</span>
                          </div>
                          <span className="font-semibold text-gray-900">{formatYemeniRiyal(s.total)} ({pct}%)</span>
                        </div>
                      );
                    })}
                  </div>
                </>
              )}
            </div>

            {/* By payment method */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
              <h3 className="font-bold text-gray-900 text-sm mb-4">حسب طريقة الدفع</h3>
              <div className="space-y-3">
                {financial.byMethod.length === 0 ? (
                  <p className="text-sm text-gray-400 text-center py-4">لا توجد بيانات</p>
                ) : financial.byMethod.map((m) => {
                  const pct = financial.totalCollected > 0
                    ? Math.round((m.total / financial.totalCollected) * 100)
                    : 0;
                  return (
                    <div key={m.method}>
                      <div className="flex items-center justify-between text-sm mb-1">
                        <span className="text-gray-700 font-medium">{METHOD_LABELS[m.method] ?? m.method}</span>
                        <span className="font-mono text-gray-900">
                          {formatYemeniRiyal(m.total)}{" "}
                          <span className="text-xs text-gray-400">({pct}%)</span>
                        </span>
                      </div>
                      <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                        <div className="h-full bg-emerald-500 rounded-full" style={{ width: `${pct}%` }} />
                      </div>
                    </div>
                  );
                })}
              </div>
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
