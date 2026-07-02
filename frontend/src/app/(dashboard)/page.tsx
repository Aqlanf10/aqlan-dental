"use client";
import { useEffect, useState } from "react";
import { Calendar, Users, Activity, FlaskConical, AlertTriangle, Wallet, Plus, ListOrdered } from "lucide-react";
import Link from "next/link";
import { StatsCard } from "@/components/dashboard/StatsCard";
import { AttentionAlerts } from "@/components/dashboard/AttentionAlerts";
import { DashboardCharts } from "@/components/dashboard/DashboardCharts";
import { TodaySchedule } from "@/components/dashboard/TodaySchedule";
import type { DashboardStats } from "@/types/dashboard";
import api from "@/lib/api";
/* ZIP-matched card styles — shared constants (identical pixels) */
import { cardStyle, cardNoPadStyle } from "@/components/ui/cardStyles";

interface RecentPatient {
  id: string;
  fullName: string;
  patientNumber: string;
  phone?: string;
  primaryDoctorName?: string;
  createdAt: string;
}

export default function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [recentPatients, setRecentPatients] = useState<RecentPatient[]>([]);

  useEffect(() => {
    api
      .get<DashboardStats>("/api/dashboard/stats")
      .then((r) => setStats(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));

    // Fetch recent patients for the "Recent Patients" section
    api
      .get<{ data: RecentPatient[] }>("/api/patients?pageSize=5&status=active")
      .then((r) => setRecentPatients(r.data.data ?? []))
      .catch(() => {});
  }, []);

  return (
    <div className="space-y-5 page-content">
      {/* Attention alerts — "يحتاج انتباهك" */}
      <AttentionAlerts />

      {/* Stats row — 4 column grid matching ZIP */}
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-4">
        <StatsCard
          title="إجمالي المرضى"
          value={loading ? "—" : (stats?.totalPatients ?? 0)}
          icon={Users}
          color="blue"
          description={loading ? "" : `+${stats?.newPatientsToday ?? 0} جديد اليوم`}
        />
        <StatsCard
          title="مواعيد اليوم"
          value={loading ? "—" : (stats?.appointmentsToday ?? 0)}
          icon={Calendar}
          color="orange"
          description={loading ? "" : `${stats?.appointmentsToday ?? 0} موعد`}
        />
        <StatsCard
          title="حالات تقويم نشطة"
          value={loading ? "—" : (stats?.activeOrthoCases ?? 0)}
          icon={Activity}
          color="purple"
          description="جارية حالياً"
        />
        <StatsCard
          title="إيرادات الشهر"
          value={loading ? "—" : (stats ? `${Math.round(stats.totalRevenueMTD / 1000)}K ر.ي` : 0)}
          icon={Wallet}
          color="green"
          description={loading ? "" : `${stats?.overdueContractsCount ?? 0} عقد متأخر السداد`}
        />
      </div>

      {/* Charts + Today's schedule — ZIP layout */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div className="xl:col-span-2">
          <DashboardCharts />
        </div>
        <TodaySchedule />
      </div>

      {/* Recent Patients + Quick Actions — ZIP layout */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        {/* Recent patients list — matches ZIP */}
        <div style={cardNoPadStyle}>
          <div className="px-5 py-4 flex items-center justify-between" style={{ borderBottom: "1px solid #f1f5f9" }}>
            <h3 className="font-extrabold text-[15px]" style={{ color: "#0d2137" }}>أحدث المرضى</h3>
            <Link
              href="/patients"
              className="text-xs font-semibold transition"
              style={{ color: "#3d7ab5" }}
              onMouseEnter={(e) => (e.currentTarget.style.color = "#2d5e8e")}
              onMouseLeave={(e) => (e.currentTarget.style.color = "#3d7ab5")}
            >
              عرض الكل
            </Link>
          </div>
          {recentPatients.length === 0 ? (
            <div className="py-8 text-center text-xs" style={{ color: "#94a3b8" }}>لا يوجد مرضى بعد</div>
          ) : (
            recentPatients.map((p, i) => (
              <Link
                key={p.id}
                href={`/patients/${p.id}`}
                className="flex items-center gap-3 px-5 py-3 transition"
                style={{ borderBottom: i < recentPatients.length - 1 ? "1px solid #f8fafc" : "none" }}
                onMouseEnter={(e) => (e.currentTarget.style.background = "#f7fafd")}
                onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
              >
                <div
                  className="w-[34px] h-[34px] rounded-full flex items-center justify-center text-[13px] font-extrabold flex-shrink-0"
                  style={{ background: "#3d7ab518", color: "#3d7ab5" }}
                >
                  {p.fullName.charAt(0)}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-[13px] font-bold truncate" style={{ color: "#0d2137" }}>{p.fullName}</p>
                  <p className="text-[11px]" style={{ color: "#94a3b8" }}>{p.patientNumber} · {p.primaryDoctorName ?? "—"}</p>
                </div>
              </Link>
            ))
          )}
        </div>

        {/* Quick Actions — matches ZIP */}
        <div className="flex flex-col gap-4">
          <div style={cardStyle}>
            <h3 className="font-extrabold text-sm mb-3.5" style={{ color: "#0d2137" }}>إجراءات سريعة</h3>
            <div className="grid grid-cols-2 gap-2">
              {[
                { label: "مريض جديد", icon: Plus, color: "#3d7ab5", href: "/patients/new" },
                { label: "موعد جديد", icon: Calendar, color: "#f5922e", href: "/appointments/new" },
                { label: "حالة تقويم", icon: Activity, color: "#a855f7", href: "/ortho/new" },
                { label: "تسجيل دفعة", icon: Wallet, color: "#22c55e", href: "/daily-operations" },
              ].map((a) => (
                <Link
                  key={a.label}
                  href={a.href}
                  className="flex items-center gap-2 px-3 py-3 rounded-[10px] text-[13px] font-semibold transition"
                  style={{ background: a.color + "10", border: `1.5px solid ${a.color}25`, color: a.color }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = a.color + "20")}
                  onMouseLeave={(e) => (e.currentTarget.style.background = a.color + "10")}
                >
                  <a.icon className="w-4 h-4" style={{ stroke: a.color }} />
                  {a.label}
                </Link>
              ))}
            </div>
          </div>

          {/* Extra stats — pending lab + overdue */}
          <div className="grid grid-cols-2 gap-4">
            <StatsCard
              title="طلبات مختبر معلقة"
              value={loading ? "—" : (stats?.pendingLabOrders ?? 0)}
              icon={FlaskConical}
              color="blue"
              description="قيد التصنيع أو الشحن"
            />
            <StatsCard
              title="عقود متأخرة"
              value={loading ? "—" : (stats?.overdueContractsCount ?? 0)}
              icon={AlertTriangle}
              color={stats?.overdueContractsCount ? "red" : "green"}
              description="أقساط متأخرة السداد"
              href="/finance-v3?tab=contracts"
            />
          </div>

          {/* Queue Status Widget */}
          <div style={cardStyle}>
            <h3 className="font-extrabold text-sm mb-3.5" style={{ color: "#0d2137" }}>حالة الانتظار</h3>
            <div className="grid grid-cols-3 gap-3">
              <div className="rounded-lg p-3 text-center" style={{ background: "#f5922e10", border: "1.5px solid #f5922e25" }}>
                <div className="text-xl font-extrabold" style={{ color: "#f5922e" }}>{stats?.queueWaitingCount ?? 0}</div>
                <div className="text-xs font-medium mt-1" style={{ color: "#64748b" }}>عدد المنتظرين</div>
              </div>
              <div className="rounded-lg p-3 text-center" style={{ background: "#22c55e10", border: "1.5px solid #22c55e25" }}>
                <div className="text-xl font-extrabold" style={{ color: "#22c55e" }}>{stats?.todayArrivedCount ?? 0}</div>
                <div className="text-xs font-medium mt-1" style={{ color: "#64748b" }}>عدد الواصلين</div>
              </div>
              <div className="rounded-lg p-3 text-center" style={{ background: "#3d7ab510", border: "1.5px solid #3d7ab525" }}>
                <div className="text-xl font-extrabold" style={{ color: "#3d7ab5" }}>{stats?.pendingBookingRequestsCount ?? 0}</div>
                <div className="text-xs font-medium mt-1" style={{ color: "#64748b" }}>طلبات حجز معلقة</div>
              </div>
            </div>
            <Link
              href="/daily-operations?tab=queue"
              className="mt-3 flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-sm font-bold text-white transition hover:opacity-90"
              style={{ background: "#3d7ab5" }}
            >
              <ListOrdered className="w-4 h-4" />
              فتح الانتظار
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
