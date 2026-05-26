# Finance V3 — Foundation Specification

> **Status:** DRAFT — Phase 1 (Audit & Foundation)  
> **Last Updated:** 2026-05-26  
> **Owner:** Aqlan Dental Clinic  
> **Review:** Required before Phase 2 implementation

---

## Table of Contents

1. [Purpose and Scope](#1-purpose-and-scope)
2. [Accounting Source of Truth](#2-accounting-source-of-truth)
3. [Core Objects](#3-core-objects)
4. [Required Formulas](#4-required-formulas)
5. [Reception Checkout Integration](#5-reception-checkout-integration)
6. [Future Posting Rules](#6-future-posting-rules)
7. [Roles and Permissions](#7-roles-and-permissions)
8. [Confirmed Defects in Current Implementation](#8-confirmed-defects-in-current-implementation)
9. [Legacy Inventory](#9-legacy-inventory)
10. [Migration / Reset Plan](#10-migration--reset-plan)
11. [Transition Risk](#11-transition-risk)
12. [Phase Roadmap](#12-phase-roadmap)

---

## 1. Purpose and Scope

The current Finance module (V1 + V2) is architecturally fragmented, contains critical data integrity defects, and provides an unreliable user experience. This document defines the foundation for a complete Finance V3 rebuild.

**Scope:**
- Define the accounting principles that will govern all financial operations.
- Specify the core financial objects and their relationships.
- Document the formulas that must be correctly implemented.
- Define role-based access control for financial data.
- Catalog all existing finance surfaces, APIs, entities, and defects.
- Plan the migration from experimental/test finance data to a clean production system.

**Out of Scope (Phase 1):**
- Writing new backend logic or database models.
- Modifying existing API contracts (only documentation and navigation changes).
- Deleting any data or tables.
- Changing auth, patient, clinical, portal, or messaging modules.

---

## 2. Accounting Source of Truth

### 2.1 Final Architecture: JournalEntry + JournalLine (Double-Entry)

The **final accounting source of truth** for Finance V3 is the `JournalEntry` + `JournalLine` model. This is a proper double-entry bookkeeping architecture where every financial event produces a balanced journal entry with at least two lines (debits and credits) that sum to zero.

**Why double-entry:**
- A single-table ledger (`CashFlowTransaction` alone) cannot guarantee that every inflow has a corresponding outflow or liability. This leads to phantom balances, unlinked payments, and reconciliation failures — all of which are present in the current system.
- Double-entry enforces the fundamental accounting equation: `Assets = Liabilities + Equity`. Every transaction must balance before it is posted.
- Audit trail is built into the structure: each `JournalEntry` links to a `FinancialDocument` (Payment, Expense, Invoice, etc.), and each `JournalLine` references an account (Treasury, Receivable, Payable, Revenue, Expense).

**Model structure:**

```
FinancialDocument (Payment, Expense, Invoice, etc.)
  └── JournalEntry (one per document, atomic posting unit)
        ├── JournalLine (debit: Treasury  +500)
        └── JournalLine (credit: Revenue  +500)
```

**JournalEntry fields:**

| Field | Purpose |
|-------|---------|
| `Id` | Primary key (Guid, never Guid.Empty) |
| `EntryNumber` | Unique sequential identifier (e.g., `JE-20260526-001`) |
| `FinancialDocumentId` | FK to the source document (Payment, Expense, etc.) |
| `FinancialDocumentType` | Discriminator: `Payment`, `Expense`, `Salary`, `Advance`, `Commission`, `Transfer`, `DrawerOpen`, `DrawerClose`, `SupplierPayment`, `Refund` |
| `Description` | Human-readable description of the transaction |
| `EntryDate` | Date of the financial event |
| `BranchId` | **Required** — never `Guid.Empty` |
| `CashierSessionId` | Link to the active cashier session (null only for non-session events) |
| `TreasuryId` | Primary treasury affected |
| `PerformedBy` | UserId of the person who created this entry |
| `IsReversal` | `true` if this is a reversal entry |
| `ReversalOfEntryId` | Points to the original entry (if this is a reversal) |
| `ReversedByEntryId` | Points to the reversal entry (if this was reversed) |
| `IsPosted` | `true` once the entry has been validated and balances confirmed |
| `CreatedAt` | Timestamp of creation |

**JournalLine fields:**

| Field | Purpose |
|-------|---------|
| `Id` | Primary key (Guid, never Guid.Empty) |
| `JournalEntryId` | FK to the parent JournalEntry |
| `AccountType` | `Treasury`, `Receivable`, `Payable`, `Revenue`, `Expense`, `Equity`, `ContraRevenue`, `ContraExpense` |
| `AccountId` | FK to the specific account instance (TreasuryId, PatientId, SupplierId, etc.) |
| `Debit` | Debit amount (zero if credit) |
| `Credit` | Credit amount (zero if debit) |
| `Description` | Line-level description |
| `BranchId` | **Required** — never `Guid.Empty` |

**Balancing invariant:**
```
SUM(JournalLine.Debit) = SUM(JournalLine.Credit)   — per JournalEntry
```

This invariant is validated before posting. An entry that does not balance is rejected.

### 2.2 CashFlowTransaction as Transitional / Operational Table

The `CashFlowTransaction` table is the **current operational ledger** used by the existing V1/V2 system. It is transitional — it will continue to be written to by the current Daily Operations checkout flow and existing APIs during the rebuild, but it is **not** the final accounting source of truth.

**Transition plan:**
- During Phase 2, the `JournalEntry` + `JournalLine` tables will be introduced alongside `CashFlowTransaction`.
- During Phase 3, new financial operations will write to both `CashFlowTransaction` (for backward compatibility with existing UI and Daily Operations) and `JournalEntry` + `JournalLine` (for the new accounting engine).
- Once all financial operations are migrated to the double-entry model, `CashFlowTransaction` will become a read-only historical record and eventually be deprecated.
- **CashFlowTransaction must NOT be removed or truncated during the transition.** It remains the active operational ledger until the new model is fully validated.

**What CashFlowTransaction currently handles (and what replaces it):**

| CashFlowTransaction Responsibility | JournalEntry + JournalLine Replacement |
|------------------------------------|---------------------------------------|
| Record payment inflow | Debit Treasury / Credit Revenue |
| Record expense outflow | Debit Expense / Credit Treasury |
| Record salary payment | Debit Salary Expense / Credit Treasury |
| Record advance payment | Debit Advance Receivable / Credit Treasury |
| Record commission payment | Debit Commission Expense / Credit Treasury |
| Record supplier payment | Debit Accounts Payable / Credit Treasury |
| Record vault transfer | Debit Destination Treasury / Credit Source Treasury |
| Record reversal | New JournalEntry with opposite Debit/Credit lines |

### 2.3 Immutable Ledger Principle

Regardless of whether a `CashFlowTransaction` or a `JournalEntry` + `JournalLine` is used, the following principles apply to all financial records:

1. **No hard deletion of posted financial transactions.** Once a financial record is created and linked to a financial event, it must never be removed from the database.

2. **No soft deletion of ledger entries.** Setting `IsActive = false` on a `CashFlowTransaction` is prohibited. This was a defect in the legacy system (see Defect #1 in Section 8).

3. **Corrections use reversal entries.** To undo a financial transaction, create a new entry with the opposite effect, linked via `ReversalOfTransactionId` / `ReversalOfEntryId`. The original entry remains with `ReversedByTransactionId` / `ReversedByEntryId` pointing to the reversal.

4. **Draft documents may be cancelled.** Invoices in `Draft` status can be cancelled without creating reversals (no ledger entry exists yet for a draft).

5. **Posted entries must be reversed.** Once a financial event has been posted to the ledger, any correction must go through the reversal pattern.

6. **Every reversal must reference the original.** This creates a bidirectional audit trail.

7. **Reversals cannot themselves be reversed.** If a reversal was made in error, the correct procedure is to create a new original-type entry (not reverse the reversal). This prevents circular chains.

### 2.4 Current CashFlowTransaction Ledger Entry Structure

Every `CashFlowTransaction` must contain:

| Field | Purpose |
|-------|---------|
| `TransactionNumber` | Unique sequential identifier (e.g., `CFT-20260526-001`) |
| `Type` | `Inflow` or `Outflow` |
| `Category` | Semantic category: `Payment`, `Refund`, `Expense`, `Salary`, `Advance`, `Commission`, `Transfer`, `DrawerOpen`, `DrawerClose`, `SupplierPayment` |
| `Amount` | Always positive; sign determined by `Type` |
| `PaymentMethod` | `Cash`, `Card`, `BankTransfer` |
| `ReferenceId` | FK to the source entity (Payment, Expense, Salary, etc.) |
| `ReferenceNumber` | Human-readable reference (Receipt number, Expense number, etc.) |
| `PerformedBy` | UserId of the person who created this entry |
| `BranchId` | **Required** — never `Guid.Empty` |
| `CashierSessionId` | Link to the active cashier session (null only for non-session events like bank transfers) |
| `TreasuryId` | Which treasury/account was affected |
| `IsReversal` | `true` if this is a reversal entry |
| `ReversalOfTransactionId` | Points to the original entry (if this is a reversal) |
| `ReversedByTransactionId` | Points to the reversal entry (if this was reversed) |
| `CreatedAt` | Timestamp of creation |

---

## 3. Core Objects

### 3.1 Patient Charge / Invoice

**Purpose:** Record a financial obligation from a patient for services rendered.

| Field | Description |
|-------|-------------|
| Invoice Number | Unique sequential identifier |
| Patient | The patient being charged |
| Line Items | One per service/procedure, with quantity, unit price, total |
| Status | `Draft` → `Issued` → `Paid` or `Cancelled` |
| Subtotal | Sum of line item totals before discount |
| Discount | Optional discount amount |
| Tax | Tax amount (if applicable) |
| Total | Subtotal − Discount + Tax |
| Paid | Sum of linked payments |
| Remaining | Total − Paid |

**Rules:**
- Draft invoices can be freely edited or cancelled.
- Issued invoices cannot have line items changed; only payments can be applied.
- Cancelling an issued invoice requires a reversal of any linked payments.
- An invoice is automatically marked `Paid` when `Paid >= Total`.

### 3.2 Receipt / Payment

**Purpose:** Record money received from a patient.

| Field | Description |
|-------|-------------|
| Receipt Number | Unique sequential identifier |
| Patient | The patient making the payment |
| Amount | Payment amount (must be > 0) |
| Payment Method | Cash, Card, Bank Transfer |
| Invoice | Optional link to a specific invoice |
| Contract | Optional link to a specific contract/installment plan |
| Received By | The user who received the payment |
| Cashier Session | The active session when payment was recorded |
| Treasury | Which treasury received the funds |

**Rules:**
- A payment must always be linked to an active cashier session.
- A payment creates a `CashFlowTransaction` of type `Inflow`.
- Deleting a payment creates a reversal `CashFlowTransaction` of type `Outflow`.
- A payment can be partially or fully refunded.

### 3.3 Refund

**Purpose:** Return money to a patient.

| Field | Description |
|-------|-------------|
| Original Payment | The payment being refunded |
| Amount | Refund amount (cannot exceed original payment) |
| Reason | Mandatory reason text |
| Refunded By | The user processing the refund |
| Treasury | Which treasury the funds are returned from |

**Rules:**
- Full refund: Creates reversal for the full payment amount.
- Partial refund: Creates reversal for the partial amount only.
- Refund amount cannot exceed the original payment amount minus any previous refunds.
- Refunds require Admin authorization.

### 3.4 Discount / Adjustment

**Purpose:** Reduce the amount a patient owes.

| Field | Description |
|-------|-------------|
| Type | `Discount` or `Adjustment` |
| Amount | Reduction amount |
| Reason | Mandatory reason |
| Approved By | Admin who authorized the discount |
| Target | Invoice or Contract being adjusted |

**Rules:**
- Discounts must be approved by Admin.
- Discounts do not create CashFlowTransactions (they reduce the obligation, not the cash).
- Adjustments may be positive (increase) or negative (decrease).
- Adjustments that affect cash require a reversal entry.

### 3.5 Contract / Installment Plan

**Purpose:** Define a payment schedule for a patient's treatment.

| Field | Description |
|-------|-------------|
| Patient | The patient under contract |
| Total Amount | Full cost of treatment |
| Down Payment | Initial payment at contract signing |
| Installments Count | Number of installment periods |
| Installment Amount | Amount per installment |
| Discount | Optional discount |
| Status | `Active` → `Completed` or `Cancelled` |
| Remaining Balance | Total − Down Payment − (Paid installments) |

**Rules:**
- Creating a contract can optionally create a down-payment receipt.
- Cancelling an active contract reverses all linked payments.
- A contract is `Completed` when `Total Paid >= Total Amount`.

### 3.6 Cashier Shift

**Purpose:** Track a single cashier's work session with the cash drawer.

| Field | Description |
|-------|-------------|
| Session Number | Unique sequential identifier |
| Cashier | The user operating the drawer |
| Branch | The clinic branch |
| Opening Balance | Starting cash in drawer |
| Opening Time | When the session started |
| Closing Time | When the session ended |
| Expected Cash | Opening Balance + Cash Inflows − Cash Outflows |
| Expected Card | Card Inflows − Card Refunds |
| Expected Bank | Bank Inflows − Bank Outflows |
| Actual Cash | Counted cash at close |
| Actual Card | Reported card total at close |
| Actual Bank | Reported bank total at close |
| Shortage/Surplus | Actual − Expected (per method) |
| Status | `Open` → `Closed` → `Reconciled` |

**Rules:**
- A cashier can have only one open session at a time.
- All cash payments require an open cashier session.
- Closing a session calculates expected totals from CashFlowTransactions.
- Reconciliation is performed by an Accountant or Admin.
- Shortages/surpluses are logged and must be explained.

### 3.7 Treasury / Account

**Purpose:** Represent a cash holding location or bank account.

| Field | Description |
|-------|-------------|
| Name | Display name (e.g., "الخزينة الرئيسية", "حساب البنك") |
| Type | `Vault` (physical cash) or `Bank` (bank account) |
| Balance | Current balance in YER |
| Branch | The branch this treasury belongs to |
| Is Active | Whether this treasury is in use |

**Rules:**
- Balance is derived from the sum of all linked CashFlowTransactions.
- Recalculation must always match: `Balance = SUM(Inflows) − SUM(Outflows)`.
- Transfers between treasuries create paired entries.
- Balance updates must use optimistic concurrency (RowVersion/xmin).

### 3.8 Expense

**Purpose:** Record operational expenditures.

| Field | Description |
|-------|-------------|
| Expense Number | Unique sequential identifier |
| Title | Description of the expense |
| Category | Rent, Utilities, Supplies, Maintenance, Other |
| Amount | Expense amount |
| Payment Method | Cash, Card, Bank Transfer |
| Approval Status | `Pending` → `Approved` or `Rejected` |
| Approved By | Admin who approved |
| Cashier Session | Active session for cash expenses |
| Treasury | Which treasury the funds come from |

**Rules:**
- Cash expenses require an open cashier session.
- Expenses below a configurable threshold can be auto-approved.
- Deleting an approved expense must create a reversal CashFlowTransaction (not soft-delete).
- Rejected expenses are not posted to the ledger.

### 3.9 Supplier Bill / Payment

**Purpose:** Track accounts payable to suppliers.

| Field | Description |
|-------|-------------|
| Bill Number | Supplier's invoice number |
| Supplier | The supplier |
| Total Amount | Full bill amount |
| Paid Amount | Sum of payments made |
| Remaining | Total − Paid |
| Status | `Unpaid`, `Partial`, `Paid` |

**Rules:**
- Payments on bills create CashFlowTransactions.
- Bill cancellation reverses all linked payments.
- Supplier statements aggregate all bills and payments.

### 3.10 Doctor Commission Payable / Payment

**Purpose:** Calculate and track doctor earnings from performed services.

| Field | Description |
|-------|-------------|
| Doctor | The earning doctor |
| Invoice Line Item | The service that earned the commission |
| Commissionable Amount | Net amount after material/lab costs |
| Doctor Share | Doctor's commission percentage × Commissionable Amount |
| Center Share | Remaining amount for the clinic |
| Status | `Calculated` → `Approved` → `Paid` |
| Payment Date | When the commission was disbursed |

**Rules:**
- Commissions are calculated when an invoice is issued (or when payment is received, configurable).
- Commission rates come from service defaults or doctor profiles.
- Payments create CashFlowTransactions with correct BranchId.
- Commission adjustments require Admin approval.

### 3.11 Salary / Advance Payment

**Purpose:** Track employee compensation.

| Field (Salary) | Description |
|-------|-------------|
| Employee | The employee |
| Year/Month | Pay period |
| Base Salary | Contracted salary |
| Deductions | Total deductions |
| Advances | Deducted advance payments |
| Net Salary | Base − Deductions − Advances |
| Status | `Generated` → `Paid` |

| Field (Advance) | Description |
|-------|-------------|
| Employee | The employee requesting the advance |
| Amount | Advance amount |
| Deduct From | Month/Year to deduct from salary |
| Status | `Pending` → `Approved` or `Rejected` |

**Rules:**
- Salary payments create CashFlowTransactions linked to cashier sessions.
- Advance approvals create CashFlowTransactions.
- Advance rejections or deletions of approved advances must create reversals.
- Salary records use `ReportsAccess` policy (not bare `[Authorize]`).

### 3.12 JournalEntry + JournalLine (Final Model)

See Section 2.1 for the full structure. This is the final accounting source of truth that will replace `CashFlowTransaction` as the system of record.

### 3.13 Audit Record

**Purpose:** Track all financial operations for compliance and troubleshooting.

| Field | Description |
|-------|-------------|
| Action | `Create`, `Update`, `Delete`, `Reverse`, `Refund`, `Approve`, `Reject`, `Close`, `Reconcile` |
| Entity Type | Payment, Expense, Invoice, etc. |
| Entity Id | The affected entity's primary key |
| Performed By | UserId |
| Timestamp | When the action occurred |
| Details | JSON with before/after values |
| Branch Id | Which branch this audit record belongs to |

---

## 4. Required Formulas

### 4.1 Patient Balance

```
Patient Balance = Total Invoiced − Total Paid − Total Discounts
```

Where:
- **Total Invoiced** = SUM(Invoice.TotalAmount) WHERE PatientId = X AND Status IN ('Issued', 'Paid')
- **Total Paid** = SUM(Payment.Amount) WHERE PatientId = X AND Payment is not reversed
- **Total Discounts** = SUM(Discount.Amount) WHERE PatientId = X AND Approved = true

**Double-counting prevention:**
- Do not sum both invoice totals and contract totals for the same patient. A contract is an alternative billing mechanism. If both exist, they must be mutually exclusive or explicitly linked.
- Refunds reduce Total Paid: subtract refund amounts from Total Paid.

### 4.2 Invoice Remaining Balance

```
Invoice Remaining = Invoice.TotalAmount − SUM(Payment.Amount) WHERE InvoiceId = X AND NOT Reversed
```

**Rules:**
- An invoice is `Paid` when `Remaining <= 0`.
- Partial payments reduce the remaining balance incrementally.
- Refunds on invoice-linked payments increase the remaining balance.

### 4.3 Contract Remaining Balance

```
Contract Remaining = Contract.TotalAmount − Contract.DiscountAmount − SUM(Payment.Amount) WHERE ContractId = X AND NOT Reversed
```

**Rules:**
- Down payment is included in the payment sum (it is the first payment).
- Installments remaining = Contract.Remaining / Contract.InstallmentAmount (rounded up).
- Overdue if next expected installment date has passed and remaining > 0.

### 4.4 Daily Cashier Expected Closing

```
Expected Cash = Opening Balance + SUM(Cash Inflows) − SUM(Cash Outflows)
Expected Card = SUM(Card Inflows) − SUM(Card Refunds)  
Expected Bank = SUM(Bank Inflows) − SUM(Bank Outflows)
```

Where all sums are from `CashFlowTransactions` WHERE `CashierSessionId = X` AND `IsReversal = false`.

**Reversal handling:**
- Reversals of inflows are outflows (subtract from expected).
- Reversals of outflows are inflows (add to expected).
- Do NOT double-count: a reversal is its own CashFlowTransaction entry; it is already included in the inflow/outflow sums by its `Type`.

**Double-counting prevention:**
- Only count each `CashFlowTransaction` once.
- Do not include both the original and the reversal as separate "events" — the reversal is already the opposite type.
- Use `IsReversal = false` for initial calculation, then separately report reversal count for audit visibility.

### 4.5 Treasury Balance

```
Treasury Balance = SUM(Inflow Amount) − SUM(Outflow Amount)
WHERE TreasuryId = X AND NOT (ReversedByTransactionId IS NOT NULL AND IsReversal = false)
```

Simplified (correct):
```
Treasury Balance = SUM(Amount WHERE Type=Inflow) − SUM(Amount WHERE Type=Outflow)
WHERE TreasuryId = X
```

This automatically accounts for reversals because:
- A reversal of an inflow is an outflow entry.
- A reversal of an outflow is an inflow entry.
- Both are just regular entries counted in the sum.

**Recalculation** must always match the stored `Balance`. If they differ, log a warning and update the stored balance to match the calculated value.

### 4.6 Profit and Loss

```
Revenue = SUM(Payment.Amount WHERE Type=Inflow AND Category=Payment) 
         − SUM(Payment.Amount WHERE Type=Outflow AND Category=Refund)

Cost of Services = SUM(InvoiceLineItem.MaterialCost + InvoiceLineItem.LabCost)

Operating Expenses = SUM(OperationalExpense.Amount WHERE Approved)
                    − SUM(Reversals of approved expenses)

Doctor Commissions = SUM(DoctorCommissionPayment.Amount WHERE NOT Reversed)

Net Profit = Revenue − Cost of Services − Operating Expenses − Doctor Commissions
```

**Double-counting prevention:**
- Revenue is calculated from actual payments received, NOT from invoices issued. An issued invoice without payment is not revenue.
- Refunds are subtracted from revenue, not added as expenses.
- Do not count both the payment and the contract total for the same service. Contracts are paid via payments — count the payments.
- Expense reversals reduce the operating expense total.
- Commission payments to doctors are a cost, not a revenue reduction.
- Supplier payments are operating expenses (captured via the expense category or supplier bills), not a separate deduction.

### 4.7 Doctor Commissions

```
Commissionable Amount = LineItem.TotalPrice − LineItem.MaterialCost − LineItem.LabCost

Doctor Share = Commissionable Amount × Doctor Commission Rate
Center Share = Commissionable Amount − Doctor Share
```

**Rules:**
- Commission rates come from service defaults, falling back to doctor profile defaults.
- Commissions are locked after approval; further changes require unlock + re-approval.
- Paid commissions cannot be reversed without Admin approval and a corresponding CashFlowTransaction reversal.

---

## 5. Reception Checkout Integration

### 5.1 Workflow Architecture

Reception does NOT use Finance V3 screens for daily collection. Reception works from **one primary operational screen only**: `/daily-operations`. This is a fundamental architectural decision that separates the operational collection layer from the accounting and supervisory layer.

**Patient checkout flow:**

```
Booking / Appointment
  → Arrival (check-in)
    → Queue (clinic queue)
      → Visit (doctor sees patient)
        → Ready for Checkout (visit complete)
          → Reception registers payment in /daily-operations
            → Payment posts to Finance V3 ledger
              → Receipt prints from Daily Operations
                → Accountant reviews/reconciles in Finance V3
```

### 5.2 Rules

1. **`/daily-operations` is the only normal reception payment-entry interface.** Reception must not be expected to navigate into Finance V3 for normal daily collection. Finance V3 is the accounting and supervisory module for Admin and Accountant.

2. **Reception may create a collection only through the approved checkout workflow.** The checkout step is embedded inside the Daily Operations patient handoff. Reception does not create standalone payments outside this flow.

3. **Payment posting must eventually be atomic.** A single checkout transaction must create all of the following as one atomic unit:
   - Payment record
   - Receipt record
   - JournalEntry + JournalLines (in the final model) or CashFlowTransaction (in the transitional model)
   - Cashier Session link
   - Treasury link
   - Invoice/Contract allocation (if applicable)

4. **No unlinked payment in the final design.** Every payment must be linked to either an invoice, a contract, or a patient checkout event. If invoice/contract allocation is not yet known at checkout time, this is documented as a Phase 3 design problem — no logic should be invented in Phase 1.

5. **Finance V3 is the accounting and supervisory module.** It is for Admin and Accountant to review balances, reconcile cashier sessions, manage invoices, approve expenses, manage suppliers, run reports, and audit financial operations. It is not a data-entry interface for reception.

6. **Daily Operations continues to use `/api/payments` and the existing cashier session flow** until the Finance V3 posting flow is fully implemented and tested. This API must not be disabled during the rebuild.

### 5.3 Permission for Checkout Collection

Reception users who are permitted to collect payments at checkout will be granted an explicit permission such as `checkout.collect` or `finance.create` (the existing `PAYMENTS_CREATE` permission key maps to `finance.create` in the current permissions system). This permission authorizes payment creation **only** within the Daily Operations checkout flow, not direct financial posting through Finance V3.

### 5.4 Current State (Phase 1)

- Daily Operations already has a checkout/payment workflow using `/api/payments`.
- This workflow is **not disabled** and continues to function normally.
- Finance V3 does not yet provide an alternative payment-entry path.
- The sidebar navigation for `/finance-v2` has been replaced with `/finance-v3` (Admin/Accountant only), but the old `/finance-v2` route and all payment APIs remain active.
- Direct old routes and APIs are **not disabled** in Phase 1.

---

## 6. Future Posting Rules

> These rules will govern all financial posting once the Finance V3 double-entry model is implemented. They are documented here as the target architecture. Phase 1 does not enforce these rules in code.

### 6.1 Mandatory Fields for Every Posted Entry

Every posted financial entry (JournalEntry + JournalLines or CashFlowTransaction) must contain:

1. **BranchId** — Required, never `Guid.Empty`. If the user's BranchId is null, the operation must be rejected with a clear error.
2. **PerformedBy (UserId)** — Required. System-generated entries (e.g., auto-reversals) must reference the user who triggered the parent action.
3. **FinancialDocument reference** — Required. Every ledger entry must be linked to a source document (Payment, Expense, Invoice, etc.). No free-floating ledger entries.
4. **TreasuryId** — Required for all entries that affect a cash or bank account. Only non-cash adjustments (discounts, write-offs) may omit TreasuryId, but they must reference the affected account instead.
5. **Audit trail** — Every financial action (create, update, reverse, approve, reject, close, reconcile) must produce an AuditRecord.

### 6.2 No Unlinked Payments

Every payment must be linked to at least one of:
- An Invoice
- A Contract
- A Patient Checkout event (visit-based payment)

Payments with no document link are prohibited in the final design. During the transition, existing unlinked payments are documented as legacy data that must be reconciled.

### 6.3 No Deletion of Posted Entries

Posted financial entries cannot be deleted or soft-deleted (`IsActive = false`). Corrections must use the reversal pattern (see Section 2.3).

### 6.4 Reversal Corrections Only

All corrections to posted financial data must be made through reversal entries. Direct editing of amount, type, or treasury on a posted entry is prohibited.

### 6.5 Balanced Double-Entry

Every `JournalEntry` must have `SUM(Debit) = SUM(Credit)`. The system must validate this before posting and reject any unbalanced entry.

### 6.6 No Guid.Empty in Financial Records

The value `Guid.Empty` (`00000000-0000-0000-0000-000000000000`) is prohibited in `BranchId`, `TreasuryId`, `PerformedBy`, `JournalEntryId`, `JournalLineId`, or any FK field in financial records. Any attempt to write `Guid.Empty` to these fields must be rejected at the application level with a clear error message.

---

## 7. Roles and Permissions

### 7.1 Role Definitions

| Role | Arabic | Description |
|------|--------|-------------|
| Admin | مدير النظام | Full access to Finance V3: all financial operations, reporting, audit, and configuration |
| Accountant | محاسب | Full Finance V3 operational and review access: reporting, reconciliation, approvals, invoice management |
| Reception | استقبال/كاشير | Payment collection only through `/daily-operations` checkout; no Finance V3 dashboard access by default |
| Doctor | طبيب | No financial entry; clinically necessary limited status view only if approved by Admin |

### 7.2 Permission Matrix

| Operation | Admin | Accountant | Reception | Doctor |
|-----------|-------|------------|-----------|--------|
| **Finance V3 Access** |
| Finance V3 dashboard (/finance-v3) | ✅ | ✅ | ❌ | ❌ |
| Patient account statement | ✅ | ✅ | ❌ | ❌ |
| Invoices (all) | ✅ | ✅ | ❌ | ❌ |
| Payments (all — review) | ✅ | ✅ | ❌ | ❌ |
| Contracts (all) | ✅ | ✅ | ❌ | ❌ |
| Expenses (all) | ✅ | ✅ | ❌ | ❌ |
| Treasuries | ✅ | ✅ | ❌ | ❌ |
| Supplier bills | ✅ | ✅ | ❌ | ❌ |
| Own commissions | ✅ | ✅ | ❌ | ✅ |
| All commissions | ✅ | ✅ | ❌ | ❌ |
| Salary records | ✅ | ✅ | ❌ | ❌ |
| P&L reports | ✅ | ✅ | ❌ | ❌ |
| Daily cash summary | ✅ | ✅ | ❌ | ❌ |
| Audit log | ✅ | ✅ | ❌ | ❌ |
| **Daily Operations Checkout** |
| Register payment at checkout | ✅ | ❌ | ✅ (via `finance.create`) | ❌ |
| Print receipt at checkout | ✅ | ❌ | ✅ | ❌ |
| Open/close cashier session | ✅ | ❌ | ✅ | ❌ |
| **Finance V3 — Create** |
| Create invoice | ✅ | ✅ | ❌ | ❌ |
| Create contract | ✅ | ✅ | ❌ | ❌ |
| Create expense | ✅ | ✅ | ❌ | ❌ |
| Create supplier bill | ✅ | ✅ | ❌ | ❌ |
| **Finance V3 — Edit** |
| Edit draft invoice | ✅ | ✅ | ❌ | ❌ |
| Edit contract terms | ✅ | ✅ | ❌ | ❌ |
| Edit commission costs | ✅ | ❌ | ❌ | ❌ |
| **Finance V3 — Post** |
| Issue invoice | ✅ | ✅ | ❌ | ❌ |
| Approve expense | ✅ | ✅ | ❌ | ❌ |
| Approve advance | ✅ | ❌ | ❌ | ❌ |
| Approve commission | ✅ | ❌ | ❌ | ❌ |
| Pay salary | ✅ | ❌ | ❌ | ❌ |
| Pay commission | ✅ | ✅ | ❌ | ❌ |
| **Finance V3 — Reverse** |
| Refund payment | ✅ | ❌ | ❌ | ❌ |
| Cancel invoice | ✅ | ✅ | ❌ | ❌ |
| Cancel contract | ✅ | ✅ | ❌ | ❌ |
| Delete payment | ✅ | ❌ | ❌ | ❌ |
| Reverse expense | ✅ | ❌ | ❌ | ❌ |
| **Finance V3 — Reconcile** |
| Close cashier session | ✅ | ❌ | ✅ | ❌ |
| Reconcile session | ✅ | ✅ | ❌ | ❌ |
| Recalculate treasury | ✅ | ❌ | ❌ | ❌ |
| **Admin Only** |
| Manage treasuries | ✅ | ❌ | ❌ | ❌ |
| Create vault transfer | ✅ | ✅ | ❌ | ❌ |
| Approve vault transfer | ✅ | ✅ | ❌ | ❌ |
| Apply discount | ✅ | ❌ | ❌ | ❌ |
| View audit log | ✅ | ✅ | ❌ | ❌ |

### 7.3 Finance V3 Access Gate

**Finance V3 is restricted to Admin and Accountant roles only.** Access is NOT granted through the `PAYMENTS_VIEW` / `finance.view` permission or any other permission key. The access check is:

```typescript
const isAuthorized = user?.role === "Admin" || user?.role === "Accountant";
```

This is an explicit role check, not a permission-based check. The reason is that `finance.view` permission is granted to Reception users for Daily Operations checkout access, and we must not allow Reception to access the Finance V3 dashboard through that same permission.

### 7.4 Branch Isolation

- Non-Admin users can only see financial data from their own branch (`BranchId`).
- Admin can see all branches.
- **Critical rule:** `BranchId` must NEVER be `Guid.Empty`. If a user's `BranchId` is null, the financial operation must be **rejected**, not silently default to `Guid.Empty`.

### 7.5 Current Limitation (Phase 1)

- The sidebar hides the old finance entry for normal navigation.
- Direct old routes (`/finance`, `/finance-v2`) and all payment APIs are **not disabled** in Phase 1.
- Daily Operations intentionally still uses the current payment API (`/api/payments`) until the Finance V3 posting flow is implemented.
- This is documented as a limitation and rollout risk: during the transition period, payments created from Daily Operations are posted using the existing (defective) logic, not the Finance V3 canonical ledger.

---

## 8. Confirmed Defects in Current Implementation

### Defect #1: Operational Expense Deletion Soft-Deletes CashFlowTransaction

**Severity:** HIGH  
**Location:** `OperationalExpensesController.Delete()`  
**Description:** When an operational expense is deleted, the linked `CashFlowTransaction` is soft-deleted by setting `IsActive = false`. This violates the immutable ledger principle.  
**Expected:** Create a reversal `CashFlowTransaction` (matching the pattern in `FinanceService.DeletePaymentAsync()`).

### Defect #2: `BranchId ?? Guid.Empty` Pattern in Financial Writes

**Severity:** HIGH  
**Locations:**
- `VaultTransfersController.Create()` — writes `Guid.Empty` as BranchId
- `SupplierBillsController.Create()` — writes `Guid.Empty` as BranchId
- `TreasuriesController.Create()` — writes `Guid.Empty` as BranchId
- `SalaryController.PaySalary()` — writes `Guid.Empty` as BranchId on CashFlowTransaction
- `AdvancePaymentController.Approve()` — writes `Guid.Empty` as BranchId
- `CommissionService.RecordPaymentAsync()` — hardcodes `Guid.Empty`

**Description:** When `currentUser.BranchId` is null, controllers silently write `Guid.Empty` instead of rejecting the request. This creates orphaned financial records.  
**Expected:** Reject the operation with a clear error if `BranchId` is null.

### Defect #3: SalaryController Uses Bare `[Authorize]`

**Severity:** MEDIUM  
**Location:** `SalaryController.GetAll()`  
**Description:** All other finance controllers use role-based policies. SalaryController uses bare `[Authorize]`, allowing any authenticated user (including patients) to list salary records.  
**Expected:** Use `[Authorize(Policy = "ReportsAccess")]`.

### Defect #4: Salary Payment Missing CashierSessionId

**Severity:** MEDIUM  
**Location:** `SalaryController.PaySalary()`  
**Description:** Cash CashFlowTransactions for salary payments have no `CashierSessionId`, breaking drawer reconciliation.  
**Expected:** Link to the active cashier session for cash salary payments.

### Defect #5: Advance Payment Delete Does Not Reverse CashFlowTransaction

**Severity:** LOW-MEDIUM  
**Location:** `AdvancePaymentController.Delete()`  
**Description:** Deleting an approved advance payment soft-deletes it without creating a reversal `CashFlowTransaction`.  
**Expected:** Create reversal for approved advances.

### Defect #6: Fragmented Finance UI

**Severity:** HIGH (user experience)  
**Description:** Two parallel finance experiences exist:
- `/finance` (V1) — 10 routes, partially orphaned from sidebar
- `/finance-v2` (V2) — 1 route with 12 internal tabs, linked from sidebar
- Plus: patient FinanceTab/PaymentsTab/ContractsTab, portal finance, commissions page, HR salaries/advances
  
Dashboard still links to V1 routes. V1 has create forms that V2 lacks. V2 has cashier/treasury that V1 lacks.  
**Expected:** Unified Finance V3 experience.

### Defect #7: 2900-line Monolithic Finance-V2 Page

**Severity:** MEDIUM (maintainability)  
**Location:** `frontend/src/app/(dashboard)/finance-v2/page.tsx`  
**Description:** The entire V2 finance UI is in a single ~2900-line file with 12 internal tabs. This makes debugging, testing, and incremental improvement extremely difficult.  
**Expected:** Component-per-tab architecture in Finance V3.

---

## 9. Legacy Inventory

### 9.1 Frontend Finance Routes

| Route | Purpose | Status |
|-------|---------|--------|
| `/finance` | V1 Finance dashboard | **DUPLICATE** — superseded by V2 |
| `/finance/overdue` | V1 Overdue contracts | **DUPLICATE** — in V2 overdue tab |
| `/finance/payments` | V1 New payment form | **DUPLICATE** — in V2 cashier tab |
| `/finance/payments/[id]` | V1 Payment receipt | **PRIMARY** — used by V2 for print |
| `/finance/invoices` | V1 Invoice list | **DUPLICATE** — in V2 invoices tab |
| `/finance/invoices/new` | V1 Create invoice | **PRIMARY** — not in V2 |
| `/finance/invoices/[id]` | V1 Invoice detail | **PRIMARY** — not in V2 |
| `/finance/contracts` | V1 Contracts list | **DUPLICATE** — in V2 contracts tab |
| `/finance/contracts/new` | V1 Create contract | **PRIMARY** — not in V2 |
| `/finance/contracts/[id]` | V1 Contract detail | **PRIMARY** — not in V2 |
| `/finance-v2` | V2 Unified finance screen | **CURRENT** — sidebar entry |
| `/finance-v3` | V3 Financial Center (rebuild) | **NEW** — Admin/Accountant only |
| `/commissions` | Commission management | **STANDALONE** |
| `/hr/salaries` | Salary management | **STANDALONE** |
| `/hr/advances` | Advance management | **STANDALONE** |
| `/patients/[id]/payments/[paymentId]/receipt` | Patient receipt print | **PRIMARY** — patient-scoped |
| `/patients/[id]/print/financial` | Patient financial statement | **PRIMARY** — patient-scoped |
| `/portal/finance` | Portal patient finance | **PRIMARY** — portal-scoped |

### 9.2 Backend Finance API Endpoints

| Controller | Route Prefix | Endpoints | Auth Policy |
|-----------|--------------|-----------|-------------|
| PaymentsController | `/api` | 10 | FinanceAccess |
| InvoicesController | `/api/invoices` | 8 | FinanceAccess |
| CashierSessionsController | `/api/cashier-sessions` | 6 | FinanceAccess |
| OperationalExpensesController | `/api/expenses` | 6 | ReportsAccess |
| TreasuriesController | `/api/treasuries` | 4 | FinanceAccess |
| VaultTransfersController | `/api/vault-transfers` | 4 | FinanceAccess |
| SupplierBillsController | `/api/supplier-bills` | 6 | ReportsAccess |
| CommissionsController | `/api/commissions` | 13 | CommissionView |
| SalaryController | `/api/salaries` | 6 | `[Authorize]` ⚠️ |
| AdvancePaymentController | `/api/advances` | 4 | ReportsAccess |
| PurchaseOrdersController | `/api/purchase-orders` | 7 | AdminOnly |

### 9.3 Finance Database Tables

| Table | Entity | Migrations |
|-------|--------|------------|
| Payments | Payment | Initial + InvoicePaymentLink |
| Contracts | Contract | Initial |
| Receipts | Receipt | Initial |
| Invoices | Invoice | AddInvoicesAndInvoiceLineItems |
| InvoiceLineItems | InvoiceLineItem | AddInvoicesAndInvoiceLineItems + CommissionSystem |
| CashierSessions | CashierSession | AddCentralFinanceV2Hub |
| CashFlowTransactions | CashFlowTransaction | AddCentralFinanceV2Hub + FinancialIntegrityAuditSprint |
| OperationalExpenses | OperationalExpense | AddCentralFinanceV2Hub + SupplierBillsAndApprovals |
| Treasuries | Treasury | AddTreasuryVaultTransfers |
| VaultTransfers | VaultTransfer | AddTreasuryVaultTransfers |
| SupplierBills | SupplierBill | AddSupplierBillsAndApprovals |
| SupplierBillPayments | SupplierBillPayment | AddSupplierBillsAndApprovals |
| SalaryRecords | SalaryRecord | AddCentralFinanceV2Hub |
| AdvancePayments | AdvancePayment | AddCentralFinanceV2Hub |
| DoctorCommissionPayments | DoctorCommissionPayment | AddDoctorCommissionSystem |
| PurchaseOrders | PurchaseOrder | AddSuppliersAndPurchases |
| PurchaseOrderLineItems | PurchaseOrderLineItem | AddSuppliersAndPurchases |
| PatientAccounts | PatientAccount | Initial (NOT finance — portal auth) |

### 9.4 Finance Services

| Service | Interface | Key Methods |
|---------|-----------|-------------|
| FinanceService | IFinanceService | CreatePayment, DeletePayment, RefundPayment, GetSummary, GetOverdue, CreateContract, UpdateContract, GetAccountStatement, GetPatientFinanceSummary |
| CommissionService | ICommissionService | GetLineItemCommission, GetInvoiceCommissions, Recalculate, Approve, RecordPayment, GetReport |
| PdfService | IPdfService | GeneratePaymentReceipt, GenerateFinancialStatement, GenerateInvoicePdf |

---

## 10. Migration / Reset Plan

> **This plan is for FUTURE execution only. It must NOT be executed in Phase 1.  
> It will be separately approved and deployed after the new Finance V3 structure is reviewed.**

### 10.1 Owner Confirmation

The clinic owner confirms that the current finance records are experimental/test data only and are not real production accounting records.

### 10.2 Finance Transaction Test Candidates

The following tables contain finance transaction data that **may** be candidates for cleanup after independent verification and explicit approval. Each table must be reviewed for row count and content before any action is taken.

| Table | Candidate Action | Justification |
|-------|-----------------|---------------|
| CashFlowTransactions | TRUNCATE (after backup) | Test ledger entries only |
| CashierSessions | TRUNCATE (after backup) | Test sessions only |
| OperationalExpenses | TRUNCATE (after backup) | Test expenses only |
| Treasuries | DELETE rows (keep table structure) | Reset balances; re-seed with correct starting values |
| VaultTransfers | TRUNCATE (after backup) | Test transfers only |
| SupplierBills | TRUNCATE (after backup) | Test bills only |
| SupplierBillPayments | TRUNCATE (after backup) | Test payments only |
| Payments | TRUNCATE (after backup) | Test payments only |
| Contracts | TRUNCATE (after backup) | Test contracts only |
| Receipts | TRUNCATE (after backup) | Auto-generated from payments |
| Invoices | TRUNCATE (after backup) | Test invoices only |
| InvoiceLineItems | TRUNCATE (after backup) | Linked to invoices |
| DoctorCommissionPayments | TRUNCATE (after backup) | Test commission payments |
| SalaryRecords | TRUNCATE (after backup) | Test salary records |
| AdvancePayments | TRUNCATE (after backup) | Test advances only |

### 10.3 Preserve Unless Independently Verified and Explicitly Approved

The following tables are **NOT** finance test data candidates. They contain operational, inventory, supplier, employee, or audit records that must be preserved unless separately verified and explicitly approved for cleanup.

| Table | Reason for Preservation |
|-------|----------------------|
| **Suppliers** | Supplier contacts are operational reference data, not test finance transactions. Must be preserved. |
| **PurchaseOrders** | May contain operational/inventory procurement history, not merely finance test data. Preserve unless independently verified. |
| **PurchaseOrderLineItems** | Linked to PurchaseOrders. Preserve unless PurchaseOrders are verified and explicitly approved for cleanup. |
| **InventoryItems** | Inventory data is operational, not finance test data. Must be preserved. |
| **LabOrders** | Lab work orders are clinical/operational records. Must be preserved. |
| **Employees** | Employee records are HR data, not finance test data. Must be preserved. |
| **AuditLogs** | Audit logs must **NEVER** be deleted. They are the compliance trail. |
| **FinancialAuditLogs** | Finance-specific audit logs must **NEVER** be deleted. They are the compliance trail for financial operations. |

### 10.4 Always Preserve — No Exceptions

The following tables must **NEVER** be truncated or deleted under any circumstances:

| Table | Reason |
|-------|--------|
| Patients | Real patient records |
| PatientAccounts | Portal authentication (NOT finance) |
| Users | Staff accounts |
| Appointments | Real appointment history |
| Visits | Clinical records |
| OrthoCases | Orthodontic treatment data |
| OrthoVisits | Orthodontic visit records |
| TreatmentPlans | Clinical treatment plans |
| DentalCharts | Clinical dental records |
| PerioAssessments | Clinical periodontal data |
| Radiographs | Uploaded medical images |
| PatientDocuments | Uploaded documents |
| Prescriptions | Prescription records |
| Referrals | Referral records |
| Messages | Messaging data |
| Conversations | Messaging conversations |
| MessageAttachments | Messaging attachments |
| ClinicSettings | System configuration |
| ClinicRooms | Room configuration |
| ClinicServices | Service catalog |
| BookingRequests | Patient booking data |
| SmsMessages / SmsTemplates | Communication records |
| EmailLogs | Communication logs |

### 10.5 Reset Procedure (Future)

1. Create a full database backup.
2. Verify the backup is restorable.
3. Run a **read-only row-count and content review** on all candidate tables (Section 10.2).
4. Obtain explicit owner approval for each table to be cleaned.
5. Run the truncation in a transaction for each approved finance table.
6. Re-seed treasuries with correct starting balances.
7. Verify all non-finance data is intact.
8. Deploy the new Finance V3 schema (Phase 2).
9. Verify the new schema works with empty tables.
10. Announce the system ready for production financial entry.

---

## 11. Transition Risk

### 11.1 Old Routes Still Reachable

During Phase 1, the following old financial routes and APIs remain active and reachable by direct URL:

- `/finance` — V1 Finance dashboard (10 sub-routes)
- `/finance-v2` — V2 Unified finance screen
- All backend API endpoints (`/api/payments`, `/api/cashier-sessions`, `/api/expenses`, etc.)

These are not disabled because:
- Daily Operations checkout depends on `/api/payments`.
- Patient receipt printing depends on `/finance/payments/[id]`.
- Patient financial statements depend on `/patients/[id]/print/financial`.
- Invoice and contract creation forms only exist in V1 routes.
- Portal finance depends on `/portal/finance`.

**Risk:** Users who know the old URLs can continue to use the defective V1/V2 financial interfaces. This is accepted as a known risk during the transition period.

**Mitigation:** The sidebar no longer links to `/finance` or `/finance-v2`. The new `/finance-v3` route is the only financial navigation entry visible to Admin and Accountant. Old routes will be deprecated and eventually removed in later phases.

### 11.2 CashFlowTransaction Remains Active

The `CashFlowTransaction` table continues to be the operational ledger during the transition. All new payments from Daily Operations, expenses, salaries, and other financial operations continue to write to this table using the existing (defective) logic.

**Risk:** New defective data (missing BranchId, soft-deleted entries) may continue to be created during the transition.

**Mitigation:** This is documented and accepted. The data cleanup plan (Section 10) will address this after the new model is validated.

### 11.3 No Data Integrity Enforcement

Phase 1 does not add any backend validation guards, migrations, or data integrity checks. The existing defects documented in Section 8 continue to exist.

**Risk:** Financial data integrity continues to degrade until Phase 2 is implemented.

**Mitigation:** This is explicitly out of scope for Phase 1. The purpose of Phase 1 is documentation, navigation, and foundation — not code fixes.

---

## 12. Phase Roadmap

### Phase 1: Audit & Foundation (Current PR)
- [x] Inventory all finance routes, APIs, entities, tables
- [x] Document confirmed defects
- [x] Create this specification document
- [x] Create Finance V3 landing screen (/finance-v3)
- [x] Hide legacy finance navigation (Admin/Accountant only)
- [x] Document Reception Checkout Integration workflow
- [x] Protect /finance-v3 route for Admin/Accountant only
- [x] Document JournalEntry + JournalLine as final accounting model
- [x] Document transition risk and old route accessibility
- [ ] **NO** code changes to backend logic
- [ ] **NO** database migrations
- [ ] **NO** data deletion
- [ ] **NO** disabling of /api/payments (Daily Operations depends on it)

### Phase 2: Canonical Double-Entry Ledger & Database Model
- Create `JournalEntry` and `JournalLine` entities with EF Core
- Add `TreasuryId` to all CashFlowTransactions
- Fix all BranchId issues (reject null BranchId instead of Guid.Empty)
- Create proper EF Core migrations
- Implement the reversal pattern consistently across all controllers
- Add validation guards for null BranchId/UserId
- Dual-write: new operations write to both CashFlowTransaction and JournalEntry+JournalLine

### Phase 3: Patient Invoices, Receipts, Discounts, Refunds, and Balances
- Rebuild invoice lifecycle (Draft → Issued → Paid/Cancelled)
- Rebuild payment collection with proper cashier session integration
- Implement Daily Operations checkout → Finance V3 atomic posting
- Implement discount/adjustment system
- Implement refund with proper reversal entries
- Patient balance calculation
- Handle invoice/contract allocation at checkout (design decision needed)

### Phase 4: Cashier Shifts, Treasury, Expenses, Suppliers, Salary, and Commissions
- Rebuild cashier session open/close/reconcile
- Rebuild treasury management with balance recalculation
- Rebuild expense approval workflow
- Rebuild supplier bill management
- Rebuild salary and advance payment workflow
- Rebuild commission calculation and payment

### Phase 5: Reports and Financial Statements
- Profit and Loss statement
- Daily cash summary
- Patient account statements
- Treasury reports
- Supplier statements
- Commission reports
- Audit trail viewer

### Phase 6: Data Migration and Cleanup
- Execute the data cleanup plan (Section 10)
- Migrate historical CashFlowTransaction data to JournalEntry + JournalLine
- Validate all balances after migration
- Deprecate old routes and APIs

### Phase 7: Deprecation and Production Lock
- Disable old V1/V2 financial routes
- Remove backward-compatible CashFlowTransaction writes
- Full production deployment with double-entry enforcement
- Staff training on new financial workflows
