import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import {
  ToothPicker,
  parseToothValue,
  formatToothValue,
  FDI_QUADRANTS,
} from "@/components/lab/ToothPicker";

/**
 * LABINV-REQ-006.
 *
 * The picker writes the same free-text column staff typed into before. That constraint is
 * what these tests defend: a selector whose output differs from a typed value would split
 * the clinic's history in two, and one that discards a notation it does not recognise
 * would delete real clinical data on first edit.
 */

describe("parseToothValue", () => {
  it("recognises FDI numbers separated by commas, Arabic commas, spaces or slashes", () => {
    expect(parseToothValue("11, 12").selected).toEqual(["11", "12"]);
    expect(parseToothValue("11، 12").selected).toEqual(["11", "12"]);
    expect(parseToothValue("11 12").selected).toEqual(["11", "12"]);
    expect(parseToothValue("11/12").selected).toEqual(["11", "12"]);
  });

  it("keeps a token it does not recognise instead of dropping it", () => {
    const { selected, extras } = parseToothValue("11, upper-arch, 99");
    expect(selected).toEqual(["11"]);
    expect(extras).toEqual(["upper-arch", "99"]);
  });

  it("does not duplicate a tooth listed twice", () => {
    expect(parseToothValue("11, 11, 12").selected).toEqual(["11", "12"]);
  });

  it("treats empty and missing values as no selection", () => {
    expect(parseToothValue("").selected).toEqual([]);
    expect(parseToothValue(undefined).selected).toEqual([]);
    expect(parseToothValue(null).selected).toEqual([]);
  });
});

describe("formatToothValue", () => {
  /**
   * The contract with the rest of the system: whatever the picker emits must be
   * indistinguishable from the same teeth typed by hand.
   */
  it("emits the same string a person would type", () => {
    expect(formatToothValue(["11", "12"], [])).toBe("11, 12");
  });

  it("carries preserved tokens through unchanged", () => {
    expect(formatToothValue(["11"], ["upper-arch"])).toBe("11, upper-arch");
  });
});

describe("FDI quadrants", () => {
  it("covers all 32 permanent teeth with no duplicates", () => {
    const all = [
      ...FDI_QUADRANTS.upperRight,
      ...FDI_QUADRANTS.upperLeft,
      ...FDI_QUADRANTS.lowerRight,
      ...FDI_QUADRANTS.lowerLeft,
    ];
    expect(all).toHaveLength(32);
    expect(new Set(all).size).toBe(32);
  });

  it("numbers each quadrant from the midline outward", () => {
    expect(FDI_QUADRANTS.upperRight[7]).toBe("11");
    expect(FDI_QUADRANTS.upperLeft[0]).toBe("21");
    expect(FDI_QUADRANTS.lowerLeft[0]).toBe("31");
    expect(FDI_QUADRANTS.lowerRight[7]).toBe("41");
  });
});

describe("ToothPicker", () => {
  it("stays a plain text field until the chart is opened", () => {
    render(<ToothPicker value="11" onChange={vi.fn()} />);

    expect(screen.getByLabelText("رقم الأسنان")).toBeTruthy();
    expect(screen.queryByLabelText("السن 16")).toBeNull();
  });

  it("adds a tooth to the stored value when clicked", () => {
    const onChange = vi.fn();
    render(<ToothPicker value="11" onChange={onChange} />);

    fireEvent.click(screen.getByLabelText("اختيار الأسنان"));
    fireEvent.click(screen.getByLabelText("السن 12"));

    expect(onChange).toHaveBeenCalledWith("11, 12");
  });

  it("removes a tooth that was already selected", () => {
    const onChange = vi.fn();
    render(<ToothPicker value="11, 12" onChange={onChange} />);

    fireEvent.click(screen.getByLabelText("اختيار الأسنان"));
    fireEvent.click(screen.getByLabelText("السن 11"));

    expect(onChange).toHaveBeenCalledWith("12");
  });

  it("shows a stored tooth as already selected", () => {
    render(<ToothPicker value="16" onChange={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("اختيار الأسنان"));

    expect(screen.getByLabelText("السن 16").getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByLabelText("السن 17").getAttribute("aria-pressed")).toBe("false");
  });

  /**
   * The one that matters most. An order written before this picker existed may say
   * "upper arch" — clearing the chart must not silently delete that instruction to the lab.
   */
  it("keeps an unrecognised notation when the selection is cleared", () => {
    const onChange = vi.fn();
    render(<ToothPicker value="11, upper-arch" onChange={onChange} />);

    fireEvent.click(screen.getByLabelText("اختيار الأسنان"));
    fireEvent.click(screen.getByText("مسح التحديد"));

    expect(onChange).toHaveBeenCalledWith("upper-arch");
  });

  it("tells the user which stored tokens the chart could not interpret", () => {
    render(<ToothPicker value="11, upper-arch" onChange={vi.fn()} />);

    fireEvent.click(screen.getByLabelText("اختيار الأسنان"));

    expect(screen.getByText(/upper-arch/)).toBeTruthy();
  });

  it("still accepts free typing", () => {
    const onChange = vi.fn();
    render(<ToothPicker value="" onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("رقم الأسنان"), { target: { value: "13" } });

    expect(onChange).toHaveBeenCalledWith("13");
  });
});
