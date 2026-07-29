import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/dom";
import userEvent from "@testing-library/user-event";
import { CastAnalysisPanel } from "@/components/ortho/CastAnalysisPanel";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

// ORTHO-TASK-002: covers the SEQ-08 (#636) fix — a 500/network failure must
// render a visible Arabic error banner + retry, never the same "no analysis
// yet" empty state a genuine 404 produces.
describe("CastAnalysisPanel", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the empty state (not an error) when the server returns 404", async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      isAxiosError: true,
      response: { status: 404 },
    });

    renderWithQueryClient(<CastAnalysisPanel caseId="case-1" />);

    await waitFor(() =>
      expect(
        screen.getByText("لا يوجد Occlusogram محفوظ بعد لهذه الحالة."),
      ).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("تعذر تحميل تحليل النماذج من الخادم"),
    ).not.toBeInTheDocument();
  });

  it("shows a visible Arabic error banner (not the empty state) on a 500", async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      isAxiosError: true,
      response: { status: 500 },
    });

    renderWithQueryClient(<CastAnalysisPanel caseId="case-1" />);

    await waitFor(() =>
      expect(
        screen.getByText("تعذر تحميل تحليل النماذج من الخادم"),
      ).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("لا يوجد Occlusogram محفوظ بعد لهذه الحالة."),
    ).not.toBeInTheDocument();
  });

  it("retries the request when the retry button is clicked", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce({ isAxiosError: true, response: { status: 500 } })
      .mockResolvedValueOnce({ data: null });

    renderWithQueryClient(<CastAnalysisPanel caseId="case-1" />);
    await waitFor(() =>
      expect(
        screen.getByText("تعذر تحميل تحليل النماذج من الخادم"),
      ).toBeInTheDocument(),
    );

    await userEvent.click(screen.getByText("إعادة المحاولة"));

    await waitFor(() =>
      expect(
        screen.getByText("لا يوجد Occlusogram محفوظ بعد لهذه الحالة."),
      ).toBeInTheDocument(),
    );
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it("renders the analysis stats on success", async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      data: {
        id: "a1",
        analysisDate: "2026-01-01",
        results: { bolton: { overallRatio: 91.3 } },
      },
    });

    renderWithQueryClient(<CastAnalysisPanel caseId="case-1" />);

    await waitFor(() =>
      expect(screen.getByText("91.3%")).toBeInTheDocument(),
    );
  });
});
