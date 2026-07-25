import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const clinicTab = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/settings/_components/ClinicTab.tsx"),
  "utf8"
);

describe("patient number prefix settings contract", () => {
  it("submits the visible prefix field to the canonical database setting", () => {
    expect(clinicTab).toContain('{ key: "patient.number_prefix", category: "patients" }');
    expect(clinicTab).toContain('api.put(`/api/settings/${encodeURIComponent(key)}`');
  });

  it("validates a short alphanumeric prefix and explains its scope", () => {
    expect(clinicTab).toContain("الحد الأقصى 8 أحرف");
    expect(clinicTab).toContain("استخدم أحرفًا وأرقامًا فقط");
    expect(clinicTab).toContain("تُستخدم في أرقام المرضى الجدد فقط");
  });
});
