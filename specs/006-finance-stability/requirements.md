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

---

## CORE-LAB — Lab Order Financial Linkage (PR #778)

### FIN-LAB-REQ-001 — the financial trail follows the order, not just its creation
- WHEN a lab order acquires BOTH an active lab AND a cost greater than zero — at
  creation OR at any later update — THEN the system SHALL ensure exactly one
  SupplierBill, one LabPayable and a posted journal entry exist for that order.
- WHEN the same order is saved repeatedly THEN the system SHALL converge on ONE
  bill and ONE payable and SHALL NOT create duplicates.
- WHEN a lab order has no lab or no cost THEN no financial trail SHALL be created;
  this is a legitimate draft state, not an error.

### FIN-LAB-REQ-002 — corrections are auditable, not silent rewrites
- WHEN the cost, currency, rate or lab of an UNPAID lab order changes THEN the
  system SHALL update the existing bill and payable, and SHALL reverse the posted
  journal entry and post a corrected one rather than editing it in place.
- WHEN a payment already exists against the payable THEN the system SHALL REFUSE a
  cost or currency change with an Arabic message, rather than rewriting the bill
  out of step with the ledger.

### FIN-LAB-REQ-003 — currencies are never summed
- Supplier.Balance is denominated in YER only. WHEN a lab bill is in a currency
  other than YER THEN it SHALL NOT move Supplier.Balance; the amount and its
  agreed rate SHALL remain on the bill.
- WHEN a lab cost changes THEN the supplier balance SHALL move by the NET delta,
  never by the accumulated sum of every edit.
- Supported currencies are YER, SAR and USD. A non-YER cost without a positive
  ExchangeRateToYer SHALL be rejected in Arabic.

### FIN-LAB-REQ-004 — no unusable financial rows
- WHEN a branch cannot be resolved for the order THEN the system SHALL REFUSE to
  write the bill rather than persist Guid.Empty in the non-nullable
  SupplierBill.BranchId, which would belong to no branch and vanish from every
  branch-scoped report.

### FIN-LAB-REQ-005 — concurrent completion is serialised
- WHEN two requests complete the same lab order concurrently THEN at most ONE
  SupplierBill SHALL exist for that LabOrderId. The check-then-create SHALL be
  serialised by a transaction-scoped advisory lock keyed on the order, using a
  process-stable key (never Guid.GetHashCode()).

### FIN-LAB-REQ-006 — commission separation is preserved
- The accrued/earned commission duality SHALL remain unchanged. CommissionService
  reads the lab cost from LabOrder.TotalCost ?? Cost directly; this work only
  ensures that value can actually be entered, and does not alter deduction rules.

## Acceptance Criteria — CORE-LAB

- WHEN a draft is completed with a lab and a cost THEN exactly one bill and one
  payable SHALL exist, verified by test.
- WHEN Update runs three times with the same values THEN the bill and payable
  counts SHALL remain 1.
- WHEN the cost changes from 5000 to 8000 THEN the supplier balance SHALL be 8000,
  not 13000.
- WHEN a SAR bill is created THEN Supplier.Balance SHALL remain unchanged.
- WHEN a payable has PaidAmount > 0 THEN a cost change SHALL return an Arabic 400
  and the bill SHALL be unmodified.
- WHEN no branch resolves THEN neither bill nor payable SHALL be created.

