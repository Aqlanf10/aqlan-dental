"use client";

import { CheckCircle2, AlertTriangle, Circle } from "lucide-react";
import { cn } from "@/lib/utils";
import type { CephReadiness } from "@/lib/cephReadiness";

/**
 * Saved-data readiness indicator for a cephalometric analysis. Tells the doctor
 * at a glance whether the PDF report / VTO can be produced from saved data —
 * instead of discovering it only on click.
 *
 * - `variant="bar"`  : full-width slim bar with verdict + reason + checklist
 *   chips (used at the top of the analysis page).
 * - `variant="compact"`: a single verdict pill with the blocking reason as a
 *   tooltip (used inside the ortho case overview).
 */
export function CephReadinessBadge({
  readiness,
  variant = "bar",
  className,
}: {
  readiness: CephReadiness;
  variant?: "bar" | "compact";
  className?: string;
}) {
  const { ready, verdict, reason, items } = readiness;
  const Icon = ready ? CheckCircle2 : AlertTriangle;

  if (variant === "compact") {
    return (
      <span
        title={reason ?? "كل البيانات محفوظة والتقرير جاهز"}
        className={cn(
          "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-bold",
          ready ? "bg-emerald-50 text-emerald-700" : "bg-amber-50 text-amber-700",
          className,
        )}
      >
        <Icon className="h-3.5 w-3.5" />
        {ready ? "✅ جاهز للتقرير" : `⚠️ ${verdict}`}
      </span>
    );
  }

  return (
    <div
      dir="rtl"
      role="status"
      className={cn(
        "flex flex-wrap items-center gap-x-3 gap-y-1.5 rounded-lg border px-3 py-2 text-xs",
        ready
          ? "border-emerald-200 bg-emerald-50 text-emerald-800"
          : "border-amber-200 bg-amber-50 text-amber-800",
        className,
      )}
    >
      <span className="flex items-center gap-1.5 font-bold">
        <Icon className="h-4 w-4" />
        {ready ? "✅ جاهز للتقرير والـ VTO" : `⚠️ ${verdict}`}
      </span>

      {!ready && reason && (
        <span className="font-medium text-amber-700">{reason}</span>
      )}

      <span className="mx-auto" />

      <ul className="flex flex-wrap items-center gap-x-3 gap-y-1">
        {items.map((item) => (
          <li
            key={item.key}
            className={cn(
              "flex items-center gap-1",
              item.ok ? "text-emerald-700" : "text-gray-400",
            )}
          >
            {item.ok ? (
              <CheckCircle2 className="h-3.5 w-3.5" />
            ) : (
              <Circle className="h-3.5 w-3.5" />
            )}
            {item.label}
          </li>
        ))}
      </ul>
    </div>
  );
}
