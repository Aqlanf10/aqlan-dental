export type ViewerPoint = { x: number; y: number };
export type ViewerRotation = 0 | 90 | 180 | 270;

export interface ViewerTransform {
  rotation: ViewerRotation;
  flipHorizontal: boolean;
  flipVertical: boolean;
}

export interface ViewerMatrix {
  a: number;
  b: number;
  c: number;
  d: number;
  e: number;
  f: number;
  width: number;
  height: number;
}

export const DEFAULT_VIEWER_TRANSFORM: ViewerTransform = {
  rotation: 0,
  flipHorizontal: false,
  flipVertical: false,
};

/**
 * Builds the image-space affine transform used for preview rotation and flips.
 * The returned bounds always start at (0, 0), which keeps fit/pan predictable.
 */
export function buildViewerMatrix(
  width: number,
  height: number,
  transform: ViewerTransform,
): ViewerMatrix {
  const sx = transform.flipHorizontal ? -1 : 1;
  const sy = transform.flipVertical ? -1 : 1;
  const fx = transform.flipHorizontal ? width : 0;
  const fy = transform.flipVertical ? height : 0;

  switch (transform.rotation) {
    case 90:
      return { a: 0, b: sx, c: -sy, d: 0, e: height - fy, f: fx, width: height, height: width };
    case 180:
      return { a: -sx, b: 0, c: 0, d: -sy, e: width - fx, f: height - fy, width, height };
    case 270:
      return { a: 0, b: -sx, c: sy, d: 0, e: fy, f: width - fx, width: height, height: width };
    default:
      return { a: sx, b: 0, c: 0, d: sy, e: fx, f: fy, width, height };
  }
}

export function applyViewerMatrix(point: ViewerPoint, matrix: ViewerMatrix): ViewerPoint {
  return {
    x: matrix.a * point.x + matrix.c * point.y + matrix.e,
    y: matrix.b * point.x + matrix.d * point.y + matrix.f,
  };
}

export function invertViewerMatrix(point: ViewerPoint, matrix: ViewerMatrix): ViewerPoint {
  const det = matrix.a * matrix.d - matrix.b * matrix.c;
  if (det === 0) return point;
  const x = point.x - matrix.e;
  const y = point.y - matrix.f;
  return {
    x: (matrix.d * x - matrix.c * y) / det,
    y: (-matrix.b * x + matrix.a * y) / det,
  };
}

export function viewerDistance(
  start: ViewerPoint,
  end: ViewerPoint,
  pixelsPerMm?: number | null,
): { pixels: number; millimeters: number | null } {
  const pixels = Math.hypot(end.x - start.x, end.y - start.y);
  return {
    pixels,
    millimeters: pixelsPerMm && pixelsPerMm > 0 ? pixels / pixelsPerMm : null,
  };
}

export function viewerAngle(
  firstArm: ViewerPoint,
  vertex: ViewerPoint,
  secondArm: ViewerPoint,
): number {
  const ax = firstArm.x - vertex.x;
  const ay = firstArm.y - vertex.y;
  const bx = secondArm.x - vertex.x;
  const by = secondArm.y - vertex.y;
  const magnitude = Math.hypot(ax, ay) * Math.hypot(bx, by);
  if (magnitude === 0) return 0;
  const cosine = Math.max(-1, Math.min(1, (ax * bx + ay * by) / magnitude));
  return Math.acos(cosine) * 180 / Math.PI;
}

export function isDefaultViewerTransform(transform: ViewerTransform): boolean {
  return transform.rotation === 0 && !transform.flipHorizontal && !transform.flipVertical;
}
