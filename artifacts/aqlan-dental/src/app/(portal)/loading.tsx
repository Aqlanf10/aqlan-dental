/**
 * FE-15: Portal loading skeleton — shown during Suspense for patient portal routes.
 */
export default function PortalLoading() {
  return (
    <div className="min-h-[60vh] flex items-center justify-center">
      <div className="flex flex-col items-center gap-3">
        <div className="h-10 w-10 animate-spin rounded-full border-4 border-slate-200 border-t-[#0E7490]" />
        <p className="text-sm text-slate-500">جارٍ التحميل…</p>
      </div>
    </div>
  );
}
