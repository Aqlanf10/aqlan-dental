"use client";

import { useEffect, useState } from "react";
import { Pill } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate } from "@/lib/utils";

interface PrescriptionDto {
  id: string;
  date: string;
  doctorName?: string;
  status?: string;
  notes?: string;
  items?: { medicationName: string; dosage: string; frequency: string; duration: string }[];
}

const PRESCRIPTION_STATUS_LABELS: Record<string, string> = {
  active: "نشطة",
  completed: "مكتملة",
  cancelled: "ملغاة",
  draft: "مسودة",
};

interface PrescriptionsTabProps {
  patientId: string;
}

export function PrescriptionsTab({ patientId }: PrescriptionsTabProps) {
  const [prescriptions, setPrescriptions] = useState<PrescriptionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setFetchError(false);
    api.get<PrescriptionDto[]>(`/api/prescriptions?patientId=${patientId}`)
      .then((r) => setPrescriptions(r.data))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
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

  if (prescriptions.length === 0) {
    return (
      <EmptyState
        icon={Pill}
        title="لا توجد وصفات طبية"
        description="لم يتم إصدار أي وصفات لهذا المريض"
        actionLabel="وصفة جديدة"
        actionHref={`/prescriptions/new?patientId=${patientId}`}
        actionVariant="outline"
      />
    );
  }

  return (
    <div className="space-y-3" dir="rtl">
      {prescriptions.map((rx) => (
        <div key={rx.id} className="p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-[#3d7ab5] hover:shadow-sm transition">
          <div className="flex items-center justify-between gap-2 flex-wrap mb-2">
            <div className="flex items-center gap-2">
              <Pill className="w-4 h-4 text-rose-500 flex-shrink-0" />
              <span className="text-sm font-medium text-[#0d2137]">{formatArabicDate(rx.date)}</span>
              {rx.doctorName && <span className="text-xs text-[#64748b]">{rx.doctorName}</span>}
            </div>
            {rx.status && (
              <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                rx.status === "active" ? "bg-green-50 text-green-700" :
                rx.status === "draft" ? "bg-yellow-50 text-yellow-700" :
                "bg-[#f1f5f9] text-[#64748b]"
              )}>
                {PRESCRIPTION_STATUS_LABELS[rx.status] ?? rx.status}
              </span>
            )}
          </div>
          {rx.items && rx.items.length > 0 && (
            <div className="space-y-1.5 mt-2">
              {rx.items.map((item, idx) => (
                <div key={idx} className="text-xs bg-[#f7fafd] rounded p-2">
                  <span className="font-semibold text-[#0d2137]">{item.medicationName}</span>
                  <span className="text-[#64748b] mx-1">—</span>
                  <span className="text-[#64748b]">{item.dosage}</span>
                  {item.frequency && <span className="text-[#94a3b8] mx-1">· {item.frequency}</span>}
                  {item.duration && <span className="text-[#94a3b8] mx-1">· {item.duration}</span>}
                </div>
              ))}
            </div>
          )}
          {rx.notes && <p className="text-xs text-[#64748b] mt-2">{rx.notes}</p>}
        </div>
      ))}
    </div>
  );
}
