import type { CSSProperties } from "react";

/**
 * Shared dashboard card style — the exact ("ZIP-matched") inline style object
 * that was duplicated verbatim across the dashboard home page and several
 * dashboard widgets. Extracted here to remove that duplication WITHOUT changing
 * any pixels (same background, radius, padding, shadow and border values).
 *
 * For new code prefer the token-based <Card /> primitive; these constants exist
 * to dedupe the existing pixel-tuned dashboard surfaces only.
 */
export const cardStyle: CSSProperties = {
  background: "#fff",
  borderRadius: 12,
  padding: 20,
  boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
  border: "1px solid #e8f0f9",
};

/** Same as {@link cardStyle} but without padding and with clipped corners. */
export const cardNoPadStyle: CSSProperties = {
  background: "#fff",
  borderRadius: 12,
  boxShadow: "0 1px 3px rgba(13,33,55,0.06), 0 1px 10px rgba(13,33,55,0.04)",
  border: "1px solid #e8f0f9",
  overflow: "hidden",
};
