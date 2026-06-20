"use client";
import type { Prescription } from "@/types/prescription";
import { formatArabicDate } from "@/lib/utils";

interface Props {
  prescription: Prescription;
  clinicName?: string;
  clinicAddress?: string;
  clinicPhone?: string;
}

export function PrescriptionPrint({
  prescription,
  clinicName = "مركز د. عقلان الكامل لطب وتقويم الأسنان",
  clinicAddress = "تعز، اليمن — شارع التحرير الأعلى",
  clinicPhone = "04-253028",
}: Props) {
  return (
    <>
      <style>{`
        @media print {
          body > *:not(#prescription-print-root) { display: none !important; }
          #prescription-print-root { display: block !important; }
          @page { margin: 1cm; }
        }
      `}</style>

      <div id="prescription-print-root" className="bg-white font-sans">
        {/* Header */}
        <div className="border-b-2 border-gray-800 pb-4 mb-5 flex items-start justify-between">
          <div>
            <h1 className="text-xl font-extrabold text-gray-900">{clinicName}</h1>
            <p className="text-sm text-gray-500 mt-0.5">{clinicAddress} · {clinicPhone}</p>
          </div>
          <div className="text-left text-sm text-gray-500">
            <div>التاريخ: <span className="font-semibold text-gray-900">{formatArabicDate(prescription.createdAt)}</span></div>
            {prescription.doctorName && (
              <div className="mt-0.5">الطبيب: <span className="font-semibold text-gray-900">{prescription.doctorName}</span></div>
            )}
          </div>
        </div>

        {/* ℞ symbol */}
        <div className="flex items-center gap-3 mb-4">
          <span className="text-3xl font-bold text-gray-700 font-serif">℞</span>
          <div className="h-px flex-1 bg-gray-300" />
        </div>

        {/* Patient info */}
        <div className="bg-gray-50 rounded-lg p-3 mb-5 text-sm">
          <span className="text-gray-500">المريض: </span>
          <span className="font-bold text-gray-900">{prescription.patientName}</span>
          <span className="text-gray-400 font-mono me-3"> ({prescription.patientNumber})</span>
        </div>

        {/* Diagnosis */}
        {prescription.diagnosis && (
          <div className="mb-5 text-sm">
            <span className="font-semibold text-gray-700">التشخيص: </span>
            <span className="text-gray-900">{prescription.diagnosis}</span>
          </div>
        )}

        {/* Drugs */}
        <div className="space-y-3 mb-6">
          {prescription.drugs.map((drug, i) => (
            <div key={i} className="border border-gray-200 rounded-lg p-3">
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-start gap-2">
                  <span className="text-clinic-blue font-bold text-sm mt-0.5">{i + 1}.</span>
                  <div>
                    <p className="font-bold text-gray-900">{drug.name}</p>
                    <p className="text-sm text-gray-600 mt-0.5">
                      {drug.dose} · {drug.frequency} · {drug.duration}
                    </p>
                    {drug.notes && (
                      <p className="text-xs text-gray-500 mt-1 italic">{drug.notes}</p>
                    )}
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Notes */}
        {prescription.notes && (
          <div className="border-t border-gray-200 pt-3 text-sm text-gray-600">
            <span className="font-semibold">ملاحظات: </span>{prescription.notes}
          </div>
        )}

        {/* Signature */}
        <div className="mt-10 pt-4 border-t border-dashed border-gray-300 flex justify-between text-sm text-gray-400">
          <span>توقيع الطبيب: ___________________</span>
          <span>ختم المركز</span>
        </div>
      </div>
    </>
  );
}
