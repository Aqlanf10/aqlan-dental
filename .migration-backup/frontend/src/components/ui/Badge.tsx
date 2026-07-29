import type { HTMLAttributes } from "react";
import { cn } from "@/lib/utils";

/**
 * Badge — small status pill (Arabic RTL safe).
 *
 * Token-aligned soft color pairs; `info` leans on the clinic blue accent so
 * status chips stay on-brand. Use for case/order/appointment statuses.
 */
export type BadgeVariant =
  | "neutral"
  | "success"
  | "warning"
  | "danger"
  | "info";

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
}

const VARIANT_CLASSES: Record<BadgeVariant, string> = {
  neutral: "bg-gray-100 text-gray-700",
  success: "bg-green-100 text-green-700",
  warning: "bg-clinic-orange-100 text-clinic-orange-600",
  danger: "bg-red-100 text-red-700",
  info: "bg-clinic-blue-100 text-clinic-blue-600",
};

export function Badge({
  variant = "neutral",
  className,
  ...props
}: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium",
        VARIANT_CLASSES[variant],
        className,
      )}
      {...props}
    />
  );
}
