"use client";
import { useEffect, useState } from "react";
import { Calendar, Users, Activity, FlaskConical } from "lucide-react";
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

  const today = new Intl.DateTimeFormat("ar-YE", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(new Date());

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">لوحة التحكم</h1>
        <p className="text-sm text-gray-500 mt-1">{today}</p>
      </div>

      {/* Stats grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
        <StatsCard
          title="مواعيد اليوم"
          value={loading ? "—" : (stats?.appointmentsToday ?? 0)}
          icon={Calendar}
          color="teal"
          description="إجمالي مواعيد اليوم"
        />
        <StatsCard
          title="مرضى جدد اليوم"
          value={loading ? "—" : (stats?.newPatientsToday ?? 0)}
          icon={Users}
          color="gold"
          description="مسجّلون اليوم"
        />
        <StatsCard
          title="حالات تقويم نشطة"
          value={loading ? "—" : (stats?.activeOrthoCases ?? 0)}
          icon={Activity}
          color="purple"
          description="حالات جارية"
        />
        <StatsCard
          title="طلبات مختبر معلقة"
          value={loading ? "—" : (stats?.pendingLabOrders ?? 0)}
          icon={FlaskConical}
          color="green"
          description="قيد التصنيع أو الشحن"
        />
      </div>

      {/* Charts + Today's schedule */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div className="xl:col-span-2">
          <DashboardCharts />
        </div>
        <TodaySchedule />
      </div>

      {/* Welcome card */}
      <div className="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 clinic-gradient rounded-xl flex items-center justify-center flex-shrink-0">
            <Activity className="w-6 h-6 text-white" />
          </div>
          <div>
            <h2 className="text-lg font-bold text-gray-900">
              مرحباً بك في Aqlan Dental Pro
            </h2>
            <p className="text-sm text-gray-500 mt-1 leading-relaxed">
              نظام إدارة متكامل لمركز د. عقلان الكامل لطب وتقويم الأسنان.
              يمكنك من هنا إدارة المرضى، المواعيد، الحالات التقويمية، والمالية.
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {["المرضى ✓", "المواعيد ✓", "التقويم ✓", "السيفالومتري ✓", "المالية ✓", "الجراحة ✓", "الإحالات ✓", "التقارير ✓"].map((f) => (
                <span
                  key={f}
                  className="text-xs px-2.5 py-1 rounded-full bg-green-100 text-green-700"
                >
                  {f}
                </span>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
