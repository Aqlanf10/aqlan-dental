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

/** Detail-only metadata carried with a loaded analysis. */
export interface CephAnalysisMetadata {
  notes: string | null;
  isAutoTraced: boolean;
  doctorId: string | null;
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
  /** Optional detail metadata; absent for flag-only computations. */
  analysisMetadata?: CephAnalysisMetadata;
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
  analysis: Pick<
    CephAnalysis,
    "xrayFileUrl" | "pixelsPerMm" | "landmarks" | "measurements"
  > & Partial<Pick<CephAnalysis, "notes" | "isAutoTraced" | "doctorId">>,
  isDirty: boolean,
): CephReadiness {
  return {
    ...computeCephReadiness({
      hasImage: Boolean(analysis.xrayFileUrl),
      hasCalibration: Boolean(analysis.pixelsPerMm && analysis.pixelsPerMm > 0),
      hasPoints: (analysis.landmarks?.length ?? 0) >= REQUIRED_LANDMARKS,
      hasMeasurements: (analysis.measurements?.length ?? 0) > 0,
      isDirty,
    }),
    analysisMetadata: {
      notes: analysis.notes?.trim() || null,
      isAutoTraced: Boolean(analysis.isAutoTraced),
      doctorId: analysis.doctorId ?? null,
    },
  };
}

// ---------------------------------------------------------------------------
// Cephalometric DATA-QUALITY signals (audit §12)
// ---------------------------------------------------------------------------
// Before layering more AI on top of the ceph workflow, the doctor needs the
// quality of the *underlying* data surfaced honestly: how confident the AI was
// in each placed landmark, whether the mm-per-pixel calibration is actually
// valid, whether the landmark set is complete, and whether the saved
// measurements still reflect the points on the canvas. These are warnings, not
// gates — the Sprint 6 approval/PDF gate (computeCephReadiness) stays the hard
// line. This block only adds advisory signals.

/**
 * Confidence is stored on a 0..1 scale (the backend clamps the AI provider's
 * value to [0,1], and the canvas renders it as `confidence * 100`%). An AI-
 * placed landmark below this threshold is flagged as low-confidence and should
 * be re-checked manually before the analysis is trusted.
 */
export const LOW_CONFIDENCE_THRESHOLD = 0.7;

/** Severity of a quality warning — drives the banner colour, not a hard gate. */
export type CephQualitySeverity = "warning" | "info";

export interface CephQualityWarning {
  /** Stable identifier for keys/testing. */
  key:
    | "calibration"
    | "lowConfidence"
    | "incompleteLandmarks"
    | "unsavedEdits"
    | "staleMeasurements";
  severity: CephQualitySeverity;
  /** Arabic, RTL message shown to the doctor. */
  message: string;
}

/** Minimal landmark shape the quality computation needs. */
export interface CephQualityLandmark {
  key?: string;
  isAiPlaced?: boolean;
  confidence?: number | null;
}

export interface CephQualityInput {
  /** Calibration value (pixels per mm) from the SAVED record or live edit. */
  pixelsPerMm?: number | null;
  /** The current landmark set (live canvas state is fine). */
  landmarks: CephQualityLandmark[];
  /** Count of SAVED computed measurements. */
  measurementCount: number;
  /** Unsaved landmark/calibration edits pending a "save & compute". */
  isDirty?: boolean;
}

export interface CephQualityReport {
  /** True when there is at least one warning-severity signal. */
  hasWarnings: boolean;
  /** All signals (warnings + info), ordered most-actionable first. */
  warnings: CephQualityWarning[];
  /** Keys of AI landmarks whose confidence is below the threshold. */
  lowConfidenceKeys: string[];
  /** Whether the saved calibration is present and a positive, finite number. */
  calibrationValid: boolean;
}

/**
 * A calibration value is only valid when it is a finite, strictly-positive
 * number. `null`/`undefined`/`0`/negative/NaN all mean "not calibrated", in
 * which case linear (mm) measurements cannot be trusted.
 */
export function isCalibrationValid(pixelsPerMm?: number | null): boolean {
  return typeof pixelsPerMm === "number" && Number.isFinite(pixelsPerMm) && pixelsPerMm > 0;
}

/**
 * Returns the keys of AI-placed landmarks whose confidence is below
 * LOW_CONFIDENCE_THRESHOLD. A manually-placed landmark is never low-confidence
 * (the doctor placed it). An AI landmark with no confidence value is treated as
 * low-confidence — an unknown confidence is not a trustworthy one.
 */
export function lowConfidenceLandmarks(
  landmarks: CephQualityLandmark[],
  threshold: number = LOW_CONFIDENCE_THRESHOLD,
): string[] {
  return (landmarks ?? [])
    .filter(
      (l) =>
        l.isAiPlaced === true &&
        (typeof l.confidence !== "number" || l.confidence < threshold),
    )
    .map((l, i) => l.key ?? `#${i}`);
}

/**
 * Computes the advisory data-quality report. Order of warnings mirrors the
 * readiness reason priority: unsaved edits first (they make everything below
 * stale), then calibration, then low-confidence AI points, then incomplete
 * landmarks, then a stale-measurements hint.
 */
export function computeCephQuality(input: CephQualityInput): CephQualityReport {
  const dirty = Boolean(input.isDirty);
  const landmarks = input.landmarks ?? [];
  const placed = landmarks.length;
  const calibrationValid = isCalibrationValid(input.pixelsPerMm);
  const lowConfidenceKeys = lowConfidenceLandmarks(landmarks);

  const warnings: CephQualityWarning[] = [];

  if (dirty) {
    warnings.push({
      key: "unsavedEdits",
      severity: "warning",
      message:
        "لديك تعديلات غير محفوظة على النقاط أو المعايرة — اضغط «حفظ وحساب» حتى تعكس القياسات الوضع الحالي.",
    });
  }

  if (!calibrationValid) {
    warnings.push({
      key: "calibration",
      severity: "warning",
      message:
        "المعايرة (بكسل/مم) غير محفوظة أو غير صالحة — قد تكون القياسات الخطية بالمليمتر غير دقيقة حتى تُعايَر الصورة.",
    });
  }

  if (lowConfidenceKeys.length > 0) {
    warnings.push({
      key: "lowConfidence",
      severity: "warning",
      message: `توجد ${lowConfidenceKeys.length} نقطة موضوعة بالذكاء الاصطناعي بثقة منخفضة (${lowConfidenceKeys.join("، ")}) — راجعها يدويًا قبل اعتماد التحليل.`,
    });
  }

  if (placed < REQUIRED_LANDMARKS) {
    warnings.push({
      key: "incompleteLandmarks",
      severity: "warning",
      message: `النقاط غير مكتملة (${placed}/${REQUIRED_LANDMARKS}) — أكمل وضع جميع النقاط قبل الاعتماد على القياسات.`,
    });
  }

  // Stale measurements: points exist but no saved measurements were computed
  // from them. (When dirty, the unsaved-edits warning already covers this, so
  // we only surface this distinct hint on a clean record.)
  if (!dirty && placed > 0 && input.measurementCount === 0) {
    warnings.push({
      key: "staleMeasurements",
      severity: "info",
      message:
        "لا توجد قياسات محفوظة لهذه النقاط بعد — اضغط «حفظ وحساب» لإعادة حساب القياسات.",
    });
  }

  return {
    hasWarnings: warnings.some((w) => w.severity === "warning"),
    warnings,
    lowConfidenceKeys,
    calibrationValid,
  };
}
