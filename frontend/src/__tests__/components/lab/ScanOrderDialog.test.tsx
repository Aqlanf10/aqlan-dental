import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ScanOrderDialog } from "@/components/lab/ScanOrderDialog";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

/**
 * LABINV-REQ-008.
 *
 * The dialog is a thin client over a permission-checked lookup, so what these tests defend
 * is the behaviour around it: that typing always works even where the camera API does not
 * exist, that the server's refusal is shown verbatim rather than reinterpreted, and that a
 * refusal never resolves to an order.
 */
describe("ScanOrderDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // jsdom has no BarcodeDetector — which is also the real state of Safari, so this is
    // the unsupported-camera path under test, not an artificial one.
    delete (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector;
  });

  afterEach(() => {
    delete (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector;
  });

  it("offers manual entry even when the browser cannot scan", () => {
    render(<ScanOrderDialog onClose={vi.fn()} onResolved={vi.fn()} />);

    expect(screen.getByLabelText("رقم الطلب")).toBeTruthy();
    expect(screen.getByText(/متصفحك لا يدعم قراءة الرموز بالكاميرا/)).toBeTruthy();
    expect(screen.queryByText("فتح الكاميرا للمسح")).toBeNull();
  });

  it("offers the camera when the browser supports it", () => {
    (window as unknown as { BarcodeDetector?: unknown }).BarcodeDetector = function () {};

    render(<ScanOrderDialog onClose={vi.fn()} onResolved={vi.fn()} />);

    expect(screen.getByText("فتح الكاميرا للمسح")).toBeTruthy();
  });

  it("resolves a typed code to its order", async () => {
    const onResolved = vi.fn();
    vi.mocked(api.get).mockResolvedValue({
      data: { id: "order-1", orderNumber: "LAB-2026-003" },
    });

    render(<ScanOrderDialog onClose={vi.fn()} onResolved={onResolved} />);
    fireEvent.change(screen.getByLabelText("رقم الطلب"), {
      target: { value: "LAB-2026-003" },
    });
    fireEvent.click(screen.getByText("بحث"));

    await waitFor(() =>
      expect(onResolved).toHaveBeenCalledWith({ id: "order-1", orderNumber: "LAB-2026-003" }),
    );
    expect(api.get).toHaveBeenCalledWith("/api/lab-orders/lookup", {
      params: { code: "LAB-2026-003" },
    });
  });

  it("submits on Enter, because that is what a barcode wedge sends", async () => {
    const onResolved = vi.fn();
    vi.mocked(api.get).mockResolvedValue({
      data: { id: "order-9", orderNumber: "LAB-2026-009" },
    });

    render(<ScanOrderDialog onClose={vi.fn()} onResolved={onResolved} />);
    const input = screen.getByLabelText("رقم الطلب");
    fireEvent.change(input, { target: { value: "LAB-2026-009" } });
    fireEvent.keyDown(input, { key: "Enter" });

    await waitFor(() => expect(onResolved).toHaveBeenCalled());
  });

  it("trims a code that arrives with whitespace from a decode", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { id: "order-1", orderNumber: "LAB-2026-003" },
    });

    render(<ScanOrderDialog onClose={vi.fn()} onResolved={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("رقم الطلب"), {
      target: { value: "  LAB-2026-003 \n" },
    });
    fireEvent.click(screen.getByText("بحث"));

    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith("/api/lab-orders/lookup", {
        params: { code: "LAB-2026-003" },
      }),
    );
  });

  it("does not call the server for an empty code", () => {
    render(<ScanOrderDialog onClose={vi.fn()} onResolved={vi.fn()} />);

    fireEvent.click(screen.getByText("بحث"));

    expect(api.get).not.toHaveBeenCalled();
    expect(screen.getByRole("alert").textContent).toContain("أدخل رقم الطلب");
  });

  /**
   * The server deliberately answers "not found", "another branch" and "not your patient"
   * identically. The dialog must show that message as-is — inventing a friendlier or more
   * specific one would leak the distinction the server took care to hide.
   */
  it("shows the server's refusal verbatim and resolves nothing", async () => {
    const onResolved = vi.fn();
    vi.mocked(api.get).mockRejectedValue({
      response: { data: { message: "لا يوجد أمر مختبر بهذا الرمز ضمن صلاحياتك" } },
    });

    render(<ScanOrderDialog onClose={vi.fn()} onResolved={onResolved} />);
    fireEvent.change(screen.getByLabelText("رقم الطلب"), {
      target: { value: "LAB-9999-999" },
    });
    fireEvent.click(screen.getByText("بحث"));

    await waitFor(() =>
      expect(screen.getByRole("alert").textContent).toContain(
        "لا يوجد أمر مختبر بهذا الرمز ضمن صلاحياتك",
      ),
    );
    expect(onResolved).not.toHaveBeenCalled();
  });

  it("falls back to the typed code when the order carries no number", async () => {
    const onResolved = vi.fn();
    vi.mocked(api.get).mockResolvedValue({ data: { id: "order-1", orderNumber: null } });

    render(<ScanOrderDialog onClose={vi.fn()} onResolved={onResolved} />);
    fireEvent.change(screen.getByLabelText("رقم الطلب"), { target: { value: "LAB-2026-003" } });
    fireEvent.click(screen.getByText("بحث"));

    await waitFor(() =>
      expect(onResolved).toHaveBeenCalledWith({ id: "order-1", orderNumber: "LAB-2026-003" }),
    );
  });
});
