import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/dom";
import { OrthoCephPanel } from "@/app/(dashboard)/ortho/[id]/_components/OrthoDiagnosisTab";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

const SERVER_ERROR = { isAxiosError: true, response: { status: 500 } };

/** Dispatches each mocked GET by URL so query declaration/fetch order never matters. */
function mockGetByUrl(overrides: Record<string, unknown>) {
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    for (const [needle, result] of Object.entries(overrides)) {
      if (url.includes(needle)) {
        if (result && typeof result === "object" && "isAxiosError" in result) throw result;
        return result;
      }
    }
    throw new Error(`unmocked GET ${url}`);
  });
}

// ORTHO-TASK-002: covers the SEQ-08 (#636) fix — useCaseCephAnalyses previously
// discarded isError, so a failed fetch rendered the same "no ceph yet" empty
// state as a case that genuinely has none.
describe("OrthoCephPanel", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the empty state when the case genuinely has no ceph analyses", async () => {
    mockGetByUrl({
      "ceph?orthoCaseId=": { data: [] },
      "photo-analysis?orthoCaseId=": { data: [] },
      "/diagnosis": { data: null },
    });

    renderWithQueryClient(<OrthoCephPanel caseId="case-1" />);

    await waitFor(() =>
      expect(
        screen.getByText("لا يوجد تحليل سيفالومتري لهذه الحالة بعد"),
      ).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("تعذر تحميل تحاليل السيفالو من الخادم"),
    ).not.toBeInTheDocument();
  });

  it("shows a visible Arabic error banner (not the empty state) when the ceph fetch fails", async () => {
    mockGetByUrl({
      "ceph?orthoCaseId=": SERVER_ERROR,
      "photo-analysis?orthoCaseId=": { data: [] },
      "/diagnosis": { data: null },
    });

    renderWithQueryClient(<OrthoCephPanel caseId="case-1" />);

    await waitFor(() =>
      expect(
        screen.getByText("تعذر تحميل تحاليل السيفالو من الخادم"),
      ).toBeInTheDocument(),
    );
    expect(
      screen.queryByText("لا يوجد تحليل سيفالومتري لهذه الحالة بعد"),
    ).not.toBeInTheDocument();
  });
});
