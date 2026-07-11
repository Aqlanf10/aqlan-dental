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
  — 🔵 Round 1 done 2026-07-11: full audit in
  `docs/audits/ms-task-006-hardcoded-values-audit.md`. All HIGH findings fixed
  in-round (commission 40% fallback now reads its existing settings key;
  clinic identity in reminder/reset emails, portal clinic info + OTP SMS,
  booking WhatsApp message, printed prescription and ceph report all
  settings-driven). Remaining medium/low items (clinic.timezone key, public
  pages/nav chrome, recall SMS template) documented in the report with
  priorities — not silently dropped.
- `MS-TASK-007` Add PR automation/checklist for spec IDs and drift checks.
