import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import TreatmentPackagesPage from "@/app/(dashboard)/settings/packages/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), put: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

const PACKAGE = {
  id: "package-1",
  name: "باقة تقويم",
  description: null,
  totalPrice: 250000,
  sessionCount: 12,
  color: "#2563eb",
  isActive: true,
  createdAt: "2026-07-11T00:00:00Z",
  updatedAt: "2026-07-11T00:00:00Z",
};

describe("SEQ-24 treatment package fetch states", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows an error with retry instead of a false empty catalog", async () => {
    vi.mocked(api.get).mockRejectedValue(new Error("Network Error"));

    render(<TreatmentPackagesPage />);

    expect(await screen.findByText("تعذر تحميل الباقات")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
    expect(screen.queryByText("لا توجد باقات")).not.toBeInTheDocument();
    expect(screen.queryByText(/أضف أول باقة/)).not.toBeInTheDocument();
  });

  it("retries the failed request", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce(new Error("Network Error"))
      .mockResolvedValueOnce({ data: [PACKAGE] });

    render(<TreatmentPackagesPage />);
    fireEvent.click(await screen.findByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("باقة تقويم")).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
    expect(screen.queryByText("تعذر تحميل الباقات")).not.toBeInTheDocument();
  });

  it("keeps loaded packages visible when a refresh fails", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce({ data: [PACKAGE] })
      .mockRejectedValueOnce({ response: { status: 503 } });

    render(<TreatmentPackagesPage />);
    expect(await screen.findByText("باقة تقويم")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("checkbox", { name: "إظهار المعطّلة" }));

    expect(await screen.findByText("تعذر تحميل الباقات")).toBeInTheDocument();
    expect(screen.getByText("باقة تقويم")).toBeInTheDocument();
    expect(screen.queryByText("لا توجد باقات")).not.toBeInTheDocument();
  });
});
