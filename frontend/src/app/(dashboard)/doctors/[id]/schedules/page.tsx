"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowRight,
  CalendarClock,
  CheckCircle,
  ChevronDown,
  ChevronUp,
  Clock,
  Copy,
  Loader2,
  AlertTriangle,
  Save,
  Trash2,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useDoctor } from "@/hooks/useDoctors";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

// ─── Types ────────────────────────────────────────────────────────────────────

interface ScheduleEntry {
  id: string;
  doctorId: string;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  isWorking: boolean;
  breakStart: string | null;
  breakEnd: string | null;
  slotDurationMinutes: number;
}

interface DaySchedule {
  dayOfWeek: number;
  isWorking: boolean;
  startTime: string;
  endTime: string;
  breakStart: string | null;
  breakEnd: string | null;
  slotDurationMinutes: number;
  /** ID from the API, if this day was previously saved */
  _id?: string;
}

type ScheduleMap = Record<number, DaySchedule>;

// ─── Constants ────────────────────────────────────────────────────────────────

const DAYS = [
  { index: 0, label: "الأحد", short: "أحد" },
  { index: 1, label: "الاثنين", short: "اثن" },
  { index: 2, label: "الثلاثاء", short: "ثلا" },
  { index: 3, label: "الأربعاء", short: "أرب" },
  { index: 4, label: "الخميس", short: "خمي" },
  { index: 5, label: "الجمعة", short: "جمع" },
  { index: 6, label: "السبت", short: "سبت" },
];

const SLOT_DURATIONS = [
  { value: 15, label: "15 دقيقة" },
  { value: 20, label: "20 دقيقة" },
  { value: 30, label: "30 دقيقة" },
  { value: 45, label: "45 دقيقة" },
  { value: 60, label: "ساعة" },
  { value: 90, label: "ساعة ونصف" },
  { value: 120, label: "ساعتان" },
];

const SPECIALTY_LABELS: Record<string, string> = {
  orthodontics: "تقويم الأسنان",
  general: "طب الأسنان العام",
  surgery: "جراحة الوجه والفكين",
  pediatric: "طب أسنان الأطفال",
  periodontics: "أمراض اللثة",
  endodontics: "علاج الجذور",
};

const DEFAULT_DAY: DaySchedule = {
  dayOfWeek: 0,
  isWorking: false,
  startTime: "09:00",
  endTime: "17:00",
  breakStart: null,
  breakEnd: null,
  slotDurationMinutes: 30,
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeDefaultSchedule(): ScheduleMap {
  return Object.fromEntries(
    DAYS.map((d) => [d.index, { ...DEFAULT_DAY, dayOfWeek: d.index }])
  );
}

function fromApiSchedule(apiData: ScheduleEntry[]): ScheduleMap {
  const map = makeDefaultSchedule();
  for (const day of apiData) {
    map[day.dayOfWeek] = {
      dayOfWeek: day.dayOfWeek,
      isWorking: day.isWorking,
      startTime: day.startTime ?? "09:00",
      endTime: day.endTime ?? "17:00",
      breakStart: day.breakStart ?? null,
      breakEnd: day.breakEnd ?? null,
      slotDurationMinutes: day.slotDurationMinutes ?? 30,
      _id: day.id,
    };
  }
  return map;
}

function getErrorMessage(err: unknown): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string } } }).response;
    if (resp?.data?.message) return resp.data.message;
  }
  if (err instanceof Error) return err.message;
  return "حدث خطأ غير متوقع";
}

// ─── DayCard Component ────────────────────────────────────────────────────────

function DayCard({
  day,
  schedule,
  onChange,
  onDelete,
  onCopyFrom,
  isDeleting,
}: {
  day: { index: number; label: string; short: string };
  schedule: DaySchedule;
  onChange: (updated: Partial<DaySchedule>) => void;
  onDelete: () => void;
  onCopyFrom: (sourceDayIndex: number) => void;
  isDeleting: boolean;
}) {
  const [showBreak, setShowBreak] = useState(!!schedule.breakStart);
  const [showCopyMenu, setShowCopyMenu] = useState(false);

  // Sync break visibility when schedule changes externally (e.g., copy from another day)
  useEffect(() => {
    if (schedule.breakStart) {
      setShowBreak(true);
    }
  }, [schedule.breakStart]);

  const otherDays = DAYS.filter((d) => d.index !== day.index);

  return (
    <div
      className={cn(
        "rounded-xl border transition-colors",
        schedule.isWorking
          ? "border-[#3d7ab5]/30 bg-[#3d7ab5]/5"
          : "border-gray-200 bg-gray-50/50"
      )}
    >
      {/* Day header row */}
      <div className="flex items-center gap-3 px-4 py-3 flex-wrap">
        {/* Day toggle button */}
        <button
          type="button"
          onClick={() => {
            if (schedule.isWorking) {
              onChange({ isWorking: false });
            } else {
              onChange({ isWorking: true });
            }
          }}
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
          {schedule.isWorking ? "يوم عمل" : "إجازة"}
        </button>

        {/* Day name label */}
        <span
          className={cn(
            "text-sm font-bold",
            schedule.isWorking ? "text-[#3d7ab5]" : "text-gray-400"
          )}
        >
          {day.label}
        </span>

        {schedule.isWorking && (
          <>
            {/* Start time */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">من</span>
              <input
                type="time"
                value={schedule.startTime}
                onChange={(e) => onChange({ startTime: e.target.value })}
                className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] bg-white"
              />
            </div>

            {/* End time */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">إلى</span>
              <input
                type="time"
                value={schedule.endTime}
                onChange={(e) => onChange({ endTime: e.target.value })}
                className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] bg-white"
              />
            </div>

            {/* Slot duration */}
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-gray-500">مدة الحجز</span>
              <select
                value={schedule.slotDurationMinutes}
                onChange={(e) =>
                  onChange({ slotDurationMinutes: Number(e.target.value) })
                }
                className="border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] bg-white"
              >
                {SLOT_DURATIONS.map((d) => (
                  <option key={d.value} value={d.value}>
                    {d.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Actions row */}
            <div className="flex items-center gap-1 ms-auto">
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
                {showBreak ? (
                  <ChevronUp className="w-3 h-3" />
                ) : (
                  <ChevronDown className="w-3 h-3" />
                )}
                {showBreak ? "إخفاء الاستراحة" : "إضافة استراحة"}
              </button>

              {/* Copy button */}
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setShowCopyMenu((v) => !v)}
                  className="flex items-center gap-1 text-xs text-gray-500 hover:text-[#3d7ab5] transition px-2 py-1 rounded-lg hover:bg-[#3d7ab5]/5"
                  title="نسخ من يوم آخر"
                >
                  <Copy className="w-3 h-3" />
                  نسخ
                </button>
                {showCopyMenu && (
                  <>
                    {/* Invisible backdrop to close menu */}
                    <div
                      className="fixed inset-0 z-10"
                      onClick={() => setShowCopyMenu(false)}
                    />
                    <div className="absolute top-full end-0 mt-1 bg-white border border-gray-200 rounded-lg shadow-lg py-1 z-20 min-w-[140px]">
                      <div className="px-3 py-1.5 text-[10px] text-gray-400 font-medium">
                        نسخ جدول من:
                      </div>
                      {otherDays.map((d) => (
                        <button
                          key={d.index}
                          type="button"
                          onClick={() => {
                            onCopyFrom(d.index);
                            setShowCopyMenu(false);
                          }}
                          className="w-full text-start px-3 py-1.5 text-xs text-gray-700 hover:bg-[#3d7ab5]/5 hover:text-[#3d7ab5] transition"
                        >
                          {d.label}
                        </button>
                      ))}
                    </div>
                  </>
                )}
              </div>

              {/* Delete button */}
              {schedule._id && (
                <button
                  type="button"
                  onClick={onDelete}
                  disabled={isDeleting}
                  className="flex items-center gap-1 text-xs text-gray-400 hover:text-red-500 transition px-2 py-1 rounded-lg hover:bg-red-50 disabled:opacity-50"
                  title="حذف جدول هذا اليوم"
                >
                  {isDeleting ? (
                    <Loader2 className="w-3 h-3 animate-spin" />
                  ) : (
                    <Trash2 className="w-3 h-3" />
                  )}
                  حذف
                </button>
              )}
            </div>
          </>
        )}

        {!schedule.isWorking && (
          <span className="text-xs text-gray-400 italic">إجازة</span>
        )}
      </div>

      {/* Break times */}
      {schedule.isWorking && showBreak && (
        <div className="flex items-center gap-3 px-4 pb-3 ps-2">
          <span className="text-xs text-amber-600 font-medium">استراحة:</span>
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-gray-500">من</span>
            <input
              type="time"
              value={schedule.breakStart ?? "13:00"}
              onChange={(e) => onChange({ breakStart: e.target.value })}
              className="border border-amber-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white"
            />
          </div>
          <div className="flex items-center gap-1.5">
            <span className="text-xs text-gray-500">إلى</span>
            <input
              type="time"
              value={schedule.breakEnd ?? "14:00"}
              onChange={(e) => onChange({ breakEnd: e.target.value })}
              className="border border-amber-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white"
            />
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Page Component ───────────────────────────────────────────────────────────

export default function DoctorSchedulesPage() {
  const params = useParams();
  const router = useRouter();
  const doctorId = params.id as string;

  // ── Data ──
  const {
    data: doctor,
    isLoading: doctorLoading,
    isError: doctorError,
  } = useDoctor(doctorId);

  const [schedule, setSchedule] = useState<ScheduleMap>(makeDefaultSchedule());
  const [scheduleLoading, setScheduleLoading] = useState(true);
  const [scheduleError, setScheduleError] = useState<string | null>(null);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [deleteDayIndex, setDeleteDayIndex] = useState<number | null>(null);

  // ── Load schedule ──
  useEffect(() => {
    if (!doctorId) return;

    setScheduleLoading(true);
    setScheduleError(null);
    api
      .get(`/api/doctors/${doctorId}/schedule`)
      .then(({ data }) => {
        setSchedule(fromApiSchedule(data));
      })
      .catch(() => setScheduleError("تعذّر تحميل جدول الدوام"))
      .finally(() => setScheduleLoading(false));
  }, [doctorId]);

  // ── Day change handler ──
  const handleDayChange = useCallback(
    (dayIndex: number, updates: Partial<DaySchedule>) => {
      setSchedule((prev) => ({
        ...prev,
        [dayIndex]: { ...prev[dayIndex], ...updates },
      }));
      setSaved(false);
      setScheduleError(null);
    },
    []
  );

  // ── Copy from another day ──
  const handleCopyFrom = useCallback(
    (targetDayIndex: number, sourceDayIndex: number) => {
      setSchedule((prev) => {
        const source = prev[sourceDayIndex];
        if (!source) return prev;
        return {
          ...prev,
          [targetDayIndex]: {
            ...prev[targetDayIndex],
            isWorking: source.isWorking,
            startTime: source.startTime,
            endTime: source.endTime,
            breakStart: source.breakStart,
            breakEnd: source.breakEnd,
            slotDurationMinutes: source.slotDurationMinutes,
          },
        };
      });
      setSaved(false);
      const sourceLabel = DAYS.find((d) => d.index === sourceDayIndex)?.label;
      const targetLabel = DAYS.find((d) => d.index === targetDayIndex)?.label;
      toast.success(`تم نسخ جدول ${sourceLabel} إلى ${targetLabel}`);
    },
    []
  );

  // ── Delete a single day's schedule ──
  const handleDeleteDay = useCallback(
    async (dayIndex: number) => {
      const daySchedule = schedule[dayIndex];
      if (!daySchedule?._id) return;

      setDeleteDayIndex(dayIndex);
      try {
        await api.delete(`/api/doctor-schedules/${daySchedule._id}`);
        setSchedule((prev) => ({
          ...prev,
          [dayIndex]: {
            ...prev[dayIndex],
            isWorking: false,
            breakStart: null,
            breakEnd: null,
            _id: undefined,
          },
        }));
        const dayLabel = DAYS.find((d) => d.index === dayIndex)?.label;
        toast.success(`تم حذف جدول يوم ${dayLabel} بنجاح`);
      } catch (err) {
        toast.error(getErrorMessage(err));
      } finally {
        setDeleteDayIndex(null);
      }
    },
    [schedule]
  );

  // ── Save all ──
  const handleSaveAll = useCallback(async () => {
    setSaving(true);
    setScheduleError(null);
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

      await api.put(`/api/doctors/${doctorId}/schedule`, body);
      setSaved(true);
      toast.success("تم حفظ جدول الدوام بالكامل بنجاح");

      // Re-fetch to get the new IDs
      const { data } = await api.get(`/api/doctors/${doctorId}/schedule`);
      setSchedule(fromApiSchedule(data));

      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      const msg = getErrorMessage(err);
      setScheduleError(msg);
      toast.error(msg);
    } finally {
      setSaving(false);
    }
  }, [schedule, doctorId]);

  // ── Computed ──
  const workingDays = useMemo(
    () => Object.values(schedule).filter((d) => d.isWorking),
    [schedule]
  );

  const totalWorkingHours = useMemo(() => {
    let total = 0;
    for (const day of workingDays) {
      const [sh, sm] = day.startTime.split(":").map(Number);
      const [eh, em] = day.endTime.split(":").map(Number);
      let hours = eh + em / 60 - (sh + sm / 60);
      // Subtract break
      if (day.breakStart && day.breakEnd) {
        const [bsh, bsm] = day.breakStart.split(":").map(Number);
        const [beh, bem] = day.breakEnd.split(":").map(Number);
        hours -= beh + bem / 60 - (bsh + bsm / 60);
      }
      if (hours > 0) total += hours;
    }
    return total;
  }, [workingDays]);

  const isLoading = doctorLoading || scheduleLoading;

  // ── Doctor info helpers ──
  const doctorName = doctor?.name ?? "";
  const doctorSpecialty = doctor?.specialty
    ? SPECIALTY_LABELS[doctor.specialty] ?? doctor.specialty
    : "";
  const doctorColor = doctor?.color ?? "#3d7ab5";
  const doctorInitials = doctor?.avatarInitials ?? doctorName.charAt(0) ?? "د";

  // ─── Render ─────────────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24 gap-3 text-gray-400">
        <Loader2 className="w-6 h-6 animate-spin" />
        <span>جارٍ تحميل بيانات الطبيب وجدول الدوام...</span>
      </div>
    );
  }

  if (doctorError) {
    return (
      <div className="flex items-center justify-center py-24">
        <div className="text-center">
          <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
          <p className="text-gray-600 font-medium mb-2">
            تعذّر تحميل بيانات الطبيب
          </p>
          <button
            onClick={() => router.push("/doctors")}
            className="text-sm text-[#3d7ab5] hover:underline font-medium"
          >
            العودة للأطباء
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* ── Header ── */}
      <div className="flex items-start justify-between flex-wrap gap-4">
        <div className="flex items-center gap-4">
          {/* Doctor avatar */}
          <div
            className="w-14 h-14 rounded-full flex items-center justify-center text-white text-lg font-bold flex-shrink-0 shadow-sm"
            style={{ backgroundColor: doctorColor }}
          >
            {doctorInitials}
          </div>
          <div>
            <h1 className="text-2xl font-bold text-[#0d2137]">
              جدول دوام: {doctorName}
            </h1>
            {doctorSpecialty && (
              <p className="text-sm text-gray-500 mt-0.5">
                {doctorSpecialty}
              </p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[#3d7ab5]/10 flex items-center justify-center">
            <CalendarClock className="w-5 h-5 text-[#3d7ab5]" />
          </div>
        </div>
      </div>

      {/* ── Back button ── */}
      <button
        onClick={() => router.push("/doctors")}
        className="inline-flex items-center gap-2 text-sm text-gray-500 hover:text-[#3d7ab5] transition font-medium"
      >
        <ArrowRight className="w-4 h-4" />
        العودة للأطباء
      </button>

      {/* ── Warning banner ── */}
      <div className="flex items-center gap-3 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">
        <AlertTriangle className="w-5 h-5 text-amber-500 flex-shrink-0" />
        <p className="text-sm text-amber-800 font-medium">
          تعديل جدول الطبيب قد يؤثر على توفر الحجز العام
        </p>
      </div>

      {/* ── Legend ── */}
      <div className="flex flex-wrap gap-4 text-xs text-gray-500">
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full bg-[#3d7ab5]" />
          يوم عمل نشط
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full bg-gray-200" />
          إجازة / يوم راحة
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full bg-amber-400" />
          وقت الاستراحة
        </div>
        <div className="ms-auto text-gray-400">
          <Clock className="w-3 h-3 inline me-1" />
          أيام العمل: <span className="font-semibold text-gray-600">{workingDays.length}</span>
          {" | "}
          إجمالي الساعات: <span className="font-semibold text-gray-600">{totalWorkingHours.toFixed(1)}</span>
        </div>
      </div>

      {/* ── Working days pills ── */}
      <div className="flex gap-1.5 flex-wrap">
        {DAYS.map((d) => (
          <span
            key={d.index}
            className={cn(
              "text-[11px] px-2.5 py-1 rounded-full font-semibold transition",
              schedule[d.index]?.isWorking
                ? "bg-[#3d7ab5] text-white"
                : "bg-gray-100 text-gray-400"
            )}
          >
            {d.short}
          </span>
        ))}
      </div>

      {/* ── Schedule content ── */}
      {scheduleError && !scheduleLoading ? (
        <div className="flex items-center justify-center py-24">
          <div className="text-center">
            <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
            <p className="text-gray-600 font-medium mb-2">{scheduleError}</p>
            <button
              onClick={() => window.location.reload()}
              className="text-sm text-[#3d7ab5] hover:underline font-medium"
            >
              إعادة المحاولة
            </button>
          </div>
        </div>
      ) : workingDays.length === 0 && !scheduleLoading ? (
        /* Empty state */
        <div className="text-center py-24 text-gray-400">
          <CalendarClock className="w-14 h-14 mx-auto mb-4 opacity-30" />
          <p className="font-semibold text-gray-500 text-lg mb-1">
            لا يوجد جدول دوام لهذا الطبيب
          </p>
          <p className="text-sm text-gray-400">
            قم بتفعيل أيام العمل بالضغط على زر &quot;إجازة&quot; لبدء إعداد الجدول
          </p>
        </div>
      ) : (
        /* Day cards */
        <div className="space-y-3">
          {DAYS.map((day) => (
            <DayCard
              key={day.index}
              day={day}
              schedule={schedule[day.index]}
              onChange={(updates) => handleDayChange(day.index, updates)}
              onDelete={() => handleDeleteDay(day.index)}
              onCopyFrom={(sourceDayIndex) =>
                handleCopyFrom(day.index, sourceDayIndex)
              }
              isDeleting={deleteDayIndex === day.index}
            />
          ))}
        </div>
      )}

      {/* ── Error message ── */}
      {scheduleError && !scheduleLoading && workingDays.length > 0 && (
        <div className="flex items-center gap-2 text-red-600 text-xs bg-red-50 rounded-lg px-3 py-2">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
          {scheduleError}
        </div>
      )}

      {/* ── Save bar ── */}
      <div className="sticky bottom-0 bg-white/80 backdrop-blur-md border-t border-gray-100 rounded-t-2xl px-6 py-4 flex items-center justify-between">
        <div>
          {saved && (
            <span className="flex items-center gap-1.5 text-emerald-600 text-sm font-medium">
              <CheckCircle className="w-4 h-4" />
              تم حفظ الجدول بنجاح
            </span>
          )}
        </div>

        <button
          onClick={handleSaveAll}
          disabled={saving}
          className={cn(
            "flex items-center gap-2 px-6 py-2.5 rounded-xl text-sm font-semibold transition shadow-sm",
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
          {saving ? "جارٍ الحفظ..." : "حفظ الجدول بالكامل"}
        </button>
      </div>
    </div>
  );
}
