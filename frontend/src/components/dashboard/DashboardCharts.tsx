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

/* ZIP-matched colors: clinic-blue #3d7ab5 for active bars, #dce8f5 for inactive */
const ORTHO_COLORS = ["#3d7ab5", "#22c55e", "#94a3b8"];

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

/* ZIP-matched card style constant */
const cardStyle: React.CSSProperties = {
  background: "#fff",
  borderRadius: 12,
  padding: 20,
  boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
  border: "1px solid #e8f0f9",
};

const TooltipRevenue = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white rounded-lg shadow-md px-3 py-2 text-xs" style={{ border: "1px solid #e8f0f9" }}>
      <p style={{ color: "#94a3b8" }} className="mb-1">{label}</p>
      <p className="font-bold" style={{ color: "#3d7ab5" }}>{payload[0].value.toLocaleString()} ر.ي</p>
    </div>
  );
};

const TooltipAppt = ({ active, payload, label }: TooltipProps) => {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white rounded-lg shadow-md px-3 py-2 text-xs" style={{ border: "1px solid #e8f0f9" }}>
      <p style={{ color: "#94a3b8" }} className="mb-1">{label}</p>
      <p className="font-bold" style={{ color: "#a855f7" }}>{payload[0].value} موعد</p>
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
        <div className="lg:col-span-2 h-56 rounded-xl" style={{ background: "#f1f5f9" }} />
        <div className="h-56 rounded-xl" style={{ background: "#f1f5f9" }} />
        <div className="lg:col-span-3 h-48 rounded-xl" style={{ background: "#f1f5f9" }} />
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
      {/* Revenue bar chart — ZIP-matched styling */}
      <div className="lg:col-span-2" style={cardStyle}>
        <div className="flex items-center justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <TrendingUp className="w-4 h-4" style={{ color: "#3d7ab5" }} />
              <h3 className="font-extrabold text-sm" style={{ color: "#0d2137" }}>الإيرادات — آخر 30 يوماً</h3>
            </div>
            <p className="text-xs mt-0.5" style={{ color: "#94a3b8" }}>
              الإجمالي: <span className="font-semibold" style={{ color: "#0d2137" }}>{totalRevenue.toLocaleString()} ر.ي</span>
            </p>
          </div>
        </div>
        <ResponsiveContainer width="100%" height={180}>
          <BarChart data={data.revenueByDay} margin={{ top: 0, right: 0, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 9, fill: "#94a3b8" }}
              tickLine={false}
              axisLine={false}
              interval={4}
            />
            <YAxis
              tickFormatter={formatYER}
              tick={{ fontSize: 9, fill: "#94a3b8" }}
              tickLine={false}
              axisLine={false}
            />
            <Tooltip content={<TooltipRevenue />} />
            <Bar dataKey="amount" fill="#3d7ab5" radius={[3, 3, 0, 0]} maxBarSize={20} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Ortho donut — ZIP-matched styling */}
      <div style={cardStyle}>
        <div className="flex items-center gap-2 mb-4">
          <Activity className="w-4 h-4" style={{ color: "#a855f7" }} />
          <h3 className="font-extrabold text-sm" style={{ color: "#0d2137" }}>حالات التقويم</h3>
        </div>
        {orthoTotal === 0 ? (
          <div className="flex items-center justify-center h-[180px] text-xs" style={{ color: "#94a3b8" }}>
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
                    <span style={{ color: "#64748b" }}>{d.name}</span>
                  </div>
                  <span className="font-semibold" style={{ color: "#0d2137" }}>{d.value}</span>
                </div>
              ))}
            </div>
          </>
        )}
      </div>

      {/* Appointments line chart — ZIP-matched styling */}
      <div className="lg:col-span-3" style={cardStyle}>
        <div className="flex items-center justify-between mb-4">
          <div>
            <div className="flex items-center gap-2">
              <Calendar className="w-4 h-4" style={{ color: "#a855f7" }} />
              <h3 className="font-extrabold text-sm" style={{ color: "#0d2137" }}>المواعيد — آخر 30 يوماً</h3>
            </div>
            <p className="text-xs mt-0.5" style={{ color: "#94a3b8" }}>
              الإجمالي: <span className="font-semibold" style={{ color: "#0d2137" }}>{totalAppts} موعد</span>
            </p>
          </div>
        </div>
        <ResponsiveContainer width="100%" height={140}>
          <LineChart data={data.appointmentsByDay} margin={{ top: 0, right: 10, left: -10, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 9, fill: "#94a3b8" }}
              tickLine={false}
              axisLine={false}
              interval={4}
            />
            <YAxis
              tick={{ fontSize: 9, fill: "#94a3b8" }}
              tickLine={false}
              axisLine={false}
              allowDecimals={false}
            />
            <Tooltip content={<TooltipAppt />} />
            <Line
              type="monotone"
              dataKey="count"
              stroke="#a855f7"
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 4, fill: "#a855f7" }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
