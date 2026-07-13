import { describe, expect, it } from "vitest";
import { getCephWorkflowStage } from "@/lib/cephWorkflow";

describe("ceph list workflow stage", () => {
  it.each([
    [{ landmarkCount: 0, hasMeasurements: false, isApproved: false }, "notStarted", "بدء التحليل"],
    [{ landmarkCount: 12, hasMeasurements: false, isApproved: false }, "landmarks", "إكمال النقاط"],
    [{ landmarkCount: 24, hasMeasurements: false, isApproved: false }, "measurements", "حفظ القياسات"],
    [{ landmarkCount: 24, hasMeasurements: true, isApproved: false }, "approval", "مراجعة واعتماد"],
    [{ landmarkCount: 24, hasMeasurements: true, isApproved: true }, "complete", "فتح التحليل"],
  ] as const)("derives %s as %s", (input, key, actionLabel) => {
    expect(getCephWorkflowStage(input)).toMatchObject({ key, actionLabel });
  });

  it("does not hide missing measurements behind an inconsistent approval flag", () => {
    expect(getCephWorkflowStage({
      landmarkCount: 24,
      hasMeasurements: false,
      isApproved: true,
    }).key).toBe("measurements");
  });
});
