import { describe, expect, it } from "vitest";
import { render } from "@testing-library/react";
import { screen } from "@testing-library/dom";
import { CephReadinessBadge } from "@/components/ceph/CephReadinessBadge";
import { cephReadinessFromAnalysis, extractCephUserNotes } from "@/lib/cephReadiness";
import type { CephAnalysis } from "@/types/ceph";

function analysis(overrides: Partial<CephAnalysis> = {}): CephAnalysis {
  return {
    id: "ceph-1",
    orthoCaseId: "case-1",
    patientId: "patient-1",
    patientName: "مريض تجريبي",
    analysisType: "steiner",
    analysisDate: "2026-07-11",
    xrayFileUrl: "/uploads/ceph.jpg",
    aiAssisted: true,
    isAutoTraced: true,
    doctorId: "doctor-1",
    notes: "مراجعة تموضع النقطة A قبل الاعتماد النهائي",
    pixelsPerMm: 10,
    landmarks: Array.from({ length: 24 }, (_, index) => ({
      key: `L${index}`,
      name: `L${index}`,
      nameAr: `L${index}`,
      x: index,
      y: index,
      isAiPlaced: false,
    })),
    measurements: [{
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
    }],
    isApproved: false,
    ...overrides,
  };
}

describe("CephReadinessBadge detail metadata", () => {
  it("carries backend detail fields and shows persisted notes in the analysis bar", () => {
    const readiness = cephReadinessFromAnalysis(analysis(), false);

    expect(readiness.analysisMetadata).toEqual({
      notes: "مراجعة تموضع النقطة A قبل الاعتماد النهائي",
      isAutoTraced: true,
      doctorId: "doctor-1",
    });

    render(<CephReadinessBadge readiness={readiness} />);

    expect(screen.getByTestId("ceph-analysis-notes")).toHaveTextContent(
      "مراجعة تموضع النقطة A قبل الاعتماد النهائي",
    );
    expect(screen.getByText(/تتبع آلي — يتطلب مراجعة الطبيب/)).toBeInTheDocument();
  });

  it("unwraps UserNotes from persisted calibration JSON instead of showing raw JSON", () => {
    const wrapped = JSON.stringify({
      PixelsPerMm: 11.5,
      ImageWidth: 1920,
      ImageHeight: 1080,
      UserNotes: "ملاحظة سريرية محفوظة داخل غلاف المعايرة",
    });

    expect(extractCephUserNotes(wrapped)).toBe(
      "ملاحظة سريرية محفوظة داخل غلاف المعايرة",
    );

    const readiness = cephReadinessFromAnalysis(analysis({ notes: wrapped }), false);
    render(<CephReadinessBadge readiness={readiness} />);

    const note = screen.getByTestId("ceph-analysis-notes");
    expect(note).toHaveTextContent("ملاحظة سريرية محفوظة داخل غلاف المعايرة");
    expect(note).not.toHaveTextContent("PixelsPerMm");
  });

  it("does not render an empty notes card for null, whitespace, or metadata-only JSON", () => {
    expect(extractCephUserNotes("   ")).toBeNull();
    expect(extractCephUserNotes('{"PixelsPerMm":10,"UserNotes":null}')).toBeNull();

    const readiness = cephReadinessFromAnalysis(
      analysis({ notes: "   ", isAutoTraced: false, doctorId: null }),
      false,
    );

    render(<CephReadinessBadge readiness={readiness} />);

    expect(screen.queryByTestId("ceph-analysis-notes")).not.toBeInTheDocument();
    expect(screen.queryByText(/تتبع آلي/)).not.toBeInTheDocument();
  });
});
