# Technical Debt Register — Aqlan Dental Pro

> **مؤرشف (سجل تاريخي فقط).** لا يُستخدم هذا الملف لتحديد ترتيب العمل بعد الآن.
> الطابور الملزم الحالي هو `docs/governance/MANDATORY_SPRINT_QUEUE.md` — اقرأه أولًا.
> هذا الملف من حقبة Sprint 7/8A (قديم) وبقي هنا فقط كسجل تاريخي لما أُنجز وقتها.

| ID | Title | Priority | Added | Status |
|----|-------|----------|-------|--------|
| TD-001 | Pre-existing broken test files (renamed to `.disabled`) | Medium | PR #68 | Open |
| TD-002 | No dedicated DTO layer for ClinicQueue controller (anonymous objects + inline request classes) | Low | Sprint 7 | Open |
| TD-003 | Duplicate TV display pages (`/clinic-display` and `/queue-display`) | Medium | Sprint 7 | **Fixed** (Sprint 7B — `/queue-display` now redirects to `/clinic-display`) |
| TD-004 | Room names hardcoded in `ClinicRoomNames.cs` (should move to Settings or DB table) | Low | Sprint 7 | Open |
| TD-005 | No pagination on `/api/clinic-queue/today` (could grow large on busy days) | Low | Sprint 7 | Open |
| TD-006 | Messaging system safety net SQL in Program.cs (similar pattern to TD-010) | Low | Pre-Sprint 7 | Open |
| TD-007 | No unit tests for ClinicQueue controller logic (only entity/model tests exist) | Medium | Sprint 7 | Open |
| TD-008 | Display endpoint returns patient full name — some clinics may prefer file-number-only mode | Low | Sprint 7B | Open |
| TD-009 | Patient portal JWT could access staff endpoints (fixed via StaffOnly policy) | **Critical** | PR #71 | **Fixed** |
| TD-010 | ClinicQueueItems safety net SQL in Program.cs — remove after migration stability confirmed | Medium | Sprint 7B | Open |
| TD-011 | Tracking fields safety net SQL in Program.cs — remove after migration 20260520000000 stability confirmed | Medium | Sprint 7 | Open |
| TD-012 | Voice calling depends on browser SpeechSynthesis and user activation gesture | Low | Sprint 7 | Open |
| TD-013 | Display full patient name may be a privacy consideration; file-number-only mode can be added later | Low | Sprint 7B | Open |
| TD-014 | Startup admin password reset hotfix remains in Program.cs | High | Sprint 8A | Open |
| TD-015 | Large remote branch backlog creates agent confusion and stale-work risk | Medium | Sprint 8A | Open |
| TD-016 | Need a production stability audit document before next feature sprint | Low | Sprint 8A | **Fixed** (Sprint 8A — `docs/sprint-8a-production-stability-audit.md`) |
| TD-017 | Vercel/Railway deployment status should be recorded after each production merge | Low | Sprint 8A | Open |

## Details

### TD-017: Production Deployment Status Recording
- **Description**: After each production merge, the latest GitHub commit status should be checked for both Vercel and Railway and recorded in the relevant sprint report or PR body.
- **Current Sprint 8A status**: After PR #81, both Railway and Vercel reported success for the latest main commit.
- **Recommendation**: Include deployment status in every final PR report before moving to the next sprint.

### TD-016: Sprint 8A Production Stability Audit (RESOLVED)
- **Description**: Before starting a new feature sprint, the project needed a dedicated production stability audit summarizing current deployment status, risks, no-go items, and the next safe work order.
- **Fix**: Added `docs/sprint-8a-production-stability-audit.md`.
- **Status**: Fixed in Sprint 8A documentation PR.

### TD-015: Remote Branch Backlog
- **Description**: The repository contains many remote branches from prior feature, fix, polish, recovery, and agent-generated workflows. A large branch backlog increases the risk of stale PRs being reopened or agents working from obsolete branches.
- **Recommendation**: Prepare a reviewed branch deletion list in a separate cleanup step. Do not delete `main`, `stable-*`, `release-*`, or recovery branches without explicit review.
- **Risk**: Medium — organizational risk, not direct runtime risk.

### TD-014: Startup Admin Password Reset Hotfix
- **Location**: `backend/src/AqlanDentalPro.API/Program.cs`
- **Description**: A one-time admin password reset block remains in startup logic. It appears guarded by a Settings flag, but it is sensitive legacy production recovery logic.
- **Recommendation**: Do not modify during feature work. Before removal, verify production admin access and confirm the reset flag state in production.
- **Risk**: High if changed carelessly; low if left untouched and documented.

### TD-011: Tracking Fields Safety Net SQL
- **Location**: `backend/src/AqlanDentalPro.API/Program.cs` (after existing ClinicQueueItems safety net)
- **Description**: A safety net block adds `AddedByUserId`, `CalledByUserId`, `Notes` columns to `ClinicQueueItems` and migrates data from the old `CalledBy` column. This is needed because Railway deployments may fail to apply EF migrations reliably.
- **Removal Criteria**: No warning logs for 2+ consecutive weeks on Railway production deployment after migration `20260520000000` is applied.
- **Risk**: Low — all operations are idempotent (IF NOT EXISTS checks).

### TD-010: ClinicQueueItems Safety Net SQL
- **Location**: `backend/src/AqlanDentalPro.API/Program.cs` (~line 1006)
- **Description**: A `CREATE TABLE IF NOT EXISTS "ClinicQueueItems"` block exists as a temporary production safety net in case the EF Core migration fails to apply on Railway. After at least 2 weeks of clean deployments with no "Failed to ensure ClinicQueueItems table" warnings in logs, this entire block should be removed so that the EF migration alone is responsible for schema management.
- **Removal Criteria**: No warning logs for 2+ consecutive weeks on Railway production deployment.
- **Risk**: Low — if the table already exists, the SQL is a no-op. But it duplicates schema logic that should live only in the migration.

### TD-012: Voice Calling Browser Dependency
- **Description**: The Arabic voice calling feature on `/clinic-display` uses the browser Web Speech API (`window.speechSynthesis`). This has several limitations:
  - Browsers may block speech until the user clicks a button (autoplay policy)
  - Not all browsers support Arabic voices
  - Voice availability varies by device/OS
  - The feature degrades gracefully — visual display continues without voice
- **Recommendation**: Consider a server-side TTS solution (e.g., Google Cloud TTS, Azure Speech) for more reliable Arabic voice output in a future sprint.

### TD-009: StaffOnly Policy (RESOLVED)
- **Description**: Patient portal JWT tokens could access staff endpoints because controllers used bare `[Authorize]` which allowed any authenticated user including Patient role.
- **Fix**: Added `StaffOnly` authorization policy that excludes Patient role. Applied to all staff controllers.
- **Status**: Merged in PR #71. Production verified — patient JWTs get 403 on staff endpoints.

### TD-004: Hardcoded Room Names
- **Description**: Room names are hardcoded in `ClinicRoomNames.cs` as `["غرفة 1", "غرفة 2", "غرفة 3"]`. This makes it impossible for clinic staff to add/rename rooms without a code change.
- **Recommendation**: Move rooms to a `ClinicRooms` database table or Settings, and provide a management UI.
