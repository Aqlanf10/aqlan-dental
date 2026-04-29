"use client";
import { useState } from "react";
import type { TreatmentStage } from "@/types/ortho";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import { CheckCircle, Circle, PlayCircle } from "lucide-react";

const STATUS_CONFIG: Record<string, { label: string; icon: typeof Circle; color: string; bgColor: string; borderColor: string }> = {
  pending:   { label: "لم تبدأ", icon: Circle,      color: "#94a3b8", bgColor: "#ffffff", borderColor: "#e8f0f9" },
  active:    { label: "جارية",   icon: PlayCircle,  color: "#3d7ab5", bgColor: "#3d7ab508", borderColor: "#3d7ab530" },
  completed: { label: "مكتملة",  icon: CheckCircle, color: "#22c55e", bgColor: "#22c55e08", borderColor: "#22c55e30" },
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
    } catch {
    } finally {
      setUpdating(null);
    }
  };

  if (!stages.length) {
    return <p className="text-sm" style={{ color: "#94a3b8" }}>لا توجد مراحل مسجلة</p>;
  }

  // Compute overall progress
  const completedCount = stages.filter(s => s.status === "completed").length;
  const progressPct = stages.length > 0 ? Math.round((completedCount / stages.length) * 100) : 0;

  return (
    <div className="space-y-4">
      {/* Overall progress bar */}
      <div className="flex items-center gap-3">
        <span className="text-xs font-medium" style={{ color: "#64748b" }}>التقدم الكلي</span>
        <div className="flex-1 h-2 rounded-full overflow-hidden" style={{ backgroundColor: "#f1f5f9" }}>
          <div
            className="h-full rounded-full transition-all"
            style={{ width: `${progressPct}%`, background: "linear-gradient(to left, #3d7ab5, #2d5e8e)" }}
          />
        </div>
        <span className="text-xs font-semibold" style={{ color: "#3d7ab5" }}>{progressPct}%</span>
      </div>

      {/* Stages list */}
      <div className="space-y-3">
        {stages.map((stage, idx) => {
          const cfg = STATUS_CONFIG[stage.status] ?? STATUS_CONFIG.pending;
          const Icon = cfg.icon;
          return (
            <div key={stage.id} className="flex items-start gap-4">
              {/* Connector */}
              <div className="flex flex-col items-center flex-shrink-0">
                <Icon
                  className={cn("w-6 h-6", updating === stage.id && "animate-pulse")}
                  style={{ color: cfg.color }}
                />
                {idx < stages.length - 1 && (
                  <div
                    className="w-0.5 h-8 mt-1"
                    style={{ backgroundColor: stage.status === "completed" ? "#22c55e40" : "#e8f0f9" }}
                  />
                )}
              </div>

              {/* Content */}
              <div
                className="flex-1 rounded-xl p-3 border text-sm transition"
                style={{ backgroundColor: cfg.bgColor, borderColor: cfg.borderColor }}
              >
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <span
                    className="font-semibold"
                    style={{ color: cfg.color }}
                  >
                    {stage.stageOrder}. {stage.stageName}
                  </span>
                  <div className="flex items-center gap-2">
                    <span className="text-xs" style={{ color: "#94a3b8" }}>{cfg.label}</span>
                    {stage.status !== "completed" && (
                      <button
                        onClick={() => handleStatusChange(stage, stage.status === "pending" ? "active" : "completed")}
                        disabled={updating === stage.id}
                        className="text-xs px-2.5 py-1 rounded-lg border transition disabled:opacity-50"
                        style={{ borderColor: "#dce8f5", color: "#3d7ab5", backgroundColor: "#ffffff" }}
                      >
                        {stage.status === "pending" ? "بدء" : "إكمال"}
                      </button>
                    )}
                    {stage.status === "completed" && (
                      <span
                        className="text-xs py-0.5 rounded-full font-medium"
                        style={{ padding: "2px 10px", backgroundColor: "#22c55e18", color: "#22c55e" }}
                      >
                        ✓ مكتملة
                      </span>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-4 mt-1 text-xs flex-wrap" style={{ color: "#64748b" }}>
                  {stage.startedAt && <span>بدأت: {stage.startedAt}</span>}
                  {stage.completedAt && <span>اكتملت: {stage.completedAt}</span>}
                  {stage.targetDurationMonths && <span>المدة المتوقعة: {stage.targetDurationMonths} أشهر</span>}
                </div>

                {/* Mini progress for active stage */}
                {stage.status === "active" && (
                  <div className="mt-2 flex items-center gap-2">
                    <div className="flex-1 h-1 rounded-full overflow-hidden" style={{ backgroundColor: "#f1f5f9" }}>
                      <div
                        className="h-full rounded-full"
                        style={{ width: "50%", background: "linear-gradient(to left, #3d7ab5, #2d5e8e)" }}
                      />
                    </div>
                    <span className="text-[10px]" style={{ color: "#3d7ab5" }}>جارية</span>
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
