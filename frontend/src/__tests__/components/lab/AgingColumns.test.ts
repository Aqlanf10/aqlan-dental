import { describe, it, expect } from "vitest";
import { agingColumns } from "@/app/(dashboard)/lab/_panels/ReportsPanel";

/**
 * CORE-LAB-020 — the ageing column headers are derived from the boundaries the server used,
 * not written out. The bug this guards against is quiet and expensive: the owner renegotiates a
 * lab to net-45, the numbers move into the new buckets, and the headers keep saying 30 and 60 —
 * so the report shows the right money under the wrong period and nobody can tell.
 */
describe("agingColumns", () => {
  it("labels the buckets from the boundaries it is given", () => {
    const cols = agingColumns({ bucket1Days: 30, bucket2Days: 60, bucket3Days: 90 });

    expect(cols.map((c) => c.label)).toEqual([
      "لم يستحق بعد",
      "١–30 يوم",
      "31–60 يوم",
      "61–90 يوم",
      "أكثر من 90 يوم",
      "بلا تاريخ استحقاق",
    ]);
  });

  it("follows the boundaries when the owner changes credit terms", () => {
    const cols = agingColumns({ bucket1Days: 7, bucket2Days: 14, bucket3Days: 21 });

    expect(cols[1].label).toBe("١–7 يوم");
    expect(cols[2].label).toBe("8–14 يوم");
    expect(cols[3].label).toBe("15–21 يوم");
    expect(cols[4].label).toBe("أكثر من 21 يوم");
  });

  it("leaves no gap or overlap between consecutive buckets", () => {
    const buckets = { bucket1Days: 30, bucket2Days: 60, bucket3Days: 90 };
    const cols = agingColumns(buckets);

    // Each bucket must start the day after the previous one ends, or a bill aged exactly
    // on a boundary falls into two columns at once — or into none.
    expect(cols[2].label.startsWith(`${buckets.bucket1Days + 1}–`)).toBe(true);
    expect(cols[3].label.startsWith(`${buckets.bucket2Days + 1}–`)).toBe(true);
  });

  it("keeps bills with no due date in their own column", () => {
    const cols = agingColumns({ bucket1Days: 30, bucket2Days: 60, bucket3Days: 90 });

    // Folding these into "لم يستحق بعد" is how a debt nobody agreed a date for stops being
    // visible, so the two must stay distinct keys.
    expect(cols.map((c) => c.key)).toContain("noDueDate");
    expect(cols.map((c) => c.key)).toContain("notYetDue");
  });
});
