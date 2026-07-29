"use client";

import { forwardRef, useId } from "react";
import type { InputHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Input — text field matching the repeated
 * `"w-full px-3 py-2 text-sm rounded-lg border ..."` pattern used across forms.
 *
 * RTL by default (inherits document `dir`); pass `dir="ltr"` for numeric/phone
 * fields. Supports an Arabic `label` and `error` text; error state recolors the
 * border and is announced via `aria-invalid` / `aria-describedby`.
 */
export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Optional field label (Arabic). */
  label?: ReactNode;
  /** Optional error message (Arabic). Recolors the border when present. */
  error?: ReactNode;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, className, id, disabled, ...props }, ref) => {
    const generatedId = useId();
    const inputId = id ?? generatedId;
    const errorId = `${inputId}-error`;

    return (
      <div className="w-full">
        {label ? (
          <label
            htmlFor={inputId}
            className="mb-1 block text-sm font-medium text-clinic-navy"
          >
            {label}
          </label>
        ) : null}
        <input
          ref={ref}
          id={inputId}
          disabled={disabled}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className={cn(
            "w-full px-3 py-2 text-sm rounded-lg border bg-white",
            "focus:outline-none focus:ring-2 focus:ring-clinic-blue",
            error ? "border-red-400" : "border-gray-300",
            "disabled:bg-gray-100 disabled:opacity-60 disabled:cursor-not-allowed",
            className,
          )}
          {...props}
        />
        {error ? (
          <p id={errorId} className="mt-1 text-xs text-red-600">
            {error}
          </p>
        ) : null}
      </div>
    );
  },
);

Input.displayName = "Input";
