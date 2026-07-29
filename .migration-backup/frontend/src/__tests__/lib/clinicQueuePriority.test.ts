import { describe, expect, it } from "vitest";
import {
  EMERGENCY_PRIORITY,
  buildPriorityChangeBody,
  isEmergencyReasonValid,
  priorityRequiresReason,
} from "@/lib/clinicQueuePriority";

describe("CORE-F-005 clinic queue emergency-priority reason", () => {
  it("only Emergency requires a reason", () => {
    expect(priorityRequiresReason(EMERGENCY_PRIORITY)).toBe(true);
    for (const p of ["Normal", "Urgent", "VIP"]) {
      expect(priorityRequiresReason(p)).toBe(false);
    }
  });

  it("validates non-empty trimmed reason", () => {
    expect(isEmergencyReasonValid("سبب")).toBe(true);
    expect(isEmergencyReasonValid("   ")).toBe(false);
    expect(isEmergencyReasonValid("")).toBe(false);
    expect(isEmergencyReasonValid(undefined)).toBe(false);
    expect(isEmergencyReasonValid(null)).toBe(false);
  });

  it("non-emergency body omits reason even if one is passed", () => {
    expect(buildPriorityChangeBody("Urgent", "ignored")).toEqual({ priority: "Urgent" });
    expect(buildPriorityChangeBody("Normal")).toEqual({ priority: "Normal" });
  });

  it("emergency body includes the trimmed reason when valid", () => {
    expect(buildPriorityChangeBody("Emergency", "  ألم حاد  ")).toEqual({
      priority: "Emergency",
      reason: "ألم حاد",
    });
  });

  it("emergency body omits reason when invalid (caller must block the request)", () => {
    expect(buildPriorityChangeBody("Emergency", "   ")).toEqual({ priority: "Emergency" });
    expect(buildPriorityChangeBody("Emergency")).toEqual({ priority: "Emergency" });
  });
});
