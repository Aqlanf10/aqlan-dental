import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/patients/[id]/page.tsx"),
  "utf8"
);

describe("patient related cases reliability contract", () => {
  it("loads orthodontic and surgery cases independently", () => {
    expect(source).toContain("Promise.allSettled([");
    expect(source).toContain('setOrthoCasesError(extractErrorMessage(orthoResult.reason, "تعذر تحميل حالات التقويم"))');
    expect(source).toContain('setSurgeryCasesError(extractErrorMessage(surgeryResult.reason, "تعذر تحميل الحالات الجراحية"))');
    expect(source).toContain("setOrthoCases(orthoResult.value.data)");
    expect(source).toContain("setSurgeryCases(surgeryResult.value.data.data ?? [])");
  });

  it("uses a dedicated retry key instead of the finance refresh", () => {
    expect(source).toContain("const [relatedCasesRetryKey, setRelatedCasesRetryKey] = useState(0)");
    expect(source).toContain("const retryRelatedCases = () => setRelatedCasesRetryKey");
    expect(source).toContain("[patientIdentifier, hasGuidPatientId, hasClinicalAccess, relatedCasesRetryKey]");
    expect(source).toContain("onClick={retryRelatedCases}");
  });

  it("keeps loading and partial failure states explicit", () => {
    expect(source).toContain('role="status"');
    expect(source).toContain("جارٍ تحميل الحالات المرتبطة…");
    expect(source).toContain('orthoCasesError ? `حالات التقويم: ${orthoCasesError}`');
    expect(source).toContain('surgeryCasesError ? `الحالات الجراحية: ${surgeryCasesError}`');
  });
});
