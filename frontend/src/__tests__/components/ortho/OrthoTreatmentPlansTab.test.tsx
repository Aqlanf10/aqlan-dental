import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/dom";
import userEvent from "@testing-library/user-event";
import { OrthoTreatmentPlansTab } from "@/app/(dashboard)/ortho/[id]/_components/OrthoTreatmentPlansTab";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

// ORTHO-TASK-002: covers the SEQ-08 (#636) fix — useTreatmentPlans previously
// discarded isError, so a failed fetch rendered the same "no plans yet" empty
// state as a case that genuinely has none.
describe("OrthoTreatmentPlansTab", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the empty state when the case genuinely has no plans", async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: [] });

    renderWithQueryClient(<OrthoTreatmentPlansTab caseId="case-1" />);

    await waitFor(() =>
      expect(screen.getByText("لا توجد خطط علاج مسجلة بعد.")).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("تعذر تحميل خطط العلاج من الخادم"),
    ).not.toBeInTheDocument();
  });

  it("shows a visible Arabic error banner (not the empty state) when the fetch fails", async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      isAxiosError: true,
      response: { status: 500 },
    });

    renderWithQueryClient(<OrthoTreatmentPlansTab caseId="case-1" />);

    await waitFor(() =>
      expect(screen.getByText("تعذر تحميل خطط العلاج من الخادم")).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("لا توجد خطط علاج مسجلة بعد."),
    ).not.toBeInTheDocument();
  });

  it("retries via the button and clears the error once the retry succeeds", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce({ isAxiosError: true, response: { status: 500 } })
      .mockResolvedValueOnce({ data: [] });

    renderWithQueryClient(<OrthoTreatmentPlansTab caseId="case-1" />);
    await waitFor(() =>
      expect(screen.getByText("تعذر تحميل خطط العلاج من الخادم")).toBeInTheDocument(),
    );

    await userEvent.click(screen.getByText("إعادة المحاولة"));

    await waitFor(() =>
      expect(screen.getByText("لا توجد خطط علاج مسجلة بعد.")).toBeInTheDocument(),
    );
  });
});
