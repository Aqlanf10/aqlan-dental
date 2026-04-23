"use client";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { PaymentForm } from "@/components/finance/PaymentForm";

export default function NewPaymentPage() {
  return (
    <div className="space-y-5 max-w-3xl">
      <div className="flex items-center gap-2 text-sm text-gray-500">
        <Link href="/finance" className="hover:text-clinic-teal transition">المالية</Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">تسجيل دفعة</span>
      </div>
      <div className="flex items-center gap-3">
        <Link href="/finance" className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
          <ArrowRight className="w-4 h-4" />
        </Link>
        <h1 className="text-2xl font-extrabold text-gray-900">تسجيل دفعة جديدة</h1>
      </div>
      <PaymentForm />
    </div>
  );
}
