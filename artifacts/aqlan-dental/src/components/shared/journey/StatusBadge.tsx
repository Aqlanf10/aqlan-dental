
import { APPOINTMENT_STATUS_ARABIC, STATUS_COLORS_TAILWIND, STATUS_COLORS_HEX } from "./constants";

interface StatusBadgeProps {
  status: string;
  variant?: "tailwind" | "hex";
  className?: string;
}

/**
 * Unified status badge for journey items.
 * Supports both Tailwind class variant (patient-journey) and hex color variant (daily-operations).
 */
export function StatusBadge({ status, variant = "tailwind", className = "" }: StatusBadgeProps) {
  const label = APPOINTMENT_STATUS_ARABIC[status] ?? status;

  if (variant === "hex") {
    const colors = STATUS_COLORS_HEX[status] ?? { bg: "#f5f5f5", text: "#6b7280", border: "#e5e7eb" };
    return (
      <span
        className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold ${className}`}
        style={{ background: colors.bg, color: colors.text, border: `1px solid ${colors.border}` }}
      >
        {label}
      </span>
    );
  }

  // Tailwind variant
  const colorClass = STATUS_COLORS_TAILWIND[status] ?? "bg-gray-100 text-gray-500";
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold ${colorClass} ${className}`}>
      {label}
    </span>
  );
}
