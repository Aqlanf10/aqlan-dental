"use client";
import { useState, useEffect } from "react";
import { Clock, Save, Loader2, CheckCircle, AlertTriangle, ChevronDown, ChevronUp } from "lucide-react";
import { cn } from "@/lib/utils";
import api from "@/lib/api";
import { useDoctors } from "@/hooks/useDoctors";

// ─── Types ────────────────────────────────────────────────────────────────────

interface Doctor {
  id: string;
  name: string;
  specialty: string;
  color?: string;
  avatarInitials?: string;
}

interface DaySchedule {
  dayOfWeek: number;
  isWorking: boolean;
  startTime: string;
  endTime: string;
  breakStart: string | null;
  breakEnd: string | null;
  slotDurationMinutes: number;
}

type ScheduleMap = Record<number, DaySchedule>;

// ─── Constants ────────────────────────────────────────────────────────────────

const DAYS = [
  { index: 0, label: "الأحد",    short: "أحد" },
  { index: 1, label: "الاثنين",  short: "اثن" },
  { index: 2, label: "الثلاثاء", short: "ثلا" },
  { index: 3, label: "الأربعاء", short: "أرب" },
  { index: 4, label: "الخميس",   short: "خمي" },
  { index: 5, label: "الجمعة",   short: "جمع" },
  { index: 6, label: "السبت",    short: "سبت" },
];

const DEFAULT_DAY: DaySchedule = {
  dayOfWeek: 0,
  isWorking: false,
  startTime: "09:00",
  endTime: "17:00",
  breakStart: null,
  breakEnd: null,
  slotDurationMinutes: 30,
};

const SLOT_DURATIONS = [
  { value: 15, label: "15 دقيقة" },
  { value: 20, label: "20 دقيقة" },
  { value: 30, label: "30 دقيقة" },
  { value: 45, label: "45 دقيقة" },
  { value: 60, label: "ساعة" },
];

function makeDefaultSchedule(): ScheduleMap {
  return Object.fromEntries(
    DAYS.map((d) => [d.index, { ...DEFAULT_DAY, dayOfWeek: d.index }])
  );
}

function fromApiSchedule(apiData: DaySchedule[]): ScheduleMap {
  const map = makeDefaultSchedule();
  for (const day of apiData) {
    map[day.dayOfWeek] = {
      ...day,
      startTime: day.startTime ?? "09:00",
      endTime: day.endTime ?? "17:00",
      breakStart: day.breakStart ?? null,
      breakEnd: day.breakEnd ?? null,
      slotDurationMinutes: day.slotDurationMinutes ?? 30,
    };
  }
  return map;
}

// ─── DayRow ───────────────────────────────────────────────────────────────────

function DayRow({
  day,
  schedule,
  onChange,
}: {
  day: { index: number; label: string };
  schedule: DaySchedule;
  onChange: (updated: Partial<DaySchedule>) => void;
}) {
  const [showBreak, setShowBreak] = useState(!!schedule.breakStart);

  return (
    <div
      className={cn(
        "rounded-xl border px-4 py-3 transition-colors",
        schedule.isWorking
          ? "border-[#3d7ab5]/30 bg-[#3d7ab5]/5"
          : "border-gray-200 bg-gray-50/50"
      )}
    >
      <div className="flex items-center gap-3 flex-wrap">
        {/* Day toggle */}
        <button
          type="button"
          onClick={() => onChange({ isWorking: !schedule.isWorking })}
          className={cn(
            "flex items-center gap-2 min-w-[110px] px-3 py-1.5 rounded-lg text-sm font-semibold transition",
            schedule.isWorking
              ? "bg-[#3d7ab5] text-white"
              : "bg-gray-200 text-gray-500"
          )}
        >
          <span
            className={cn(
              "w-3 h-3 rounded-full",
              schedule.isWorking ? "bg-white" : "bg-gray-400"
            )}
          />
          {day.label}
        </button>

        {schedule.isWorking && (
          <>
            {/* Start time */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">من</span>
              <input
                type="time"
                value={schedule.startTime}
                onChange={(e) => onChange({ startTime: e.target.value })}
                className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]"
              />
            </div>

            {/* End time */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">إلى</span>
              <input
                type="time"
                value={schedule.endTime}
                onChange={(e) => onChange({ endTime: e.target.value })}
                className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]"
              />
            </div>

            {/* Slot duration */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">مدة الحجز</span>
              <select
                value={schedule.slotDurationMinutes}
                onChange={(e) => onChange({ slotDurationMinutes: Number(e.target.value) })}
                className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] bg-white"
              >
                {SLOT_DURATIONS.map((d) => (
                  <option key={d.value} value={d.value}>{d.label}</option>
                ))}
              </select>
            </div>

            {/* Break toggle */}
            <button
              type="button"
              onClick={() => {
                if (showBreak) {
                  onChange({ breakStart: null, breakEnd: null });
                  setShowBreak(false);
                } else {
                  onChange({ breakStart: "13:00", breakEnd: "14:00" });
                  setShowBreak(true);
                }
              }}
              className="flex items-center gap-1 text-xs text-gray-500 hover:text-[#3d7ab5] transition px-2 py-1 rounded-lg hover:bg-[#3d7ab5]/5"
            >
              {showBreak ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
              {showBreak ? "إخفاء الاستراحة" : "إضافة استراحة"}
            </button>
          </>
        )}

        {!schedule.isWorking && (
          <span className="text-xs text-gray-400 italic">إجازة</span>
        )}
      </div>

      {/* Break times */}
      {schedule.isWorking && showBreak && (
        <div className="flex items-center gap-3 mt-2 pr-2">
          <span className="text-xs text-amber-600 font-medium">استراحة:</span>
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-gray-500">من</span>
            <input
              type="time"
              value={schedule.breakStart ?? "13:00"}
              onChange={(e) => onChange({ breakStart: e.target.value })}
              className="border border-amber-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
          </div>
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-gray-500">إلى</span>
            <input
              type="time"
              value={schedule.breakEnd ?? "14:00"}
              onChange={(e) => onChange({ breakEnd: e.target.value })}
              className="border border-amber-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
          </div>
        </div>
      )}
    </div>
  );
}

// ─── DoctorScheduleCard ───────────────────────────────────────────────────────

function DoctorScheduleCard({ doctor }: { doctor: Doctor }) {
  const [schedule, setSchedule] = useState<ScheduleMap>(makeDefaultSchedule());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState(true);

  useEffect(() => {
    api.get(`/api/doctors/${doctor.id}/schedule`)
      .then(({ data }) => setSchedule(fromApiSchedule(data)))
      .catch(() => setError("تعذّر تحميل الجدول"))
      .finally(() => setLoading(false));
  }, [doctor.id]);

  const handleDayChange = (dayIndex: number, updates: Partial<DaySchedule>) => {
    setSchedule((prev) => ({
      ...prev,
      [dayIndex]: { ...prev[dayIndex], ...updates },
    }));
    setSaved(false);
    setError(null);
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const body = Object.values(schedule).map((day) => ({
        dayOfWeek: day.dayOfWeek,
        isWorking: day.isWorking,
        startTime: day.startTime,
        endTime: day.endTime,
        breakStart: day.breakStart ?? null,
        breakEnd: day.breakEnd ?? null,
        slotDurationMinutes: day.slotDurationMinutes,
      }));
      await api.put(`/api/doctors/${doctor.id}/schedule`, body);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch {
      setError("فشل حفظ الجدول. حاول مرة أخرى.");
    } finally {
      setSaving(false);
    }
  };

  const workingDays = Object.values(schedule).filter((d) => d.isWorking);

  return (
    <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm overflow-hidden">
      {/* Header */}
      <div
        className="flex items-center gap-3 px-5 py-4 cursor-pointer hover:bg-gray-50/50 transition"
        onClick={() => setExpanded((v) => !v)}
      >
        <div
          className="w-10 h-10 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0"
          style={{ backgroundColor: doctor.color ?? "#3d7ab5" }}
        >
          {doctor.avatarInitials ?? doctor.name.charAt(0)}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-semibold text-gray-900 text-sm">{doctor.name}</p>
          <p className="text-xs text-gray-400">{SPECIALTY_LABELS[doctor.specialty] ?? doctor.specialty}</p>
        </div>

        {/* Working days pills */}
        <div className="hidden sm:flex gap-1 flex-wrap">
          {DAYS.map((d) => (
            <span
              key={d.index}
              className={cn(
                "text-[10px] px-1.5 py-0.5 rounded font-medium",
                schedule[d.index]?.isWorking
                  ? "bg-[#3d7ab5] text-white"
                  : "bg-gray-100 text-gray-400"
              )}
            >
              {d.short}
            </span>
          ))}
        </div>

        <span className="text-xs text-gray-400 hidden sm:block ml-2">
          {workingDays.length} أيام عمل
        </span>
        {expanded ? (
          <ChevronUp className="w-4 h-4 text-gray-400 flex-shrink-0" />
        ) : (
          <ChevronDown className="w-4 h-4 text-gray-400 flex-shrink-0" />
        )}
      </div>

      {/* Body */}
      {expanded && (
        <div className="px-5 pb-5 border-t border-gray-100">
          {loading ? (
            <div className="flex items-center justify-center py-8 gap-2 text-gray-400">
              <Loader2 className="w-4 h-4 animate-spin" />
              <span className="text-sm">جارٍ التحميل...</span>
            </div>
          ) : (
            <>
              <div className="mt-4 space-y-2">
                {DAYS.map((day) => (
                  <DayRow
                    key={day.index}
                    day={day}
                    schedule={schedule[day.index]}
                    onChange={(updates) => handleDayChange(day.index, updates)}
                  />
                ))}
              </div>

              {error && (
                <div className="mt-3 flex items-center gap-2 text-red-600 text-xs bg-red-50 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
                  {error}
                </div>
              )}

              <div className="mt-4 flex items-center justify-between">
                {saved && (
                  <span className="flex items-center gap-1.5 text-emerald-600 text-xs">
                    <CheckCircle className="w-4 h-4" />
                    تم حفظ الجدول بنجاح
                  </span>
                )}
                <div className="flex-1" />
                <button
                  onClick={handleSave}
                  disabled={saving}
                  className={cn(
                    "flex items-center gap-2 px-5 py-2 rounded-xl text-sm font-semibold transition",
                    saving
                      ? "bg-gray-100 text-gray-400 cursor-not-allowed"
                      : "bg-[#3d7ab5] text-white hover:opacity-90"
                  )}
                >
                  {saving ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <Save className="w-4 h-4" />
                  )}
                  {saving ? "جارٍ الحفظ..." : "حفظ الجدول"}
                </button>
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Specialty labels ─────────────────────────────────────────────────────────

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم الأسنان",
  general: "طب الأسنان العام",
  surgery: "جراحة الوجه والفكين",
  pediatric: "طب أسنان الأطفال",
  periodontics: "أمراض اللثة",
  endodontics: "علاج الجذور",
};

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function SchedulePage() {
  // FE-13: useDoctors() replaces useState + fetch.
  const { data, isLoading: loading, isError } = useDoctors();
  // Local Doctor interface requires `specialty: string`; the hook's DoctorDto
  // types it as optional. The /api/doctors endpoint always returns a specialty
  // string, so we cast to satisfy the local type without changing it.
  const doctors = (data ?? []) as unknown as Doctor[];
  const error = isError ? "تعذّر تحميل قائمة الأطباء" : null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">جداول أوقات الأطباء</h1>
          <p className="text-sm text-gray-500 mt-1">
            حدّد أيام وساعات عمل كل طبيب لضبط الحجوزات
          </p>
        </div>
        <div className="w-10 h-10 rounded-xl bg-[#3d7ab5]/10 flex items-center justify-center">
          <Clock className="w-5 h-5 text-[#3d7ab5]" />
        </div>
      </div>

      {/* Legend */}
      <div className="flex flex-wrap gap-3 text-xs text-gray-500">
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full bg-[#3d7ab5]" />
          يوم عمل نشط
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full bg-gray-200" />
          إجازة / يوم راحة
        </div>
        <div className="flex items-center gap-1.5 mr-auto text-amber-600">
          <Clock className="w-3 h-3" />
          التعديلات تؤثر على متاحية الحجوزات فوراً
        </div>
      </div>

      {/* Content */}
      {loading ? (
        <div className="flex items-center justify-center py-24 gap-3 text-gray-400">
          <Loader2 className="w-6 h-6 animate-spin" />
          <span>جارٍ تحميل الأطباء...</span>
        </div>
      ) : error ? (
        <div className="flex items-center justify-center py-24">
          <div className="text-center">
            <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
            <p className="text-gray-600 font-medium">{error}</p>
          </div>
        </div>
      ) : doctors.length === 0 ? (
        <div className="text-center py-24 text-gray-400">
          <Clock className="w-12 h-12 mx-auto mb-3 opacity-40" />
          <p className="font-medium">لا يوجد أطباء في النظام</p>
        </div>
      ) : (
        <div className="space-y-4">
          {doctors.map((doctor) => (
            <DoctorScheduleCard key={doctor.id} doctor={doctor} />
          ))}
        </div>
      )}
    </div>
  );
}
