import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { PaymentsTab } from "@/components/patient/tabs/PaymentsTab";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("@/hooks/useDoctors", () => ({
  useDoctors: () => ({ data: [] }),
}));

vi.mock("@/hooks/useCashierSession", () => ({
  useActiveCashierSession: () => ({ data: null }),
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { id: "admin-1", role: "Admin" } }),
}));

vi.mock("@/stores/toastStore", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

describe("PaymentsTab honest loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("does not render a trusted zero total when the payments request fails", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.startsWith("/api/payments")) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "خدمة المدفوعات غير متاحة حالياً" } },
        });
      }
      return Promise.resolve({ data: [] });
    });

    render(<PaymentsTab patientId="patient-1" />);

    expect(await screen.findByText("خدمة المدفوعات غير متاحة حالياً")).toBeInTheDocument();
    expect(screen.queryByText("إجمالي المدفوعات")).not.toBeInTheDocument();
    expect(screen.queryByText("لا توجد مدفوعات")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("keeps zero as a valid value only after a successful empty response", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });

    render(<PaymentsTab patientId="patient-1" />);

    expect(await screen.findByText("لا توجد مدفوعات")).toBeInTheDocument();
    expect(screen.getByText("إجمالي المدفوعات")).toBeInTheDocument();
    expect(screen.getByText("0 ر.ي")).toBeInTheDocument();
  });
});
