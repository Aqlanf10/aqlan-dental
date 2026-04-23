import { PatientForm } from "@/components/patients/PatientForm";
import Link from "next/link";
import { ArrowRight } from "lucide-react";

export default function NewPatientPage() {
  return (
    <div className="space-y-5 max-w-4xl mx-auto">
      <div className="flex items-center gap-3">
        <Link
          href="/patients"
          className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-500 transition"
        >
          <ArrowRight className="w-5 h-5" />
        </Link>
        <div>
          <h1 className="text-2xl font-extrabold text-gray-900">تسجيل مريض جديد</h1>
          <p className="text-sm text-gray-500 mt-0.5">أدخل بيانات المريض والتاريخ الطبي</p>
        </div>
      </div>
      <PatientForm />
    </div>
  );
}
