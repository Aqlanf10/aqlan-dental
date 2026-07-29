import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { StrictMode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DocumentTemplatesPage from "@/app/(dashboard)/settings/templates/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), put: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

const TEMPLATE = {
  id: "template-1",
  title: "موافقة علاج",
  category: "consent",
  description: null,
  content: "نص الموافقة",
  requiresSignature: true,
  isActive: true,
  updatedAt: "2026-07-11T00:00:00Z",
};

describe("SEQ-25 document template fetch states", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows an error with retry instead of a false empty list", async () => {
    vi.mocked(api.get).mockRejectedValue(new Error("Network Error"));
    render(<DocumentTemplatesPage />);

    expect(await screen.findByText("تعذر تحميل قوالب الوثائق")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
    expect(screen.queryByText("لا توجد قوالب بعد")).not.toBeInTheDocument();
  });

  it("retries the failed request", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce(new Error("Network Error"))
      .mockResolvedValueOnce({ data: [TEMPLATE] });
    render(<DocumentTemplatesPage />);

    fireEvent.click(await screen.findByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("موافقة علاج")).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });

  it("keeps loaded templates visible after a failed refresh", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce({ data: [TEMPLATE] })
      .mockRejectedValueOnce({ response: { status: 503 } });
    render(
      <StrictMode>
        <DocumentTemplatesPage />
      </StrictMode>,
    );

    expect(await screen.findByText("تعذر تحميل قوالب الوثائق")).toBeInTheDocument();
    expect(screen.getByText("موافقة علاج")).toBeInTheDocument();
    expect(screen.queryByText("لا توجد قوالب بعد")).not.toBeInTheDocument();
  });
});
