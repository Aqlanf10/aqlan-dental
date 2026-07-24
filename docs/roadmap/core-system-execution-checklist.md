# Core System Execution Checklist

- Last updated: 2026-07-24 (continuation audit)
- Current phase: 1
- Current branch: `codex/core-p1-s3-route-role-manifest`
- Current PR: pending
- Rule: update this file after every core-system PR.

## Current Snapshot

- [x] Cephalometry PRs #697, #698, and #699 are draft and paused.
- [x] Phase 0 branch starts from `origin/main` commit `73a8c3e4`.
- [x] Backend Release build passed with 0 errors and 109 warnings.
- [x] Backend unit tests passed: 2,429 / 2,429.
- [x] Frontend type check passed after removing stale generated `.next` state.
- [x] Frontend lint passed with warnings.
- [x] Frontend tests passed: 383 / 383.
- [x] Frontend production build passed.
- [x] PR #700 CI passed: backend, frontend, encoding, and E2E jobs are green.
- [x] PR #700 backend coverage recorded: 8.28% lines and 37.28% branches.
- [x] PR #700 E2E evidence recorded: 1 public smoke passed and 4 authenticated tests skipped.
- [x] PR #700 merged to `main` as `da6e1e54`.
- [x] PR #701 aligned Reception appointment navigation and merged as `260cc4c1`.
- [x] PR #702 locked canonical route ownership and merged as `c3183d40`.
- [ ] Authenticated end-to-end patient journey is verified.
- [ ] Fresh and representative legacy database migration parity is verified.

## Finding Register

- [ ] `CORE-F-001` Critical: prove and repair cross-route visit idempotency.
- [ ] `CORE-F-002` Critical: establish safe EF/startup schema ownership transition.
- [~] `CORE-F-003` High: align queue reorder frontend/backend request contract. (FIXED 2026-07-24, branch `fix/core-f-003-queue-reorder-contract`, pending CI/merge.)
- [ ] `CORE-F-004` High: guard and audit queue priority/reorder actions.
- [ ] `CORE-F-005` High: remove VIP and implement controlled emergency behavior.
- [x] `CORE-F-006` High: align Reception appointment route access.
- [ ] `CORE-F-007` High: prevent invalid mixed-currency aggregates and PDF labels.
- [ ] `CORE-F-008` High: centralize identity, language, logo, and print contract.
- [ ] `CORE-F-009` High: make authenticated E2E execution status explicit.
- [ ] `CORE-F-010` High: ratchet and eventually block on meaningful backend coverage.
- [ ] `CORE-F-011` Medium: add identity-number duplicate detection and reviewed merge.
- [ ] `CORE-F-012` Medium: close lab target-state and escalation gaps.

## Phase 0 - Baseline

- [x] Preserve paused cephalometry branches and draft PRs.
- [x] Read governance, master/module specs, audits, sidebar, route guards, policies,
  settings, CI, tests, and migration chain.
- [x] Record local and remote verification baseline.
- [x] Publish current-state report with severity-ranked evidence.
- [x] Publish ordered priority plan and select first Phase 1 slice.
- [x] Review Phase 0 diff and encoding.
- [x] Open Phase 0 draft PR #700 to `main`.
- [x] Owner authorized completed branches to merge; PR #700 passed required checks and merged.

## Phase 1 - Architecture And Navigation

- [x] `CORE-P1-S1` Align Reception access to canonical `/appointments` and add tests.
- [x] `CORE-P1-S2` Lock canonical route/owner inventory and redirects with tests.
- [x] `CORE-P1-S3` Create one frontend route-role manifest.
- [ ] `CORE-P1-S4` Map canonical routes to backend policy ownership.
- [ ] `CORE-P1-S5` Distinguish executed and skipped authenticated E2E in CI.
- [ ] Phase 1 exit gate approved.

## Phase 2 - Settings, Identity, Language, Printing

- [ ] Define central settings and identity schema.
- [ ] Resolve runtime logo and text identity from one source.
- [ ] Implement Arabic RTL and English LTR application contracts.
- [ ] Implement independent print-language selection.
- [ ] Migrate supported print generators to one identity/currency contract.
- [ ] Phase 2 exit gate approved.

## Phase 3 - Roles And Permissions

- [ ] Publish role/action matrix.
- [ ] Add granular server actions for overrides, queue, payments, refunds, print, and settings.
- [ ] Add denial, approval, and audit tests.
- [ ] Phase 3 exit gate approved.

## Phase 4 - Patient Integrity

- [ ] Define canonical patient identifier contract.
- [ ] Add identity-number duplicate signal where available.
- [ ] Add permission-gated reviewed merge and audit history.
- [ ] Prove all journey modules retain one patient reference.
- [ ] Phase 4 exit gate approved.

## Phase 5 - Appointments And Capacity

- [ ] Enforce schedules, durations, leave, holidays, rooms, branches, and capacity.
- [ ] Define grace and overbooking settings.
- [ ] Add concurrent booking and clinic-date tests.
- [ ] Phase 5 exit gate approved.

## Phase 6 - Daily Operations And Queue

- [ ] Repair reorder wire contract.
- [ ] Implement settings-driven FIFO/grace behavior.
- [ ] Remove VIP.
- [ ] Require reason, approval, permission, and audit for Emergency.
- [ ] Verify privacy-safe waiting display and two-device consistency.
- [ ] Phase 6 exit gate approved.

## Phase 7 - Doctor Clinic And Visit

- [ ] Reproduce cross-route concurrency with synthetic data.
- [ ] Enforce one active visit per appointment.
- [ ] Enforce one active draft invoice per visit.
- [ ] Add repeated-click, concurrent-device, and transition tests.
- [ ] Phase 7 exit gate approved.

## Phase 8 - Lab

- [ ] Reconcile target statuses with current lifecycle.
- [ ] Implement manager/doctor/reception delay escalation.
- [ ] Prove receipt-before-delivery and audited override behavior.
- [ ] Reconcile patient price, lab cost, payable, and remake accounting.
- [ ] Phase 8 exit gate approved.

## Phase 9 - Multi-Currency Finance

- [ ] Define original, account, and reporting currency semantics.
- [ ] Preserve immutable transaction exchange-rate snapshots.
- [ ] Group unlike currencies or convert with an explicit rate/time.
- [ ] Add YER/SAR/USD cashier, refund, treasury, report, and PDF fixtures.
- [ ] Phase 9 exit gate approved.

## Phase 10 - Inventory, Administration, Reports

- [ ] Reconcile inventory/service consumption links.
- [ ] Reconcile staff/branch/room administration permissions.
- [ ] Reconcile operational reports to canonical records.
- [ ] Apply shared identity/print contract.
- [ ] Phase 10 exit gate approved.

## Phase 11 - Basic Orthodontics

- [ ] Stabilize patient-owned records, examinations, analyses, and diagnosis.
- [ ] Stabilize plan alternatives, approved plan, stages, visits, extraction, retention.
- [ ] Complete core orthodontic journey tests without advanced AI/VTO.
- [ ] Phase 11 exit gate approved.

## Phase 12 - Cephalometry Return Gate

- [ ] Phases 1 through 11 are approved.
- [ ] Authenticated patient journey passes end to end.
- [ ] No open Critical defect remains.
- [ ] `main` CI is green and authenticated E2E was actually executed.
- [ ] Paused cephalometry PRs are rebased/revalidated against current `main`.
- [ ] Owner explicitly authorizes cephalometry resumption.

## Continuation Update — 2026-07-24

See `docs/audits/CLOUD_WORK_CONTINUATION_AUDIT_2026-07-24.md` and
`docs/roadmap/CLOUD_WORK_MAINTENANCE_CONTINUATION_PLAN.md`.

- `main` advanced to `93c2985` (last PR #718). This checklist (dated 2026-07-17) was
  stale relative to merged work; reconciled here.
- Phase 9 multi-currency finance work merged out of documented order: PRs #711–#718
  (advance visibility, expense vouchers, per-currency ledger, payment FX snapshots,
  cashier multi-currency + reconciliation, year-end close, opening balances). The
  Phase 9 checklist items are therefore substantially addressed and need a
  reconciliation/re-verification pass (Track E2), not fresh implementation.
- `CORE-F-003` fixed this session (queue reorder wire contract), pending CI + merge.
- `recovery/audit-002-patient-journey` remains **unmerged** (1 commit ahead, 20 behind)
  and is tracked for review/rebase (Track C1).
- Local build/test could not run in the continuation sandbox (proxy blocks .NET SDK +
  api.github.com; process time cap). Verification for new work is via GitHub Actions CI.
