import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { PortalAccessTab } from "@/components/patient/tabs/PortalAccessTab";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
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

const NO_ACCOUNT = {
  username: "GM0001",
  accountActive: false,
  mustChangePassword: false,
  hasPortalAccount: false,
};

describe("PortalAccessTab honest credential loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a retryable Arabic error instead of claiming the patient has no account", async () => {
    vi.mocked(api.get).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 503,
        data: { message: "خدمة بوابة المرضى غير متاحة حالياً" },
      },
    });

    render(<PortalAccessTab patientId="patient-1" patientNumber="GM0001" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة بوابة المرضى غير متاحة حالياً");
    expect(screen.getByText(/لم نعتبر فشل الطلب/)).toBeInTheDocument();
    expect(screen.queryByText("لا يوجد حساب بوابة لهذا المريض")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "إنشاء حساب بوابة" })).not.toBeInTheDocument();
  });

  it("retries the request and then renders the genuine no-account state", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce({
        isAxiosError: true,
        response: { status: 503, data: { message: "انقطاع مؤقت" } },
      })
      .mockResolvedValueOnce({ data: NO_ACCOUNT });

    render(<PortalAccessTab patientId="patient-1" patientNumber="GM0001" />);

    fireEvent.click(await screen.findByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("لا يوجد حساب بوابة لهذا المريض")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إنشاء حساب بوابة" })).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });

  it("preserves the genuine no-account response without showing an error", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: NO_ACCOUNT });

    render(<PortalAccessTab patientId="patient-1" patientNumber="GM0001" />);

    expect(await screen.findByText("لا يوجد حساب بوابة لهذا المريض")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
