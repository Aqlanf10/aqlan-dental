"use client";
import type { Prescription } from "@/types/prescription";

interface Props {
  prescription: Prescription;
  clinicName?: string;
  clinicAddress?: string;
  clinicPhone?: string;
  leadDoctor?: string;
  leadDoctorCredentials?: string;
}

// Spec 010 (RX-REQ-003): the printed prescription is in English — it leaves the
// clinic with the patient (pharmacies/external centers read English). Identity
// defaults mirror the settings fallbacks; callers pass useClinicBranding() EN
// fields so the form stays settings-driven.
export function PrescriptionPrint({
  prescription,
  clinicName = "Dr. Aqlan Alkamel Center for Orthodontics, Dental Implants & Cosmetic Dentistry",
  clinicAddress = "Upper Al-Tahrir Street, Taiz, Yemen",
  clinicPhone = "04-253028",
  leadDoctor = "Dr. Aqlan Alkamel — Orthodontic Specialist",
  leadDoctorCredentials = "Central University of Manila — Philippines",
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

      <div id="prescription-print-root" dir="ltr" className="bg-white font-sans text-left">
        {/* Header */}
        <div className="border-b-2 border-gray-800 pb-4 mb-5 flex items-start justify-between">
          <div>
            <h1 className="text-lg font-extrabold text-gray-900">{clinicName}</h1>
            <p className="text-sm font-semibold text-gray-700 mt-0.5">{leadDoctor}</p>
            <p className="text-xs text-gray-500">{leadDoctorCredentials}</p>
          </div>
          <div className="text-right text-sm text-gray-500 shrink-0">
            <div>Date: <span className="font-semibold text-gray-900">{prescription.createdAt}</span></div>
            {prescription.doctorName && (
              <div className="mt-0.5">Doctor: <span className="font-semibold text-gray-900">{prescription.doctorName}</span></div>
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
          <span className="text-gray-500">Patient: </span>
          <span className="font-bold text-gray-900">{prescription.patientName}</span>
          <span className="text-gray-400 font-mono ms-3"> ({prescription.patientNumber})</span>
        </div>

        {/* Diagnosis */}
        {prescription.diagnosis && (
          <div className="mb-5 text-sm">
            <span className="font-semibold text-gray-700">Diagnosis: </span>
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
            <span className="font-semibold">Notes: </span>{prescription.notes}
          </div>
        )}

        {/* Signature */}
        <div className="mt-10 pt-4 border-t border-dashed border-gray-300 flex justify-between text-sm text-gray-400">
          <span>Doctor&apos;s signature: ___________________</span>
          <span>Clinic stamp</span>
        </div>

        {/* Contact footer */}
        <div className="mt-6 pt-3 border-t border-gray-200 text-center text-xs text-gray-400">
          {clinicAddress} · Tel: {clinicPhone}
        </div>
      </div>
    </>
  );
}
