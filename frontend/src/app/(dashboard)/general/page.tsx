"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { Stethoscope, UserPlus } from "lucide-react";
import type { GeneralTreatment } from "@/types/dental";
import type { PatientListItem } from "@/types/patient";
import api from "@/lib/api";
import { formatYemeniRiyal, formatArabicDate } from "@/lib/utils";
import { PatientCombobox } from "@/components/shared/PatientCombobox";

export default function GeneralPage() {
  const [treatments, setTreatments] = useState<GeneralTreatment[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedPatient, setSelectedPatient] = useState<PatientListItem | null>(null);

  useEffect(() => {
    api.get<GeneralTreatment[]>("/api/general/recent-treatments?limit=30")
      .then((r) => setTreatments(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">طب الأسنان العام</h1>
          <p className="text-sm text-gray-500 mt-0.5">آخر المعالجات والمخططات السنية</p>
        </div>
      </div>

      {/* Quick patient navigator */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
        <p className="text-sm font-medium text-gray-700 mb-2">الانتقال السريع إلى ملف مريض</p>
        <div className="flex items-center gap-3">
          <div className="flex-1">
            <PatientCombobox
              onSelect={(p: PatientListItem) => setSelectedPatient(p)}
              placeholder="ابحث بالاسم أو رقم المريض..."
            />
          </div>
          {selectedPatient && (
            <Link
              href={`/patients/${selectedPatient.id}?tab=chart`}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 transition whitespace-nowrap"
            >
              <UserPlus className="w-4 h-4" />
              {selectedPatient.fullName}
            </Link>
          )}
        </div>
      </div>

      {/* Recent treatments */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h2 className="font-bold text-gray-900">آخر المعالجات</h2>
          <span className="text-xs text-gray-400">{treatments.length} معالجة</span>
        </div>

        {loading ? (
          <div className="p-5 space-y-2 animate-pulse">
            {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-lg" />)}
          </div>
        ) : treatments.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <Stethoscope className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p className="text-sm">لا توجد معالجات بعد</p>
            <p className="text-xs mt-1">ابحث عن مريض أعلاه للوصول لمخططه السني</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  {["التاريخ", "المريض", "نوع العلاج", "السن", "الطبيب", "التكلفة", ""].map((h) => (
                    <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {treatments.map((t) => (
                  <tr key={t.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3 text-gray-600 text-xs">{formatArabicDate(t.createdAt)}</td>
                    <td className="px-4 py-3 font-medium text-gray-900">{t.patientName}</td>
                    <td className="px-4 py-3 text-gray-700">{t.treatmentType}</td>
                    <td className="px-4 py-3 font-mono text-gray-700">{t.toothNumber ?? "—"}</td>
                    <td className="px-4 py-3 text-gray-600">{t.doctorName ?? "—"}</td>
                    <td className="px-4 py-3 font-mono text-green-700">
                      {t.cost ? formatYemeniRiyal(t.cost) : "—"}
                    </td>
                    <td className="px-4 py-3">
                      <Link href={`/patients/${t.patientId}`} className="text-xs text-clinic-teal hover:underline font-medium">
                        ملف المريض
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
