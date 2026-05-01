"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Calendar, Activity, Wallet, Pill, Clock, Scissors,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate, APPOINTMENT_STATUS_LABELS } from "@/lib/utils";

interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number;
  totalOutstanding: number;
  prescriptionsCount: number;
}

interface TimelineEvent {
  type: string;
  id: string;
  date: string;
  title: string;
  description: string;
  status?: string;
}

interface OrthoCase {
  id: string;
  caseNumber: string;
  applianceType?: string;
  status: string;
  stagePercentage: number;
  doctorName?: string;
}

interface SurgeryCase {
  id: string;
  caseNumber: string;
  surgeryType: string;
  status: string;
  doctorName?: string;
}

const ORTHO_STATUS_LABELS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };
const SURGERY_STATUS_LABELS: Record<string, string> = { scheduled: "مجدولة", in_progress: "جارية", completed: "مكتملة", cancelled: "ملغاة" };

const STATUS_COLORS: Record<string, string> = {
  Scheduled: "bg-blue-100 text-blue-700",
  Confirmed: "bg-clinic-blue-50 text-clinic-blue",
  Arrived: "bg-yellow-100 text-yellow-700",
  InProgress: "bg-purple-100 text-purple-700",
  Completed: "bg-green-100 text-green-700",
  Cancelled: "bg-gray-100 text-gray-500",
  NoShow: "bg-red-100 text-red-700",
};

interface OverviewTabProps {
  patientId: string;
  summary: PatientSummary | null;
}

export function OverviewTab({ patientId, summary }: OverviewTabProps) {
  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [orthoCases, setOrthoCases] = useState<OrthoCase[]>([]);
  const [surgeryCases, setSurgeryCases] = useState<SurgeryCase[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`).then((r) => r.data).catch(() => []),
      api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=5`).then((r) => r.data).catch(() => []),
      api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientId}&pageSize=5`).then((r) => r.data.data ?? []).catch(() => []),
    ]).then(([timeline, ortho, surgery]) => {
      setEvents(timeline);
      setOrthoCases(ortho);
      setSurgeryCases(surgery);
      setLoading(false);
    });
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-20 bg-gray-100 rounded-lg" />
        <div className="h-32 bg-gray-100 rounded-lg" />
      </div>
    );
  }

  const statCards: { icon: LucideIcon; label: string; value: string | number; color: string; bg: string }[] = [
    { icon: Calendar, label: "المواعيد", value: summary?.totalAppointments ?? "—", color: "text-blue-600", bg: "bg-blue-50" },
    { icon: Calendar, label: "مكتملة", value: summary?.completedAppointments ?? "—", color: "text-green-600", bg: "bg-green-50" },
    { icon: Activity, label: "تقويم نشط", value: summary?.activeOrthoCases ?? "—", color: "text-purple-600", bg: "bg-purple-50" },
    { icon: Wallet, label: "مدفوع", value: summary ? `${summary.totalPaid.toLocaleString()}` : "—", color: "text-clinic-blue", bg: "bg-clinic-blue-50" },
    { icon: Wallet, label: "متبقي", value: summary ? `${summary.totalOutstanding.toLocaleString()}` : "—", color: "text-orange-600", bg: "bg-orange-50" },
    { icon: Pill, label: "الوصفات", value: summary?.prescriptionsCount ?? "—", color: "text-rose-600", bg: "bg-rose-50" },
  ];

  return (
    <div className="space-y-6" dir="rtl">
      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        {statCards.map(({ icon: Icon, label, value, color, bg }) => (
          <div key={label} className={cn("rounded-lg px-3 py-2 flex items-center gap-2", bg)}>
            <Icon className={cn("w-4 h-4 flex-shrink-0", color)} />
            <div className="min-w-0">
              <p className="text-xs text-gray-500 truncate">{label}</p>
              <p className={cn("text-sm font-bold truncate", color)}>{value}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Cases */}
      {(orthoCases.length > 0 || surgeryCases.length > 0) && (
        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-gray-700">الحالات النشطة</h3>
          {orthoCases.length > 0 && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">الحالات التقويمية</p>
              {orthoCases.map((c) => (
                <Link key={c.id} href={`/ortho/${c.id}`}
                  className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg hover:bg-clinic-blue-50 hover:border-clinic-blue-100 border border-transparent transition"
                >
                  <div className="flex items-center gap-2">
                    <Activity className="w-3.5 h-3.5 text-clinic-blue flex-shrink-0" />
                    <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                    {c.applianceType && <span className="text-xs text-gray-500">{c.applianceType}</span>}
                  </div>
                  <div className="flex items-center gap-3">
                    <div className="flex items-center gap-1.5">
                      <div className="w-16 h-1.5 bg-gray-200 rounded-full overflow-hidden">
                        <div className="h-full bg-clinic-blue rounded-full" style={{ width: `${c.stagePercentage}%` }} />
                      </div>
                      <span className="text-xs text-gray-500">{c.stagePercentage}%</span>
                    </div>
                    <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                      c.status === "active" ? "bg-clinic-blue-50 text-clinic-blue" : "bg-gray-100 text-gray-500"
                    )}>
                      {ORTHO_STATUS_LABELS[c.status] ?? c.status}
                    </span>
                  </div>
                </Link>
              ))}
            </div>
          )}
          {surgeryCases.length > 0 && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">الحالات الجراحية</p>
              {surgeryCases.map((c) => (
                <Link key={c.id} href={`/surgery/${c.id}`}
                  className="flex items-center justify-between p-2.5 bg-gray-50 rounded-lg hover:bg-red-50 hover:border-red-200 border border-transparent transition"
                >
                  <div className="flex items-center gap-2">
                    <Scissors className="w-3.5 h-3.5 text-red-600 flex-shrink-0" />
                    <span className="text-sm font-medium text-gray-900">{c.caseNumber}</span>
                    <span className="text-xs text-gray-500">{c.surgeryType}</span>
                  </div>
                  <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                    c.status === "completed" ? "bg-green-50 text-green-700" :
                    c.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
                    "bg-gray-100 text-gray-500"
                  )}>
                    {SURGERY_STATUS_LABELS[c.status] ?? c.status}
                  </span>
                </Link>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Recent Activity */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-3">النشاط الأخير</h3>
        {events.length === 0 ? (
          <p className="text-sm text-gray-400">لا يوجد نشاط بعد</p>
        ) : (
          <div className="relative">
            <div className="absolute right-[19px] top-0 bottom-0 w-0.5 bg-gray-100" />
            <div className="space-y-3">
              {events.slice(0, 5).map((ev) => (
                <div key={ev.id} className="flex gap-3 relative">
                  <div className="w-8 h-8 rounded-full bg-white border-2 border-clinic-blue flex items-center justify-center flex-shrink-0 z-10">
                    <Clock className="w-3.5 h-3.5 text-clinic-blue" />
                  </div>
                  <div className="flex-1 bg-gray-50 rounded-lg p-2.5 border border-gray-100">
                    <div className="flex items-center justify-between gap-2 flex-wrap">
                      <span className="font-semibold text-xs text-gray-900">{ev.title}</span>
                      {ev.status && (
                        <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", STATUS_COLORS[ev.status] ?? "bg-gray-100 text-gray-600")}>
                          {APPOINTMENT_STATUS_LABELS[ev.status] ?? ev.status}
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-500 mt-0.5">{ev.description}</p>
                    <p className="text-xs text-gray-400 mt-1">{formatArabicDate(ev.date)}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
