import type { CephAnalysisList } from "@/types/ceph";

export type CephWorkflowStageKey =
  | "notStarted"
  | "landmarks"
  | "measurements"
  | "approval"
  | "complete";

export interface CephWorkflowStage {
  key: CephWorkflowStageKey;
  label: string;
  actionLabel: string;
  complete: boolean;
}

export function getCephWorkflowStage(
  analysis: Pick<CephAnalysisList, "landmarkCount" | "hasMeasurements" | "isApproved">,
): CephWorkflowStage {
  if (analysis.landmarkCount === 0) {
    return { key: "notStarted", label: "لم يبدأ التتبع", actionLabel: "بدء التحليل", complete: false };
  }
  if (analysis.landmarkCount < 24) {
    return { key: "landmarks", label: "النقاط غير مكتملة", actionLabel: "إكمال النقاط", complete: false };
  }
  if (!analysis.hasMeasurements) {
    return { key: "measurements", label: "القياسات غير محفوظة", actionLabel: "حفظ القياسات", complete: false };
  }
  if (!analysis.isApproved) {
    return { key: "approval", label: "بانتظار اعتماد الطبيب", actionLabel: "مراجعة واعتماد", complete: false };
  }
  return { key: "complete", label: "مكتمل ومعتمد", actionLabel: "فتح التحليل", complete: true };
}
