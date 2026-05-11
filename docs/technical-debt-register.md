# Technical Debt Register

**Last Updated**: 2026-05-11
**Context**: Post PR #68 and PR #69 stability verification

---

## Resolved Items

### TD-001: Duplicated Startup SQL Hotfix Blocks in Program.cs
- **Resolved In**: PR #68
- **Description**: Multiple unconditional SQL execution blocks were added to `Program.cs` during Sprint 6 to fix admin login and migration issues on Railway. These blocks ran on every startup without protection, creating race conditions when multiple instances started simultaneously.
- **Resolution**: Consolidated into a single advisory-lock-guarded block gated by `ENABLE_STARTUP_DB_MAINTENANCE` environment variable. The PostgreSQL advisory lock (`pg_advisory_lock`) ensures only one instance executes maintenance at a time, and the environment variable gate allows controlled enablement.

### TD-002: Startup DB Maintenance Not Enabled on Railway
- **Resolved In**: PR #68 (environment variables set post-merge)
- **Description**: The advisory-lock-guarded maintenance block introduced in PR #68 was disabled on Railway because `ENABLE_STARTUP_DB_MAINTENANCE` was not set as an environment variable. This meant pending migrations and maintenance tasks were not running on deployment.
- **Resolution**: Added `ENABLE_STARTUP_DB_MAINTENANCE=true` and `DB_MAINTENANCE_LOCK_KEY=918273645` to the Railway backend service environment. Verified that the maintenance block executes and the advisory lock is acquired and released safely on startup.

### TD-003: AuditLogs FK Constraint Violation for PatientAccounts
- **Resolved In**: PR #69
- **Description**: `AuditLogMiddleware` assumed every authenticated principal has a `UserId` that exists in the `Users` table. Patient portal accounts are stored in the `PatientAccounts` table, not `Users`. When a patient portal user performed actions (e.g., sending messages), the middleware inserted their `PatientAccount.Id` into `AuditLogs.UserId`, violating the `FK_AuditLogs_Users_UserId` foreign key constraint and causing HTTP 500 errors.
- **Resolution**: Modified `AuditLogMiddleware` to detect patient portal principals (via claims such as `role=Patient` or `portal=true`). For patient portal requests, `UserId` is set to `null` and identity information is stored in the audit log's `Details` metadata field. Staff and admin audit logging continues to use `UserId` references to the `Users` table as before. No schema changes were required.

### TD-004: Forbid() Returning 400 Instead of 403
- **Resolved In**: PR #68
- **Description**: When the JWT authentication middleware encountered an authenticated user without the required role/policy, it returned HTTP 400 (Bad Request) instead of the semantically correct HTTP 403 (Forbidden). This confused clients and made authorization debugging harder.
- **Resolution**: Added `DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme` to the JWT authentication configuration in `Program.cs`. This ensures `Forbid()` correctly returns HTTP 403 with the appropriate challenge response.

### TD-005: No Safe Branch Cleanup Process
- **Resolved In**: PR #68
- **Description**: After multiple sprints and hotfixes, numerous merged remote branches accumulated in the repository with no documented process for safe cleanup.
- **Resolution**: Created `scripts/list-merged-branches.sh` (with `--delete` mode behind interactive confirmation) and `docs/branch-cleanup.md` documentation. The script excludes protected branches (`main`, `HEAD`, `stable-*`, `release-*`) and always fetches with `--prune` before listing.

---

## Remaining Items

### TD-006: AuditLogMiddlewareTests.cs.disabled Should Be Re-enabled
- **Priority**: Medium
- **Description**: The audit log middleware test file was disabled (renamed with `.disabled` extension) during the PR #69 hotfix to avoid breaking the test project build. The tests need to be updated to reflect the new patient portal audit logging behavior and then re-enabled as part of a test project refactor.
- **Recommendation**: Re-enable and update `AuditLogMiddlewareTests.cs.disabled` after the test project is refactored to properly support middleware testing with mock authentication contexts. Ensure tests cover both staff/admin audit logging (UserId populated) and patient portal audit logging (UserId null, identity in metadata).

### TD-007: Old GitHub Branches Need Manual Review and Cleanup
- **Priority**: Low
- **Description**: Several merged remote branches remain in the repository. While `scripts/list-merged-branches.sh` can identify them, manual review is recommended before deletion to ensure no branch contains unmerged work or is being used as a reference.
- **Recommendation**: Run `./scripts/list-merged-branches.sh` periodically and review the output before using `--delete` mode. Coordinate with the team to confirm no one is actively working on a listed branch.

### TD-008: Avoid Manual SQL Hotfixes Unless Emergency-Only
- **Priority**: Ongoing
- **Description**: The Sprint 6 experience showed that adding raw SQL blocks to `Program.cs` for emergency fixes creates significant technical debt. These blocks are hard to test, can cause race conditions, and make the codebase harder to maintain.
- **Recommendation**: All database changes should go through the EF Core migration system. Direct SQL in `Program.cs` should only be used for genuine emergencies (e.g., production down, data corruption) and must be removed or consolidated in the next available PR. The advisory-lock-guarded maintenance block from PR #68 provides a safer pattern for any future startup maintenance needs.

### TD-009: Patient Portal JWT May Access Staff Endpoints
- **Priority**: Medium (Security)
- **Description**: During post-PR69 verification, it was observed that patient portal JWT tokens may allow access to some staff endpoints (e.g., `GET /api/patients` returns 200 with data). This suggests that some controller endpoints may lack proper authorization policies or the patient's linked `User` record may grant unintended access.
- **Recommendation**: Audit all API controllers for authorization policies. Ensure every staff-only endpoint has `[Authorize(Policy = "AdminOnly")]`, `[Authorize(Policy = "StaffAccess")]`, or equivalent. Patient portal tokens should only be able to access `/api/portal/*` endpoints. This should be addressed in a dedicated security review sprint.

---

## Debt Prevention Guidelines

1. **Never add unconditional SQL to Program.cs** — Use the advisory-lock-guarded maintenance block gated by `ENABLE_STARTUP_DB_MAINTENANCE`.
2. **Always consider multi-table authentication** — When adding middleware or services that reference `Users`, account for `PatientAccounts` as a separate identity context.
3. **Test authorization policies** — Every new endpoint must have an explicit `[Authorize]` policy; never rely on default behavior.
4. **Clean up branches regularly** — Run the merged branch listing script after each sprint and clean up stale branches.
5. **Document hotfixes** — Every emergency fix must be documented in this register and followed up with a proper implementation in the next PR.
