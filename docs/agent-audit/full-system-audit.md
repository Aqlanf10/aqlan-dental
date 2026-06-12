# Full System Audit — Aqlan Dental Pro
**التاريخ:** 2026-06-12 · **الفرع:** `claude/wizardly-carson-qys671` · **أساس التدقيق:** فحص فعلي للكود المصدري (ليس افتراضات)

> مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان

---

## 1. Architecture Summary

Monorepo with two applications plus docs:

```
backend/   ASP.NET Core 8 Web API — Clean Architecture (4 projects)
  src/AqlanDentalPro.API/             Controllers, Middleware, Hubs (SignalR), Authorization
  src/AqlanDentalPro.Application/     DTOs, Validators, Service interfaces
  src/AqlanDentalPro.Domain/          Entities, Enums, Constants
  src/AqlanDentalPro.Infrastructure/  EF Core (PostgreSQL), Services, Repositories, Seed
  tests/AqlanDentalPro.UnitTests/     1300+ xUnit tests (InMemory EF provider)
frontend/  Next.js 14 (App Router) + TypeScript + Tailwind, Arabic RTL
  src/app/(dashboard)/   Staff screens (patient-journey, finance-v3, lab, appointments…)
  src/app/(portal)/      Patient portal
  src/app/(public)/      Public booking site
docs/      Roadmaps, sprint reports, audits, technical debt register
```

- **Deployment:** Vercel (frontend) + Railway (backend + PostgreSQL), Dockerfile included.
- **Realtime:** SignalR hub with branch-scoped pushes (queue updates, patient-called events).
- **PDF:** QuestPDF (Community) with Noto Naskh Arabic fonts bundled in `backend/Fonts/`.
- **Currency:** Yemeni Rial (YER). UI fully Arabic RTL.

## 2. Backend Stack
.NET 8 · EF Core + Npgsql · JWT auth (access 15min / refresh 7d HTTP-only cookie) · SignalR · QuestPDF · FluentValidation · Rate limiting · xUnit + Moq + FluentAssertions.

## 3. Frontend Stack
Next.js 14 · TypeScript (strict, `tsc --noEmit` clean) · Tailwind CSS · React Query · Zustand stores · axios with 401-refresh queueing · Vitest (80 tests) · Playwright config present.

## 4. Database Structure (key aggregates)
- **Patients** (normalized phones, soft delete, unique indexes), Appointments, Visits, ClinicQueueItems (queue state machine), ClinicRooms, ClinicServices.
- **Finance V3:** Invoices + InvoiceLineItems (with per-line doctor commission, lab/material/other direct costs), Payments, Contracts, CashierSessions, Treasuries (Vault/Bank per branch), CashFlowTransactions (dual-write), JournalEntries + JournalLines (canonical double-entry ledger), OperationalExpenses, SupplierBills, AdvancePayments, Salaries.
- **Lab:** Labs, LabWorkTypes, LabWorkPrices (unique per lab+worktype), LabOrders (+Items, +Attachments, +StatusHistory), LabPayables (lab debts → finance).
- **Other:** Users/Roles/RolePermissions, BookingRequests, Messages, OrthoCases, Prescriptions, Settings (key/value), AuditLogs.
- EF Core migrations under Infrastructure; startup maintenance guarded by PostgreSQL advisory lock (`DB_MAINTENANCE_LOCK_KEY`).

## 5. Existing Modules (verified working)
Patients · Appointments (conflict detection) · Daily Operations / Patient Journey (intake → queue → room → visit → handoff → checkout) · Clinic Queue + TV display · Finance V3 (invoices, payments, cashier shifts, treasuries, journal, expenses, commissions, contracts) · Lab (master data, catalog, prices, orders, payables, reports) · PDF (receipt, invoice, financial statement, lab order) · Reports (P&L, daily ops) · Messaging + WhatsApp webhook (HMAC validated) · Patient portal · Public booking (reCAPTCHA + honeypot) · HR (attendance, salaries) · Ortho · Settings (key/value + controller).

## 6. Missing / Incomplete Modules
- **Settings UI coverage:** entity + controller exist, but most financial rules are read from config or entity fields; a full settings admin screen covering consultation fee, commission rules, debt rules is partial. (Roadmap: see development-roadmap.md)
- **Doctor↔Room assignment:** no `DoctorRoom` entity; doctors are restricted to *patients* via `PatientAccessService` (primary doctor/appointment/visit/referral links) but not to *rooms*.
- **Inventory/Purchase orders:** schema-tolerant reads added recently (PR #344) — module is transitional.

## 7. Broken / Risky Areas Found (and status)
| Finding | Severity | Status |
|---|---|---|
| UTC date used for "today" across 30+ frontend screens (Yemen UTC+3 ⇒ after 21:00 the daily screens show tomorrow) | **High** | **Fixed** — `localDateString()` helper, all call sites migrated |
| Exception `detail` + `type` leaked in PDF/lab 500 responses (debug leftovers, commit d07db58) | **High (security)** | **Fixed** — 6 sites cleaned, test inverted to forbid leaks |
| `RejectExpense` had no transaction/row-lock → approve/reject race could double-process an expense | Medium | **Fixed** — same `FOR UPDATE` pattern as ApproveExpense |
| Treasury outflows never check balance → negative treasury possible silently | Medium | **Fixed** — central configurable guard (warn-only by default, see finance-audit.md) |
| Repo contained 400+ unrelated files (`skills/`, `download/`, `railway_logs.json`, committed `.env`, 4 broken submodule gitlinks breaking fresh clones) | Medium (hygiene) | **Fixed** — removed from tracking |
| Patient-portal refresh token kept in localStorage (staff refresh token is HTTP-only cookie) | Medium | Open — documented in security-audit.md |
| Manual `User.IsInRole` checks scattered in a few controllers alongside centralized policies | Low | Open — documented in technical-debt.md |

## 8. Security Risks
See `security-audit.md`. Summary: JWT validation solid (issuer/audience/lifetime/key, zero clock skew); 401/403 separation correct; CORS explicit origins (no wildcard); rate limiting + lockout on auth; security headers middleware; webhook HMAC with fixed-time compare; production fail-fast on placeholder secrets. Main open items: portal token storage, doctor-room restrictions absent, access token in localStorage (XSS surface).

## 9. Finance Risks
See `finance-audit.md`. Verified safe: draft invoices excluded from revenue; commission from actual collections with lab/material cost deduction before doctor share; open cashier shift required for payments/refunds/commission payouts/cash expenses; refunds & cancellations transactional. Fixed this sprint: expense reject race, treasury balance guard.

## 10. Daily Operations Risks
See `daily-operations-audit.md`. The journey is implemented end-to-end with advisory-lock concurrency protection and Arabic error messages everywhere (no silent failures found). The one critical bug (UTC "today") is fixed. Draft-invoice dedup and handoff state validation already existed (re-verified).

## 11. PDF / Printing Risks
See `pdf-printing-audit.md`. Arabic font registration is correct ("Noto Naskh Arabic" with multi-path fallback, thread-safe). Download/print strictly separated on the frontend with static-analysis tests. Receipt is compact 105×148mm with clinic header/footer. Fixed: exception detail leak in error responses.

## 12. UI/UX Problems
- Daily screens date filtering bug (fixed).
- Patient journey page is feature-complete; relies on 60s polling + manual refresh rather than SignalR on that page (acceptable; improvement candidate).
- Some screens dense (reports page ~1300 lines single file) — refactor candidates, not blockers.

## 13. Code Quality
- Backend: clean architecture respected; 82 compiler warnings (mostly CS8602 nullable in tests) — no errors.
- Frontend: tsc strict clean; 1 lint warning (unused `_patientId`) — fixed.
- Large controllers (PatientJourneyController ~2000 lines, LabOrdersController ~1300) — split candidates.

## 14. Test Coverage Gaps
- 1309 backend tests + 80 frontend tests, all green.
- Gaps: PatientJourney endpoint integration tests, timezone regression tests (added in this sprint for utils), concurrency stress tests, refund↔commission interaction.

## 15. Deployment Risks
- `--warnaserror:CS9113` only in CI; consider widening.
- Railway/Vercel must keep `NEXT_PUBLIC_API_URL`, JWT secret, connection string, `ADMIN_DEFAULT_PASSWORD` set (Program.cs fail-fast protects against placeholders).
- Startup migration guarded by advisory lock — safe for multi-instance.

## 16. Recommended Roadmap
See `development-roadmap.md`.
