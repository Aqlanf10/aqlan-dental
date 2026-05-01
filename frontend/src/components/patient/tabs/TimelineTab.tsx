"use client";

import { useEffect, useState } from "react";
import { Clock } from "lucide-react";
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
    <div className="relative">
      <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-[#f1f5f9]" />
      <div className="space-y-4">
        {events.map((ev) => (
          <div key={ev.id} className="flex gap-4 relative">
            <div className="w-10 h-10 rounded-full bg-white border-2 border-clinic-blue flex items-center justify-center flex-shrink-0 z-10">
              <Clock className="w-4 h-4 text-clinic-blue" />
            </div>
            <div className="flex-1 bg-[#f7fafd] rounded-lg p-3 border border-[#e8f0f9]">
              <div className="flex items-center justify-between gap-2 flex-wrap">
                <span className="font-semibold text-sm text-[#0d2137]">{ev.title}</span>
                {ev.status && (
                  <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", STATUS_COLORS[ev.status] ?? "bg-[#f1f5f9] text-[#64748b]")}>
                    {APPOINTMENT_STATUS_LABELS[ev.status] ?? ev.status}
                  </span>
                )}
              </div>
              <p className="text-xs text-[#64748b] mt-0.5">{ev.description}</p>
              <p className="text-xs text-[#94a3b8] mt-1">{formatArabicDate(ev.date)}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
