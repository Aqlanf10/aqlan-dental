"use client";
import { useEffect, useState } from "react";
import { Pill, User, FileText } from "lucide-react";
import portalApi from "@/lib/portalApi";
import type { PatientPrescription } from "@/types/patientPortal";

export default function PortalPrescriptionsPage() {
  const [prescriptions, setPrescriptions] = useState<PatientPrescription[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    portalApi.get<PatientPrescription[]>("/api/portal/prescriptions?limit=50")
      .then((r) => setPrescriptions(r.data))
      .catch((err) => setError(err?.response?.data?.message || "حدث خطأ في تحميل الوصفات الطبية"))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="pb-24">
      {/* Header */}
      <div className="bg-white border-b border-gray-100 px-5 pt-10 pb-4">
        <h1 className="text-xl font-extrabold text-gray-900">الوصفات الطبية</h1>
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
              <div key={i} className="bg-white rounded-2xl h-32" />
            ))}
          </div>
        ) : prescriptions.length === 0 ? (
          <div className="text-center py-16">
            <Pill className="w-12 h-12 text-gray-300 mx-auto mb-3" />
            <p className="text-sm text-gray-500">لا توجد وصفات طبية</p>
            <p className="text-xs text-gray-400 mt-1">ستظهر هنا عند إصدار وصفة طبية لك</p>
          </div>
        ) : (
          prescriptions.map((p) => (
            <div key={p.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4">
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center">
                    <Pill className="w-4 h-4 text-purple-600" />
                  </div>
                  <div>
                    <p className="font-bold text-gray-900 text-sm">
                      {p.drugs.length > 0 ? p.drugs[0].name : "وصفة طبية"}
                      {p.drugs.length > 1 && (
                        <span className="text-gray-400 font-normal"> +{p.drugs.length - 1} أخرى</span>
                      )}
                    </p>
                    <p className="text-xs text-gray-400">{p.createdAt}</p>
                  </div>
                </div>
                <div className="text-end">
                  {p.doctorName && (
                    <span className="text-xs text-gray-500 flex items-center gap-1">
                      <User className="w-3 h-3" /> {p.doctorName}
                    </span>
                  )}
                </div>
              </div>

              {/* Diagnosis */}
              {p.diagnosis && (
                <div className="text-xs text-gray-600 bg-gray-50 rounded-lg p-2 mb-3">
                  <span className="font-medium text-gray-500">التشخيص: </span>{p.diagnosis}
                </div>
              )}

              {/* Drugs List */}
              {p.drugs.length > 0 && (
                <div className="space-y-2 mb-3">
                  {p.drugs.map((drug, i) => (
                    <div key={i} className="bg-blue-50/50 rounded-lg p-2.5">
                      <p className="text-sm font-medium text-gray-900">{drug.name}</p>
                      <div className="flex flex-wrap gap-1.5 mt-1">
                        {drug.dosage && (
                          <span className="text-[11px] bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">
                            الجرعة: {drug.dosage}
                          </span>
                        )}
                        {drug.frequency && (
                          <span className="text-[11px] bg-purple-100 text-purple-700 px-2 py-0.5 rounded-full">
                            التكرار: {drug.frequency}
                          </span>
                        )}
                        {drug.duration && (
                          <span className="text-[11px] bg-amber-100 text-amber-700 px-2 py-0.5 rounded-full">
                            المدة: {drug.duration}
                          </span>
                        )}
                      </div>
                      {drug.notes && (
                        <p className="text-[11px] text-gray-500 mt-1">{drug.notes}</p>
                      )}
                    </div>
                  ))}
                </div>
              )}

              {/* Instructions */}
              {p.instructions && (
                <div className="text-xs text-gray-600 bg-green-50 rounded-lg p-2 flex items-start gap-1.5">
                  <FileText className="w-3.5 h-3.5 text-green-600 mt-0.5 flex-shrink-0" />
                  <span>{p.instructions}</span>
                </div>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
