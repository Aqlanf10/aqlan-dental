import { describe, expect, it } from "vitest";
import { canReadClinical, canWriteClinical } from "@/lib/roles";

describe("clinical role capabilities", () => {
  it.each([
    "Admin",
    "Doctor",
    "Orthodontist",
    "GeneralDentist",
    "OralSurgeon",
  ])("allows %s to read and write clinical records", (role) => {
    expect(canReadClinical(role)).toBe(true);
    expect(canWriteClinical(role)).toBe(true);
  });

  it.each([
    "Reception",
    "Accountant",
    "Assistant",
    "BranchManager",
    "Patient",
    null,
    undefined,
  ])("blocks %s from clinical records and write controls", (role) => {
    expect(canReadClinical(role)).toBe(false);
    expect(canWriteClinical(role)).toBe(false);
  });
});
