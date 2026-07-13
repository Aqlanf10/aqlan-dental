import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ModelAnalysisPage from "@/app/(dashboard)/ortho/[id]/model-analysis/page";
import api from "@/lib/api";

vi.mock("next/navigation", () => ({ useParams: () => ({ id: "case-1" }) }));
vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
  },
}));

const emptyResult = {
  bolton: null,
  upperArch: null,
  lowerArch: null,
  pont: null,
  howe: null,
  moyers: null,
  tanakaJohnston: null,
  huckaba: [],
  warnings: [],
};

describe("OcclusogramPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockImplementation(async (url: string) => {
      if (url.endsWith("/model-analyses")) return { data: [] } as never;
      if (url.endsWith("/photos")) return { data: [] } as never;
      return { data: { caseNumber: "ORTHO-1", patientName: "مريض تجريبي" } } as never;
    });
    vi.mocked(api.post).mockResolvedValue({ data: emptyResult } as never);
  });

  it("presents the three occlusogram modes over the canonical model-analysis owner", async () => {
    render(<ModelAnalysisPage />);

    expect(await screen.findByRole("heading", { name: "Occlusogram وتحليل النماذج" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "حجم الأسنان" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "عرض وطول القوس" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "عدم الانتظام" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "عدم الانتظام" }));
    expect(screen.getByRole("heading", { name: "تحليل عدم الانتظام والمسافة" })).toBeInTheDocument();
  });

  it("calculates through the existing canonical preview endpoint", async () => {
    render(<ModelAnalysisPage />);
    await screen.findByRole("heading", { name: "Occlusogram وتحليل النماذج" });

    await userEvent.click(screen.getByRole("button", { name: "احسب" }));

    await waitFor(() => expect(api.post).toHaveBeenCalledWith(
      "/api/ortho-cases/case-1/model-analyses/preview",
      expect.objectContaining({ toothWidths: expect.any(Object), moyersPercentile: 75 }),
    ));
  });
});
