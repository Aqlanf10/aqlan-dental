import { describe, expect, it, vi } from "vitest";
import {
  buildCephMeasurementCsv,
  downloadCephMeasurementCsv,
} from "@/lib/cephMeasurementCsv";
import type { CephMeasurement } from "@/types/ceph";

const measurement: CephMeasurement = {
  name: "SNA",
  nameAr: "زاوية SNA",
  value: 84.5,
  normal: 82,
  stdDev: 2,
  unit: "°",
  deviation: 2.5,
  severity: "mild",
  direction: "above",
  analysisGroup: "steiner",
  interpretationAr: "تقدّم الفك العلوي",
};

describe("ceph measurement CSV", () => {
  it("builds an Arabic Excel-compatible table from saved measurements", () => {
    const csv = buildCephMeasurementCsv({
      analysisId: "analysis-1",
      patientName: "مريض تجريبي",
      caseNumber: "ORT-42",
      analysisDate: "2026-07-13",
      measurements: [measurement],
    });

    expect(csv.startsWith("\ufeff")).toBe(true);
    expect(csv).toContain('"المريض","مريض تجريبي"');
    expect(csv).toContain('"ستاينر","زاوية SNA","SNA","84.5","°","82","2","2.5"');
    expect(csv).toContain('"انحراف خفيف","أعلى من المعيار","تقدّم الفك العلوي"');
  });

  it("escapes quotes and line breaks and neutralizes spreadsheet formulas", () => {
    const csv = buildCephMeasurementCsv({
      analysisId: "analysis-2",
      patientName: '=HYPERLINK("https://invalid.test","x")',
      analysisDate: "2026-07-13",
      measurements: [{
        ...measurement,
        nameAr: 'قياس، "خاص"',
        value: null,
        deviation: null,
        apiInterpretation: "سطر أول\nسطر ثان",
      }],
    });

    expect(csv).toContain('"\'=HYPERLINK(""https://invalid.test"",""x"")"');
    expect(csv).toContain('"قياس، ""خاص"""');
    expect(csv).toContain('"SNA","","°"');
    expect(csv).toContain('"سطر أول\nسطر ثان"');
  });

  it("downloads the generated CSV with a deterministic filename", () => {
    const createObjectURL = vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:ceph");
    const revokeObjectURL = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => undefined);
    const click = vi.fn();
    const originalCreateElement = document.createElement.bind(document);
    vi.spyOn(document, "createElement").mockImplementation(((tagName: string) => {
      if (tagName === "a") return { click } as unknown as HTMLAnchorElement;
      return originalCreateElement(tagName);
    }) as typeof document.createElement);

    downloadCephMeasurementCsv({
      analysisId: "analysis-3",
      patientName: "مريض",
      analysisDate: "2026-07-13",
      measurements: [measurement],
    });

    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:ceph");
  });
});
