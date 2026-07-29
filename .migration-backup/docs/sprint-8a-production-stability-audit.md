# Sprint 8A — Production Stability & Cleanup Audit

Date: 2026-05-12
Branch: `chore/sprint-8a-production-stability-cleanup`
Scope: Documentation and planning only. No runtime code, authentication, database schema, migrations, or deployment settings are changed in this PR.

## Current Production Status

After PR #81 was merged into `main`:

| Check | Status | Notes |
|---|---|---|
| GitHub PR backlog | Clean | No open PRs after closing stale PRs #70 and #77 and merging #81. |
| Railway backend deployment | Success | Latest main commit reported success. |
| Vercel frontend deployment | Success | Latest main commit reported success. |
| Last merged PR | #81 | Frontend-only clinic display voice announcement wording update. |
| Database/schema changes in #81 | None | No backend, auth, DB, schema, or migration changes. |

## Why Sprint 8A Exists

The project is now functional enough to continue feature work, but several stability risks should be controlled before starting large new modules. The goal of Sprint 8A is to protect production from accidental breakage and prepare a clean path for the next feature sprint.

## Confirmed Strengths

- The repository currently has no open PRs.
- Staff-only API protection was already strengthened in PR #71 using the `StaffOnly` policy.
- The clinic queue workflow is documented and production-oriented.
- The display screen is intentionally public but privacy-limited.
- Frontend and backend deployments are currently successful.
- The current technical debt register exists and is actively maintained.

## Main Stability Risks

### 1. Startup database maintenance remains complex

`backend/src/AqlanDentalPro.API/Program.cs` still contains startup database maintenance and safety-net SQL logic. Some of this exists because Railway migrations previously drifted from production. This is understandable, but it should be reduced carefully over time.

Rules for future cleanup:

- Do not remove any safety-net SQL without production log evidence that the related EF migration has been stable for at least two weeks.
- Do not add new unconditional SQL hotfixes to `Program.cs`.
- Prefer EF migrations for schema changes.
- Any unavoidable production safety net must be idempotent, gated, documented, and tracked in this file and `technical-debt-register.md`.

### 2. Admin password reset hotfix remains in startup

There is a one-time admin reset block in `Program.cs`. It appears guarded by a Settings flag, but it is still sensitive and should be treated as legacy production recovery logic.

Rules:

- Do not modify this block during feature work.
- Do not change admin credentials in code.
- If removal is planned, first verify production admin access and the reset flag state.

### 3. Test coverage needs controller-level strengthening

The technical debt register already records that ClinicQueue has entity/model tests but lacks sufficient controller logic tests. This matters because queue actions are workflow-heavy: call, enter room, start visit, complete, cancel, and duplicate prevention.

Recommended test additions:

- Unauthenticated staff endpoints return 401.
- Patient portal JWT cannot access staff queue endpoints.
- Duplicate active queue items are blocked.
- `start` creates or links a visit once only.
- `display` remains anonymous and privacy-safe.
- Room changes are only allowed for valid statuses.

### 4. Room management is currently hardcoded

Rooms are currently fixed as `غرفة 1`, `غرفة 2`, and `غرفة 3`. This is acceptable for the current clinic setup but should become configurable in a later sprint.

Recommended next feature sprint:

- Add `ClinicRooms` or Settings-backed room configuration.
- Add UI for Admin/Reception to add, rename, disable, and order rooms.
- Keep backward compatibility with the current three default rooms.

### 5. Branch backlog is large

The repository still contains many remote branches from prior sprints, fixes, experiments, and recovery work. This creates confusion for agents and increases the risk of someone reopening stale work.

Rules:

- Do not delete branches automatically in this PR.
- Keep `main`, `stable-*`, `release-*`, and recovery branches unless explicitly reviewed.
- Prepare a reviewed deletion list in a separate cleanup PR or manual admin step.

## Next Safe Work Order

1. Keep this PR documentation-only.
2. Merge it after review.
3. Run a separate branch cleanup review.
4. Add ClinicQueue controller tests.
5. Only then start the next feature sprint: configurable clinic rooms.

## Explicit No-Go Items for Sprint 8A

- No schema changes.
- No new EF migrations.
- No auth/password changes.
- No changes to patient portal JWT behavior.
- No direct production database operations.
- No deletion of branches in this PR.
- No UI redesign.
- No new feature modules.

## Recommended Sprint 8B

**Sprint 8B — Configurable Clinic Rooms**

Goal: Replace hardcoded room names with manageable settings while preserving the existing default rooms.

Suggested deliverables:

- Backend: room source via Settings or `ClinicRooms` table.
- Frontend: Admin/Reception room management screen.
- ClinicQueue: use active room list.
- ClinicDisplay: use room display names safely.
- Tests: room validation and fallback behavior.
