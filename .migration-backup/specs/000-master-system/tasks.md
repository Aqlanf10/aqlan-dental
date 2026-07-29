# 000 Master System Tasks

These are future task groups only. Do not implement them from this spec without a child feature spec and small task plan.

- `MS-TASK-001` Maintain module map after route/controller/service changes.
  — Ongoing. Last synced 2026-07-10: MOD-012 now lists the six TD-021 A1–A4
  successor services (`FinanceService`/`IFinanceService` retired, #626–#628);
  MOD-018 documents the SEQ-03 deep-linkable permissions tab + the three
  Admin-only redirect stubs (#625).
- `MS-TASK-002` Ratchet test coverage around finance, auth, patient access, daily operations, lab, ortho, ceph, and surgery.
- `MS-TASK-003` Review sidebar/route permission alignment every sprint.
- `MS-TASK-004` Complete traceability table for all requirements in future sprint.
- `MS-TASK-005` Runtime-verify daily operations, doctor clinic, ceph AI review, and report identity.
- `MS-TASK-006` Audit settings usage to remove remaining hardcoded business rules.
  — 🔵 Three rounds completed through 2026-07-11. Full evidence is in
  `docs/audits/ms-task-006-hardcoded-values-audit.md`.
  Round 1 removed high-risk hardcoded money/identity values; round 2 covered
  public booking, recall, waiting-display, and booking-contact surfaces; round 3
  (SEQ-13, PR #648) made clinic timezone configuration real and safe, including
  reminder-job alignment and fallback tests. Remaining medium/low UI-chrome
  identity items are documented and intentionally not hidden.
- `MS-TASK-007` Add PR automation/checklist for spec IDs and drift checks.
