import { SURGERY_STATUS_LABELS, SURGERY_STATUS_COLORS } from "@/types/surgery";

interface StatusBadgeProps {
  status: string;
  size?: "sm" | "md";
}

export function StatusBadge({ status, size = "sm" }: StatusBadgeProps) {
  const label = SURGERY_STATUS_LABELS[status] ?? status;
  const color = SURGERY_STATUS_COLORS[status] ?? "bg-gray-100 text-gray-500";
  const sizeClass = size === "sm" ? "text-xs px-2 py-0.5" : "text-sm px-3 py-1";

  return (
    <span className={`inline-flex items-center rounded-full font-medium ${color} ${sizeClass}`}>
      {label}
    </span>
  );
}
