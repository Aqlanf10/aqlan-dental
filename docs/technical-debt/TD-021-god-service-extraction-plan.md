# TD-021 — God-service / god-controller extraction plan (multi-PR)

**Status:** planned (execution-ready) — no code moved yet.
**Owner decision recorded:** these are large structural refactors in the finance/ortho hot
path. They MUST NOT be done in a single session (CLAUDE.md warns explicitly against big
refactors in the hot path before launch). This document is the "خطة دقيقة متعددة الـ PRs"
prerequisite so each future PR is small, isolated, behaviour-preserving, and independently
verifiable.

Guiding rules for every PR below:
- **Behaviour-preserving only.** Move code; do not change logic. If a bug is found while
  moving, fix it in a *separate* PR first.
- **One cohesive slice per PR.** Never move a method whose private helpers are shared with
  methods that stay behind — split or duplicate-then-converge instead.
- **Tests stay green at every step.** The finance suite (`AqlanDentalPro.UnitTests/Finance/*`)
  and ceph/ortho suites must pass unchanged; add characterization tests *before* moving if a
  slice is thin on coverage.
- **DI + interface updates are part of the same PR** as the move, so `main` never has a
  dangling reference.

---

## Part A — `FinanceService` (2256 lines, 20 public methods)

`backend/src/AqlanDentalPro.Infrastructure/Services/FinanceService.cs`

Injected deps today: `AppDbContext db`, `IJournalEntryService journalEntryService`,
`ICurrentUserService currentUser`, `ILogger`, treasury/settings helpers. The god-service
mixes five concerns that should each become a focused service behind its own interface.

### Concern map (for slicing)

| Cluster | Public methods (line refs) | Notes |
|---------|----------------------------|-------|
| Contracts | `GetContractsAsync` (58), `GetContractByIdAsync` (81), `CreateContractAsync` (121), `UpdateContractAsync` (478), `UpdateContractStatusAsync` (521), `GetOverdueContractsAsync` (183) | read + CRUD + status machine |
| Payments | `GetPaymentByIdAsync` (174), `GetPaymentsAsync` (246), `CreatePaymentAsync` (267), `UpdatePaymentAsync` (839), `DeletePaymentAsync` (870), `RefundPaymentAsync` (963) | cashier-shift gated; uses `DualWrite*` + `ResolveTreasuryNoSaveAsync` |
| Statements/summary | `GetAccountStatementAsync` (633), `GetSummaryAsync` (735), `GetPatientFinanceSummaryAsync` (1224) | read-only aggregation |
| Supplier/refund | `PaySupplierBillAsync` (1342), `ProcessRefundAsync` (1481) | credit-note + supplier ledger |
| Invoice ledger | `PostInvoiceIssuedEntryAsync` (2109), `ReverseInvoiceIssuedEntryAsync` (2158), `TryMarkInvoicePaidAsync` (1800) | double-entry posting |

Private helpers shared across clusters (must be mapped before any move):
`DualWriteReversalEntryAsync` (2187), `DualWriteRefundEntryAsync` (2215),
`ResolveTreasuryNoSaveAsync` (used by refund + payment). These bind Payments and
Supplier/refund together — do NOT split those two clusters apart in the first passes.

### Recommended PR sequence (safest first)

**PR A1 — extract `InvoiceLedgerService` (lowest risk, most isolated).**
- Move `PostInvoiceIssuedEntryAsync` + `ReverseInvoiceIssuedEntryAsync` into a new
  `IInvoiceLedgerService` / `InvoiceLedgerService` (deps: `db`, `journalEntryService`,
  `currentUser`, `logger` only — verified self-contained, no shared private helpers).
- Leave `TryMarkInvoicePaidAsync` in FinanceService for now (it is entangled with payment
  allocation — a later pass).
- Update the 3 call sites: `InvoicesController.cs:715`, `InvoicesController.cs:813`,
  `FinanceV3Controller.cs:322` to inject `IInvoiceLedgerService`.
- Register in DI (`ServiceRegistrationConfiguration.cs`), remove the two methods from
  `IFinanceService`.
- Characterization test first: assert issuance posts a Debit PatientReceivable / Credit
  Revenue JE and cancellation posts the mirrored reversal (both auto-posted). These money
  paths are under-tested today — add the test in PR A1 before the move.

**PR A2 — extract `FinanceReadService`** (statements/summary cluster: `GetAccountStatementAsync`,
`GetSummaryAsync`, `GetPatientFinanceSummaryAsync`). Read-only, no cashier-shift/treasury
mutation → safe. Watch for shared private read helpers; move or duplicate-then-converge.

**PR A3 — extract `ContractService`** (contracts cluster). CRUD + status machine; verify no
payment-side private helper is used. The status transitions (active/completed/cancelled) are
the only mutation surface — keep the existing permission checks in the controller.

**PR A4 — extract `PaymentService` + `SupplierRefundService` together** (they share
`DualWrite*` + `ResolveTreasuryNoSaveAsync`). This is the highest-risk slice (cashier-shift
gating, treasury decrement, dual-write ledger). Do it LAST, after A1–A3 have shrunk the file
and proven the pattern. Consider a shared internal `LedgerWriter` for the `DualWrite*`
helpers rather than duplicating them.

After A1–A4, `FinanceService` is either empty (delete it) or a thin façade. `IFinanceService`
is decomposed into the focused interfaces.

---

## Part B — `OrthoCasesController` (1626 lines)

`backend/src/AqlanDentalPro.API/Controllers/OrthoCasesController.cs`

God-controller: overview/exam/diagnosis/plan/records endpoints all in one file. Extraction
here is **controller → service**, not controller-splitting-only, because most of the bulk is
business logic that belongs in an `OrthoCaseService`.

Recommended sequence (mirror the FinanceService discipline):
1. **PR B1** — move the read-only overview/exam/diagnosis query building into an
   `IOrthoCaseReadService`; the controller actions become thin adapters. (Note: `GetById`
   already has a per-action `OrthoSurgicalAccess` policy override — preserve it exactly.)
2. **PR B2** — extract the treatment-plan mutation endpoints into an `OrthoTreatmentPlanService`.
3. **PR B3** — extract records/photos endpoints.

Keep every `[Authorize]` policy (class-level `OrthoAccess` + the per-action overrides) byte-for-
byte during the move; access scoping is security-critical and was fixed carefully in the
ortho-surgical work.

---

## Part C — related deferred items (tracked, not started)

- **`StartupDatabaseMaintenance` C-08 phased deletion** (4592 lines): long-lived; only ever
  *remove* a hotfix once its migration is confirmed applied on prod for a full backup cycle.
  Do not batch multiple removals.
- **CashFlowTransaction dual-write removal** (Phase 7): blocked until finance-v3 journal is the
  sole source of truth on prod; needs a read-migration + backfill verification first.
- **Phase 3 enforcement** (every payment MUST link to an invoice/contract): blocked on PR A4
  (PaymentService extraction) so the enforcement lives in one place.

---

## Verification checklist (every PR)
- `dotnet build -c Release` — 0 errors.
- `dotnet test tests/AqlanDentalPro.UnitTests -c Release` — full suite green (currently 2245).
- Grep for stale references to moved methods (interface + call sites) — none remain.
- No migration, no schema change (these are pure code moves).
- Diff is a *move*: reviewer can see the deleted block equals the added block.
