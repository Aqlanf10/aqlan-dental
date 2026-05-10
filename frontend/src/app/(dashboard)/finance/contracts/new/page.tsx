"use client";
import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { ContractForm } from "@/components/finance/ContractForm";

function NewContractContent() {
  const params = useSearchParams();
  const patientId   = params.get("patientId")   ?? undefined;
  const patientName = params.get("patientName")  ?? undefined;

  return (
    <div className="space-y-5 max-w-3xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/finance" className="hover:text-clinic-blue transition">المالية</Link>
        <span>/</span>
        <Link href="/finance/contracts" className="hover:text-clinic-blue transition">العقود</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">عقد جديد</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/finance/contracts" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">إنشاء عقد جديد</h1>
      </div>
      <ContractForm defaultPatientId={patientId} defaultPatientName={patientName} />
    </div>
  );
}

export default function NewContractPage() {
  return (
    <Suspense>
      <NewContractContent />
    </Suspense>
  );
}
