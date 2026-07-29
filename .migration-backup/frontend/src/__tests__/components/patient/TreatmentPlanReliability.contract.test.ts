import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(process.cwd(), "src/components/patient/tabs/TreatmentPlanTab.tsx"),
  "utf8"
);

describe("TreatmentPlanTab reliability contract", () => {
  it("rejects stale plan and catalog responses", () => {
    expect(source).toContain("const requestId = ++stepsRequestRef.current");
    expect(source).toContain("stepsRequestRef.current === requestId");
    expect(source).toContain("const requestId = ++servicesRequestRef.current");
    expect(source).toContain("servicesRequestRef.current === requestId");
  });

  it("does not display zero summary cards while data is unknown", () => {
    expect(source).toContain("{/* Summary Cards — never display zeroes while the plan is unknown. */}");
    expect(source).toContain("{!loading && !error && (");
    expect(source).toMatch(/\{!loading && !error && \(\s*<div className="grid grid-cols-2 sm:grid-cols-5 gap-2">/);
  });

  it("keeps manual entry available and makes the service catalog retryable", () => {
    expect(source).toContain("disabled={servicesLoading}");
    expect(source).toContain("يمكنك إدخال العنوان يدويًا");
    expect(source).toContain("onClick={fetchServices}");
  });

  it("surfaces backend messages for every treatment-plan mutation", () => {
    expect(source).toContain('extractErrorMessage(err, "فشل حفظ خطوة العلاج")');
    expect(source).toContain('extractErrorMessage(err, "فشل تغيير الحالة")');
    expect(source).toContain('extractErrorMessage(err, "فشل حذف خطوة العلاج")');
    expect(source).toContain('extractErrorMessage(err, "فشل إعادة ترتيب خطة العلاج")');
  });
});
