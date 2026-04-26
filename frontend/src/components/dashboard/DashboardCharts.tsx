"use client";
import { useEffect, useState } from "react";
import {
  BarChart, Bar, LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, PieChart, Pie, Cell,
} from "recharts";
import { TrendingUp, Calendar, Activity } from "lucide-react";
import api from "@/lib/api";

interface DailyRevenue   { date: string; amount: number }
interface DailyAppt      { date: string; count: number }
interface OrthoStatus    { active: number; completed: number; cancelled: number }
interface ChartsData {
  revenueByDay:      DailyRevenue[];
  appointmentsByDay: DailyAppt[];
  orthoByStatus:     OrthoStatus;
}

const ORTHO_COLORS = ["#0E7490", "#059669", "#6B7280"];

function formatYER(val: number) {
  if (val >= 1_000_000) return `${(val / 1_000_000).toFixed(1)}م`;
  if (val >= 1_000)     return `${(val / 1_000).toFixed(0)}ك`;
  return String(val);
}

interface TooltipProps {
  active?: boolean;
  payload?: { value: number }[];
  label?: string;
}

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

export function DashboardCharts() {
  const [data, setData] = useState<ChartsData | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<ChartsData>("/api/dashboard/charts")
      .then((r) => setData(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 animate-pulse">
        <div className="lg:col-span-2 h-56 bg-gray-100 rounded-xl" />
        <div className="h-56 bg-gray-100 rounded-xl" />
        <div className="lg:col-span-3 h-48 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!data) return null;

  const orthoTotal = data.orthoByStatus.active + data.orthoByStatus.completed + data.orthoByStatus.cancelled;
  const pieData = [
    { name: "نشطة",    value: data.orthoByStatus.active },
    { name: "مكتملة",  value: data.orthoByStatus.completed },
    { name: "ملغاة",   value: data.orthoByStatus.cancelled },
  ].filter((d) => d.value > 0);

  const totalRevenue = data.revenueByDay.reduce((s, d) => s + d.amount, 0);
  const totalAppts   = data.appointmentsByDay.reduce((s, d) => s + d.count, 0);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
      {/* Revenue bar chart */}
      <div className="lg:col-span-2 bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <TrendingUp className="w-4 h-4 text-clinic-teal" />
              <h3 className="font-bold text-gray-900 text-sm">الإيرادات — آخر 30 يوماً</h3>
            </div>
            <p className="text-xs text-gray-400 mt-0.5">
              الإجمالي: <span className="font-semibold text-gray-700">{totalRevenue.toLocaleString()} ر.ي</span>
            </p>
          </div>
        </div>
        <ResponsiveContainer width="100%" height={180}>
          <BarChart data={data.revenueByDay} margin={{ top: 0, right: 0, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 9, fill: "#9CA3AF" }}
              tickLine={false}
              axisLine={false}
              interval={4}
            />
            <YAxis
              tickFormatter={formatYER}
              tick={{ fontSize: 9, fill: "#9CA3AF" }}
              tickLine={false}
              axisLine={false}
            />
            <Tooltip content={<TooltipRevenue />} />
            <Bar dataKey="amount" fill="#0E7490" radius={[3, 3, 0, 0]} maxBarSize={20} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Ortho donut */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-center gap-2 mb-4">
          <Activity className="w-4 h-4 text-purple-600" />
          <h3 className="font-bold text-gray-900 text-sm">حالات التقويم</h3>
        </div>
        {orthoTotal === 0 ? (
          <div className="flex items-center justify-center h-[180px] text-gray-300 text-xs">
            لا توجد حالات بعد
          </div>
        ) : (
          <>
            <ResponsiveContainer width="100%" height={140}>
              <PieChart>
                <Pie
                  data={pieData}
                  cx="50%"
                  cy="50%"
                  innerRadius={40}
                  outerRadius={65}
                  paddingAngle={3}
                  dataKey="value"
                >
                  {pieData.map((_, i) => (
                    <Cell key={i} fill={ORTHO_COLORS[i % ORTHO_COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(v) => [`${v} حالة`, ""]} />
              </PieChart>
            </ResponsiveContainer>
            <div className="mt-2 space-y-1">
              {pieData.map((d, i) => (
                <div key={d.name} className="flex items-center justify-between text-xs">
                  <div className="flex items-center gap-1.5">
                    <span className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: ORTHO_COLORS[i] }} />
                    <span className="text-gray-600">{d.name}</span>
                  </div>
                  <span className="font-semibold text-gray-900">{d.value}</span>
                </div>
              ))}
            </div>
          </>
        )}
      </div>

      {/* Appointments line chart */}
      <div className="lg:col-span-3 bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <Calendar className="w-4 h-4 text-purple-600" />
              <h3 className="font-bold text-gray-900 text-sm">المواعيد — آخر 30 يوماً</h3>
            </div>
            <p className="text-xs text-gray-400 mt-0.5">
              الإجمالي: <span className="font-semibold text-gray-700">{totalAppts} موعد</span>
            </p>
          </div>
        </div>
        <ResponsiveContainer width="100%" height={140}>
          <LineChart data={data.appointmentsByDay} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 9, fill: "#9CA3AF" }}
              tickLine={false}
              axisLine={false}
              interval={4}
            />
            <YAxis
              tick={{ fontSize: 9, fill: "#9CA3AF" }}
              tickLine={false}
              axisLine={false}
              allowDecimals={false}
            />
            <Tooltip content={<TooltipAppt />} />
            <Line
              type="monotone"
              dataKey="count"
              stroke="#7C3AED"
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 4, fill: "#7C3AED" }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
