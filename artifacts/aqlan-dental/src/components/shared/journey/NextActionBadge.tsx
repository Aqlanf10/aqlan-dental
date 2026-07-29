
import { NEXT_ACTION_ARABIC, ACTION_COLORS } from "./constants";

interface NextActionBadgeProps {
  action: string;
  className?: string;
}

/**
 * Unified next-action badge for journey items.
 * Shows the Arabic label with the action's color coding.
 */
export function NextActionBadge({ action, className = "" }: NextActionBadgeProps) {
  if (!action || action === "None") return null;

  const label = NEXT_ACTION_ARABIC[action] ?? action;
  const colorClass = ACTION_COLORS[action] ?? "bg-gray-300";

  return (
    <span className={`inline-flex items-center px-2.5 py-1 rounded-lg text-[11px] font-bold text-white ${colorClass} ${className}`}>
      {label}
    </span>
  );
}
