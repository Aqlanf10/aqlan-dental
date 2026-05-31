# Finance + Program.cs Technical Debt Cleanup

**Branch**: `chore/finance-program-cleanup`
**Based on**: `chore/system-technical-debt-cleanup` (PR #263, commit `11ca2e6`)
**Date**: 2026-05-31

---

## What Was Extracted

### Program.cs → Configuration/ (8 extension method classes)

| File | Method | Extracted Section |
|------|--------|-------------------|
| `Configuration/RedisConfiguration.cs` | `AddRedisConfiguration()` | Redis IConnectionMultiplexer registration with 3-attempt fallback |
| `Configuration/JwtAuthenticationConfiguration.cs` | `AddJwtAuthentication()` | JWT Bearer authentication setup + TokenValidationParameters |
| `Configuration/AuthorizationPolicyConfiguration.cs` | `AddAuthorizationPolicies()` | All 17 role-based authorization policies |
| `Configuration/CorsConfiguration.cs` | `AddCorsConfiguration()` | AllowFrontend + AllowPublicApi CORS policies |
| `Configuration/RateLimiterConfiguration.cs` | `AddRateLimiterConfiguration()` | Auth, Booking, Portal, PasswordReset rate limiter policies + global limiter |
| `Configuration/ServiceRegistrationConfiguration.cs` | `AddApplicationServices()` | All DI repository + service registrations + HttpClients |
| `Configuration/FluentValidationConfiguration.cs` | `AddFluentValidationConfiguration()` | FluentValidation auto-validation + assembly scanning |
| `Configuration/ControllersConfiguration.cs` | `AddControllersConfiguration()` | SignalR, Controllers (JSON options), Swagger, static files, FormOptions |

### FinanceV3Controller → 4 partial class files

| File | Contents | Methods |
|------|----------|---------|
| `FinanceV3Controller.Helpers.cs` | Helper/utility methods | 5: MapDocumentTypeToCategory, CalculateCashCategoryAsync, ResolveBranchIdAsync, CalculateContractOutstandingAsync, CalculateInvoiceOutstandingAsync |
| `FinanceV3Controller.Reports.cs` | Read-only GET endpoints | 17: Dashboard, JournalEntries, AccountBalances, DailyCashSummary, ProfitAndLoss, PatientBalance, Treasuries, AuditTrail, PatientAccounts, TrialBalance, Payments, Invoices, Contracts, SupplierBills, VaultTransfers, Expenses, JournalEntryById |
| `FinanceV3Controller.CashierSessions.cs` | Cashier session endpoints | 3: GetActiveCashierSessionV3, CloseCashierSession, ReconcileCashierSession |
| `FinanceV3Controller.Treasuries.cs` | Treasury write endpoints | 3: CreateTreasury, CreateVaultTransfer, RecalculateTreasuryBalance |

---

## What Was Intentionally NOT Moved

### Program.cs (stays in place)

1. **Fail-Fast validation** (lines 31-42) — Early startup check, must stay before any registration
2. **Serilog configuration** (lines 44-49) — Tightly coupled to `builder.Host`
3. **Database (PostgreSQL) registration** (lines 52-54) — Simple 3-liner, not worth extracting
4. **`builder.WebHost.ConfigureKestrel()`** — Operates on `WebApplicationBuilder`, not `IServiceCollection`
5. **ALL post-`builder.Build()` code** (lines 85+) — SQL hotfixes, middleware pipeline, seed data, advisory lock maintenance — too risky to move

### FinanceV3Controller (stays in main file)

1. **Write endpoints**: CreatePayment, DeletePayment, CancelInvoice, CreateExpense, ApproveExpense, RejectExpense, DeleteExpense, CreateSupplierBill, PaySupplierBill — these are tightly coupled with the main controller context
2. **CancelInvoiceRequest DTO** — defined outside the controller class, kept in main file
3. **Class header + constructor** — must remain in only one partial file

---

## Line Count Changes

| File | Before | After | Change |
|------|--------|-------|--------|
| `Program.cs` | 3,507 | 3,090 | -417 (-12%) |
| `FinanceV3Controller.cs` | 3,324 | 962 | -2,362 (-71%) |

New files added:
- 8 Configuration files: 597 lines total
- 4 FinanceV3 partial files: 2,402 lines total

---

## Remaining Technical Debt

### High Priority

1. **Program.cs SQL hotfix blocks** (~2,500 lines of idempotent SQL DDL) — Should be extracted into a `StartupDatabaseMaintenance` service class, but requires careful testing to ensure idempotency is preserved and startup order is maintained.

2. **FinanceService 6x `BranchId ?? Guid.Empty`** — Service-layer guards needed. Currently `FinanceService` silently creates records with `Guid.Empty` BranchId when the current user has no branch assigned. Should throw or return error instead.

3. **ReportsController missing branch filters** — Cross-branch data exposure risk. Non-admin users from one branch could potentially access data from all branches.

4. **AdvancePaymentController missing branch filter** — No `ICurrentUserService` injection, so no branch scoping at all.

### Medium Priority

5. **DaySchedule optimistic update not reverted on failure** — Frontend updates UI optimistically but doesn't roll back on API error. Needs UX discussion about error handling pattern.

6. **Contracts `IsOverdue` hardcoded `false`** — Business logic decision needed: should overdue be calculated dynamically or stored?

7. **Invoice detail type alignment** — Frontend types don't match backend response shape for invoice line items. Requires backend response shape changes.

### Low Priority

8. **Payment method casing inconsistency** — `Cash` vs `cash` across modules. Needs cross-module audit and normalization.

9. **Program.cs unused `using` statements** — After extraction, some `using` directives in Program.cs may be unused (e.g., `StackExchange.Redis`, `Microsoft.AspNetCore.Authentication.JwtBearer`). Can be cleaned up in a future linting pass.

---

## Next Safe Cleanup Phases

### Phase 2: Startup SQL Extraction
- Move all `app.Services.CreateScope()` + `ExecuteSqlRawAsync` blocks into a `StartupDatabaseMaintenance` service
- Register as `IHostedService` that runs before the app accepts requests
- Keep the `try/catch` pattern for each block
- Gated by same `ENABLE_STARTUP_DB_MAINTENANCE` flag

### Phase 3: FinanceV3 Write Endpoint Decomposition
- Extract remaining write endpoints from main `FinanceV3Controller.cs` into:
  - `FinanceV3Controller.Payments.cs` — CreatePayment, DeletePayment
  - `FinanceV3Controller.Expenses.cs` — CreateExpense, ApproveExpense, RejectExpense, DeleteExpense
  - `FinanceV3Controller.SupplierBills.cs` — CreateSupplierBill, PaySupplierBill
  - `FinanceV3Controller.Invoices.cs` — CancelInvoice
- This would reduce the main file to just the header (~120 lines)

### Phase 4: Service Layer Branch Guards
- Add `ICurrentUserService` to `FinanceService`
- Replace all `BranchId ?? Guid.Empty` with proper guards
- Add branch filters to `ReportsController` and `AdvancePaymentController`

### Phase 5: Frontend Type Alignment
- Align invoice detail types with backend response shapes
- Normalize payment method casing across all modules
- Add overdue calculation to contracts endpoint
