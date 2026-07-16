# Core System Current State

- Status: Phase 0 audit baseline
- Baseline date: 2026-07-17
- Baseline commit: `73a8c3e4` (`origin/main`)
- Spec: `specs/011-core-system-stabilization/`

## 1. Executive Summary

The repository builds and its automated unit/component suites pass, but green CI is
not yet evidence that the clinic's authenticated patient journey works end to end.
The highest risks are cross-route visit duplication, queue authorization and API
contract gaps, migration ownership split between EF and startup SQL, incomplete
multi-currency reporting, and low backend coverage.

Cephalometry is preserved in draft PRs #697, #698, and #699 and is paused until the
Phase 12 return gate. Phase 0 makes no product or migration changes.

## 2. Baseline And Open Work

- `main` was synchronized at `73a8c3e4` before this audit branch was created.
- Open PRs are #697, #698, and #699. All are draft, cephalometry-only, and marked
  **Paused — Resume After Core System Stabilization**.
- PR #697 has green backend, frontend, encoding, Vercel, and nominal E2E checks.
  PRs #698 and #699 currently show successful Vercel checks only because they are
  stacked on #697 rather than based directly on `main`.
- No non-cephalometry implementation PR was open at the audit time.

## 3. Automated Verification Baseline

| Check | Result | Notes |
|---|---:|---|
| Backend Release build | Pass | 0 errors, 109 warnings |
| Backend unit tests | Pass | 2,429 / 2,429 |
| Frontend type check | Pass | A stale `.next` reference from another branch was removed before the clean rerun |
| Frontend lint | Pass with warnings | Unused symbols, two hook dependency/ref warnings, and image warnings |
| Frontend tests | Pass | 383 / 383 |
| Frontend production build | Pass | Next.js build completed on the clean `main` worktree |
| Authenticated patient-journey E2E | Not proven | `Needs runtime verification` |

The GitHub E2E job exits green when `E2E_API_URL` is absent. Therefore a green CI
workflow does not prove that Playwright authenticated flows ran. Backend coverage
enforcement is also `continue-on-error: true`; comments in `.github/workflows/ci.yml`
record an approximate baseline of 6% lines and 28% branches with safety floors of
4% and 26%.

## 4. Canonical Module Ownership

| Capability | Canonical UI owner | Current assessment |
|---|---|---|
| Patients | `/patients` and patient file | Active; duplicate checks are partial |
| Appointments | `/appointments`, `/schedule`, recall | Active; Reception access conflicts across layers |
| Check-in and queue | `/daily-operations` | Active; queue contract and priority controls need repair |
| Doctor clinic | `/doctor-clinic` and visit APIs | Active; cross-entry idempotency needs a DB guarantee |
| Lab | `/lab` and subroutes | Active; strong core lifecycle, missing requested intermediate states |
| Finance | `/finance-v3` and checkout integration | Active; multi-currency reporting/printing is incomplete |
| Orthodontics | `/ortho` in the patient context | Active but must stabilize before advanced AI/VTO |
| Cephalometry | Orthodontic case context | Existing module remains available; new work paused |

The hidden `/clinic-command-center` is an Admin overlay, not the canonical dashboard.
`/lab/dashboard` is a lab summary, not a second global dashboard.

## 5. Routes And Navigation

Compatibility redirects currently include:

- `/clinic-queue` -> `/daily-operations?tab=queue`
- `/patient-journey` -> `/daily-operations`
- `/patient-journey/[patientId]` -> the patient file
- `/users`, `/settings/users`, and `/settings/permissions` -> unified settings
- `/ortho-surgical` -> `/ortho`

These redirects are acceptable only while the destination remains canonical and
tests prevent new links from targeting the aliases. Route ownership is duplicated
between `Sidebar.tsx`, `routePermissions.ts`, page guards, and backend policies.

Confirmed mismatch: backend `AppointmentAccess` includes Reception, but both the
`/appointments` sidebar item and route guard exclude Reception. This obstructs the
owner-directed appointments workflow even though recall and daily operations allow
Reception.

## 6. Permissions And Auditability

`PermissionGuard` supports only `view`, `create`, `edit`, `delete`, `export`, and
`approve`. The target operating model also needs explicit permissions for queue
reordering/priority, payment collection, refunds, discounts, overrides, printing,
and settings management.

`StaffOnly` means any authenticated identity that is not in the Patient role. The
queue controller relies on that broad policy. Its priority and reorder endpoints do
not apply a granular permission guard. Emergency requires a reason and writes an
audit record, but it has no required doctor/manager approval; other priority and
ordering changes are not audited.

Severity: **High security and accountability gap**. Runtime exploitation was not
attempted; authorization behavior beyond static policy evidence is marked
`Needs runtime verification`.

## 7. Patient Integrity

The patient model has filtered unique indexes for normalized phone and WhatsApp.
The duplicate endpoint checks normalized phone, WhatsApp, patient number, and name
with optional birth date. The form blocks strong phone/number matches and can allow
name-only warnings.

No identity-number match or reviewed, permission-gated merge workflow was found.
All modules generally reference the canonical patient ID, but complete cross-module
integrity remains an end-to-end verification item.

## 8. Appointments And Capacity

Appointments have a non-unique composite index on doctor/date/start time. Conflict
validation exists in application code, but the audit has not proven complete rules
for concurrent booking, doctor leave, holidays, room capacity, grace periods, and
overbooking. Yemen clinic-date helpers exist in important paths, but date behavior
across all scheduling routes is not yet covered by one deterministic suite.

Severity: **High workflow risk**, with capacity and concurrency behavior marked
`Needs runtime verification`.

## 9. Daily Operations And Queue

Confirmed defects:

- Backend reorder expects a raw `List<ReorderItemRequest>`; the frontend sends an
  object containing `orders`. Reordering is expected to fail model binding.
- The enum and UI expose `VIP`, which conflicts with `CORE-REQ-010`.
- The frontend sends no reason when Emergency is selected, while the backend
  requires one, so the action is expected to fail.
- Reorder and priority changes have no granular server permission.
- Queue ordering uses priority before sort order/time, so it is not strict FIFO.

The database does have a filtered unique index preventing two active queue items
for the same patient and clinic date. Privacy of the public waiting display is
`Needs runtime verification`.

Severity: **High functional/security gap**.

## 10. Doctor Clinic And Visit Lifecycle

Both the appointment and queue routes check for an existing visit and use PostgreSQL
advisory locks. However, the appointment route locks the appointment ID while the
queue route locks the queue-item ID. Concurrent calls through the two routes can
therefore enter different critical sections. `VisitConfiguration` has no unique
filtered index on active `AppointmentId`.

Invoices similarly have non-unique indexes on `VisitId` and `AppointmentId`; there
is no database guarantee of one active draft invoice per visit. Application checks
reduce ordinary duplicates but do not replace a cross-route data constraint.

Severity: **Critical data-integrity risk**. A controlled concurrency reproduction
against a disposable PostgreSQL database is required before the repair PR.

## 11. Laboratory

The lab module already enforces a useful lifecycle:
`draft -> sent -> manufacturing -> tryIn/ready -> received -> delivered`, with
returned/remake/cancelled branches, status history, overdue queries, daily alerts,
ready notifications, receipt-before-delivery, and remake fields.

Gaps against the target workflow include explicit `Confirmed` and
`Patient Appointment Needed` states, documented manager escalation behavior,
patient price/payable completeness, and an end-to-end test proving that final
installation cannot be booked before receipt without an audited override.

Severity: **Medium feature-completeness gap**; core lifecycle is not missing.

## 12. Finance And Currencies

Entities preserve invoice/contract/treasury currency and payments preserve received
currency, account currency, exchange rate, and applied amount. YER/SAR/USD paths and
currency-specific treasuries exist.

Material gaps remain:

- Several aggregate queries sum contract/invoice/account values without grouping
  by their account currency or returning an explicit conversion rate.
- Journal lines do not carry an explicit currency, limiting auditability of some
  consolidated journal KPIs.
- Invoice and statement PDFs still hardcode `r.y.` equivalents in Arabic for totals;
  invoice currency is ignored in those lines.
- At least one payment notification formats all collections as YER.

Severity: **High financial-reporting risk**. Mixed-currency fixtures and reconciliation
tests are required before relying on consolidated totals.

## 13. Settings, Identity, Language, And Printing

Finance PDFs resolve clinic text identity from settings, but logo rendering uses a
static bundled `Fonts/logo.png` cached for the process lifetime. The sidebar and root
metadata also hardcode the clinic name/logo.

The language settings page stores module preferences, but its own source states that
only orthodontic presentation output currently honors them. The application root is
fixed to Arabic RTL, the API client sends Arabic, and no app-wide localization layer
was found. Print language is fragmented across specialized English print components
rather than independently controlled by one print contract.

Severity: **High architecture gap** for Phase 2.

## 14. Migrations And Startup Schema Ownership

There are 93 timestamped migration classes. Only 16 contain an explicit `[Migration]`
attribute, and two files share prefix `20260604000000`. A non-migration helper class
also lives in the migration directory.

`StartupDatabaseMaintenance.cs` is approximately 5,330 lines and performs extensive
raw DDL plus direct deletion/insertion in `__EFMigrationsHistory`. Historical audits
describe this as deliberate production reconciliation, so Phase 0 does not alter it.
It nevertheless creates two schema owners: EF migrations and startup maintenance.

Severity: **Critical operational risk** for deployment and fresh/legacy database
parity. The existing phased-removal plan in
`docs/agent-audit/c-08-startup-maintenance-audit.md` must be followed, not bypassed.

## 15. Hardcoded Business Rules And Technical Debt

- Clinic identity, logos, Arabic labels, and some currency symbols are hardcoded.
- Queue priority and ordering rules are encoded in enum/controller/UI instead of a
  centrally governed operating-rule setting.
- Role lists are repeated in sidebar, route guards, page guards, and policies.
- Startup schema repair is a large, production-critical module with broad ownership.
- The backend build reports 109 warnings, including nullable dereference risks in
  finance reports, lab, and reporting paths, plus obsolete xmin API usage.
- Frontend lint warns about stale hook dependencies/ref cleanup and unused symbols.

## 16. Test Gaps

Raw test counts are healthy, but backend line coverage is very low and the threshold
is non-blocking. Missing proof includes:

- Authenticated full journey: patient -> appointment -> check-in -> queue -> visit ->
  lab -> invoice/payment -> next appointment.
- Two-device/repeated-click visit and invoice idempotency.
- Queue reorder wire contract and granular permission matrix.
- Mixed YER/SAR/USD invoice, payment, refund, cashier, treasury, report, and PDF cases.
- Migration parity for fresh, representative legacy, and production-like schemas.
- Arabic RTL, English LTR, and independently selected print language.

## 17. Severity-Ranked Findings

| ID | Severity | Finding | Owning phase |
|---|---|---|---:|
| CORE-F-001 | Critical | Cross-route visit lock mismatch and no unique active appointment visit | 7, with incident-priority exception |
| CORE-F-002 | Critical | EF/startup SQL share schema ownership and rewrite migration history | 0/1 deployment safety track |
| CORE-F-003 | High | Queue reorder contract mismatch | 6, or incident-priority exception |
| CORE-F-004 | High | Queue priority/reorder lack granular permission and full audit | 3/6 |
| CORE-F-005 | High | VIP and emergency UI conflict with required queue policy | 6 |
| CORE-F-006 | High | Reception appointment access differs across server/sidebar/guard | 1 |
| CORE-F-007 | High | Multi-currency aggregates and PDFs do not consistently preserve currency meaning | 9 |
| CORE-F-008 | High | Identity/language/printing sources are fragmented and partly hardcoded | 2 |
| CORE-F-009 | High | Authenticated E2E can be skipped while CI remains green | 0/1 |
| CORE-F-010 | High | Backend coverage threshold is low and non-blocking | Cross-phase |
| CORE-F-011 | Medium | Patient duplicate matching lacks identity number and reviewed merge | 4 |
| CORE-F-012 | Medium | Lab lacks some target states/escalation proof | 8 |

## 18. Phase 0 Decision

Phase 0 can close after this report, the priority plan, and the execution checklist
are reviewed in a draft PR. The first Phase 1 slice is intentionally small:

`CORE-P1-S1` aligns Reception access to the existing canonical `/appointments` route
across sidebar and route guard, adds route-access tests, and does not widen backend
authorization because `AppointmentAccess` already includes Reception.

Critical findings remain visible and may be handled only through a documented
incident-priority exception or their owning phase. No cephalometry work resumes.
