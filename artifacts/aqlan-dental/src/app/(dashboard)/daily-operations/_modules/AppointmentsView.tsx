
import { useState } from "react";
import { useDoctors } from "@/hooks/useDoctors";
import {
  CalendarDays, List, Calendar,
  LayoutGrid,
} from "lucide-react";
import { DaySchedule } from "@/components/appointments/DaySchedule";
import { WeekCalendar } from "@/components/appointments/WeekCalendar";
import { MonthCalendar } from "@/components/appointments/MonthCalendar";
import { UpcomingWidget } from "@/components/appointments/UpcomingWidget";
import { formatArabicDate, cn } from "@/lib/utils";
import { rtlBackIcon as RtlBackIcon, rtlNextIcon as RtlNextIcon } from "@/lib/rtlIcons";
import { NAVY, BLUE } from "../_lib/constants";

interface Doctor { id: string; name: string; color?: string; specialty?: string; }

const MONTHS_AR = ["يناير","فبراير","مارس","أبريل","مايو","يونيو","يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر"];

function toDateStr(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,"0")}-${String(d.getDate()).padStart(2,"0")}`;
}

type ViewMode = "day" | "week" | "month";

export default function AppointmentsView() {
  const [date, setDate] = useState(() => toDateStr(new Date()));
  const [view, setView] = useState<ViewMode>("day");
  const [doctorId, setDoctorId] = useState<string>("");
  // FE-13: useDoctors() replaces useState + fetch.
  const { data: doctors = [] } = useDoctors();

  const anchorDate = new Date(date + "T12:00:00");
  const shift = (days: number) => { const d = new Date(date + "T12:00:00"); d.setDate(d.getDate() + days); setDate(toDateStr(d)); };
  const shiftWeek = (weeks: number) => shift(weeks * 7);
  const shiftMonth = (months: number) => { const d = new Date(date + "T12:00:00"); d.setMonth(d.getMonth() + months); setDate(toDateStr(d)); };

  const isToday = date === toDateStr(new Date());
  const monthLabel = `${MONTHS_AR[anchorDate.getMonth()]} ${anchorDate.getFullYear()}`;

  const navigatePrev = () => { if (view === "day") shift(-1); else if (view === "week") shiftWeek(-1); else shiftMonth(-1); };
  const navigateNext = () => { if (view === "day") shift(1); else if (view === "week") shiftWeek(1); else shiftMonth(1); };

  return (
    <div className="flex flex-col h-full">
      {/* Controls bar */}
      <div className="flex-shrink-0 flex items-center gap-3 px-3 py-2 border-b" style={{ borderColor: "#f1f5f9", background: "#fff" }}>
        {/* Date nav */}
        <button onClick={navigatePrev} className="p-1.5 rounded-lg hover:bg-gray-100 transition" style={{ color: "#64748b" }}>
          <RtlBackIcon className="w-4 h-4" />
        </button>
        <div className="flex items-center gap-2">
          <CalendarDays className="w-4 h-4 flex-shrink-0" style={{ color: BLUE }} />
          <span className="text-sm font-bold" style={{ color: NAVY }}>
            {view === "month" ? monthLabel : formatArabicDate(date)}
          </span>
          {isToday && view !== "month" && <span className="text-[10px] font-bold px-1.5 py-0.5 rounded" style={{ background: BLUE + "15", color: BLUE }}>اليوم</span>}
        </div>
        <button onClick={navigateNext} className="p-1.5 rounded-lg hover:bg-gray-100 transition" style={{ color: "#64748b" }}>
          <RtlNextIcon className="w-4 h-4" />
        </button>
        <input type="date" value={date} onChange={e => setDate(e.target.value)}
          className="text-xs border rounded-lg px-2 py-1 outline-none" style={{ color: NAVY }} />
        {!isToday && (
          <button onClick={() => setDate(toDateStr(new Date()))}
            className="text-[11px] px-2 py-1 rounded-lg border font-medium" style={{ borderColor: BLUE + "40", color: BLUE }}>اليوم</button>
        )}

        <div className="flex-1" />

        {/* Doctor filter */}
        {doctors.length > 0 && (
          <div className="relative flex items-center">
            <select value={doctorId} onChange={e => setDoctorId(e.target.value)}
              className="text-xs rounded-lg px-2 py-1.5 outline-none border-0" style={{ background: "#f5f7fa", color: NAVY }}>
              <option value="">كل الأطباء</option>
              {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>
        )}

        {/* View toggle */}
        <div className="flex items-center rounded-lg border overflow-hidden" style={{ borderColor: "#e5e7eb" }}>
          {([["day","يومي",List],["week","أسبوعي",LayoutGrid],["month","شهري",Calendar]] as const).map(([v,l,I]) => (
            <button key={v} onClick={() => setView(v)}
              className={cn("flex items-center gap-1 px-2.5 py-1.5 text-[11px] font-bold transition",
                view === v ? "text-white" : "hover:bg-gray-50")}
              style={view === v ? { background: BLUE } : { color: "#64748b" }}>
              <I className="w-3 h-3" />{l}
            </button>
          ))}
        </div>
      </div>

      {/* Schedule content */}
      <div className="flex-1 overflow-auto p-3">
        <div className="grid grid-cols-1 xl:grid-cols-3 gap-3">
          <div className="xl:col-span-2 bg-white rounded-xl border p-4" style={{ borderColor: "#e5e7eb" }}>
            {view === "day" ? (
              <DaySchedule date={date} doctorId={doctorId || undefined} />
            ) : view === "week" ? (
              <WeekCalendar anchor={date} doctorId={doctorId || undefined} onDateClick={d => { setDate(d); setView("day"); }} />
            ) : (
              <MonthCalendar anchor={date} doctorId={doctorId || undefined} onDateClick={d => { setDate(d); setView("day"); }} />
            )}
          </div>
          <UpcomingWidget />
        </div>
      </div>
    </div>
  );
}
