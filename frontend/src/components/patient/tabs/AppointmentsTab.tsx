"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Calendar, Plus } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate, formatTime, APPOINTMENT_STATUS_LABELS, localDateString } from "@/lib/utils";
// FE-09: centralized appointment status colors
import { APPOINTMENT_STATUS_COLORS as STATUS_COLORS } from "@/lib/statusStyles";

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

interface AppointmentsTabProps {
  patientId: string;
  patientName: string;
}

export function AppointmentsTab({ patientId, patientName }: AppointmentsTabProps) {
  const [appointments, setAppointments] = useState<AppointmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  // Date range filter
  const today = new Date();
  const threeMonthsAgo = new Date(today);
  threeMonthsAgo.setMonth(threeMonthsAgo.getMonth() - 3);
  const threeMonthsLater = new Date(today);
  threeMonthsLater.setMonth(threeMonthsLater.getMonth() + 3);

  const [fromDate, setFromDate] = useState(localDateString(threeMonthsAgo));
  const [toDate, setToDate] = useState(localDateString(threeMonthsLater));

  useEffect(() => {
    setLoading(true);
    setFetchError(false);
    api.get<AppointmentDto[]>(`/api/appointments/patient/${patientId}`)
      .then((r) => setAppointments(r.data))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);

  const filtered = appointments.filter((a) => {
    if (fromDate && a.date < fromDate) return false;
    if (toDate && a.date > toDate) return false;
    return true;
  });

  return (
    <div className="space-y-4" dir="rtl">
      {/* Filter */}
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="text-xs text-[#64748b] block mb-1">من تاريخ</label>
          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
            className="text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue"
          />
        </div>
        <div>
          <label className="text-xs text-[#64748b] block mb-1">إلى تاريخ</label>
          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
            className="text-sm border border-[#e8f0f9] rounded-lg px-3 py-2 focus:outline-none focus:border-clinic-blue"
          />
        </div>
        <Link
          href={`/appointments/new?patientId=${patientId}&patientName=${encodeURIComponent(patientName)}`}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
        >
          <Plus className="w-3.5 h-3.5" />
          موعد جديد
        </Link>
      </div>

      {/* List */}
      {loading ? (
        <div className="space-y-2 animate-pulse">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-14 bg-[#f1f5f9] rounded-lg" />
          ))}
        </div>
      ) : fetchError ? (
        <div className="p-4 text-center">
          <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
          <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
        </div>
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={Calendar}
          title="لا توجد مواعيد"
          description="لم يتم حجز أي مواعيد لهذا المريض بعد"
          actionLabel="موعد جديد"
          actionHref={`/appointments/new?patientId=${patientId}&patientName=${encodeURIComponent(patientName)}`}
        />
      ) : (
        <div className="space-y-2">
          {filtered.map((apt) => (
            <Link key={apt.id} href={`/appointments/${apt.id}`}
              className="flex items-center justify-between p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-[#3d7ab5] hover:shadow-sm transition"
            >
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg bg-[#3d7ab518] flex items-center justify-center flex-shrink-0">
                  <Calendar className="w-5 h-5 text-[#3d7ab5]" />
                </div>
                <div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-[#0d2137]">
                      {formatArabicDate(apt.date)}
                    </span>
                    <span className="text-xs text-[#64748b]" dir="ltr">
                      {formatTime(apt.startTime)} – {formatTime(apt.endTime)}
                    </span>
                  </div>
                  {apt.doctorName && (
                    <p className="text-xs text-[#64748b]">{apt.doctorName}</p>
                  )}
                  {apt.treatmentType && (
                    <p className="text-xs text-[#94a3b8]">{apt.treatmentType}</p>
                  )}
                </div>
              </div>
              <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", STATUS_COLORS[apt.status] ?? "bg-[#f1f5f9] text-[#64748b]")}>
                {APPOINTMENT_STATUS_LABELS[apt.status] ?? apt.status}
              </span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
