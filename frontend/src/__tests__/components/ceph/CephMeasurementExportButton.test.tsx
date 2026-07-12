import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CephMeasurementExportButton } from "@/components/ceph/CephMeasurementExportButton";
import { downloadCephMeasurementCsv } from "@/lib/cephMeasurementCsv";
import type { CephAnalysis, CephMeasurement } from "@/types/ceph";

vi.mock("@/lib/cephMeasurementCsv", () => ({
  downloadCephMeasurementCsv: vi.fn(),
}));

const measurement: CephMeasurement = {
  name: "SNA",
  nameAr: "زاوية SNA",
  value: 82,
  normal: 82,
  stdDev: 2,
  unit: "°",
  deviation: 0,
  severity: "normal",
  direction: "within",
  analysisGroup: "steiner",
};

const analysis = {
  id: "analysis-1",
  patientName: "مريض تجريبي",
  caseNumber: "ORT-1",
  analysisDate: "2026-07-13",
  isApproved: true,
  measurements: [measurement],
} as CephAnalysis;

describe("CephMeasurementExportButton", () => {
  beforeEach(() => vi.clearAllMocks());

  it.each([
    ["تحليل غير معتمد", { isApproved: false }, 24, false],
    ["تعديلات غير محفوظة", {}, 24, true],
    ["لا توجد معالم", {}, 0, false],
    ["لا توجد قياسات محفوظة", { measurements: [] }, 24, false],
  ])("يعطل التصدير عند %s", (_label, override, placedCount, hasUnsavedEdits) => {
    render(
      <CephMeasurementExportButton
        analysis={{ ...analysis, ...override }}
        placedCount={placedCount}
        hasUnsavedEdits={hasUnsavedEdits}
      />,
    );

    const button = screen.getByRole("button", { name: "تصدير القياسات CSV" });
    expect(button).toBeDisabled();
    fireEvent.click(button);
    expect(downloadCephMeasurementCsv).not.toHaveBeenCalled();
  });

  it("exports the saved snapshot after approval", () => {
    render(
      <CephMeasurementExportButton
        analysis={analysis}
        placedCount={24}
        hasUnsavedEdits={false}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "تصدير القياسات CSV" }));

    expect(downloadCephMeasurementCsv).toHaveBeenCalledWith({
      analysisId: "analysis-1",
      patientName: "مريض تجريبي",
      caseNumber: "ORT-1",
      analysisDate: "2026-07-13",
      measurements: [measurement],
    });
  });
});
