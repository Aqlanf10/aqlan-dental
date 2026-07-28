# 006 Finance Design

- Frontend owner: `frontend/src/app/(dashboard)/finance-v3/`.
- Backend owners: `FinanceV3Controller*.cs`, finance-related controllers.
- Services: `FinanceService`, `FinanceSettingsReader`, `FinanceClinicIdentity`, `TreasuryResolutionService`, `JournalEntryService`, `CommissionService`.
- Entities: `Invoice`, `InvoiceLineItem`, `Payment`, `Contract`, `CashierSession`, `Treasury`, `VaultTransfer`, `SupplierBill`, `CreditNote`, `JournalEntry`, `JournalLine`, `DoctorCommissionPayment`.
- Permissions: `ReportsAccess`, `FinanceAccess`, `FinanceWrite`, `CashierAccess`, `AdminOnly`, commission policies.
- Tests: `backend/tests/AqlanDentalPro.UnitTests/Finance/`, Commissions, Invoices, Services.
- Finance list surfaces use an explicit fetch-error state; a failed refresh keeps the last successful rows visible beside the retry action.

Allowed files: finance owners, finance tests, specs.

Forbidden files: migrations unless approved, unrelated UI modules, route renames, production settings.

Rollback: revert entire finance behavior change and any spec updates together.

---

## CORE-LAB — Lab Order Financial Linkage (PR #778)

### Problem

The SupplierBill + LabPayable + journal-entry linkage lived ONLY inline inside
`LabOrdersController.Create`, gated on "has a lab AND cost > 0".
`UpdateLabOrderRequest` carried no Cost/Currency/ExchangeRateToYer at all, and
`Update` had no equivalent linkage code. A draft saved without a lab or cost —
which the create modal allows — could therefore never become financially real.

### Design

`LabOrderFinanceSyncService` (Infrastructure/Services) owns the linkage:

- **Idempotency key:** the single `SupplierBill` whose `LabOrderId` is this order.
  Absent → create trail. Present → reconcile it.
- **Does NOT call SaveChanges.** The caller owns the transaction so the order and
  its financial trail commit atomically; a failure cannot leave a half-linked row.
- **Concurrency:** a transaction-scoped `pg_advisory_xact_lock` keyed on the order
  via `StableLockKeyHelper.StableGuidToLong` is taken BEFORE the existence check.
  The pre-existing BillNumber lock only serialises numbering, which happens after
  the check, so it could not prevent a duplicate bill.
- **Ledger corrections:** posted entries are reversed via `CreateReversalEntryAsync`
  and re-posted, never mutated in place.
- **Currency:** only a YER bill moves `Supplier.Balance`; changes apply the net
  delta by unwinding the old contribution first.

Callers: `LabOrdersController.Update` and the transition into `sent`. The `sent`
call is a safety net — idempotent, so it is a no-op when the trail already exists.

`Create` still holds its own inline copy. It works and is covered by tests, and
consolidating it was judged unjustified risk without a local compiler. Tracked as
follow-up; the two paths must not diverge.

