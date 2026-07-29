import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/dom";
import { CasePresentationPanel } from "@/components/ortho/CasePresentationPanel";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

const OK_OVERVIEW = { data: { isDiagnosisApproved: false, visitsCount: 0 } };
const OK_PHOTOS = { data: [] };
const OK_DEFINITION = { data: { slides: [] } };
const OK_CHECKLIST = { data: {} };
const NOT_FOUND = { isAxiosError: true, response: { status: 404 } };
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

// ORTHO-TASK-002: covers the SEQ-08 (#636) fix — any of the 5 underlying
// queries failing with a real error must show a visible Arabic banner +
// retry-all button, never silently render the same "not ready" checklist
// a genuinely-incomplete case would show.
describe("CasePresentationPanel", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows no error banner when every query succeeds (model-analysis 404 = genuinely none yet)", async () => {
    mockGetByUrl({
      "/overview": OK_OVERVIEW,
      "/photos": OK_PHOTOS,
      "/case-presentation/definition": OK_DEFINITION,
      "/checklist": OK_CHECKLIST,
      "/model-analyses/latest": NOT_FOUND,
    });

    renderWithQueryClient(<CasePresentationPanel caseId="case-1" />);

    // Codex P2: the heading renders synchronously, so wait for the queries to
    // actually settle (all 5 requests fired + the loading spinner replaced by
    // the readiness list) before asserting the error banner is absent —
    // otherwise this negative assertion can pass before a regression surfaces.
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(5));
    await waitFor(() =>
      expect(screen.getByText("تحليل النماذج / Bolton")).toBeInTheDocument(),
    );
    expect(
      screen.queryByText(/تعذر تحميل بعض بيانات الجاهزية/),
    ).not.toBeInTheDocument();
  });

  it("shows a visible Arabic error banner when a query genuinely fails (500)", async () => {
    mockGetByUrl({
      "/overview": OK_OVERVIEW,
      "/photos": OK_PHOTOS,
      "/case-presentation/definition": OK_DEFINITION,
      "/checklist": SERVER_ERROR,
      "/model-analyses/latest": NOT_FOUND,
    });

    renderWithQueryClient(<CasePresentationPanel caseId="case-1" />);

    await waitFor(() =>
      expect(
        screen.getByText(/تعذر تحميل بعض بيانات الجاهزية من الخادم/),
      ).toBeInTheDocument(),
    );
  });
});
