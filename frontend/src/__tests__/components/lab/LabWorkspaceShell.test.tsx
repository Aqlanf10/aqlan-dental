import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  LabWorkspaceShell, isLabView, LAB_VIEWS,
} from "@/app/(dashboard)/lab/_panels/LabWorkspaceShell";
import { useHasPermission } from "@/hooks/usePermissions";

vi.mock("@/hooks/usePermissions", async () => {
  const actual = await vi.importActual<typeof import("@/hooks/usePermissions")>(
    "@/hooks/usePermissions",
  );
  return { ...actual, useHasPermission: vi.fn() };
});

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

/**
 * CORE-LAB-017 — the lab is one workspace with pivots instead of five sidebar links.
 *
 * The behaviour that matters here is not the styling: it is that a pivot the user has no
 * permission for must not be offered at all. Rendering a "المستحقات" tab to someone who will
 * be refused by the server turns a permission boundary into a dead end.
 */
describe("CORE-LAB-017 lab workspace shell", () => {
  beforeEach(() => vi.clearAllMocks());

  const allowAll = () => vi.mocked(useHasPermission).mockReturnValue(true);

  it("offers every pivot to a user who holds all the lab permissions", () => {
    allowAll();
    render(<LabWorkspaceShell active="orders" onChange={vi.fn()}>محتوى</LabWorkspaceShell>);

    for (const view of LAB_VIEWS) {
      expect(screen.getByRole("tab", { name: new RegExp(view.label) })).toBeInTheDocument();
    }
  });

  it("hides the money pivots from a user who lacks their permission", () => {
    // Orders yes, payables and reports no — the clinical staff case.
    vi.mocked(useHasPermission).mockImplementation((key: string) =>
      key === "lab_orders.view",
    );

    render(<LabWorkspaceShell active="orders" onChange={vi.fn()}>محتوى</LabWorkspaceShell>);

    expect(screen.getByRole("tab", { name: /الطلبات/ })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /المتأخرة/ })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /المستحقات/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /التقارير/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /نظرة عامة/ })).not.toBeInTheDocument();
  });

  it("marks exactly one pivot as selected", () => {
    allowAll();
    render(<LabWorkspaceShell active="payables" onChange={vi.fn()}>محتوى</LabWorkspaceShell>);

    const selected = screen.getAllByRole("tab").filter(
      (t) => t.getAttribute("aria-selected") === "true",
    );
    expect(selected).toHaveLength(1);
    expect(selected[0]).toHaveTextContent("المستحقات");
  });

  it("reports the chosen pivot back to the caller", () => {
    allowAll();
    const onChange = vi.fn();
    render(<LabWorkspaceShell active="orders" onChange={onChange}>محتوى</LabWorkspaceShell>);

    fireEvent.click(screen.getByRole("tab", { name: /التقارير/ }));

    expect(onChange).toHaveBeenCalledWith("reports");
  });

  it("renders the active panel's own command-bar actions", () => {
    // The bar is a slot, not a fixed menu — so a pivot can never show a button that does
    // nothing on it.
    allowAll();
    render(
      <LabWorkspaceShell active="orders" onChange={vi.fn()} actions={<button>طلب جديد</button>}>
        محتوى
      </LabWorkspaceShell>,
    );

    expect(screen.getByRole("button", { name: "طلب جديد" })).toBeInTheDocument();
  });

  it("falls back to the orders pivot for an unknown or missing ?view=", () => {
    // A stale bookmark pointing at a pivot that no longer exists must land somewhere real.
    expect(isLabView("payables")).toBe(true);
    expect(isLabView("nonsense")).toBe(false);
    expect(isLabView(null)).toBe(false);
  });
});
