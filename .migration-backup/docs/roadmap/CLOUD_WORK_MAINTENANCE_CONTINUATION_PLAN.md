# Cloud Work Maintenance & Continuation Plan — 2026-07-24

- Companion to: `docs/audits/CLOUD_WORK_CONTINUATION_AUDIT_2026-07-24.md`
- Governing queue: `docs/governance/MANDATORY_SPRINT_QUEUE.md` (active gate CORE-00)
- `main` baseline: `93c2985` (last PR #718)
- Execution rules: inherit `docs/roadmap/core-system-priority-plan.md` §"Rules Of Execution":
  small independent branch from current `main`; one big problem per PR; product fixes
  not mixed with audit/governance PRs; additive migrations only with chain/snapshot/
  rollback review; runtime claims labelled until observed; ceph PRs #697/#698/#699
  never auto-merged; every merged PR updates the execution checklist.

## Cephalometry freeze (unchanged)

Ceph development stays frozen under CORE-00. Branches `codex/seq-57/58/59`
(PRs #697/#698/#699) are **not** merged, not rebuilt into `main`, and are revalidated
only at the Phase 12 return gate. Constraints preserved: ADP-LM-LAT-v1.0, Ceph Pilot,
WebCeph comparator-only (not gold standard), reviewer A/B blinding, no PatientId in the
pilot, no AI trained on WebCeph points, no clinical-accuracy claim. Current drift: 34
commits behind `main` — a future rebase plan is required before revalidation.

---

## Tracks

Each task: `ID · Severity · Problem · Root cause · Scope / Out-of-scope · Files ·
Tests · Migration risk · Dependencies · Exit gate · Complexity`.

### Track A — Production & data safety
- **A1 · Critical · CORE-F-002 schema ownership.** Root cause: EF migrations and
  `StartupDatabaseMaintenance.cs` both own schema. Scope: follow the documented
  phased-removal plan in `docs/agent-audit/c-08-startup-maintenance-audit.md`; make
  changes additive; no rewrite of history. Out-of-scope: deleting startup maintenance
  in one step. Files: `Infrastructure/Data/StartupDatabaseMaintenance.cs`, migrations.
  Tests: fresh-DB + representative-legacy migration parity. Migration risk: **High**.
  Deps: disposable Postgres. Exit: documented parity, no destructive DDL. Complexity: L.
- **A2 · High · CORE-F-009 honest E2E.** Make CI distinguish executed vs skipped
  authenticated E2E; a skipped journey must not read as verified. Files:
  `.github/workflows/ci.yml`, Playwright config. Tests: CI signal. Migration: none.
  Exit: CI surfaces skip explicitly. Complexity: S.
- **A3 · High · CORE-F-010 coverage ratchet.** Raise non-blocking backend coverage
  floor incrementally toward blocking. Migration: none. Complexity: M.

### Track B — Core architecture & navigation (Phase 1 residue)
- **B1 · High · CORE-F-003 queue reorder contract — DONE this session.** Branch
  `fix/core-f-003-queue-reorder-contract`, awaiting CI. See audit §5.
- **B2 · CORE-P1-S4 · backend policy ownership per canonical route.** Map each
  canonical route to its backend authorization policy with contract tests. Migration:
  none. Complexity: M.
- **B3 · CORE-P1-S5 · derive sidebar/guards from one route manifest** (server remains
  authority). Complexity: M.

### Track C — Patient journey
- **C1 · High · Reconcile `recovery/audit-002-patient-journey`.** Root cause: an
  unmerged commit "fix patient journey financial handoff" (CheckoutService +
  PatientJourneyController + dailyOperationsRoute) is 20 commits behind `main`. Scope:
  review, rebase onto `main`, re-run its tests, decide merge or supersede. Out-of-scope:
  new journey features. Exit: branch either merged green or explicitly closed with
  rationale. Complexity: M.
- **C2 · Critical · CORE-F-001 visit/invoice idempotency.** Root cause: cross-route
  advisory locks target different IDs; no unique filtered index on active
  `Visits.AppointmentId`, non-unique invoice indexes. Scope: reproduce concurrency on a
  disposable DB, add a DB-level guarantee (additive unique filtered index) + repeated-
  click/two-device tests. Migration risk: **Medium (additive index)**. Deps: A1 review
  of startup-maintenance interaction. Exit: no duplicate visit/invoice under concurrency.
  Complexity: L. (Incident-priority exception permitted.)

### Track D — Permissions & audit
- **D1 · High · CORE-F-004 queue priority/reorder authorization + audit.** Add granular
  server permission for reorder/priority and audit records for all ordering changes.
  Depends on B1. Complexity: M.
- **D2 · High · CORE-F-005 remove VIP; controlled emergency.** Align emergency reason
  requirement FE↔BE; remove `VIP`; require reason/approval/permission/audit. Note:
  enum change → check data/migration impact (additive/renaming policy). Complexity: M.

### Track E — Finance reconciliation
- **E1 · High · CORE-F-007 residual currency correctness.** Re-verify per-currency
  aggregation after #711–#718; fix residual PDF hardcoded `r.y.` totals, the
  payment-notification-as-YER path, and journal-line currency. Scope: reporting/PDF
  only; no ledger re-architecture. Tests: YER/SAR/USD same-day fixtures across
  cashier/treasury/refund/report/PDF. Migration: none/additive. Complexity: M.
- **E2 · Update finance sections of current-state + checklist** to reflect merged
  #711–#718 (governance PR). Complexity: S.

### Track F — Settings, identity & printing
- **F1 · High · CORE-F-008.** One settings-driven identity/logo/language/print contract;
  remove hardcoded clinic name/logo in sidebar/root/PDF; runtime logo from settings.
  Owner identity keys per CLAUDE.md (`clinic.name`, `clinic.lead_doctor`, …). Complexity: L.

### Track G — Lab & inventory
- **G1 · Medium · CORE-F-012.** Add Confirmed / Patient-Appointment-Needed states,
  manager escalation, and receipt-before-delivery override audit; reconcile price/
  payable/remake accounting. Complexity: M.

### Track H — Basic orthodontics
- **H1.** Stabilize patient-owned ortho records/diagnosis/plans/stages/visits/retention
  without advanced AI/VTO. Gate before any ceph return. Complexity: L.

### Track I — Cephalometry return gate (Phase 12, blocked)
- **I1.** Only after Tracks A–H exit, `main` CI green, authenticated journey passes, no
  open Critical: compute ceph drift/conflicts, produce a safe rebase plan for
  #697/#698/#699, and require explicit owner authorization. No merge before that.

---

## Immediate execution order (next slices)

1. **B1 (F-003)** — pushed; monitor CI, merge when green (done-pending-CI).
2. **E2** — reconcile stale finance docs/checklist to #711–#718 (this governance PR family).
3. **C1** — review & rebase `recovery/audit-002-patient-journey`.
4. **D2 (F-005)** — emergency-reason FE↔BE alignment + VIP removal (small, high value for the crowding pain-point).
5. **C2 (F-001)** — concurrency repro + additive unique index (incident-priority, larger).

Later phases proceed in Track order; ceph (Track I) stays frozen.

## Metrics tracked after each PR (per priority plan)
Open Critical/High count · canonical routes with role contracts · authenticated
journey steps passing · backend coverage + blocking status · frontend warnings ·
fresh/legacy migration parity · multi-currency reconciliation fixtures · ceph
return-gate completion count.
