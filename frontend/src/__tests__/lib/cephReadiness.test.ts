import { describe, it, expect } from "vitest";
import {
  computeCephReadiness,
  cephReadinessFromAnalysis,
  type CephReadinessInput,
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
  // A fully-placed landmark set (24) — the report/VTO requirement.
  const landmarks = (count: number) =>
    Array.from({ length: count }, (_, i) => ({ key: `L${i}` }));

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
