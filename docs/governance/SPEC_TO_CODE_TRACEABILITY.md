# Spec To Code Traceability

Initial table based on static repo inspection. Incomplete rows are marked `Needs completion in future sprint`.

| Spec ID | Module | Requirement | Files implementing it | Tests | Status | Last updated | Risk |
|---|---|---|---|---|---|---|---|
| `MS-REQ-001` | Clinic identity | Settings-backed clinic identity | `FinanceClinicIdentity.cs`, `SettingsController.cs`, PDF generators | Finance PDF tests | partial | 2026-07-02 | Medium |
| `MS-REQ-002` | Arabic RTL | Arabic RTL UI | `frontend/src/app/layout.tsx`, `globals.css`, Arabic UI files | Encoding guard | active | 2026-07-02 | Medium |
| `MS-REQ-003` | Roles | User roles and policies | `UserRole.cs`, `AuthorizationPolicyConfiguration.cs`, `usePermissions.ts` | Authorization/Permissions tests | active | 2026-07-02 | High |
| `MS-REQ-004` | Patients | Patient access/privacy | `PatientAccessFilter.cs`, `PatientAccessService`, `PatientsController.cs` | Authorization, portal, patient tests | active | 2026-07-02 | High |
| `MS-REQ-005` | Daily operations | Reception workflow | `daily-operations/`, `DailyOperationsController.cs`, `PatientJourneyController.cs`, `ClinicQueueController.cs` | DailyOperations, ClinicQueue tests | active | 2026-07-02 | High |
| `MS-REQ-006` | Appointments | Appointment scheduling | `AppointmentsController.cs`, `AppointmentService.cs`, appointment UI/hooks | Appointment tests | active | 2026-07-02 | Medium |
| `MS-REQ-007` | Queue | Waiting queue | `ClinicQueueController.cs`, `ClinicQueueItem`, daily operations modules | ClinicQueue tests | active | 2026-07-02 | Medium |
| `MS-REQ-008` | Doctor clinic | Doctor workspace | `doctor-clinic/`, patient journey/visit APIs | Needs completion in future sprint | needs review | 2026-07-02 | High |
| `MS-REQ-009` | Orthodontics | Ortho workspace | `ortho/`, `components/ortho/`, `OrthoCasesController.cs`, `OrthoService` | Ortho tests | active | 2026-07-02 | High |
| `MS-REQ-010` | Cephalometry | Ceph and draft AI | `ceph/`, `components/ceph/`, `CephController.cs`, `CephAiDraftService` | Ceph tests | active, runtime review needed | 2026-07-02 | High |
| `MS-REQ-011` | Surgery | Surgery and ortho-surgical | `surgery/`, `ortho-surgical/`, `SurgeryController.cs`, `OrthoSurgicalCasesController.cs` | Surgery and OrthoSurgical tests | active | 2026-07-02 | High |
| `MS-REQ-012` | Finance | Finance V3 | `finance-v3/`, `FinanceV3Controller*.cs`, `FinanceService.cs`, `TreasuryResolutionService.cs` | Finance tests | active | 2026-07-02 | Critical |
| `MS-REQ-013` | Lab | Lab orders/payables/reports | `lab/`, `LabOrdersController.cs`, `LabPayablesController.cs`, `LabReportsController.cs` | Lab tests | active | 2026-07-02 | High |
| `MS-REQ-014` | Inventory | Inventory/purchases/suppliers | `inventory/`, `InventoryController.cs`, `PurchaseOrdersController.cs` | Inventory tests | active | 2026-07-02 | Medium |
| `MS-REQ-015` | Reports | Reports/PDF | `ReportsController.cs`, `PdfService.cs`, PDF generators, print pages | PDF tests | active | 2026-07-02 | High |
| `MS-REQ-016` | Settings | Settings-backed rules | `SettingsController.cs`, settings pages, `FinanceSettingsKeys.cs` | Settings/finance tests | partial | 2026-07-02 | High |
| `MS-REQ-017` | Navigation | Sidebar/routes | `Sidebar.tsx`, `routePermissions.ts`, dashboard layout | Needs completion in future sprint | active | 2026-07-02 | Medium |
| `MS-REQ-018` | Production | Deployment/CI safety | `Program.cs`, `.github/workflows/ci.yml`, `encoding-guard.yml` | CI | active | 2026-07-02 | High |
| `001` | Navigation | No duplicate routes | `Sidebar.tsx`, `routePermissions.ts` | Needs completion in future sprint | active | 2026-07-02 | Medium |
| `NAV-REQ-001` | Navigation | One canonical staff sidebar | `frontend/src/components/layout/Sidebar.tsx` | Needs completion in future sprint | documented | 2026-07-02 | Medium |
| `NAV-REQ-002` | Navigation | Dashboard and command center ownership | `frontend/src/app/(dashboard)/page.tsx`, `frontend/src/app/(dashboard)/clinic-command-center/page.tsx` | Needs runtime verification | needs owner decision | 2026-07-02 | Medium |
| `NAV-REQ-003` | Navigation | Explicit root dashboard guard behavior | `frontend/src/lib/routePermissions.ts`, `frontend/src/app/(dashboard)/layout.tsx` | Needs route guard tests | needs review | 2026-07-02 | Medium |
| `NAV-REQ-004` | Daily operations | Queue and patient journey redirects stay compatibility-only | `frontend/src/app/(dashboard)/clinic-queue/page.tsx`, `frontend/src/app/(dashboard)/patient-journey/page.tsx` | Needs runtime verification | documented | 2026-07-02 | Medium |
| `NAV-REQ-006` | Lab | Accountant lab subroute access matches visible links | `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts` | Needs role smoke/tests | mismatch found | 2026-07-02 | High |
| `NAV-REQ-007` | Lab/settings | BranchManager lab settings access matches visible links | `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts` | Needs role smoke/tests | mismatch found | 2026-07-02 | High |
| `NAV-REQ-010` | Ortho/surgery | Ortho-surgical route remains contextual or owner-approved visible | `frontend/src/app/(dashboard)/ortho-surgical/page.tsx`, `frontend/src/lib/routePermissions.ts` | Needs runtime verification | needs owner decision | 2026-07-02 | Medium |
| `NAV-REQ-013` | Finance | Finance remains a single sidebar entry | `frontend/src/app/(dashboard)/finance-v3/page.tsx`, `frontend/src/components/layout/Sidebar.tsx` | Needs completion in future sprint | documented | 2026-07-02 | Low |
| `NAV-REQ-014` | Daily operations | Daily operations owns reception tabs | `frontend/src/app/(dashboard)/daily-operations/` | Needs runtime verification | documented | 2026-07-02 | Medium |
| `NAV-REQ-020` | Dashboard | Honest dashboard-alert failure state with retry and cached-data preservation | `frontend/src/components/dashboard/AttentionAlerts.tsx` | `AttentionAlerts.test.tsx` | fixed (SEQ-20, PR #657) | 2026-07-11 | Medium |
| `NAV-REQ-021` | Dashboard | Recent patients loading is distinct from a genuine empty list | `frontend/src/app/(dashboard)/page.tsx` | `DashboardPage.test.tsx` | fixed (SEQ-21, PR #658) | 2026-07-11 | Medium |
| `002` | Daily operations | One reception workflow | daily operations UI/API | DailyOperations/ClinicQueue tests | active | 2026-07-02 | High |
| `003` | Doctor clinic | Doctor workflow | doctor-clinic UI, patient journey APIs | Needs completion in future sprint | needs review | 2026-07-02 | High |
| `004` | Orthodontics | Ortho workspace | ortho UI/API/services | Ortho tests | active | 2026-07-02 | High |
| `005` | Cephalometry | Draft AI and reports | ceph UI/API/services | Ceph tests | active | 2026-07-02 | High |
| `006` | Finance | Finance stability | finance UI/API/services/settings | Finance tests | active | 2026-07-02 | Critical |
| `007` | Lab/inventory | Lab-inventory integration | lab/inventory UI/API | Lab/Inventory tests | partial | 2026-07-02 | High |
| `008` | Users/roles | Auth and permissions | auth/users/policies/route guard | Auth/Authorization tests | active | 2026-07-02 | Critical |
| `009` | Reports | Reports/PDF | reports/print/PDF services | PDF tests | active | 2026-07-02 | High |

## Production Owner QA — Round 1 Continuation (2026-07-02)

Live production QA (admin) beyond the initial round. After the `ClinicServices.Color`
fix deployed, `/api/dashboard/stats` still 500'd, exposing two further schema-drift
500s on the two most-used operational screens, both fixed as idempotent startup
hotfixes (C-08 pattern, no migration). Two additional 500s (enum-type drift) were
investigated and documented for a follow-up round (owner decision needed).

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `QA1C-01` | Finance/Schema | `Contracts."Currency"` missing → `/api/dashboard/stats` + `/api/contracts` 500 (batched DDL rollback on the Treasury unique-index step) | `StartupDatabaseMaintenance.EnsureMultiCurrencyColumnsAsync` | fixed | yes | yes (post-deploy 200) | no |
| `QA1C-02` | Ortho/Journey | `Visits."WireUpper"/"WireLower"/"CurrentStage"`/`OrthoCaseId` missing (no hotfix) → `/api/patient-journey/today` 500 when a day has an appointment+visit | `StartupDatabaseMaintenance.EnsureVisitOrthoFieldsSchemaAsync` (new) | fixed | yes | yes (post-deploy 200) | no |
| `QA1C-03` | Inventory | `/api/suppliers` 500 — likely enum-type drift on `Suppliers."Type"` (not a missing column) | `SuppliersController.GetAll` | documented | no | n/a | yes |
| `QA1C-04` | Finance | `/api/finance-v3/expenses` 500 — likely enum-type drift on `OperationalExpenses."Category"/"ApprovalStatus"` | `FinanceV3Controller.GetExpenses` | documented | no | n/a | yes |
| `QA1C-05` | Navigation | Full admin sidebar crawl: no blank pages, no unexpected 404/redirect; only 500s were QA1C-01/02 screens | `Sidebar.tsx`, all dashboard routes | verified | n/a | n/a | no |

## Production Owner QA — Round 2 (2026-07-03)

Live production QA (admin, real browser). Root-caused why QA1C-01/02's fixes and
QA1C-03/04's fix (landed separately as QA-602 between rounds) were still visibly
broken in production: a same-day TD-021 refactor (FinanceService decomposition,
PRs #603-#607) had a **squash-merge that silently dropped the actual code**
(`FinanceReadService`/`IFinanceReadService` + 3 call-site updates) for PR #604,
leaving `main` unable to compile since `dotnet build` errored on
`IFinanceService.GetPatientFinanceSummaryAsync` not existing. Every Railway deploy
attempt after that commit failed, so production kept serving a stale image from
*before* QA1C-01/02/QA-602's fixes — that stale image is what QA round 2 actually
tested against, not current `main`.

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `QA2-01` | Finance/Build | `main` failed to build (`CS1061` on `IFinanceService.GetPatientFinanceSummaryAsync`) since TD-021 PR #604's squash merge dropped `FinanceReadService`/`IFinanceReadService` and 3 call-site updates — blocked every Railway deploy since | `FinanceReadService.cs`, `IFinanceReadService.cs` (restored), `PatientJourneyService.cs`, `PaymentsController.cs`, `PatientsController.cs` | fixed (PR #608, merged) | yes | pending (Railway redeploy in progress at time of writing) | no |
| `QA2-02` | Repo hygiene | Same TD-021 merge committed the entire global Claude Code `skills/` directory (1028 files, 61MB) + a stray `tool-results/` debug file into the project | `.gitignore` (+`/skills/`, +`/tool-results/`), 1029 files removed from tracking | fixed (PR #608, merged) | yes | yes (repo size back to normal) | no |
| `QA2-03` | Finance/Schema | `ContractService.GetContractsAsync`/`GetContractByIdAsync` did `c.Patient.FirstName` unconditionally; EF Core's global soft-delete filter nulls `Contract.Patient` (not excludes the row) for a contract whose patient was later soft-deleted, even though the FK is required — latent `NullReferenceException` risk independent of QA1C-01 | `ContractService.cs` (null-guard, `"مريض محذوف"` fallback) | fixed (PR #608, merged) | yes | yes (regression test + full suite green) | no |
| `QA2-04` | Daily operations | `/api/dashboard/stats`, `/api/patient-journey/{id}/daily-summary` (blocks arrival check-in), and `/api/suppliers` all still 500 live — **not new bugs**, these are QA1C-01/02 and QA-602's already-merged fixes on `main`, simply not yet deployed because of QA2-01 | `StartupDatabaseMaintenance.cs` (already fixed on `main`) | already fixed, awaiting deploy | no (no new code needed) | pending Railway redeploy | no |
| `QA2-05` | Appointments | Full booking workflow verified end-to-end live: create patient → book appointment (doctor-schedule-aware slot picker correctly blocked a day the doctor is off) → appointment persists with correct patient/doctor enrichment on `GetById` and range queries | `AppointmentsController.cs`, `appointments/new` page | verified, no bug | n/a | n/a | no |
| `QA2-06` | Daily operations | Arrival check-in (`تسجيل وصول`) surfaces a clear Arabic toast (`فشل إتمام العملية`) on failure rather than failing silently — good UX; underlying failure is QA2-04 | `daily-operations` UI | verified (error handling), blocked by QA2-04 | n/a | pending Railway redeploy | no |
| `QA2-07` | Communication | SignalR WebSocket (`/hubs/messaging`) still fails to connect in production console on every page — same known Railway/Vercel WS-proxy limitation as QA1C; system degrades gracefully (polling), not user-blocking | `useSignalRMessaging.ts`, `useSignalRClinicQueue.ts`, `usePortalSignalR.ts` | documented, unchanged | no | n/a | yes (owner: is realtime push worth a dedicated WS ingress?) |
| `QA2-08` | Appointments UI | Minor: appointment color `<input type="color">` receives an empty string default, producing a harmless browser console warning (`does not conform to #rrggbb`) — cosmetic only | `appointments/new` page | documented, not fixed | no | n/a | no (low severity, optional cleanup) |
| `QA2-09` | Lab | Lab order creation verified end-to-end live: patient search (reasonable terms), lab/work-type/cost selection, save → 201 (`LAB-2026-002`), correct display in list with actions | `LabOrdersController.cs`, `lab/page.tsx` | verified, no bug | n/a | n/a | no |
| `QA2-10` | Finance | Payment collection verified end-to-end live: full form (amount/currency/method/service/doctor/notes), save → 200, receipt `RCP-20260703-001` auto-generated, clear Arabic success toast. **Creates real test financial data** (100 YER) — not deleted, tagged QA in service description + notes | `PaymentsController.cs`, patient payments tab | verified, no bug | n/a | n/a | owner: delete the 100 YER test payment if desired |
| `QA2-11` | Deployment | Railway deploy stall root-caused via API-surface forensics: deployed commit is in the #584–#597 range (refine-landmark route present; #598 Currency fix and #602 enum fix absent — 200/500 discriminator reconfirmed). Stall began at #598 (23:39 Jul 2), **before** the TD-021 build break — corrects QA2-04's attribution. Cause is outside the repo (main is CI-green and buildable); owner checklist for the Railway dashboard written into the QA report | `docs/qa/PRODUCTION_OWNER_QA_ROUND_1.md` (تشخيص توقف نشر Railway) | diagnosed, blocked on Railway dashboard | no (no code fix applicable) | pending first successful Railway deploy | yes (Railway dashboard access required) |

## Production Owner QA — Round 3 (2026-07-04)

Live production QA (admin, real browser). Reconfirmed the Railway deploy stall is
STILL active >24h after round 2's diagnosis (same 200/500 API fingerprints); all
previously-fixed-on-main 500s remain live, which blocks the entire daily-operations
check-in workflow on a day with real appointments. No new test data created —
round 2's QA patient (GM-2026-059) was reused read-only.

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `QA3-01` | Communication | `/referrals` page crashes to the error boundary (`e.map is not a function`) — API returns a `{data,total,page,pageSize}` envelope while the page maps a bare array; silent `catch(() => {})` also hid load failures behind an empty state | `referrals/page.tsx` (envelope unwrap + Arabic error state with retry) | fixed | yes | yes (live crash repro'd pre-fix; tsc/lint/build green) | no |
| `QA3-02` | Daily operations | When `/api/patient-journey/today` fails, the board renders "لا توجد مواعيد" — reception believes the day is empty (silent failure, contra CLAUDE.md) | `daily-operations/page.tsx` (`isError` banner + retry, spec 002 §honest-failure) | fixed | yes | pending (banner visible now; auto-clear after Railway redeploy) | no |
| `QA3-03` | Orthodontics | `/api/ortho-cases/{id}/overview` 500 live → case header KPIs all render "–" silently; same stale-deploy root cause (full `Contracts`+`Payments` materialization hits missing `Currency`) | `OrthoCaseQueryService.cs` (no change needed — heals on deploy) | documented | no | pending Railway redeploy | yes (same Railway action) |
| `QA3-04` | Inventory/Lab-supply | `IsReadSchemaCompatibilityFailure` in Suppliers/Inventory/PurchaseOrders inspected only `InnerException`, so the schema fallback NEVER fired (EF surfaces read `PostgresException` directly; enum drift throws `InvalidCastException`) — dead safety net | 3 controllers hardened to the `LabOrdersController` pattern + `ReadSchemaCompatibilityFailureTests.cs` (15 tests) | fixed | yes | yes (unit-tested; live path also heals on deploy) | no |
| `QA3-05` | Navigation | `/hr` root 404s while listed in `routePermissions` (sidebar only links the 4 sub-pages) | `hr/page.tsx` (redirect → `/hr/attendance`) | fixed | yes | yes | no |
| `QA3-06` | Clinic display | React #418 hydration warning on `/clinic-display` console; screen fully functional | — | documented | no | n/a | no |
| `QA3-07` | Deployment | Stall persists: deployed image still #584–#597 by API fingerprint; dashboard/stats, patient-journey/today, suppliers all still 500 live | — (owner checklist already in QA report) | blocked on owner | no | pending first successful deploy | **yes — urgent** |
| `QA4-01` | Orthodontics | Ortho case header rendered silent "—" dashes in all KPIs when `/api/ortho-cases/{id}/overview` fails (deferred improvement from QA3-03) — misleading during any server fault | `ortho/[id]/page.tsx` (`isError` banner + retry, same pattern as QA3-02) | fixed | yes | yes (behavioral: local frontend w/ fix + live failing production API → banner rendered) | no |
| `QA4-02` | Deployment | Boot blocks listening (and `/health`) on an unbounded ~40-step DDL hotfix pipeline; with the old instance still serving (SignalR polling fallback), blocked `ALTER TABLE` locks can push boot past Railway's 120s healthcheck — a self-perpetuating deploy-stall mechanism whose onset matches #598 exactly | `StartupDatabaseMaintenance.cs` (90s boot budget, env-tunable, background completion + loud logging), `StartupBootBudgetTests.cs` (9 tests) | fixed | yes | pending first real deploy (constrained by the stall itself) | yes (owner should trigger a Redeploy after merge and watch the deploy log) |
| `QA4-03` | Settings/Users | No reachable user-accounts/permissions management screen: `/settings/users`, `/settings/permissions`, `/users` all 404 and the settings hub has no link — functional gap matching the deferred "unified settings screen" priority | — | documented | no | n/a | yes (scope decision) |
| `QA4-04` | Deployment | Round-4 live re-verification: Vercel frontend is CURRENT (#616 `method="post"` present in production HTML) while the backend image is still #584–#597 (same 200/500 discriminators; deployed build's pending-migrations list unchanged) — stall is Railway-only, ~2 days old | `docs/qa/PRODUCTION_OWNER_QA_ROUND_1.md` (الجولة الرابعة) | diagnosed | no | pending first successful deploy | **yes — urgent** |
| `QA4-07` | Deployment | Post-merge live probe: deploy stall IS broken (image ≥ #615 — `schemaFallback:true` fingerprint on /api/suppliers) but the DDL hotfixes leave no trace on the production DB (currency columns still missing, enum types still integer) — the real failure reason is swallowed into Railway-only log warnings, undiagnosable remotely | `StartupHotfixJournal.cs` (new), key hotfix catches wired, `GET /api/finance-v3/diagnostic/startup-hotfixes` (AdminOnly), `StartupHotfixJournalTests.cs` (6 tests) | fixed (diagnosability) | yes | pending next deploy (endpoint then reveals the root cause of the failing ALTERs) | yes (act on the revealed reason — e.g. re-own tables if 42501) |
| `QA4-08` | Deployment | Journal (QA4-07, now live) proved the hotfix pipeline completes in ~2s with ZERO failures — the covered columns are all no-ops — so the surviving 500s (contracts, dashboard/stats, patient-journey/today, expenses) are type drift in columns NO hotfix covers (CashFlow enum columns proven integer; the rest unknowable remotely) | `GET /api/finance-v3/diagnostic/schema-columns` (AdminOnly, information_schema types only for a hardcoded 13-table whitelist — no row data, no user input in SQL) | fixed (diagnosability) | yes | pending next deploy (then the drifted columns are enumerable in one call) | yes (approve the targeted type-normalization heal once named) |

## Production Owner QA — Round 5 (2026-07-05/06)

Live production QA (admin, real browser + direct API isolation). Reconfirmed the
Railway deploy stall is STILL active 3+ days after round 2's diagnosis and after
the #617–#619 boot-budget/diagnostics merges — the same 200/500 API fingerprints
persist, so every round 1–4 fix on `main` has still not reached production. The
full reception journey (intake → queue → call → enter-room → start → complete)
was verified working end-to-end via direct API with a validated state machine and
Arabic messages, proving the first successful deploy restores the reception
screen immediately. One NEW bug was found and fixed; the queue module's UTC-date
family was mapped and scheduled.

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `QA5-01` | Deployment | Deploy stall still live at 2026-07-05 23:00 UTC (same fingerprints: dashboard/stats, patient-journey/{id}/daily-summary, contracts, finance-v3/expenses all 500; finance-v3/contracts 200) | — (owner checklist in QA report round 2 section) | blocked on owner | no | pending first successful deploy | **yes — urgent, unchanged** |
| `QA5-02` | Clinic queue | `StartVisit` stamped `VisitDate` with the UTC server date — live QA visit dated 2026-07-05 while its appointment/clinic day is 2026-07-06 (clinic verifiably operates past midnight) | `ClinicQueueController.cs` (appointment clinic date, fallback `ClinicTimeProvider.ClinicToday()`) | fixed | yes | yes (data-evidenced root cause; suite green) | no |
| `QA5-03` | Clinic queue/Journey | UTC-today family: 11 call sites across `ClinicQueueController` / `CheckoutService` / `PatientJourneyService.GetDailySummary` use `DateTime.UtcNow` dates — internally consistent but cross the clinic day between 00:00–03:00 Yemen | dedicated unification sprint (PR #582 pattern) | documented | no | n/a | scheduling |
| `QA5-04` | Daily operations | Journey action failures show a generic «فشل إتمام العملية» toast, hiding the server's Arabic message (QA3-02 silent-failure family) | daily-operations journey action handlers | documented | no | n/a | no |
| `QA5-05` | API contracts | `POST /api/appointments` response returns empty patientName/doctorName; patients list projection nulls firstName/lastName (UI unaffected — refetches) | `AppointmentsController.cs`, `PatientsController.cs` | documented | no | n/a | no |

## Production Owner QA — Round 6 (2026-07-06)

API-only live verification (browser launch blocked by an environmental proxy
issue this round — see QA6-04). Confirmed the Railway deploy stall (QA2/4/5-01)
is finally resolved, then extended the QA4-08 schema-drift diagnostic to
`JournalEntries`/`JournalLines` and ruled out schema drift as the cause of the
4 still-failing endpoints. No new test data created; read-only API sweep only.

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `QA6-01` | Deployment | Railway deploy stall (QA2/4/5-01) is resolved — fresh boot cycle with zero hotfix failures, timestamped to the #620 merge | `GET /api/finance-v3/diagnostic/startup-hotfixes` live | confirmed resolved | no | yes (live) | no — closed |
| `QA6-02` | Finance/Dashboard | 4 endpoints still 500 live even with a current, healthy deploy: `dashboard/stats`, `contracts?status=active`, `finance-v3/expenses`, `patient-journey/{id}/daily-summary` — root cause is no longer the deploy stall | live probes (200/500 fingerprints unchanged post-fix) | diagnosed (partially) | no | pending Railway log access or local repro | yes — needs either log access or a local dev environment to isolate the exception |
| `QA6-03` | Finance | Extended QA4-08's schema-columns diagnostic to `JournalEntries`/`JournalLines` (the one remaining "previously unknowable" gap its own doc comment flagged) — result: `AccountType`/`FinancialDocumentType` are both `character varying` as code expects, **no drift** | `FinanceV3Controller.cs` (`GetSchemaColumnsDiagnostic` whitelist +2 tables) | fixed (diagnosability) + ruled out | yes | yes (live) | no — this specific theory is closed |
| `QA6-04` | Tooling | Chromium (Playwright) could not reach any external host (including a non-project test host) through this session's configured egress proxy — `ERR_CONNECTION_RESET` on every attempt — blocking all visual/RTL/console-error verification this round | — (environment limitation, not app code) | documented | no | n/a | yes — schedule a round with working browser access for the visual/UX checklist items |

## SEQ-03 — Unified users/permissions settings screen (2026-07-06)

Queue item SEQ-03 (from `MANDATORY_SPRINT_QUEUE.md`). The queue's own note proved
correct on inspection: `UsersTab`/`RolesTab` already exist and work inside the
settings hub's permissions tab — the gap was only that the tab wasn't
deep-linkable and three legacy paths 404'd (QA4-03).

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `SEQ-03` | Settings/Users | `/settings/users`, `/settings/permissions`, `/users` all 404 while the working users/roles UI sits unlinkable behind a state-only tab in the settings hub | `settings/page.tsx` (URL-driven `?tab=` via `useSearchParams`+`Suspense`, finance-v3 pattern), 3 redirect stubs (`settings/users/`, `settings/permissions/`, `users/page.tsx` → `/settings?tab=permissions`, hr-pattern), `routePermissions.ts` (`/users` Admin-only entry), `routePermissions.test.ts` (+1 test, 7 assertions), spec 008 evidence note | fixed | yes | tsc/lint/vitest (180/180)/build all green | no |

## SEQ-08 — Ortho module audit vs specs/004 (2026-07-10)

Read-only audit (per the queue's own warning that `docs/ortho-module/MASTER-PLAN.md`
is stale) against the 5 concrete acceptance rules in
`specs/004-orthodontics-workspace/requirements.md`. 4 of 5 confirmed compliant
with no code change needed; one concrete regression found and fixed.

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `SEQ-08-audit` | Ortho | ORTHO-REQ-001 (single `/ortho` workspace), 003 (AI draft-only), 004 (no FinanceV3 bypass), 005 (existing lab-order system only) — all confirmed compliant by direct code inspection, no violation found | — (read-only audit) | confirmed compliant | no | n/a | no — closed |
| `SEQ-08` | Ortho | ORTHO-REQ-006 (visible Arabic error state + retry on fetch failure, QA4-01's pattern) was only ever applied to `ortho/[id]/page.tsx` — 4 sibling components silently rendered a genuine fetch failure as the same "no data yet" empty state, hiding real server errors from the doctor | `CastAnalysisPanel.tsx`, `CasePresentationPanel.tsx`, `OrthoSurgicalPlanningTab.tsx`, `OrthoTreatmentPlansTab.tsx` + `OrthoCephPanel` (in `OrthoDiagnosisTab.tsx`) — each now renders an `isError`-driven Arabic banner + retry, matching the already-compliant pattern in `ceph/compare`, `ceph/vto`, `LabOrdersPanel` | fixed | yes | tsc/lint/vitest (180/180)/build all green | no |
