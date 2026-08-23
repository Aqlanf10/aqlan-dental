"use client";

import { useQuery } from "@tanstack/react-query";
import { CalendarDays, Phone } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

/**
 * Today's schedule, phone-sized.
 *
 * Reads /api/appointments/today rather than the general list endpoint so the server decides
 * what "today" means. The clinic is UTC+3 and this app is most likely to be opened in the
 * evening — the exact conditions under which a browser-side date turns into yesterday.
 */

interface TodayAppointment {
  id: string;
  patientName?: string;
  patientPhone?: string;
  doctorName?: string;
  startTime?: string;
  endTime?: string;
  status?: string;
  serviceName?: string;
  notes?: string;
}

const STATUS_LABEL: Record<string, { text: string; className: string }> = {
  Scheduled: { text: "مجدول", className: "bg-blue-50 text-blue-700" },
  Confirmed: { text: "مؤكد", className: "bg-emerald-50 text-emerald-700" },
  Arrived: { text: "وصل", className: "bg-cyan-50 text-cyan-700" },
  InProgress: { text: "قيد العلاج", className: "bg-amber-50 text-amber-700" },
  Completed: { text: "اكتمل", className: "bg-gray-100 text-gray-600" },
  Cancelled: { text: "ملغى", className: "bg-red-50 text-red-700" },
  NoShow: { text: "لم يحضر", className: "bg-red-50 text-red-700" },
};

/** Trims a "14:30:00" or ISO time down to what a person reads off a schedule. */
function shortTime(value?: string): string {
  if (!value) return "—";
  const match = value.match(/(\d{1,2}):(\d{2})/);
  return match ? `${match[1]}:${match[2]}` : value;
}

export default function MobileAppointmentsPage() {
  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ["m", "appointments", "today"],
    queryFn: async () => {
      const res = await api.get<TodayAppointment[] | { data: TodayAppointment[] }>(
        "/api/appointments/today",
      );
      return Array.isArray(res.data) ? res.data : (res.data?.data ?? []);
    },
  });

  const appointments = data ?? [];

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h1 className="text-base font-bold text-gray-900">مواعيد اليوم</h1>
        <button
          type="button"
          onClick={() => refetch()}
          className="text-xs text-cyan-700 font-medium px-2 py-1 -m-1"
        >
          {isFetching ? "جارٍ التحديث…" : "تحديث"}
        </button>
      </div>

      {isLoading ? (
        <p className="text-sm text-gray-500 py-8 text-center">جارٍ التحميل…</p>
      ) : isError ? (
        // Distinguished from "no appointments" on purpose: an empty schedule and a failed
        // request look identical if you only show a blank list, and on a phone that is the
        // difference between "nothing today" and "you are not seeing today".
        <div className="bg-white rounded-xl p-5 text-center space-y-3">
          <p className="text-sm text-red-700">تعذّر تحميل المواعيد.</p>
          <button
            type="button"
            onClick={() => refetch()}
            className="text-sm font-bold text-cyan-700 min-h-11 px-4"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : appointments.length === 0 ? (
        <div className="bg-white rounded-xl p-8 text-center text-gray-400">
          <CalendarDays className="w-8 h-8 mx-auto mb-2" />
          <p className="text-sm font-medium">لا توجد مواعيد اليوم</p>
        </div>
      ) : (
        <ul className="space-y-2">
          {appointments.map((appt) => {
            const status = STATUS_LABEL[appt.status ?? ""] ?? {
              text: appt.status ?? "—",
              className: "bg-gray-100 text-gray-600",
            };
            return (
              <li key={appt.id} className="bg-white rounded-xl p-3 shadow-sm">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-bold text-gray-900 text-sm truncate">
                      {appt.patientName ?? "—"}
                    </p>
                    <p className="text-xs text-gray-500 truncate">
                      {appt.serviceName ?? "—"}
                      {appt.doctorName ? ` · ${appt.doctorName}` : ""}
                    </p>
                  </div>
                  <span className="text-sm font-bold text-cyan-800 shrink-0 tabular-nums">
                    {shortTime(appt.startTime)}
                  </span>
                </div>

                <div className="flex items-center justify-between gap-2 mt-2">
                  <span className={cn("px-2 py-0.5 rounded-full text-[11px] font-medium", status.className)}>
                    {status.text}
                  </span>
                  {appt.patientPhone && (
                    // A tel: link rather than a copy button — chasing a no-show is the single
                    // most common reason to pull the schedule out on a phone.
                    <a
                      href={`tel:${appt.patientPhone}`}
                      className="inline-flex items-center gap-1 text-xs font-medium text-cyan-700 min-h-11 px-2"
                    >
                      <Phone className="w-4 h-4" />
                      اتصال
                    </a>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
