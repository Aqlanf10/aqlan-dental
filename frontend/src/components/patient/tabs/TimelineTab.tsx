"use client";

import { useEffect, useState } from "react";
import { Clock, Stethoscope, CreditCard, FolderOpen, Image as ImageIcon, ScanLine } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";
// FE-09: centralized appointment status colors (includes 'signed' for mixed timeline views)
import { APPOINTMENT_STATUS_COLORS as STATUS_COLORS } from "@/lib/statusStyles";

interface TimelineEvent {
  type: string;
  id: string;
  date: string;
  title: string;
  description: string;
  status?: string;
}

const TYPE_ICONS: Record<string, typeof Clock> = {
  appointment: Clock,
  visit: Stethoscope,
  payment: CreditCard,
  document: FolderOpen,
  photo: ImageIcon,
  radiograph: ScanLine,
};

const TYPE_COLORS: Record<string, string> = {
  appointment: "border-[#3d7ab5] text-[#3d7ab5]",
  visit: "border-green-500 text-green-600",
  payment: "border-[#f5922e] text-[#f5922e]",
  document: "border-[#a855f7] text-[#a855f7]",
  photo: "border-[#3d7ab5] text-[#3d7ab5]",
  radiograph: "border-purple-500 text-purple-600",
};

const TYPE_LABELS: Record<string, string> = {
  appointment: "موعد",
  visit: "زيارة",
  payment: "دفعة",
  document: "مستند",
  photo: "صورة",
  radiograph: "أشعة",
};

const TYPE_BADGE_COLORS: Record<string, string> = {
  appointment: "bg-[#3d7ab518] text-[#3d7ab5]",
  visit: "bg-green-100 text-green-700",
  payment: "bg-orange-100 text-orange-700",
  document: "bg-purple-100 text-purple-700",
  photo: "bg-blue-100 text-blue-700",
  radiograph: "bg-purple-100 text-purple-700",
};

interface TimelineTabProps {
  patientId: string;
}

const FILTER_OPTIONS = [
  { value: "all", label: "الكل" },
  { value: "appointment", label: "مواعيد" },
  { value: "visit", label: "زيارات" },
  { value: "payment", label: "مدفوعات" },
  { value: "document", label: "مستندات" },
  { value: "photo", label: "صور" },
  { value: "radiograph", label: "أشعة" },
] as const;

export function TimelineTab({ patientId }: TimelineTabProps) {
  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);
  const [filterType, setFilterType] = useState("all");

  useEffect(() => {
    setFetchError(false);
    api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`)
      .then((r) => setEvents(r.data))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  if (fetchError) {
    return (
      <div className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }

  if (!events.length) {
    return (
      <EmptyState
        icon={Clock}
        title="لا يوجد سجل زمني"
        description="ستظهر هنا جميع الأنشطة المتعلقة بالمريض"
      />
    );
  }

  const filteredEvents = filterType === "all"
    ? events
    : events.filter((ev) => ev.type === filterType);

  return (
    <div dir="rtl">
      {/* Filter Bar */}
      <div className="flex flex-wrap gap-2 mb-4">
        {FILTER_OPTIONS.map((opt) => (
          <button
            key={opt.value}
            onClick={() => setFilterType(opt.value)}
            className={cn(
              "px-3 py-1.5 text-xs font-medium rounded-full transition",
              filterType === opt.value
                ? "bg-[#3d7ab5] text-white"
                : "bg-[#f7fafd] text-[#64748b] border border-[#e8f0f9]"
            )}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {/* Timeline Content */}
      {!filteredEvents.length ? (
        <EmptyState
          icon={Clock}
          title="لا توجد نتائج"
          description="لا توجد أحداث من النوع المحدد"
        />
      ) : (
        <div className="relative">
          <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-[#f1f5f9]" />
          <div className="space-y-4">
            {filteredEvents.map((ev) => {
          const Icon = TYPE_ICONS[ev.type] ?? Clock;
          const borderColor = TYPE_COLORS[ev.type] ?? "border-[#3d7ab5] text-[#3d7ab5]";
          const badgeColor = TYPE_BADGE_COLORS[ev.type] ?? "bg-[#3d7ab518] text-[#3d7ab5]";
          return (
            <div key={`${ev.type}-${ev.id}`} className="flex gap-4 relative">
              <div className={cn("w-10 h-10 rounded-full bg-white border-2 flex items-center justify-center flex-shrink-0 z-10", borderColor)}>
                <Icon className="w-4 h-4" />
              </div>
              <div className="flex-1 bg-[#f7fafd] rounded-xl p-3 border border-[#e8f0f9] hover:bg-white hover:shadow-sm transition">
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-sm text-[#0d2137]">{ev.title}</span>
                    <span className={cn("text-[10px] px-1.5 py-0.5 rounded font-medium", badgeColor)}>
                      {TYPE_LABELS[ev.type] ?? ev.type}
                    </span>
                  </div>
                  {ev.status && (
                    <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", STATUS_COLORS[ev.status] ?? "bg-[#f1f5f9] text-[#64748b]")}>
                      {ev.status === "signed" ? "موقّع" : (APPOINTMENT_STATUS_LABELS[ev.status] ?? ev.status)}
                    </span>
                  )}
                </div>
                <p className="text-xs text-[#64748b] mt-0.5">{ev.description}</p>
                <p className="text-xs text-[#94a3b8] mt-1">{formatArabicDate(ev.date)}</p>
              </div>
            </div>
          );
        })}
          </div>
        </div>
      )}
    </div>
  );
}
