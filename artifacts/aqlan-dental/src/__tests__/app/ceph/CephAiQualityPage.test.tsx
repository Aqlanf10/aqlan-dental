import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import CephAiQualityPage from "@/app/(dashboard)/ceph/ai-quality/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({ default: { get: vi.fn() } }));

describe("CephAiQualityPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows an honest insufficient-sample state before accuracy is established", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        reviewedPointCount: 0,
        reviewedAnalysisCount: 0,
        minimumReviewedPoints: 30,
        minimumReviewedAnalyses: 3,
        sufficientSample: false,
        notice: "يلزم لكل إصدار نموذج 30 نقطة مصححة من 3 تحليلات مستقلة على الأقل قبل عرض نتيجته كمؤشر قابل للمتابعة.",
        models: [],
      },
    });

    render(<CephAiQualityPage />);

    expect(await screen.findByRole("heading", { name: "النتيجة أولية والعينة غير كافية" })).toBeInTheDocument();
    expect(screen.getByText(/لا توجد تصحيحات محفوظة بعد/)).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith("/api/ceph/ai/accuracy");
  });

  it("renders model-specific correction metrics", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        reviewedPointCount: 42,
        reviewedAnalysisCount: 3,
        minimumReviewedPoints: 30,
        minimumReviewedAnalyses: 3,
        sufficientSample: true,
        notice: "مؤشرات محلية.",
        models: [{
          modelId: "gemini/vision-test",
          reviewedPointCount: 42,
          analysisCount: 3,
          sufficientSample: true,
          meanErrorMm: 0.82,
          medianErrorMm: 0.7,
          p95ErrorMm: 1.8,
          withinOneMmPercent: 76.2,
          withinTwoMmPercent: 95.2,
        }],
      },
    });

    render(<CephAiQualityPage />);

    expect(await screen.findByText("gemini/vision-test")).toBeInTheDocument();
    expect(screen.getByText("عينة قابلة للمتابعة")).toBeInTheDocument();
    expect(screen.getByText("0.82 mm")).toBeInTheDocument();
    expect(screen.getByText("95.2%")).toBeInTheDocument();
  });
});
