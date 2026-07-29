
import { useEffect } from "react";
import { AlertTriangle, RotateCw } from "lucide-react";

export default function FinanceError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("الصفحة المالية error:", error);
  }, [error]);

  return (
    <div
      className="flex min-h-[400px] flex-col items-center justify-center gap-4 p-8 text-center"
      dir="rtl"
    >
      <div className="mb-2 flex h-16 w-16 items-center justify-center rounded-full bg-red-50">
        <AlertTriangle className="h-8 w-8 text-red-500" />
      </div>
      <p className="text-lg font-semibold text-gray-800">حدث خطأ غير متوقع</p>
      <p className="max-w-md text-sm text-gray-500">
        تعذّر تحميل الصفحة المالية. يرجى إعادة المحاولة أو التواصل مع الدعم إذا استمرت المشكلة.
      </p>
      <button
        onClick={reset}
        className="inline-flex items-center gap-2 rounded-lg bg-cyan-700 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-cyan-800"
      >
        <RotateCw className="h-4 w-4" />
        إعادة المحاولة
      </button>
    </div>
  );
}
