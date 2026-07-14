import { describe, it, expect } from "vitest";
import {
  CORE_LANDMARK_KEYS,
  computeCephReadiness,
  cephReadinessFromAnalysis,
  computeCephQuality,
  isCalibrationValid,
  lowConfidenceLandmarks,
  LOW_CONFIDENCE_THRESHOLD,
  type CephReadinessInput,
  type CephQualityLandmark,
} from "@/lib/cephReadiness";
import type { CephAnalysis } from "@/types/ceph";

const complete: CephReadinessInput = {
  hasImage: true,
  hasCalibration: true,
  hasPoints: true,
  hasMeasurements: true,
  isDirty: false,
};

describe("computeCephReadiness", () => {
  it("is ready when every input is present and nothing is unsaved", () => {
    const r = computeCephReadiness(complete);
    expect(r.ready).toBe(true);
    expect(r.reason).toBeNull();
    expect(r.verdict).toBe("جاهز للتقرير");
    expect(r.items.every((i) => i.ok)).toBe(true);
  });

  it("blocks readiness on unsaved edits, even when all data exists", () => {
    const r = computeCephReadiness({ ...complete, isDirty: true });
    expect(r.ready).toBe(false);
    expect(r.verdict).toBe("يحتاج حفظ وحساب");
    expect(r.reason).toContain("غير محفوظة");
  });

  it("reports missing measurements when not yet computed", () => {
    const r = computeCephReadiness({ ...complete, hasMeasurements: false });
    expect(r.ready).toBe(false);
    expect(r.reason).toContain("القياسات");
  });

  it("prioritizes the unsaved reason over missing inputs", () => {
    const r = computeCephReadiness({
      hasImage: true,
      hasCalibration: false,
      hasPoints: true,
      hasMeasurements: false,
      isDirty: true,
    });
    expect(r.reason).toContain("غير محفوظة");
  });

  it("flags a missing image first among saved-data gaps", () => {
    const r = computeCephReadiness({
      hasImage: false,
      hasCalibration: false,
      hasPoints: false,
      hasMeasurements: false,
      isDirty: false,
    });
    expect(r.ready).toBe(false);
    expect(r.reason).toContain("صورة");
  });

  it("notes missing calibration when everything else is saved", () => {
    const r = computeCephReadiness({ ...complete, hasCalibration: false });
    expect(r.ready).toBe(false);
    expect(r.reason).toContain("المعايرة");
  });
});

describe("cephReadinessFromAnalysis", () => {
  // A fully-placed CORE landmark set — the report/VTO requirement. Codex P1
  // on #677: readiness now checks the core KEYS, not the count, so the test
  // helper takes a slice of the real core set.
  const landmarks = (count: number) =>
    CORE_LANDMARK_KEYS.slice(0, count).map((key) => ({ key }));

  const withLandmarks = (count: number) =>
    ({
      xrayFileUrl: "/uploads/x.jpg",
      pixelsPerMm: 12.5,
      landmarks: landmarks(count),
      measurements: [{ id: "m1" }],
    }) as unknown as Pick<
      CephAnalysis,
      "xrayFileUrl" | "pixelsPerMm" | "landmarks" | "measurements"
    >;

  const base = withLandmarks(24);

  it("is ready with 24 landmarks + calibration + measurements + image, not dirty", () => {
    const r = cephReadinessFromAnalysis(base, false);
    expect(r.ready).toBe(true);
    expect(r.items.find((i) => i.key === "hasPoints")?.ok).toBe(true);
  });

  it("is NOT ready when optional points pad the count but core points are missing (Codex P1 #677)", () => {
    const padded = {
      ...base,
      landmarks: [
        ...CORE_LANDMARK_KEYS.slice(0, 21).map((key) => ({ key })),
        { key: "SPog" }, { key: "U6" }, { key: "L6" },
      ],
    } as unknown as typeof base;
    const r = cephReadinessFromAnalysis(padded, false);
    expect(r.ready).toBe(false);
    expect(r.items.find((i) => i.key === "hasPoints")?.ok).toBe(false);
  });

  it("stays ready when the full core set PLUS the optional points are placed", () => {
    const full = {
      ...base,
      landmarks: [
        ...CORE_LANDMARK_KEYS.map((key) => ({ key })),
        { key: "SPog" }, { key: "U6" }, { key: "L6" },
      ],
    } as unknown as typeof base;
    expect(cephReadinessFromAnalysis(full, false).ready).toBe(true);
  });

  it("is NOT ready with a single landmark", () => {
    const r = cephReadinessFromAnalysis(withLandmarks(1), false);
    expect(r.ready).toBe(false);
    expect(r.items.find((i) => i.key === "hasPoints")?.ok).toBe(false);
  });

  it("is NOT ready at 23 landmarks (just below the full set)", () => {
    const r = cephReadinessFromAnalysis(withLandmarks(23), false);
    expect(r.ready).toBe(false);
    expect(r.items.find((i) => i.key === "hasPoints")?.ok).toBe(false);
    expect(r.reason).toContain("النقاط");
  });

  it("treats null calibration as not-saved", () => {
    const r = cephReadinessFromAnalysis({ ...base, pixelsPerMm: null }, false);
    expect(r.ready).toBe(false);
    expect(r.items.find((i) => i.key === "hasCalibration")?.ok).toBe(false);
  });

  it("treats a dirty canvas as not ready", () => {
    expect(cephReadinessFromAnalysis(base, true).ready).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Data-quality signals (audit §12)
// ---------------------------------------------------------------------------

describe("isCalibrationValid", () => {
  it("accepts a finite, positive number", () => {
    expect(isCalibrationValid(12.5)).toBe(true);
  });
  it("rejects null / undefined / zero / negative / NaN", () => {
    expect(isCalibrationValid(null)).toBe(false);
    expect(isCalibrationValid(undefined)).toBe(false);
    expect(isCalibrationValid(0)).toBe(false);
    expect(isCalibrationValid(-3)).toBe(false);
    expect(isCalibrationValid(Number.NaN)).toBe(false);
    expect(isCalibrationValid(Number.POSITIVE_INFINITY)).toBe(false);
  });
});

describe("lowConfidenceLandmarks", () => {
  const lm = (over: Partial<CephQualityLandmark>): CephQualityLandmark => ({
    key: "S",
    isAiPlaced: false,
    confidence: 1,
    ...over,
  });

  it("uses a 0..1 threshold of 0.7 by default", () => {
    expect(LOW_CONFIDENCE_THRESHOLD).toBe(0.7);
  });

  it("flags AI landmarks below the threshold", () => {
    const keys = lowConfidenceLandmarks([
      lm({ key: "S", isAiPlaced: true, confidence: 0.55 }),
      lm({ key: "N", isAiPlaced: true, confidence: 0.95 }),
    ]);
    expect(keys).toEqual(["S"]);
  });

  it("never flags a manually-placed landmark, even at low confidence", () => {
    const keys = lowConfidenceLandmarks([
      lm({ key: "A", isAiPlaced: false, confidence: 0.1 }),
    ]);
    expect(keys).toEqual([]);
  });

  it("treats an AI landmark with unknown confidence as low-confidence", () => {
    const keys = lowConfidenceLandmarks([
      lm({ key: "B", isAiPlaced: true, confidence: undefined }),
    ]);
    expect(keys).toEqual(["B"]);
  });

  it("does not flag an AI landmark exactly at the threshold", () => {
    const keys = lowConfidenceLandmarks([
      lm({ key: "Po", isAiPlaced: true, confidence: 0.7 }),
    ]);
    expect(keys).toEqual([]);
  });
});

describe("computeCephQuality", () => {
  const aiLow = (key: string): CephQualityLandmark => ({
    key,
    isAiPlaced: true,
    confidence: 0.4,
  });
  const manual = (key: string): CephQualityLandmark => ({
    key,
    isAiPlaced: false,
    confidence: 1,
  });
  const fullManualSet = CORE_LANDMARK_KEYS.map((k) => manual(k));

  it("reports no warnings for a complete, calibrated, computed, clean analysis", () => {
    const q = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: fullManualSet,
      measurementCount: 10,
      isDirty: false,
    });
    expect(q.hasWarnings).toBe(false);
    expect(q.warnings).toHaveLength(0);
    expect(q.calibrationValid).toBe(true);
    expect(q.lowConfidenceKeys).toEqual([]);
  });

  it("warns about missing/invalid calibration", () => {
    const q = computeCephQuality({
      pixelsPerMm: null,
      landmarks: fullManualSet,
      measurementCount: 10,
      isDirty: false,
    });
    expect(q.calibrationValid).toBe(false);
    expect(q.warnings.some((w) => w.key === "calibration")).toBe(true);
    expect(q.hasWarnings).toBe(true);
  });

  it("warns about low-confidence AI landmarks and lists their keys", () => {
    const q = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: [...fullManualSet.slice(0, 22), aiLow("S"), aiLow("N")],
      measurementCount: 10,
      isDirty: false,
    });
    const w = q.warnings.find((x) => x.key === "lowConfidence");
    expect(w).toBeDefined();
    expect(q.lowConfidenceKeys).toEqual(["S", "N"]);
    expect(w?.message).toContain("S");
    expect(w?.message).toContain("N");
  });

  it("blocks quality clearance while AI or WebCeph points remain unreviewed", () => {
    const q = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: [
        ...fullManualSet,
        { key: "WC_Ba", placementSource: "webceph-import", isReviewed: false },
        { key: "Xi", placementSource: "webceph-import", isReviewed: true },
      ],
      measurementCount: 10,
      isDirty: false,
    });

    expect(q.unreviewedExternalKeys).toEqual(["WC_Ba"]);
    expect(q.warnings.find((warning) => warning.key === "unreviewedExternal")?.message)
      .toContain("1 نقطة");
    expect(q.hasWarnings).toBe(true);
  });

  it("warns when fewer than 24 landmarks are placed", () => {
    const q = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: fullManualSet.slice(0, 20),
      measurementCount: 10,
      isDirty: false,
    });
    const w = q.warnings.find((x) => x.key === "incompleteLandmarks");
    expect(w).toBeDefined();
    expect(w?.message).toContain("20/24");
  });

  it("warns about unsaved edits when dirty", () => {
    const q = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: fullManualSet,
      measurementCount: 10,
      isDirty: true,
    });
    expect(q.warnings[0]?.key).toBe("unsavedEdits");
  });

  it("flags stale measurements only on a clean record with points but no measurements", () => {
    const clean = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: fullManualSet,
      measurementCount: 0,
      isDirty: false,
    });
    expect(clean.warnings.some((w) => w.key === "staleMeasurements")).toBe(true);

    // When dirty, the unsaved-edits warning subsumes the stale hint.
    const dirty = computeCephQuality({
      pixelsPerMm: 12.5,
      landmarks: fullManualSet,
      measurementCount: 0,
      isDirty: true,
    });
    expect(dirty.warnings.some((w) => w.key === "staleMeasurements")).toBe(false);
  });
});
