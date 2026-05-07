"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Scissors } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface SurgeryCaseDto {
  id: string;
  caseNumber: string;
  surgeryType: string;
  status: string;
  doctorName?: string;
  scheduledDate?: string;
  notes?: string;
}

const SURGERY_STATUS_LABELS: Record<string, string> = {
  scheduled: "مجدولة",
  in_progress: "جارية",
  completed: "مكتملة",
  cancelled: "ملغاة",
};

interface SurgeryTabProps {
  patientId: string;
}

export function SurgeryTab({ patientId }: SurgeryTabProps) {
  const [cases, setCases] = useState<SurgeryCaseDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<{ data: SurgeryCaseDto[] }>(`/api/surgery-cases?patientId=${patientId}`)
      .then((r) => setCases(r.data.data ?? []))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  if (cases.length === 0) {
    return (
      <div className="text-center py-12 text-[#94a3b8]" dir="rtl">
        <Scissors className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا توجد حالات جراحية</p>
      </div>
    );
  }

  return (
    <div className="space-y-2" dir="rtl">
      {cases.map((c) => (
        <Link key={c.id} href={`/surgery/${c.id}`}
          className="flex items-center justify-between p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-red-200 hover:bg-red-50/30 transition"
        >
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-red-50 flex items-center justify-center flex-shrink-0">
              <Scissors className="w-5 h-5 text-red-500" />
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-sm font-medium text-[#0d2137]">{c.caseNumber}</span>
                <span className="text-xs text-[#64748b]">{c.surgeryType}</span>
              </div>
              {c.doctorName && <p className="text-xs text-[#94a3b8]">{c.doctorName}</p>}
              {c.scheduledDate && <p className="text-xs text-[#94a3b8]">{c.scheduledDate}</p>}
            </div>
          </div>
          <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium flex-shrink-0",
            c.status === "completed" ? "bg-green-50 text-green-700" :
            c.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
            c.status === "scheduled" ? "bg-[#3d7ab518] text-[#3d7ab5]" :
            "bg-[#f1f5f9] text-[#64748b]"
          )}>
            {SURGERY_STATUS_LABELS[c.status] ?? c.status}
          </span>
        </Link>
      ))}
    </div>
  );
}
