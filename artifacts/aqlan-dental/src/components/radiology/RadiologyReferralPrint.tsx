import type { RadiologyOrder } from "@/types/radiologyOrder";
import { studyTypeLabelEn } from "@/types/radiologyOrder";

interface Props {
  order: RadiologyOrder;
  clinicName?: string;
  clinicAddress?: string;
  clinicPhone?: string;
  leadDoctor?: string;
  leadDoctorCredentials?: string;
}

function ageFromDob(dob?: string): string | null {
  if (!dob) return null;
  const birth = new Date(dob + "T00:00:00");
  if (isNaN(birth.getTime())) return null;
  const now = new Date();
  let age = now.getFullYear() - birth.getFullYear();
  const m = now.getMonth() - birth.getMonth();
  if (m < 0 || (m === 0 && now.getDate() < birth.getDate())) age--;
  return age >= 0 ? String(age) : null;
}

// Spec 010 (RX-REQ-002): English referral form the patient carries to the
// external radiology center. Identity defaults mirror the settings fallbacks;
// callers pass useClinicBranding() EN fields so the form stays settings-driven.
export function RadiologyReferralPrint({
  order,
  clinicName = "Dr. Aqlan Alkamel Center for Orthodontics, Dental Implants & Cosmetic Dentistry",
  clinicAddress = "Upper Al-Tahrir Street, Taiz, Yemen",
  clinicPhone = "04-253028",
  leadDoctor = "Dr. Aqlan Alkamel — Orthodontic Specialist",
  leadDoctorCredentials = "Central University of Manila — Philippines",
}: Props) {
  const age = ageFromDob(order.patientDateOfBirth);

  return (
    <>
      {/* Codex P2 (#676): the old body>*:not(#root) selector hid the dashboard
          ancestor that CONTAINS this component (it is never a direct child of
          <body>), and a hidden ancestor cannot be re-shown — printing came out
          blank. Chrome is hidden instead by the global body.printing print CSS
          plus print:hidden on the page-level screen elements. */}
      <style>{`
        @media print {
          @page { margin: 1cm; }
        }
      `}</style>

      <div id="radiology-referral-print-root" dir="ltr" className="bg-white font-sans text-left">
        {/* Header */}
        <div className="border-b-2 border-gray-800 pb-4 mb-5 flex items-start justify-between">
          <div>
            <h1 className="text-lg font-extrabold text-gray-900">{clinicName}</h1>
            <p className="text-sm font-semibold text-gray-700 mt-0.5">{leadDoctor}</p>
            <p className="text-xs text-gray-500">{leadDoctorCredentials}</p>
          </div>
          <div className="text-right text-sm text-gray-500 shrink-0">
            <div>Date: <span className="font-semibold text-gray-900">{order.createdAt}</span></div>
            {order.doctorName && (
              <div className="mt-0.5">Referring doctor: <span className="font-semibold text-gray-900">{order.doctorName}</span></div>
            )}
          </div>
        </div>

        {/* Title */}
        <div className="text-center mb-5">
          <h2 className="text-xl font-extrabold text-gray-900 tracking-wide">RADIOLOGY REFERRAL</h2>
          <p className="text-sm text-gray-500 mt-0.5">To: The Radiology Center</p>
        </div>

        {/* Patient block */}
        <div className="bg-gray-50 rounded-lg p-4 mb-5 text-sm grid grid-cols-2 gap-x-6 gap-y-2">
          <div className="col-span-2">
            <span className="text-gray-500">Patient: </span>
            <span className="font-bold text-gray-900">{order.patientName}</span>
            <span className="text-gray-400 font-mono ms-3">({order.patientNumber})</span>
          </div>
          {age && (
            <div>
              <span className="text-gray-500">Age: </span>
              <span className="font-semibold text-gray-900">{age} years</span>
            </div>
          )}
          {order.patientGender && (
            <div>
              <span className="text-gray-500">Sex: </span>
              <span className="font-semibold text-gray-900">{order.patientGender}</span>
            </div>
          )}
        </div>

        {/* Requested study */}
        <div className="border-2 border-gray-800 rounded-lg p-4 mb-5">
          <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Requested study</p>
          <p className="text-lg font-extrabold text-gray-900">{studyTypeLabelEn(order.studyType)}</p>
          {order.region && (
            <p className="text-sm text-gray-700 mt-2">
              <span className="font-semibold">Region of interest: </span>{order.region}
            </p>
          )}
        </div>

        {/* Clinical notes */}
        {order.clinicalNotes && (
          <div className="mb-6 text-sm">
            <p className="font-semibold text-gray-700 mb-1">Clinical information:</p>
            <p className="text-gray-900 whitespace-pre-wrap">{order.clinicalNotes}</p>
          </div>
        )}

        {/* Signature */}
        <div className="mt-10 pt-4 border-t border-dashed border-gray-300 flex justify-between text-sm text-gray-400">
          <span>Doctor&apos;s signature: ___________________</span>
          <span>Clinic stamp</span>
        </div>

        {/* Contact footer */}
        <div className="mt-6 pt-3 border-t border-gray-200 text-center text-xs text-gray-400">
          {clinicAddress} · Tel: {clinicPhone} · Please return the images/report with the patient.
        </div>
      </div>
    </>
  );
}
