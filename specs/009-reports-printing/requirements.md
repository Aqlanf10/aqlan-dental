# 009 Reports Printing Requirements

## Current State

Evidence: `ReportsController.cs`, `OperationalReportsController.cs`, `PdfService.cs`,
`FinanceClinicIdentity.cs`, specialized PDF generators and `frontend/src/app/(dashboard)/reports/`.

- `REP-REQ-001`: Reports and PDFs SHALL use Arabic-capable rendering.
- `REP-REQ-002`: Clinic identity SHALL come from Settings or approved fallback helpers.
- `REP-REQ-003`: Finance receipts/invoices SHALL use `FinanceClinicIdentity`.
- `REP-REQ-004`: Clinical reports SHALL not claim unreviewed AI diagnosis.
- `REP-REQ-005`: Reports access SHALL respect `ReportsAccess` or module-specific policies.

## Target State

Consistent Arabic PDFs and reports with correct clinic identity, permissions and explicit currencies.

## Acceptance Criteria

- WHEN a PDF/report is created THEN Settings-backed identity SHALL be used.
- WHEN report output changes THEN tests or visual runtime verification SHALL be documented.
- PDF layout changes need runtime visual verification.

---

## REP-OPS — Operational patient-level reports

### Evidence

The original dashboard was aggregate-first. `overdue-contracts` already returned
patient names, but there was no consistent patient-row reporting surface. The
financial endpoint mixed physical currencies and the frontend mapped most summary
export buttons to routes that did not exist. Corrected analysis:
`docs/audits/REPORTS_MODULE_ANALYSIS_2026-07-28.md`.

### Requirements

- `REP-OPS-REQ-001`: Operational reports SHALL return paginated patient-level rows,
  not only aggregates.
- `REP-OPS-REQ-002`: Every operational report SHALL require `ReportsAccess`. Admin
  receives a consolidated view; a non-admin report user SHALL be restricted to
  their assigned branch and a branchless non-admin SHALL receive 403. Doctors are
  not members of `ReportsAccess` and SHALL NOT receive this PHI surface unless a
  separate product decision and patient-access policy are implemented.
- `REP-OPS-REQ-003`: "today" and "this month" SHALL derive from
  `ClinicTimeProvider.ClinicToday()`; UTC timestamps SHALL use clinic-local UTC
  boundaries and the frontend SHALL use `localDateString()`.
- `REP-OPS-REQ-004`: Monetary output SHALL keep YER/SAR/USD separate or convert at
  the rate stored on the document. Currencies SHALL NOT be summed blindly.
- `REP-OPS-REQ-005`: A failed load SHALL NOT render as "no results".
- `REP-OPS-REQ-006`: Every row-returning endpoint SHALL be paginated.
- `REP-OPS-REQ-007`: The operational layer SHALL be additive under
  `/api/reports/operations/*`. Existing financial output may be extended with
  explicit currency fields while retaining YER-only legacy scalars for compatibility.

### Acceptance Criteria

- WHEN a user without `ReportsAccess` requests a report THEN policy SHALL deny it.
- WHEN a non-admin report user has a branch THEN only that branch's rows SHALL return.
- WHEN an outstanding-balance report runs THEN every row SHALL carry patient,
  total, paid, remaining and currency.
- WHEN "today" is requested near midnight Yemen time THEN clinic-day boundaries
  SHALL be used and proven by a deterministic test.
- WHEN rows exceed one page THEN the response SHALL be paginated.
- CSV export SHALL use the same columns and branch scope as the screen.
