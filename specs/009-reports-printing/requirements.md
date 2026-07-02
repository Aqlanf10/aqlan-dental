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
