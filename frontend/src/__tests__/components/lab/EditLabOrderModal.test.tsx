import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, fireEvent } from "@testing-library/dom";
import { EditLabOrderModal } from "@/components/lab/EditLabOrderModal";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";
import type { LabOrder } from "@/types/lab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));
vi.mock("@/stores/toastStore", () => ({
  toast: { error: vi.fn(), success: vi.fn(), info: vi.fn() },
}));

const LABS = [{ id: "lab-1", name: "معمل الأمل" }];

function buildOrder(overrides: Partial<LabOrder> = {}): LabOrder {
  return {
    id: "order-1",
    orderNumber: "LAB-2026-003",
    patientId: "p-1",
    patientName: "مريض تجريبي",
    patientNumber: "GM-2026-065",
    applianceType: "تاج زيركون",
    status: "draft",
    priority: "normal",
    createdAt: "2026-07-01",
    ...overrides,
  } as LabOrder;
}

// CORE-LAB-003: the production draft LAB-2026-003 could only be cancelled — there was
// no way to attach a lab or a cost, so it could never be sent and never reached the
// books. These tests pin the completion path and the guard that keeps an incomplete
// draft from being sent.
describe("EditLabOrderModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockResolvedValue({ data: { data: LABS } });
    vi.mocked(api.put).mockResolvedValue({ data: {} });
  });

  it("blocks sending while the draft has no lab and explains why", async () => {
    renderWithQueryClient(
      <EditLabOrderModal order={buildOrder({ labId: undefined, cost: undefined })} open onClose={() => {}} />,
    );

    await waitFor(() => expect(screen.getByText(/إكمال طلب المعمل/)).toBeInTheDocument());

    expect(screen.getByText(/حدد المعمل أولًا/)).toBeInTheDocument();
    const send = screen.getByRole("button", { name: /إرسال/ });
    expect(send).toBeDisabled();

    fireEvent.click(send);
    expect(api.put).not.toHaveBeenCalled();
  });

  it("still blocks sending when a lab is chosen but the cost is missing", async () => {
    renderWithQueryClient(
      <EditLabOrderModal order={buildOrder({ labId: "lab-1", cost: undefined })} open onClose={() => {}} />,
    );

    await waitFor(() => expect(screen.getByText(/أدخل تكلفة أكبر من صفر/)).toBeInTheDocument());
    expect(screen.getByRole("button", { name: /إرسال/ })).toBeDisabled();
  });

  it("requires the exchange rate before sending a non-YER cost", async () => {
    renderWithQueryClient(
      <EditLabOrderModal
        order={buildOrder({ labId: "lab-1", cost: 300, currency: "SAR", exchangeRateToYer: undefined })}
        open
        onClose={() => {}}
      />,
    );

    await waitFor(() => expect(screen.getByText(/أدخل سعر الصرف/)).toBeInTheDocument());
    expect(screen.getByRole("button", { name: /إرسال/ })).toBeDisabled();
  });

  it("saves the completed draft and then sends it, in that order", async () => {
    const onClose = vi.fn();
    renderWithQueryClient(
      <EditLabOrderModal
        order={buildOrder({ labId: "lab-1", cost: 12000, currency: "YER" })}
        open
        onClose={onClose}
      />,
    );

    await waitFor(() => expect(screen.getByRole("button", { name: /إرسال/ })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: /إرسال/ }));

    // Persist the edits first — sending against stale data would be rejected.
    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(2));
    expect(vi.mocked(api.put).mock.calls[0][0]).toBe("/api/lab-orders/order-1");
    expect(vi.mocked(api.put).mock.calls[1][0]).toBe("/api/lab-orders/order-1/status");
    expect(vi.mocked(api.put).mock.calls[1][1]).toEqual({ status: "sent" });
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it("surfaces the server's own Arabic message when the send is rejected", async () => {
    vi.mocked(api.put).mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { message: "لا يمكن إرسال الطلب قبل تحديد المعمل." } },
    });

    renderWithQueryClient(
      <EditLabOrderModal
        order={buildOrder({ labId: "lab-1", cost: 12000, currency: "YER" })}
        open
        onClose={() => {}}
      />,
    );

    await waitFor(() => expect(screen.getByRole("button", { name: /إرسال/ })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: /إرسال/ }));

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith("لا يمكن إرسال الطلب قبل تحديد المعمل."),
    );
  });

  it("shows a retry affordance instead of an empty picker when the labs list fails", async () => {
    vi.mocked(api.get).mockRejectedValue({ isAxiosError: true, response: { status: 500 } });

    renderWithQueryClient(<EditLabOrderModal order={buildOrder()} open onClose={() => {}} />);

    await waitFor(() => expect(screen.getByText(/تعذر تحميل قائمة المعامل/)).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });
});
