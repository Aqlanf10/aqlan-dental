import { describe, expect, it } from "vitest";
import {
  buildSuperimpositionExportMetadata,
  hasMixedOrthoCases,
  registerSuperimpositionRecords,
  type SuperimpositionRecordInput,
} from "@/lib/cephSuperimposition";

function record(
  id: string,
  shiftX: number,
  shiftY: number,
  includeN = true,
): SuperimpositionRecordInput {
  return {
    id,
    label: id,
    date: "2026-07-13",
    color: id === "first" ? "#2563eb" : "#059669",
    landmarks: [
      { key: "S", x: shiftX, y: shiftY },
      ...(includeN ? [{ key: "N", x: shiftX + 100, y: shiftY }] : []),
      { key: "A", x: shiftX + 80, y: shiftY + 60 },
    ],
  };
}

describe("registerSuperimpositionRecords", () => {
  it("registers every tracing into the selected reference frame", () => {
    const result = registerSuperimpositionRecords(
      [record("first", 0, 0), record("second", 300, 120), record("third", -50, 80)],
      "second",
    );

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    const reference = result.records.find(item => item.id === "second")!;
    for (const registered of result.records) {
      expect(registered.points.get("S")).toEqual(reference.points.get("S"));
      expect(registered.points.get("N")).toEqual(reference.points.get("N"));
      expect(registered.points.get("A")!.x).toBeCloseTo(reference.points.get("A")!.x, 6);
      expect(registered.points.get("A")!.y).toBeCloseTo(reference.points.get("A")!.y, 6);
    }
  });

  it("reports every record missing an SN registration landmark", () => {
    const result = registerSuperimpositionRecords(
      [record("first", 0, 0), record("second", 20, 20, false)],
      "first",
    );

    expect(result).toEqual({
      ok: false,
      reason: "registration_landmarks_missing",
      missingRecordIds: ["second"],
    });
  });

  it("rejects an unknown reference rather than silently changing it", () => {
    const result = registerSuperimpositionRecords(
      [record("first", 0, 0), record("second", 20, 20)],
      "missing",
    );

    expect(result).toEqual({
      ok: false,
      reason: "reference_missing",
      missingRecordIds: ["missing"],
    });
  });
});

describe("buildSuperimpositionExportMetadata", () => {
  it("preserves reference, identity, colors, dates, and opacity", () => {
    const records = [record("first", 0, 0), record("second", 20, 20)];

    const metadata = buildSuperimpositionExportMetadata(records, "second", {
      first: 0.35,
      second: 1,
    });

    expect(metadata.referenceId).toBe("second");
    expect(metadata.registerKeys).toEqual(["S", "N"]);
    expect(metadata.records).toEqual([
      expect.objectContaining({ id: "first", color: "#2563eb", opacity: 0.35 }),
      expect.objectContaining({ id: "second", color: "#059669", opacity: 1 }),
    ]);
  });
});

describe("hasMixedOrthoCases", () => {
  it("blocks cross-case overlays even when every record is individually accessible", () => {
    expect(hasMixedOrthoCases([
      { orthoCaseId: "case-a" },
      { orthoCaseId: "case-b" },
    ])).toBe(true);
    expect(hasMixedOrthoCases([
      { orthoCaseId: "case-a" },
      { orthoCaseId: "case-a" },
    ])).toBe(false);
  });
});
