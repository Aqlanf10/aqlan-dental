"use client";
import { useState } from "react";
import Link from "next/link";
import { ChevronRight, ChevronLeft, CalendarDays, Plus } from "lucide-react";
import { DaySchedule } from "@/components/appointments/DaySchedule";
import { formatArabicDate } from "@/lib/utils";

function toDateStr(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

export default function AppointmentsPage() {
  const [date, setDate] = useState(() => toDateStr(new Date()));

  const shift = (days: number) => {
    const d = new Date(date);
    d.setDate(d.getDate() + days);
    setDate(toDateStr(d));
  };

  const isToday = date === toDateStr(new Date());

  return (
    <div className="space-y-5 max-w-3xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">المواعيد</h1>
          <p className="text-sm text-gray-500 mt-0.5">جدول المواعيد اليومي</p>
        </div>
        <Link
          href="/appointments/new"
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition"
        >
          <Plus className="w-4 h-4" />
          موعد جديد
        </Link>
      </div>

      {/* Date Navigator */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <div className="flex items-center gap-3">
          <button
            onClick={() => shift(-1)}
            className="p-1.5 rounded-lg hover:bg-gray-100 transition text-gray-500"
            aria-label="اليوم السابق"
          >
            <ChevronRight className="w-5 h-5" />
          </button>

          <div className="flex-1 flex items-center gap-3">
            <CalendarDays className="w-5 h-5 text-clinic-teal flex-shrink-0" />
            <div>
              <div className="font-bold text-gray-900">
                {formatArabicDate(date)}
              </div>
              {isToday && (
                <span className="text-xs text-clinic-teal font-medium">اليوم</span>
              )}
            </div>
          </div>

          <input
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            className="text-sm border border-gray-200 rounded-lg px-2 py-1.5 text-gray-700 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          />

          <button
            onClick={() => shift(1)}
            className="p-1.5 rounded-lg hover:bg-gray-100 transition text-gray-500"
            aria-label="اليوم التالي"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>

          {!isToday && (
            <button
              onClick={() => setDate(toDateStr(new Date()))}
              className="text-xs px-3 py-1.5 rounded-lg border border-clinic-teal text-clinic-teal hover:bg-teal-50 transition font-medium"
            >
              اليوم
            </button>
          )}
        </div>
      </div>

      {/* Schedule */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
        <DaySchedule date={date} />
      </div>
    </div>
  );
}
