import { describe, expect, it } from "vitest";
import { compareStoredPhotoMeasurements, parseStoredPhotoMeasurements, selectChronologicalPhotoPair } from "@/lib/photoAnalysisComparison";

describe("photoAnalysisComparison", () => {
  it("parses finite saved measurements and ignores malformed or duplicate rows", () => {
    const parsed = parseStoredPhotoMeasurements(JSON.stringify([
      { key: "FacialConvexity", nameAr: "تحدب الوجه", value: 12.5, normal: 12 },
      { key: "FacialConvexity", nameAr: "مكرر", value: 99 },
      { key: "Bad", nameAr: "غير صالح", value: "12" },
      null,
    ]));

    expect(parsed).toEqual([
      expect.objectContaining({ key: "FacialConvexity", nameAr: "تحدب الوجه", value: 12.5, normal: 12 }),
    ]);
    expect(parseStoredPhotoMeasurements("not-json")).toEqual([]);
  });

  it("compares the union of saved measurements without inventing missing values", () => {
    const rows = compareStoredPhotoMeasurements(
      [{ key: "A", nameAr: "أ", value: 10 }, { key: "B", nameAr: "ب", value: null }],
      [{ key: "A", nameAr: "أ", value: 12.25 }, { key: "C", nameAr: "ج", value: 3 }],
    );

    expect(rows).toEqual([
      { key: "A", nameAr: "أ", baseValue: 10, targetValue: 12.25, delta: 2.25 },
      { key: "B", nameAr: "ب", baseValue: null, targetValue: null, delta: null },
      { key: "C", nameAr: "ج", baseValue: null, targetValue: 3, delta: null },
    ]);
  });

  it("normalizes a reversed request to an earlier before and later after record", () => {
    const records = [
      { id: "middle", analysisDate: "2026-03-01" },
      { id: "latest", analysisDate: "2026-07-01" },
      { id: "oldest", analysisDate: "2026-01-01" },
    ];

    expect(selectChronologicalPhotoPair(records, "latest", "oldest")).toEqual({
      baseId: "oldest",
      targetId: "latest",
    });
    expect(selectChronologicalPhotoPair(records, "oldest", "middle")).toEqual({
      baseId: "oldest",
      targetId: "middle",
    });
  });
});
