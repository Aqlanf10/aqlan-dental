# 009 Reports Printing Requirements

## Current State

Evidence: `ReportsController.cs`, `PdfService.cs`, `FinanceClinicIdentity.cs`, specialized PDF generators, `frontend/src/app/(dashboard)/reports/`, patient print routes, finance/ceph/ortho/lab PDF tests.

- `REP-REQ-001`: Reports and PDFs SHALL use Arabic-capable rendering.
- `REP-REQ-002`: Clinic identity SHALL come from Settings or approved fallback helpers.
- `REP-REQ-003`: Finance receipts/invoices SHALL use `FinanceClinicIdentity`.
- `REP-REQ-004`: Clinical reports SHALL not claim unreviewed AI diagnosis.
- `REP-REQ-005`: Reports access SHALL respect `ReportsAccess` or module-specific policies.

## Target State

Consistent Arabic PDFs and reports with correct clinic identity and permissions.

## Risks

Hardcoded clinic text, broken Arabic shaping, wrong finance document identity, clinical overclaim.

## Allowed Future Work

Improve report layouts, add tests, align headers/footers, add settings-backed fields.

## Forbidden Future Work

Hardcoded owner identity in new reports, bypassing report permissions, fake clinical claims.

## Acceptance Criteria

- WHEN a PDF/report is created THEN Settings-backed identity SHALL be used.
- WHEN report output changes THEN tests or visual runtime verification SHALL be documented.
- Needs runtime verification for visual layout.

---

## REP-OPS — Operational (patient-level) Reports — owner directive 2026-07-28

### Evidence

Every analytical endpoint in `ReportsController` aggregates. A programmatic sweep
for `PatientName` / `PatientNumber` / `FirstName` across `center-summary`,
`financial`, `patient-retention`, `treatment-plan-completion`, `overdue-contracts`
and `ortho-progress` returns **zero** matches. The module answers "how many" and
never "which patients". `export/patients` is an unfiltered CSV dump of everyone,
not a report. Full analysis: `docs/audits/REPORTS_MODULE_ANALYSIS_2026-07-28.md`.

### Requirements

- `REP-OPS-REQ-001`: Operational reports SHALL return PAGINATED PATIENT-LEVEL ROWS
  (identifier, name, contact where relevant), not only aggregates.
- `REP-OPS-REQ-002`: **Every** operational report SHALL filter rows by the caller's
  accessible patients. A restricted doctor SHALL see only their own patients.
  Reports returning names are a PHI surface; the same gap was a confirmed P1 three
  times in this repository (CORE-APPT-001, CORE-PAT-006, CORE-LAB-006).
- `REP-OPS-REQ-003`: "today" / "this month" SHALL derive from
  `ClinicTimeProvider.ClinicToday()`, never the host clock; the frontend SHALL use
  `localDateString()`.
- `REP-OPS-REQ-004`: Monetary output SHALL keep YER/SAR/USD separate or convert at
  the rate stored on the document. Currencies SHALL NOT be summed blindly.
- `REP-OPS-REQ-005`: A failed load SHALL NOT render as "no results".
- `REP-OPS-REQ-006`: Every row-returning endpoint SHALL be paginated.
- `REP-OPS-REQ-007`: The existing analytical endpoints SHALL remain unchanged; the
  operational layer is additive under `/api/reports/operational/*`.

### Acceptance Criteria

- WHEN a restricted doctor requests any operational report THEN only their own
  patients' rows SHALL be returned, proven by a regression test per endpoint.
- WHEN an outstanding-balance report runs THEN each row SHALL carry the patient,
  total, paid and remaining amounts with currency preserved.
- WHEN "today" is requested near midnight Yemen time THEN the clinic day SHALL be
  used, proven by a deterministic test.
- WHEN a report would return more than one page THEN it SHALL page rather than
  return an unbounded set.
- No operational report SHALL merge without its patient-access test.

