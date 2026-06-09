/**
 * Patient Journey — Page-specific constants and helpers.
 *
 * Shared constants and types are imported from:
 *   @/components/shared/journey/constants  (labels, colors, helpers)
 *   @/components/shared/journey/types       (TodayJourneyItem, etc.)
 *
 * This file re-exports shared items for convenience and adds
 * page-specific items only used by /patient-journey.
 */

// ─── Re-export shared types ───────────────────────────────────────────────
export type { TodayJourneyItem as JourneyItem, TodayJourneyItem, ServiceOption, RoomOption } from "@/components/shared/journey/types";

// ─── Re-export shared constants & helpers ─────────────────────────────────
export {
  JOURNEY_STEPS,
  STEP_ORDER,
  getStepIndex,
  APPOINTMENT_STATUS_ARABIC as STATUS_LABELS,
  NEXT_ACTION_ARABIC as ACTION_LABELS,
  STATUS_COLORS_TAILWIND as STATUS_COLORS,
  ACTION_COLORS,
  PAYMENT_METHODS,
  SEVERITY_STYLES,
  TIMELINE_DOT_COLORS,
  fmtRial,
  fmtDate,
  fmtTime,
  getInitials,
  inputCls,
  isDoctorRole,
  isReceptionRole,
  isAccountantRole,
  APPOINTMENT_STATUS_ARABIC,
  NEXT_ACTION_ARABIC,
  CHECKOUT_STATUS_ARABIC,
} from "@/components/shared/journey/constants";

// ─── Page-specific: Step status helper ────────────────────────────────────

export const STEP_ORDER_MAP: Record<string, number> = {
  Scheduled: 0,
  Arrived: 1,
  Waiting: 2,
  Called: 3,
  InRoom: 4,
  InProgress: 5,
  Handoff: 6,
  Checkout: 7,
  Completed: 8,
};

export function getStepStatus(stepKey: string, currentStep: string): "done" | "current" | "pending" {
  const currentIdx = STEP_ORDER_MAP[currentStep] ?? -1;
  const stepIdx = STEP_ORDER_MAP[stepKey] ?? -1;
  if (currentIdx < 0 || stepIdx < 0) return "pending";
  if (stepIdx < currentIdx) return "done";
  if (stepIdx === currentIdx) return "current";
  return "pending";
}
