"use client";
// Sprint 11A — extracted from the former monolithic settings/page.tsx.
// Behavior unchanged: same UI, same API calls, same state management.

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/// <summary>
/// Field wrapper with a default/custom badge and optional error/hint text.
/// </summary>
export function FieldWithBadge({
  label,
  isDefault,
  error,
  hint,
  children,
}: {
  label: string;
  isDefault: boolean;
  error?: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <div>
      <div className="flex items-center justify-between mb-1.5">
        <label className="block text-sm font-medium text-gray-700">{label}</label>
        <span
          className={cn(
            "text-[10px] px-1.5 py-0.5 rounded-full font-medium",
            isDefault
              ? "bg-gray-100 text-gray-500"
              : "bg-blue-100 text-blue-700"
          )}
        >
          {isDefault ? "افتراضي" : "مخصص"}
        </span>
      </div>
      {children}
      {error && <p className="text-xs text-red-600 mt-1">{error}</p>}
      {hint && !error && <p className="text-xs text-gray-500 mt-1">{hint}</p>}
    </div>
  );
}
