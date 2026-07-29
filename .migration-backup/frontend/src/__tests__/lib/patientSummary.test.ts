import { describe, it, expect } from "vitest";
import { patientBalanceView } from "@/lib/patientSummary";
import { formatArabicDate } from "@/lib/utils";

// CORE-PAT-001: the patient banner used `summary?.totalOutstanding ?? 0` while
// the summary request was fetched with `.catch(() => {})`. A failed request
// therefore rendered "0 ر.ي" — a patient WITH debt looked fully paid.
describe("patientBalanceView", () => {
  it("reports the real balance when the summary loaded", () => {
    expect(patientBalanceView({ totalOutstanding: 300000 }, null)).toEqual({
      state: "loaded",
      outstanding: 300000,
      hasDebt: true,
    });
  });

  it("treats a genuine zero as a loaded zero, not as unknown", () => {
    const v = patientBalanceView({ totalOutstanding: 0 }, null);
    expect(v.state).toBe("loaded");
    expect(v.outstanding).toBe(0);
    expect(v.hasDebt).toBe(false);
  });

  it("never claims a zero balance when the summary failed", () => {
    const v = patientBalanceView(null, "تعذر تحميل ملخص المريض");
    expect(v.state).toBe("error");
    expect(v.outstanding).toBeNull();
    expect(v.hasDebt).toBe(false);
  });

  it("is 'loading', not 'zero', before the first response arrives", () => {
    const v = patientBalanceView(null, null);
    expect(v.state).toBe("loading");
    expect(v.outstanding).toBeNull();
  });

  it("treats a summary without an outstanding figure as unknown", () => {
    expect(patientBalanceView({}, "boom").outstanding).toBeNull();
    expect(patientBalanceView({ totalOutstanding: null }, null).state).toBe("loading");
  });
});

// CORE-PAT-004: a stale DTO field made formatArabicDate receive undefined,
// which threw RangeError during render and took the whole patient file down
// into the error boundary.
describe("formatArabicDate", () => {
  it("renders a dash instead of throwing on missing or invalid input", () => {
    expect(() => formatArabicDate(undefined)).not.toThrow();
    expect(formatArabicDate(undefined)).toBe("—");
    expect(formatArabicDate(null)).toBe("—");
    expect(formatArabicDate("")).toBe("—");
    expect(formatArabicDate("not-a-date")).toBe("—");
  });

  it("still formats a valid date", () => {
    expect(formatArabicDate("2026-07-24")).not.toBe("—");
  });
});
