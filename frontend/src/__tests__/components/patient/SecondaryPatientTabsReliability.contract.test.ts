import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const tabs = [
  { file: "LabOrdersTab.tsx", fallback: "فشل تحميل طلبات المختبر", reset: "setOrders([])" },
  { file: "ContractsTab.tsx", fallback: "فشل تحميل العقود", reset: "setContracts([])" },
  { file: "GeneralDentistryTab.tsx", fallback: "فشل تحميل بيانات طب الأسنان العام", reset: "setTreatments([])" },
];

describe("secondary patient tabs read reliability", () => {
  for (const tab of tabs) {
    const source = readFileSync(resolve(process.cwd(), `src/components/patient/tabs/${tab.file}`), "utf8");

    it(`${tab.file} rejects stale responses and resets stale patient data`, () => {
      expect(source).toContain("const requestId = ++requestRef.current");
      expect(source).toContain("requestRef.current === requestId");
      expect(source).toContain(tab.reset);
    });

    it(`${tab.file} surfaces backend messages and an explicit error state`, () => {
      expect(source).toContain(`extractErrorMessage(`);
      expect(source).toContain(tab.fallback);
      expect(source).toContain('role="alert"');
      expect(source).toContain("إعادة المحاولة");
    });
  }
});
