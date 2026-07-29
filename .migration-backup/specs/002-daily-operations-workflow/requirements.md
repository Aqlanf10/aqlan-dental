# 002 Daily Operations Workflow Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/daily-operations/`, `DailyOperationsController.cs`, `PatientJourneyController.cs`, `ClinicQueueController.cs`, `CheckoutService`, `PatientJourneyService`, `frontend/src/hooks/useSignalRClinicQueue.ts`, tests under `backend/tests/AqlanDentalPro.UnitTests/DailyOperations/` and `ClinicQueue/`.

- `DO-REQ-001`: Daily operations SHALL be the canonical reception workspace.
- `DO-REQ-002`: Check-in, walk-in, queue, room movement, visit close, quick payment, lab view, and daily report SHALL stay in this module unless a spec says otherwise.
- `DO-REQ-003`: Finance actions inside daily operations SHALL respect cashier/finance access.
- `DO-REQ-004`: Patient records accessed here SHALL respect patient privacy and backend guards.
- `DO-REQ-005`: Arabic error messages SHALL be used for blocked actions.
- `DO-REQ-006`: WHEN the today-journey data source fails THEN the board SHALL show an explicit Arabic failure notice with a retry action, and SHALL NOT present the day as empty ("لا توجد مواعيد") — a failed load is not an empty day. (Added by QA round 3 — `QA3-02`; implemented in `daily-operations/page.tsx`.)

## Target State

One fast daily workflow that reduces crowding and avoids parallel queue screens.

## Risks

Incomplete workflow, stale SignalR state, unauthorized payment actions, route duplication.

## Allowed Future Work

Improve queue UX, room assignment, no-show/recall, daily report, quick payment, and lab handoff within existing files.

## Forbidden Future Work

New daily dashboard, bypassing cashier session rules, frontend-only patient access.

## Acceptance Criteria

- WHEN reception performs a daily action THEN the existing daily operations APIs SHALL own it.
- WHEN money is collected THEN cashier/finance rules SHALL apply.
- WHEN behavior cannot be seen statically THEN mark `Needs runtime verification`.
