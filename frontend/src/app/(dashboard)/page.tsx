"use client";
import { useEffect, useState } from "react";
import { Calendar, Users, Activity, FlaskConical, AlertTriangle, Wallet } from "lucide-react";
import { StatsCard } from "@/components/dashboard/StatsCard";
import { DashboardCharts } from "@/components/dashboard/DashboardCharts";
import { TodaySchedule } from "@/components/dashboard/TodaySchedule";
import type { DashboardStats } from "@/types/dashboard";
import api from "@/lib/api";

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

  return (
    <div className="space-y-5 page-content">
      {/* Stats row — 4 column grid matching ZIP */}
      <div className="grid grid-cols-2 xl:grid-cols-4 gap-4">
        <StatsCard
          title="إجمالي المرضى"
          value={loading ? "—" : (stats?.appointmentsToday ?? 0)}
          icon={Users}
          color="blue"
          description={loading ? "" : `+${stats?.newPatientsToday ?? 0} مسجّلون اليوم`}
        />
        <StatsCard
          title="مواعيد اليوم"
          value={loading ? "—" : (stats?.appointmentsToday ?? 0)}
          icon={Calendar}
          color="orange"
          description="إجمالي مواعيد اليوم"
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
          description={loading ? "" : `متأخرات: ${Math.round((stats?.overdueContractsCount ?? 0) / 1000)}K`}
        />
      </div>

      {/* Charts + Today's schedule */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div className="xl:col-span-2">
          <DashboardCharts />
        </div>
        <TodaySchedule />
      </div>

      {/* Extra stats row */}
      <div className="grid grid-cols-2 xl:grid-cols-2 gap-4">
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
          href="/finance/overdue"
        />
      </div>
    </div>
  );
}
