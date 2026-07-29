import { describe, it, expect } from "vitest";
import { computePhotoMeasurements, profileType } from "@/lib/photoAnalysis";

type Pt = { x: number; y: number };

describe("computePhotoMeasurements", () => {
  it("computes a right-angle nasolabial angle from a known L-shape", () => {
    // Cm above Sn, Ls to the right of Sn → 90° at Sn (image coords, +y down).
    const pts: Record<string, Pt> = {
      Cm: { x: 0, y: -10 }, Sn: { x: 0, y: 0 }, Ls: { x: 10, y: 0 },
    };
    const naso = computePhotoMeasurements(pts).find((m) => m.key === "Nasolabial")!;
    expect(naso.value).toBeCloseTo(90, 1);
  });

  it("facial convexity is 180 minus the G-Sn-Pog angle (0 when collinear)", () => {
    // G, Sn, Pog vertically collinear → angle 180° → convexity 0°.
    const pts: Record<string, Pt> = {
      G: { x: 0, y: 0 }, Sn: { x: 0, y: 10 }, Pog: { x: 0, y: 20 },
    };
    const fc = computePhotoMeasurements(pts).find((m) => m.key === "FacialConvexity")!;
    expect(fc.value).toBeCloseTo(0, 1);
  });

  it("flags a convex profile and classifies it Class II", () => {
    // Pog retruded (to the left) relative to the G–Sn line → convex.
    const pts: Record<string, Pt> = {
      G: { x: 0, y: 0 }, Sn: { x: 10, y: 50 }, Pog: { x: 0, y: 100 },
    };
    const ms = computePhotoMeasurements(pts);
    const fc = ms.find((m) => m.key === "FacialConvexity")!;
    expect(fc.value! > 16).toBe(true);
    expect(profileType(ms)).toContain("محدّب");
  });

  it("returns null values when required landmarks are missing", () => {
    const ms = computePhotoMeasurements({ Sn: { x: 0, y: 0 } });
    expect(ms.every((m) => m.value === null)).toBe(true);
    expect(profileType(ms)).toBeNull();
  });

  it("classifies deviation by SD and gives a directional interpretation", () => {
    // Nasolabial 90° (norm 102 ± 8 → deviation −12 → 1.5 SD → mild, acute).
    const pts: Record<string, Pt> = {
      Cm: { x: 0, y: -10 }, Sn: { x: 0, y: 0 }, Ls: { x: 10, y: 0 },
    };
    const naso = computePhotoMeasurements(pts).find((m) => m.key === "Nasolabial")!;
    expect(naso.deviation).toBeCloseTo(-12, 1);
    expect(naso.severity).toBe("mild");
    expect(naso.interpretationAr).toContain("بروز");
  });
});
