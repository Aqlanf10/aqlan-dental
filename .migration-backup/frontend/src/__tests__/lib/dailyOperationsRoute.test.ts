import { describe, expect, it } from "vitest";
import { getDailyOperationsModuleTab } from "@/lib/dailyOperationsRoute";

describe("daily operations route modules", () => {
  it.each(["appointments", "queue", "checkout", "lab", "report"])(
    "accepts the supported %s module",
    (tab) => {
      expect(getDailyOperationsModuleTab(tab)).toBe(tab);
    },
  );

  it.each([null, "finance", "__proto__", "QUEUE"])(
    "falls back safely for an unsupported module value",
    (tab) => {
      expect(getDailyOperationsModuleTab(tab)).toBeNull();
    },
  );
});
