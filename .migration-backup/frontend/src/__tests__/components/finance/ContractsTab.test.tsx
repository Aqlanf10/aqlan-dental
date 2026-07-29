import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ContractsTab } from "@/app/(dashboard)/finance-v3/components/ContractsTab";
import { api } from "@/lib/api";

vi.mock("@/lib/api", () => ({
  api: { get: vi.fn(), post: vi.fn() },
}));

vi.mock("@/stores/toastStore", () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

const CONTRACT = {
  id: "contract-1",
  contractNumber: "CTR-001",
  patientId: "patient-1",
  patientName: "أحمد محمد",
  patientNumber: "P-001",
  totalAmount: 100000,
  paidAmount: 25000,
  outstandingAmount: 75000,
  status: "Active",
  startDate: "2026-07-11",
  isOverdue: false,
};

describe("SEQ-22 finance contracts fetch states", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows an error with retry instead of a false empty state", async () => {
    vi.mocked(api.get).mockRejectedValue({ response: { data: { message: "تعذر تحميل العقود الآن" } } });

    render(<ContractsTab />);

    expect(await screen.findByText("تعذر تحميل العقود الآن")).toBeInTheDocument();
    expect(screen.queryByText("لا توجد عقود")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("retries the failed request", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValueOnce({ data: { data: [CONTRACT], total: 1 } });

    render(<ContractsTab />);
    fireEvent.click(await screen.findByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("CTR-001")).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
    expect(screen.queryByText("فشل في تحميل العقود")).not.toBeInTheDocument();
  });

  it("keeps previously loaded contracts visible after a refresh failure", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce({ data: { data: [CONTRACT], total: 1 } })
      .mockRejectedValueOnce({ response: { status: 503 } });

    render(<ContractsTab />);
    expect(await screen.findByText("CTR-001")).toBeInTheDocument();

    fireEvent.click(screen.getByTitle("تحديث"));

    expect(await screen.findByText("فشل في تحميل العقود")).toBeInTheDocument();
    expect(screen.getByText("CTR-001")).toBeInTheDocument();
    expect(screen.queryByText("لا توجد عقود")).not.toBeInTheDocument();
  });
});
