"use client";
import { useMemo } from "react";
import type { TreatmentStage } from "@/types/ortho";
import { cn, formatArabicDate } from "@/lib/utils";
import { GitBranch } from "lucide-react";

/* ------------------------------------------------------------------ */
/*  Status visual config                                               */
/* ------------------------------------------------------------------ */

const STATUS_CONFIG: Record<
  TreatmentStage["status"],
  {
    label: string;
    chip: string;
    dot: string;
    card: string;
    title: string;
    pulse?: boolean;
  }
> = {
  completed: {
    label: "مكتملة",
    chip: "bg-green-100 text-green-700",
    dot: "bg-green-500 ring-green-100",
    card: "border-green-200 bg-green-50/40",
    title: "text-green-900",
  },
  active: {
    label: "جارية",
    chip: "bg-clinic-blue-50 text-clinic-blue",
    dot: "bg-clinic-blue ring-clinic-blue-50",
    card: "border-clinic-blue-100 bg-clinic-blue-50/50",
    title: "text-clinic-navy",
    pulse: true,
  },
  pending: {
    label: "قادمة",
    chip: "bg-gray-100 text-gray-500",
    dot: "bg-gray-300 ring-gray-100",
    card: "border-gray-200 bg-white",
    title: "text-gray-500",
  },
};

interface Props {
  stages: TreatmentStage[];
}

/**
 * Read-only vertical timeline of the orthodontic treatment stages.
 * Rendered above the stages management panel in the case detail page.
 */
export function OrthoStagesTimeline({ stages }: Props) {
  const ordered = useMemo(
    () => [...stages].sort((a, b) => a.stageOrder - b.stageOrder),
    [stages]
  );

  const total = ordered.length;
  const completedCount = ordered.filter(
    (s) => s.status === "completed"
  ).length;
  const activeIndex = ordered.findIndex((s) => s.status === "active");
  const percent = total > 0 ? Math.round((completedCount / total) * 100) : 0;

  const currentLabel = useMemo(() => {
    if (total === 0) return null;
    if (completedCount === total) return "اكتملت جميع المراحل";
    if (activeIndex >= 0)
      return `المرحلة الحالية: ${activeIndex + 1} من ${total}`;
    const firstPending = ordered.findIndex((s) => s.status === "pending");
    if (firstPending >= 0)
      return `المرحلة القادمة: ${firstPending + 1} من ${total}`;
    return `المرحلة الحالية: ${completedCount} من ${total}`;
  }, [total, completedCount, activeIndex, ordered]);

  if (total === 0) {
    return (
      <div className="rounded-lg border border-dashed border-gray-200 bg-gray-50 py-10 text-center">
        <GitBranch className="mx-auto h-8 w-8 text-gray-300" />
        <p className="mt-3 text-sm font-medium text-gray-500">
          لم تُضف مراحل علاج بعد
        </p>
        <p className="mt-1 text-xs text-gray-400">
          يمكن إضافة مراحل العلاج من قسم إدارة المراحل في هذا التبويب.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Progress summary */}
      <div className="rounded-lg border border-gray-200 bg-white p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-sm font-semibold text-gray-900">{currentLabel}</p>
          <span className="text-sm font-semibold text-clinic-blue">
            {percent}%
          </span>
        </div>
        <div className="mt-3 h-2 overflow-hidden rounded-full bg-gray-100">
          <div
            className="h-full rounded-full bg-clinic-blue transition-all"
            style={{ width: `${percent}%` }}
          />
        </div>
        <p className="mt-2 text-xs text-gray-500">
          {completedCount} من {total} مراحل مكتملة
        </p>
      </div>

      {/* Vertical timeline */}
      <ol className="space-y-0">
        {ordered.map((stage, idx) => {
          const cfg = STATUS_CONFIG[stage.status] ?? STATUS_CONFIG.pending;
          const isLast = idx === ordered.length - 1;
          const dateRange = [
            stage.startedAt
              ? `بدأت: ${formatArabicDate(stage.startedAt)}`
              : null,
            stage.completedAt
              ? `اكتملت: ${formatArabicDate(stage.completedAt)}`
              : null,
          ].filter(Boolean);

          return (
            <li key={stage.id} className="flex gap-4">
              {/* Dot + connecting line */}
              <div className="flex flex-col items-center">
                <span
                  className={cn(
                    "mt-1.5 h-3.5 w-3.5 flex-shrink-0 rounded-full ring-4",
                    cfg.dot,
                    cfg.pulse && "animate-pulse"
                  )}
                  aria-hidden="true"
                />
                {!isLast && (
                  <span
                    className={cn(
                      "w-0.5 flex-1",
                      stage.status === "completed"
                        ? "bg-green-300"
                        : "bg-gray-200"
                    )}
                    aria-hidden="true"
                  />
                )}
              </div>

              {/* Stage card */}
              <div
                className={cn(
                  "mb-4 flex-1 rounded-lg border p-4 text-sm transition",
                  cfg.card
                )}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className={cn("font-semibold", cfg.title)}>
                    {stage.stageOrder}. {stage.stageName}
                  </span>
                  <span
                    className={cn(
                      "rounded-full px-2.5 py-0.5 text-xs font-medium",
                      cfg.chip
                    )}
                  >
                    {cfg.label}
                  </span>
                </div>

                {(dateRange.length > 0 || stage.targetDurationMonths) && (
                  <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500">
                    {dateRange.map((d) => (
                      <span key={d}>{d}</span>
                    ))}
                    {stage.targetDurationMonths ? (
                      <span>
                        المدة المتوقعة: {stage.targetDurationMonths} أشهر
                      </span>
                    ) : null}
                  </div>
                )}

                {stage.notes && (
                  <p className="mt-2 text-xs leading-relaxed text-gray-600">
                    {stage.notes}
                  </p>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
