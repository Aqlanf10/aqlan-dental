"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Pill } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { formatArabicDate } from "@/lib/utils";

// CORE-PAT-005: the real list payload from GET /api/prescriptions. The old
// shape (date/status/items[]) matched nothing the API sends — every field was
// undefined, and because the response is an envelope the component also called
// .map on a non-array and crashed the patient file. Drug details live only on
// GET /api/prescriptions/{id}, so the list links out instead of faking them.
interface PrescriptionDto {
  id: string;
  createdAt: string;
  doctorName?: string;
  diagnosis?: string;
  drugCount: number;
  notes?: string;
}

interface PrescriptionListResponse {
  data: PrescriptionDto[];
  total: number;
}

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
    setLoading(true);
    api.get<PrescriptionListResponse>(`/api/prescriptions?patientId=${patientId}`)
      .then((r) => setPrescriptions(r.data?.data ?? []))
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

  if (!Array.isArray(prescriptions) || prescriptions.length === 0) {
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
    <div className="space-y-3">
      {prescriptions.map((rx) => (
        <div key={rx.id} className="p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-[#3d7ab5] hover:shadow-sm transition">
          <div className="flex items-center justify-between gap-2 flex-wrap mb-2">
            <div className="flex items-center gap-2">
              <Pill className="w-4 h-4 text-rose-500 flex-shrink-0" />
              <span className="text-sm font-medium text-[#0d2137]">{formatArabicDate(rx.createdAt)}</span>
              {rx.doctorName && <span className="text-xs text-[#64748b]">{rx.doctorName}</span>}
            </div>
            <Link
              href={`/prescriptions/${rx.id}`}
              className="text-xs px-1.5 py-0.5 rounded-full font-medium bg-rose-50 text-rose-700 hover:bg-rose-100 transition"
            >
              {rx.drugCount} دواء — عرض وطباعة
            </Link>
          </div>
          {rx.diagnosis && <p className="text-xs text-[#64748b] mt-1">التشخيص: {rx.diagnosis}</p>}
          {rx.notes && <p className="text-xs text-[#64748b] mt-2">{rx.notes}</p>}
        </div>
      ))}
    </div>
  );
}
