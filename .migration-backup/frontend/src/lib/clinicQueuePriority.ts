/**
 * CORE-F-005 (part 1) — Clinic queue emergency-priority reason contract.
 *
 * The backend `ClinicQueueController.ChangePriority` **requires** a non-empty
 * `reason` when the new priority is `Emergency` (returns
 * `400 "الرجاء إدخال سبب الحالة الإسعافية لتوثيق السجل"` otherwise) and writes a
 * formal audit record from it. The queue UI previously PATCHed only `{ priority }`,
 * so choosing "حالة إسعافية" always failed. These helpers make the required-reason
 * rule explicit and testable, and build the exact body the server expects.
 */
export const EMERGENCY_PRIORITY = "Emergency";

export function priorityRequiresReason(priority: string): boolean {
  return priority === EMERGENCY_PRIORITY;
}

export function isEmergencyReasonValid(reason: string | null | undefined): boolean {
  return typeof reason === "string" && reason.trim().length > 0;
}

export interface PriorityChangeBody {
  priority: string;
  reason?: string;
}

/**
 * Builds the PATCH body for `/api/clinic-queue/{id}/priority`. A trimmed `reason`
 * is included only when the priority requires one and a valid reason is present;
 * callers must block the request for Emergency until `isEmergencyReasonValid`.
 */
export function buildPriorityChangeBody(priority: string, reason?: string | null): PriorityChangeBody {
  if (priorityRequiresReason(priority) && isEmergencyReasonValid(reason)) {
    return { priority, reason: (reason as string).trim() };
  }
  return { priority };
}
