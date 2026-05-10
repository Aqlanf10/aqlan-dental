"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { AppointmentForm } from "@/components/appointments/AppointmentForm";
import type { Appointment } from "@/types/appointment";
import api from "@/lib/api";

export default function EditAppointmentPage() {
  const { id } = useParams<{ id: string }>();
  const [appt, setAppt] = useState<Appointment | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    api.get<Appointment>(`/api/appointments/${id}`)
      .then((r) => setAppt(r.data))
      .catch(() => setNotFound(true))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="space-y-4 animate-pulse max-w-3xl">
        <div className="h-8 w-48 bg-gray-100 rounded-lg" />
        <div className="h-64 bg-gray-100 rounded-xl" />
      </div>
    );
  }

  if (notFound || !appt) {
    return (
      <div className="text-center py-20 text-gray-500">
        <p>الموعد غير موجود</p>
        <Link href="/appointments" className="text-clinic-blue text-sm underline mt-2 inline-block">
          العودة إلى المواعيد
        </Link>
      </div>
    );
  }

  const patientLabel = `${appt.patientName} (${appt.patientNumber})`;

  return (
    <div className="space-y-5 max-w-3xl">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/appointments" className="hover:text-clinic-blue transition">المواعيد</Link>
        <ChevronRight className="w-4 h-4" />
        <span className="text-gray-900 font-medium">تعديل الموعد</span>
      </div>

      <div>
        <h1 className="text-2xl font-extrabold text-gray-900">تعديل الموعد</h1>
        <p className="text-sm text-gray-500 mt-0.5">{patientLabel}</p>
      </div>

      <AppointmentForm
        appointmentId={id}
        defaultPatientId={appt.patientId}
        defaultPatientName={patientLabel}
        editDefaults={{
          doctorId:        appt.doctorId,
          appointmentDate: appt.appointmentDate,
          startTime:       appt.startTime,
          durationMinutes: appt.durationMinutes,
          appointmentType: appt.appointmentType,
          notes:           appt.notes ?? "",
        }}
      />
    </div>
  );
}
