import { describe, expect, it } from "vitest";
import type { CephLandmark } from "@/types/ceph";
import { buildPaMeasurements } from "@/lib/cephPa";
import { PA_LANDMARK_ORDER } from "@/lib/cephPa";
import { cephReadinessFromAnalysis } from "@/lib/cephReadiness";

const lm = (key: string, x: number, y: number): CephLandmark => ({
  key, x, y, name: key, nameAr: key, isAiPlaced: false,
});

describe("buildPaMeasurements", () => {
  it("uses the tilted ZR-ZL reference and patient-left sign convention", () => {
    const rows = buildPaMeasurements([
      lm("ZR", 0, 0), lm("ZL", 100, 10), lm("Cg", 50, 5),
      lm("ANS", 60, 6), lm("JR", 20, 22), lm("JL", 80, 38),
      lm("AgR", 10, 50), lm("AgL", 90, 70),
      lm("U6R", 25, 30), lm("U6L", 75, 40),
    ], 10);
    const byName = Object.fromEntries(rows.map((row) => [row.name, row.value]));
    expect(byName["PA-ANS-MSR"]).toBe(1);
    expect(byName["PA-JJ-Width"]).toBeCloseTo(6.2, 1);
    expect(byName["PA-J-Vertical-Asymmetry"]).toBe(1);
    expect(byName["PA-Maxillary-Cant"]).toBeCloseTo(5.6, 1);
  });

  it("omits linear measurements without calibration but keeps cant angles", () => {
    const rows = buildPaMeasurements([
      lm("ZR", 0, 0), lm("ZL", 100, 0), lm("JR", 10, 20), lm("JL", 90, 20),
      lm("U6R", 20, 40), lm("U6L", 80, 45),
    ], null);
    expect(rows.map((row) => row.name)).toEqual(["PA-Maxillary-Cant"]);
  });

  it("uses the 15-point PA set for report readiness", () => {
    const landmarks = PA_LANDMARK_ORDER.map((key, index) => lm(key, index * 10, index * 5));
    const measurements = buildPaMeasurements([
      ...landmarks.filter((point) => point.key !== "ZR" && point.key !== "ZL"),
      lm("ZR", 0, 0), lm("ZL", 100, 0),
    ], 10);
    const input = { analysisType: "pa" as const, xrayFileUrl: "/uploads/pa.jpg", pixelsPerMm: 10, landmarks, measurements };

    expect(cephReadinessFromAnalysis(input, false).ready).toBe(true);
    expect(cephReadinessFromAnalysis({ ...input, landmarks: landmarks.slice(0, -1) }, false).ready).toBe(false);
  });
});
