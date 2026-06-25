import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * EmptyState — centered "no data" placeholder (Arabic RTL).
 * Optional `icon`, a required Arabic `message`, optional `description`
 * and an optional `action` (e.g. a Button to create the first record).
 */
export interface EmptyStateProps {
  /** Optional leading icon (e.g. a lucide icon element). */
  icon?: ReactNode;
  /** Primary message (Arabic). */
  message: ReactNode;
  /** Optional secondary description (Arabic). */
  description?: ReactNode;
  /** Optional action element. */
  action?: ReactNode;
  className?: string;
}

export function EmptyState({
  icon,
  message,
  description,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-2 px-4 py-10 text-center",
        className,
      )}
    >
      {icon ? <div className="text-gray-400">{icon}</div> : null}
      <p className="text-sm font-medium text-gray-700">{message}</p>
      {description ? (
        <p className="text-xs text-gray-500">{description}</p>
      ) : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
