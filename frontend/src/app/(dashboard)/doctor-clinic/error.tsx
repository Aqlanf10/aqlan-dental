"use client";

import { useEffect } from "react";
import Link from "next/link";
import { AlertTriangle, RefreshCw, UserRoundX } from "lucide-react";
import { isDoctorAccountNotLinkedError } from "./_lib/errors";

export default function DoctorClinicError({
  error,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const accountNotLinked = isDoctorAccountNotLinkedError(error);

  useEffect(() => {
    if (accountNotLinked) {
      console.warn("Doctor clinic account is not linked to a doctor record");
      return;
    }
    console.error("Doctor clinic load error:", error);
  }, [accountNotLinked, error]);

  if (accountNotLinked) {
    return (
      <div
        role="alert"
        dir="rtl"
        className="flex min-h-[420px] flex-col items-center justify-center gap-4 p-8 text-center"
      >
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-amber-50">
          <UserRoundX className="h-8 w-8 text-amber-700" />
        </div>
        <div>
          <h1 className="text-lg font-bold text-gray-900">حسابك غير مرتبط بسجل طبيب</h1>
          <p className="mt-2 max-w-lg text-sm leading-6 text-gray-600">
            لا يمكن تحديد مرضاك لأن حساب المستخدم لا يحتوي على معرّف الطبيب. اطلب من مدير النظام ربط الحساب بسجل الطبيب من إعدادات المستخدمين والصلاحيات.
          </p>
        </div>
        <Link
          href="/daily-operations"
          className="inline-flex items-center rounded-lg border border-gray-300 bg-white px-5 py-2.5 text-sm font-bold text-gray-700 hover:bg-gray-50"
        >
          العودة إلى التشغيل اليومي
        </Link>
      </div>
    );
  }

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
