# Finance V3 Audit — Aqlan Dental Pro
**التاريخ:** 2026-06-12 · مبني على فحص الكود الفعلي

## Verified-Safe Behaviors (with code evidence)

| Rule | Evidence | Verdict |
|---|---|---|
| Draft invoices excluded from collected revenue & patient balances | `FinanceService.cs:531-537, 947-953`; dashboard filters to `Issued\|\|Paid` in `FinanceV3Controller.Reports.cs:995-998` | ✅ |
| Doctor commission from **actual collections** | `FinanceV3Controller.DoctorCommissions.cs:176-342` — `earned-from-collections` endpoint pro-rates by `paid/total` ratio per invoice | ✅ |
| Lab + material + other direct costs deducted **before** doctor share | `DoctorCommissions.cs:290-314`: `netCommissionable = max(0, collected − lab − material − other)` then `× pct` | ✅ |
| Open cashier shift required for payments | `FinanceService.cs:184-189` (Arabic error) — also enforced for refunds (843-846), commission payouts (`CommissionService.cs:240-245`), cash expense approval, supplier bill payment, lab payable payment | ✅ |
| Refund atomicity | `FinanceService.cs:895-908` — transaction wraps treasury update + journal dual-write + refund payment + cashflow | ✅ |
| Invoice cancellation atomicity | `FinanceV3Controller.cs:259-273` — transaction + `ReverseInvoiceIssuedEntryAsync` | ✅ |
| Contract cancellation atomicity | `FinanceService.cs:416-502` + `ContractCancellationAtomicityTests.cs` | ✅ |
| Commission payout concurrency | `pg_advisory_xact_lock` + in-lock re-check (`CommissionService.cs:258-273`) | ✅ |

There are **two commission views by design**: `/doctor-commissions` = commission **due** (from issued line items), `/doctor-commissions/earned-from-collections` = commission **earned** (from money actually collected). Both deduct direct costs.

## Bugs Found & Fixed in This Sprint

### 1. `RejectExpense` race condition (Medium) — FIXED
`FinanceV3Controller.cs` — approve path used a `FOR UPDATE` row lock inside a transaction, but reject did a plain read-modify-write. A concurrent approve+reject could post an expense to the ledger *and* mark it rejected/soft-deleted.
**Fix:** reject now uses the identical transaction + `SELECT … FOR UPDATE` + in-lock status re-check pattern.

### 2. No treasury balance check on outflows (Medium) — FIXED (configurable)
`TreasuryResolutionService.DecrementTreasuryBalanceAsync` decremented unconditionally — expenses, salaries, supplier bills, lab payables, advances and commission payouts could silently drive a cash drawer negative.
**Fix (production-safe):**
- New Settings key **`finance.prevent_negative_treasury_balance`** (`true`/`1` = enforce).
- **Default = warn-only** (a `LogWarning` is emitted whenever a balance would go negative) so existing deployments with unseeded opening balances are not disrupted.
- When enabled, the outflow is rejected with a clear Arabic message including current balance and required amount.
- Covered by `TreasuryNegativeBalanceGuardTests` (6 scenarios: sufficient, exact-zero, negative under default, negative under enforce, false/0/empty values).

**To enable in production:** insert/update Settings row `finance.prevent_negative_treasury_balance = true` (Category: `finance`) once opening balances are confirmed.

## Re-verified (audit suspicions that turned out already handled)
- **Draft invoice duplication** (PatientJourney `create-draft-invoice`): double-checked — has fast-path check *and* in-transaction re-check under advisory lock (`PatientJourneyController.cs:1695-1764`). No fix needed.
- **Overdue calc null safety**: already fixed via `GetValueOrDefault()` (`Reports.cs:341-348`).
- **Commission earned sum null-cast**: safe (`(decimal?)` cast + `?? 0m`).

## Remaining Recommendations (next sprints)
1. Tests for refund ↔ commission recalculation interaction and partial refunds.
2. Consider surfacing the treasury guard setting in the Settings UI (currently DB-level).
3. Document the two commission endpoints' semantics in API docs/UI labels.
