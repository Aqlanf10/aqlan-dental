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

### FIN-LAB-REQ-001 — the financial trail begins when the order is sent
- WHEN a lab order enters `sent` with BOTH an active lab AND a cost greater than
  zero THEN the system SHALL ensure exactly one SupplierBill, one LabPayable and
  a posted journal entry exist for that order.
- WHEN the same sent order is saved repeatedly THEN the system SHALL converge on
  ONE bill and ONE payable and SHALL NOT create duplicates.
- A draft SHALL NOT create a bill, payable, journal entry or supplier balance,
  even after its lab and cost are filled. Draft data becomes financially real
  only when the order is sent.

### FIN-LAB-REQ-002 — corrections are auditable, not silent rewrites
- WHEN the cost, currency, rate or lab of an UNPAID lab order changes THEN the
  system SHALL update the existing bill and payable, and SHALL reverse the posted
  journal entry and post a corrected one rather than editing it in place.
- WHEN a payment already exists against the payable THEN the system SHALL REFUSE
  changing the cost, currency, exchange rate or lab with an Arabic message,
  rather than rewriting the bill out of step with the ledger.
- WHEN an unpaid sent order is cancelled or deleted THEN the system SHALL reverse
  its posted journal entry, cancel/deactivate its bill and payable, and unwind
  its YER supplier-balance contribution atomically.
- WHEN a paid or partially-paid sent order is cancelled or deleted THEN the
  system SHALL refuse the action and preserve both the order and financial trail.

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

### FIN-LAB-REQ-007 — a paid commission is corrected, never rewritten
- WHEN the actual lab cost behind a commission changes AND that commission is not
  yet Paid THEN the line item SHALL be recalculated in place.
- WHEN the commission is already Paid THEN the line item SHALL NOT be modified in
  any way. A separate signed `DoctorCommissionAdjustment` SHALL be raised instead,
  referencing the lab order, the invoice and the original commission line, and
  SHALL join the doctor's next settlement.
- Re-running the correction SHALL converge, not accumulate: the amount raised is
  measured against the corrections already recorded for that line item, so a
  repeat run raises nothing and successive cost changes close the gap exactly once.
- A correction SHALL count toward the doctor's payable balance from the moment it
  is raised. `Settled` records which disbursement carried it and SHALL NOT be used
  as an arithmetic gate; only `Cancelled` removes a correction from the balance.

### FIN-LAB-REQ-008 — a draft is not a decision; a cancellation is
- A lab order in `draft` SHALL neither be deducted (the clinic has not committed
  to the cost) nor forced to zero (which would discard the service's own estimate).
  The line's existing LabCost SHALL be left untouched.
- This is sound because the status table permits only `draft → sent | cancelled`,
  so `draft` is always the initial state and never a retreat from a committed one.
- A `cancelled` or soft-deleted order SHALL release its deduction to zero, because
  a cost nobody owes must not keep reducing the doctor's share.

### FIN-LAB-REQ-009 — lab cost is converted into the invoice's currency
- WHEN the lab order and the invoice share a currency THEN the cost SHALL be used
  as-is.
- WHEN the invoice is in YER and the lab order is not THEN the cost SHALL be
  converted using the order's ExchangeRateToYer.
- WHEN the invoice and the lab order are in two different non-YER currencies, or
  the required rate is missing or non-positive, THEN no deduction SHALL be written.
  The line SHALL be left exactly as it was and reported for manual resolution —
  never converted by a guessed rate.

## Acceptance Criteria — CORE-LAB

- WHEN a draft is completed with a lab and a cost THEN no bill SHALL exist until
  it is sent; after sending exactly one bill and one payable SHALL exist.
- WHEN Update runs three times with the same values THEN the bill and payable
  counts SHALL remain 1.
- WHEN the cost changes from 5000 to 8000 THEN the supplier balance SHALL be 8000,
  not 13000.
- WHEN a SAR bill is created THEN Supplier.Balance SHALL remain unchanged.
- WHEN a payable has PaidAmount > 0 THEN changing cost, lab, currency or rate,
  cancelling, or deleting SHALL return an Arabic 400 and leave the trail unmodified.
- WHEN an unpaid sent order is cancelled THEN its bill and payable SHALL be
  inactive/cancelled and its YER supplier balance contribution SHALL be removed.
- WHEN the list endpoint returns a SAR/USD order THEN it SHALL include labId,
  cost/totalCost, currency and ExchangeRateToYer so the editor can round-trip it.
- WHEN no branch resolves THEN neither bill nor payable SHALL be created.

## Acceptance Criteria — CORE-FIN-LAB-ADJ

- WHEN an Approved (unpaid) commission's lab order moves from 20,000 to 35,000 on a
  100,000 service at 40% THEN the line SHALL read LabCost 35,000 and commission
  26,000, and NO adjustment row SHALL exist.
- WHEN the same change lands on a Paid commission THEN the line SHALL still read
  LabCost 20,000 and commission 32,000, and exactly one adjustment of −6,000 SHALL
  exist carrying both figures and both lab costs.
- WHEN the resync runs a second time with nothing changed THEN it SHALL raise
  nothing and the adjustment count SHALL remain 1.
- WHEN the cost then moves to 30,000 THEN a second adjustment of +2,000 SHALL be
  raised and the adjustments SHALL sum to −4,000, not −10,000.
- WHEN a lab order behind a Paid commission is cancelled THEN an adjustment of the
  full deduction SHALL be raised in the doctor's favour.
- WHEN a doctor carries a +1,500 correction THEN a 4,500 payment SHALL be accepted
  where 3,000 was accrued; with a −1,000 correction a 3,000 payment SHALL be refused.
- WHEN a payment is recorded THEN open corrections SHALL be stamped with it AND
  SHALL still count toward the balance, so the same payment cannot be made twice.
- WHEN a correction is cancelled THEN it SHALL leave the balance and the resync
  SHALL be free to raise the difference again.
- WHEN the backfill sweep runs THEN it SHALL touch only line items that already
  carry a lab-order link, and SHALL report every record it left untouched.

