import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrthoSurgicalPlanningTab } from "@/app/(dashboard)/ortho/[id]/_components/OrthoSurgicalPlanningTab";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));
vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { role: "Admin" } }),
}));

// ORTHO-TASK-002: covers the SEQ-08 (#636) fix — fetchWorkspace's catch used to
// only toast and leave data=null, so a genuine request failure rendered the
// same "لم يُنشأ تخطيط جراحي بعد" create-workspace CTA as a case that
// genuinely has no surgical workspace yet. Now a distinct error state.
describe("OrthoSurgicalPlanningTab", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the create-workspace CTA when the case genuinely has none yet", async () => {
    vi.mocked(api.get).mockResolvedValueOnce({ data: { data: [] } });

    render(<OrthoSurgicalPlanningTab caseId="case-1" />);

    await waitFor(() =>
      expect(
        screen.getByText("فتح التخطيط الجراحي لهذه الحالة"),
      ).toBeInTheDocument(),
    );
  });

  it("shows a visible Arabic error banner (not the create CTA) when the list call fails", async () => {
    vi.mocked(api.get).mockRejectedValueOnce({
      response: { data: { message: "تعذر الاتصال بالخادم" } },
    });

    render(<OrthoSurgicalPlanningTab caseId="case-1" />);

    await waitFor(() =>
      expect(screen.getByText("تعذر الاتصال بالخادم")).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("فتح التخطيط الجراحي لهذه الحالة"),
    ).not.toBeInTheDocument();
  });

  it("retries via the button on error and recovers to the CTA on success", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce({ response: { data: { message: "تعذر الاتصال بالخادم" } } })
      .mockResolvedValueOnce({ data: { data: [] } });

    render(<OrthoSurgicalPlanningTab caseId="case-1" />);
    await waitFor(() =>
      expect(screen.getByText("تعذر الاتصال بالخادم")).toBeInTheDocument(),
    );

    await userEvent.click(screen.getByText("إعادة المحاولة"));

    await waitFor(() =>
      expect(
        screen.getByText("فتح التخطيط الجراحي لهذه الحالة"),
      ).toBeInTheDocument(),
    );
  });
});
