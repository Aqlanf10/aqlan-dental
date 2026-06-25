import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * SectionHeader — titled section header (Arabic RTL).
 * Title + optional subtitle on the start side; optional `action` slot
 * (button/link) on the end side.
 */
export interface SectionHeaderProps {
  title: ReactNode;
  subtitle?: ReactNode;
  /** Optional trailing action (e.g. a Button), placed at the row end. */
  action?: ReactNode;
  className?: string;
}

export function SectionHeader({
  title,
  subtitle,
  action,
  className,
}: SectionHeaderProps) {
  return (
    <div
      className={cn("flex items-start justify-between gap-3", className)}
    >
      <div>
        <h2 className="text-lg font-semibold text-clinic-navy">{title}</h2>
        {subtitle ? (
          <p className="mt-0.5 text-sm text-gray-500">{subtitle}</p>
        ) : null}
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
}
