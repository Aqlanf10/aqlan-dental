import { describe, it, expect } from "vitest";
import { tracingPolylines, TRACING_CONTOURS } from "@/lib/cephTracing";

describe("tracingPolylines", () => {
  it("returns every contour when all landmarks are present", () => {
    const all = tracingPolylines(() => true);
    expect(all.map((c) => c.id)).toEqual(TRACING_CONTOURS.map((c) => c.id));
    // each keeps its full ordered key list
    const mandible = all.find((c) => c.id === "mandible")!;
    expect(mandible.keys).toEqual(["Co", "Go", "Me", "Pog", "B"]);
  });

  it("drops contours left with fewer than two present landmarks", () => {
    // Only S+N form a pair; every other contour keeps ≤1 of these keys.
    const present = new Set(["S", "N"]);
    const out = tracingPolylines((k) => present.has(k));
    expect(out.map((c) => c.id)).toEqual(["cranial-base"]);
  });

  it("keeps only the present keys of a partially-placed contour, in order", () => {
    // Mandible with Go, Pog, B present but Co/Me missing → ['Go','Pog','B'].
    const present = new Set(["Go", "Pog", "B"]);
    const out = tracingPolylines((k) => present.has(k));
    const mandible = out.find((c) => c.id === "mandible");
    expect(mandible?.keys).toEqual(["Go", "Pog", "B"]);
  });

  it("returns nothing when no landmark forms a pair", () => {
    expect(tracingPolylines(() => false)).toEqual([]);
  });

  // SEQ-40: "A?B" keys draw the first PRESENT alternative — the soft-tissue
  // profile ends at soft-tissue Pogonion when placed, else legacy hard Pog.
  it("prefers SPog over Pog for the soft-tissue terminal when both exist", () => {
    const soft = tracingPolylines(() => true).find((c) => c.id === "soft-tissue");
    expect(soft?.keys).toEqual(["Pn", "Cm", "LS", "LI", "SPog"]);
  });

  it("falls back to hard Pog for older analyses without SPog", () => {
    const present = new Set(["Pn", "Cm", "LS", "LI", "Pog"]);
    const soft = tracingPolylines((k) => present.has(k)).find((c) => c.id === "soft-tissue");
    expect(soft?.keys).toEqual(["Pn", "Cm", "LS", "LI", "Pog"]);
  });
});
