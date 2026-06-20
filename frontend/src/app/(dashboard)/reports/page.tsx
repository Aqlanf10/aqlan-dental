"use client";
import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, LineChart, Line, PieChart, Pie, Cell,
  AreaChart, Area,
} from "recharts";
import {
  BarChart2, Users, Calendar, TrendingUp, Stethoscope, Wallet,
  Download, ChevronLeft, ChevronRight, RefreshCw, FileSpreadsheet,
  Activity, PieChart as PieChartIcon, Scissors, ClipboardList,
  Pill, Heart, Filter, Building2, UserCheck, Clock,
} from "lucide-react";
import api, { downloadBlob } from "@/lib/api";
import { formatYemeniRiyal, localDateString } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { Skeleton } from "@/components/ui/skeleton";

/* ═══════════════════════════════════════════════════════════════════════════
   TYPES
   ═══════════════════════════════════════════════════════════════════════════ */

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

interface AppointmentAnalytics {
  totalAppointments: number;
  statusDistribution: { status: string; count: number }[];
  peakHours: { hour: number; count: number }[];
  averagePerDay: number;
  noShowRate: number;
  cancellationRate: number;
  completionRate: number;
  byType: { type: string; count: number }[];
}

interface PatientDemographics {
  ageDistribution: { bucket: string; count: number }[];
  genderSplit: { label: string; count: number; percentage: number }[];
  referralSources: { source: string; count: number }[];
  newPatientsPerMonth: { month: string; count: number }[];
  activePatients: number;
}

interface RevenueComparison {
  currentPeriodTotal: number;
  previousPeriodTotal: number;
  percentageChange: number;
  monthlyComparison: { month: string; currentPeriod: number; previousPeriod: number }[];
  specialtyComparison: { specialty: string; currentPeriod: number; previousPeriod: number }[];
}

interface OverdueContracts {
  overdueContracts: {
    contractId: string; patientName: string;
    totalAmount: number; paidAmount: number; remaining: number;
    daysOverdue: number;
  }[];
  agingBuckets: { bucket: string; amount: number }[];
  totalOverdueAmount: number;
}

interface OrthoProgress {
  casesByStatus: { status: string; count: number }[];
  averageTreatmentDurationMonths: number;
  casesPerDoctor: { doctorId: string; doctorName: string; count: number }[];
  orthoRevenue: number;
}

interface SurgeryStats {
  casesByType: { type: string; count: number }[];
  casesPerDoctor: { doctorId: string; doctorName: string; count: number }[];
  monthlyTrend: { month: string; count: number }[];
}

interface TreatmentPlanCompletion {
  stepsByStatus: { status: string; count: number }[];
  completionRate: number;
  perDoctor: { doctorId: string; doctorName: string; totalSteps: number; completedSteps: number; completionRate: number }[];
}

interface LabOrders {
  ordersByStatus: { status: string; count: number }[];
  perDoctor: { doctorId: string; doctorName: string; count: number }[];
  overdueOrdersCount: number;
}

interface PrescriptionAnalytics {
  mostPrescribed: { drugName: string; count: number }[];
  perDoctor: { doctorId: string; doctorName: string; count: number }[];
  monthlyTrend: { month: string; count: number }[];
}

interface PatientRetention {
  newPatients: number;
  returningPatients: number;
  averageDaysBetweenVisits: number;
  churnRiskPatients: number;
}

interface BookingFunnel {
  byStatus: { status: string; count: number }[];
  conversionRate: number;
  appointmentCreationRate: number;
  perService: { service: string; total: number; confirmed: number; converted: number }[];
}

/* ═══════════════════════════════════════════════════════════════════════════
   CONSTANTS
   ═══════════════════════════════════════════════════════════════════════════ */

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم", general: "عام", surgery: "جراحة", other: "أخرى",
  pediatric: "أطفال", cosmetic: "تجميل", endodontics: "علاج عصب",
};
const METHOD_LABELS: Record<string, string> = {
  cash: "نقد", bank_transfer: "تحويل", card: "بطاقة",
};
const STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول", Confirmed: "مؤكد", Arrived: "وصل",
  InProgress: "جاري العلاج", Completed: "مكتمل", Cancelled: "ملغي", NoShow: "لم يحضر",
  pending: "قيد الانتظار", in_progress: "جاري العلاج", completed: "مكتمل",
  cancelled: "ملغي", active: "نشط", overdue: "متأخر",
  delivered: "تم التسليم", sent: "مرسل", draft: "مسودة",
};
const CHART_COLORS = ["#3d7ab5", "#f5922e", "#10b981", "#8b5cf6", "#ef4444", "#06b6d4", "#f97316", "#6366f1"];
const SPECIALTY_COLORS = ["#3d7ab5", "#8b5cf6", "#ef4444", "#f97316"];

type ReportKey =
  | "center-summary" | "appointment-analytics" | "patient-demographics"
  | "financial" | "revenue-comparison" | "overdue-contracts"
  | "doctor-performance" | "ortho-progress" | "surgery-stats" | "treatment-plan-completion"
  | "lab-orders" | "prescription-analytics" | "patient-retention" | "booking-funnel";

interface ReportCategory {
  key: string;
  label: string;
  icon: typeof BarChart2;
  reports: { key: ReportKey; label: string; icon: typeof BarChart2; desc: string }[];
}

const REPORT_CATEGORIES: ReportCategory[] = [
  {
    key: "general", label: "عام", icon: BarChart2,
    reports: [
      { key: "center-summary", label: "ملخص المركز", icon: Building2, desc: "نظرة شاملة على أداء المركز" },
      { key: "appointment-analytics", label: "تحليل المواعيد", icon: Calendar, desc: "إحصائيات تفصيلية للمواعيد" },
      { key: "patient-demographics", label: "ديموغرافيا المرضى", icon: Users, desc: "توزيع المرضى حسب العمر والجنس" },
    ],
  },
  {
    key: "financial", label: "مالي", icon: Wallet,
    reports: [
      { key: "financial", label: "التقرير المالي", icon: Wallet, desc: "تفاصيل الإيرادات والتحصيل" },
      { key: "revenue-comparison", label: "مقارنة الإيرادات", icon: TrendingUp, desc: "مقارنة الإيرادات بين الفترات" },
      { key: "overdue-contracts", label: "العقود المتأخرة", icon: Clock, desc: "العقود ذات الأقساط المتأخرة" },
    ],
  },
  {
    key: "specialties", label: "تخصصات", icon: Stethoscope,
    reports: [
      { key: "doctor-performance", label: "أداء الأطباء", icon: Stethoscope, desc: "مقارنة أداء الأطباء" },
      { key: "ortho-progress", label: "حالات التقويم", icon: Activity, desc: "تتبع حالات التقويم" },
      { key: "surgery-stats", label: "إحصائيات الجراحة", icon: Scissors, desc: "إحصائيات العمليات الجراحية" },
      { key: "treatment-plan-completion", label: "خطط العلاج", icon: ClipboardList, desc: "نسبة إكمال خطط العلاج" },
    ],
  },
  {
    key: "operational", label: "تشغيلي", icon: Filter,
    reports: [
      { key: "lab-orders", label: "طلبات المختبر", icon: FileSpreadsheet, desc: "تتبع طلبات المختبر" },
      { key: "prescription-analytics", label: "الوصفات الطبية", icon: Pill, desc: "تحليل الوصفات الطبية" },
      { key: "patient-retention", label: "احتفاظ المرضى", icon: Heart, desc: "معدلات احتفاظ المرضى" },
      { key: "booking-funnel", label: "قمع الحجوزات", icon: PieChartIcon, desc: "تحليل قمع تحويل الحجوزات" },
    ],
  },
];

/* ═══════════════════════════════════════════════════════════════════════════
   HELPERS
   ═══════════════════════════════════════════════════════════════════════════ */

function formatYER(val: number) {
  if (val >= 1_000_000) return `${(val / 1_000_000).toFixed(1)}م`;
  if (val >= 1_000) return `${(val / 1_000).toFixed(0)}ك`;
  return String(val);
}

function formatPct(val: number) {
  return `${Math.round(val)}%`;
}

interface TooltipPayloadItem { value: number; name?: string; payload?: Record<string, unknown> }
interface CustomTooltipProps { active?: boolean; payload?: TooltipPayloadItem[]; label?: string }

const TooltipRevenue = ({ active, payload, label }: CustomTooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-gray-500 mb-1">{label}</p>
      <p className="font-bold text-clinic-blue">{payload[0].value.toLocaleString()} ر.ي</p>
    </div>
  );
};

const TooltipCount = ({ active, payload, label, unit }: CustomTooltipProps & { unit?: string }) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-md px-3 py-2 text-xs">
      <p className="text-gray-500 mb-1">{label}</p>
      <p className="font-bold text-clinic-blue">{payload[0].value} {unit ?? ""}</p>
    </div>
  );
};

function downloadCsv(url: string, filename: string) {
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
}

/* ═══════════════════════════════════════════════════════════════════════════
   STAT CARD
   ═══════════════════════════════════════════════════════════════════════════ */

function StatCard({ label, value, icon: Icon, color }: { label: string; value: string; icon: typeof BarChart2; color: string }) {
  return (
    <div className={`rounded-xl border p-4 ${color}`}>
      <Icon className="w-5 h-5 mb-2 opacity-80" />
      <p className="text-2xl font-extrabold leading-tight font-mono">{value}</p>
      <p className="text-xs font-medium mt-1 opacity-70">{label}</p>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   LOADING SKELETON
   ═══════════════════════════════════════════════════════════════════════════ */

function ReportSkeleton() {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        {[1, 2, 3].map((i) => (
          <div key={i} className="bg-white rounded-xl border border-gray-200 p-4 space-y-3">
            <Skeleton className="h-5 w-5 rounded" />
            <Skeleton className="h-7 w-20" />
            <Skeleton className="h-3 w-16" />
          </div>
        ))}
      </div>
      <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-3">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-48 w-full" />
      </div>
      <div className="bg-white rounded-xl border border-gray-200 p-5 space-y-3">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-36 w-full" />
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   ERROR STATE
   ═══════════════════════════════════════════════════════════════════════════ */

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-gray-400 space-y-4">
      <p className="text-sm">فشل تحميل البيانات</p>
      <button onClick={onRetry}
        className="flex items-center gap-2 px-4 py-2 text-sm bg-clinic-blue text-white rounded-lg hover:bg-clinic-blue-600 transition">
        <RefreshCw className="w-4 h-4" />
        إعادة المحاولة
      </button>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   CHART WRAPPER
   ═══════════════════════════════════════════════════════════════════════════ */

function ChartCard({ title, icon, children }: { title: string; icon?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
      <h3 className="font-bold text-gray-900 text-sm mb-4 flex items-center gap-2">
        {icon}
        {title}
      </h3>
      {children}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: ملخص المركز (Center Summary)
   ═══════════════════════════════════════════════════════════════════════════ */

function CenterSummaryReport({ data }: { data: CenterSummary }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي المرضى" value={data.totalPatients.toString()} icon={Users} color="bg-blue-50 text-blue-600 border-blue-200" />
        <StatCard label="مرضى جدد" value={data.newPatients.toString()} icon={TrendingUp} color="bg-clinic-blue-50 text-clinic-blue border-clinic-blue-100" />
        <StatCard label="إجمالي المواعيد" value={data.totalAppointments.toString()} icon={Calendar} color="bg-purple-50 text-purple-600 border-purple-200" />
        <StatCard label="مواعيد مكتملة" value={data.completedAppointments.toString()} icon={Calendar} color="bg-green-50 text-green-600 border-green-200" />
        <StatCard label="حالات تقويم نشطة" value={data.activeOrthoCases.toString()} icon={Stethoscope} color="bg-yellow-50 text-yellow-600 border-yellow-200" />
        <StatCard label="الإيرادات" value={formatYemeniRiyal(data.totalRevenue)} icon={Wallet} color="bg-emerald-50 text-emerald-600 border-emerald-200" />
      </div>
      {data.totalAppointments > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <p className="text-sm font-semibold text-gray-700 mb-3">نسبة إكمال المواعيد</p>
          <div className="flex items-center gap-3">
            <div className="flex-1 h-3 bg-gray-100 rounded-full overflow-hidden">
              <div
                className="h-full bg-clinic-blue rounded-full transition-all"
                style={{ width: `${Math.round((data.completedAppointments / data.totalAppointments) * 100)}%` }}
              />
            </div>
            <span className="text-sm font-bold text-gray-900 flex-shrink-0">
              {Math.round((data.completedAppointments / data.totalAppointments) * 100)}%
            </span>
          </div>
        </div>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: تحليل المواعيد (Appointment Analytics)
   ═══════════════════════════════════════════════════════════════════════════ */

function AppointmentAnalyticsReport({ data }: { data: AppointmentAnalytics }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="إجمالي المواعيد" value={data.totalAppointments.toString()} icon={Calendar} color="bg-blue-50 text-blue-600 border-blue-200" />
        <StatCard label="متوسط يومي" value={data.averagePerDay.toFixed(1)} icon={TrendingUp} color="bg-purple-50 text-purple-600 border-purple-200" />
        <StatCard label="نسبة عدم الحضور" value={formatPct(data.noShowRate)} icon={UserCheck} color="bg-red-50 text-red-600 border-red-200" />
        <StatCard label="نسبة الإكمال" value={formatPct(data.completionRate)} icon={Activity} color="bg-green-50 text-green-600 border-green-200" />
      </div>

      {/* Status distribution */}
      {data.statusDistribution.length > 0 && (
        <ChartCard title="توزيع الحالات" icon={<Calendar className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={200}>
            <PieChart>
              <Pie data={data.statusDistribution} cx="50%" cy="50%" innerRadius={50} outerRadius={80}
                paddingAngle={3} dataKey="count" nameKey="status">
                {data.statusDistribution.map((_, i) => (
                  <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                ))}
              </Pie>
              <Tooltip formatter={(v) => [v, ""]} />
            </PieChart>
          </ResponsiveContainer>
          <div className="mt-3 flex flex-wrap gap-3">
            {data.statusDistribution.map((s, i) => (
              <div key={s.status} className="flex items-center gap-1.5 text-xs">
                <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: CHART_COLORS[i % CHART_COLORS.length] }} />
                <span className="text-gray-600">{STATUS_LABELS[s.status] ?? s.status}</span>
                <span className="font-semibold">{s.count}</span>
              </div>
            ))}
          </div>
        </ChartCard>
      )}

      {/* Peak hours */}
      {data.peakHours.length > 0 && (
        <ChartCard title="ساعات الذروة" icon={<Clock className="w-4 h-4 text-clinic-orange" />}>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={data.peakHours} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="hour" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false}
                tickFormatter={(h: number) => `${h}:00`} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="موعد" />} />
              <Bar dataKey="count" fill="#f5922e" radius={[4, 4, 0, 0]} maxBarSize={28} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {/* By type */}
      {data.byType.length > 0 && (
        <ChartCard title="حسب النوع" icon={<BarChart2 className="w-4 h-4 text-purple-600" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.byType} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="type" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="موعد" />} />
              <Bar dataKey="count" fill="#8b5cf6" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: ديموغرافيا المرضى (Patient Demographics)
   ═══════════════════════════════════════════════════════════════════════════ */

function PatientDemographicsReport({ data }: { data: PatientDemographics }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="المرضى النشطون" value={data.activePatients.toString()} icon={Users} color="bg-blue-50 text-blue-600 border-blue-200" />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Age distribution */}
        {data.ageDistribution.length > 0 && (
          <ChartCard title="التوزيع العمري" icon={<Users className="w-4 h-4 text-clinic-blue" />}>
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={data.ageDistribution} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                <XAxis dataKey="bucket" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
                <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
                <Tooltip content={<TooltipCount unit="مريض" />} />
                <Bar dataKey="count" fill="#3d7ab5" radius={[4, 4, 0, 0]} maxBarSize={32} />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        )}

        {/* Gender split */}
        {data.genderSplit.length > 0 && (
          <ChartCard title="التوزيع حسب الجنس" icon={<Users className="w-4 h-4 text-purple-600" />}>
            <ResponsiveContainer width="100%" height={200}>
              <PieChart>
                <Pie data={data.genderSplit} cx="50%" cy="50%" innerRadius={50} outerRadius={80}
                  paddingAngle={3} dataKey="count" nameKey="label">
                  {data.genderSplit.map((_, i) => (
                    <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v) => [v, ""]} />
              </PieChart>
            </ResponsiveContainer>
            <div className="mt-3 flex justify-center gap-4">
              {data.genderSplit.map((g, i) => (
                <div key={g.label} className="flex items-center gap-1.5 text-xs">
                  <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: CHART_COLORS[i % CHART_COLORS.length] }} />
                  <span className="text-gray-600">{g.label}</span>
                  <span className="font-semibold">{g.count} ({formatPct(g.percentage)})</span>
                </div>
              ))}
            </div>
          </ChartCard>
        )}
      </div>

      {/* New patients per month */}
      {data.newPatientsPerMonth.length > 0 && (
        <ChartCard title="المرضى الجدد شهرياً" icon={<TrendingUp className="w-4 h-4 text-emerald-600" />}>
          <ResponsiveContainer width="100%" height={200}>
            <AreaChart data={data.newPatientsPerMonth} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="month" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="مريض" />} />
              <Area type="monotone" dataKey="count" stroke="#10b981" fill="#d1fae5" strokeWidth={2} />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {/* Referral sources */}
      {data.referralSources.length > 0 && (
        <ChartCard title="مصادر الإحالة" icon={<Building2 className="w-4 h-4 text-clinic-orange" />}>
          <div className="space-y-3">
            {data.referralSources.map((s) => {
              const maxCount = Math.max(...data.referralSources.map((r) => r.count));
              const pct = maxCount > 0 ? Math.round((s.count / maxCount) * 100) : 0;
              return (
                <div key={s.source}>
                  <div className="flex items-center justify-between text-sm mb-1">
                    <span className="text-gray-700 font-medium">{s.source}</span>
                    <span className="font-mono text-gray-900">{s.count}</span>
                  </div>
                  <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div className="h-full bg-clinic-orange rounded-full" style={{ width: `${pct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: التقرير المالي (Financial)
   ═══════════════════════════════════════════════════════════════════════════ */

function FinancialReportView({ data }: { data: FinancialReport }) {
  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-gray-500">إجمالي المحصّل في الفترة</p>
            <p className="text-3xl font-extrabold text-emerald-700 font-mono mt-1">
              {formatYemeniRiyal(data.totalCollected)}
            </p>
          </div>
          <Wallet className="w-14 h-14 text-emerald-100" />
        </div>
      </div>

      {data.daily.length > 0 && (
        <ChartCard title="الإيرادات اليومية" icon={<TrendingUp className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.daily} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="date" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false}
                interval={Math.ceil(data.daily.length / 8)} />
              <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <Tooltip content={<TooltipRevenue />} />
              <Bar dataKey="total" fill="#3d7ab5" radius={[3, 3, 0, 0]} maxBarSize={20} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.daily.length > 0 && (
        <ChartCard title="دفعات يومية (عدد)" icon={<Calendar className="w-4 h-4 text-purple-600" />}>
          <ResponsiveContainer width="100%" height={140}>
            <LineChart data={data.daily} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="date" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false}
                interval={Math.ceil(data.daily.length / 8)} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="دفعة" />} />
              <Line type="monotone" dataKey="count" stroke="#8b5cf6" strokeWidth={2} dot={false} activeDot={{ r: 4, fill: "#8b5cf6" }} />
            </LineChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {data.bySpecialty.length > 0 && (
          <ChartCard title="حسب التخصص">
            <ResponsiveContainer width="100%" height={140}>
              <PieChart>
                <Pie data={data.bySpecialty} cx="50%" cy="50%" innerRadius={40} outerRadius={65}
                  paddingAngle={3} dataKey="total" nameKey="specialty">
                  {data.bySpecialty.map((_, i) => (
                    <Cell key={i} fill={SPECIALTY_COLORS[i % SPECIALTY_COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, ""]} />
              </PieChart>
            </ResponsiveContainer>
            <div className="mt-3 space-y-2">
              {data.bySpecialty.map((s, i) => {
                const pct = data.totalCollected > 0 ? Math.round((s.total / data.totalCollected) * 100) : 0;
                return (
                  <div key={s.specialty} className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-1.5">
                      <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: SPECIALTY_COLORS[i % SPECIALTY_COLORS.length] }} />
                      <span className="text-gray-600">{SPECIALTY_LABELS[s.specialty] ?? s.specialty}</span>
                    </div>
                    <span className="font-semibold text-gray-900">{formatYemeniRiyal(s.total)} ({pct}%)</span>
                  </div>
                );
              })}
            </div>
          </ChartCard>
        )}

        {data.byMethod.length > 0 && (
          <ChartCard title="حسب طريقة الدفع">
            <div className="space-y-3">
              {data.byMethod.map((m) => {
                const pct = data.totalCollected > 0 ? Math.round((m.total / data.totalCollected) * 100) : 0;
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
          </ChartCard>
        )}
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: مقارنة الإيرادات (Revenue Comparison)
   ═══════════════════════════════════════════════════════════════════════════ */

function RevenueComparisonReport({ data }: { data: RevenueComparison }) {
  const isPositive = data.percentageChange >= 0;
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="الفترة الحالية" value={formatYemeniRiyal(data.currentPeriodTotal)} icon={Wallet} color="bg-emerald-50 text-emerald-600 border-emerald-200" />
        <StatCard label="الفترة السابقة" value={formatYemeniRiyal(data.previousPeriodTotal)} icon={Wallet} color="bg-gray-50 text-gray-600 border-gray-200" />
        <StatCard label="نسبة التغيير" value={formatPct(Math.abs(data.percentageChange))}
          icon={isPositive ? TrendingUp : TrendingUp}
          color={isPositive ? "bg-green-50 text-green-600 border-green-200" : "bg-red-50 text-red-600 border-red-200"} />
      </div>

      {data.monthlyComparison.length > 0 && (
        <ChartCard title="المقارنة الشهرية" icon={<TrendingUp className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={data.monthlyComparison} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="month" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <Tooltip formatter={(v, n) => [`${Number(v).toLocaleString()} ر.ي`, n === "currentPeriod" ? "الحالية" : "السابقة"]} />
              <Bar dataKey="currentPeriod" fill="#3d7ab5" name="الحالية" radius={[3, 3, 0, 0]} maxBarSize={24} />
              <Bar dataKey="previousPeriod" fill="#d1d5db" name="السابقة" radius={[3, 3, 0, 0]} maxBarSize={24} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.specialtyComparison.length > 0 && (
        <ChartCard title="مقارنة حسب التخصص" icon={<Stethoscope className="w-4 h-4 text-clinic-orange" />}>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={data.specialtyComparison} layout="vertical" margin={{ top: 0, right: 20, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis type="number" tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} />
              <YAxis type="category" dataKey="specialty" tick={{ fontSize: 10, fill: "#6B7280" }} width={60}
                tickFormatter={(v: string) => SPECIALTY_LABELS[v] ?? v} />
              <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, ""]} />
              <Bar dataKey="currentPeriod" fill="#3d7ab5" radius={[0, 3, 3, 0]} maxBarSize={16} />
              <Bar dataKey="previousPeriod" fill="#d1d5db" radius={[0, 3, 3, 0]} maxBarSize={16} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: العقود المتأخرة (Overdue Contracts)
   ═══════════════════════════════════════════════════════════════════════════ */

function OverdueContractsReport({ data }: { data: OverdueContracts }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي المتأخرات" value={formatYemeniRiyal(data.totalOverdueAmount)} icon={Wallet} color="bg-red-50 text-red-600 border-red-200" />
        <StatCard label="عدد العقود المتأخرة" value={data.overdueContracts.length.toString()} icon={FileSpreadsheet} color="bg-orange-50 text-orange-600 border-orange-200" />
      </div>

      {data.agingBuckets.length > 0 && (
        <ChartCard title="توزيع تقادم الديون" icon={<Clock className="w-4 h-4 text-red-500" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.agingBuckets} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="bucket" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <Tooltip content={<TooltipRevenue />} />
              <Bar dataKey="amount" fill="#ef4444" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.overdueContracts.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-gray-100">
            <h3 className="font-bold text-gray-900 text-sm">تفاصيل العقود المتأخرة</h3>
          </div>
          <div className="overflow-x-auto max-h-96 overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200 sticky top-0">
                <tr>
                  {["المريض", "المبلغ الإجمالي", "المدفوع", "المتبقي", "أيام التأخير"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.overdueContracts.map((c) => (
                  <tr key={c.contractId} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-medium text-gray-900">{c.patientName}</td>
                    <td className="px-4 py-3 font-mono">{formatYemeniRiyal(c.totalAmount)}</td>
                    <td className="px-4 py-3 font-mono text-green-700">{formatYemeniRiyal(c.paidAmount)}</td>
                    <td className="px-4 py-3 font-mono font-semibold text-red-700">{formatYemeniRiyal(c.remaining)}</td>
                    <td className="px-4 py-3 font-mono text-red-600">{c.daysOverdue} يوم</td>
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

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: أداء الأطباء (Doctor Performance)
   ═══════════════════════════════════════════════════════════════════════════ */

function DoctorPerformanceReport({ data }: { data: DoctorPerformance[] }) {
  return (
    <div className="space-y-4">
      <ChartCard title="الإيرادات حسب الطبيب" icon={<Wallet className="w-4 h-4 text-emerald-600" />}>
        <ResponsiveContainer width="100%" height={180}>
          <BarChart data={data} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
            <XAxis dataKey="name" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
            <YAxis tickFormatter={formatYER} tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
            <Tooltip formatter={(v) => [`${Number(v).toLocaleString()} ر.ي`, "الإيرادات"]} />
            <Bar dataKey="revenue" fill="#3d7ab5" radius={[4, 4, 0, 0]} maxBarSize={32} />
          </BarChart>
        </ResponsiveContainer>
      </ChartCard>

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
              {data.map((p) => (
                <tr key={p.doctorId} className="hover:bg-gray-50 transition">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <div className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: p.color ?? "#3d7ab5" }} />
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
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: حالات التقويم (Ortho Progress)
   ═══════════════════════════════════════════════════════════════════════════ */

function OrthoProgressReport({ data }: { data: OrthoProgress }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إيرادات التقويم" value={formatYemeniRiyal(data.orthoRevenue)} icon={Wallet} color="bg-emerald-50 text-emerald-600 border-emerald-200" />
        <StatCard label="متوسط مدة العلاج" value={`${data.averageTreatmentDurationMonths} شهر`} icon={Clock} color="bg-blue-50 text-blue-600 border-blue-200" />
        <StatCard label="إجمالي الحالات" value={data.casesByStatus.reduce((a, c) => a + c.count, 0).toString()} icon={Activity} color="bg-purple-50 text-purple-600 border-purple-200" />
      </div>

      {data.casesByStatus.length > 0 && (
        <ChartCard title="الحالات حسب الحالة" icon={<Activity className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={200}>
            <PieChart>
              <Pie data={data.casesByStatus} cx="50%" cy="50%" innerRadius={50} outerRadius={80}
                paddingAngle={3} dataKey="count" nameKey="status">
                {data.casesByStatus.map((_, i) => (
                  <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                ))}
              </Pie>
              <Tooltip formatter={(v) => [v, ""]} />
            </PieChart>
          </ResponsiveContainer>
          <div className="mt-3 flex flex-wrap gap-3">
            {data.casesByStatus.map((s, i) => (
              <div key={s.status} className="flex items-center gap-1.5 text-xs">
                <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: CHART_COLORS[i % CHART_COLORS.length] }} />
                <span className="text-gray-600">{STATUS_LABELS[s.status] ?? s.status}</span>
                <span className="font-semibold">{s.count}</span>
              </div>
            ))}
          </div>
        </ChartCard>
      )}

      {data.casesPerDoctor.length > 0 && (
        <ChartCard title="الحالات حسب الطبيب" icon={<Stethoscope className="w-4 h-4 text-clinic-orange" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.casesPerDoctor} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="doctorName" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="حالة" />} />
              <Bar dataKey="count" fill="#f5922e" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: إحصائيات الجراحة (Surgery Stats)
   ═══════════════════════════════════════════════════════════════════════════ */

function SurgeryStatsReport({ data }: { data: SurgeryStats }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي العمليات" value={data.casesByType.reduce((a, c) => a + c.count, 0).toString()} icon={Scissors} color="bg-blue-50 text-blue-600 border-blue-200" />
      </div>

      {data.casesByType.length > 0 && (
        <ChartCard title="حسب نوع العملية" icon={<Scissors className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={data.casesByType} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="type" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="عملية" />} />
              <Bar dataKey="count" fill="#3d7ab5" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.casesPerDoctor.length > 0 && (
        <ChartCard title="العمليات حسب الطبيب" icon={<Stethoscope className="w-4 h-4 text-clinic-orange" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.casesPerDoctor} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="doctorName" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="عملية" />} />
              <Bar dataKey="count" fill="#f5922e" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.monthlyTrend.length > 0 && (
        <ChartCard title="الاتجاه الشهري" icon={<TrendingUp className="w-4 h-4 text-emerald-600" />}>
          <ResponsiveContainer width="100%" height={180}>
            <LineChart data={data.monthlyTrend} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="month" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="عملية" />} />
              <Line type="monotone" dataKey="count" stroke="#10b981" strokeWidth={2} dot={{ r: 3 }} activeDot={{ r: 5 }} />
            </LineChart>
          </ResponsiveContainer>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: خطط العلاج (Treatment Plan Completion)
   ═══════════════════════════════════════════════════════════════════════════ */

function TreatmentPlanCompletionReport({ data }: { data: TreatmentPlanCompletion }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="نسبة الإكمال" value={formatPct(data.completionRate)} icon={ClipboardList} color="bg-green-50 text-green-600 border-green-200" />
        <StatCard label="إجمالي الخطوات" value={data.stepsByStatus.reduce((a, s) => a + s.count, 0).toString()} icon={Activity} color="bg-blue-50 text-blue-600 border-blue-200" />
      </div>

      {data.stepsByStatus.length > 0 && (
        <ChartCard title="الخطوات حسب الحالة" icon={<ClipboardList className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={200}>
            <PieChart>
              <Pie data={data.stepsByStatus} cx="50%" cy="50%" innerRadius={50} outerRadius={80}
                paddingAngle={3} dataKey="count" nameKey="status">
                {data.stepsByStatus.map((_, i) => (
                  <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                ))}
              </Pie>
              <Tooltip formatter={(v) => [v, ""]} />
            </PieChart>
          </ResponsiveContainer>
          <div className="mt-3 flex flex-wrap gap-3">
            {data.stepsByStatus.map((s, i) => (
              <div key={s.status} className="flex items-center gap-1.5 text-xs">
                <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: CHART_COLORS[i % CHART_COLORS.length] }} />
                <span className="text-gray-600">{STATUS_LABELS[s.status] ?? s.status}</span>
                <span className="font-semibold">{s.count}</span>
              </div>
            ))}
          </div>
        </ChartCard>
      )}

      {data.perDoctor.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-gray-100">
            <h3 className="font-bold text-gray-900 text-sm">التفصيل حسب الطبيب</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["الطبيب", "الإجمالي", "المكتمل", "نسبة الإكمال"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.perDoctor.map((d) => (
                  <tr key={d.doctorId} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 font-medium text-gray-900">{d.doctorName}</td>
                    <td className="px-4 py-3 font-mono text-gray-700">{d.totalSteps}</td>
                    <td className="px-4 py-3 font-mono text-green-700">{d.completedSteps}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden max-w-20">
                          <div className="h-full bg-green-500 rounded-full" style={{ width: `${d.completionRate}%` }} />
                        </div>
                        <span className="font-mono text-sm font-semibold text-green-700">{formatPct(d.completionRate)}</span>
                      </div>
                    </td>
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

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: طلبات المختبر (Lab Orders)
   ═══════════════════════════════════════════════════════════════════════════ */

function LabOrdersReport({ data }: { data: LabOrders }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="طلبات متأخرة" value={data.overdueOrdersCount.toString()} icon={Clock} color="bg-red-50 text-red-600 border-red-200" />
        <StatCard label="إجمالي الطلبات" value={data.ordersByStatus.reduce((a, s) => a + s.count, 0).toString()} icon={FileSpreadsheet} color="bg-blue-50 text-blue-600 border-blue-200" />
      </div>

      {data.ordersByStatus.length > 0 && (
        <ChartCard title="الطلبات حسب الحالة" icon={<FileSpreadsheet className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={200}>
            <PieChart>
              <Pie data={data.ordersByStatus} cx="50%" cy="50%" innerRadius={50} outerRadius={80}
                paddingAngle={3} dataKey="count" nameKey="status">
                {data.ordersByStatus.map((_, i) => (
                  <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                ))}
              </Pie>
              <Tooltip formatter={(v) => [v, ""]} />
            </PieChart>
          </ResponsiveContainer>
          <div className="mt-3 flex flex-wrap gap-3">
            {data.ordersByStatus.map((s, i) => (
              <div key={s.status} className="flex items-center gap-1.5 text-xs">
                <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: CHART_COLORS[i % CHART_COLORS.length] }} />
                <span className="text-gray-600">{STATUS_LABELS[s.status] ?? s.status}</span>
                <span className="font-semibold">{s.count}</span>
              </div>
            ))}
          </div>
        </ChartCard>
      )}

      {data.perDoctor.length > 0 && (
        <ChartCard title="الطلبات حسب الطبيب" icon={<Stethoscope className="w-4 h-4 text-clinic-orange" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.perDoctor} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="doctorName" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="طلب" />} />
              <Bar dataKey="count" fill="#f5922e" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: الوصفات الطبية (Prescription Analytics)
   ═══════════════════════════════════════════════════════════════════════════ */

function PrescriptionAnalyticsReport({ data }: { data: PrescriptionAnalytics }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="إجمالي الوصفات" value={data.monthlyTrend.reduce((a, m) => a + m.count, 0).toString()} icon={Pill} color="bg-blue-50 text-blue-600 border-blue-200" />
      </div>

      {data.mostPrescribed.length > 0 && (
        <ChartCard title="الأدوية الأكثر وصفاً" icon={<Pill className="w-4 h-4 text-clinic-blue" />}>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={data.mostPrescribed} layout="vertical" margin={{ top: 0, right: 20, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis type="number" tick={{ fontSize: 9, fill: "#9CA3AF" }} allowDecimals={false} />
              <YAxis type="category" dataKey="drugName" tick={{ fontSize: 10, fill: "#6B7280" }} width={100} />
              <Tooltip content={<TooltipCount unit="وصفة" />} />
              <Bar dataKey="count" fill="#3d7ab5" radius={[0, 3, 3, 0]} maxBarSize={20} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.perDoctor.length > 0 && (
        <ChartCard title="الوصفات حسب الطبيب" icon={<Stethoscope className="w-4 h-4 text-clinic-orange" />}>
          <ResponsiveContainer width="100%" height={180}>
            <BarChart data={data.perDoctor} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="doctorName" tick={{ fontSize: 10, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="وصفة" />} />
              <Bar dataKey="count" fill="#f5922e" radius={[4, 4, 0, 0]} maxBarSize={32} />
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      )}

      {data.monthlyTrend.length > 0 && (
        <ChartCard title="الاتجاه الشهري" icon={<TrendingUp className="w-4 h-4 text-emerald-600" />}>
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={data.monthlyTrend} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
              <XAxis dataKey="month" tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} />
              <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
              <Tooltip content={<TooltipCount unit="وصفة" />} />
              <Area type="monotone" dataKey="count" stroke="#10b981" fill="#d1fae5" strokeWidth={2} />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: احتفاظ المرضى (Patient Retention)
   ═══════════════════════════════════════════════════════════════════════════ */

function PatientRetentionReport({ data }: { data: PatientRetention }) {
  const total = data.newPatients + data.returningPatients;
  const returningPct = total > 0 ? Math.round((data.returningPatients / total) * 100) : 0;
  const newPct = total > 0 ? 100 - returningPct : 0;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="المرضى الجدد" value={data.newPatients.toString()} icon={UserCheck} color="bg-blue-50 text-blue-600 border-blue-200" />
        <StatCard label="المرضى العائدون" value={data.returningPatients.toString()} icon={Heart} color="bg-green-50 text-green-600 border-green-200" />
        <StatCard label="متوسط أيام بين الزيارات" value={`${data.averageDaysBetweenVisits}`} icon={Clock} color="bg-purple-50 text-purple-600 border-purple-200" />
        <StatCard label="خطر فقدان العملاء" value={data.churnRiskPatients.toString()} icon={TrendingUp} color="bg-red-50 text-red-600 border-red-200" />
      </div>

      <ChartCard title="توزيع المرضى" icon={<Users className="w-4 h-4 text-clinic-blue" />}>
        <div className="flex items-center gap-6">
          <ResponsiveContainer width={180} height={180}>
            <PieChart>
              <Pie
                data={[
                  { name: "جدد", value: data.newPatients },
                  { name: "عائدون", value: data.returningPatients },
                ]}
                cx="50%" cy="50%" innerRadius={50} outerRadius={80}
                paddingAngle={3} dataKey="value"
              >
                <Cell fill="#3d7ab5" />
                <Cell fill="#10b981" />
              </Pie>
              <Tooltip />
            </PieChart>
          </ResponsiveContainer>
          <div className="space-y-4">
            <div>
              <div className="flex items-center gap-2 text-sm mb-1">
                <span className="w-3 h-3 rounded-full bg-clinic-blue" />
                <span className="text-gray-600">مرضى جدد</span>
              </div>
              <p className="text-2xl font-extrabold font-mono text-clinic-blue">{data.newPatients}</p>
              <p className="text-xs text-gray-400">{newPct}% من الإجمالي</p>
            </div>
            <div>
              <div className="flex items-center gap-2 text-sm mb-1">
                <span className="w-3 h-3 rounded-full bg-emerald-500" />
                <span className="text-gray-600">مرضى عائدون</span>
              </div>
              <p className="text-2xl font-extrabold font-mono text-emerald-600">{data.returningPatients}</p>
              <p className="text-xs text-gray-400">{returningPct}% من الإجمالي</p>
            </div>
          </div>
        </div>
      </ChartCard>

      {data.churnRiskPatients > 0 && (
        <div className="bg-red-50 border border-red-200 rounded-xl p-4">
          <div className="flex items-center gap-2 mb-2">
            <TrendingUp className="w-5 h-5 text-red-600" />
            <p className="text-sm font-bold text-red-700">تنبيه: {data.churnRiskPatients} مريض معرضون للمغادرة</p>
          </div>
          <p className="text-xs text-red-600">هؤلاء المرضى لم يزوروا المركز منذ فترة طويلة. يُنصح بالتواصل معهم.</p>
        </div>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   REPORT: قمع الحجوزات (Booking Funnel)
   ═══════════════════════════════════════════════════════════════════════════ */

function BookingFunnelReport({ data }: { data: BookingFunnel }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        <StatCard label="نسبة التحويل" value={formatPct(data.conversionRate)} icon={TrendingUp} color="bg-green-50 text-green-600 border-green-200" />
        <StatCard label="نسبة إنشاء المواعيد" value={formatPct(data.appointmentCreationRate)} icon={Calendar} color="bg-blue-50 text-blue-600 border-blue-200" />
      </div>

      {data.byStatus.length > 0 && (
        <ChartCard title="قمع الحجوزات" icon={<PieChartIcon className="w-4 h-4 text-clinic-blue" />}>
          <div className="space-y-3">
            {data.byStatus.map((s, i) => {
              const maxCount = Math.max(...data.byStatus.map((b) => b.count));
              const widthPct = maxCount > 0 ? Math.round((s.count / maxCount) * 100) : 0;
              return (
                <div key={s.status} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-700 font-medium">{STATUS_LABELS[s.status] ?? s.status}</span>
                    <span className="font-mono font-semibold text-gray-900">{s.count}</span>
                  </div>
                  <div className="h-8 bg-gray-50 rounded-lg overflow-hidden flex items-center">
                    <div
                      className="h-full flex items-center justify-start px-3 rounded-lg transition-all"
                      style={{ width: `${widthPct}%`, backgroundColor: CHART_COLORS[i % CHART_COLORS.length] + "22", borderRight: `3px solid ${CHART_COLORS[i % CHART_COLORS.length]}` }}
                    >
                      <span className="text-xs font-bold" style={{ color: CHART_COLORS[i % CHART_COLORS.length] }}>
                        {widthPct}%
                      </span>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </ChartCard>
      )}

      {data.perService.length > 0 && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-gray-100">
            <h3 className="font-bold text-gray-900 text-sm">التفصيل حسب الخدمة</h3>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  {["الخدمة", "الإجمالي", "المؤكد", "المحول", "نسبة التحويل"].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.perService.map((s) => {
                  const convRate = s.confirmed > 0 ? Math.round((s.converted / s.confirmed) * 100) : 0;
                  return (
                    <tr key={s.service} className="hover:bg-gray-50 transition">
                      <td className="px-4 py-3 font-medium text-gray-900">{s.service}</td>
                      <td className="px-4 py-3 font-mono text-gray-700">{s.total}</td>
                      <td className="px-4 py-3 font-mono text-green-700">{s.confirmed}</td>
                      <td className="px-4 py-3 font-mono text-blue-700">{s.converted}</td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden max-w-20">
                            <div className="h-full bg-green-500 rounded-full" style={{ width: `${convRate}%` }} />
                          </div>
                          <span className="font-mono text-sm font-semibold text-green-700">{formatPct(convRate)}</span>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   MAIN PAGE
   ═══════════════════════════════════════════════════════════════════════════ */

export default function ReportsPage() {
  const [activeReport, setActiveReport] = useState<ReportKey>("center-summary");
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [exporting, setExporting] = useState(false);
  const today = localDateString();
  const monthAgo = localDateString(new Date(Date.now() - 30 * 86400000));
  const [from, setFrom] = useState(monthAgo);
  const [to, setTo] = useState(today);

  /* ─── Data states ────────────────────────────────────────────────────── */
  const [centerSummary, setCenterSummary] = useState<CenterSummary | null>(null);
  const [appointmentAnalytics, setAppointmentAnalytics] = useState<AppointmentAnalytics | null>(null);
  const [patientDemographics, setPatientDemographics] = useState<PatientDemographics | null>(null);
  const [financial, setFinancial] = useState<FinancialReport | null>(null);
  const [revenueComparison, setRevenueComparison] = useState<RevenueComparison | null>(null);
  const [overdueContracts, setOverdueContracts] = useState<OverdueContracts | null>(null);
  const [doctorPerformance, setDoctorPerformance] = useState<DoctorPerformance[]>([]);
  const [orthoProgress, setOrthoProgress] = useState<OrthoProgress | null>(null);
  const [surgeryStats, setSurgeryStats] = useState<SurgeryStats | null>(null);
  const [treatmentPlanCompletion, setTreatmentPlanCompletion] = useState<TreatmentPlanCompletion | null>(null);
  const [labOrders, setLabOrders] = useState<LabOrders | null>(null);
  const [prescriptionAnalytics, setPrescriptionAnalytics] = useState<PrescriptionAnalytics | null>(null);
  const [patientRetention, setPatientRetention] = useState<PatientRetention | null>(null);
  const [bookingFunnel, setBookingFunnel] = useState<BookingFunnel | null>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  /* ─── Data fetcher ───────────────────────────────────────────────────── */
  const fetchData = useCallback(() => {
    setLoading(true);
    setError(false);
    const params = `?from=${from}&to=${to}`;

    const endpointMap: Record<ReportKey, string> = {
      "center-summary": "/api/reports/center-summary",
      "appointment-analytics": "/api/reports/appointment-analytics",
      "patient-demographics": "/api/reports/patient-demographics",
      "financial": "/api/reports/financial",
      "revenue-comparison": "/api/reports/revenue-comparison",
      "overdue-contracts": "/api/reports/overdue-contracts",
      "doctor-performance": "/api/reports/doctor-performance",
      "ortho-progress": "/api/reports/ortho-progress",
      "surgery-stats": "/api/reports/surgery-stats",
      "treatment-plan-completion": "/api/reports/treatment-plan-completion",
      "lab-orders": "/api/reports/lab-orders",
      "prescription-analytics": "/api/reports/prescription-analytics",
      "patient-retention": "/api/reports/patient-retention",
      "booking-funnel": "/api/reports/booking-funnel",
    };

    const setterMap: Record<ReportKey, (data: unknown) => void> = {
      "center-summary": (d) => setCenterSummary(d as CenterSummary),
      "appointment-analytics": (d) => setAppointmentAnalytics(d as AppointmentAnalytics),
      "patient-demographics": (d) => setPatientDemographics(d as PatientDemographics),
      "financial": (d) => setFinancial(d as FinancialReport),
      "revenue-comparison": (d) => setRevenueComparison(d as RevenueComparison),
      "overdue-contracts": (d) => setOverdueContracts(d as OverdueContracts),
      "doctor-performance": (d) => setDoctorPerformance(d as DoctorPerformance[]),
      "ortho-progress": (d) => setOrthoProgress(d as OrthoProgress),
      "surgery-stats": (d) => setSurgeryStats(d as SurgeryStats),
      "treatment-plan-completion": (d) => setTreatmentPlanCompletion(d as TreatmentPlanCompletion),
      "lab-orders": (d) => setLabOrders(d as LabOrders),
      "prescription-analytics": (d) => setPrescriptionAnalytics(d as PrescriptionAnalytics),
      "patient-retention": (d) => setPatientRetention(d as PatientRetention),
      "booking-funnel": (d) => setBookingFunnel(d as BookingFunnel),
    };

    const endpoint = endpointMap[activeReport];
    const setter = setterMap[activeReport];

    api.get(endpoint + params)
      .then((r) => setter(r.data))
      .catch(() => {
        setError(true);
        toast.error("فشل تحميل البيانات — تحقق من الاتصال وحاول مجدداً");
      })
      .finally(() => setLoading(false));
  }, [activeReport, from, to]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  /* ─── CSV Export ─────────────────────────────────────────────────────── */
  const handleExport = async () => {
    setExporting(true);
    try {
      // FE-16: Use the downloadBlob helper instead of fetch() so the JWT is injected
      // automatically and the 401 refresh interceptor works (previously this used a
      // manual Authorization header that silently failed when the token expired).
      const exportEndpoints: Record<ReportKey, string> = {
        "center-summary": "/api/reports/export/center-summary",
        "appointment-analytics": "/api/reports/export/appointment-analytics",
        "patient-demographics": "/api/reports/export/patient-demographics",
        "financial": "/api/reports/export/payments",
        "revenue-comparison": "/api/reports/export/revenue-comparison",
        "overdue-contracts": "/api/reports/export/overdue-contracts",
        "doctor-performance": "/api/reports/export/doctor-performance",
        "ortho-progress": "/api/reports/export/ortho-progress",
        "surgery-stats": "/api/reports/export/surgery-stats",
        "treatment-plan-completion": "/api/reports/export/treatment-plan-completion",
        "lab-orders": "/api/reports/export/lab-orders",
        "prescription-analytics": "/api/reports/export/prescription-analytics",
        "patient-retention": "/api/reports/export/patient-retention",
        "booking-funnel": "/api/reports/export/booking-funnel",
      };
      const blob = await downloadBlob(exportEndpoints[activeReport], { from, to });
      const blobUrl = URL.createObjectURL(blob);
      downloadCsv(blobUrl, `${activeReport}_${today}.csv`);
      URL.revokeObjectURL(blobUrl);
    } catch {
      toast.error("فشل تصدير البيانات — تحقق من الاتصال وحاول مجدداً");
    } finally {
      setExporting(false);
    }
  };

  /* ─── Find current report info ───────────────────────────────────────── */
  const currentReportInfo = REPORT_CATEGORIES
    .flatMap((c) => c.reports)
    .find((r) => r.key === activeReport);

  /* ─── Render content ─────────────────────────────────────────────────── */
  const renderReport = () => {
    if (loading) return <ReportSkeleton />;
    if (error) return <ErrorState onRetry={fetchData} />;

    switch (activeReport) {
      case "center-summary":
        return centerSummary ? <CenterSummaryReport data={centerSummary} /> : <EmptyState />;
      case "appointment-analytics":
        return appointmentAnalytics ? <AppointmentAnalyticsReport data={appointmentAnalytics} /> : <EmptyState />;
      case "patient-demographics":
        return patientDemographics ? <PatientDemographicsReport data={patientDemographics} /> : <EmptyState />;
      case "financial":
        return financial ? <FinancialReportView data={financial} /> : <EmptyState />;
      case "revenue-comparison":
        return revenueComparison ? <RevenueComparisonReport data={revenueComparison} /> : <EmptyState />;
      case "overdue-contracts":
        return overdueContracts ? <OverdueContractsReport data={overdueContracts} /> : <EmptyState />;
      case "doctor-performance":
        return doctorPerformance.length > 0 ? <DoctorPerformanceReport data={doctorPerformance} /> : <EmptyState />;
      case "ortho-progress":
        return orthoProgress ? <OrthoProgressReport data={orthoProgress} /> : <EmptyState />;
      case "surgery-stats":
        return surgeryStats ? <SurgeryStatsReport data={surgeryStats} /> : <EmptyState />;
      case "treatment-plan-completion":
        return treatmentPlanCompletion ? <TreatmentPlanCompletionReport data={treatmentPlanCompletion} /> : <EmptyState />;
      case "lab-orders":
        return labOrders ? <LabOrdersReport data={labOrders} /> : <EmptyState />;
      case "prescription-analytics":
        return prescriptionAnalytics ? <PrescriptionAnalyticsReport data={prescriptionAnalytics} /> : <EmptyState />;
      case "patient-retention":
        return patientRetention ? <PatientRetentionReport data={patientRetention} /> : <EmptyState />;
      case "booking-funnel":
        return bookingFunnel ? <BookingFunnelReport data={bookingFunnel} /> : <EmptyState />;
      default:
        return <EmptyState />;
    }
  };

  return (
    <div className="max-w-7xl">
      {/* Header */}
      <div className="mb-5">
        <h1 className="text-2xl font-extrabold text-gray-900">التقارير</h1>
        <p className="text-sm text-gray-500 mt-0.5">التقارير والإحصائيات التفصيلية</p>
      </div>

      <div className="flex gap-5">
        {/* ─── Sidebar ────────────────────────────────────────────────── */}
        {/* Mobile overlay */}
        {sidebarOpen && (
          <div className="fixed inset-0 z-40 bg-black/30 lg:hidden" onClick={() => setSidebarOpen(false)} />
        )}

        <aside className={`
          fixed top-0 right-0 z-50 h-full w-64 bg-white border-l border-gray-200 shadow-lg
          transform transition-transform duration-200 lg:relative lg:transform-none lg:shadow-none
          lg:border-0 lg:bg-transparent lg:w-56 lg:flex-shrink-0
          ${sidebarOpen ? "translate-x-0" : "translate-x-full lg:translate-x-0"}
        `}>
          {/* Mobile header */}
          <div className="flex items-center justify-between p-4 border-b border-gray-100 lg:hidden">
            <span className="font-bold text-gray-900">التقارير</span>
            <button onClick={() => setSidebarOpen(false)} className="p-1 hover:bg-gray-100 rounded-lg">
              <ChevronRight className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          <nav className="p-3 space-y-1 lg:p-0">
            {REPORT_CATEGORIES.map((cat) => (
              <div key={cat.key} className="mb-3">
                <div className="flex items-center gap-2 px-3 py-2 text-xs font-bold text-gray-400 uppercase tracking-wide">
                  <cat.icon className="w-3.5 h-3.5" />
                  {cat.label}
                </div>
                {cat.reports.map((report) => {
                  const isActive = activeReport === report.key;
                  return (
                    <button
                      key={report.key}
                      onClick={() => {
                        setActiveReport(report.key);
                        setSidebarOpen(false);
                      }}
                      className={`w-full flex items-center gap-2.5 px-3 py-2.5 text-sm rounded-lg transition text-start ${
                        isActive
                          ? "bg-clinic-blue-50 text-clinic-blue font-semibold"
                          : "text-gray-600 hover:bg-gray-50 hover:text-gray-900"
                      }`}
                    >
                      <report.icon className={`w-4 h-4 flex-shrink-0 ${isActive ? "text-clinic-blue" : "text-gray-400"}`} />
                      {report.label}
                    </button>
                  );
                })}
              </div>
            ))}

            {/* صفحة مستقلة: تقرير العمليات والزحمة */}
            <div className="mb-3">
              <div className="flex items-center gap-2 px-3 py-2 text-xs font-bold text-gray-400 uppercase tracking-wide">
                <Activity className="w-3.5 h-3.5" />
                العمليات
              </div>
              <Link
                href="/reports/operations"
                onClick={() => setSidebarOpen(false)}
                className="w-full flex items-center gap-2.5 px-3 py-2.5 text-sm rounded-lg transition text-start text-gray-600 hover:bg-gray-50 hover:text-gray-900"
              >
                <Clock className="w-4 h-4 flex-shrink-0 text-gray-400" />
                تقرير العمليات والزحمة
              </Link>
            </div>
          </nav>
        </aside>

        {/* ─── Main content ─────────────────────────────────────────── */}
        <main className="flex-1 min-w-0 space-y-4">
          {/* Toolbar */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-3 flex items-center gap-2 flex-wrap">
            {/* Mobile menu toggle */}
            <button
              onClick={() => setSidebarOpen(true)}
              className="lg:hidden flex items-center gap-1 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50 rounded-lg border border-gray-200"
            >
              <ChevronLeft className="w-4 h-4" />
              التقارير
            </button>

            {/* Report title */}
            {currentReportInfo && (
              <div className="hidden sm:flex items-center gap-2 ms-1">
                <currentReportInfo.icon className="w-4 h-4 text-clinic-blue" />
                <span className="text-sm font-semibold text-gray-900">{currentReportInfo.label}</span>
              </div>
            )}

            {/* Date range */}
            <div className="flex items-center gap-2 md:ms-auto flex-wrap">
              <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
                className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-clinic-blue" />
              <span className="text-gray-400">—</span>
              <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
                className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-clinic-blue" />
            </div>

            {/* Export */}
            <button
              onClick={handleExport}
              disabled={exporting}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-50 transition ms-1"
            >
              <Download className="w-3.5 h-3.5" />
              {exporting ? "جارٍ التصدير..." : "تصدير CSV"}
            </button>
          </div>

          {/* Report description */}
          {currentReportInfo && (
            <p className="text-xs text-gray-400">{currentReportInfo.desc}</p>
          )}

          {/* Report content */}
          {renderReport()}
        </main>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   EMPTY STATE
   ═══════════════════════════════════════════════════════════════════════════ */
function EmptyState() {
  return (
    <div className="text-center py-16 text-gray-400 text-sm">لا توجد بيانات للعرض</div>
  );
}
