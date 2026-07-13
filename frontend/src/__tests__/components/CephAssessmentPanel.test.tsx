import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { CephAssessmentPanel } from "@/components/ceph/CephAssessmentPanel";
import type { CephDiagnosis } from "@/types/ceph";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

const approvedDiagnosis: CephDiagnosis = {
  skeletalClass: "Class II",
  doctorApproved: true,
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.get).mockResolvedValue({ data: [] });
  vi.mocked(api.post).mockResolvedValue({
    data: { added: 1, skippedExisting: 0, items: [{ description: "علاقة هيكلية: الصنف الثاني" }] },
  });
});

describe("CephAssessmentPanel", () => {
  it("posts only findings explicitly selected by the doctor", async () => {
    render(
      <CephAssessmentPanel
        analysisId="analysis-1"
        orthoCaseId="case-1"
        analysisApproved
        hasUnsavedChanges={false}
        diagnosis={approvedDiagnosis}
        measurements={[]}
      />,
    );

    const transfer = screen.getByRole("button", { name: "إضافة المحدد (0)" });
    expect(transfer).toBeDisabled();
    const checkbox = await screen.findByRole("checkbox", { name: "إضافة العلاقة الهيكلية" });
    await waitFor(() => expect(checkbox).toBeEnabled());
    fireEvent.click(checkbox);
    fireEvent.click(screen.getByRole("button", { name: "إضافة المحدد (1)" }));

    await waitFor(() => expect(api.post).toHaveBeenCalledOnce());
    expect(api.post).toHaveBeenCalledWith("/api/ceph/analysis-1/assessment/problem-list", {
      items: [{
        category: "skeletal",
        description: "علاقة هيكلية: الصنف الثاني",
        severity: "moderate",
      }],
    });
  });

  it("keeps handoff controls disabled until both approval gates pass", async () => {
    render(
      <CephAssessmentPanel
        analysisId="analysis-1"
        orthoCaseId="case-1"
        analysisApproved={false}
        hasUnsavedChanges={false}
        diagnosis={approvedDiagnosis}
        measurements={[]}
      />,
    );

    expect(screen.getByText("اعتماد التحليل مطلوب قبل التسليم.")).toBeInTheDocument();
    const checkbox = await screen.findByRole("checkbox", { name: "إضافة العلاقة الهيكلية" });
    expect(checkbox).toBeDisabled();
    fireEvent.click(checkbox);
    expect(api.post).not.toHaveBeenCalled();
  });

  it("marks a matching existing problem and prevents duplicate selection", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: [{ description: "علاقة هيكلية: الصنف الثاني" }],
    });
    render(
      <CephAssessmentPanel
        analysisId="analysis-1"
        orthoCaseId="case-1"
        analysisApproved
        hasUnsavedChanges={false}
        diagnosis={approvedDiagnosis}
        measurements={[]}
      />,
    );

    expect(await screen.findByText("موجودة في قائمة المشكلات")).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "إضافة العلاقة الهيكلية" })).toBeDisabled();
  });

  it("blocks handoff while the canvas has unsaved landmark or calibration changes", async () => {
    render(
      <CephAssessmentPanel
        analysisId="analysis-1"
        orthoCaseId="case-1"
        analysisApproved
        hasUnsavedChanges
        diagnosis={approvedDiagnosis}
        measurements={[]}
      />,
    );

    expect(screen.getByText("احفظ تغييرات المعالم والمعايرة قبل التسليم.")).toBeInTheDocument();
    const checkbox = await screen.findByRole("checkbox", { name: "إضافة العلاقة الهيكلية" });
    expect(checkbox).toBeDisabled();
  });
});
