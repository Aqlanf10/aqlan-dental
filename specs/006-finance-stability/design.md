# 006 Finance Design

- Frontend owner: `frontend/src/app/(dashboard)/finance-v3/`.
- Backend owners: `FinanceV3Controller*.cs`, finance-related controllers.
- Services: `FinanceService`, `FinanceSettingsReader`, `FinanceClinicIdentity`, `TreasuryResolutionService`, `JournalEntryService`, `CommissionService`.
- Entities: `Invoice`, `InvoiceLineItem`, `Payment`, `Contract`, `CashierSession`, `Treasury`, `VaultTransfer`, `SupplierBill`, `CreditNote`, `JournalEntry`, `JournalLine`, `DoctorCommissionPayment`.
- Permissions: `ReportsAccess`, `FinanceAccess`, `FinanceWrite`, `CashierAccess`, `AdminOnly`, commission policies.
- Tests: `backend/tests/AqlanDentalPro.UnitTests/Finance/`, Commissions, Invoices, Services.

Allowed files: finance owners, finance tests, specs.

Forbidden files: migrations unless approved, unrelated UI modules, route renames, production settings.

Rollback: revert entire finance behavior change and any spec updates together.
