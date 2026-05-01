"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Calendar, Plus } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate, formatTime, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";

interface AppointmentDto {
  id: string;
  patientName?: string;
  doctorName?: string;
  date: string;
  startTime: string;
  endTime: string;
  status: string;
  treatmentType?: string;
  notes?: string;
}

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-100 text-blue-700",
  Confirmed: "bg-teal-100 text-teal-700",
  Arrived: "bg-yellow-100 text-yellow-700",
  InProgress: "bg-purple-100 text-purple-700",
  Completed: "bg-green-100 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-100 text-red-700",
};

interface AppointmentsTabProps {
  patientId: string;
  patientName: string;
}

export function AppointmentsTab({ patientId, patientName }: AppointmentsTabProps) {
  const [appointments, setAppointments] = useState<AppointmentDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Date range filter
  const today = new Date();
  const threeMonthsAgo = new Date(today);
  threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3);
  const threeMonthsLater = new Date(today);
  threeMonthsLater.setMonth(threeMonthsLater.getMonth() + 3);

  const [fromDate, setFromDate] = useState(threeMonthsAgo.toISOString().split("T")[0]);
  const [toDate, setToDate] = useState(threeMonthsLater.toISOString().split("T")[0]);

  useEffect(() => {
    setLoading(true);
    api.get<AppointmentDto[]>(`/api/appointments?from=${fromDate}&to=${toDate}&patientId=${patientId}`)
      .then((r) => setAppointments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId, fromDate, toDate]);

  return (
    <div className="space-y-4" dir="rtl">
      {/* Filter */}
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="text-xs text-gray-500 block mb-1">من تاريخ</label>
          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
          />
        </div>
        <div>
          <label className="text-xs text-gray-500 block mb-1">إلى تاريخ</label>
          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-teal"
          />
        </div>
        <Link
          href={`/appointments/new?patientId=${patientId}&patientName=${encodeURIComponent(patientName)}`}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          موعد جديد
        </Link>
      </div>

      {/* List */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-14 bg-gray-100 rounded-lg" />
          ))}
        </div>
      ) : appointments.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Calendar className="w-10 h-10 mx-auto mb-2 opacity-30" />
          <p className="text-sm">لا توجد مواعيد في الفترة المحددة</p>
        </div>
      ) : (
        <div className="space-y-2">
          {appointments.map((apt) => (
            <Link key={apt.id} href={`/appointments/${apt.id}`}
              className="flex items-center justify-between p-3 bg-white border border-gray-100 rounded-lg hover:border-gray-200 transition"
            >
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center flex-shrink-0">
                  <Calendar className="w-5 h-5 text-blue-500" />
                </div>
                <div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-gray-900">
                      {formatArabicDate(apt.date)}
                    </span>
                    <span className="text-xs text-gray-500" dir="ltr">
                      {formatTime(apt.startTime)} – {formatTime(apt.endTime)}
                    </span>
                  </div>
                  {apt.doctorName && (
                    <p className="text-xs text-gray-500">{apt.doctorName}</p>
                  )}
                  {apt.treatmentType && (
                    <p className="text-xs text-gray-400">{apt.treatmentType}</p>
                  )}
                </div>
              </div>
              <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", STATUS_COLORS[apt.status] ?? "bg-gray-100 text-gray-600")}>
                {APPOINTMENT_STATUS_LABELS[apt.status] ?? apt.status}
              </span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
