import { describe, it, expect } from "vitest";
import { computeFrontalMeasurements, frontalSummary } from "@/lib/photoAnalysisFrontal";

type Pt = { x: number; y: number };

/** A perfectly symmetric, canonically-proportioned frontal layout. */
function symmetricFace(): Record<string, Pt> {
  return {
    // eyes: each eye width 30, intercanthal 30 (= eye width)
    ExR: { x: 0, y: 100 }, EnR: { x: 30, y: 100 },
    EnL: { x: 60, y: 100 }, ExL: { x: 90, y: 100 },
    PR: { x: 15, y: 100 }, PL: { x: 75, y: 100 }, // interpupillary 60
    AlR: { x: 30, y: 150 }, AlL: { x: 60, y: 150 }, // nasal width 30 (= intercanthal)
    ChR: { x: 22, y: 180 }, ChL: { x: 68, y: 180 }, // mouth width 46 (~1.53× nose), level
    Tr: { x: 45, y: 0 }, G: { x: 45, y: 60 }, Sn: { x: 45, y: 160 }, Me: { x: 45, y: 260 },
    // thirds: Tr-G = 60, G-Sn = 100, Sn-Me = 100
  };
}

describe("computeFrontalMeasurements", () => {
  it("reports a symmetric face as within range across all measures", () => {
    const ms = computeFrontalMeasurements(symmetricFace());
    const v = (k: string) => ms.find((m) => m.key === k)!;
    expect(v("EyeSymmetry").value).toBeCloseTo(1, 2);
    expect(v("IntercanthalEye").value).toBeCloseTo(1, 2);
    expect(v("NasalWidth").value).toBeCloseTo(1, 2);
    expect(v("MouthCant").value).toBeCloseTo(0, 2);
    // upper third (60) / middle (100) = 0.6 → below 1.0 norm → flagged
    expect(v("UpperMiddleThird").value).toBeCloseTo(0.6, 2);
    expect(frontalSummary(ms)).toContain("خارج");
  });

  it("computes ratios from the actual distances", () => {
    const ms = computeFrontalMeasurements(symmetricFace());
    const v = (k: string) => ms.find((m) => m.key === k)!;
    expect(v("MouthNose").value).toBeCloseTo(46 / 30, 2);
    expect(v("MouthInterpupillary").value).toBeCloseTo(46 / 60, 2);
    expect(v("LowerMiddleThird").value).toBeCloseTo(1, 2);
  });

  it("flags eye asymmetry as severe", () => {
    const pts = symmetricFace();
    pts.EnR = { x: 45, y: 100 }; // right eye much wider (45) than left (30)
    const sym = computeFrontalMeasurements(pts).find((m) => m.key === "EyeSymmetry")!;
    expect(sym.value!).toBeGreaterThan(1.1);
    expect(sym.severity).toBe("severe");
    expect(sym.interpretationAr).toContain("اليمنى أعرض");
  });

  it("detects a mouth cant from unequal commissure heights", () => {
    const pts = symmetricFace();
    pts.ChR = { x: 22, y: 188 }; // right corner 8px lower than left
    const cant = computeFrontalMeasurements(pts).find((m) => m.key === "MouthCant")!;
    expect(cant.value!).toBeGreaterThan(0);
    expect(cant.severity).not.toBe("normal");
  });

  it("returns null values for measures whose landmarks are missing", () => {
    const ms = computeFrontalMeasurements({ Sn: { x: 0, y: 0 } });
    expect(ms.every((m) => m.value === null)).toBe(true);
    expect(frontalSummary(ms)).toBeNull();
  });
});
