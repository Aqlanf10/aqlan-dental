"use client";

import { useEffect } from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";

export default function DoctorClinicError({
  error,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Doctor clinic load error:", error);
  }, [error]);

  return (
    <div
      role="alert"
      dir="rtl"
      className="flex min-h-[420px] flex-col items-center justify-center gap-4 p-8 text-center"
    >
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-red-50">
        <AlertTriangle className="h-8 w-8 text-red-600" />
      </div>
      <div>
        <h1 className="text-lg font-bold text-gray-900">تعذر تحميل مرضى عيادة الطبيب</h1>
        <p className="mt-2 max-w-lg text-sm leading-6 text-gray-600">
          لم يتمكن النظام من جلب قائمة المرضى الحالية. هذه ليست قائمة فارغة؛ تحقق من الاتصال ثم أعد المحاولة.
        </p>
      </div>
      <button
        type="button"
        onClick={() => window.location.reload()}
        className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-5 py-2.5 text-sm font-bold text-white hover:opacity-90"
      >
        <RefreshCw className="h-4 w-4" />
        إعادة المحاولة
      </button>
    </div>
  );
}
