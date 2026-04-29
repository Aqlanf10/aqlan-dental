"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Users, Calendar, TrendingUp, Smile,
  UserPlus, FileText, Clock, Stethoscope,
} from "lucide-react";
import { StatsCard } from "@/components/dashboard/StatsCard";
import { DashboardCharts } from "@/components/dashboard/DashboardCharts";
import { TodaySchedule } from "@/components/dashboard/TodaySchedule";
import type { DashboardStats } from "@/types/dashboard";
import api from "@/lib/api";

/* ─── Quick Action Card ─── */
function QuickAction({
  icon: Icon,
  label,
  color,
  href,
}: {
  icon: React.ElementType;
  label: string;
  color: string;
  href: string;
}) {
  return (
    <Link
      href={href}
      className="flex flex-col items-center justify-center gap-2 rounded-xl p-4 border border-dashed transition-all hover:shadow-md hover:scale-[1.02]"
      style={{
        background: color + "10",
        borderColor: color + "40",
      }}
    >
      <div
        className="w-10 h-10 rounded-lg flex items-center justify-center"
        style={{ background: color + "20" }}
      >
        <Icon className="w-5 h-5" style={{ color }} />
      </div>
      <span className="text-xs font-semibold text-[#0d2137]">{label}</span>
    </Link>
  );
}

/* ─── Doctor Performance Row ─── */
function DoctorPerformance({
  name,
  patients,
  maxPatients,
  color,
}: {
  name: string;
  patients: number;
  maxPatients: number;
  color: string;
}) {
  const pct = maxPatients > 0 ? Math.round((patients / maxPatients) * 100) : 0;
  return (
    <div className="flex items-center gap-3">
      <span className="text-xs font-medium text-[#0d2137] w-28 flex-shrink-0 truncate">{name}</span>
      <div className="flex-1 h-2 bg-[#f1f5f9] rounded-full overflow-hidden">
        <div
          className="h-full rounded-full"
          style={{
            width: `${pct}%`,
            background: `linear-gradient(90deg, ${color}, ${color}cc)`,
          }}
        />
      </div>
      <span className="text-xs font-semibold text-[#64748b] w-8 text-left flex-shrink-0">{patients}</span>
    </div>
  );
}

/* ─── Dashboard Page ─── */
export default function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<DashboardStats>("/api/dashboard/stats")
      .then((r) => setStats(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const today = new Intl.DateTimeFormat("ar-YE", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(new Date());

  /* Sample doctor performance data */
  const doctors = [
    { name: "د. عقلان الكامل", patients: 18, color: "#3d7ab5" },
    { name: "د. عائشة غازي", patients: 12, color: "#a855f7" },
    { name: "د. إيمان الكامل", patients: 8, color: "#22c55e" },
    { name: "د. هشام القدسي", patients: 6, color: "#f5922e" },
  ];
  const maxPatients = Math.max(...doctors.map((d) => d.patients), 1);

  /* Recent patients sample (fallback when API data not available) */
  const recentPatients = [
    { name: "أحمد محمد الشميري", id: "p1", date: "اليوم" },
    { name: "فاطمة علي السقاف", id: "p2", date: "اليوم" },
    { name: "خالد عبدالله نعمان", id: "p3", date: "أمس" },
    { name: "سارة محمد الحميدي", id: "p4", date: "أمس" },
  ];

  return (
    <div className="space-y-4" style={{ background: "#eef3f9", margin: -24, padding: 24, minHeight: "calc(100vh - 64px)" }}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-[22px] font-extrabold text-[#0d2137]">لوحة التحكم</h1>
          <p className="text-sm text-[#64748b] mt-0.5">{today}</p>
        </div>
      </div>

      {/* Stats grid — 4 cards */}
      <div className="flex gap-[14px] overflow-x-auto pb-1">
        <div className="flex-1 min-w-[200px]">
          <StatsCard
            title="إجمالي المرضى"
            value={loading ? "—" : (stats?.appointmentsToday ?? 1248)}
            icon={Users}
            color="#3d7ab5"
            description="مريض مسجّل"
          />
        </div>
        <div className="flex-1 min-w-[200px]">
          <StatsCard
            title="مواعيد اليوم"
            value={loading ? "—" : (stats?.appointmentsToday ?? 0)}
            icon={Calendar}
            color="#f5922e"
            description="موعد مجدول"
          />
        </div>
        <div className="flex-1 min-w-[200px]">
          <StatsCard
            title="الإيرادات الشهرية"
            value={loading ? "—" : (stats ? `${stats.totalRevenueMTD.toLocaleString()} ر.ي` : "0 ر.ي")}
            icon={TrendingUp}
            color="#22c55e"
            description="المحصّل هذا الشهر"
          />
        </div>
        <div className="flex-1 min-w-[200px]">
          <StatsCard
            title="حالات التقويم"
            value={loading ? "—" : (stats?.activeOrthoCases ?? 0)}
            icon={Smile}
            color="#a855f7"
            description="حالة نشطة"
          />
        </div>
      </div>

      {/* Main grid: Left content + Right sidebar */}
      <div className="grid gap-4" style={{ gridTemplateColumns: "1fr 380px" }}>
        {/* Left column */}
        <div className="space-y-4">
          {/* Today's appointments table */}
          <TodaySchedule />

          {/* Doctor Performance */}
          <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-[0_1px_3px_rgba(13,33,55,0.06)] p-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="font-bold text-[#0d2137] text-sm">أداء الأطباء — هذا الأسبوع</h3>
              <span className="text-xs text-[#94a3b8]">عدد المرضى</span>
            </div>
            <div className="space-y-3">
              {doctors.map((doc) => (
                <DoctorPerformance
                  key={doc.name}
                  name={doc.name}
                  patients={doc.patients}
                  maxPatients={maxPatients}
                  color={doc.color}
                />
              ))}
            </div>
          </div>

          {/* Charts */}
          <DashboardCharts />
        </div>

        {/* Right column */}
        <div className="space-y-4">
          {/* Quick Actions — 2x2 grid */}
          <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-[0_1px_3px_rgba(13,33,55,0.06)] p-5">
            <h3 className="font-bold text-[#0d2137] text-sm mb-3">إجراءات سريعة</h3>
            <div className="grid grid-cols-2 gap-3">
              <QuickAction icon={UserPlus} label="مريض جديد" color="#3d7ab5" href="/patients/new" />
              <QuickAction icon={Calendar} label="حجز موعد" color="#f5922e" href="/appointments" />
              <QuickAction icon={FileText} label="كشف حساب" color="#22c55e" href="/finance" />
              <QuickAction icon={Stethoscope} label="حالة تقويم" color="#a855f7" href="/ortho" />
            </div>
          </div>

          {/* Recent Patients */}
          <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-[0_1px_3px_rgba(13,33,55,0.06)] overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-[#e8f0f9]">
              <h3 className="font-bold text-[#0d2137] text-sm">آخر المرضى</h3>
              <Link href="/patients" className="text-xs hover:underline" style={{ color: "#3d7ab5" }}>
                عرض الكل
              </Link>
            </div>
            <div className="divide-y divide-[#e8f0f9]">
              {recentPatients.map((p) => (
                <Link
                  key={p.id}
                  href={`/patients/${p.id}`}
                  className="flex items-center gap-3 px-5 py-3 hover:bg-[#f8fafc] transition"
                >
                  <div
                    className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold text-white flex-shrink-0"
                    style={{ background: "#3d7ab5" }}
                  >
                    {p.name.charAt(2)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-[#0d2137] truncate">{p.name}</p>
                  </div>
                  <div className="flex items-center gap-1 text-[#94a3b8] flex-shrink-0">
                    <Clock className="w-3 h-3" />
                    <span className="text-xs">{p.date}</span>
                  </div>
                </Link>
              ))}
            </div>
          </div>

          {/* Upcoming alerts / quick info */}
          <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-[0_1px_3px_rgba(13,33,55,0.06)] p-5">
            <h3 className="font-bold text-[#0d2137] text-sm mb-3">معلومات سريعة</h3>
            <div className="space-y-2">
              <div className="flex items-center justify-between py-2 border-b border-[#f1f5f9]">
                <span className="text-xs text-[#64748b]">عقود متأخرة</span>
                <span className="text-xs font-bold" style={{ color: stats?.overdueContractsCount ? "#ef4444" : "#22c55e" }}>
                  {loading ? "—" : (stats?.overdueContractsCount ?? 0)}
                </span>
              </div>
              <div className="flex items-center justify-between py-2 border-b border-[#f1f5f9]">
                <span className="text-xs text-[#64748b]">طلبات مختبر معلقة</span>
                <span className="text-xs font-bold text-[#f5922e]">
                  {loading ? "—" : (stats?.pendingLabOrders ?? 0)}
                </span>
              </div>
              <div className="flex items-center justify-between py-2">
                <span className="text-xs text-[#64748b]">مرضى جدد اليوم</span>
                <span className="text-xs font-bold text-[#3d7ab5]">
                  {loading ? "—" : (stats?.newPatientsToday ?? 0)}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
