export interface ImagePoint {
  x: number;
  y: number;
}

export interface RedactionRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export type CalibrationInputMode = "pixelsPerMm" | "mmPerPixel";

const clamp = (value: number, min: number, max: number) =>
  Math.min(max, Math.max(min, value));

export function normalizeRedactionRect(
  start: ImagePoint,
  end: ImagePoint,
  imageWidth: number,
  imageHeight: number,
  minimumSize = 4,
): RedactionRect | null {
  if (imageWidth <= 0 || imageHeight <= 0) return null;

  const startX = clamp(start.x, 0, imageWidth);
  const startY = clamp(start.y, 0, imageHeight);
  const endX = clamp(end.x, 0, imageWidth);
  const endY = clamp(end.y, 0, imageHeight);
  const x = Math.min(startX, endX);
  const y = Math.min(startY, endY);
  const width = Math.abs(endX - startX);
  const height = Math.abs(endY - startY);

  if (width < minimumSize || height < minimumSize) return null;
  return { x, y, width, height };
}

export function toMmPerPixel(
  rawValue: string | number,
  mode: CalibrationInputMode,
): number | null {
  const value = typeof rawValue === "number" ? rawValue : Number(rawValue);
  if (!Number.isFinite(value) || value <= 0) return null;
  const mmPerPixel = mode === "pixelsPerMm" ? 1 / value : value;
  if (mmPerPixel <= 0 || mmPerPixel > 10) return null;
  return Number(mmPerPixel.toFixed(8));
}

export async function createOpaquePilotToken(): Promise<string> {
  const randomBytes = new Uint8Array(32);
  crypto.getRandomValues(randomBytes);
  const digest = await crypto.subtle.digest("SHA-256", randomBytes);
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}
