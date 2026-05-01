"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import type { PatientProfile } from "@/types/patient";
import api from "@/lib/api";
import { PatientForm } from "@/components/patients/PatientForm";

export default function PatientEditPage() {
  const { id } = useParams<{ id: string }>();
  const [patient, setPatient] = useState<PatientProfile | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then((r) => setPatient(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-10 bg-gray-100 rounded-xl w-64" />
        <div className="h-96 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (!patient) {
    return (
      <div className="text-center py-20 text-gray-400">المريض غير موجود</div>
    );
  }

  const defaultValues = {
    firstName:   patient.firstName,
    middleName:  patient.middleName,
    lastName:    patient.lastName,
    dateOfBirth: patient.dateOfBirth,
    gender:      patient.gender as "Male" | "Female" | undefined,
    phone:       patient.phone,
    whatsApp:    patient.whatsApp,
    address:     patient.address,
    occupation:  patient.occupation,
    referralSource: patient.referralSource,
    // Medical history
    chronicDiseases:    patient.medicalHistory?.chronicDiseases,
    currentMedications: patient.medicalHistory?.currentMedications,
    drugAllergies:      patient.medicalHistory?.drugAllergies,
    bleedingDisorders:  patient.medicalHistory?.bleedingDisorders ?? false,
    isPregnant:         patient.medicalHistory?.isPregnant as "yes" | "no" | "na" | undefined,
    tmjProblems:        patient.medicalHistory?.tmjProblems ?? false,
    previousSurgeries:  patient.medicalHistory?.previousSurgeries,
    medNotes:           patient.medicalHistory?.notes,
    // Dental history
    chiefComplaint:     patient.dentalHistory?.chiefComplaint,
    previousTreatments: patient.dentalHistory?.previousTreatments,
    mouthBreathing:     patient.dentalHistory?.mouthBreathing ?? false,
    bruxism:            patient.dentalHistory?.bruxism ?? false,
    thumbSucking:       patient.dentalHistory?.thumbSucking ?? false,
    tongueThrusing:     patient.dentalHistory?.tongueThrusing ?? false,
    dentalNotes:        patient.dentalHistory?.notes,
  };

  return (
    <div className="space-y-5 max-w-5xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/patients" className="hover:text-clinic-blue transition">المرضى</Link>
        <span>/</span>
        <Link href={`/patients/${id}`} className="hover:text-clinic-blue transition">
          {patient.firstName} {patient.lastName}
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">تعديل</span>
      </div>

      <div className="flex items-center gap-3">
        <Link
          href={`/patients/${id}`}
          className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
        >
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">
          تعديل بيانات {patient.firstName} {patient.lastName}
        </h1>
      </div>

      <PatientForm defaultValues={defaultValues} patientId={id} />
    </div>
  );
}
