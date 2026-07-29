import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import PhotoAnalysisComparePage from "@/app/(dashboard)/ceph/photo/compare/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({ default: { get: vi.fn() } }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("orthoCaseId=case-1&viewType=profile"),
}));
vi.mock("@/hooks/useClinicBranding", () => ({ resolveImageUrl: (url: string) => url }));

const records = [
  { id: "before", orthoCaseId: "case-1", viewType: "profile", imageFileUrl: "/before.jpg", analysisDate: "2026-01-01" },
  { id: "after", orthoCaseId: "case-1", viewType: "profile", imageFileUrl: "/after.jpg", analysisDate: "2026-06-01" },
];

describe("PhotoAnalysisComparePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.includes("?orthoCaseId=")) return Promise.resolve({ data: records }) as never;
      const record = url.endsWith("before") ? records[0] : records[1];
      const value = url.endsWith("before") ? 10 : 12.5;
      return Promise.resolve({ data: { ...record, measurementsJson: JSON.stringify([{ key: "FacialConvexity", nameAr: "تحدب الوجه", value }]) } }) as never;
    });
  });

  it("renders a saved-record image comparison and honest measurement delta", async () => {
    render(<PhotoAnalysisComparePage />);

    expect(await screen.findByRole("heading", { name: "فرق القياسات المحفوظة" })).toBeInTheDocument();
    expect(screen.getByText("تحدب الوجه")).toBeInTheDocument();
    expect(screen.getByText("+2.5")).toBeInTheDocument();
    expect(screen.getByRole("slider", { name: "موضع فاصل المقارنة قبل وبعد" })).toBeInTheDocument();
    expect(screen.getByText(/لا تنشئ إطارات وسيطة/)).toBeInTheDocument();
  });
});
