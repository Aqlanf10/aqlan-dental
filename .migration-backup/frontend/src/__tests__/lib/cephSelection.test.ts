import { describe, it, expect } from "vitest";
import { pickLatestCeph, sortCephByLatest, type CephSelectable } from "@/lib/cephSelection";

const a = (id: string, analysisDate: string, createdAt?: string): CephSelectable => ({
  id,
  analysisDate,
  createdAt,
});

describe("pickLatestCeph — matches the deck generator (analysisDate DESC, createdAt DESC)", () => {
  it("picks the newer analysisDate regardless of input order", () => {
    const list = [a("old", "2026-06-10", "2026-06-10T08:00:00Z"), a("new", "2026-06-17", "2026-06-17T08:00:00Z")];
    expect(pickLatestCeph(list)?.id).toBe("new");
    expect(pickLatestCeph([...list].reverse())?.id).toBe("new");
  });

  it("breaks a same-day tie by the later createdAt", () => {
    const earlier = a("earlier", "2026-06-17", "2026-06-17T09:00:00Z");
    const later = a("later", "2026-06-17", "2026-06-17T15:30:00Z");
    expect(pickLatestCeph([earlier, later])?.id).toBe("later");
    // Order-independent: same winner whichever way the API returned them.
    expect(pickLatestCeph([later, earlier])?.id).toBe("later");
  });

  it("agrees with an explicit full sort on a same-day set", () => {
    const list = [
      a("noon", "2026-06-17", "2026-06-17T12:00:00Z"),
      a("morning", "2026-06-17", "2026-06-17T07:00:00Z"),
      a("evening", "2026-06-17", "2026-06-17T19:00:00Z"),
    ];
    expect(sortCephByLatest(list).map((x) => x.id)).toEqual(["evening", "noon", "morning"]);
    expect(pickLatestCeph(list)?.id).toBe("evening");
  });

  it("preserves incoming order as a stable fallback when createdAt is absent", () => {
    const list = [a("first", "2026-06-17"), a("second", "2026-06-17")];
    expect(pickLatestCeph(list)?.id).toBe("first");
  });

  it("returns undefined for empty or missing input", () => {
    expect(pickLatestCeph([])).toBeUndefined();
    expect(pickLatestCeph(undefined)).toBeUndefined();
  });

  it("does not mutate the input array", () => {
    const list = [a("x", "2026-06-10"), a("y", "2026-06-17")];
    const snapshot = list.map((i) => i.id);
    pickLatestCeph(list);
    sortCephByLatest(list);
    expect(list.map((i) => i.id)).toEqual(snapshot);
  });
});
