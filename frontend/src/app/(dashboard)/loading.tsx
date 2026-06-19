/**
 * FE-15: Dashboard loading skeleton — shown by Next.js App Router during Suspense
 * (streaming) while route segments load. Previously there were zero loading.tsx
 * files, so slow navigations showed the previous page until the new one fully
 * hydrated, causing a jarring UX.
 */
export default function DashboardLoading() {
  return (
    <div dir="rtl" className="min-h-[60vh] flex items-center justify-center">
      <div className="flex flex-col items-center gap-3">
        <div className="h-10 w-10 animate-spin rounded-full border-4 border-slate-200 border-t-[#1a3a5c]" />
        <p className="text-sm text-slate-500">جارٍ التحميل…</p>
      </div>
    </div>
  );
}
