
/**
 * Fetch-failure banner (SEQ-11 / ORTHO-REQ-006 family): a 500/network error
 * must never render the same UI as "no data yet" — misleading empty states
 * hide outages during live clinic use. Same visual pattern as the ortho
 * QueryErrorState in `ortho/[id]/_components/_shared.tsx`.
 */
export function QueryErrorBanner({
  text,
  onRetry,
}: {
  text: string;
  onRetry: () => void;
}) {
  return (
    <div
      className="rounded-xl border border-red-200 py-10 text-center"
      style={{ background: "#fef2f2" }}
    >
      <p className="text-sm font-medium" style={{ color: "#b91c1c" }}>
        {text}
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 rounded-lg border border-red-300 px-4 py-1.5 text-sm font-medium text-red-700 transition hover:bg-red-100"
      >
        إعادة المحاولة
      </button>
    </div>
  );
}
