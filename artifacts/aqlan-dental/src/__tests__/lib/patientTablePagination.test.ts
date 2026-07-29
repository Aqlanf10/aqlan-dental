import { describe, expect, it } from "vitest";
import { getPatientPaginationItems } from "@/lib/patientTablePagination";

describe("patient table pagination", () => {
  it("shows every page when the result set is short", () => {
    expect(getPatientPaginationItems(4, 1)).toEqual([1, 2, 3, 4]);
  });

  it("keeps the current middle page reachable with both ellipses", () => {
    expect(getPatientPaginationItems(10, 5)).toEqual([
      1, "ellipsis-left", 4, 5, 6, "ellipsis-right", 10,
    ]);
  });

  it("keeps the final pages reachable instead of stopping at page three", () => {
    expect(getPatientPaginationItems(10, 9)).toEqual([
      1, "ellipsis-left", 6, 7, 8, 9, 10,
    ]);
  });

  it("clamps invalid current pages safely", () => {
    expect(getPatientPaginationItems(10, 99)).toContain(10);
    expect(getPatientPaginationItems(10, -4)).toContain(1);
  });
});
