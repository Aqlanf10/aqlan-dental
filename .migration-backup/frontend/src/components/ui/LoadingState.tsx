import { cn } from "@/lib/utils";
import { TableSkeleton, CardSkeleton } from "@/components/ui/skeleton";

/**
 * LoadingState — centered loading indicator (Arabic RTL).
 *
 * Default is a small spinner with an optional Arabic `label`. For content-shaped
 * placeholders pass `variant="table"` or `variant="card"` to reuse the existing
 * skeleton primitives instead.
 */
export interface LoadingStateProps {
  /** Optional Arabic label shown under the spinner. */
  label?: string;
  /** Placeholder style. Defaults to a spinner. */
  variant?: "spinner" | "table" | "card";
  className?: string;
}

export function LoadingState({
  label = "جارٍ التحميل...",
  variant = "spinner",
  className,
}: LoadingStateProps) {
  if (variant === "table") {
    return <TableSkeleton />;
  }
  if (variant === "card") {
    return <CardSkeleton />;
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        "flex flex-col items-center justify-center gap-3 px-4 py-10 text-center",
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="h-7 w-7 animate-spin rounded-full border-2 border-clinic-blue-100 border-t-clinic-navy"
      />
      {label ? <p className="text-sm text-gray-500">{label}</p> : null}
    </div>
  );
}
