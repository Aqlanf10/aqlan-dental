import { describe, expect, it } from "vitest";
import { buildMonthToDateComparisonRanges, calculatePercentageChange } from "@/lib/financePeriodComparison";

describe("buildMonthToDateComparisonRanges", () => {
  it("compares the current month-to-date with the same number of days in the previous month", () => {
    const ranges = buildMonthToDateComparisonRanges(new Date(2026, 5, 15));

    expect(ranges.current).toEqual({ fromDate: "2026-06-01", toDate: "2026-06-15" });
    expect(ranges.previous).toEqual({ fromDate: "2026-05-01", toDate: "2026-05-15" });
  });

  it("caps the previous comparison at the previous month's last day", () => {
    const ranges = buildMonthToDateComparisonRanges(new Date(2026, 2, 31));

    expect(ranges.current).toEqual({ fromDate: "2026-03-01", toDate: "2026-03-31" });
    expect(ranges.previous).toEqual({ fromDate: "2026-02-01", toDate: "2026-02-28" });
  });
});

describe("calculatePercentageChange", () => {
  it("calculates positive and negative change", () => {
    expect(calculatePercentageChange(150, 100)).toBe(50);
    expect(calculatePercentageChange(75, 100)).toBe(-25);
  });

  it("handles a zero previous period without infinity", () => {
    expect(calculatePercentageChange(0, 0)).toBe(0);
    expect(calculatePercentageChange(100, 0)).toBeNull();
  });
});
