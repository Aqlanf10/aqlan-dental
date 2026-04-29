"use client";
import { useState } from "react";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, LineChart, Line, PieChart, Pie, Cell,
} from "recharts";
import {
  BarChart2, Users, Calendar, TrendingUp, Stethoscope, Wallet,
  ShieldCheck, SmilePlus, Scissors, Activity,
} from "lucide-react";
import {
  useCenterSummary,
  useDoctorPerformance,
  useFinancialReport,
  useOrthoReport,
  useGeneralReport,
  useSurgeryReport,
  useGrowthAnalysis,
} from "@/hooks/useReports";
import { useProfitLoss } from "@/hooks/useFinance";
import { formatYemeniRiyal, cn } from "@/lib/utils";
import type {
  OrthoReport,
  GeneralDentistryReport,
  SurgeryReport,
  GrowthAnalysis,
} from "@/types/finance";

// ─── Constants ──────────────────────────────────────────────────────────────

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم", general: "عام", surgery: "جراحة", other: "أخرى",
  ortho: "تقويم",
};
const METHOD_LABELS: Record<string, string> = {
  cash: "نقد", bank_transfer: "تحويل", card: "بطاقة",
};
const SPECIALTY_COLORS = ["#3d7ab5", "#7C3AED", "#DC2626", "#D97706", "#059669"];

const ORTHO_STAGE_LABELS: Record<string, string> = {
  alignment: "المحاذاة", leveling: "التسوية", space_closure: "إغلاق الفراغات",
  finishing: "الإنهاء", retention: "الاحتفاظ", debonding: "إزالة الجهاز",
};

const TREATMENT_TYPE_LABELS: Record<string, string> = {
  filling: "حشو", extraction: "خلع", root_canal: "علاج عصب",
  cleaning: "تنظيف", crown: "تاج/جسر", whitening: "تبييض",
  veneer: "قشور", implant: "زراعة", denture: "طقم",
  perio: "لثة", other: "أخرى",
};

const SURGERY_TYPE_LABELS: Record<string, string> = {
  impaction: "خلع ملحقة", fracture: "كسر", cyst: "كيسة",
  tumor: "ورم", bone_graft: "ترقيع عظم", implant: "زراعة",
  tmj: "مفصل فكي", other: "أخرى",
};

const SURGERY_STATUS_LABELS: Record<string, string> = {
  scheduled: "مجدول", in_progress: "جارٍ", completed: "مكتمل",
  cancelled: "ملغى", referred: "محال",
};

function formatYER(val: number) {
  if (val >= 1_000_000) return `${(val / 1_000_000).toFixed(1)}م`;
  if (val >= 1_000) return `${(val / 1_000).toFixed(0)}ك`;
  return String(val);
}

// ─── Tooltip components ─────────────────────────────────────────────────────

interface TooltipProps { active?: boolean; payload?: { value: number }[]; label?: string; }

const TooltipRevenue = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-[#e8f0f9] rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-[#64748b] mb-1">{label}</p>
      <p className="font-bold text-accent-blue">{payload[0].value.toLocaleString()} ر.ي</p>
    </div>
  );
};

const TooltipAppt = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-[#e8f0f9] rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-[#64748b] mb-1">{label}</p>
      <p className="font-bold text-[#a855f7]">{payload[0].value}</p>
    </div>
  );
};

const TooltipCount = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-[#e8f0f9] rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-[#64748b] mb-1">{label}</p>
      <p className="font-bold text-accent-blue">{payload[0].value}</p>
    </div>
  );
};

// ─── Report tabs definition ─────────────────────────────────────────────────

type ReportType = "center" | "doctors" | "financial" | "ortho" | "general" | "surgery" | "growth" | "profit";

const REPORT_TABS: { key: ReportType; label: string; icon: typeof BarChart2 }[] = [
  { key: "center",    label: "ملخص المركز",      icon: BarChart2 },
  { key: "doctors",   label: "أداء الأطباء",     icon: Stethoscope },
  { key: "financial", label: "التقرير المالي",   icon: Wallet },
  { key: "ortho",     label: "تقارير التقويم",   icon: ShieldCheck },
  { key: "general",   label: "طب الأسنان العام", icon: SmilePlus },
  { key: "surgery",   label: "جراحة الفكين",    icon: Scissors },
  { key: "growth",    label: "تحليل النمو",      icon: Activity },
  { key: "profit",    label: "الأرباح والخسائر", icon: TrendingUp },
];

// ─── Main Component ─────────────────────────────────────────────────────────

export default function ReportsPage() {
  const [activeReport, setActiveReport] = useState<ReportType>("center");
  const today = new Date().toISOString().slice(0, 10);
  const monthAgo = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);
  const [from, setFrom] = useState(monthAgo);
  const [to, setTo] = useState(today);

  // Queries for all report types
  const centerQ = useCenterSummary(from, to);
  const doctorsQ = useDoctorPerformance(from, to);
  const financialQ = useFinancialReport(from, to);
  const orthoQ = useOrthoReport(from, to);
  const generalQ = useGeneralReport(from, to);
  const surgeryQ = useSurgeryReport(from, to);
  const growthQ = useGrowthAnalysis();
  const profitQ = useProfitLoss(from, to);

  const loading =
    (activeReport === "center" && centerQ.isLoading) ||
    (activeReport === "doctors" && doctorsQ.isLoading) ||
    (activeReport === "financial" && financialQ.isLoading) ||
    (activeReport === "ortho" && orthoQ.isLoading) ||
    (activeReport === "general" && generalQ.isLoading) ||
    (activeReport === "surgery" && surgeryQ.isLoading) ||
    (activeReport === "growth" && growthQ.isLoading) ||
    (activeReport === "profit" && profitQ.isLoading);

  return (
    <div className="space-y-5 max-w-6xl">
      <div>
        <h1 className="text-2xl font-extrabold text-[#0d2137]">التقارير</h1>
        <p className="text-sm text-[#64748b] mt-0.5">التقارير والإحصائيات التفصيلية</p>
      </div>

      {/* Tabs + date range */}
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-3 flex items-center gap-2 flex-wrap">
        {REPORT_TABS.map(({ key, label, icon: Icon }) => (
          <button key={key} onClick={() => setActiveReport(key)}
            className={`flex items-center gap-2 px-3 py-1.5 text-sm font-medium rounded-lg transition ${
              activeReport === key ? "bg-accent-blue text-white" : "text-[#64748b] hover:bg-[#eef3f9]"
            }`}
          >
            <Icon className="w-4 h-4" />
            <span className="hidden sm:inline">{label}</span>
          </button>
        ))}
        {activeReport !== "growth" && (
          <div className="flex items-center gap-2 md:ms-auto">
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
              className="text-sm border border-[#e8f0f9] rounded-lg px-2 py-1.5" />
            <span className="text-[#94a3b8]">—</span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
              className="text-sm border border-[#e8f0f9] rounded-lg px-2 py-1.5" />
          </div>
        )}
      </div>

      {loading ? (
        <div className="h-64 bg-[#eef3f9] rounded-xl animate-pulse" />
      ) : (
        <>
          {activeReport === "center" && <CenterReport summary={centerQ.data} />}
          {activeReport === "doctors" && <DoctorsReport performance={doctorsQ.data ?? []} />}
          {activeReport === "financial" && <FinancialReportView report={financialQ.data} />}
          {activeReport === "ortho" && <OrthoReportView report={orthoQ.data} />}
          {activeReport === "general" && <GeneralReportView report={generalQ.data} />}
          {activeReport === "surgery" && <SurgeryReportView report={surgeryQ.data} />}
          {activeReport === "growth" && <GrowthReportView analysis={growthQ.data} />}
          {activeReport === "profit" && <ProfitLossReport data={profitQ.data} />}
        </>
      )}
    </div>
  );
}

// ─── Center Report ──────────────────────────────────────────────────────────

function CenterReport({ summary }: { summary?: import("@/types/finance").CenterSummary }) {
  if (!summary) return <EmptyState />;
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي المرضى" value={summary.totalPatients.toString()} icon={Users} color="bg-[#3d7ab518] text-accent-blue border-[#3d7ab530]" />
        <StatCard label="مرضى جدد" value={summary.newPatients.toString()} icon={TrendingUp} color="bg-light-blue text-accent-blue border-teal-200" />
        <StatCard label="إجمالي المواعيد" value={summary.totalAppointments.toString()} icon={Calendar} color="bg-[#a855f718] text-[#a855f7] border-purple-200" />
        <StatCard label="مواعيد مكتملة" value={summary.completedAppointments.toString()} icon={Calendar} color="bg-[#22c55e18] text-[#22c55e] border-green-200" />
        <StatCard label="حالات تقويم نشطة" value={summary.activeOrthoCases.toString()} icon={Stethoscope} color="bg-[#f59e0b18] text-[#f59e0b] border-[#f59e0b30]" />
        <StatCard label="الإيرادات" value={formatYemeniRiyal(summary.totalRevenue)} icon={Wallet} color="bg-[#22c55e18] text-[#22c55e] border-emerald-200" />
      </div>
      {summary.totalAppointments > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <p className="text-sm font-bold text-[#64748b] mb-3">نسبة إكمال المواعيد</p>
          <div className="flex items-center gap-3">
            <div className="flex-1 h-3 bg-[#eef3f9] rounded-full overflow-hidden">
              <div
                className="h-full bg-accent-blue rounded-full transition-all"
                style={{ width: `${Math.round((summary.completedAppointments / summary.totalAppointments) * 100)}%` }}
              />
            </div>
            <span className="text-sm font-bold text-[#0d2137] flex-shrink-0">
              {Math.round((summary.completedAppointments / summary.totalAppointments) * 100)}%
            </span>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Doctors Report ─────────────────────────────────────────────────────────

function DoctorsReport({ performance }: { performance: import("@/types/finance").DoctorPerformance[] }) {
  if (!performance.length) return <EmptyState />;
  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
        <h3 className="font-bold text-[#0d2137] text-sm mb-4 flex items-center gap-2">
          <Wallet className="w-4 h-4 text-[#22c55e]" />
          الإيرادات حسب الطبيب
        </h3>
        <ResponsiveContainer width="100%" height={180}>
          <BarChart data={performance} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
            <XAxis dataKey="name" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
            <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
            <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, "الإيرادات"]} />
            <Bar dataKey="revenue" fill="#3d7ab5" radius={[4, 4, 0, 0]} maxBarSize={32} />
          </BarChart>
        </ResponsiveContainer>
      </div>
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-[#f7fafd] border-b border-[#e8f0f9]">
              <tr>
                {["الطبيب", "المواعيد", "المكتملة", "حالات تقويم", "معالجات", "الإيرادات"].map((h) => (
                  <th key={h} className="text-start px-4 py-3 text-xs font-bold text-[#64748b] whitespace-nowrap">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f1f5f9]">
              {performance.map((p) => (
                <tr key={p.doctorId} className="hover:bg-[#f7fafd] transition">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <div className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: p.color ?? "#3d7ab5" }} />
                      <span className="font-medium text-[#0d2137]">{p.name}</span>
                    </div>
                    {p.specialty && <div className="text-xs text-[#94a3b8] mr-4.5">{p.specialty}</div>}
                  </td>
                  <td className="px-4 py-3 font-mono text-[#64748b]">{p.appointmentCount}</td>
                  <td className="px-4 py-3 font-mono text-[#22c55e]">{p.completedCount}</td>
                  <td className="px-4 py-3 font-mono text-[#64748b]">{p.orthoCasesCount}</td>
                  <td className="px-4 py-3 font-mono text-[#64748b]">{p.treatmentsCount}</td>
                  <td className="px-4 py-3 font-mono font-semibold text-[#22c55e]">{formatYemeniRiyal(p.revenue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

// ─── Financial Report ───────────────────────────────────────────────────────

function FinancialReportView({ report }: { report?: import("@/types/finance").FinancialReport }) {
  if (!report) return <EmptyState />;
  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-[#64748b]">إجمالي المحصّل في الفترة</p>
            <p className="text-3xl font-extrabold text-[#22c55e] font-mono mt-1">
              {formatYemeniRiyal(report.totalCollected)}
            </p>
          </div>
          <Wallet className="w-14 h-14 text-emerald-100" />
        </div>
      </div>

      {report.daily.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4 flex items-center gap-2">
            <TrendingUp className="w-4 h-4 text-accent-blue" />
            الإيرادات اليومية
          </h3>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={report.daily} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="date" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false}
                interval={Math.ceil(report.daily.length / 8)} />
              <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <Tooltip content={<TooltipRevenue />} />
              <Bar dataKey="total" fill="#3d7ab5" radius={[3, 3, 0, 0]} maxBarSize={20} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      {report.daily.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-[#a855f7]" />
            دفعات يومية (عدد)
          </h3>
          <ResponsiveContainer width="100%" height={140}>
            <LineChart data={report.daily} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="date" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false}
                interval={Math.ceil(report.daily.length / 8)} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipAppt />} />
              <Line type="monotone" dataKey="count" stroke="#a855f7" strokeWidth={2} dot={false} activeDot={{ r: 4, fill: "#7C3AED" }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* By specialty */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">حسب التخصص</h3>
          {report.bySpecialty.length === 0 ? (
            <p className="text-sm text-[#94a3b8] text-center py-4">لا توجد بيانات</p>
          ) : (
            <>
              <ResponsiveContainer width="100%" height={140}>
                <PieChart>
                  <Pie data={report.bySpecialty} cx="50%" cy="50%" innerRadius={40} outerRadius={65}
                    paddingAngle={3} dataKey="total" nameKey="specialty">
                    {report.bySpecialty.map((_, i) => (
                      <Cell key={i} fill={SPECIALTY_COLORS[i % SPECIALTY_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, ""]} />
                </PieChart>
              </ResponsiveContainer>
              <div className="mt-3 space-y-2">
                {report.bySpecialty.map((s, i) => {
                  const pct = report.totalCollected > 0 ? Math.round((s.total / report.totalCollected) * 100) : 0;
                  return (
                    <div key={s.specialty} className="flex items-center justify-between text-xs">
                      <div className="flex items-center gap-1.5">
                        <span className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                          style={{ backgroundColor: SPECIALTY_COLORS[i % SPECIALTY_COLORS.length] }} />
                        <span className="text-[#64748b]">{SPECIALTY_LABELS[s.specialty] ?? s.specialty}</span>
                      </div>
                      <span className="font-semibold text-[#0d2137]">{formatYemeniRiyal(s.total)} ({pct}%)</span>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </div>

        {/* By payment method */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">حسب طريقة الدفع</h3>
          <div className="space-y-3">
            {report.byMethod.length === 0 ? (
              <p className="text-sm text-[#94a3b8] text-center py-4">لا توجد بيانات</p>
            ) : report.byMethod.map((m) => {
              const pct = report.totalCollected > 0 ? Math.round((m.total / report.totalCollected) * 100) : 0;
              return (
                <div key={m.method}>
                  <div className="flex items-center justify-between text-sm mb-1">
                    <span className="text-[#64748b] font-medium">{METHOD_LABELS[m.method] ?? m.method}</span>
                    <span className="font-mono text-[#0d2137]">
                      {formatYemeniRiyal(m.total)}{" "}
                      <span className="text-xs text-[#94a3b8]">({pct}%)</span>
                    </span>
                  </div>
                  <div className="h-2 bg-[#eef3f9] rounded-full overflow-hidden">
                    <div className="h-full bg-[#22c55e18]0 rounded-full" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Ortho Report ───────────────────────────────────────────────────────────

function OrthoReportView({ report }: { report?: OrthoReport }) {
  if (!report) return <EmptyState />;

  const stageData = report.byStage.map((s) => ({
    stage: ORTHO_STAGE_LABELS[s.stage] ?? s.stage,
    count: s.count,
  }));

  return (
    <div className="space-y-4">
      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="حالات نشطة" value={report.activeCases.toString()} icon={ShieldCheck} color="bg-light-blue text-accent-blue border-teal-200" />
        <StatCard label="حالات مكتملة" value={report.completedCases.toString()} icon={TrendingUp} color="bg-[#22c55e18] text-[#22c55e] border-green-200" />
        <StatCard label="حالات جديدة" value={report.newCases.toString()} icon={Users} color="bg-[#3d7ab518] text-accent-blue border-[#3d7ab530]" />
        <StatCard label="متوسط المدة (شهر)" value={report.averageDuration.toFixed(1)} icon={Calendar} color="bg-[#a855f718] text-[#a855f7] border-purple-200" />
      </div>

      {/* Cases by stage */}
      {stageData.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">توزيع المراحل</h3>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={stageData} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="stage" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount />} />
              <Bar dataKey="count" fill="#3d7ab5" radius={[4, 4, 0, 0]} maxBarSize={40} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* Monthly cases trend */}
      {report.monthlyCases.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">حالات التقويم الشهرية</h3>
          <ResponsiveContainer width="100%" height={180}>
            <LineChart data={report.monthlyCases} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="month" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount />} />
              <Line type="monotone" dataKey="count" stroke="#3d7ab5" strokeWidth={2} dot={{ fill: "#3d7ab5", r: 3 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {/* By doctor table */}
      {report.byDoctor.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <div className="px-5 py-3 border-b border-[#f1f5f9]">
            <h3 className="font-bold text-[#0d2137] text-sm">حسب الطبيب</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd]">
                <tr>
                  {["الطبيب", "عدد الحالات"].map((h) => (
                    <th key={h} className="text-start px-4 py-2.5 text-xs font-bold text-[#64748b]">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {report.byDoctor.map((d) => (
                  <tr key={d.doctorId} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-2.5 font-medium text-[#0d2137]">{d.doctorName}</td>
                    <td className="px-4 py-2.5 font-mono">{d.count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── General Dentistry Report ───────────────────────────────────────────────

function GeneralReportView({ report }: { report?: GeneralDentistryReport }) {
  if (!report) return <EmptyState />;

  const treatmentPieData = report.byType.map((t) => ({
    name: TREATMENT_TYPE_LABELS[t.type] ?? t.type,
    value: t.count,
    revenue: t.revenue,
  }));

  const topProceduresData = report.topProcedures.map((p) => ({
    procedure: TREATMENT_TYPE_LABELS[p.procedure] ?? p.procedure,
    count: p.count,
  }));

  return (
    <div className="space-y-4">
      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي المعالجات" value={report.totalTreatments.toString()} icon={SmilePlus} color="bg-light-blue text-accent-blue border-teal-200" />
        <StatCard label="إجمالي الإيرادات" value={formatYemeniRiyal(report.byType.reduce((s, t) => s + t.revenue, 0))} icon={Wallet} color="bg-[#22c55e18] text-[#22c55e] border-emerald-200" />
        <StatCard label="أنواع المعالجات" value={report.byType.length.toString()} icon={BarChart2} color="bg-[#a855f718] text-[#a855f7] border-purple-200" />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Treatment type distribution pie chart */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">توزيع أنواع المعالجات</h3>
          {treatmentPieData.length === 0 ? (
            <p className="text-sm text-[#94a3b8] text-center py-8">لا توجد بيانات</p>
          ) : (
            <>
              <ResponsiveContainer width="100%" height={180}>
                <PieChart>
                  <Pie data={treatmentPieData} cx="50%" cy="50%" innerRadius={40} outerRadius={70}
                    paddingAngle={3} dataKey="value" nameKey="name">
                    {treatmentPieData.map((_, i) => (
                      <Cell key={i} fill={SPECIALTY_COLORS[i % SPECIALTY_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(v, name) => [`${v} معالجة`, name]} />
                </PieChart>
              </ResponsiveContainer>
              <div className="mt-3 space-y-1.5 max-h-40 overflow-y-auto">
                {treatmentPieData.map((t, i) => (
                  <div key={t.name} className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-1.5">
                      <span className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                        style={{ backgroundColor: SPECIALTY_COLORS[i % SPECIALTY_COLORS.length] }} />
                      <span className="text-[#64748b]">{t.name}</span>
                    </div>
                    <span className="font-semibold text-[#0d2137]">{t.value} ({formatYemeniRiyal(t.revenue)})</span>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>

        {/* Top procedures bar chart */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">أكثر المعالجات</h3>
          {topProceduresData.length === 0 ? (
            <p className="text-sm text-[#94a3b8] text-center py-8">لا توجد بيانات</p>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={topProceduresData} layout="vertical" margin={{ top: 0, right: 20, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                <XAxis type="number" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
                <YAxis type="category" dataKey="procedure" tick={{ fontSize: 10, fill: "#6B7280" }} tickLine={false} axisLine={false} width={70} />
                <Tooltip content={<TooltipCount />} />
                <Bar dataKey="count" fill="#059669" radius={[0, 4, 4, 0]} maxBarSize={20} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* By doctor table */}
      {report.byDoctor.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <div className="px-5 py-3 border-b border-[#f1f5f9]">
            <h3 className="font-bold text-[#0d2137] text-sm">حسب الطبيب</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd]">
                <tr>
                  {["الطبيب", "عدد المعالجات", "الإيرادات"].map((h) => (
                    <th key={h} className="text-start px-4 py-2.5 text-xs font-bold text-[#64748b]">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {report.byDoctor.map((d) => (
                  <tr key={d.doctorId} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-2.5 font-medium text-[#0d2137]">{d.doctorName}</td>
                    <td className="px-4 py-2.5 font-mono">{d.count}</td>
                    <td className="px-4 py-2.5 font-mono font-semibold text-[#22c55e]">{formatYemeniRiyal(d.revenue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Surgery Report ─────────────────────────────────────────────────────────

function SurgeryReportView({ report }: { report?: SurgeryReport }) {
  if (!report) return <EmptyState />;

  const typeData = report.byType.map((t) => ({
    type: SURGERY_TYPE_LABELS[t.type] ?? t.type,
    count: t.count,
  }));

  const statusData = report.byStatus.map((s) => ({
    status: SURGERY_STATUS_LABELS[s.status] ?? s.status,
    count: s.count,
  }));

  return (
    <div className="space-y-4">
      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي الحالات" value={report.totalCases.toString()} icon={Scissors} color="bg-[#ef444418] text-[#ef4444] border-[#ef444430]" />
        <StatCard label="أنواع العمليات" value={report.byType.length.toString()} icon={BarChart2} color="bg-[#f5922e18] text-[#f5922e] border-orange-200" />
        <StatCard label="الأطباء" value={report.byDoctor.length.toString()} icon={Stethoscope} color="bg-light-blue text-accent-blue border-teal-200" />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Case type distribution */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">توزيع أنواع العمليات</h3>
          {typeData.length === 0 ? (
            <p className="text-sm text-[#94a3b8] text-center py-8">لا توجد بيانات</p>
          ) : (
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie data={typeData} cx="50%" cy="50%" innerRadius={40} outerRadius={70}
                  paddingAngle={3} dataKey="count" nameKey="type">
                  {typeData.map((_, i) => (
                    <Cell key={i} fill={SPECIALTY_COLORS[i % SPECIALTY_COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v, name) => [`${v} حالة`, name]} />
              </PieChart>
            </ResponsiveContainer>
          )}
          {typeData.length > 0 && (
            <div className="mt-3 space-y-1.5">
              {typeData.map((t, i) => (
                <div key={t.type} className="flex items-center justify-between text-xs">
                  <div className="flex items-center gap-1.5">
                    <span className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                      style={{ backgroundColor: SPECIALTY_COLORS[i % SPECIALTY_COLORS.length] }} />
                    <span className="text-[#64748b]">{t.type}</span>
                  </div>
                  <span className="font-semibold text-[#0d2137]">{t.count}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Status breakdown */}
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">توزيع الحالات حسب الوضع</h3>
          {statusData.length === 0 ? (
            <p className="text-sm text-[#94a3b8] text-center py-8">لا توجد بيانات</p>
          ) : (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={statusData} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                <XAxis dataKey="status" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
                <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
                <Tooltip content={<TooltipCount />} />
                <Bar dataKey="count" fill="#DC2626" radius={[4, 4, 0, 0]} maxBarSize={40} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* By doctor */}
      {report.byDoctor.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <div className="px-5 py-3 border-b border-[#f1f5f9]">
            <h3 className="font-bold text-[#0d2137] text-sm">حسب الطبيب</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd]">
                <tr>
                  {["الطبيب", "عدد الحالات"].map((h) => (
                    <th key={h} className="text-start px-4 py-2.5 text-xs font-bold text-[#64748b]">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {report.byDoctor.map((d) => (
                  <tr key={d.doctorId} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-2.5 font-medium text-[#0d2137]">{d.doctorName}</td>
                    <td className="px-4 py-2.5 font-mono">{d.count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Growth Analysis ────────────────────────────────────────────────────────

function GrowthReportView({ analysis }: { analysis?: GrowthAnalysis }) {
  if (!analysis) return <EmptyState />;

  return (
    <div className="space-y-4">
      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="مرضى جدد (12 شهر)" value={analysis.totalNew.toString()} icon={Users} color="bg-light-blue text-accent-blue border-teal-200" />
        <StatCard label="مرضى عائدون" value={analysis.totalReturning.toString()} icon={TrendingUp} color="bg-[#3d7ab518] text-accent-blue border-[#3d7ab530]" />
        <StatCard label="معدل النمو" value={`${analysis.growthRate.toFixed(1)}%`} icon={Activity} color="bg-[#22c55e18] text-[#22c55e] border-emerald-200" />
        <StatCard label="إجمالي المرضى" value={analysis.totalNew + analysis.totalReturning + ""} icon={Users} color="bg-[#a855f718] text-[#a855f7] border-purple-200" />
      </div>

      {/* Monthly growth line chart */}
      {analysis.months.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-4">المرضى الجدد شهرياً (12 شهر)</h3>
          <ResponsiveContainer width="100%" height={250}>
            <LineChart data={analysis.months} margin={{ top: 5, right: 20, left: -10, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="month" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip />
              <Line type="monotone" dataKey="newPatients" name="مرضى جدد" stroke="#3d7ab5" strokeWidth={2.5}
                dot={{ fill: "#3d7ab5", r: 3 }} activeDot={{ r: 5 }} />
              <Line type="monotone" dataKey="returningPatients" name="مرضى عائدون" stroke="#a855f7" strokeWidth={2}
                dot={{ fill: "#7C3AED", r: 2 }} activeDot={{ r: 4 }} strokeDasharray="5 5" />
            </LineChart>
          </ResponsiveContainer>
          <div className="flex items-center justify-center gap-6 mt-3 text-xs text-[#64748b]">
            <div className="flex items-center gap-1.5">
              <span className="w-6 h-0.5 bg-[#3d7ab5] inline-block" />
              مرضى جدد
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-6 h-0.5 bg-[#7C3AED] inline-block" style={{ borderTop: "2px dashed #7C3AED" }} />
              مرضى عائدون
            </div>
          </div>
        </div>
      )}

      {/* Monthly table */}
      {analysis.months.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card overflow-hidden">
          <div className="px-5 py-3 border-b border-[#f1f5f9]">
            <h3 className="font-bold text-[#0d2137] text-sm">تفاصيل شهرية</h3>
          </div>
          <div className="overflow-x-auto max-h-72 overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="bg-[#f7fafd] sticky top-0">
                <tr>
                  {["الشهر", "جدد", "عائدون", "الإجمالي"].map((h) => (
                    <th key={h} className="text-start px-4 py-2.5 text-xs font-bold text-[#64748b]">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f1f5f9]">
                {analysis.months.map((m) => (
                  <tr key={m.month} className="hover:bg-[#f7fafd] transition">
                    <td className="px-4 py-2.5 font-medium text-[#0d2137]">{m.month}</td>
                    <td className="px-4 py-2.5 font-mono text-accent-blue font-semibold">{m.newPatients}</td>
                    <td className="px-4 py-2.5 font-mono text-[#a855f7]">{m.returningPatients}</td>
                    <td className="px-4 py-2.5 font-mono font-bold text-[#0d2137]">{m.total}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Shared Components ──────────────────────────────────────────────────────

function StatCard({ label, value, icon: Icon, color }: { label: string; value: string; icon: typeof BarChart2; color: string }) {
  return (
    <div className={`rounded-xl border p-4 ${color}`}>
      <Icon className="w-5 h-5 mb-2 opacity-80" />
      <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
      <p className="text-xs font-medium mt-1 opacity-70">{label}</p>
    </div>
  );
}

function EmptyState() {
  return (
    <div className="text-center py-16 text-[#94a3b8] text-sm">لا توجد بيانات للعرض</div>
  );
}

// ─── Profit & Loss Report ───────────────────────────────────────────────────

function ProfitLossReport({ data }: { data?: import("@/types/finance").ProfitLoss }) {
  if (!data) return <EmptyState />;

  const isProfit = data.netProfit >= 0;

  return (
    <div className="space-y-4">
      {/* Main P&L card */}
      <div className={cn(
        "rounded-xl border-2 p-6",
        isProfit ? "bg-[#22c55e18] border-green-300" : "bg-[#ef444418] border-red-300"
      )}>
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-[#64748b]">
              {isProfit ? "صافي الربح" : "صافي الخسارة"}
            </p>
            <p className={cn("text-4xl font-extrabold font-mono mt-1", isProfit ? "text-[#22c55e]" : "text-[#ef4444]")}>
              {formatYemeniRiyal(Math.abs(data.netProfit))}
            </p>
            <p className="text-xs text-[#64748b] mt-1">
              هامش الربح: {data.profitMargin}%
            </p>
          </div>
          <div className={cn("text-6xl", isProfit ? "text-green-200" : "text-red-200")}>
            {isProfit ? "📈" : "📉"}
          </div>
        </div>
      </div>

      {/* Revenue vs Expenses comparison */}
      <div className="grid grid-cols-2 gap-4">
        <div className="bg-[#22c55e18] border border-emerald-200 rounded-xl p-5 text-center">
          <p className="text-sm text-[#22c55e] font-medium">إجمالي الإيرادات</p>
          <p className="text-2xl font-extrabold text-[#22c55e] font-mono mt-1">{formatYemeniRiyal(data.totalRevenue)}</p>
        </div>
        <div className="bg-[#ef444418] border border-[#ef444430] rounded-xl p-5 text-center">
          <p className="text-sm text-[#ef4444] font-medium">إجمالي المصروفات</p>
          <p className="text-2xl font-extrabold text-[#ef4444] font-mono mt-1">{formatYemeniRiyal(data.totalExpenses)}</p>
        </div>
      </div>

      {/* Revenue by specialty */}
      {data.revenue.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-3">الإيرادات حسب التخصص</h3>
          <div className="space-y-2">
            {data.revenue.map((r) => {
              const pct = data.totalRevenue > 0 ? Math.round((r.totalRevenue / data.totalRevenue) * 100) : 0;
              return (
                <div key={r.specialty}>
                  <div className="flex items-center justify-between text-sm mb-1">
                    <span className="text-[#64748b] font-medium">{r.specialtyAr}</span>
                    <span className="font-mono text-[#0d2137]">{formatYemeniRiyal(r.totalRevenue)} ({pct}%)</span>
                  </div>
                  <div className="h-2 bg-[#eef3f9] rounded-full overflow-hidden">
                    <div className="h-full bg-[#22c55e18]0 rounded-full" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Expenses by category */}
      {data.expenses.length > 0 && (
        <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-card p-5">
          <h3 className="font-bold text-[#0d2137] text-sm mb-3">المصروفات حسب الفئة</h3>
          <div className="space-y-2">
            {data.expenses.map((e) => {
              const pct = data.totalExpenses > 0 ? Math.round((e.total / data.totalExpenses) * 100) : 0;
              return (
                <div key={e.category}>
                  <div className="flex items-center justify-between text-sm mb-1">
                    <span className="text-[#64748b] font-medium">{e.categoryAr}</span>
                    <span className="font-mono text-[#0d2137]">{formatYemeniRiyal(e.total)} ({pct}%)</span>
                  </div>
                  <div className="h-2 bg-[#eef3f9] rounded-full overflow-hidden">
                    <div className="h-full bg-red-400 rounded-full" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
