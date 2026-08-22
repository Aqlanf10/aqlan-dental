"use client";
import { useEffect, useState } from "react";
import { Stethoscope, Calendar, User, ClipboardList } from "lucide-react";
import portalApi from "@/lib/portalApi";
import type { PatientTreatment, PatientVisit } from "@/types/patientPortal";
import { cn } from "@/lib/utils";

type Tab = "treatments" | "visits";

const SPECIALTY_LABELS: Record<string, string> = {
  General: "عام",
  Orthodontics: "تقويم",
  OralSurgery: "جراحة فم",
  Periodontics: "لثة",
  Endodontics: "معالجة جذر",
  Prosthodontics: "تركيبات",
};

export default function PortalTreatmentsPage() {
  const [tab, setTab] = useState<Tab>("treatments");
  const [treatments, setTreatments] = useState<PatientTreatment[]>([]);
  const [visits, setVisits] = useState<PatientVisit[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    if (tab === "treatments") {
      portalApi.get<PatientTreatment[]>("/api/portal/treatments?limit=50")
        .then((r) => setTreatments(r.data))
        .catch((err) => setError(err?.response?.data?.message || "حدث خطأ في تحميل العلاجات"))
        .finally(() => setLoading(false));
    } else {
      portalApi.get<PatientVisit[]>("/api/portal/visits?limit=50")
        .then((r) => setVisits(r.data))
        .catch((err) => setError(err?.response?.data?.message || "حدث خطأ في تحميل الزيارات"))
        .finally(() => setLoading(false));
    }
  }, [tab]);

  return (
    <div className="pb-24">
      {/* Header */}
      <div className="bg-white border-b border-gray-100 px-5 pt-10 pb-4">
        <h1 className="text-xl font-extrabold text-gray-900 mb-4">العلاجات</h1>
        <div className="flex gap-2">
          <button
            onClick={() => setTab("treatments")}
            className={cn(
              "px-4 py-1.5 text-xs font-medium rounded-full transition",
              tab === "treatments" ? "bg-clinic-blue-50 text-clinic-blue" : "bg-gray-100 text-gray-500"
            )}
          >
            الإجراءات
          </button>
          <button
            onClick={() => setTab("visits")}
            className={cn(
              "px-4 py-1.5 text-xs font-medium rounded-full transition",
              tab === "visits" ? "bg-clinic-blue-50 text-clinic-blue" : "bg-gray-100 text-gray-500"
            )}
          >
            الزيارات
          </button>
        </div>
      </div>

      <div className="px-4 mt-4 space-y-3">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-2xl p-4 text-center">
            {error}
          </div>
        )}
        {loading ? (
          <div className="space-y-3 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="bg-white rounded-2xl h-24" />
            ))}
          </div>
        ) : tab === "treatments" ? (
          treatments.length === 0 ? (
            <div className="text-center py-16">
              <Stethoscope className="w-12 h-12 text-gray-300 mx-auto mb-3" />
              <p className="text-sm text-gray-500">لا توجد علاجات مسجلة حالياً</p>
              <p className="text-xs text-gray-400 mt-1">ستظهر هنا عند إجراء أي علاج</p>
            </div>
          ) : (
            treatments.map((t) => (
              <div key={t.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <p className="font-bold text-gray-900">{t.treatmentType}</p>
                      {t.toothNumber && (
                        <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full">
                          سن {t.toothNumber}
                        </span>
                      )}
                    </div>
                    {(t.materialUsed || t.doctorName || t.specialty) && (
                      <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-gray-500">
                        {t.materialUsed && <span>المادة: {t.materialUsed}</span>}
                        {t.doctorName && (
                          <span className="flex items-center gap-1">
                            <User className="w-3 h-3" /> {t.doctorName}
                          </span>
                        )}
                        {t.specialty && <span>{SPECIALTY_LABELS[t.specialty] || t.specialty}</span>}
                      </div>
                    )}
                  </div>
                  <div className="text-end">
                    <span className="text-xs text-gray-400">{t.visitDate || t.createdAt}</span>
                  </div>
                </div>
                {t.notes && (
                  <p className="text-xs text-gray-400 mt-2 bg-gray-50 rounded-lg p-2">{t.notes}</p>
                )}
              </div>
            ))
          )
        ) : visits.length === 0 ? (
          <div className="text-center py-16">
            <ClipboardList className="w-12 h-12 text-gray-300 mx-auto mb-3" />
            <p className="text-sm text-gray-500">لا توجد زيارات مسجلة حالياً</p>
            <p className="text-xs text-gray-400 mt-1">ستظهر هنا عند إتمام أي زيارة</p>
          </div>
        ) : (
          visits.map((v) => (
            <div key={v.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <Calendar className="w-4 h-4 text-clinic-blue" />
                  <span className="font-bold text-gray-900">{v.visitDate}</span>
                </div>
                {v.specialty && (
                  <span className="text-xs bg-clinic-blue-50 text-clinic-blue px-2 py-0.5 rounded-full">
                    {SPECIALTY_LABELS[v.specialty] || v.specialty}
                  </span>
                )}
              </div>
              {v.chiefComplaint && (
                <p className="text-sm text-gray-700 bg-gray-50 rounded-lg p-2 mb-2">
                  <span className="text-gray-500">الشكوى: </span>{v.chiefComplaint}
                </p>
              )}
              {v.treatmentDone && (
                <p className="text-sm text-gray-700 bg-blue-50 rounded-lg p-2 mb-2">
                  <span className="text-blue-500">العلاج: </span>{v.treatmentDone}
                </p>
              )}
              {v.instructions && (
                <p className="text-sm text-gray-600 bg-green-50 rounded-lg p-2 mb-2">
                  <span className="text-green-600">التعليمات: </span>{v.instructions}
                </p>
              )}
              <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-gray-500">
                {v.doctorName && (
                  <span className="flex items-center gap-1">
                    <User className="w-3 h-3" /> {v.doctorName}
                  </span>
                )}
                {v.treatmentCount > 0 && (
                  <span>{v.treatmentCount} إجراء</span>
                )}
              </div>
              {v.nextVisitPlan && (
                <div className="mt-2 pt-2 border-t border-gray-100">
                  <p className="text-xs text-clinic-orange font-medium">خطة الزيارة القادمة: {v.nextVisitPlan}</p>
                  {v.nextVisitDate && (
                    <p className="text-xs text-gray-400">الموعد: {v.nextVisitDate}</p>
                  )}
                </div>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
