"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import {
  Activity, ArrowRight, Clock, Hourglass, RefreshCw, Stethoscope,
  TrendingUp, UserX, Users,
} from "lucide-react";
import api from "@/lib/api";
import { localDateString } from "@/lib/utils";
import { Skeleton } from "@/components/ui/skeleton";

/* ═══════════════════════════════════════════════════════════════════════════
   TYPES — GET /api/clinic-queue/analytics?fromDate=&toDate=
   ═══════════════════════════════════════════════════════════════════════════ */

interface OpsDoctorStat {
  doctorName: string;
  totalPatients: number;
  completed: number;
  noShow: number;
  avgServiceTime: number;
}

interface OpsAnalytics {
  dateRange: { from: string; to: string };
  totalItems: number;
  completed: number;
  noShow: number;
  cancelled: number;
  completionRate: number;
  noShowRate: number;
  avgServiceTimeMinutes: number;
  avgWaitTimeMinutes: number;
  avgCalledToInRoomMinutes: number;
  avgInRoomToStartMinutes: number;
  doctorStats: OpsDoctorStat[];
  hourlyDistribution: { hour: number; count: number }[];
  priorityDistribution: { priority: string; priorityArabic: string; count: number }[];
}

/* ═══════════════════════════════════════════════════════════════════════════
   HELPERS
   ═══════════════════════════════════════════════════════════════════════════ */

/** ساعة 24 → صيغة عربية 12 ساعة مثل: ٩ ص / ٣ م */
function formatHourArabic(hour: number): string {
  const suffix = hour < 12 ? "ص" : "م";
  const h12 = hour % 12 === 0 ? 12 : hour % 12;
  return `${h12.toLocaleString("ar-EG")} ${suffix}`;
}

function formatMinutes(val: number | null | undefined): string {
  if (val == null) return "—";
  return `${Math.round(val)} د`;
}

function formatPct(val: number | null | undefined): string {
  if (val == null) return "—";
  return `${Math.round(val)}%`;
}

type PresetKey = "today" | "week" | "month30" | "thisMonth";

function presetRange(key: PresetKey): { from: string; to: string } {
  const today = localDateString();
  const d = new Date();
  switch (key) {
    case "today":
      return { from: today, to: today };
    case "week":
      d.setDate(d.getDate() - 6);
      return { from: localDateString(d), to: today };
    case "month30":
      d.setDate(d.getDate() - 29);
      return { from: localDateString(d), to: today };
    case "thisMonth":
      return { from: localDateString(new Date(d.getFullYear(), d.getMonth(), 1)), to: today };
  }
}

const PRESETS: { key: PresetKey; label: string }[] = [
  { key: "today", label: "اليوم" },
  { key: "week", label: "آخر 7 أيام" },
  { key: "month30", label: "آخر 30 يوم" },
  { key: "thisMonth", label: "هذا الشهر" },
];

const PRIORITY_BADGE: Record<string, string> = {
  Emergency: "bg-red-50 text-red-700 border-red-200",
  Urgent: "bg-orange-50 text-orange-700 border-orange-200",
  Appointment: "bg-blue-50 text-blue-700 border-blue-200",
  Normal: "bg-gray-50 text-gray-600 border-gray-200",
};

/* ═══════════════════════════════════════════════════════════════════════════
   SMALL UI PIECES
   ═══════════════════════════════════════════════════════════════════════════ */

function HeadlineCard({
  label, value, icon: Icon, color, sub,
}: {
  label: string; value: string; icon: typeof Users; color: string; sub?: string;
}) {
  return (
    <div className={`bg-white rounded-xl border shadow-sm p-4 ${color}`}>
      <div className="flex items-center gap-2 text-xs font-medium opacity-80">
        <Icon className="w-4 h-4" />
        {label}
      </div>
      <p className="text-2xl font-extrabold mt-1.5">{value}</p>
      {sub && <p className="text-[11px] opacity-70 mt-0.5">{sub}</p>}
    </div>
  );
}

function HourTooltip({ active, payload, label }: { active?: boolean; payload?: { value: number }[]; label?: number }) {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-md px-3 py-2 text-xs" dir="rtl">
      <p className="text-gray-500 mb-1">{typeof label === "number" ? formatHourArabic(label) : label}</p>
      <p className="font-bold text-clinic-blue">{payload[0].value} مريض</p>
    </div>
  );
}

function PageSkeleton() {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-24 rounded-xl" />
        ))}
      </div>
      <Skeleton className="h-64 rounded-xl" />
      <Skeleton className="h-48 rounded-xl" />
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   PAGE
   ═══════════════════════════════════════════════════════════════════════════ */

export default function OperationsReportPage() {
  const initial = presetRange("week");
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);

  const { data, isLoading, isError, refetch, isFetching } = useQuery<OpsAnalytics>({
    queryKey: ["ops-analytics", from, to],
    queryFn: async () => {
      const res = await api.get<OpsAnalytics>(
        `/api/clinic-queue/analytics?fromDate=${from}&toDate=${to}`
      );
      return res.data;
    },
    enabled: Boolean(from && to),
  });

  const applyPreset = (key: PresetKey) => {
    const r = presetRange(key);
    setFrom(r.from);
    setTo(r.to);
  };

  const isPresetActive = (key: PresetKey) => {
    const r = presetRange(key);
    return r.from === from && r.to === to;
  };

  const noShowHigh = (data?.noShowRate ?? 0) > 15;
  const waitHigh = (data?.avgWaitTimeMinutes ?? 0) > 30;

  const hourly = (data?.hourlyDistribution ?? [])
    .slice()
    .sort((a, b) => a.hour - b.hour);

  return (
    <div className="max-w-7xl space-y-4" dir="rtl">
      {/* Header */}
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">تقرير العمليات والزحمة</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            تحليل قائمة الانتظار: الغياب، أوقات الانتظار، ساعات الذروة وأداء الأطباء
          </p>
        </div>
        <Link
          href="/reports"
          className="flex items-center gap-1.5 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50 rounded-lg border border-gray-200 transition"
        >
          <ArrowRight className="w-4 h-4" />
          كل التقارير
        </Link>
      </div>

      {/* Date range toolbar */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-3 flex items-center gap-2 flex-wrap">
        <div className="flex items-center gap-1.5 flex-wrap">
          {PRESETS.map((p) => (
            <button
              key={p.key}
              onClick={() => applyPreset(p.key)}
              className={`px-3 py-1.5 text-xs rounded-lg border transition ${
                isPresetActive(p.key)
                  ? "bg-clinic-blue-50 border-clinic-blue text-clinic-blue font-semibold"
                  : "border-gray-200 text-gray-600 hover:bg-gray-50"
              }`}
            >
              {p.label}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2 md:ms-auto flex-wrap">
          <input
            type="date"
            value={from}
            max={to}
            onChange={(e) => setFrom(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-clinic-blue"
          />
          <span className="text-gray-400">—</span>
          <input
            type="date"
            value={to}
            min={from}
            onChange={(e) => setTo(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-clinic-blue"
          />
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-50 transition"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isFetching ? "animate-spin" : ""}`} />
            تحديث
          </button>
        </div>
      </div>

      {/* Content */}
      {isLoading ? (
        <PageSkeleton />
      ) : isError ? (
        <div className="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
          <p className="text-sm font-semibold text-red-700">تعذر تحميل بيانات التقرير</p>
          <p className="text-xs text-red-500 mt-1">تحقق من الاتصال ثم أعد المحاولة</p>
          <button
            onClick={() => refetch()}
            className="mt-3 px-4 py-2 text-sm rounded-lg bg-white border border-red-200 text-red-700 hover:bg-red-100 transition"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : !data || data.totalItems === 0 ? (
        <div className="bg-white border border-gray-200 rounded-xl p-10 text-center">
          <Activity className="w-10 h-10 text-gray-300 mx-auto mb-3" />
          <p className="text-sm font-semibold text-gray-600">لا توجد بيانات في هذه الفترة</p>
          <p className="text-xs text-gray-400 mt-1">جرّب توسيع نطاق التاريخ أعلاه</p>
        </div>
      ) : (
        <>
          {/* Headline cards */}
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
            <HeadlineCard
              label="معدل الغياب"
              value={formatPct(data.noShowRate)}
              icon={UserX}
              color={noShowHigh ? "border-red-300 bg-red-50 text-red-700" : "border-gray-200 text-gray-800"}
              sub={`${data.noShow} لم يحضر`}
            />
            <HeadlineCard
              label="متوسط الانتظار"
              value={formatMinutes(data.avgWaitTimeMinutes)}
              icon={Hourglass}
              color={waitHigh ? "border-orange-300 bg-orange-50 text-orange-700" : "border-gray-200 text-gray-800"}
            />
            <HeadlineCard
              label="متوسط مدة العلاج"
              value={formatMinutes(data.avgServiceTimeMinutes)}
              icon={Stethoscope}
              color="border-gray-200 text-gray-800"
            />
            <HeadlineCard
              label="نسبة الإكمال"
              value={formatPct(data.completionRate)}
              icon={TrendingUp}
              color="border-gray-200 text-gray-800"
              sub={`${data.completed} مكتمل`}
            />
            <HeadlineCard
              label="إجمالي المرضى"
              value={data.totalItems.toLocaleString("ar-EG")}
              icon={Users}
              color="border-gray-200 text-gray-800"
              sub={data.cancelled > 0 ? `${data.cancelled} ملغي` : undefined}
            />
          </div>

          {/* Peak hours */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
            <div className="flex items-center gap-2 mb-3">
              <Clock className="w-4 h-4 text-clinic-orange" />
              <h2 className="text-sm font-bold text-gray-900">ساعات الذروة</h2>
            </div>
            {hourly.length === 0 ? (
              <p className="text-xs text-gray-400 py-6 text-center">لا توجد بيانات ساعات لهذه الفترة</p>
            ) : (
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={hourly} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
                  <XAxis
                    dataKey="hour"
                    tick={{ fontSize: 10, fill: "#9CA3AF" }}
                    tickLine={false}
                    axisLine={false}
                    tickFormatter={(h: number) => formatHourArabic(h)}
                  />
                  <YAxis tick={{ fontSize: 9, fill: "#9CA3AF" }} tickLine={false} axisLine={false} allowDecimals={false} />
                  <Tooltip content={<HourTooltip />} />
                  <Bar dataKey="count" fill="#f5922e" radius={[4, 4, 0, 0]} maxBarSize={28} />
                </BarChart>
              </ResponsiveContainer>
            )}
          </div>

          {/* Doctor stats */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
            <div className="flex items-center gap-2 mb-3">
              <Stethoscope className="w-4 h-4 text-clinic-blue" />
              <h2 className="text-sm font-bold text-gray-900">أداء الأطباء</h2>
            </div>
            {data.doctorStats.length === 0 ? (
              <p className="text-xs text-gray-400 py-6 text-center">لا توجد بيانات أطباء لهذه الفترة</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-xs text-gray-400 border-b border-gray-100">
                      <th className="text-start font-medium py-2 px-2">الطبيب</th>
                      <th className="text-center font-medium py-2 px-2">المرضى</th>
                      <th className="text-center font-medium py-2 px-2">مكتمل</th>
                      <th className="text-center font-medium py-2 px-2">غياب</th>
                      <th className="text-center font-medium py-2 px-2">متوسط مدة العلاج (د)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.doctorStats.map((d) => (
                      <tr key={d.doctorName} className="border-b border-gray-50 hover:bg-gray-50/60">
                        <td className="py-2.5 px-2 font-semibold text-gray-800">{d.doctorName}</td>
                        <td className="py-2.5 px-2 text-center text-gray-700">{d.totalPatients}</td>
                        <td className="py-2.5 px-2 text-center text-green-600 font-medium">{d.completed}</td>
                        <td className={`py-2.5 px-2 text-center font-medium ${d.noShow > 0 ? "text-red-600" : "text-gray-400"}`}>
                          {d.noShow}
                        </td>
                        <td className="py-2.5 px-2 text-center text-gray-700">{Math.round(d.avgServiceTime)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Priority distribution */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
            <div className="flex items-center gap-2 mb-3">
              <Activity className="w-4 h-4 text-purple-600" />
              <h2 className="text-sm font-bold text-gray-900">توزيع الأولويات</h2>
            </div>
            {data.priorityDistribution.length === 0 ? (
              <p className="text-xs text-gray-400 py-4 text-center">لا توجد بيانات أولويات لهذه الفترة</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {data.priorityDistribution.map((p) => (
                  <span
                    key={p.priority}
                    className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-full border ${
                      PRIORITY_BADGE[p.priority] ?? "bg-gray-50 text-gray-600 border-gray-200"
                    }`}
                  >
                    <span className="font-medium">{p.priorityArabic || p.priority}</span>
                    <span className="font-bold">{p.count}</span>
                  </span>
                ))}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
