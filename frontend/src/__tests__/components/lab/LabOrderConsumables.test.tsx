import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent, waitFor } from "@testing-library/dom";
import { LabOrderConsumables } from "@/components/lab/LabOrderConsumables";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

const role = vi.hoisted(() => ({ current: "Admin" }));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { id: "u-1", role: role.current } }),
}));

vi.mock("@/stores/toastStore", () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

/**
 * LABINV-REQ-011.
 *
 * The behaviours defended here are the two that would cost the clinic something real: that
 * the material cost is never presented as part of what is owed to the lab, and that a
 * request which would drive stock negative cannot be submitted.
 */
describe("LabOrderConsumables", () => {
  const CONSUMED = {
    labOrderId: "order-1",
    orderNumber: "LAB-2026-011",
    lines: [
      {
        id: "adj-1",
        inventoryItemId: "item-1",
        itemName: "Zirconia block",
        unit: "block",
        consumedQuantity: 3,
        costPerUnit: 250,
        reason: "استهلاك أمر مختبر: LAB-2026-011",
        createdAt: "2026-08-20 10:00",
      },
    ],
    materialCost: 750,
    currency: "YER",
    unpricedLineCount: 0,
  };

  const INVENTORY = {
    data: [
      { id: "item-1", name: "Zirconia block", quantity: 10, unit: "block", costPerUnit: 250 },
      { id: "item-2", name: "Porcelain", quantity: 2, unit: "g", costPerUnit: 100 },
    ],
  };

  const wire = (consumed: unknown = CONSUMED) => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.includes("/consumables")) return Promise.resolve({ data: consumed });
      return Promise.resolve({ data: INVENTORY });
    });
  };

  beforeEach(() => {
    vi.clearAllMocks();
    role.current = "Admin";
    wire();
  });

  const render = () =>
    renderWithQueryClient(
      <LabOrderConsumables orderId="order-1" orderNumber="LAB-2026-011" onClose={vi.fn()} />,
    );

  it("shows what the order has already consumed", async () => {
    render();

    await waitFor(() => expect(screen.getByText("Zirconia block")).toBeTruthy());
    expect(screen.getByText(/750 YER/)).toBeTruthy();
  });

  /**
   * The whole point of the slice. A number shown beside an order will be read as part of
   * that order's cost unless the screen says otherwise, and here it explicitly must not be.
   */
  it("says in plain Arabic that the cost is not owed to the lab", async () => {
    render();

    await waitFor(() =>
      expect(screen.getByText(/ليست جزءًا من مستحقات المعمل/)).toBeTruthy(),
    );
    expect(screen.getByText(/لا تدخل في خصم تكلفة المختبر من عمولة الطبيب/)).toBeTruthy();
  });

  it("reports an incomplete total rather than presenting it as complete", async () => {
    wire({ ...CONSUMED, materialCost: 750, unpricedLineCount: 2 });
    render();

    await waitFor(() => expect(screen.getByText(/2 مادة بلا سعر وحدة/)).toBeTruthy());
  });

  it("says so when nothing has been consumed yet", async () => {
    wire({ ...CONSUMED, lines: [], materialCost: 0 });
    render();

    await waitFor(() => expect(screen.getByText(/لم تُصرف أي مواد لهذا الأمر بعد/)).toBeTruthy());
  });

  /**
   * Moving stock is AdminOnly on the server. Showing a non-admin a form that fails on submit
   * would be worse than not showing one.
   */
  it("hides the recording form from a role that cannot move stock", async () => {
    role.current = "Receptionist";
    render();

    await waitFor(() => expect(screen.getByText("Zirconia block")).toBeTruthy());
    expect(screen.queryByText("صرف من المخزون")).toBeNull();
    expect(screen.queryByText("إضافة مادة")).toBeNull();
  });

  it("does not fetch the item list for a role that cannot use it", async () => {
    role.current = "Receptionist";
    render();

    await waitFor(() => expect(screen.getByText("Zirconia block")).toBeTruthy());
    expect(vi.mocked(api.get).mock.calls.every(([url]) => String(url).includes("/consumables")))
      .toBe(true);
  });

  it("submits the chosen materials against the order", async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    render();

    await waitFor(() => expect(screen.getByText("إضافة مادة")).toBeTruthy());
    fireEvent.click(screen.getByText("إضافة مادة"));

    await waitFor(() => expect(screen.getByLabelText("المادة 1")).toBeTruthy());
    fireEvent.change(screen.getByLabelText("المادة 1"), { target: { value: "item-1" } });
    fireEvent.change(screen.getByLabelText("الكمية 1"), { target: { value: "3" } });

    fireEvent.click(screen.getByText("صرف من المخزون"));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/api/inventory/consume-lab-order", {
        labOrderId: "order-1",
        items: [{ inventoryItemId: "item-1", quantity: 3 }],
        notes: undefined,
      }),
    );
  });

  /**
   * The server refuses a quantity above the balance; catching it here means the user is told
   * before they commit rather than after.
   */
  it("refuses to submit more than the balance holds", async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    render();

    await waitFor(() => expect(screen.getByText("إضافة مادة")).toBeTruthy());
    fireEvent.click(screen.getByText("إضافة مادة"));

    await waitFor(() => expect(screen.getByLabelText("المادة 1")).toBeTruthy());
    fireEvent.change(screen.getByLabelText("المادة 1"), { target: { value: "item-2" } });
    fireEvent.change(screen.getByLabelText("الكمية 1"), { target: { value: "5" } });

    await waitFor(() => expect(screen.getByText(/المتاح 2 فقط/)).toBeTruthy());
    expect((screen.getByText("صرف من المخزون") as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click(screen.getByText("صرف من المخزون"));
    expect(api.post).not.toHaveBeenCalled();
  });

  /**
   * Two lines for one item are each checked against the full opening stock, so the pair can
   * pass a per-line check and still overdraw. The server refuses them; so must the form.
   */
  it("refuses the same item on two lines instead of merging it", async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    render();

    await waitFor(() => expect(screen.getByText("إضافة مادة")).toBeTruthy());
    fireEvent.click(screen.getByText("إضافة مادة"));
    fireEvent.click(screen.getByText("إضافة مادة"));

    await waitFor(() => expect(screen.getByLabelText("المادة 2")).toBeTruthy());
    fireEvent.change(screen.getByLabelText("المادة 1"), { target: { value: "item-1" } });
    fireEvent.change(screen.getByLabelText("الكمية 1"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("المادة 2"), { target: { value: "item-1" } });
    fireEvent.change(screen.getByLabelText("الكمية 2"), { target: { value: "2" } });

    await waitFor(() => expect(screen.getByText(/المادة مكرّرة في أكثر من سطر/)).toBeTruthy());
    expect((screen.getByText("صرف من المخزون") as HTMLButtonElement).disabled).toBe(true);

    fireEvent.click(screen.getByText("صرف من المخزون"));
    expect(api.post).not.toHaveBeenCalled();
  });

  it("shows the server's refusal instead of reinterpreting it", async () => {
    wire(null);
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.includes("/consumables")) {
        return Promise.reject({
          response: { data: { message: "لا يوجد أمر مختبر بهذا الرمز ضمن صلاحياتك" } },
        });
      }
      return Promise.resolve({ data: INVENTORY });
    });

    render();

    await waitFor(() =>
      expect(screen.getByRole("alert").textContent).toContain(
        "لا يوجد أمر مختبر بهذا الرمز ضمن صلاحياتك",
      ),
    );
  });
});
