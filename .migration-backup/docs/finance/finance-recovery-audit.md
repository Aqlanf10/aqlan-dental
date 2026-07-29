# Finance Recovery Audit

## Objective

Make Finance V3 the clinic's operational accounting workspace: one traceable
financial event creates its source document, ledger entry, treasury movement,
audit record, and printable voucher without mixing currencies.

## Non-negotiable rules

1. Source amounts are never added across `YER`, `SAR`, and `USD`.
2. Every non-YER event stores the exchange rate used at posting time; settings
   may suggest a rate but may not rewrite historical events.
3. A posted entry is corrected by a reversal, never by editing or deleting it.
4. Every cash outflow has a selected, same-currency treasury and a disbursement
   voucher linked to its journal entry.
5. A fiscal-period close blocks ordinary posting and creates explicit opening
   balances for the following period.

## Current findings

| Area | Finding | Recovery action |
| --- | --- | --- |
| Supplier and lab payables | Three overlapping models exist: `SupplierBills`, Finance V3 supplier endpoints, and `LabPayables`. | Make `SupplierBill` the Finance V3 payable source; map lab orders to it and retire duplicate write paths. |
| Supplier balances | Legacy `Supplier.Balance` is a scalar and cannot represent more than one currency. | Read balances grouped by currency; leave the scalar YER-only until legacy consumers are migrated. |
| Currency | Treasuries and patient payments have partial currency support, but supplier bills, supplier payments, and journal entries did not retain currency or rate snapshots. | Add source currency and immutable YER rate snapshots to the payable workflow. |
| Journals | The journal tab is read-only and lacks a controlled manual-journal workflow. | Add draft, validation, approval/posting, reversal, and document links. |
| Vouchers | The generic journal voucher endpoint exists but is hidden from the supplier payment journey. | Return the journal entry from supplier payment and download its disbursement voucher directly. |
| Fiscal close | Cashier shift close exists; fiscal periods and year-end close do not. | Add fiscal periods, a close checklist, carry-forward entries, and blocked-period guards. |
| Reporting | Several summary queries sum journal lines without a currency dimension. | Group source reports by currency and add base-currency equivalents only from stored rate snapshots. |

## Delivery order

1. Currency-safe supplier/lab payables, historical opening balances, payment,
   journal link, and voucher.
2. Consolidate lab-order liabilities into the same payable ledger and surface
   them from the lab and treatment workflows.
3. Manual journal workflow with balanced lines, approval, posting, reversal,
   and source/voucher links.
4. Fiscal period setup, close, carry-forward, and posting guards.
5. Currency-safe patient, expense, treasury, P&L, and dashboard reporting.
