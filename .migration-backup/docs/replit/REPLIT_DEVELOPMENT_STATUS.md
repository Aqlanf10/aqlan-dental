# Replit Development Environment — Baseline Status

Last updated: 2026-07-29 (baseline phase)
Branch: `replit-development` (local commits only — nothing pushed to `origin`)
Last successful commit on this branch: `336ff7b2` — "fix(startup): fix fresh-DB baseline bootstrap crash on truly empty databases"

## Environment

- **Backend**: ASP.NET Core .NET 8, unchanged package (`backend/`, run via the Replit artifact `artifacts/api-server`).
- **Frontend**: Next.js 15 + React 19, unchanged package (`frontend/`, run via the Replit artifact `artifacts/aqlan-dental`).
- **Database**: isolated PostgreSQL instance inside Replit (dev-only, fake/seeded data). No connection to Railway or any production database at any point. Two databases were used during baseline:
  - `aqlan_dental_replit_dev_clean` — the long-running dev database used by the two running artifacts/workflows. Currently has 102/102 migrations applied, 18 seeded users, plus demo seed data added by hand during earlier QA (`DoctorSchedules`, `Labs`, `LabWorkPrices` — see "Known seeding gaps" below).
  - `aqlan_dental_baseline` — a throwaway database created and dropped solely to verify migrations run cleanly from a truly empty schema (see next section). Not used by any running service.
- Redis: not currently wired into any Replit workflow; not exercised in this baseline (no code path in this session depended on it).

## Baseline verification performed

1. **Fresh-database migration run (from scratch)** — created a brand-new empty Postgres database and started the API against it directly (no manual DB patching). This exposed and fixed a real bug (see below). After the fix: schema bootstraps cleanly, all 102 migrations are recorded in `__EFMigrationsHistory`, `DbSeeder` completes and creates 18 users, and `POST /api/auth/login` returns a valid JWT for `admin` on the very first boot. This is the authoritative proof that migrations work from zero, independent of any of the earlier hand-patched dev database.
2. **Backend build**: `dotnet build` — 0 errors (113 pre-existing nullable-reference warnings, no new ones introduced).
3. **Backend unit tests**: `dotnet test tests/AqlanDentalPro.UnitTests` — **2624/2624 passed**.
4. **Backend integration tests**: `dotnet test tests/AqlanDentalPro.IntegrationTests` — **cannot run in this environment**. They use Testcontainers to spin up a disposable Postgres container, and the Replit sandbox does not permit privileged container creation (`OCI runtime create failed ... mounting sysfs ... operation not permitted`). This is an environment limitation, not a code defect — flagged here so it isn't mistaken for a regression later.
5. **Frontend typecheck**: `tsc --noEmit` — clean, 0 errors.
6. **Frontend lint**: `next lint` — clean, only pre-existing warnings (a handful of unused imports, two `<img>` vs `next/image` suggestions, one `react-hooks/exhaustive-deps`). No errors.
7. **Frontend unit tests**: `vitest run` — **517/517 passed** (90 test files).
8. **Frontend production build**: `next build` — succeeds, all routes compiled (static + dynamic).
9. **Login smoke test**: verified end-to-end against both the fresh baseline DB and the running dev DB.

## Bugs found and fixed during baseline (committed to `replit-development`)

1. **Migration-history reconciliation bug** (`StartupDatabaseMaintenance.cs`, `EnsureInvoicesAndMigrationHistoryAsync`) — 3 of the self-healing "HOTFIX" guard conditions checked the wrong table/column (copy-paste bug), so they unconditionally deleted 3 valid rows from `__EFMigrationsHistory` on every startup. This caused EF to re-attempt already-applied migrations and crash with "already exists" errors, aborting `DbSeeder` before it could create any users — the original cause of "login always fails, `Users` table is empty" on this environment. Fixed the 3 DELETE guard conditions and added matching INSERT-back self-healing for them. Commit: `96a59628`.
2. **Fresh-database bootstrap crash** (`StartupDatabaseMaintenance.cs`, `EnsureFreshDatabaseMigratedAsync`) — the empty-database code path called `db.Database.ExecuteSqlRawAsync(createScript)` on the full `GenerateCreateScript()` output. EF's raw-SQL helper treats the string as a composite format string even with zero parameters; the generated DDL legitimately contains literal `{` characters (Postgres array/JSONB default literals like `'{}'`), which crashed with `FormatException: Expected an ASCII digit`. **This path had never been exercised end-to-end before** — every earlier test in this environment ran against a database that already had history/Users and so took the `MigrateAsync()` branch instead. Fixed by running the script through a raw ADO `DbCommand` instead of EF's helper. Commit: `336ff7b2`. Verified: a truly empty database now bootstraps, seeds, and accepts login on first boot.

## Known seeding gaps (not code bugs — missing demo data)

`DbSeeder` never seeds these two operational tables, so a genuinely fresh database has no bookable doctor time slots and no labs to select in the lab-order flow:
- `DoctorSchedules` — empty by default. Worked around for the running dev DB by inserting default Sun–Thu 09:00–17:00 schedules for the 5 active doctors directly via SQL (not a `DbSeeder` change).
- `Labs` / `LabWorkPrices` — empty by default. Worked around the same way: inserted 2 demo labs with pricing for all existing work types.

These workarounds only exist in the currently-running `aqlan_dental_replit_dev_clean` database, **not** in `DbSeeder` itself — a fresh clone still won't have bookable doctor slots or labs until `DbSeeder` is extended, or the same manual SQL is re-run. This is a real product gap worth a future module task if the team wants first-boot demo data to be fully self-contained.

## QA findings (manual + Playwright-based testing subagent, against the running dev DB)

- Admin login, dashboard, patients, appointments (after seeding `DoctorSchedules`), lab order creation (after seeding `Labs`), and role-gating (reception correctly blocked from `/finance-v3` with 403) all verified working end-to-end.
- Admin/reception logout correctly redirects to `/login` (an earlier report of a "stuck access-denied screen" on logout was a one-frame transient render during the auth-state transition, not a real bug — reproduced and confirmed not to persist).
- **Lab ↔ Finance integration gap**: creating a lab order (status `مسودة` / draft) does **not** currently produce any row on `/lab/payables` or any visible line item on `/finance-v3`, despite a migration named `IntegrateLabPayablesWithFinanceV3` existing in the migration history. Not yet root-caused — needs investigation of whether a payable is only generated once an order leaves draft status (e.g. on confirmation/dispatch) or whether the integration itself is incomplete. Flagged as a candidate first module.
- Minor/cosmetic: intermittent SignalR "Server timeout elapsed" / negotiation `502`s noticed in the browser console during heavier flows (self-recovering, did not block any tested action); an unrelated empty-CSS-color console warning not yet root-caused (cosmetic).

## Module inventory

### Backend API controllers (`backend/src/AqlanDentalPro.API/Controllers/`)
Auth, Patients, Appointments, Doctors, DoctorSchedules, Branches, Users, Employees, Attendance, Leave, Salary, Contracts, EmployeeDocuments (HR)
FinanceV3 (+ AccountingPeriods, CashierSessions, DisbursementVouchers, DoctorCommissions, ManualJournals, OpeningBalances, Reports, Treasuries), FinanceV3Suppliers, Invoices, Payments, AdvancePayment, Treasuries, VaultTransfers, Commissions, SupplierBills, PurchaseOrders, OperationalExpenses
Labs, LabOrders, LabPayables, LabReports, LabWorkPrices, LabWorkTypes
Inventory, ServiceConsumables, TreatmentPackages, ServicesSettings, RoomsSettings
Ortho: OrthoCases, OrthoCaseAi, OrthoModelAnalyses, OrthoSurgicalCases, Ceph, CephAiModels, CephBenchmark, CephNorms, PhotoAnalysis
ClinicQueue, DailyOperations, Surgery, Visits, TreatmentPlan, RadiologyOrders, ClinicalPhotos, Documents, DocumentTemplates, Prescriptions, Referrals
PatientJourney, PatientSegments, PatientPortal, PatientPortalMessages
Messages, Notifications, Sms, WhatsApp, WhatsAppWebhook, EmailStats
AuditLogs, Backup, Reports, Dashboard, General, Search, Settings, Uploads, BookingRequests, CashierSessions, AiSettings, Public

### Frontend route groups (`frontend/src/app/`)
- `(auth)` — login, reset-password
- `(dashboard)` — appointments, booking-requests, branches, ceph, clinic-command-center, clinic-queue, daily-operations, doctor-clinic, doctors, employees, finance-v3, general, hr, inventory, lab, messages, ortho, ortho-surgical, patient-journey, patients, patient-segments, prescriptions, radiology-orders, referrals, reports, schedule, settings, sms, surgery, users, whatsapp
- `(portal)/portal` — patient self-service portal (appointments, finance, messages, prescriptions, profile, treatments, change-password, login)
- `(public)`, `clinic-display` — public-facing pages

### Dependencies between modules (not exhaustive, notable ones)
- Appointments depend on `DoctorSchedules` for slot availability.
- Lab Orders depend on `Labs` + `LabWorkPrices`/`LabWorkTypes`; intended to flow into `LabPayables` and then `FinanceV3` (currently not confirmed working — see QA findings).
- FinanceV3 is the central ledger most other financial modules (Invoices, Payments, Commissions, SupplierBills, Treasuries) are expected to post into.
- PatientPortal is a separate auth/session surface from the staff dashboard, backed by the same `Patients`/`Users` domain.

## Development priorities (suggested, pending your choice of first module)

1. **Lab ↔ Finance integration** — real gap found in QA, has a dedicated migration suggesting it was meant to work; good first module.
2. **DbSeeder gaps** (`DoctorSchedules`, `Labs`) — low effort, removes reliance on manual SQL workarounds for anyone re-cloning this environment.
3. General module-by-module hardening per the workflow described in your brief (read → data-flow trace → bug list w/ severity → acceptance criteria → fix → test → checkpoint → commit → report), one module at a time, starting wherever you'd like.

## Current task

Baseline phase only, per your instructions. Stopped here — waiting for you to name the first module to work on.
