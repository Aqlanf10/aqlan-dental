import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const source = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/patients/[id]/page.tsx"),
  "utf8"
);

describe("Patient profile identifier routing contract", () => {
  it("resolves patient numbers before any GUID-only profile request", () => {
    expect(source).toContain('api.get(`/api/patients/by-number/${encodeURIComponent(patientId)}`)');
    expect(source).toContain('if (!patientIdentifier || !hasGuidPatientId) return;');
    expect(source).toContain('api.get<PatientProfile>(`/api/patients/${patientIdentifier}`)');

    const guardIndex = source.indexOf('if (!patientIdentifier || !hasGuidPatientId) return;');
    const profileRequestIndex = source.indexOf('api.get<PatientProfile>(`/api/patients/${patientIdentifier}`)');
    expect(guardIndex).toBeGreaterThan(-1);
    expect(profileRequestIndex).toBeGreaterThan(guardIndex);
  });

  it("distinguishes a real 404 from retryable lookup failures", () => {
    expect(source).toContain("if (status === 404)");
    expect(source).toContain("setResolutionRetryable(false)");
    expect(source).toContain("setResolutionRetryable(true)");
    expect(source).toContain("تعذر البحث عن المريض برقم الملف — تحقق من الاتصال وحاول مجدداً");
  });

  it("provides independent retries for number resolution and GUID profile loading", () => {
    expect(source).toContain("const [resolutionRetryKey, setResolutionRetryKey] = useState(0)");
    expect(source).toContain("const [profileRetryKey, setProfileRetryKey] = useState(0)");
    expect(source).toContain("setProfileRetryKey(k => k + 1)");
    expect(source).toContain("setResolutionRetryKey(k => k + 1)");
  });

  it("guards stale requests when the route identifier changes", () => {
    expect(source).toContain("let cancelled = false");
    expect(source.match(/return \(\) => \{ cancelled = true; \};/g)?.length ?? 0).toBeGreaterThanOrEqual(2);
  });
});
