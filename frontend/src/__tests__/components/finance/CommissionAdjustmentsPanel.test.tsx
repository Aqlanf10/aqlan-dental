import { fireEvent, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CommissionAdjustmentsPanel } from "@/app/(dashboard)/finance-v3/components/CommissionAdjustmentsPanel";
import { renderWithQueryClient } from "../../testUtils/renderWithQueryClient";
import api from "@/lib/api";
import { formatYER } from "@/app/(dashboard)/finance-v3/components/FinanceHelpers";
import { toast } from "@/stores/toastStore";

vi.mock("@/lib/api", () => {
  const client = { get: vi.fn(), post: vi.fn() };
  return { default: client, api: client };
});

vi.mock("@/stores/toastStore", () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

const OVERPAID = {
  id: "adj-1",
  doctorId: "doc-1",
  doctorName: "د. عقلان",
  invoiceLineItemId: "line-1",
  invoiceId: "inv-1",
  invoiceNumber: "INV-2026-001",
  patientName: "سالم المريض",
  serviceName: "تركيب تاج",
  labOrderId: "lo-1",
  labOrderNumber: "LAB-2026-003",
  paidCommissionAmount: 32000,
  recalculatedCommissionAmount: 26000,
  adjustmentAmount: -6000,
  previousLabCost: 20000,
  currentLabCost: 35000,
  reason: "تغيّرت تكلفة المعمل الفعلية بعد صرف العمولة",
  status: "Pending" as const,
  settledByPaymentId: null,
  settledOn: null,
  createdAt: "2026-07-29T10:00:00Z",
};

describe("CORE-FIN-LAB-ADJ commission adjustments panel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows an error with retry rather than a false 'no differences' state", async () => {
    // The empty state here reads as "the doctor's commissions are all correct". Showing it
    // after a failed load would be a claim the screen cannot back up.
    vi.mocked(api.get).mockRejectedValue(new Error("offline"));

    renderWithQueryClient(<CommissionAdjustmentsPanel />);

    expect(
      await screen.findByText("تعذّر تحميل بنود التسوية — لا يمكن تأكيد عدم وجود فروقات")
    ).toBeInTheDocument();
    expect(
      screen.queryByText("لا توجد فروقات في تكاليف المعامل تستدعي تسوية")
    ).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("renders a negative correction as an overpayment the doctor owes back", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [OVERPAID] });

    renderWithQueryClient(<CommissionAdjustmentsPanel />);

    expect(await screen.findByText("LAB-2026-003")).toBeInTheDocument();
    expect(screen.getByText("سالم المريض")).toBeInTheDocument();

    // Both figures are shown side by side, because the point of the row is the gap between
    // what was paid and what should have been. formatYER is reused rather than reimplemented
    // so this asserts the value, not the Arabic locale's digit shapes.
    expect(screen.getByText(formatYER(32000))).toBeInTheDocument();
    expect(screen.getByText(formatYER(26000))).toBeInTheDocument();

    // The sign says who owes whom, and a negative carries its own minus — no "+" prefix.
    expect(screen.getByText(formatYER(-6000))).toBeInTheDocument();

    expect(screen.getByText(/1 بند تسوية بانتظار الصرف/)).toBeInTheDocument();
  });

  it("marks a correction the clinic still owes with an explicit plus", async () => {
    const OWED = {
      ...OVERPAID,
      id: "adj-2",
      paidCommissionAmount: 32000,
      recalculatedCommissionAmount: 35200,
      adjustmentAmount: 3200,
      currentLabCost: 12000,
    };
    vi.mocked(api.get).mockResolvedValue({ data: [OWED] });

    renderWithQueryClient(<CommissionAdjustmentsPanel />);

    expect(await screen.findByText(`+${formatYER(3200)}`)).toBeInTheDocument();
  });

  it("shows the empty state only when the request actually succeeded with no rows", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });

    renderWithQueryClient(<CommissionAdjustmentsPanel />);

    expect(
      await screen.findByText("لا توجد فروقات في تكاليف المعامل تستدعي تسوية")
    ).toBeInTheDocument();
  });

  it("surfaces records the backfill sweep deliberately left untouched", async () => {
    // A sweep that silently skips ambiguous records would read as "everything is corrected".
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    vi.mocked(api.post).mockResolvedValue({
      data: {
        lineItemsExamined: 12,
        recalculated: 3,
        adjustmentsRaised: 1,
        netAdjustmentAmount: -6000,
        skipped: ["LAB-2026-009: عملة الفاتورة (USD) تختلف عن عملة طلب المعمل (SAR)"],
      },
    });

    renderWithQueryClient(<CommissionAdjustmentsPanel />);
    fireEvent.click(await screen.findByRole("button", { name: /مزامنة تكاليف المعامل/ }));

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(expect.stringContaining("تُركت 1 حالة دون تعديل"))
    );
    expect(toast.success).toHaveBeenCalledWith(expect.stringContaining("أُنشئ 1 بند تسوية"));
  });

  it("refuses to cancel a correction without a reason", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [OVERPAID] });

    renderWithQueryClient(<CommissionAdjustmentsPanel />);
    fireEvent.click(await screen.findByRole("button", { name: "إلغاء" }));
    fireEvent.click(screen.getByRole("button", { name: /تأكيد الإلغاء/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("سبب الإلغاء مطلوب"));
    expect(api.post).not.toHaveBeenCalled();
  });

  it("cancels a correction with its reason attached", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [OVERPAID] });
    vi.mocked(api.post).mockResolvedValue({ data: { ...OVERPAID, status: "Cancelled" } });

    renderWithQueryClient(<CommissionAdjustmentsPanel />);
    fireEvent.click(await screen.findByRole("button", { name: "إلغاء" }));

    fireEvent.change(screen.getByPlaceholderText(/أُدخلت التكلفة خطأً/), {
      target: { value: "صُحّحت من المصدر" },
    });
    fireEvent.click(screen.getByRole("button", { name: /تأكيد الإلغاء/ }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith("/api/commissions/adjustments/adj-1/cancel", {
        reason: "صُحّحت من المصدر",
      })
    );
  });
});
