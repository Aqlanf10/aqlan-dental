"use client";

import { forwardRef } from "react";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Button — shared action primitive (Arabic RTL safe).
 *
 * Variants compose the existing `clinic.*` Tailwind tokens (no new colors):
 *   - primary    → bg-clinic-navy (brand dark sky)
 *   - secondary  → outlined neutral surface
 *   - ghost      → transparent, hover tint
 *   - danger     → red destructive action
 *
 * Sizes: sm | md. Supports `disabled` and an optional leading `icon`.
 */
export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
export type ButtonSize = "sm" | "md";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Optional leading icon (rendered before the label, RTL-aware via flex gap). */
  icon?: ReactNode;
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary:
    "bg-clinic-navy text-white hover:opacity-90 disabled:opacity-50",
  secondary:
    "bg-white text-clinic-navy border border-gray-300 hover:bg-gray-50 disabled:opacity-50",
  ghost:
    "bg-transparent text-clinic-navy hover:bg-gray-100 disabled:opacity-50",
  danger:
    "bg-red-600 text-white hover:bg-red-700 disabled:opacity-50",
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: "px-3 py-1.5 text-xs",
  md: "px-4 py-2 text-sm",
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    { variant = "primary", size = "md", icon, className, children, type, ...props },
    ref,
  ) => (
    <button
      ref={ref}
      type={type ?? "button"}
      className={cn(
        "inline-flex items-center justify-center gap-1.5 rounded-lg font-medium transition-colors",
        "focus:outline-none focus:ring-2 focus:ring-clinic-blue focus:ring-offset-1",
        "disabled:cursor-not-allowed",
        VARIANT_CLASSES[variant],
        SIZE_CLASSES[size],
        className,
      )}
      {...props}
    >
      {icon}
      {children}
    </button>
  ),
);

Button.displayName = "Button";
