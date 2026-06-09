"use client";

import { useEffect, useState } from "react";
import { Calendar, DollarSign, FileText, Grid3x3, User } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn } from "@/lib/utils";
import { DentalChart } from "@/components/dental/DentalChart";
import { TreatmentHistory } from "@/components/dental/TreatmentHistory";

interface GeneralTreatmentDto {
  id: string;
  patientId?: string;
  visitId?: string;
  toothNumber?: string;
  treatmentType?: string;
  materialUsed?: string;
  anesthesiaType?: string;
  cost?: number;
  status?: string;
  createdAt?: string;
  doctorName?: string;
  notes?: string;
}

interface GeneralDentistryTabProps {
  patientId: string;
}

function statusLabel(status?: string) {
  if (status === "completed") return "مكتمل";
  if (status === "in_progress") return "جارٍ";
  return status;
}

export function GeneralDentistryTab({ patientId }: GeneralDentistryTabProps) {
  const [treatments, setTreatments] = useState<GeneralTreatmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setLoading(true);
    setFetchError(false);
    api.get<GeneralTreatmentDto[]>(`/api/general-treatments/${patientId}`)
      .then((r) => setTreatments(r.data))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);

  return (
    <div className="space-y-6" dir="rtl">
      <DentalChart patientId={patientId} />

      <div className="border-t border-[#e8f0f9] pt-6">
        <h3 className="text-sm font-semibold text-[#0d2137] mb-3 flex items-center gap-2">
          <Grid3x3 className="w-4 h-4 text-clinic-blue" />
          علاجات طب الأسنان العام
        </h3>

        {loading ? (
          <div className="space-y-2 animate-pulse">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-24 bg-[#f1f5f9] rounded-xl" />
            ))}
          </div>
        ) : fetchError ? (
          <div className="p-4 text-center">
            <p className="text-sm text-red-600 mb-2">فشل في تحميل بيانات طب الأسنان العام</p>
            <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">
              إعادة المحاولة
            </button>
          </div>
        ) : treatments.length === 0 ? (
          <EmptyState
            icon={Grid3x3}
            title="لا توجد علاجات أسنان عامة"
            description="لم يتم تسجيل أي علاجات أسنان عامة لهذا المريض"
          />
        ) : (
          <div className="space-y-3">
            {treatments.map((t) => (
              <div key={t.id} className="p-4 bg-[#f7fafd] rounded-xl border border-[#e8f0f9]">
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-xl bg-clinic-blue-50 flex items-center justify-center flex-shrink-0">
                      <Grid3x3 className="w-5 h-5 text-clinic-blue" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        {t.toothNumber && (
                          <span className="text-xs bg-[#3d7ab518] text-[#3d7ab5] px-2 py-0.5 rounded-full font-bold">
                            سن {t.toothNumber}
                          </span>
                        )}
                        <span className="text-sm font-semibold text-[#0d2137]">
                          {t.treatmentType ?? "علاج أسنان عام"}
                        </span>
                        {t.visitId && (
                          <span className="text-[10px] bg-emerald-50 text-emerald-700 px-2 py-0.5 rounded-full font-semibold">
                            مرتبط بالزيارة
                          </span>
                        )}
                      </div>
                      <div className="flex items-center gap-3 mt-1 flex-wrap text-xs text-[#64748b]">
                        {t.doctorName && (
                          <span className="flex items-center gap-1">
                            <User className="w-3 h-3" />
                            {t.doctorName}
                          </span>
                        )}
                        {t.createdAt && (
                          <span className="flex items-center gap-1">
                            <Calendar className="w-3 h-3" />
                            {t.createdAt}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 flex-shrink-0">
                    {t.cost != null && t.cost > 0 && (
                      <span className="inline-flex items-center gap-1 text-xs font-bold text-[#3d7ab5] bg-white border border-[#d6e8fa] rounded-full px-2 py-1">
                        <DollarSign className="w-3 h-3" />
                        {t.cost.toLocaleString()} ر.ي
                      </span>
                    )}
                    {t.status && (
                      <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                        t.status === "completed" ? "bg-green-50 text-green-700" :
                        t.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
                        "bg-[#f1f5f9] text-[#64748b]"
                      )}>
                        {statusLabel(t.status)}
                      </span>
                    )}
                  </div>
                </div>

                {(t.materialUsed || t.anesthesiaType) && (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mt-3">
                    {t.materialUsed && (
                      <div className="rounded-lg bg-white border border-[#e8f0f9] p-2">
                        <p className="text-[10px] text-[#94a3b8] font-semibold">المادة المستخدمة</p>
                        <p className="text-xs text-[#0d2137] font-medium">{t.materialUsed}</p>
                      </div>
                    )}
                    {t.anesthesiaType && (
                      <div className="rounded-lg bg-white border border-[#e8f0f9] p-2">
                        <p className="text-[10px] text-[#94a3b8] font-semibold">التخدير</p>
                        <p className="text-xs text-[#0d2137] font-medium">{t.anesthesiaType}</p>
                      </div>
                    )}
                  </div>
                )}

                {t.notes && (
                  <div className="mt-3 rounded-lg bg-white border border-[#e8f0f9] p-3">
                    <div className="flex items-center gap-1.5 mb-1.5">
                      <FileText className="w-3.5 h-3.5 text-[#3d7ab5]" />
                      <span className="text-xs font-semibold text-[#3d7ab5]">التفاصيل السريرية</span>
                    </div>
                    <p className="text-xs leading-6 text-[#334155] whitespace-pre-line">{t.notes}</p>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="border-t border-[#e8f0f9] pt-6">
        <TreatmentHistory patientId={patientId} />
      </div>
    </div>
  );
}
