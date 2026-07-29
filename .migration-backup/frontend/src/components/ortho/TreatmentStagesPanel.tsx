"use client";
import { useState } from "react";
import type { TreatmentStage } from "@/types/ortho";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { cn } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import { CheckCircle, Circle, PlayCircle } from "lucide-react";

const STATUS_CONFIG: Record<string, { label: string; icon: typeof Circle; color: string }> = {
  pending:   { label: "لم تبدأ", icon: Circle,      color: "text-gray-300" },
  active:    { label: "جارية",   icon: PlayCircle,  color: "text-clinic-blue" },
  completed: { label: "مكتملة",  icon: CheckCircle, color: "text-green-500" },
};

interface Props {
  caseId: string;
  stages: TreatmentStage[];
  onUpdate?: (updated: TreatmentStage) => void;
}

export function TreatmentStagesPanel({ caseId, stages, onUpdate }: Props) {
  const [updating, setUpdating] = useState<string | null>(null);

  const handleStatusChange = async (stage: TreatmentStage, newStatus: string) => {
    setUpdating(stage.id);
    try {
      const { data } = await api.put<TreatmentStage>(
        `/api/ortho-cases/${caseId}/stages/${stage.id}`,
        { status: newStatus }
      );
      onUpdate?.(data);
    } catch (err) {
      // A silent failure here reads as "the stage advanced" — it didn't.
      toast.error(extractErrorMessage(err, "فشل تحديث حالة المرحلة"));
    } finally {
      setUpdating(null);
    }
  };

  if (!stages.length) {
    return <p className="text-gray-400 text-sm">لا توجد مراحل مسجلة</p>;
  }

  return (
    <div className="space-y-3">
      {stages.map((stage, idx) => {
        const cfg = STATUS_CONFIG[stage.status] ?? STATUS_CONFIG.pending;
        const Icon = cfg.icon;
        return (
          <div key={stage.id} className="flex items-start gap-4">
            {/* Connector */}
            <div className="flex flex-col items-center flex-shrink-0">
              <Icon className={cn("w-6 h-6", cfg.color, updating === stage.id && "animate-pulse")} />
              {idx < stages.length - 1 && (
                <div className={cn("w-0.5 h-8 mt-1", stage.status === "completed" ? "bg-green-300" : "bg-gray-200")} />
              )}
            </div>

            {/* Content */}
            <div className={cn(
              "flex-1 rounded-lg p-3 border text-sm transition",
              stage.status === "active"    && "bg-clinic-blue-50 border-clinic-blue-100",
              stage.status === "completed" && "bg-green-50 border-green-200",
              stage.status === "pending"   && "bg-white border-gray-200"
            )}>
              <div className="flex items-center justify-between gap-2 flex-wrap">
                <span className={cn(
                  "font-semibold",
                  stage.status === "active"    && "text-clinic-navy-700",
                  stage.status === "completed" && "text-green-800",
                  stage.status === "pending"   && "text-gray-500"
                )}>
                  {stage.stageOrder}. {stage.stageName}
                </span>
                <div className="flex items-center gap-2">
                  <span className="text-xs text-gray-400">{cfg.label}</span>
                  {stage.status !== "completed" && (
                    <button
                      onClick={() => handleStatusChange(stage, stage.status === "pending" ? "active" : "completed")}
                      disabled={updating === stage.id}
                      className="text-xs px-2 py-0.5 rounded bg-white border border-gray-200 hover:bg-gray-50 transition disabled:opacity-50"
                    >
                      {stage.status === "pending" ? "بدء" : "إكمال"}
                    </button>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-4 mt-1 text-xs text-gray-500 flex-wrap">
                {stage.startedAt && <span>بدأت: {stage.startedAt}</span>}
                {stage.completedAt && <span>اكتملت: {stage.completedAt}</span>}
                {stage.targetDurationMonths && <span>المدة المتوقعة: {stage.targetDurationMonths} أشهر</span>}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
