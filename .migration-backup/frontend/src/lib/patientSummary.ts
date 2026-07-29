// ---------------------------------------------------------------------------
// CORE-PAT-001 — "unknown balance" is NOT "zero balance".
// ---------------------------------------------------------------------------
// The patient profile header shows a financial balance card. It used to read
//   const outstanding = summary?.totalOutstanding ?? 0;
// while the summary request was fetched with `.catch(() => {})`. So whenever
// /api/patients/{id}/summary failed (network blip, 500, permission change), the
// card confidently rendered "0 ر.ي" and the debt badge disappeared — a patient
// who owes money looked fully paid, with no error anywhere on screen.
//
// This module makes the three states explicit and testable: loading, failed,
// and loaded. Only the loaded state may render a number.

export interface PatientSummaryBalance {
  totalOutstanding?: number | null;
}

export type PatientBalanceState = "loading" | "error" | "loaded";

export interface PatientBalanceView {
  state: PatientBalanceState;
  /** The amount to render — null unless the summary actually loaded. */
  outstanding: number | null;
  /** Never true on unknown data, so no false "no debt" and no false debt badge. */
  hasDebt: boolean;
}

/**
 * Decides what the balance card may claim.
 *
 * @param summary the loaded summary, or null when it is not available
 * @param error   the Arabic failure message, or null when there was no failure
 */
export function patientBalanceView(
  summary: PatientSummaryBalance | null | undefined,
  error: string | null | undefined,
): PatientBalanceView {
  if (summary && typeof summary.totalOutstanding === "number") {
    const outstanding = summary.totalOutstanding;
    return { state: "loaded", outstanding, hasDebt: outstanding > 0 };
  }

  // A summary that loaded but carries no outstanding figure is still "unknown":
  // rendering 0 for it would repeat exactly the bug this module exists to stop.
  if (error) return { state: "error", outstanding: null, hasDebt: false };
  return { state: "loading", outstanding: null, hasDebt: false };
}
