import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { FinanceTab } from "@/components/patient/tabs/FinanceTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const statement = {
  patientId: "patient-1",
  patientName: "مريض تجريبي",
  patientNumber: "GM-2026-001",
  totalContracted: 100000,
  totalDiscounts: 5000,
  totalPaid: 25000,
  totalRemaining: 70000,
  activeContracts: 1,
  completedContracts: 0,
  contracts: [],
  totalPaymentsCount: 0,
  payments: [],
  recentPayments: [],
};

const invoice = {
  id: "invoice-1",
  invoiceNumber: "INV-001",
  status: "Issued",
  totalAmount: 10000,
  createdAt: "2026-07-25T00:00:00Z",
  updatedAt: "2026-07-25T00:00:00Z",
};

describe("FinanceTab retry reliability", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("retries a failed account statement without showing a false empty state", async () => {
    let recovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).endsWith("/account-statement")) {
        return recovered
          ? Promise.resolve({ data: statement })
          : Promise.reject({ response: { status: 503, data: { message: "كشف الحساب غير متاح" } } });
      }
      return Promise.resolve({ data: [] });
    });

    render(<FinanceTab patientId="patient-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("كشف الحساب غير متاح");
    expect(screen.queryByText("لا توجد بيانات مالية")).not.toBeInTheDocument();

    recovered = true;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("إجمالي العقود")).toBeInTheDocument();
    expect(screen.queryByText("كشف الحساب غير متاح")).not.toBeInTheDocument();
  });

  it("keeps the statement visible when only invoices fail and retries them", async () => {
    let invoicesRecovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).endsWith("/account-statement")) {
        return Promise.resolve({ data: statement });
      }
      return invoicesRecovered
        ? Promise.resolve({ data: [invoice] })
        : Promise.reject({ response: { status: 500, data: { message: "تعذر تحميل الفواتير الآن" } } });
    });

    render(<FinanceTab patientId="patient-1" />);

    expect(await screen.findByText("إجمالي العقود")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("تعذر تحميل الفواتير الآن");
    expect(screen.queryByText("لا توجد فواتير مسجّلة")).not.toBeInTheDocument();

    invoicesRecovered = true;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("INV-001")).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByText("تعذر تحميل الفواتير الآن")).not.toBeInTheDocument();
    });
  });

  it("clears a previous error when refreshKey triggers a successful reload", async () => {
    let recovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).endsWith("/account-statement")) {
        return recovered
          ? Promise.resolve({ data: statement })
          : Promise.reject({ response: { status: 500, data: { message: "فشل أولي" } } });
      }
      return Promise.resolve({ data: [] });
    });

    const { rerender } = render(<FinanceTab patientId="patient-1" refreshKey={0} />);
    expect(await screen.findByRole("alert")).toHaveTextContent("فشل أولي");

    recovered = true;
    rerender(<FinanceTab patientId="patient-1" refreshKey={1} />);

    expect(await screen.findByText("إجمالي العقود")).toBeInTheDocument();
    expect(screen.queryByText("فشل أولي")).not.toBeInTheDocument();
  });
});
