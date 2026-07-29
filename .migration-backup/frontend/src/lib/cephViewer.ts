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
  let matrix: ViewerMatrix;
  switch (transform.rotation) {
    case 90:
      matrix = { a: 0, b: 1, c: -1, d: 0, e: height, f: 0, width: height, height: width };
      break;
    case 180:
      matrix = { a: -1, b: 0, c: 0, d: -1, e: width, f: height, width, height };
      break;
    case 270:
      matrix = { a: 0, b: -1, c: 1, d: 0, e: 0, f: width, width: height, height: width };
      break;
    default:
      matrix = { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0, width, height };
      break;
  }

  // Flip in the displayed axes after rotation, matching the toolbar labels.
  if (transform.flipHorizontal) {
    matrix = {
      ...matrix,
      a: -matrix.a,
      c: -matrix.c,
      e: matrix.width - matrix.e,
    };
  }
  if (transform.flipVertical) {
    matrix = {
      ...matrix,
      b: -matrix.b,
      d: -matrix.d,
      f: matrix.height - matrix.f,
    };
  }
  return matrix;
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
