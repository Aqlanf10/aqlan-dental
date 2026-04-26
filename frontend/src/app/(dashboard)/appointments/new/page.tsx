"use client";
import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { AppointmentForm } from "@/components/appointments/AppointmentForm";

function NewAppointmentContent() {
  const params = useSearchParams();
  const patientId   = params.get("patientId")   ?? undefined;
  const patientName = params.get("patientName")  ?? undefined;
  const date        = params.get("date")         ?? undefined;
  const startTime   = params.get("startTime")    ?? undefined;

  return (
    <div className="space-y-5 max-w-3xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/appointments" className="hover:text-clinic-teal transition">المواعيد</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">موعد جديد</span>
      </div>

      <div className="flex items-center gap-3">
        <Link
          href="/appointments"
          className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500"
        >
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إضافة موعد جديد</h1>
      </div>

      <AppointmentForm
        defaultPatientId={patientId}
        defaultPatientName={patientName}
        editDefaults={date || startTime ? {
          doctorId: "",
          appointmentDate: date ?? new Date().toISOString().slice(0, 10),
          startTime: startTime ?? "",
          durationMinutes: 30,
          appointmentType: "",
        } : undefined}
      />
    </div>
  );
}

export default function NewAppointmentPage() {
  return (
    <Suspense>
      <NewAppointmentContent />
    </Suspense>
  );
}
