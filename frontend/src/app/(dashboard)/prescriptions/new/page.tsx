"use client";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { PrescriptionForm } from "@/components/prescriptions/PrescriptionForm";

export default function NewPrescriptionPage() {
  return (
    <div className="space-y-5 max-w-3xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/prescriptions" className="hover:text-clinic-teal transition">الوصفات</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">وصفة جديدة</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/prescriptions" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إنشاء وصفة طبية</h1>
      </div>
      <PrescriptionForm />
    </div>
  );
}
