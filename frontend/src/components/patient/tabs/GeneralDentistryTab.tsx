"use client";

import { useEffect, useState } from "react";
import { Grid3x3 } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { DentalChart } from "@/components/dental/DentalChart";
import { TreatmentHistory } from "@/components/dental/TreatmentHistory";

interface GeneralTreatmentDto {
  id: string;
  toothNumber?: string;
  treatmentType?: string;
  status?: string;
  date?: string;
  doctorName?: string;
  notes?: string;
}

interface GeneralDentistryTabProps {
  patientId: string;
}

export function GeneralDentistryTab({ patientId }: GeneralDentistryTabProps) {
  const [treatments, setTreatments] = useState<GeneralTreatmentDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<GeneralTreatmentDto[]>(`/api/general-treatments/${patientId}`)
      .then((r) => setTreatments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  return (
    <div className="space-y-6" dir="rtl">
      {/* Dental Chart */}
      <div>
        <DentalChart patientId={patientId} />
      </div>

      {/* Treatments List */}
      <div className="border-t border-gray-100 pt-6">
        <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
          <Grid3x3 className="w-4 h-4 text-clinic-blue" />
          العلاجات العامة
        </h3>
        {loading ? (
          <div className="space-y-2 animate-pulse">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-12 bg-gray-100 rounded-lg" />
            ))}
          </div>
        ) : treatments.length === 0 ? (
          <p className="text-sm text-gray-400">لا توجد علاجات عامة مسجّلة</p>
        ) : (
          <div className="space-y-2">
            {treatments.map((t) => (
              <div key={t.id} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-100">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-clinic-blue-50 flex items-center justify-center flex-shrink-0">
                    <Grid3x3 className="w-4 h-4 text-clinic-blue" />
                  </div>
                  <div>
                    <div className="flex items-center gap-2 flex-wrap">
                      {t.toothNumber && <span className="text-xs bg-blue-50 text-blue-600 px-1.5 py-0.5 rounded">سن {t.toothNumber}</span>}
                      <span className="text-sm font-medium text-gray-900">{t.treatmentType ?? "علاج"}</span>
                    </div>
                    {t.doctorName && <p className="text-xs text-gray-400">{t.doctorName}</p>}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {t.status && (
                    <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                      t.status === "completed" ? "bg-green-50 text-green-700" :
                      t.status === "in_progress" ? "bg-yellow-50 text-yellow-700" :
                      "bg-gray-100 text-gray-500"
                    )}>
                      {t.status === "completed" ? "مكتمل" : t.status === "in_progress" ? "جارٍ" : t.status}
                    </span>
                  )}
                  {t.date && <span className="text-xs text-gray-400">{t.date}</span>}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Treatment History */}
      <div className="border-t border-gray-100 pt-6">
        <TreatmentHistory patientId={patientId} />
      </div>
    </div>
  );
}
