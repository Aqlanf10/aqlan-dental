import { describe, expect, it } from "vitest";
import { patientActiveOrthoCasesUrl } from "@/lib/orthoCaseRoutes";

describe("active orthodontic case routes", () => {
  it("always sends the clinical active status explicitly", () => {
    const url = patientActiveOrthoCasesUrl("patient 1", 5);
    const parsed = new URL(url, "https://clinic.example");

    expect(parsed.pathname).toBe("/api/ortho-cases");
    expect(parsed.searchParams.get("patientId")).toBe("patient 1");
    expect(parsed.searchParams.get("status")).toBe("active");
    expect(parsed.searchParams.get("page")).toBe("1");
    expect(parsed.searchParams.get("pageSize")).toBe("5");
  });

  it("uses a bounded full patient list for surgical linking", () => {
    const parsed = new URL(
      patientActiveOrthoCasesUrl("patient-1"),
      "https://clinic.example",
    );
    expect(parsed.searchParams.get("status")).toBe("active");
    expect(parsed.searchParams.get("pageSize")).toBe("100");
  });
});
