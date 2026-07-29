import { describe, expect, it } from "vitest";
import {
  applyViewerMatrix,
  buildViewerMatrix,
  invertViewerMatrix,
  viewerAngle,
  viewerDistance,
  type ViewerRotation,
} from "@/lib/cephViewer";

describe("ceph viewer measurements", () => {
  it("reports calibrated and pixel distances without changing source points", () => {
    const start = { x: 0, y: 0 };
    const end = { x: 30, y: 40 };
    expect(viewerDistance(start, end, 5)).toEqual({ pixels: 50, millimeters: 10 });
    expect(viewerDistance(start, end, null)).toEqual({ pixels: 50, millimeters: null });
    expect(start).toEqual({ x: 0, y: 0 });
    expect(end).toEqual({ x: 30, y: 40 });
  });

  it("measures the angle at the middle point", () => {
    expect(viewerAngle({ x: 1, y: 0 }, { x: 0, y: 0 }, { x: 0, y: 1 })).toBeCloseTo(90, 8);
    expect(viewerAngle({ x: -1, y: 0 }, { x: 0, y: 0 }, { x: 1, y: 0 })).toBeCloseTo(180, 8);
  });
});

describe("ceph viewer preview transforms", () => {
  const rotations: ViewerRotation[] = [0, 90, 180, 270];

  it.each(rotations)("round-trips image coordinates after a %i degree rotation", rotation => {
    const matrix = buildViewerMatrix(800, 600, {
      rotation,
      flipHorizontal: true,
      flipVertical: true,
    });
    const source = { x: 173.5, y: 411.25 };
    const displayed = applyViewerMatrix(source, matrix);
    const restored = invertViewerMatrix(displayed, matrix);
    expect(restored.x).toBeCloseTo(source.x, 8);
    expect(restored.y).toBeCloseTo(source.y, 8);
  });

  it("swaps fitted dimensions for quarter turns", () => {
    expect(buildViewerMatrix(800, 600, { rotation: 90, flipHorizontal: false, flipVertical: false }))
      .toMatchObject({ width: 600, height: 800 });
    expect(buildViewerMatrix(800, 600, { rotation: 180, flipHorizontal: false, flipVertical: false }))
      .toMatchObject({ width: 800, height: 600 });
  });

  it("applies horizontal and vertical flips in displayed axes after rotation", () => {
    const source = { x: 100, y: 200 };
    const horizontal = buildViewerMatrix(800, 600, {
      rotation: 90,
      flipHorizontal: true,
      flipVertical: false,
    });
    const vertical = buildViewerMatrix(800, 600, {
      rotation: 90,
      flipHorizontal: false,
      flipVertical: true,
    });
    expect(applyViewerMatrix(source, horizontal)).toEqual({ x: 200, y: 100 });
    expect(applyViewerMatrix(source, vertical)).toEqual({ x: 400, y: 700 });
  });

  it("keeps every transformed corner inside the transformed image bounds", () => {
    const matrix = buildViewerMatrix(800, 600, {
      rotation: 270,
      flipHorizontal: true,
      flipVertical: false,
    });
    const corners = [
      { x: 0, y: 0 },
      { x: 800, y: 0 },
      { x: 0, y: 600 },
      { x: 800, y: 600 },
    ].map(point => applyViewerMatrix(point, matrix));
    for (const point of corners) {
      expect(point.x).toBeGreaterThanOrEqual(0);
      expect(point.x).toBeLessThanOrEqual(matrix.width);
      expect(point.y).toBeGreaterThanOrEqual(0);
      expect(point.y).toBeLessThanOrEqual(matrix.height);
    }
  });
});
