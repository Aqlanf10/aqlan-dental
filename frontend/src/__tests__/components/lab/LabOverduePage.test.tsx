import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/dom";
// CORE-LAB-017: /lab/overdue is a redirect stub now — the screen itself is a pivot inside
// the lab workspace, so the test points at the panel that actually renders.
import { LabOverduePanel as LabOverduePage } from "@/app/(dashboard)/lab/_panels/OverduePanel";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));

const SERVER_ERROR = { isAxiosError: true, response: { status: 500 } };

// SEQ-11 (#646): during live clinic use, a failed fetch on the overdue lab
// orders screen must never read as "لا توجد طلبات متأخرة" — that hides a
// backlog exactly when the owner is checking for one.
describe("LabOverduePage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders the genuine empty state when the API confirms zero overdue orders", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { data: [], count: 0 } });
    renderWithQueryClient(<LabOverduePage />);
    await waitFor(() =>
      expect(screen.getByText("لا توجد طلبات متأخرة")).toBeInTheDocument(),
    );
    expect(screen.queryByText(/تعذر تحميل الطلبات المتأخرة/)).not.toBeInTheDocument();
  });

  it("shows an error banner with retry — not the empty state — when the fetch fails", async () => {
    vi.mocked(api.get).mockRejectedValue(SERVER_ERROR);
    renderWithQueryClient(<LabOverduePage />);
    await waitFor(() =>
      expect(screen.getByText(/تعذر تحميل الطلبات المتأخرة/)).toBeInTheDocument(),
    );
    expect(screen.queryByText("لا توجد طلبات متأخرة")).not.toBeInTheDocument();
    expect(screen.getByText("إعادة المحاولة")).toBeInTheDocument();
  });
});
