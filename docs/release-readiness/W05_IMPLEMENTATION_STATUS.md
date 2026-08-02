# W05 — Clinic Business Date

Status: implemented; final CI verification in progress.

## Scope completed

- Block arrival, queue entry, and treatment start for future appointments.
- Re-check the business-date rule inside transaction/advisory-lock boundaries.
- Allow an explicit Admin-only override with a mandatory operational reason.
- Persist every successful override in `AuditLog` with operation, appointment date, business date, reason, timezone, user, and event time.
- Use the canonical `IClinicClock` and the `Asia/Aden` business-date boundary.
- Apply the guard to the active Patient Journey service and every legacy appointment/queue entry point, including single and batch status mutations.
- Return appointment date and real arrival/queue/visit timestamps in the journey read model.
- Provide an Admin reason dialog in Daily Operations that retries the same operation with the explicit override payload.

## Regression coverage

- Same-day and past appointments are allowed.
- Tomorrow is blocked without override.
- Non-admin override is forbidden.
- Blank manager reason is rejected.
- Valid reason is normalized.
- Asia/Aden midnight boundary is tested.
- Intake and treatment-start behavior verify no mutation on rejection, the post-lock status transition, and persisted audit evidence on override.
- Appointment status and legacy treatment-start controller tests exercise the real mutations on the InMemory provider.
- Frontend contract verifies all guarded mutations, Admin-only prompt, and mandatory reason; a React rendering test verifies the real event timestamps.

Branch: `codex/w05-clinic-business-date`
PR: #805
