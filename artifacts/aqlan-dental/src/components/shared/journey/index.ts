/**
 * Shared Journey Module — Single Source of Truth.
 *
 * Re-exports all shared constants, types, and UI components
 * used by both /patient-journey and /daily-operations pages.
 */

// Constants & helpers
export * from "./constants";

// Types
export * from "./types";

// UI Components
export { StatusBadge } from "./StatusBadge";
export { NextActionBadge } from "./NextActionBadge";
export { JourneyStepProgressBar } from "./JourneyStepProgressBar";
export { ConfirmDialog } from "./ConfirmDialog";
export { BookAppointmentModal } from "./BookAppointmentModal";
