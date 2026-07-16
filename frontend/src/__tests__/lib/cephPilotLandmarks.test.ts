import { describe, expect, it } from "vitest";
import { PILOT_CORE_LANDMARKS, PILOT_LANDMARKS } from "@/lib/cephPilotLandmarks";

describe("cephPilotLandmarks", () => {
  it("pins the ratified 24 core and 3 optional lateral landmarks", () => {
    expect(PILOT_CORE_LANDMARKS).toHaveLength(24);
    expect(PILOT_LANDMARKS).toHaveLength(27);
    expect(PILOT_LANDMARKS.filter(item => !item.core).map(item => item.key)).toEqual(["SPog", "U6", "L6"]);
    expect(new Set(PILOT_LANDMARKS.map(item => item.key)).size).toBe(27);
  });
});
