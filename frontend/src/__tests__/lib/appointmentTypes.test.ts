import { describe, it, expect } from "vitest";
import { APPOINTMENT_TYPES } from "@/components/shared/journey/constants";

/**
 * Coverage for the ortho-visit-type options the owner asked for (Task 4.2):
 * the daily-operations booking modal and any other consumer of
 * APPOINTMENT_TYPES must offer Bonding / Adjustment / Debonding / Retainer so
 * the orthodontist can classify an ortho appointment without free-typing.
 */
describe("APPOINTMENT_TYPES — ortho visit types", () => {
  it("includes Bonding / Adjustment / Debonding / Retainer", () => {
    const values = APPOINTMENT_TYPES.map(t => t.value);
    expect(values).toContain("OrthoBonding");
    expect(values).toContain("OrthoAdjustment");
    expect(values).toContain("OrthoDebonding");
    expect(values).toContain("OrthoRetainer");
  });

  it("labels every ortho type in Arabic", () => {
    const ortho = APPOINTMENT_TYPES.filter(t => t.value.startsWith("Ortho"));
    for (const t of ortho) {
      expect(t.label.length).toBeGreaterThan(0);
      // sanity: no English-only labels slipped through
      expect(t.label).toMatch(/[\u0600-\u06FF]/);
    }
  });

  it("keeps the existing non-ortho types so other flows stay intact", () => {
    const values = APPOINTMENT_TYPES.map(t => t.value);
    expect(values).toContain("Consultation");
    expect(values).toContain("FollowUp");
    expect(values).toContain("Treatment");
    expect(values).toContain("Emergency");
    expect(values).toContain("Surgery");
  });
});
