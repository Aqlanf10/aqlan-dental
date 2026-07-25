import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { FinanceTab } from "@/components/patient/tabs/FinanceTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const payment = (index: number) => ({
  id: `payment-${index}`,
  patientId: "patient-1",
  patientName: "مريض تجريبي",
  amount: index * 1000,
  paymentDate: `2026-01-${String(Math.min(index, 28)).padStart(2, "0")}`,
  serviceDescription: `دفعة رقم ${index}`,
});

describe("FinanceTab complete patient payment history", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    const payments = Array.from({ length: 25 }, (_, index) => payment(index + 1));
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.includes("account-statement")) {
        return Promise.resolve({
          data: {
            patientId: "patient-1",
            patientName: "مريض تجريبي",
            patientNumber: "P-001",
            totalContracted: 0,
            totalDiscounts: 0,
            totalPaid: 325000,
            totalRemaining: 0,
            activeContracts: 0,
            completedContracts: 0,
            contracts: [],
            totalPaymentsCount: 25,
            payments,
            recentPayments: payments.slice(0, 20),
          },
        });
      }
      return Promise.resolve({ data: [] });
    });
  });

  it("renders payments beyond the legacy twenty-row window", async () => {
    render(<FinanceTab patientId="patient-1" />);

    expect(await screen.findByText("سجل المدفوعات (25)")).toBeInTheDocument();
    expect(screen.getByText("دفعة رقم 25")).toBeInTheDocument();
    expect(screen.getAllByText(/دفعة رقم/)).toHaveLength(25);
  });
});
