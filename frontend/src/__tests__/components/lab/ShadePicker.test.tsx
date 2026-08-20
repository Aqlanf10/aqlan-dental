import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent, waitFor } from "@testing-library/dom";
import { ShadePicker } from "@/components/lab/ShadePicker";
import { renderWithQueryClient } from "@/__tests__/testUtils/renderWithQueryClient";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

/**
 * LABINV-REQ-007.
 *
 * "A2", "a2" and "A 2" used to be three different values in the same column. The picker
 * fixes that going forward without rewriting what is already stored — so the behaviours
 * worth pinning are that it recognises an existing value whatever its case, and that it
 * never rewrites a shade the clinician deliberately typed.
 */
describe("ShadePicker", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockResolvedValue({ data: { shades: [] } });
  });

  it("stays a plain text field until the guide is opened", () => {
    renderWithQueryClient(<ShadePicker value="A2" onChange={vi.fn()} />);

    expect(screen.getByLabelText("الظل (اللون)")).toBeTruthy();
    expect(screen.queryByLabelText("درجة اللون A2")).toBeNull();
  });

  it("falls back to the standard guide when the clinic configured none", async () => {
    renderWithQueryClient(<ShadePicker value="" onChange={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("اختيار درجة اللون"));

    await waitFor(() => expect(screen.getByLabelText("درجة اللون A1")).toBeTruthy());
    expect(screen.getByLabelText("درجة اللون D4")).toBeTruthy();
  });

  it("uses the clinic's own guide when one is configured", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { shades: ["BL1", "BL2", "BL3"] } });

    renderWithQueryClient(<ShadePicker value="" onChange={vi.fn()} />);
    fireEvent.click(screen.getByLabelText("اختيار درجة اللون"));

    await waitFor(() => expect(screen.getByLabelText("درجة اللون BL2")).toBeTruthy());
    expect(screen.queryByLabelText("درجة اللون A1")).toBeNull();
  });

  it("writes the chosen shade", async () => {
    const onChange = vi.fn();
    renderWithQueryClient(<ShadePicker value="" onChange={onChange} />);

    fireEvent.click(screen.getByLabelText("اختيار درجة اللون"));
    await waitFor(() => expect(screen.getByLabelText("درجة اللون A3")).toBeTruthy());
    fireEvent.click(screen.getByLabelText("درجة اللون A3"));

    expect(onChange).toHaveBeenCalledWith("A3");
  });

  /**
   * A value stored years ago as "a2" is the same shade as "A2". Showing it as unset would
   * push the user to re-pick it and quietly change the order's history.
   */
  it("recognises an existing value regardless of case", async () => {
    renderWithQueryClient(<ShadePicker value="a2" onChange={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("اختيار درجة اللون"));

    await waitFor(() =>
      expect(screen.getByLabelText("درجة اللون A2").getAttribute("aria-pressed")).toBe("true"),
    );
    expect(screen.queryByText(/ليست ضمن دليل الألوان المعتمد/)).toBeNull();
  });

  /**
   * A shade outside the guide can be a real instruction to the lab. It is flagged, never
   * corrected — normalising it would change what the lab is asked to make.
   */
  it("flags an off-guide shade without rewriting it", async () => {
    const onChange = vi.fn();
    renderWithQueryClient(<ShadePicker value="BL2 مع شفافية" onChange={onChange} />);

    await waitFor(() =>
      expect(screen.getByText(/ليست ضمن دليل الألوان المعتمد/)).toBeTruthy(),
    );
    expect(onChange).not.toHaveBeenCalled();
    expect((screen.getByLabelText("الظل (اللون)") as HTMLInputElement).value).toBe(
      "BL2 مع شفافية",
    );
  });

  it("still accepts free typing", () => {
    const onChange = vi.fn();
    renderWithQueryClient(<ShadePicker value="" onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("الظل (اللون)"), { target: { value: "C3" } });

    expect(onChange).toHaveBeenCalledWith("C3");
  });
});
