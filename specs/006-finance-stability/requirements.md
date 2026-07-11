# 006 Finance Stability Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/finance-v3/`, `FinanceV3Controller*.cs`, `ContractsController.cs`, `InvoicesController.cs`, `PaymentsController.cs`, `CashierSessionsController.cs`, `FinanceService.cs`, `TreasuryResolutionService.cs`, `FinanceSettingsKeys.cs`, finance tests.

- `FIN-REQ-001`: `/finance-v3` SHALL be the canonical finance workspace.
- `FIN-REQ-002`: Finance writes SHALL require `FinanceWrite`, `AdminOnly`, or the exact existing finance policy.
- `FIN-REQ-003`: Payments/refunds/commission payouts SHALL respect cashier session rules.
- `FIN-REQ-004`: Treasury outflows SHALL use `TreasuryResolutionService` and settings such as `finance.prevent_negative_treasury_balance`.
- `FIN-REQ-005`: Finance settings SHALL be read from Settings, not hardcoded.
- `FIN-REQ-006`: Finance PDFs SHALL use `FinanceClinicIdentity`.
- `FIN-REQ-007`: Finance data-fetch failures SHALL render a visible error and retry action, SHALL NOT render the successful empty state, and SHALL preserve previously loaded data during a failed refresh.

## Target State

Stable, auditable finance with no silent behavior changes.

## Risks

Money loss, double posting, bad refunds, wrong commissions, negative treasury, permission bypass.

## Allowed Future Work

Bug fixes, tests, report clarity, UI cleanup, settings-backed improvements.

## Forbidden Future Work

Cheap model finance edits, new finance module, bypassing settings, deleting tests, casual migrations.

## Acceptance Criteria

- WHEN finance logic changes THEN relevant finance tests SHALL be run or updated.
- WHEN a finance setting exists THEN code SHALL use it.
- WHEN risk is unclear THEN stop and write a report.
