"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Activity } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

interface OrthoCaseDto {
  id: string;
  caseNumber: string;
  applianceType?: string;
  status: string;
  stagePercentage: number;
  doctorName?: string;
  startDate?: string;
}

const ORTHO_STATUS_LABELS: Record<string, string> = { active: "نشطة", completed: "مكتملة", cancelled: "ملغاة" };

interface OrthodonticsTabProps {
  patientId: string;
}

export function OrthodonticsTab({ patientId }: OrthodonticsTabProps) {
  const [cases, setCases] = useState<OrthoCaseDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<OrthoCaseDto[]>(`/api/ortho-cases?patientId=${patientId}`)
      .then((r) => setCases(r.data))
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
        <Activity className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا توجد حالات تقويمية</p>
      </div>
    );
  }

  return (
    <div className="space-y-2" dir="rtl">
      {cases.map((c) => (
        <Link key={c.id} href={`/ortho/${c.id}`}
          className="flex items-center justify-between p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-clinic-blue-100 hover:bg-clinic-blue-50/30 transition"
        >
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-purple-50 flex items-center justify-center flex-shrink-0">
              <Activity className="w-5 h-5 text-purple-500" />
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-sm font-medium text-[#0d2137]">{c.caseNumber}</span>
                {c.applianceType && <span className="text-xs text-[#64748b]">{c.applianceType}</span>}
              </div>
              {c.doctorName && <p className="text-xs text-[#94a3b8]">{c.doctorName}</p>}
              {c.startDate && <p className="text-xs text-[#94a3b8]">{c.startDate}</p>}
            </div>
          </div>
          <div className="flex items-center gap-3 flex-shrink-0">
            <div className="flex items-center gap-1.5">
              <div className="w-20 h-1.5 bg-[#dce8f5] rounded-full overflow-hidden">
                <div className="h-full bg-clinic-blue rounded-full" style={{ width: `${c.stagePercentage}%` }} />
              </div>
              <span className="text-xs text-[#64748b]">{c.stagePercentage}%</span>
            </div>
            <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
              c.status === "active" ? "bg-clinic-blue-50 text-clinic-blue" : "bg-[#f1f5f9] text-[#64748b]"
            )}>
              {ORTHO_STATUS_LABELS[c.status] ?? c.status}
            </span>
          </div>
        </Link>
      ))}
    </div>
  );
}
