"use client";

import { useState } from "react";
import { Phone, Send, Clock, CheckCheck, XCircle, Search } from "lucide-react";
import api from "@/lib/api";
import { useQuery } from "@tanstack/react-query";

interface SmsReminder {
  id: string;
  patientId: string;
  patientName: string;
  patientPhone: string;
  appointmentDate: string;
  appointmentTime: string;
  doctorName: string;
  message: string;
  status: "pending" | "sent" | "delivered" | "failed";
  sentAt?: string;
  createdAt: string;
}

export default function SmsPage() {
  const [statusFilter, setStatusFilter] = useState("all");
  const [searchTerm, setSearchTerm] = useState("");
  const [sendingAll, setSendingAll] = useState(false);

  // Get today's appointments for SMS reminders
  const { data: todayAppointments, isLoading } = useQuery({
    queryKey: ["appointments", "today"],
    queryFn: async () => {
      const { data } = await api.get("/api/appointments/today");
      return data;
    },
    staleTime: 30_000,
  });

  const reminders: SmsReminder[] = (todayAppointments ?? []).map((apt: Record<string, unknown>) => ({
    id: apt.id as string,
    patientId: apt.patientId as string,
    patientName: apt.patientName as string,
    patientPhone: (apt.patientPhone as string) || "—",
    appointmentDate: apt.appointmentDate as string,
    appointmentTime: apt.startTime as string,
    doctorName: apt.doctorName as string,
    message: `تذكير: لديك موعد في مركز د. عقلان بتاريخ ${apt.appointmentDate} الساعة ${apt.startTime} مع ${apt.doctorName || "الطبيب"}`,
    status: "pending" as const,
    createdAt: apt.createdAt as string,
  }));

  const filteredReminders = reminders.filter(
    (r) =>
      r.patientName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      r.patientPhone?.includes(searchTerm)
  );

  const statusColors: Record<string, string> = {
    pending: "bg-amber-100 text-amber-700",
    sent: "bg-blue-100 text-blue-700",
    delivered: "bg-green-100 text-green-700",
    failed: "bg-red-100 text-red-700",
  };

  const statusIcons: Record<string, React.ReactNode> = {
    pending: <Clock className="w-3.5 h-3.5" />,
    sent: <Send className="w-3.5 h-3.5" />,
    delivered: <CheckCheck className="w-3.5 h-3.5" />,
    failed: <XCircle className="w-3.5 h-3.5" />,
  };

  const statusLabels: Record<string, string> = {
    pending: "قيد الانتظار",
    sent: "تم الإرسال",
    delivered: "تم التوصيل",
    failed: "فشل الإرسال",
  };

  const stats = {
    total: reminders.length,
    pending: reminders.filter((r) => r.status === "pending").length,
    sent: reminders.filter((r) => r.status === "sent").length,
    delivered: reminders.filter((r) => r.status === "delivered").length,
    failed: reminders.filter((r) => r.status === "failed").length,
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-extrabold text-[#0d2137]">تذكيرات SMS</h2>
          <p className="text-sm text-gray-500 mt-1">
            إرسال تذكيرات تلقائية للمرضى قبل المواعيد
          </p>
        </div>
        <button
          onClick={() => setSendingAll(true)}
          disabled={stats.pending === 0 || sendingAll}
          className="flex items-center gap-2 px-5 py-2.5 bg-[#3d7ab5] text-white rounded-xl text-sm font-bold hover:bg-[#2d5e8e] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Send className="w-4 h-4" />
          إرسال الكل ({stats.pending})
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {[
          { label: "الإجمالي", value: stats.total, color: "bg-[#f0f5fb] text-[#3d7ab5]" },
          { label: "قيد الانتظار", value: stats.pending, color: "bg-amber-50 text-amber-600" },
          { label: "تم الإرسال", value: stats.sent + stats.delivered, color: "bg-green-50 text-green-600" },
          { label: "فشل", value: stats.failed, color: "bg-red-50 text-red-600" },
        ].map((stat) => (
          <div key={stat.label} className={`${stat.color} rounded-xl p-4 text-center`}>
            <p className="text-2xl font-extrabold">{stat.value}</p>
            <p className="text-xs font-bold mt-1 opacity-70">{stat.label}</p>
          </div>
        ))}
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[220px] max-w-sm">
          <Search className="w-4 h-4 absolute top-1/2 -translate-y-1/2 right-3 text-gray-400" />
          <input
            type="text"
            placeholder="بحث بالاسم أو الرقم..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full h-10 pr-10 pl-4 text-sm rounded-xl border border-[#dce8f5] bg-[#f7fafd] focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]/30 focus:border-[#3d7ab5]"
          />
        </div>
        <div className="flex items-center gap-1.5">
          {["all", "pending", "sent", "delivered", "failed"].map((s) => (
            <button
              key={s}
              onClick={() => setStatusFilter(s)}
              className={`px-3 py-1.5 text-xs font-bold rounded-lg transition-colors ${
                statusFilter === s
                  ? "bg-[#3d7ab5] text-white"
                  : "bg-[#f0f5fb] text-gray-600 hover:bg-[#dce8f5]"
              }`}
            >
              {s === "all" ? "الكل" : statusLabels[s]}
            </button>
          ))}
        </div>
      </div>

      {/* Reminders List */}
      {isLoading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="animate-pulse h-24 bg-gray-100 rounded-xl" />
          ))}
        </div>
      ) : filteredReminders.length === 0 ? (
        <div className="text-center py-20">
          <Phone className="w-16 h-16 mx-auto text-gray-200 mb-4" />
          <p className="text-gray-400 text-lg font-bold">لا توجد تذكيرات</p>
          <p className="text-gray-300 text-sm mt-1">
            ستظهر تذكيرات المواعيد هنا تلقائياً
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {filteredReminders.map((reminder) => (
            <div
              key={reminder.id}
              className="p-4 rounded-xl border border-[#e8f0f9] bg-white hover:shadow-md transition-all"
            >
              <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-full bg-[#3d7ab5]/10 flex items-center justify-center flex-shrink-0">
                  <Phone className="w-5 h-5 text-[#3d7ab5]" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-sm font-bold text-[#0d2137]">
                      {reminder.patientName}
                    </span>
                    <span className="text-xs text-gray-400">{reminder.patientPhone}</span>
                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full flex items-center gap-1 ${statusColors[reminder.status]}`}>
                      {statusIcons[reminder.status]}
                      {statusLabels[reminder.status]}
                    </span>
                  </div>
                  <p className="text-xs text-gray-600 bg-[#f7fafd] rounded-lg p-2 mt-1">
                    {reminder.message}
                  </p>
                  <div className="flex items-center gap-3 mt-2 text-[11px] text-gray-400">
                    <span>موعد: {reminder.appointmentDate} - {reminder.appointmentTime}</span>
                    <span>الطبيب: {reminder.doctorName}</span>
                  </div>
                </div>
                {reminder.status === "pending" && (
                  <button
                    className="px-3 py-1.5 bg-[#3d7ab5] text-white rounded-lg text-xs font-bold hover:bg-[#2d5e8e] transition-colors flex-shrink-0"
                    title="إرسال الآن"
                  >
                    إرسال
                  </button>
                )}
                {reminder.status === "failed" && (
                  <button
                    className="px-3 py-1.5 bg-red-100 text-red-600 rounded-lg text-xs font-bold hover:bg-red-200 transition-colors flex-shrink-0"
                    title="إعادة المحاولة"
                  >
                    إعادة
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
