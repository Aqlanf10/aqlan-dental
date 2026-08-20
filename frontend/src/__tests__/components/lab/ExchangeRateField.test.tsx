import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, fireEvent } from "@testing-library/dom";
import { ExchangeRateField, type ExchangeRateSnapshot } from "@/components/lab/ExchangeRateField";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

/**
 * LABINV-REQ-010.
 *
 * The field these tests cover replaced a bare number input that nobody checked. The rate
 * typed there multiplies into the lab order cost, the supplier bill, and the lab-cost
 * deduction inside the doctor's commission — so what matters is not that a number renders,
 * but that the user is never handed a number they would wrongly assume was verified.
 */

function snapshot(overrides: Partial<ExchangeRateSnapshot> = {}): ExchangeRateSnapshot {
  return {
    market: "sanaa",
    marketLabel: "سوق صنعاء",
    baseCurrency: "YER",
    currencies: ["YER", "SAR", "USD"],
    ratesToYer: { YER: 1, SAR: 142, USD: 535 },
    updatedOn: "2026-08-18",
    ageInDays: 2,
    staleAfterDays: 14,
    isStale: false,
    markets: [
      { key: "sanaa", label: "سوق صنعاء" },
      { key: "aden", label: "سوق عدن" },
      { key: "custom", label: "سعر مخصص للمركز" },
    ],
    ...overrides,
  };
}

describe("ExchangeRateField", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockResolvedValue({ data: snapshot() });
  });

  it("prefills the configured market rate when the field is empty", async () => {
    const onChange = vi.fn();
    renderWithQueryClient(
      <ExchangeRateField currency="SAR" value={undefined} onChange={onChange} />,
    );

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(142));
  });

  it("names the market and the review date so the number is attributable", async () => {
    renderWithQueryClient(
      <ExchangeRateField currency="USD" value={535} onChange={vi.fn()} />,
    );

    await waitFor(() => {
      expect(screen.getByText(/سوق صنعاء/)).toBeTruthy();
      expect(screen.getByText(/آخر مراجعة 2026-08-18/)).toBeTruthy();
    });
  });

  /**
   * The point of the whole feature. A rate the user already set is theirs; silently
   * replacing it with the market rate is the same class of bug as the silent default
   * this feature removes.
   */
  it("does not overwrite a rate the user already set", async () => {
    const onChange = vi.fn();
    renderWithQueryClient(
      <ExchangeRateField currency="SAR" value={150} onChange={onChange} />,
    );

    // Wait for text that only renders once the rate has actually arrived and been
    // applied to the DOM. Waiting on `api.get` alone passes before React re-renders,
    // which made an earlier version of this test green even when the component did
    // overwrite the user's value.
    await waitFor(() => expect(screen.getByText(/السعر المعتمد \(سوق صنعاء\)/)).toBeTruthy());

    expect(onChange).not.toHaveBeenCalled();
  });

  it("marks a rate that differs from the market as a deliberate override", async () => {
    renderWithQueryClient(
      <ExchangeRateField currency="SAR" value={150} onChange={vi.fn()} />,
    );

    await waitFor(() => {
      expect(screen.getByText(/سعر مخصص لهذا الطلب/)).toBeTruthy();
    });
  });

  it("offers a way back to the market rate after a manual override", async () => {
    const onChange = vi.fn();
    renderWithQueryClient(
      <ExchangeRateField currency="SAR" value={150} onChange={onChange} />,
    );

    const restore = await waitFor(() => screen.getByText("استعادة سعر السوق"));
    fireEvent.click(restore);

    expect(onChange).toHaveBeenCalledWith(142);
  });

  /**
   * Staleness must be visible. An eight-month-old rate in a market that moves weekly is
   * not "the rate" — it is a number from a different economy.
   */
  it("warns when the stored rates are past their review window", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: snapshot({ isStale: true, ageInDays: 240, updatedOn: "2025-12-20" }),
    });

    renderWithQueryClient(
      <ExchangeRateField currency="USD" value={535} onChange={vi.fn()} />,
    );

    await waitFor(() => {
      expect(screen.getByText(/لم تُراجَع منذ 240 يومًا/)).toBeTruthy();
    });
  });

  it("says the rates were never reviewed rather than implying they are current", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: snapshot({ isStale: true, updatedOn: null, ageInDays: null }),
    });

    renderWithQueryClient(
      <ExchangeRateField currency="USD" value={535} onChange={vi.fn()} />,
    );

    await waitFor(() => {
      expect(screen.getByText(/لم تُراجَع بعد منذ تركيب النظام/)).toBeTruthy();
    });
  });

  /**
   * A failed lookup must not look like a working one. The user may still type a rate,
   * but they are told the system could not offer one.
   */
  it("states a failed lookup and never invents a fallback rate", async () => {
    const onChange = vi.fn();
    vi.mocked(api.get).mockRejectedValue(new Error("network down"));

    renderWithQueryClient(
      <ExchangeRateField currency="SAR" value={undefined} onChange={onChange} />,
    );

    await waitFor(() => {
      expect(screen.getByText(/تعذر جلب سعر الصرف المعتمد/)).toBeTruthy();
    });
    // No rate was offered, so none may have been written into the form.
    expect(onChange).not.toHaveBeenCalled();
  });

  it("says so when the configured market has no rate for the chosen currency", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: snapshot({ ratesToYer: { YER: 1 } }),
    });

    renderWithQueryClient(
      <ExchangeRateField currency="USD" value={undefined} onChange={vi.fn()} />,
    );

    await waitFor(() => {
      expect(screen.getByText(/لا يوجد سعر معتمد لعملة USD/)).toBeTruthy();
    });
  });
});
