"use client";

import { AlertTriangle, Info, ShieldCheck } from "lucide-react";
import { cn } from "@/lib/utils";
import type { CephQualityReport } from "@/lib/cephReadiness";

/**
 * Data-quality signals panel for a cephalometric analysis (audit §12). Surfaces
 * advisory warnings — missing/invalid calibration, low-confidence AI landmarks,
 * incomplete landmark set, unsaved edits, stale measurements — BEFORE the doctor
 * trusts the numbers. These are advisory only; the Sprint 6 approval/PDF gate
 * stays the hard line.
 */
export function CephQualityPanel({
  quality,
  className,
}: {
  quality: CephQualityReport;
  className?: string;
}) {
  if (quality.warnings.length === 0) {
    return (
      <div
        role="status"
        className={cn(
          "flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-800",
          className,
        )}
      >
        <ShieldCheck className="h-4 w-4 flex-shrink-0" />
        لا توجد ملاحظات على جودة البيانات — المعايرة والنقاط والقياسات تبدو متّسقة.
      </div>
    );
  }

  return (
    <div
      role="status"
      className={cn(
        "rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-900",
        className,
      )}
    >
      <div className="mb-1 flex items-center gap-1.5 text-xs font-bold text-amber-900">
        <AlertTriangle className="h-4 w-4 flex-shrink-0" />
        ملاحظات جودة البيانات ({quality.warnings.filter((w) => w.severity === "warning").length})
      </div>
      <ul className="space-y-1">
        {quality.warnings.map((w) => {
          const Icon = w.severity === "warning" ? AlertTriangle : Info;
          return (
            <li
              key={w.key}
              className={cn(
                "flex items-start gap-1.5 text-[11px] leading-5",
                w.severity === "warning" ? "text-amber-800" : "text-amber-700/90",
              )}
            >
              <Icon className="mt-0.5 h-3.5 w-3.5 flex-shrink-0" />
              <span>{w.message}</span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
