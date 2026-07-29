import { describe, expect, it } from "vitest";
import { buildCephAssessment } from "@/lib/cephAssessment";
import type { CephDiagnosis, CephMeasurement } from "@/types/ceph";

const diagnosis: CephDiagnosis = {
  skeletalClass: "Class II",
  verticalPattern: "Hyperdivergent",
  incisorInclination: "ميلان القواطع ضمن الحدود الطبيعية",
  softTissueSummary: "الأنسجة الرخوة في حدود طبيعية",
  doctorApproved: true,
};

const measurement: CephMeasurement = {
  name: "IMPA",
  nameAr: "ميلان القاطع السفلي",
  value: 105,
  normal: 90,
  stdDev: 3,
  unit: "°",
  deviation: 15,
  severity: "severe",
  direction: "above",
  analysisGroup: "tweed",
  interpretationAr: "ميلان أمامي واضح",
};

describe("buildCephAssessment", () => {
  it("organizes diagnosis into skeletal, dental and soft-tissue findings", () => {
    const findings = buildCephAssessment(diagnosis, []);
    expect(findings.map(finding => finding.section)).toEqual([
      "skeletal",
      "skeletal",
      "dental",
      "soft_tissue",
    ]);
    expect(findings.find(finding => finding.id === "diagnosis-skeletal")?.isProblem).toBe(true);
    expect(findings.find(finding => finding.id === "diagnosis-incisors")?.isProblem).toBe(false);
  });

  it("turns only abnormal measurements into selectable problem findings", () => {
    const normal = { ...measurement, name: "FMA", severity: "normal" as const };
    const findings = buildCephAssessment(null, [measurement, normal]);
    expect(findings).toHaveLength(1);
    expect(findings[0]).toMatchObject({
      id: "measurement-IMPA",
      section: "dental",
      severity: "severe",
      isProblem: true,
    });
    expect(findings[0].description).toContain("ميلان أمامي واضح");
  });

  it("does not invent findings when no saved diagnosis or abnormal measurement exists", () => {
    expect(buildCephAssessment(null, [])).toEqual([]);
  });

  it.each(["UL-SLine", "LL-SLine", "Upper-Lip-ELine", "Lower-Lip-ELine", "Nasolabial"])(
    "classifies %s as a soft-tissue finding",
    name => {
      const findings = buildCephAssessment(null, [{ ...measurement, name }]);
      expect(findings[0].section).toBe("soft_tissue");
    },
  );
});
