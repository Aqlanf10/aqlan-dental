import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CephPage from "@/app/(dashboard)/ceph/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const base = {
  orthoCaseId: "case-1",
  caseNumber: "ORT-1",
  patientName: "مريض تجريبي",
  analysisType: "steiner" as const,
  analysisDate: "2026-07-13",
  xrayFileUrl: "/uploads/xray.jpg",
  aiAssisted: false,
  landmarkCount: 24,
  hasMeasurements: true,
  createdAt: "2026-07-13T10:00:00Z",
};

describe("CephPage workflow status", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockResolvedValue({
      data: [
        { ...base, id: "pending", isApproved: false },
        { ...base, id: "approved", patientName: "مريض معتمد", isApproved: true },
      ],
    });
  });

  it("distinguishes saved measurements from final doctor approval", async () => {
    render(<CephPage />);

    await waitFor(() => expect(screen.getByText("بانتظار اعتماد الطبيب")).toBeInTheDocument());
    expect(screen.getByText("مكتمل ومعتمد")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "مراجعة واعتماد" })).toHaveAttribute("href", "/ceph/pending");
    expect(screen.getByRole("link", { name: "فتح التحليل" })).toHaveAttribute("href", "/ceph/approved");
    expect(screen.getByText("لا تعني اعتماد التقرير")).toBeInTheDocument();
    expect(screen.queryByText("جاهزة للتقرير و VTO")).not.toBeInTheDocument();
  });

  it("counts only approved complete analyses as complete", async () => {
    render(<CephPage />);

    await waitFor(() => expect(screen.getByText("مكتملة ومعتمدة")).toBeInTheDocument());
    const approvedSummary = screen.getByText("مكتملة ومعتمدة").closest("div.rounded-xl");
    expect(approvedSummary).toHaveTextContent("1");
    const followUpSummary = screen.getByText("تحتاج متابعة").closest("div.rounded-xl");
    expect(followUpSummary).toHaveTextContent("1");
  });
});
