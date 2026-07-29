// ---------------------------------------------------------------------------
// "Latest cephalometric analysis" selection — must match the backend deck.
// ---------------------------------------------------------------------------
// The presentation generator (OrthoCasePresentationService) picks the analysis
// to render with: AnalysisDate DESC, then CreatedAt DESC. The UI's readiness /
// status must select the SAME record, otherwise — when two analyses share an
// AnalysisDate (same calendar day) — the UI could show readiness for one while
// the generated deck uses another. This helper applies that exact rule.

/** Minimal shape needed to order analyses the way the deck generator does. */
export interface CephSelectable {
  id: string;
  /** Date-only, e.g. "2026-06-17" (the list DTO serializes it day-precision). */
  analysisDate: string;
  /** ISO creation timestamp; the deterministic tiebreaker. */
  createdAt?: string;
}

/**
 * Orders analyses newest-first by (analysisDate DESC, createdAt DESC). Both
 * fields compare lexicographically because their formats are
 * chronologically-sortable strings (yyyy-MM-dd and ISO-8601). When createdAt is
 * unavailable on both sides the original order is preserved (stable sort), so a
 * server already returning this order stays intact.
 */
export function sortCephByLatest<T extends CephSelectable>(list: readonly T[]): T[] {
  return [...list].sort((a, b) => {
    const byDate = (b.analysisDate ?? "").localeCompare(a.analysisDate ?? "");
    if (byDate !== 0) return byDate;
    const ca = a.createdAt ?? "";
    const cb = b.createdAt ?? "";
    if (ca && cb) return cb.localeCompare(ca);
    return 0; // stable fallback when createdAt is missing
  });
}

/** The single analysis the deck generator would treat as "latest", or undefined. */
export function pickLatestCeph<T extends CephSelectable>(list: readonly T[] | undefined): T | undefined {
  if (!list || list.length === 0) return undefined;
  return sortCephByLatest(list)[0];
}
