# Daily Operations / Patient Journey Audit — Aqlan Dental Pro
**التاريخ:** 2026-06-12

## End-to-End Journey (verified implemented)
Main screen: `frontend/src/app/(dashboard)/patient-journey/page.tsx` (left: today's patients with status/service/room filters; right: patient panel with contextual actions). Backend: `PatientJourneyController.cs`, `ClinicQueueController.cs`, `DailyOperationsController.cs`.

| Step | Endpoint | Policy |
|---|---|---|
| Today list | `GET /api/patient-journey/today` | StaffOnly |
| Arrival + intake (complaint, consultation fee, room) | `POST /{appointmentId}/intake` | AdminOrReception |
| Move to waiting | `POST /{appointmentId}/send-to-queue` | AdminOrReception |
| Call patient | `POST /api/clinic-queue/{id}/call` | staff |
| Enter room | `POST /api/clinic-queue/{id}/enter-room` | staff |
| Doctor starts visit (creates Visit) | `POST /{appointmentId}/start-visit` | StaffOnly |
| Doctor handoff to reception | `POST /{visitId}/handoff-to-reception` | DoctorAccess |
| Draft invoice | `POST /{visitId}/create-draft-invoice` | FinanceAccess |
| Checkout | `POST /{id}/checkout` | AdminOrReception |
| Daily report | `GET /api/daily-operations/report` | FinanceAccess |

- **Concurrency:** every state transition uses `pg_advisory_xact_lock` + in-lock re-check (queue add/call/enter-room/start, handoff, draft invoice). Verified at `ClinicQueueController.cs:170,276,351` and `PatientJourneyController.cs:1028,1170,1326,1740`.
- **Realtime:** SignalR branch-scoped pushes after commit (`PatientCalled`, `QueueUpdated`); journey page also polls every 60s.
- **Role separation:** reception cannot perform clinical actions (handoff is DoctorAccess); doctors cannot perform intake/queue/checkout/invoice (AdminOrReception/FinanceAccess); finance section hidden from doctors in UI (`page.tsx:1195`).
- **Error handling:** all mutations surface backend Arabic `message` via toast — no silent failures found; all 4xx/5xx responses carry Arabic messages.

## Critical Bug Found & Fixed
### "Today" computed in UTC (High) — FIXED frontend-wide
`new Date().toISOString().split("T")[0]` converts to UTC before extracting the date. Yemen is UTC+3, so **after 21:00 local time every daily screen (patient journey, today schedule, finance tabs, attendance, lab, reports…) silently switched to tomorrow's date** — today's appointments "disappeared" in the evening shift.
**Fix:** new helper `localDateString()` in `frontend/src/lib/utils.ts` (local-timezone YYYY-MM-DD) and migration of all ~30 call sites across patient-journey, dashboard TodaySchedule, appointments, finance-v3 tabs (Overview month-start had the same shift bug), commissions, lab, HR attendance, portal, public booking, patient tabs. Covered by unit tests in `frontend/src/__tests__/`.

## Suspicions Re-verified as Already Handled
- **Draft invoice duplication:** dedup exists (fast-path + in-transaction re-check) — `PatientJourneyController.cs:1695-1764`.
- **Handoff state validation:** terminal/duplicate states rejected before and inside the lock — `PatientJourneyController.cs:1310-1346`.
- **Consultation fee before entry:** computed flag + manager override by design (`ManagerOverrideAllowed`), overrides audit-logged.

## Open Improvements (next sprints)
1. Server-side bound on journey date filter (currently any date accepted — format-validated only).
2. Backend validation for checkout amount > 0 (frontend validates; API direct calls could send 0).
3. Integration tests for journey state machine; SignalR live updates on the journey page instead of polling.
