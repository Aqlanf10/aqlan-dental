# Mobile owner directive — 2026-08-21

The owner explicitly approved continuing development, verification, merging, and
then continuing into the Aqlan Dental Pro mobile client.

This directive is recorded separately because `MANDATORY_SPRINT_QUEUE.md` still
contains the historical `CORE-00` marker even though the Phase 1 exit-gate commit
states that evidence is complete and only owner approval remained.

For mobile work:

- `MOBILE-01`: native foundation + secure staff session + dashboard/patients/appointments.
- Mobile consumes the existing ASP.NET Core API and PostgreSQL database.
- Web authentication remains HttpOnly-cookie based.
- Native refresh tokens are returned only from the explicit `/api/auth/mobile/*`
  route aliases and are stored with the OS secure credential store.
- Temporary-password users must satisfy the existing `MustChangePassword` gate before app access.
- Every mobile PR must pass the existing backend/frontend gates plus the Mobile
  workflow before merge.

This file documents the owner's direct reprioritization without deleting or
rewriting historical queue evidence.
