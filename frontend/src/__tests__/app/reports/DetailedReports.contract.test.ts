import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const detailedSource = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/reports/operations/page.tsx"),
  "utf8",
);
const summarySource = readFileSync(
  resolve(process.cwd(), "src/app/(dashboard)/reports/page.tsx"),
  "utf8",
);

describe("detailed reports contract", () => {
  it.each([
    "new-patients",
    "treated-patients",
    "income",
    "outstanding-balances",
    "treatment-progress",
    "returning-patients",
    "ortho-cases",
  ])("offers the %s report", (reportType) => {
    expect(detailedSource).toContain(`"${reportType}"`);
  });

  it("loads paginated details and exports through authenticated API helpers", () => {
    expect(detailedSource).toContain('"/api/reports/operations/details"');
    expect(detailedSource).toContain('"/api/reports/operations/export"');
    expect(detailedSource).toContain("pageSize: 50");
    expect(detailedSource).toContain("downloadBlob");
  });

  it("keeps currencies explicit instead of formatting all totals as YER", () => {
    expect(summarySource).toContain("totalsByCurrency");
    expect(summarySource).toContain("item.currency");
    expect(summarySource).not.toContain("formatYemeniRiyal(data.totalCollected)");
  });

  it("does not call the former nonexistent summary export routes", () => {
    [
      "export/center-summary",
      "export/revenue-comparison",
      "export/doctor-performance",
      "export/surgery-stats",
      "export/lab-orders",
      "export/prescription-analytics",
      "export/booking-funnel",
    ].forEach((route) => expect(summarySource).not.toContain(route));
  });
});
