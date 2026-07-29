/**
 * FE-15: Portal error boundary — catches uncaught errors in patient portal routes
 * and shows a friendly Arabic message instead of a white screen.
 */

export default function PortalError({ reset }: { error: Error; reset: () => void }) {
  return (
    <div className="min-h-[60vh] flex items-center justify-center p-4">
      <div className="max-w-md text-center">
        <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-red-50">
          <svg className="h-6 w-6 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
        </div>
        <h2 className="mb-2 text-lg font-bold text-slate-900">حدث خطأ غير متوقع</h2>
        <p className="mb-4 text-sm text-slate-500">
          تعذّر تحميل هذه الصفحة. يرجى المحاولة مرة أخرى.
        </p>
        <button
          onClick={reset}
          className="rounded-lg bg-[#0E7490] px-4 py-2 text-sm font-medium text-white hover:bg-[#0c5a6e] transition-colors"
        >
          إعادة المحاولة
        </button>
      </div>
    </div>
  );
}
