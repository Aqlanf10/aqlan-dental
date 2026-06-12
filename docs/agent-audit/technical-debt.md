# Technical Debt Register — Agent Audit 2026-06-12

(يكمل السجل الموجود في `docs/technical-debt-register.md` — هذه نتائج تدقيق هذه الجولة فقط)

## Resolved This Sprint
| ID | Item | Resolution |
|---|---|---|
| AD-01 | 400+ unrelated files committed (`skills/` 18MB, `download/`, `railway_logs.json`, root `.env`, 4 broken submodule gitlinks `aqlan-dental*`/`repo` that errored on fresh clones) | Removed from git tracking; worklogs moved to `docs/worklogs/` |
| AD-02 | UTC "today" bug in 26 frontend files | `localDateString()` helper + migration + tests |
| AD-03 | Exception detail leak in 6× 500-responses | Removed; test inverted to forbid |
| AD-04 | `RejectExpense` unguarded read-modify-write | Transaction + FOR UPDATE |
| AD-05 | Unconditional treasury decrements | Configurable guard + warning log + 6 tests |
| AD-06 | Lint warning (unused `_patientId`) | Removed |

## Open Debt (prioritized)
| ID | Item | Where | Priority |
|---|---|---|---|
| TD-A | Portal refresh token in localStorage | `stores/patientAuthStore.ts`, `lib/portalApi.ts` | High |
| TD-B | No doctor↔room assignment model (doctors restricted to patients only) | needs `DoctorRoom` entity + journey/queue filters | Medium |
| TD-C | Scattered `User.IsInRole` checks alongside policies | `PatientJourneyController.cs:507,715`, `DashboardController` | Medium |
| TD-D | Giant files: `PatientJourneyController.cs` (~2000 ln), `LabOrdersController.cs` (~1300 ln), `reports/page.tsx` (~1300 ln), `patient-journey/page.tsx` (~1460 ln) | split into partials/components | Medium |
| TD-E | 82 backend compiler warnings (mostly CS8602 in tests) | tests | Low |
| TD-F | Journey page polls (60s) instead of SignalR live updates | `usePatientJourney.ts` | Low |
| TD-G | Backend checkout amount not validated > 0 server-side | `PatientJourneyController.Checkout` | Low |
| TD-H | Journey date filter accepts any date (format-checked only) | `PatientJourneyController.cs:36-43` | Low |
| TD-I | 2 pre-existing lint warnings in `daily-operations/` components | frontend | Low |
| TD-J | Settings UI doesn't yet expose `finance.prevent_negative_treasury_balance` | SettingsController/UI | Low |
| TD-K | Inventory/purchase-orders module is schema-tolerant transitional code (TD-020) | see `docs/technical-debt/TD-020-raw-sql-inventory.md` | Tracked |
