// ---------------------------------------------------------------------------
// Cephalometric "ready for report/VTO" readiness — single source of truth.
// ---------------------------------------------------------------------------
// A ceph analysis can export a PDF report or open the VTO only from SAVED data:
// image present, calibration saved, landmarks saved, measurements computed, and
// no unsaved edits pending. This module computes that verdict so the analysis
// page badge AND the ortho case overview can agree on exactly the same gate.

import type { CephAnalysis } from "@/types/ceph";

/**
 * Full cephalometric landmark set. A report/VTO is only valid once every
 * landmark is placed (matches LANDMARK_ORDER and the "X/24" UI counters), so
 * readiness requires the complete set — not just a single saved point.
 */
export const REQUIRED_LANDMARKS = 24;

export interface CephReadinessInput {
  /** An x-ray image is attached to the analysis. */
  hasImage: boolean;
  /** Calibration (pixels per mm) has been saved. */
  hasCalibration: boolean;
  /** All required landmarks (24) are saved on the record. */
  hasPoints: boolean;
  /** Measurements have been computed and saved. */
  hasMeasurements: boolean;
  /** The doctor has edited points/calibration without pressing "save & compute". */
  isDirty?: boolean;
}

export interface CephReadinessItem {
  key: keyof CephReadinessInput;
  label: string;
  ok: boolean;
}

export interface CephReadiness {
  /** True only when every requirement is met and nothing is unsaved. */
  ready: boolean;
  /** Per-requirement breakdown for a checklist UI (ordered). */
  items: CephReadinessItem[];
  /** Short verdict label for a badge/pill. */
  verdict: string;
  /** The single most important blocking reason, or null when ready. */
  reason: string | null;
}

/**
 * Computes the report/VTO readiness from saved-data flags. `isDirty` reflects
 * unsaved landmark/calibration edits and, when true, always blocks readiness —
 * because the PDF and VTO are generated from the saved record, not the canvas.
 */
export function computeCephReadiness(input: CephReadinessInput): CephReadiness {
  const dirty = Boolean(input.isDirty);

  const items: CephReadinessItem[] = [
    { key: "hasImage", label: "صورة الأشعة", ok: input.hasImage },
    { key: "hasCalibration", label: "المعايرة محفوظة", ok: input.hasCalibration },
    { key: "hasPoints", label: "النقاط مكتملة", ok: input.hasPoints },
    { key: "hasMeasurements", label: "القياسات محفوظة", ok: input.hasMeasurements },
  ];

  const ready =
    input.hasImage &&
    input.hasCalibration &&
    input.hasPoints &&
    input.hasMeasurements &&
    !dirty;

  // Most actionable reason first: unsaved edits dominate, then missing inputs.
  let reason: string | null = null;
  if (dirty) reason = "لديك تعديلات غير محفوظة — اضغط «حفظ وحساب»";
  else if (!input.hasImage) reason = "لا توجد صورة أشعة بعد";
  else if (!input.hasPoints) reason = `النقاط غير مكتملة (يلزم وضع ${REQUIRED_LANDMARKS} نقطة)`;
  else if (!input.hasMeasurements) reason = "لم تُحسب القياسات — اضغط «حفظ وحساب»";
  else if (!input.hasCalibration) reason = "المعايرة غير محفوظة (القياسات الخطية بالمليمتر تحتاج معايرة)";

  return {
    ready,
    items,
    verdict: ready ? "جاهز للتقرير" : dirty ? "يحتاج حفظ وحساب" : "غير مكتمل",
    reason,
  };
}

/** Convenience: derive readiness directly from a loaded analysis + dirty flag. */
export function cephReadinessFromAnalysis(
  analysis: Pick<CephAnalysis, "xrayFileUrl" | "pixelsPerMm" | "landmarks" | "measurements">,
  isDirty: boolean,
): CephReadiness {
  return computeCephReadiness({
    hasImage: Boolean(analysis.xrayFileUrl),
    hasCalibration: Boolean(analysis.pixelsPerMm && analysis.pixelsPerMm > 0),
    hasPoints: (analysis.landmarks?.length ?? 0) >= REQUIRED_LANDMARKS,
    hasMeasurements: (analysis.measurements?.length ?? 0) > 0,
    isDirty,
  });
}
