// ---------------------------------------------------------------------------
// Anatomical tracing contours — connect placed landmarks into the recognizable
// cephalometric outline (cranial base, maxilla, mandible, facial + soft-tissue
// profile, incisors). Pure data + a pure selector so the canvas and the PDF
// report draw the SAME tracing. Honest: it only connects landmarks the
// orthodontist has placed; nothing is invented.
// ---------------------------------------------------------------------------

/** One anatomical contour: an ordered list of landmark keys + its colour. */
export interface TracingContour {
  id: string;
  /** Ordered landmark keys forming the polyline. */
  keys: string[];
  color: string;
}

export const TRACING_CONTOURS: TracingContour[] = [
  { id: "cranial-base", keys: ["S", "N"], color: "#60A5FA" },
  { id: "maxilla", keys: ["PNS", "ANS", "A"], color: "#FB923C" },
  { id: "mandible", keys: ["Co", "Go", "Me", "Pog", "B"], color: "#F87171" },
  { id: "ramus", keys: ["Ar", "Go"], color: "#F87171" },
  { id: "facial-profile", keys: ["N", "A", "Pog"], color: "#FBBF24" },
  { id: "soft-tissue", keys: ["Pn", "Cm", "LS", "LI", "Pog"], color: "#F472B6" },
  { id: "upper-incisor", keys: ["U1A", "U1T"], color: "#34D399" },
  { id: "lower-incisor", keys: ["L1A", "L1T"], color: "#10B981" },
];

/** A drawable polyline: the contour colour + the present landmark keys in order. */
export interface TracingPolyline {
  id: string;
  color: string;
  keys: string[];
}

/**
 * Returns the contours that can actually be drawn given which landmarks exist:
 * each contour keeps only its present keys (preserving order) and is included
 * only when at least two remain (a single point draws no line).
 */
export function tracingPolylines(isPresent: (key: string) => boolean): TracingPolyline[] {
  const out: TracingPolyline[] = [];
  for (const c of TRACING_CONTOURS) {
    const keys = c.keys.filter(isPresent);
    if (keys.length >= 2) out.push({ id: c.id, color: c.color, keys });
  }
  return out;
}
