"use client";
import { useEffect, useState } from "react";
import { Stethoscope, ChevronLeft } from "lucide-react";
import portalApi from "@/lib/portalApi";
import type { PatientTreatment, PatientPrescription } from "@/types/patientPortal";
import { cn } from "@/lib/utils";
import Link from "next/link";

type Tab = "treatments" | "prescriptions";

export default function PortalTreatmentsPage() {
  const [tab, setTab] = useState<Tab>("treatments");
  const [treatments, setTreatments] = useState<PatientTreatment[]>([]);
  const [prescriptions, setPrescriptions] = useState<PatientPrescription[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    if (tab === "treatments") {
      portalApi.get<PatientTreatment[]>("/api/portal/treatments?limit=50")
        .then((r) => setTreatments(r.data))
        .catch(() => {})
        .finally(() => setLoading(false));
    } else {
      portalApi.get<PatientPrescription[]>("/api/portal/prescriptions?limit=50")
        .then((r) => setPrescriptions(r.data))
        .catch(() => {})
        .finally(() => setLoading(false));
    }
  }, [tab]);

  return (
    <div className="pb-20">
      {/* Header */}
      <div className="bg-white border-b border-gray-100 px-5 pt-10 pb-4">
        <div className="flex items-center gap-2 mb-4">
          <Link href="/portal" className="text-gray-400 hover:text-gray-600">
            <ChevronLeft className="w-5 h-5" />
          </Link>
          <h1 className="text-xl font-extrabold text-gray-900">العلاجات والوصفات</h1>
        </div>

        <div className="flex gap-2">
          <button
            onClick={() => setTab("treatments")}
            className={cn(
              "px-4 py-1.5 text-xs font-medium rounded-full transition",
              tab === "treatments" ? "bg-teal-100 text-teal-700" : "bg-gray-100 text-gray-500"
            )}
          >
            العلاجات
          </button>
          <button
            onClick={() => setTab("prescriptions")}
            className={cn(
              "px-4 py-1.5 text-xs font-medium rounded-full transition",
              tab === "prescriptions" ? "bg-teal-100 text-teal-700" : "bg-gray-100 text-gray-500"
            )}
          >
            الوصفات
          </button>
        </div>
      </div>

      <div className="px-4 mt-4 space-y-3">
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
              <p className="text-sm text-gray-500">لا توجد علاجات مسجلة</p>
            </div>
          ) : (
            treatments.map((t) => (
              <div key={t.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
                <div className="flex items-start justify-between">
                  <div>
                    <p className="font-bold text-gray-900">{t.treatmentType}</p>
                    {t.toothNumber && (
                      <p className="text-xs text-gray-500 mt-0.5">سن رقم {t.toothNumber}</p>
                    )}
                  </div>
                  <span className="text-xs text-gray-400">{t.createdAt}</span>
                </div>
                {(t.materialUsed || t.doctorName) && (
                  <div className="flex items-center gap-3 mt-2 text-xs text-gray-500">
                    {t.materialUsed && <span>المادة: {t.materialUsed}</span>}
                    {t.doctorName && <span>الطبيب: {t.doctorName}</span>}
                  </div>
                )}
                {t.notes && (
                  <p className="text-xs text-gray-400 mt-2 bg-gray-50 rounded-lg p-2">{t.notes}</p>
                )}
              </div>
            ))
          )
        ) : prescriptions.length === 0 ? (
          <div className="text-center py-16">
            <p className="text-sm text-gray-500">لا توجد وصفات طبية</p>
          </div>
        ) : (
          prescriptions.map((p) => (
            <div key={p.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
              <div className="flex items-start justify-between">
                <p className="font-bold text-gray-900">{p.medicationName}</p>
                <span className="text-xs text-gray-400">{p.createdAt}</span>
              </div>
              <div className="flex flex-wrap gap-2 mt-2">
                {p.dosage && (
                  <span className="text-xs bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full">الجرعة: {p.dosage}</span>
                )}
                {p.frequency && (
                  <span className="text-xs bg-purple-50 text-purple-700 px-2 py-0.5 rounded-full">التكرار: {p.frequency}</span>
                )}
                {p.duration && (
                  <span className="text-xs bg-amber-50 text-amber-700 px-2 py-0.5 rounded-full">المدة: {p.duration}</span>
                )}
              </div>
              {p.instructions && (
                <p className="text-xs text-gray-500 mt-2">{p.instructions}</p>
              )}
              {p.doctorName && (
                <p className="text-xs text-gray-400 mt-1">الطبيب: {p.doctorName}</p>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
