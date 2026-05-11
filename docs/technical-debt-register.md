# Technical Debt Register — Aqlan Dental Pro

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

## Details

### TD-010: ClinicQueueItems Safety Net SQL
- **Location**: `backend/src/AqlanDentalPro.API/Program.cs` (~line 1006)
- **Description**: A `CREATE TABLE IF NOT EXISTS "ClinicQueueItems"` block exists as a temporary production safety net in case the EF Core migration fails to apply on Railway. After at least 2 weeks of clean deployments with no "Failed to ensure ClinicQueueItems table" warnings in logs, this entire block should be removed so that the EF migration alone is responsible for schema management.
- **Removal Criteria**: No warning logs for 2+ consecutive weeks on Railway production deployment.
- **Risk**: Low — if the table already exists, the SQL is a no-op. But it duplicates schema logic that should live only in the migration.

### TD-009: StaffOnly Policy (RESOLVED)
- **Description**: Patient portal JWT tokens could access staff endpoints because controllers used bare `[Authorize]` which allowed any authenticated user including Patient role.
- **Fix**: Added `StaffOnly` authorization policy that excludes Patient role. Applied to all 21 staff controllers.
- **Status**: Merged in PR #71. Production verified — patient JWTs get 403 on staff endpoints.

### TD-008: Display Privacy — Full Name Exposure
- **Description**: The `/api/clinic-queue/display` (anonymous, no-auth) endpoint returns the patient's full name (`FirstName + LastName`). While this is standard for clinic calling displays, some clinics may prefer showing only the file number for enhanced privacy.
- **Recommendation**: Consider adding a clinic setting to toggle between "full name" and "file number only" display modes.
