import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CephSuperimposeCanvas } from "@/components/ceph/CephSuperimposeCanvas";

const records = [
  {
    id: "analysis:first",
    label: "قبل العلاج",
    date: "١ يناير ٢٠٢٦",
    color: "#2563eb",
    landmarks: [
      { key: "S", x: 0, y: 0 },
      { key: "N", x: 100, y: 0 },
      { key: "A", x: 80, y: 60 },
    ],
  },
  {
    id: "analysis:second",
    label: "بعد العلاج",
    date: "١ يوليو ٢٠٢٦",
    color: "#059669",
    landmarks: [
      { key: "S", x: 20, y: 30 },
      { key: "N", x: 120, y: 30 },
      { key: "A", x: 102, y: 88 },
    ],
  },
  {
    id: "version:third",
    label: "نسخة: المراجعة",
    date: "١٣ يوليو ٢٠٢٦",
    color: "#dc2626",
    landmarks: [
      { key: "S", x: -20, y: 10 },
      { key: "N", x: 80, y: 10 },
      { key: "A", x: 64, y: 72 },
    ],
  },
];

afterEach(() => vi.restoreAllMocks());

describe("CephSuperimposeCanvas", () => {
  it("renders multiple colored layers and lets the doctor change reference and opacity", () => {
    render(<CephSuperimposeCanvas records={records} initialReferenceId="analysis:first" />);

    const firstReference = screen.getByRole("radio", { name: "اختيار قبل العلاج كمرجع" });
    const secondReference = screen.getByRole("radio", { name: "اختيار بعد العلاج كمرجع" });
    expect(firstReference).toBeChecked();

    fireEvent.click(secondReference);
    expect(secondReference).toBeChecked();
    expect(screen.getByText(/المرجع: بعد العلاج/)).toBeInTheDocument();

    const opacity = screen.getByRole("slider", { name: "شفافية نسخة: المراجعة" });
    fireEvent.change(opacity, { target: { value: "0.35" } });
    expect(opacity).toHaveValue("0.35");
    expect(screen.getByRole("img")).toHaveAttribute("aria-label", expect.stringContaining("3 سجلات"));
  });

  it("exports SVG with auditable record and reference metadata", () => {
    vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:ceph-superimposition");
    vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
    const originalSerialize = XMLSerializer.prototype.serializeToString;
    let serialized = "";
    vi.spyOn(XMLSerializer.prototype, "serializeToString").mockImplementation(function (
      this: XMLSerializer,
      node: Node,
    ) {
      serialized = originalSerialize.call(this, node);
      return serialized;
    });
    render(<CephSuperimposeCanvas records={records} initialReferenceId="analysis:first" />);

    fireEvent.click(screen.getByRole("button", { name: "تصدير SVG" }));

    expect(serialized).toContain("aqlan-ceph-superimposition");
    expect(serialized).toContain("analysis:first");
    expect(serialized).toContain("analysis:second");
    expect(serialized).toContain("version:third");
    expect(serialized).toContain("referenceId");
    expect(URL.createObjectURL).toHaveBeenCalledOnce();
  });
});
