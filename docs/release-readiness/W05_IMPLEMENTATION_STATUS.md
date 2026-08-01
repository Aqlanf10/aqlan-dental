# W05 — Clinic Business Date

Status: implementation and verification in progress.

Scope:
- Block arrival, queue entry, and treatment start for future appointments.
- Allow an explicit manager-only override with a mandatory reason.
- Record every successful override in the audit log within the journey transaction.
- Use the canonical clinic clock and Asia/Aden business-date boundary.

Branch: `codex/w05-clinic-business-date`
PR: #805
