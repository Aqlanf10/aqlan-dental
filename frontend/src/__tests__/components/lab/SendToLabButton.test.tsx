import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SendToLabButton } from "@/components/lab/SendToLabButton";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));
vi.mock("@/stores/toastStore", () => ({
  toast: { error: vi.fn(), success: vi.fn(), info: vi.fn() },
}));

/**
 * LABINV-REQ-009.
 *
 * The button opens WhatsApp with a message the server composed. What matters here is that
 * it never opens a half-formed link: a `wa.me/` URL with no number opens WhatsApp to
 * nobody, and the user walks away believing the lab was notified.
 */
describe("SendToLabButton", () => {
  let openSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    vi.clearAllMocks();
    openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
  });

  afterEach(() => {
    openSpy.mockRestore();
  });

  it("opens WhatsApp with the lab's number and the server's message", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { phone: "770245745", labName: "معمل الأمل", message: "أمر عمل معمل" },
    });

    render(<SendToLabButton orderId="order-1" />);
    fireEvent.click(screen.getByText(/واتساب/));

    await waitFor(() => expect(openSpy).toHaveBeenCalled());

    const url = String(openSpy.mock.calls[0][0]);
    // The local number is normalised to full international form before it reaches wa.me —
    // a bare "770245745" resolves to no account outside Yemen's dialling context.
    expect(url).toContain("https://wa.me/967770245745");
    expect(decodeURIComponent(url)).toContain("أمر عمل معمل");
  });

  it("asks the server for the message rather than composing one locally", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { phone: "770245745", labName: null, message: "نص" },
    });

    render(<SendToLabButton orderId="order-42" />);
    fireEvent.click(screen.getByText(/واتساب/));

    await waitFor(() =>
      expect(api.get).toHaveBeenCalledWith("/api/lab-orders/order-42/whatsapp-message"),
    );
  });

  /**
   * The server refuses an order whose lab has no number. That refusal must reach the user
   * as the server wrote it, and no window may open.
   */
  it("surfaces the server's refusal and opens nothing", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: {
        data: {
          message: "لا يوجد رقم واتساب أو هاتف للمعمل «معمل الأمل» — أضِفه من الإعدادات ← المعامل",
        },
      },
    });

    render(<SendToLabButton orderId="order-1" />);
    fireEvent.click(screen.getByText(/واتساب/));

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(
        expect.stringContaining("لا يوجد رقم واتساب أو هاتف للمعمل"),
      ),
    );
    expect(openSpy).not.toHaveBeenCalled();
  });

  /**
   * Defence in depth: a number that survives the server but normalises to nothing here
   * must not produce `wa.me/` with an empty path.
   */
  it("refuses a number that normalises to nothing", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { phone: "---", labName: "معمل الأمل", message: "نص" },
    });

    render(<SendToLabButton orderId="order-1" />);
    fireEvent.click(screen.getByText(/واتساب/));

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith(expect.stringContaining("رقم المعمل غير صالح")),
    );
    expect(openSpy).not.toHaveBeenCalled();
  });

  it("does not fire twice while a request is in flight", async () => {
    let resolveRequest: ((value: unknown) => void) | undefined;
    vi.mocked(api.get).mockReturnValue(
      new Promise((resolve) => {
        resolveRequest = resolve;
      }) as never,
    );

    render(<SendToLabButton orderId="order-1" />);
    const button = screen.getByText(/واتساب/);
    fireEvent.click(button);
    fireEvent.click(button);

    expect(api.get).toHaveBeenCalledTimes(1);

    resolveRequest?.({ data: { phone: "770245745", labName: null, message: "نص" } });
    await waitFor(() => expect(openSpy).toHaveBeenCalledTimes(1));
  });
});
