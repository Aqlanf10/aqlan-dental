
import { JOURNEY_STEPS, getStepIndex } from "./constants";

interface JourneyStepProgressBarProps {
  journeyStep: string;
  variant?: "dark" | "light";
  className?: string;
}

/**
 * Unified journey step progress bar.
 * Shows the patient's current position in the journey workflow.
 * Used by both /patient-journey and /daily-operations pages.
 */
export function JourneyStepProgressBar({
  journeyStep,
  variant = "dark",
  className = "",
}: JourneyStepProgressBarProps) {
  const currentIdx = getStepIndex(journeyStep);

  const steps = JOURNEY_STEPS.filter((s) => s.key !== "Confirmed"); // Confirmed is rarely shown

  return (
    <div className={`flex items-center gap-0.5 ${className}`}>
      {steps.map((step, idx) => {
        const stepIdx = getStepIndex(step.key);
        const isDone = currentIdx >= 0 && stepIdx >= 0 && stepIdx < currentIdx;
        const isCurrent = stepIdx === currentIdx;

        const dotColor =
          variant === "dark"
            ? isDone
              ? "bg-emerald-400"
              : isCurrent
                ? "bg-amber-400"
                : "bg-white/30"
            : isDone
              ? "bg-emerald-500"
              : isCurrent
                ? "bg-amber-500"
                : "bg-gray-200";

        const lineColor =
          variant === "dark"
            ? isDone
              ? "bg-emerald-400"
              : "bg-white/20"
            : isDone
              ? "bg-emerald-300"
              : "bg-gray-200";

        return (
          <div key={step.key} className="flex items-center">
            <div
              className={`w-2 h-2 rounded-full transition-colors ${dotColor}`}
              title={step.label}
            />
            {idx < steps.length - 1 && (
              <div className={`w-3 h-0.5 ${lineColor}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}
