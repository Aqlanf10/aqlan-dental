"use client";

import type { ReactNode } from "react";
import { Save } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Shared UI primitives extracted from the ortho case detail page (FE-20).
 * Used by every tab component under `_components/`.
 *
 * Structural extraction only — no styling/logic changes.
 */

/** Labelled field wrapper used across exam/diagnosis/plan/etc. forms. */
export function Field({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <label className={cn("block", className)}>
      <span className="mb-1 block text-xs font-medium text-gray-500">
        {label}
      </span>
      {children}
    </label>
  );
}

/** Empty-state placeholder used by every list-style tab. */
export function EmptyState({ text }: { text: string }) {
  return (
    <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 py-10 text-center text-sm text-gray-400">
      {text}
    </div>
  );
}

/**
 * Fetch-failure banner (ORTHO-REQ-006): a 500/network error must never render
 * the same UI as "no data yet". Same visual pattern as ortho/[id]/page.tsx.
 */
export function QueryErrorState({
  text,
  onRetry,
}: {
  text: string;
  onRetry: () => void;
}) {
  return (
    <div
      className="rounded-lg border border-red-200 py-8 text-center"
      style={{ background: "#fef2f2" }}
    >
      <p className="text-sm font-medium" style={{ color: "#b91c1c" }}>
        {text}
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 rounded-lg border border-red-300 px-4 py-1.5 text-sm font-medium text-red-700 transition hover:bg-red-100"
      >
        إعادة المحاولة
      </button>
    </div>
  );
}

/** Standard save button with a saving state used by every form. */
export function SaveButton({
  saving,
  children,
}: {
  saving?: boolean;
  children: React.ReactNode;
}) {
  return (
    <button
      type="submit"
      disabled={saving}
      className="inline-flex items-center gap-2 rounded-lg bg-clinic-blue px-4 py-2 text-sm font-medium text-white transition hover:opacity-90 disabled:opacity-60"
    >
      <Save className="h-4 w-4" />
      {saving ? "جاري الحفظ..." : children}
    </button>
  );
}
