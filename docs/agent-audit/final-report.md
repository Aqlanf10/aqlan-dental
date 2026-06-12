# Final Report — Maintenance & Stabilization Sprint
**التاريخ:** 2026-06-12 · **الفرع:** `claude/wizardly-carson-qys671`

## What Was Done

### 1. Full audit (4 parallel deep audits, code-evidence based)
- `full-system-audit.md` — architecture, stacks, modules, risks
- `finance-audit.md` — Finance V3 (verified safe + 2 bugs fixed)
- `security-audit.md` — auth/authz/secrets/CORS/headers (1 high fix + prioritized open items)
- `daily-operations-audit.md` — journey end-to-end (1 critical fix)
- `pdf-printing-audit.md` — QuestPDF/Arabic fonts/download-print separation
- `technical-debt.md`, `development-roadmap.md`, `manual-smoke-test.md`

### 2. Bugs fixed
| Fix | Area | Files |
|---|---|---|
| "Today" computed in UTC — after 21:00 Yemen time all daily screens showed tomorrow (appointments "disappeared" in evening shift); finance month-start shifted to previous month | Frontend (critical) | `lib/utils.ts` (+`localDateString`), 26 files migrated, 4 new tests |
| Exception `detail`+`type` leaked in 500 responses (debug leftover) | Backend security | 6 sites in Payments/Invoices/Reports/LabOrders controllers; leak-forbidding test |
| `RejectExpense` race (no transaction/lock) vs concurrent approve | Finance | `FinanceV3Controller.cs` — FOR UPDATE + in-lock re-check |
| Treasury outflows could silently go negative | Finance | `TreasuryResolutionService` — configurable guard `finance.prevent_negative_treasury_balance` (warn-only default, Arabic error when enforced), 6 new tests |
| **Fresh install was impossible**: 31/65 migrations invisible to EF (no `[Migration]` attribute), migrations referencing future tables, startup hotfixes pre-creating tables breaking `InitialCreate`, broken `email` index filter, 48 tables missing soft-delete columns → empty-database deployments / disaster recovery could never come up | Backend (critical for DR/new envs) | Empty-DB-only model baseline (`GenerateCreateScript` + history record, advisory-locked); table-existence guards in 2 historical migrations (no-op on migrated DBs); idempotent soft-delete column hotfix; `"Email"` filter fix. **Verified end-to-end: clean install now boots, migrates, seeds, and passes 27/27 live smoke tests** |
| Lab order create 500 when client omits doctorId: `Users.Id` written into `LabOrders.DoctorId` (FK to `Doctors.Id`) | Backend | `LabOrdersController` resolves the current user's Doctor row instead |
| Repo hygiene: 400+ unrelated files, broken submodule gitlinks (broke fresh clones), committed `.env`, 18MB skills pack, logs | Repo | removed from tracking; worklogs → `docs/worklogs/` |
| Unused-var lint warning | Frontend | `BookAppointmentModal.tsx` |

### 3. Re-verified (suspected bugs that were already handled correctly)
Draft-invoice dedup (double-checked locking), handoff state validation, overdue null-safety, draft exclusion from revenue, commission-from-collections with lab/material deductions, cashier-shift gating, refund/cancel atomicity.

## Test Results
| Suite | Result |
|---|---|
| Backend `dotnet build -c Release` | ✅ 0 errors (82 pre-existing warnings) |
| Backend `dotnet test` | ✅ **1318/1318** passed (1309 baseline + 9 new) |
| Frontend `tsc --noEmit` | ✅ clean |
| Frontend `npm run lint` | ✅ no new warnings (2 pre-existing in untouched files) |
| Frontend `vitest run` | ✅ **84/84** passed (80 baseline + 4 new) |
| Frontend `npm run build` | ✅ production build succeeds |
| Manual smoke test (live API + fresh local PostgreSQL 16) | ✅ **27/27** — full patient journey (login×3 → patient → appointment → arrival → queue → call → room → visit → handoff → payment+shift → receipt PDF → checkout → next appt → lab order+PDF → finance/commissions → 401/403 matrix). See `manual-smoke-test.md` |

## Remaining Known Issues
See `technical-debt.md` (TD-A…TD-K). Top three: portal refresh-token storage, doctor↔room assignments, scattered role checks.

## Recommended Next Sprint
Sprint A (session security) then Sprint B (doctor rooms) — see `development-roadmap.md`.

## Deployment Notes
- No new environment variables required. No migrations added. No breaking API changes (only *removed* fields are the accidental `detail`/`type` in error bodies).
- Optional new behavior: enable strict treasury guard by inserting Settings row `finance.prevent_negative_treasury_balance = true` once opening balances are verified.
- Frontend change is behavioral only after 21:00 local (now correct day).

## Rollback Plan
All changes are in normal commits on one branch; `git revert` of any individual commit is safe:
- Treasury guard default is warn-only — reverting changes nothing functionally for deployments that didn't enable the setting.
- Exception-detail removal: revert restores debug fields (not recommended).
- Timezone fix: pure frontend; redeploy previous Vercel build to roll back instantly.
