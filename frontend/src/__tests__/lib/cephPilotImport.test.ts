import { describe, expect, it } from "vitest";
import { normalizeRedactionRect, toMmPerPixel } from "@/lib/cephPilotImport";

describe("cephPilotImport", () => {
  it("normalizes a reverse drag and clamps it to image pixels", () => {
    expect(normalizeRedactionRect(
      { x: 900, y: 700 },
      { x: -20, y: 50 },
      800,
      600,
    )).toEqual({ x: 0, y: 50, width: 800, height: 550 });
  });

  it("rejects click-sized masks so a visual confirmation cannot imply a hidden area", () => {
    expect(normalizeRedactionRect(
      { x: 10, y: 10 },
      { x: 12, y: 13 },
      800,
      600,
    )).toBeNull();
  });

  it("converts WebCeph pixels-per-mm calibration without display rounding", () => {
    expect(toMmPerPixel("4.477", "pixelsPerMm")).toBe(0.22336386);
    expect(toMmPerPixel("0.22336386", "mmPerPixel")).toBe(0.22336386);
  });

  it("rejects missing, negative, and implausible calibration values", () => {
    expect(toMmPerPixel("", "pixelsPerMm")).toBeNull();
    expect(toMmPerPixel(-1, "mmPerPixel")).toBeNull();
    expect(toMmPerPixel(11, "mmPerPixel")).toBeNull();
  });
});
