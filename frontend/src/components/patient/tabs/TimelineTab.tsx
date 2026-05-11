"use client";

import { useEffect, useState } from "react";
import { Clock, Stethoscope, CreditCard, FolderOpen, Image as ImageIcon, ScanLine } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";

interface TimelineEvent {
  type: string;
  id: string;
  date: string;
  title: string;
  description: string;
  status?: string;
}

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-[#3d7ab518] text-[#3d7ab5]",
  Confirmed: "bg-clinic-blue-50 text-clinic-blue",
  Arrived: "bg-yellow-100 text-yellow-700",
  InProgress: "bg-purple-100 text-purple-700",
  Completed: "bg-green-100 text-green-700",
  Cancelled: "bg-[#f1f5f9] text-[#64748b]",
  NoShow: "bg-red-100 text-red-700",
  signed: "bg-green-100 text-green-700",
};

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

export function TimelineTab({ patientId }: TimelineTabProps) {
  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`)
      .then((r) => setEvents(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  if (!events.length) {
    return (
      <div className="text-center py-12 text-[#94a3b8]">
        <Clock className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا يوجد سجل زمني بعد</p>
      </div>
    );
  }

  return (
    <div className="relative" dir="rtl">
      <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-[#f1f5f9]" />
      <div className="space-y-4">
        {events.map((ev) => {
          const Icon = TYPE_ICONS[ev.type] ?? Clock;
          const borderColor = TYPE_COLORS[ev.type] ?? "border-[#3d7ab5] text-[#3d7ab5]";
          const badgeColor = TYPE_BADGE_COLORS[ev.type] ?? "bg-[#3d7ab518] text-[#3d7ab5]";
          return (
            <div key={`${ev.type}-${ev.id}`} className="flex gap-4 relative">
              <div className={cn("w-10 h-10 rounded-full bg-white border-2 flex items-center justify-center flex-shrink-0 z-10", borderColor)}>
                <Icon className="w-4 h-4" />
              </div>
              <div className="flex-1 bg-[#f7fafd] rounded-lg p-3 border border-[#e8f0f9]">
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
  );
}
