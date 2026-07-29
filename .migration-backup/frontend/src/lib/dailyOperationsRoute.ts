/**
 * Query-string contract for the canonical daily-operations workspace.
 * Keep this separate from the page so every route that links to a module
 * resolves the same, safe allow-list of tabs.
 */
export const DAILY_OPERATIONS_MODULE_TABS = [
  "appointments",
  "journey",
  "queue",
  "rooms",
  "checkout",
  "booking",
  "lab",
  "report",
] as const;

export type DailyOperationsModuleTab = (typeof DAILY_OPERATIONS_MODULE_TABS)[number];

export function getDailyOperationsModuleTab(
  value: string | null,
): DailyOperationsModuleTab | null {
  return DAILY_OPERATIONS_MODULE_TABS.includes(value as DailyOperationsModuleTab)
    ? (value as DailyOperationsModuleTab)
    : null;
}
