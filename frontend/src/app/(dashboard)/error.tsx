"use client";

import { useEffect } from "react";

export default function DashboardError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Dashboard error:", error);
  }, [error]);

  return (
    <div className="flex flex-col items-center justify-center min-h-[400px] gap-4 p-8 text-center">
      <div className="w-16 h-16 rounded-full bg-red-50 flex items-center justify-center mb-2">
        <svg className="w-8 h-8 text-red-500" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
        </svg>
      </div>
      <p className="font-semibold text-gray-800 text-lg">حدث خطأ غير متوقع</p>
      <p className="text-sm text-gray-500 max-w-md">
        حدث خطأ أثناء تحميل هذه الصفحة. يرجى المحاولة مرة أخرى أو التواصل مع الدعم إذا استمرت المشكلة.
      </p>
      <button
        onClick={reset}
        className="px-5 py-2.5 bg-cyan-700 text-white rounded-lg text-sm font-medium hover:bg-cyan-800 transition"
      >
        إعادة المحاولة
      </button>
    </div>
  );
}
