# TD-020: Raw SQL Inventory & Classification

**Created:** 2026-05-13
**Production commit:** `e51d98e4b6e8` (after PR #99 merge)
**Status:** Phase C1-d — B10/B11/B12/B13 conversation schema converted to EF migration

---

## Executive Summary

| Metric | Count |
|--------|-------|
| Files containing raw SQL | 2 (Program.cs + ClinicQueueController.cs) |
| Total `ExecuteSqlRawAsync` calls in Program.cs | 41 (was 45 — B10/B11/B12/B13 removed) |
| Total `CreateCommand()` / `CommandText` in Program.cs | 3 pairs (6 lines) |
| Total raw SQL in Program.cs | **42** (was 46 — B10/B11/B12/B13 removed) |
| Total raw SQL in ClinicQueueController.cs | **2** (advisory locks — KEEP) |
| Total backend raw SQL | **44** (was 48) |
| Blocks active WITHOUT env gate (ungated) | 4 (A1-A4: admin password reset) |
| Blocks gated by `ENABLE_STARTUP_DB_MAINTENANCE` | 39 (B1, B4-B7, B14-B47 — B2/B3/B8/B9/B10/B11/B12/B13 removed) |

**SQL Injection verdict:** No exploitable vectors found. All interpolated values are either `int` from configuration or hardcoded `string[]` arrays — none derive from user input.

---

## Files Containing Raw SQL

| # | File | Blocks | Guard | Category |
|---|------|--------|-------|----------|
| 1 | `src/AqlanDentalPro.API/Program.cs` | **50** | Ungated (A1-A4, 4 blocks) + `ENABLE_STARTUP_DB_MAINTENANCE` (B1-B47, 47 blocks) | Startup maintenance + Admin setup |
| 2 | `src/AqlanDentalPro.Infrastructure/Data/Seed/DbSeeder.cs` | 0 (was 4) | `ENABLE_STARTUP_DB_MAINTENANCE` (via caller) | ~~Seeder / schema drift hotfix~~ **Eliminated in Phase B2** |
| 3 | `src/AqlanDentalPro.API/Controllers/MessagesController.cs` | 0 (was 31) | `[Authorize(Policy = "AdminOnly")]` | ~~Messaging schema hotfix~~ **Eliminated in Phase B1** |
| 4 | `src/AqlanDentalPro.API/Controllers/ClinicQueueController.cs` | 2 | `[Authorize(Policy = "StaffOnly")]` | Advisory locks |

---

## Category Breakdown

### Category 1: Startup DB Maintenance / Schema Drift Hotfix

**File:** `Program.cs`
**Guard:** `ENABLE_STARTUP_DB_MAINTENANCE` (lines 464-1291)
**Blocks:** B1-B47 (47 blocks)
**Currently active in production:** NO (env var defaults to `false`)

| # | Line | Purpose | Risk | Action | Phase C0 Notes |
|---|------|---------|------|--------|---------------|
| B1 | 482 | Advisory lock acquisition (`pg_try_advisory_lock`) | Low | C — Keep as advisory lock | Must keep. Uses interpolated `lockKey` from config (int type — no injection risk). |
| B2 | ~~521-533~~ | ~~`ALTER TABLE ... ADD COLUMN "DeletedAt"/"DeletedBy"` — loop over 39 tables~~ | Medium | ~~A — Convert to EF migration~~ | **Removed in Phase C1-a.** Replaced by EF migration `20260522000000_AddSoftDeleteColumnsToLegacyTables`. |
| B3 | ~~507-517~~ | ~~`ADD COLUMN "NormalizedPhone"/"NormalizedWhatsApp" TO "Patients"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-b.** Replaced by EF migration `20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes`. |
| B4 | 553 | `UPDATE "Patients" SET "NormalizedPhone" = CASE ...` (phone normalization) | Medium | E — Keep until Phase C1-e verified | **Legacy data backfill.** Backfills `NormalizedPhone` from `Phone` for existing patients where `Phone` is set but `NormalizedPhone` is NULL. Do NOT remove until production verification confirms zero rows needing backfill OR a replacement idempotent data migration is applied (see Phase C1-e). |
| B5 | 575 | `UPDATE "Patients" SET "NormalizedWhatsApp" = CASE ...` (WhatsApp normalization) | Medium | E — Keep until Phase C1-e verified | **Legacy data backfill.** Same as B4 but for `NormalizedWhatsApp`/`WhatsApp`. Do NOT remove until production verification or replacement migration (see Phase C1-e). |
| B6 | 598 | Deduplicate `NormalizedPhone` (complex CTE) | Low | E — Keep until B4/B5 strategy complete | **Dedup safety guard.** NULLs duplicate `NormalizedPhone` values (oldest/first row kept) before unique index creation. Keep until B4/B5 backfill strategy is complete and production verification confirms no duplicates remain. |
| B7 | 609 | Deduplicate `NormalizedWhatsApp` (complex CTE) | Low | E — Keep until B4/B5 strategy complete | **Dedup safety guard.** Same as B6 but for `NormalizedWhatsApp`. Keep until B4/B5 strategy is complete. |
| B8 | ~~586-590~~ | ~~`CREATE UNIQUE INDEX "IX_Patients_NormalizedPhone"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-b.** Replaced by EF migration `20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes`. |
| B9 | ~~591-595~~ | ~~`CREATE UNIQUE INDEX "IX_Patients_NormalizedWhatsApp"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-b.** Replaced by EF migration `20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes`. |
| B10 | ~~578-590~~ | ~~`ADD COLUMN "ConversationType"/"PatientId"/"BranchId" TO "Conversations"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-d.** Replaced by EF migration `20260524000000_AddConversationPatientBranchFieldsAndIndexes`. |
| B11 | ~~592-594~~ | ~~`CREATE INDEX "IX_Conversations_PatientId"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-d.** Replaced by EF migration `20260524000000_AddConversationPatientBranchFieldsAndIndexes`. |
| B12 | ~~595-597~~ | ~~`CREATE INDEX "IX_Conversations_ConversationType"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-d.** Replaced by EF migration `20260524000000_AddConversationPatientBranchFieldsAndIndexes`. |
| B13 | ~~599-607~~ | ~~`ADD FK "FK_Conversations_Patients_PatientId"`~~ | Low | ~~A — Convert to EF migration~~ | **Removed in Phase C1-d.** Replaced by EF migration `20260524000000_AddConversationPatientBranchFieldsAndIndexes`. |
| B14 | 670 | `INSERT INTO "__EFMigrationsHistory" '20260501000000_AddNormalizedPhoneFields'` | Medium | D — Delete as obsolete |
| B15 | 670 | `INSERT INTO "__EFMigrationsHistory" '20260501010000_AddPatientConversationSupport'` | Medium | D — Delete as obsolete |
| B16 | 677 | `INSERT INTO "__EFMigrationsHistory" '20260501020000_AddSoftDeleteToMessagingTables'` | Medium | D — Delete as obsolete |
| B17 | 696 | `CREATE TABLE "PatientAccounts" (...)` | High | A — Convert to EF migration |
| B18 | 715 | `ADD FK "FK_PatientAccounts_Patients_PatientId"` | Low | A — Convert to EF migration |
| B19 | 723 | `CREATE UNIQUE INDEX "IX_PatientAccounts_PatientId"` | Low | A — Convert to EF migration |
| B20 | 727 | `CREATE UNIQUE INDEX "IX_PatientAccounts_PhoneNumber"` | Low | A — Convert to EF migration |
| B21 | 731 | `INSERT INTO "__EFMigrationsHistory" '20260430120000_AddPatientPortal'` | Medium | D — Delete as obsolete |
| B22 | 738 | `INSERT INTO "__EFMigrationsHistory" '20260430140000_AddWhatsAppIntegration'` | Medium | D — Delete as obsolete |
| B23 | 745 | `INSERT INTO "__EFMigrationsHistory" '20260430160000_AddGeneralDentistryEnhancements'` | Medium | D — Delete as obsolete |
| B24 | 762 | `ADD COLUMN "Username"/"PasswordHash"/"PasswordSalt"/"InitialPassword" TO "PatientAccounts"` | High | A — Convert to EF migration |
| B25 | 778 | `CREATE UNIQUE INDEX "IX_PatientAccounts_Username"` | Low | A — Convert to EF migration |
| B26 | 792 | `ADD COLUMN "Diagnosis"/"NextVisitPlan" TO "Visits" + indexes` | Medium | A — Convert to EF migration |
| B27 | 808 | `ADD COLUMN "FileName"/"FileSize"/"MimeType"/"Notes"/"UploadedBy" TO "Documents"` | Medium | A — Convert to EF migration |
| B28 | 833 | `INSERT INTO "__EFMigrationsHistory" '20260502000000_AddVisitsDocumentsFields'` | Medium | D — Delete as obsolete |
| B29 | 851 | `ADD COLUMN "RoomName"/"ArrivedAt"/"CalledAt"/"InRoomAt" TO "Appointments"` | Medium | A — Convert to EF migration |
| B30 | 872 | `INSERT INTO "__EFMigrationsHistory" '20260502100000_AddQueueFieldsToAppointments'` | Medium | D — Delete as obsolete |
| B31 | 890 | `CREATE TABLE "DoctorSchedules" (...)` | High | A — Convert to EF migration |
| B32 | 909 | `ADD FK "FK_DoctorSchedules_Doctors_DoctorId"` | Low | A — Convert to EF migration |
| B33 | 918 | `CREATE UNIQUE INDEX "IX_DoctorSchedules_DoctorId_DayOfWeek"` | Low | A — Convert to EF migration |
| B34 | 925 | `INSERT INTO "__EFMigrationsHistory" '20260502120000_AddDoctorSchedules'` | Medium | D — Delete as obsolete |
| B35 | 957 | `CREATE TABLE "Conversations" (...)` — fallback block | High | A — Convert to EF migration |
| B36 | 1000 | `CREATE TABLE "ConversationParticipants" (...)` — fallback block | High | A — Convert to EF migration |
| B37 | 1027 | `CREATE TABLE "Messages" (...)` — fallback block | High | A — Convert to EF migration |
| B38 | 1061 | `CREATE TABLE "MessageReads" (...)` — fallback block | High | A — Convert to EF migration |
| B39 | 1087 | `INSERT INTO "__EFMigrationsHistory" '20260430000000_AddMessagingSystem'` | Medium | D — Delete as obsolete |
| B40 | 1118-1147 | `CREATE TABLE "ClinicQueueItems" (...)` + 2 indexes | High | A — Convert to EF migration | **Do NOT remove yet.** Safety net for Railway. Requires stable EF migration replacement first (TD-010). |
| B41 | 1149 | `ADD FKs for ClinicQueueItems` | Low | A — Convert to EF migration |
| B42 | 1171 | `INSERT INTO "__EFMigrationsHistory" '20260514000000_AddClinicQueueItem'` | Medium | D — Delete as obsolete |
| B43 | 1193 | `ADD COLUMN "AddedByUserId"/"CalledByUserId"/"Notes" TO "ClinicQueueItems"` | Medium | A — Convert to EF migration |
| B44 | 1209-1216 | Data migration: move "CalledBy" → "CalledByUserId", then DROP COLUMN | Medium | E — Keep temporarily, then D | **Contains DROP COLUMN** — destructive. Already applied in production. Do NOT remove until migration 20260520000000 is confirmed stable. |
| B45 | 1218 | `ADD FKs "FK_ClinicQueueItems_Users_AddedByUserId"` etc. | Low | A — Convert to EF migration |
| B46 | 1232 | `INSERT INTO "__EFMigrationsHistory" '20260520000000_AddClinicQueueItemTrackingFields'` | Medium | D — Delete as obsolete |
| B47 | 1278 | Advisory lock release (`pg_advisory_unlock`) | Low | C — Keep as advisory lock |

### Category 2: Messaging Schema Hotfix (Admin Endpoint)

**File:** `MessagesController.cs`
**Guard:** `[Authorize(Policy = "AdminOnly")]` on `EnsureSchema()` endpoint
**Blocks:** M0-M30 (31 blocks)
**Currently active in production:** YES (admin-only HTTP endpoint, not called on startup)

| # | Line | Purpose | Risk | Action |
|---|------|---------|------|--------|
| M0 | 49 | `SELECT EXISTS (tables)` via `CreateCommand()` | Low | D — Delete with entire endpoint |
| M1 | 32 | `ALTER TABLE ... ADD COLUMN "DeletedAt"/"DeletedBy"` — loop 4 tables | Low | D — Delete (duplicate of B2) |
| M2 | 60 | `ADD COLUMN "ConversationType"/"PatientId"/"BranchId"` | Low | D — Delete (duplicate of B10) |
| M3 | 74 | `CREATE INDEX "IX_Conversations_PatientId"` | Low | D — Delete (duplicate of B11) |
| M4 | 77 | `CREATE INDEX "IX_Conversations_ConversationType"` | Low | D — Delete (duplicate of B12) |
| M5 | 81 | `ADD FK "FK_Conversations_Patients_PatientId"` | Low | D — Delete (duplicate of B13) |
| M6 | 95 | `ADD COLUMN "IsEdited"/"EditedAt" TO "Messages"` | Low | D — Delete |
| M7 | 110 | `CREATE TABLE "Conversations" (...)` | Low | D — Delete (duplicate of B35) |
| M8 | 124 | `CREATE INDEX "IX_Conversations_LastMessageAt"` | Low | D — Delete |
| M9 | 129 | `ADD COLUMN "ConversationType"/"PatientId"/"BranchId"` | Low | D — Delete (duplicate of M2) |
| M10 | 143 | `CREATE INDEX "IX_Conversations_PatientId"` | Low | D — Delete (triplicate) |
| M11 | 147 | `CREATE INDEX "IX_Conversations_ConversationType"` | Low | D — Delete (triplicate) |
| M12 | 151 | `ADD FKs for Conversations` | Low | D — Delete (duplicate) |
| M13 | 168 | `CREATE TABLE "ConversationParticipants" (...)` | Low | D — Delete (duplicate of B36) |
| M14 | 182 | `CREATE UNIQUE INDEX "IX_ConversationParticipants_..."` | Low | D — Delete |
| M15 | 187 | `ADD FKs for ConversationParticipants` | Low | D — Delete |
| M16 | 200 | `CREATE TABLE "Messages" (...)` | Low | D — Delete (duplicate of B37) |
| M17 | 217 | `CREATE INDEX "IX_Messages_ConversationId"` | Low | D — Delete |
| M18 | 221 | `CREATE INDEX "IX_Messages_CreatedAt"` | Low | D — Delete |
| M19 | 225 | `ADD FKs for Messages` | Low | D — Delete |
| M20 | 242 | `CREATE TABLE "MessageReads" (...)` | Low | D — Delete (duplicate of B38) |
| M21 | 254 | `CREATE UNIQUE INDEX "IX_MessageReads_..."` | Low | D — Delete |
| M22 | 259 | `ADD FKs for MessageReads` | Low | D — Delete |
| M23 | 273 | `INSERT INTO "__EFMigrationsHistory"` — messaging | Medium | D — Delete (duplicate of B39) |
| M24 | 282 | Multi-column `ADD COLUMN` for Conversations + Messages | Low | D — Delete (duplicate) |
| M25 | 308 | `CREATE INDEX "IX_Conversations_PatientId"` | Low | D — Delete (triplicate) |
| M26 | 311 | `CREATE INDEX "IX_Conversations_ConversationType"` | Low | D — Delete (triplicate) |
| M27 | 315 | `ADD FK "FK_Conversations_Patients_PatientId"` | Low | D — Delete (triplicate) |
| M28 | 324 | `INSERT INTO "__EFMigrationsHistory"` — patient conversation | Medium | D — Delete (duplicate of B15) |
| M29 | 333 | `ADD COLUMN "IsEdited"/"EditedAt" TO "Messages"` | Low | D — Delete (duplicate of M6) |
| M30 | 344 | `INSERT INTO "__EFMigrationsHistory"` — message editing | Medium | D — Delete |

### Category 3: Seeder / Admin Setup

**File:** `DbSeeder.cs`
**Guard:** `ENABLE_STARTUP_DB_MAINTENANCE` (via Program.cs caller)
**Blocks:** S1-S4 (4 blocks) → **0 after Phase B2**
**Currently active in production:** NO

| # | Line | Purpose | Risk | Action | Status |
|---|------|---------|------|--------|--------|
| S1 | ~~24~~ | `ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "PasswordSalt"` | Low | A — Convert to EF migration | ✅ Done (Phase B2) |
| S2 | ~~28~~ | `UPDATE "Patients" SET "Phone" = '' WHERE ... NOT IN (dedup)` | Low | E — Keep temporarily, then D | ✅ Done (Phase B2) |
| S3 | ~~44~~ | `CREATE UNIQUE INDEX "IX_Patients_Phone"` | Low | A — Convert to EF migration | ✅ Done (Phase B2) |
| S4 | ~~53~~ | `CREATE UNIQUE INDEX "IX_Patients_WhatsApp"` | Low | A — Convert to EF migration | ✅ Done (Phase B2) |

**Phase B2 resolution:** All 4 blocks replaced by EF migration `20260521000000_AddPasswordSaltAndPatientPhoneIndexes`. The migration:
- Adds `PasswordSalt` column to `Users` with `DEFAULT ''` (matching the original raw SQL behavior)
- Deduplicates `Phone` values before creating the unique index (preserving S2 logic)
- Creates `IX_Patients_Phone` unique filtered index
- Creates `IX_Patients_WhatsApp` unique filtered index
- Updates `AppDbContextModelSnapshot` to include the Phone/WhatsApp indexes

### Category 4: Clinic Queue Advisory Locks

**File:** `ClinicQueueController.cs`
**Guard:** `[Authorize(Policy = "StaffOnly")]`
**Blocks:** Q1-Q2 (2 blocks)
**Currently active in production:** YES

| # | Line | Method | Purpose | Risk | Action |
|---|------|--------|---------|------|--------|
| Q1 | 102 | `AddToQueue()` | `SELECT pg_advisory_xact_lock({0})` — queue concurrency | Low | C — Keep as advisory lock |
| Q2 | 558 | `CheckIn()` | `SELECT pg_advisory_xact_lock({0})` — queue concurrency | Low | C — Keep as advisory lock |

### Category 5: Admin Password Reset (Ungated Startup)

**File:** `Program.cs`
**Guard:** Idempotent via Settings table flag (no env var gate)
**Blocks:** A1-A4 (4 blocks)
**Currently active in production:** YES (runs at every startup)

| # | Line | Purpose | Risk | Action |
|---|------|---------|------|--------|
| A1 | 312 | `DO $$ ... CREATE TABLE "Settings" IF NOT EXISTS ...` | Medium | A — Convert to EF migration |
| A2 | 332 | `SELECT COUNT(*) FROM "Settings"` via `CreateCommand()` | Low | A — Replace with EF LINQ |
| A3 | 348 | `UPDATE "Users" SET "PasswordHash"/"PasswordSalt" WHERE "Username"='admin'` | High | B — Convert to `ExecuteSqlAsync` with FormattableString |
| A4 | 354 | `INSERT INTO "Settings" ... 'admin.password.reset.2026' ...` | Medium | A — Convert to EF migration or EF LINQ |

---

## Risk Summary by Action

| Action | Count | Description |
|--------|-------|-------------|
| **A** — Convert to EF migration | ~40 | Schema changes that should be proper EF Core migrations |
| **B** — Convert to parameterized `ExecuteSqlAsync` | 1 | Non-schema SQL needing safe parameterization |
| **C** — Keep as advisory lock | 3 | PostgreSQL advisory locks (concurrency, not schema) |
| **D** — Delete as obsolete | ~37 | Duplicated blocks in MessagesController, fake migration history entries |
| **E** — Keep temporarily with guard | 4 | Data backfills that may be needed once more |

---

## Top 10 Highest-Risk Blocks (Phase B Priority)

| Rank | Block | File | Line | Risk | Reason |
|------|-------|------|------|------|--------|
| 1 | B17 | Program.cs | 696 | High | `CREATE TABLE "PatientAccounts"` — no EF migration exists |
| 2 | B31 | Program.cs | 890 | High | `CREATE TABLE "DoctorSchedules"` — no EF migration exists |
| 3 | B40 | Program.cs | 1117 | High | `CREATE TABLE "ClinicQueueItems"` — no EF migration exists |
| 4 | A3 | Program.cs | 348 | High | Admin password reset — writes sensitive data, no env gate |
| 5 | B24 | Program.cs | 762 | High | Adds auth columns (`PasswordHash`, `PasswordSalt`) to PatientAccounts |
| 6 | B35 | Program.cs | 957 | High | `CREATE TABLE "Conversations"` — fallback, no EF migration |
| 7 | B36 | Program.cs | 1000 | High | `CREATE TABLE "ConversationParticipants"` — no EF migration |
| 8 | B37 | Program.cs | 1027 | High | `CREATE TABLE "Messages"` — no EF migration |
| 9 | B38 | Program.cs | 1061 | High | `CREATE TABLE "MessageReads"` — no EF migration |
| 10 | B4 | Program.cs | 553 | Medium | Bulk `UPDATE "Patients"` with complex CASE normalization |

---

## Blocks That Must NOT Be Converted

| Block | File | Line | Reason |
|-------|------|------|--------|
| B1 | Program.cs | 482 | `pg_try_advisory_lock` — PostgreSQL concurrency primitive, not schema |
| B47 | Program.cs | 1278 | `pg_advisory_unlock` — paired with B1 |
| Q1 | ClinicQueueController.cs | 102 | `pg_advisory_xact_lock` — queue concurrency control |
| Q2 | ClinicQueueController.cs | 558 | `pg_advisory_xact_lock` — queue concurrency control |

These 4 blocks use PostgreSQL advisory locks for concurrency control and cannot be expressed as EF migrations. They should be kept as raw SQL but parameterized with `FormattableString` for type safety.

---

## Duplicate Analysis

The `EnsureSchema()` endpoint in `MessagesController.cs` (31 blocks) is **nearly 100% duplicated** from the startup maintenance block in `Program.cs`. Once proper EF migrations are created, both the entire `EnsureSchema()` endpoint and the corresponding startup blocks can be deleted.

---

## Migration Roadmap

### Phase C1: Convert Gated Schema Blocks to EF Migrations (Safe — env=false in prod)
Create formal EF Core migrations for all schema changes in the gated block (B2-B46). These blocks are **not active in production** (`ENABLE_STARTUP_DB_MAINTENANCE=false`), making this the safest phase.

**Recommended order (lowest risk first):**
1. **C1-a:** Soft-delete columns (B2) — 34 tables, already applied in production, simple ADD COLUMN
2. **C1-b:** NormalizedPhone/NormalizedWhatsApp columns + indexes (B3, B8, B9) — already applied
3. **C1-c:** Conversation columns + indexes + FK (B10-B13) — already applied
4. **C1-d:** PatientAccounts table + columns + indexes + FK (B17-B20, B24-B25) — high priority, no proper migration exists
5. **C1-e:** PatientAccounts username/password columns (B24, B25) — auth-related, high priority
6. **C1-f:** Visits/Documents columns + indexes (B26-B27) — already applied
7. **C1-g:** Appointments queue columns (B29) — already applied
8. **C1-h:** DoctorSchedules table + FK + index (B31-B33) — no proper migration exists
9. **C1-i:** Messaging tables fallback (B35-B38) — already created by `MigrateAsync()`, fallback only
10. **C1-j:** ClinicQueueItems table + tracking fields + FKs (B40-B45) — **do last** (TD-010 safety net)
11. **C1-k:** Delete all fake migration history entries (B14-B16, B21-B23, B28, B30, B34, B39, B42, B46)

**Blocks to keep in Program.cs during C1:**
- B1/B47: Advisory lock/unlock (structural — needed as long as any gated block remains)
- B4/B5: Data backfills (one-time — may still need to run on staging)
- B6/B7: Deduplication (one-time — safe to delete after C1-b confirms indexes exist)
- B44: Data migration with DROP COLUMN (destructive — keep until migration 20260520000000 stability confirmed)

### Phase C2: Refactor Ungated Admin Password Reset (Higher risk — runs every startup)
The admin password reset block (A1-A4) runs **unconditionally at every startup**. This is the most sensitive block:
- A1: CREATE TABLE Settings IF NOT EXISTS → Convert to EF migration
- A2: SELECT COUNT(*) via CreateCommand → Replace with EF LINQ
- A3: UPDATE Users password via ExecuteSqlRawAsync → Keep but parameterize with FormattableString
- A4: INSERT Settings flag → Replace with EF LINQ

**Risk:** This block touches admin credentials. Must be carefully tested. Consider keeping the idempotent flag-check logic but moving it to DbSeeder (gated by `ENABLE_STARTUP_DB_MAINTENANCE`).

### Phase C3: Clean Up Remaining Blocks
- Delete all gated blocks that have been converted to migrations (B2-B46)
- Remove advisory lock/unlock (B1/B47) if no gated blocks remain
- Remove `ENABLE_STARTUP_DB_MAINTENANCE` config check if no gated blocks remain
- Remove `DbSeeder.SeedAsync(db, logger)` call if it becomes empty

### Phase D: Remove Obsolete Blocks
Delete all raw SQL that has been superseded by EF migrations:
- All 31 blocks in `MessagesController.EnsureSchema()` endpoint — **already done in Phase B1**
- All fake migration history entries — done in Phase C1-k
- Deduplication queries (B6, B7) — done in Phase C1-b

### Phase E: Final Production Verification
- Enable `ENABLE_STARTUP_DB_MAINTENANCE=true` on staging
- Verify no schema drift between fresh DB and upgraded DB
- Remove the startup maintenance block entirely from Program.cs
- Remove `ENABLE_STARTUP_DB_MAINTENANCE` config key
- Confirm `dotnet ef database update` handles all schema changes
- Final smoke test

---

## Phase B2: DbSeeder Raw SQL Elimination (2026-05-13)

**Branch:** `td-020-phase-b2-dbseeder-raw-sql`
**Migration:** `20260521000000_AddPasswordSaltAndPatientPhoneIndexes`

### What Changed

| Item | Before | After |
|------|--------|-------|
| DbSeeder `ExecuteSqlRawAsync` calls | 4 | 0 |
| DbSeeder raw SQL lines | ~35 | 0 |
| EF migrations added | 0 | 1 |

### Blocks Removed

| Block | Original Line | Purpose | Replacement |
|-------|--------------|---------|-------------|
| S1 | 24 | `ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "PasswordSalt"` | `migrationBuilder.Sql(DO $$ ... IF NOT EXISTS ... ADD COLUMN)` (idempotent) |
| S2 | 28-35 | Deduplicate `Phone` values before unique index | `migrationBuilder.Sql(UPDATE "Patients" SET "Phone" = '' WHERE ...)` (same logic, re-runnable) |
| S3 | 38-44 | `CREATE UNIQUE INDEX "IX_Patients_Phone"` | `migrationBuilder.Sql(CREATE UNIQUE INDEX IF NOT EXISTS)` (idempotent) |
| S4 | 47-53 | `CREATE UNIQUE INDEX "IX_Patients_WhatsApp"` | `migrationBuilder.Sql(CREATE UNIQUE INDEX IF NOT EXISTS)` (idempotent) + WhatsApp dedup |

### Files Changed
1. `src/AqlanDentalPro.Infrastructure/Data/Seed/DbSeeder.cs` — Removed 4 `ExecuteSqlRawAsync` calls, replaced with comment referencing migration
2. `src/AqlanDentalPro.Infrastructure/Data/Migrations/20260521000000_AddPasswordSaltAndPatientPhoneIndexes.cs` — New EF migration
3. `src/AqlanDentalPro.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` — Added `HasIndex("Phone")` and `HasIndex("WhatsApp")` entries
4. `docs/technical-debt/TD-020-raw-sql-inventory.md` — Updated with Phase B2 results

### Migration Idempotency
All operations in this migration are safe for databases where the old DbSeeder raw SQL already applied these changes:
- **PasswordSalt column:** Uses `DO $$ ... IF NOT EXISTS (SELECT 1 FROM information_schema.columns ...)` guard
- **Phone dedup:** `UPDATE ... WHERE` with idempotent condition — affects 0 rows if no duplicates
- **Phone index:** Uses `CREATE UNIQUE INDEX IF NOT EXISTS` (PostgreSQL 9.5+)
- **WhatsApp dedup:** Same pattern as Phone — deduplicates before index creation
- **WhatsApp index:** Uses `CREATE UNIQUE INDEX IF NOT EXISTS`
- **Down():** Uses `DROP INDEX IF EXISTS` and `DROP COLUMN IF EXISTS`

### Production Behavior Impact
**None.** The migration applies the exact same schema changes that the raw SQL was doing:
- `PasswordSalt` column: guarded by `IF NOT EXISTS` — safe if already present
- Phone/WhatsApp indexes: guarded by `IF NOT EXISTS` — safe if already present
- Phone/WhatsApp deduplication: re-runnable — affects 0 rows if no duplicates exist
- WhatsApp deduplication added as new safety measure (old DbSeeder did not dedup WhatsApp)

### Important Notes
- `DbSeeder.cs` is gated by `ENABLE_STARTUP_DB_MAINTENANCE` in production — it does NOT run on normal startup
- The `MigrateAsync()` call inside DbSeeder will now apply this migration when maintenance mode is enabled
- No changes to `Program.cs` startup maintenance blocks (A1-A4, B1-B47) per scope rules
- No changes to `ClinicQueueController.cs` advisory locks (Q1, Q2) per scope rules
- No auth/password behavior changes
- Raw SQL moved from DbSeeder.cs (runtime) into guarded EF migration (schema-only) — this is the correct architectural location

---

## Phase C0: Program.cs Raw SQL Risk Review (2026-05-13)

**Purpose:** Full risk review of all remaining raw SQL in Program.cs before any code changes.
**Branch:** `td-020-phase-c0-program-raw-sql-review` (docs-only — no code changes)
**Scope:** Review-only. No code, schema, migration, auth, or environment variable changes.

### Current State After Phase B2

| Metric | Value |
|--------|-------|
| Production commit | `e51d98e4b6e8` |
| CI status | All green (287/287 tests) |
| Backend tests | 287/287 passing |
| DbSeeder raw SQL | 0 |
| MessagesController raw SQL | 0 |
| ClinicQueueController raw SQL | 2 (advisory locks — KEEP) |
| Program.cs raw SQL | **50** |
| Total backend raw SQL | **52** |

### Production Guard Verification

| Guard | Value | Status |
|-------|-------|--------|
| `ENABLE_STARTUP_DB_MAINTENANCE` | `false` (default) | Confirmed — gated block (B1-B47) does NOT run |
| Admin password reset (A1-A4) | No env gate — always runs | Confirmed — runs at every startup but is idempotent via Settings flag |

### Blocks Active Even With ENABLE_STARTUP_DB_MAINTENANCE=false

These 4 blocks run **unconditionally** on every startup:

| Block | Line | Purpose | Risk Assessment |
|-------|------|---------|----------------|
| A1 | 312 | CREATE TABLE "Settings" IF NOT EXISTS | Low risk — idempotent, only creates table if missing |
| A2 | 330-338 | SELECT COUNT(*) check for reset flag | Low risk — read-only query |
| A3 | 348-351 | UPDATE "Users" SET password for admin | **High risk** — writes sensitive data, but guarded by flag (only runs once) and uses parameterized `{0}`/`{1}` |
| A4 | 354-357 | INSERT reset flag into Settings | Low risk — idempotent via flag check |

**Important:** Block A3 is the only block that **modifies credentials**. It is:
- Idempotent (only runs if Settings flag does not exist)
- Parameterized (uses `{0}` and `{1}` placeholders, not string interpolation)
- Non-destructive (sets a new password, does not delete existing data)
- Expected to have already fired in production (flag should already be set)

### Full Block Classification

#### Category A — Ungated Startup / Admin Setup (4 blocks, ACTIVE in production)

| Block | Line | SQL Type | Purpose | Risk | Recommended Action | Why |
|-------|------|----------|---------|------|-------------------|-----|
| A1 | 312 | Literal (raw string) | CREATE TABLE "Settings" IF NOT EXISTS | Medium | Convert to EF migration | Table should be part of the EF model |
| A2 | 330-338 | CreateCommand/CommandText | SELECT COUNT(*) flag check | Low | Replace with EF LINQ | Unnecessary raw SQL for a simple count query |
| A3 | 348-351 | Parameterized (`{0}`, `{1}`) | UPDATE admin password | **High** | Keep temporarily, move to DbSeeder | Must remain until admin password is properly managed; move to gated DbSeeder in Phase C2 |
| A4 | 354-357 | Literal (raw string) | INSERT reset flag | Medium | Replace with EF LINQ | Unnecessary raw SQL for a simple insert |

#### Category B — ENABLE_STARTUP_DB_MAINTENANCE Gated (47 blocks, INACTIVE in production)

##### B-I: Schema — Column Additions (Convert to EF migration)

| Block | Line(s) | Tables Affected | Risk | Phase |
|-------|---------|----------------|------|-------|
| B2 | 521-533 | 34 tables (DeletedAt/DeletedBy) | Medium | C1-a |
| B3 | 541-551 | Patients (NormalizedPhone/NormalizedWhatsApp) | Low | C1-b |
| B10 | 631-644 | Conversations (ConversationType/PatientId/BranchId) | Low | C1-c |
| B24 | 762-778 | PatientAccounts (Username/PasswordHash/PasswordSalt/InitialPassword) | **High** | C1-d |
| B26 | 792-806 | Visits (Diagnosis/NextVisitPlan) + indexes | Medium | C1-f |
| B27 | 808-831 | Documents (FileName/FileSize/MimeType/Notes/UploadedBy) + indexes | Medium | C1-f |
| B29 | 851-870 | Appointments (RoomName/ArrivedAt/CalledAt/InRoomAt) + index | Medium | C1-g |
| B43 | 1193-1206 | ClinicQueueItems (AddedByUserId/CalledByUserId/Notes) | Medium | C1-j |

##### B-II: Schema — Table Creations (Convert to EF migration)

| Block | Line(s) | Table | Risk | Phase |
|-------|---------|-------|------|-------|
| B17 | 696-715 | PatientAccounts | **High** | C1-d |
| B31 | 890-908 | DoctorSchedules | **High** | C1-h |
| B35 | 958-999 | Conversations (fallback) | High | C1-i |
| B36 | 1000-1026 | ConversationParticipants (fallback) | High | C1-i |
| B37 | 1028-1060 | Messages (fallback) | High | C1-i |
| B38 | 1062-1085 | MessageReads (fallback) | High | C1-i |
| B40 | 1118-1147 | ClinicQueueItems + 2 indexes | **High** | C1-j (do last) |

##### B-III: Schema — Indexes (Convert to EF migration)

| Block | Line(s) | Index | Risk | Phase |
|-------|---------|-------|------|-------|
| B8 | 620-624 | IX_Patients_NormalizedPhone (unique, filtered) | Low | C1-b |
| B9 | 625-629 | IX_Patients_NormalizedWhatsApp (unique, filtered) | Low | C1-b |
| B11 | 646-648 | IX_Conversations_PatientId | Low | C1-c |
| B12 | 649-651 | IX_Conversations_ConversationType | Low | C1-c |
| B19 | 724-727 | IX_PatientAccounts_PatientId (unique) | Low | C1-d |
| B20 | 728-731 | IX_PatientAccounts_PhoneNumber (unique) | Low | C1-d |
| B25 | 779-781 | IX_PatientAccounts_Username (unique, filtered) | Low | C1-d |
| B33 | 919-923 | IX_DoctorSchedules_DoctorId_DayOfWeek (unique, filtered) | Low | C1-h |

##### B-IV: Schema — Foreign Keys (Convert to EF migration)

| Block | Line(s) | FK | Risk | Phase |
|-------|---------|-----|------|-------|
| B13 | 654-661 | FK_Conversations_Patients_PatientId | Low | C1-c |
| B18 | 716-723 | FK_PatientAccounts_Patients_PatientId | Low | C1-d |
| B32 | 910-917 | FK_DoctorSchedules_Doctors_DoctorId | Low | C1-h |
| B41 | 1150-1169 | 4 FKs for ClinicQueueItems | Low | C1-j |
| B45 | 1219-1230 | FK_ClinicQueueItems_Users_AddedByUserId/CalledByUserId | Low | C1-j |

##### B-V: Data Backfills (Keep temporarily, do NOT remove yet)

| Block | Line(s) | Purpose | Risk | Why Keep |
|-------|---------|---------|------|----------|
| B4 | 553-573 | UPDATE Patients NormalizedPhone (CASE normalization) | Medium | **Legacy data backfill** for existing rows where Phone set but NormalizedPhone NULL. Do NOT remove until production verification or replacement migration (Phase C1-e). |
| B5 | 575-594 | UPDATE Patients NormalizedWhatsApp (CASE normalization) | Medium | Same as B4 for WhatsApp/NormalizedWhatsApp. Do NOT remove until Phase C1-e verified. |
| B6 | 598-608 | Deduplicate NormalizedPhone (CTE) | Low | Dedup safety guard. Keep until B4/B5 backfill strategy is complete and no duplicates confirmed. |
| B7 | 609-619 | Deduplicate NormalizedWhatsApp (CTE) | Low | Same as B6 for NormalizedWhatsApp. Keep until B4/B5 strategy complete. |
| B44 | 1209-1216 | Data migration: CalledBy -> CalledByUserId, then DROP COLUMN | Medium | **Contains destructive DROP COLUMN.** Keep until migration 20260520000000 is confirmed stable |

##### B-VI: Fake Migration History (Delete as obsolete)

| Block | Line(s) | MigrationId | Phase |
|-------|---------|-------------|-------|
| B14 | 664-670 | 20260501000000_AddNormalizedPhoneFields | C1-k |
| B15 | 671-677 | 20260501010000_AddPatientConversationSupport | C1-k |
| B16 | 678-684 | 20260501020000_AddSoftDeleteToMessagingTables | C1-k |
| B21 | 732-738 | 20260430120000_AddPatientPortal | C1-k |
| B22 | 739-745 | 20260430140000_AddWhatsAppIntegration | C1-k |
| B23 | 746-752 | 20260430160000_AddGeneralDentistryEnhancements | C1-k |
| B28 | 834-840 | 20260502000000_AddVisitsDocumentsFields | C1-k |
| B30 | 873-879 | 20260502100000_AddQueueFieldsToAppointments | C1-k |
| B34 | 926-932 | 20260502120000_AddDoctorSchedules | C1-k |
| B39 | 1088-1094 | 20260430000000_AddMessagingSystem | C1-k |
| B42 | 1172-1178 | 20260514000000_AddClinicQueueItem | C1-k |
| B46 | 1233-1239 | 20260520000000_AddClinicQueueItemTrackingFields | C1-k |

##### B-VII: Infrastructure — Advisory Locks (Keep permanently)

| Block | Line(s) | Purpose | Risk | Action |
|-------|---------|---------|------|--------|
| B1 | 480-485 | pg_try_advisory_lock acquisition | Low | Keep — concurrency primitive |
| B47 | 1278-1279 | pg_advisory_unlock release | Low | Keep — paired with B1 |

### Risk Matrix Summary

| Risk Level | Count (Program.cs) | Blocks |
|-----------|-------------------|--------|
| **High** | 8 | A3, B17, B24, B31, B35, B36, B37, B38, B40 |
| **Medium** | 17 | A1, A4, B2, B4, B5, B14-B16, B21-B23, B26-B28, B29-B30, B34, B39, B42-B44, B46 |
| **Low** | 25 | A2, B1, B3, B6-B13, B18-B20, B25, B32-B33, B41, B45, B47 |

### Blocks Safe to Convert First (Phase C1)

**Phase C1-a through C1-i are safe** because they are inside the `ENABLE_STARTUP_DB_MAINTENANCE=false` gate. Converting them to EF migrations carries zero production risk since the blocks do not execute in production.

**Safest conversion order:**
1. Soft-delete columns (B2) — pure ADD COLUMN, idempotent
2. NormalizedPhone/WhatsApp columns + indexes (B3, B8, B9) — already applied
3. Conversation columns + indexes + FK (B10-B13) — already applied
4. PatientAccounts full setup (B17-B20, B24-B25) — no EF migration exists yet
5. Visits/Documents columns + indexes (B26-B27) — already applied
6. Appointments queue columns (B29) — already applied
7. DoctorSchedules full setup (B31-B33) — no EF migration exists yet
8. Messaging tables fallback (B35-B38) — redundant with MigrateAsync()
9. ClinicQueueItems full setup (B40-B45) — **do last** per TD-010
10. Delete fake migration history entries (B14-B16, B21-B23, B28, B30, B34, B39, B42, B46)

### Blocks That Must NOT Be Touched Yet

| Block | Reason |
|-------|--------|
| A3 (admin password reset) | Touches admin credentials; must remain until proper password management is implemented |
| B4/B5 (phone normalization backfills) | One-time data migration; may need re-run for staging environments |
| B44 (CalledBy DROP COLUMN) | Destructive operation; must keep until migration 20260520000000 is confirmed stable |
| B40 (ClinicQueueItems safety net) | TD-010 safety net for Railway; must remain until 2+ weeks of clean deployments |
| B1/B47 (advisory lock/unlock) | Infrastructure; keep as long as any gated block remains |
| Q1/Q2 (ClinicQueueController advisory locks) | Runtime concurrency control; never convert to migration |

### SQL Injection Analysis

| Pattern | Count | Risk | Details |
|---------|-------|------|---------|
| `$"..."` interpolation | 1 (B1 lockKey) | None | `int` from config — not user input |
| `$"..."` interpolation (table names in B2 loop) | 1 (B2 softDeleteSql) | None | Hardcoded `string[]` array — not user input |
| `{0}`/`{1}` parameterized | 1 (A3 password) | None | EF Core `ExecuteSqlRawAsync` auto-parameterizes `{0}`/`{1}` |
| Literal SQL | 46 | None | Pure string literals, no interpolation |
| `CreateCommand().CommandText` | 3 | None | Advisory lock (int), flag check (hardcoded), unlock (int) |

**Verdict:** No exploitable SQL injection vectors exist in Program.cs.

### Production Safety Notes

1. **ENABLE_STARTUP_DB_MAINTENANCE=false** is confirmed as the default. All 46 gated blocks (B1, B3-B47) are inactive in production. B2 was removed in Phase C1-a.
2. The 4 ungated blocks (A1-A4) run at every startup but are idempotent — they check for a Settings flag and skip if already applied.
3. No data-destructive operations exist outside the gated block, except B44 (DROP COLUMN) which is also gated.
4. The admin password reset (A3) uses `ADMIN_DEFAULT_PASSWORD` env var with fallback to `"ChangeMeImmediately2026!"`.
5. Railway logs should show "Admin password reset already applied, skipping" on every startup (confirming the flag is set).

---

## Phase C1-a: Convert B2 Soft-Delete Columns to EF Migration (2026-05-13)

**Branch:** `td-020-phase-c1a-soft-delete-migration`
**Migration:** `20260522000000_AddSoftDeleteColumnsToLegacyTables`
**Scope:** Convert only B2 from Program.cs to an idempotent EF migration. No other blocks touched.

### What Changed

| Item | Before | After |
|------|--------|-------|
| Program.cs `ExecuteSqlRawAsync` calls | 49 | 48 |
| Program.cs raw SQL blocks | 50 | 49 |
| Total backend raw SQL | 52 | 51 |
| EF migrations added | 0 | 1 |
| Lines removed from Program.cs | — | 34 |

### Block Removed

| Block | Original Lines | Purpose | Replacement |
|-------|---------------|---------|-------------|
| B2 | 504-539 | Add DeletedAt/DeletedBy to 39 tables via foreach loop | `migrationBuilder.Sql(DO $$ ... IF NOT EXISTS ... ADD COLUMN)` for each table (idempotent) |

### Exact Table List (39 tables)

Patients, Users, Doctors, Branches, Appointments, Conversations, ConversationParticipants, Messages, MessageReads, Visits, Payments, Contracts, OrthoCases, OrthoVisits, TreatmentStages, RetentionRecords, SurgeryCases, Prescriptions, Notifications, AuditLogs, Settings, Inventory, LabOrders, InternalReferrals, ClinicalPhotos, Radiographs, Documents, DentalCharts, ToothConditions, GeneralTreatments, WhatsAppMessages, WhatsAppTemplates, PatientAccounts, CephAnalyses, PerioRecords, GeneralTreatmentPlanItems, MedicalHistories, DentalHistories, Receipts

### Files Changed

1. `backend/src/AqlanDentalPro.API/Program.cs` — Removed B2 foreach loop (lines 504-539), replaced with 2-line comment referencing migration
2. `backend/src/AqlanDentalPro.Infrastructure/Data/Migrations/20260522000000_AddSoftDeleteColumnsToLegacyTables.cs` — New idempotent EF migration
3. `docs/technical-debt/TD-020-raw-sql-inventory.md` — Updated with Phase C1-a results

### Migration Idempotency

All operations in this migration are safe for databases where the old Program.cs raw SQL already applied these changes:

- **Table existence guard:** Each operation first checks `information_schema.tables` for the target table — no error if the table does not exist (e.g., on fresh databases where not all tables have been created yet)
- **Column existence guard:** Each operation checks `information_schema.columns` for the specific column — no error if DeletedAt or DeletedBy already exist
- **Column types match exactly:** `DeletedAt` is `timestamp with time zone NULL`, `DeletedBy` is `uuid NULL` — identical to the original B2 raw SQL
- **Down():** Intentionally no-op — DeletedAt/DeletedBy may have existed before this migration due to legacy Program.cs B2 runtime maintenance. Dropping them could remove existing soft-delete data/schema.
- **No AppDbContextModelSnapshot changes needed:** DeletedAt/DeletedBy are already defined on BaseEntity and present in the EF model

### Production Behavior Impact

**None.** The migration applies the exact same schema changes that the B2 raw SQL was doing:

- DeletedAt/DeletedBy columns are added only if they don't already exist
- The B2 block was inside the `ENABLE_STARTUP_DB_MAINTENANCE=false` gate, so it was NOT running in production anyway
- When `dotnet ef database update` runs (or `MigrateAsync()` is called), this migration will apply the columns safely
- On production databases where B2 was previously applied via maintenance mode, the migration will be a no-op (columns already exist)
- On fresh databases, the columns will be added automatically by EF Core migrations

### Blocks NOT Touched

| Block | Status | Reason |
|-------|--------|--------|
| A1-A4 (admin password reset) | Unchanged | Not in scope |
| B1/B47 (advisory locks) | Unchanged | Infrastructure — keep as long as gated blocks remain |
| B3-B46 | Unchanged | Not in scope for this phase |
| ClinicQueueController.cs (Q1/Q2) | Unchanged | Not in scope |
| DbSeeder.cs | Unchanged | Already at 0 raw SQL |
| Frontend | Unchanged | Not in scope |
| Auth/password behavior | Unchanged | Not in scope |

### Post-Review Fixes (Codex Review)

Two issues identified by Codex review were fixed in a follow-up commit:

**P1 — Migration discovery metadata:**
- Added `[Migration("20260522000000_AddSoftDeleteColumnsToLegacyTables")]` attribute
- Added `using Microsoft.EntityFrameworkCore.Infrastructure;` for the attribute
- Without this attribute, EF Core derives the migration ID from the class name (`AddSoftDeleteColumnsToLegacyTables` instead of `20260522000000_AddSoftDeleteColumnsToLegacyTables`), breaking timestamp-based ordering
- Note: 8 other migrations in this project also lack the `[Migration]` attribute — those are out of scope for this PR

**P2 — Down() safety:**
- Replaced destructive `Down()` (which dropped DeletedAt/DeletedBy from 39 tables) with a no-op
- Rationale: DeletedAt/DeletedBy may have existed before this migration due to legacy Program.cs B2 runtime schema maintenance. Rolling back this migration should not remove existing soft-delete schema/data.

**Verification:**
- Backend build: 0 errors
- Backend tests: 287/287 passed
- Frontend lint: pass
- Frontend build: pass
- Migration class compiles and is discoverable via `[Migration]` attribute

---

## Phase C1-b: Convert B3/B8/B9 Normalized Phone Schema to EF Migration (2026-05-23)

**Branch:** `td-020-phase-c1b-normalized-phone-schema`
**Migration:** `20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes`
**Scope:** Convert B3 (ADD COLUMN NormalizedPhone/NormalizedWhatsApp) and B8/B9 (CREATE UNIQUE INDEX) from Program.cs to an idempotent EF migration. B4/B5 (data backfill) and B6/B7 (deduplication) remain in Program.cs per scope rules.

### What Changed

| Item | Before | After |
|------|--------|-------|
| Program.cs `ExecuteSqlRawAsync` calls | 48 | 45 |
| Program.cs raw SQL blocks | 49 | 46 |
| Total backend raw SQL | 51 | 48 |
| EF migrations added | 0 | 1 |
| Lines removed from Program.cs | — | ~14 |

### Blocks Removed

| Block | Original Lines | Purpose | Replacement |
|-------|---------------|---------|-------------|
| B3 | 507-517 | `DO $$ ... ADD COLUMN "NormalizedPhone"/"NormalizedWhatsApp" TO "Patients"` | `migrationBuilder.Sql(DO $$ ... IF NOT EXISTS ... ADD COLUMN)` in migration (idempotent) |
| B8 | 586-590 | `CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedPhone"` | `migrationBuilder.Sql(CREATE UNIQUE INDEX IF NOT EXISTS ...)` in migration |
| B9 | 591-595 | `CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedWhatsApp"` | `migrationBuilder.Sql(CREATE UNIQUE INDEX IF NOT EXISTS ...)` in migration |

### Blocks NOT Removed (Remain in Program.cs)

| Block | Lines | Purpose | Reason |
|-------|-------|---------|--------|
| B4 | ~511-529 | `UPDATE "Patients" SET "NormalizedPhone" = CASE ...` (phone normalization backfill) | One-time data backfill; already applied in production but may need re-run on staging |
| B5 | ~532-551 | `UPDATE "Patients" SET "NormalizedWhatsApp" = CASE ...` (WhatsApp normalization backfill) | Same as B4 |
| B6 | ~553-564 | Deduplicate `NormalizedPhone` (CTE) | One-time dedup; safe to delete after C1-b indexes confirmed applied |
| B7 | ~565-576 | Deduplicate `NormalizedWhatsApp` (CTE) | Same as B6 |

### Files Changed

1. `backend/src/AqlanDentalPro.API/Program.cs` — Removed B3 (lines 507-517) and B8/B9 (lines 586-595); replaced with 2-line comment referencing migration
2. `backend/src/AqlanDentalPro.Infrastructure/Data/Migrations/20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes.cs` — New idempotent EF migration
3. `docs/technical-debt/TD-020-raw-sql-inventory.md` — Updated with Phase C1-b results

### Migration Idempotency

All operations in this migration are safe for databases where the old Program.cs raw SQL or the earlier `20260501000000_AddNormalizedPhoneFields` migration already applied these changes:

- **Column existence guards:** Each `ADD COLUMN` is wrapped in `DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns ...) THEN ... END IF; END $$;` — no error if columns already exist
- **Table existence guard:** Also checks `information_schema.tables` for `Patients` before attempting any DDL
- **Index creation:** Uses `CREATE UNIQUE INDEX IF NOT EXISTS` (PostgreSQL 9.5+) — no error if indexes already exist
- **Column type:** `character varying(20) NULL` — matches EF model (`HasMaxLength(20)` in `PatientConfiguration.cs`) and the original Program.cs B3 block exactly; no conflict if column already exists
- **Down():** Intentionally no-op — NormalizedPhone/NormalizedWhatsApp may have existed before this migration due to legacy Program.cs B3/B8/B9 runtime maintenance. Dropping them could remove existing normalized phone data/schema.

### Production Behavior Impact

**None.** The migration covers the exact same schema changes that B3/B8/B9 were doing:

- Both columns and both indexes are guarded by `IF NOT EXISTS` — safe if already present
- The B3/B8/B9 blocks were inside the `ENABLE_STARTUP_DB_MAINTENANCE=false` gate, so they were NOT running in production anyway
- B4/B5 (data backfill) and B6/B7 (deduplication) remain in Program.cs — their order relative to the schema changes is preserved since `MigrateAsync()` runs before the gated block

### Relation to Existing Migration `20260501000000_AddNormalizedPhoneFields`

An earlier migration (`20260501000000_AddNormalizedPhoneFields`) also adds these columns and indexes, but uses non-idempotent `migrationBuilder.AddColumn` and `migrationBuilder.CreateIndex`. Program.cs pre-inserts its history entry (B14) to prevent EF from re-running it. The new migration (`20260523000000_...`) is a safe idempotent complement that handles databases where B14 was never inserted and the columns/indexes may already exist from Program.cs runtime execution.

### Blocks NOT Touched

| Block | Status | Reason |
|-------|--------|--------|
| A1-A4 (admin password reset) | Unchanged | Not in scope |
| B1/B47 (advisory locks) | Unchanged | Infrastructure — keep as long as gated blocks remain |
| B4/B5 (phone normalization backfills) | Unchanged | One-time data backfill; out of scope per task rules |
| B6/B7 (deduplication) | Unchanged | Out of scope per task rules |
| B10-B46 | Unchanged | Not in scope for this phase |
| ClinicQueueController.cs (Q1/Q2) | Unchanged | Not in scope |
| DbSeeder.cs | Unchanged | Already at 0 raw SQL |
| Frontend | Unchanged | Not in scope |
| Auth/password behavior | Unchanged | Not in scope |
| `ENABLE_STARTUP_DB_MAINTENANCE` | Unchanged | Not enabled |

### Column Type Fix (Post-Initial-Commit)

A follow-up commit corrected the column type from `text NULL` to `character varying(20) NULL`:

- **EF model:** `PatientConfiguration.cs` defines `HasMaxLength(20)` → EF generates `character varying(20)`
- **Legacy B3 block:** Original Program.cs used `character varying(20) NULL` exactly
- **Fix:** Migration now uses `character varying(20) NULL` — identical type on all paths (EF model, original B3, new migration)
- No change to indexes, guards, or `Down()` no-op

---

## Phase C1-d: Convert Conversation Schema Blocks to EF Migration (2026-05-24)

**Branch:** `td-020-phase-c1d-conversation-schema-migration`
**Migration:** `20260524000000_AddConversationPatientBranchFieldsAndIndexes`
**Scope:** Convert B10 (ADD COLUMN), B11/B12 (CREATE INDEX), B13 (ADD FK) from Program.cs to an idempotent EF migration. No data changes.

### What Changed

| Item | Before | After |
|------|--------|-------|
| Program.cs `ExecuteSqlRawAsync` calls | 45 | 41 |
| Program.cs raw SQL blocks | 46 | 42 |
| Total backend raw SQL | 48 | 44 |
| EF migrations added | 0 | 1 |
| Lines removed from Program.cs | — | ~31 |

### Blocks Removed

| Block | Original Lines | Purpose | Replacement |
|-------|---------------|---------|-------------|
| B10 | 578-590 | `DO $$ ... ADD COLUMN "ConversationType"/"PatientId"/"BranchId" TO "Conversations"` | `migrationBuilder.Sql(DO $$ ... IF NOT EXISTS ... ADD COLUMN)` per column (idempotent) |
| B11 | 592-594 | `CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId"` | `migrationBuilder.Sql(CREATE INDEX IF NOT EXISTS ...)` in migration |
| B12 | 595-597 | `CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType"` | `migrationBuilder.Sql(CREATE INDEX IF NOT EXISTS ...)` in migration |
| B13 | 599-607 | `DO $$ ... ADD CONSTRAINT "FK_Conversations_Patients_PatientId"` | `migrationBuilder.Sql(DO $$ ... IF NOT EXISTS ... ADD CONSTRAINT)` in migration |

### Migration Idempotency

All operations are safe for databases where these columns/indexes/FK already exist (from `20260430221624_AddConversationPatientAndType`, `20260501010000_AddPatientConversationSupport`, or Program.cs B10-B13 runtime maintenance):

- **ConversationType column:** `DO $$ IF NOT EXISTS (information_schema.columns) THEN ADD COLUMN character varying(20) NOT NULL DEFAULT 'StaffToStaff'` — no error if already exists
- **PatientId column:** Same pattern — `uuid NULL` — no error if already exists
- **BranchId column:** Same pattern — `uuid NULL` — no error if already exists
- **Indexes:** `CREATE INDEX IF NOT EXISTS` inside `DO $$ IF EXISTS (Conversations table)` — no error if table or index does not exist
- **FK:** `DO $$ IF EXISTS (Conversations table) AND EXISTS (Patients table) AND EXISTS (PatientId column) AND NOT EXISTS (constraint)` — fully guarded
- **Down():** Intentionally no-op — dropping conversation columns/FK would break active messaging features

### Column Types — EF Model Verification

| Column | EF Config (`ConversationConfiguration.cs`) | Migration DDL |
|--------|-------------------------------------------|---------------|
| ConversationType | `character varying(20)` with default `StaffToStaff` | `character varying(20) NOT NULL DEFAULT 'StaffToStaff'` ✅ |
| PatientId | `uuid NULL` (FK → Patients.Id, SetNull) | `uuid NULL` ✅ |
| BranchId | `uuid NULL` (FK → Branches.Id, SetNull) | `uuid NULL` ✅ |

### Relation to Existing Migrations

Two earlier migrations partially cover this schema:
- `20260430221624_AddConversationPatientAndType` — adds `ConversationType` (as `text`, not `character varying(20)`) + `PatientId` + index + FK (no `BranchId`)
- `20260501010000_AddPatientConversationSupport` — adds all 3 columns + indexes + both FKs (non-idempotent)

Program.cs B15 pre-inserts the history entry for `20260501010000` without running it. The new migration is the safe idempotent complement for databases in any intermediate state.

### Blocks NOT Touched

| Block | Status | Reason |
|-------|--------|--------|
| A1-A4 (admin password reset) | Unchanged | Not in scope |
| B1/B47 (advisory locks) | Unchanged | Infrastructure |
| B4/B5/B6/B7 (phone backfill/dedup) | Unchanged | Remain in Program.cs — B4/B5 are legacy data backfill blocks (existing patients may have Phone/WhatsApp populated but NormalizedPhone/NormalizedWhatsApp NULL); B6/B7 are dedup safety guards; safe removal requires production verification or a replacement idempotent data migration (see Phase C1-e) |
| B14-B46 | Unchanged | Not in scope for this phase |
| ClinicQueueController.cs (Q1/Q2) | Unchanged | Not in scope |
| Frontend | Unchanged | Not in scope |
| Messaging/conversation behavior | Unchanged | Migration adds schema only; no data changes |
| Auth/password behavior | Unchanged | Not in scope |
| `ENABLE_STARTUP_DB_MAINTENANCE` | Unchanged | Not enabled |

---

## Phase C1-e: Safe Legacy Patient Phone Normalization Backfill Plan (2026-05-13)

**Branch:** `td-020-phase-c1e-phone-backfill-plan`
**Scope:** Documentation and planning only. No code, migration, schema, or data changes.
**Status:** Review pending — production verification required before any deletion.

### Why PR #103 Was Closed Without Merging

PR #103 (TD-020 Phase C1-c) attempted to delete B4/B5/B6/B7 from `Program.cs`. It was **closed without merging** because its approach is unsafe.

**The core problem:** B4/B5 are not redundant schema helpers — they are legacy data backfill blocks.

| Block | Nature | Risk of Deletion |
|-------|--------|------------------|
| B4 | `UPDATE "Patients" SET "NormalizedPhone" = CASE ...` | Removes the only automated path to populate `NormalizedPhone` for patients created before `PhoneNormalizer` was wired into `PatientService`. Any such rows remain `NULL` and are invisible to normalized-phone duplicate detection. |
| B5 | `UPDATE "Patients" SET "NormalizedWhatsApp" = CASE ...` | Same as B4 but for `NormalizedWhatsApp`/`WhatsApp`. |
| B6 | Deduplicate `NormalizedPhone` (CTE) | If B4 still needs to run, duplicate normalized values could violate the unique index. B6 must remain until B4's backfill is verified complete. |
| B7 | Deduplicate `NormalizedWhatsApp` (CTE) | Same as B6 for `NormalizedWhatsApp`. |

The C1-b migration (`20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes`) only handles **schema** (ADD COLUMN + CREATE UNIQUE INDEX). It does **not** populate existing rows. Unique indexes protect against *future* duplicates but do not fix *existing* NULL normalized fields.

### Current State (After PR #104 Merge)

| Item | State |
|------|-------|
| `NormalizedPhone` / `NormalizedWhatsApp` columns | Present — added via C1-b migration |
| `IX_Patients_NormalizedPhone` unique filtered index | Present — added via C1-b migration |
| `IX_Patients_NormalizedWhatsApp` unique filtered index | Present — added via C1-b migration |
| New patient creation | `PhoneNormalizer.Normalize()` called in `PatientService.CreateAsync()` — normalized fields set for all new rows |
| Patient update | `PhoneNormalizer.Normalize()` called in `PatientService.UpdateAsync()` — normalized fields set on every save |
| Legacy rows (created before normalization) | Unknown — may have `Phone`/`WhatsApp` set but `NormalizedPhone`/`NormalizedWhatsApp` NULL |
| B4/B5/B6/B7 in Program.cs | Present and unchanged — gated by `ENABLE_STARTUP_DB_MAINTENANCE=false` |
| Program.cs raw SQL blocks | **42** |
| Total backend raw SQL | **44** |

### Application Code Verification

`PatientService.cs` (`Application/Services/PatientService.cs`) calls `PhoneNormalizer.Normalize()` on both create and update paths:

```csharp
// CreateAsync (lines 47-48, 64, 66)
var normalizedPhone = PhoneNormalizer.Normalize(req.Phone);
var normalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);
// ...
NormalizedPhone = normalizedPhone,
NormalizedWhatsApp = normalizedWhatsApp,

// UpdateAsync (lines 162-163, 187, 189)
var normalizedPhone = PhoneNormalizer.Normalize(req.Phone);
var normalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);
// ...
patient.NormalizedPhone = PhoneNormalizer.Normalize(req.Phone);
patient.NormalizedWhatsApp = PhoneNormalizer.Normalize(req.WhatsApp);
```

**Conclusion:** All patients created or updated *after* `PhoneNormalizer` was introduced have correct normalized values. Patients created *before* that change (legacy rows) may still have NULL normalized fields if they have never been updated via the application since.

### EF Model Verification

`PatientConfiguration.cs` defines:

```csharp
builder.Property(p => p.NormalizedPhone).HasMaxLength(20);        // character varying(20) NULL
builder.Property(p => p.NormalizedWhatsApp).HasMaxLength(20);     // character varying(20) NULL
builder.HasIndex(p => p.NormalizedPhone).IsUnique()
    .HasFilter("\"NormalizedPhone\" IS NOT NULL AND \"NormalizedPhone\" != ''");
builder.HasIndex(p => p.NormalizedWhatsApp).IsUnique()
    .HasFilter("\"NormalizedWhatsApp\" IS NOT NULL AND \"NormalizedWhatsApp\" != ''");
```

Schema is correct. The gap is **data**, not schema.

### Safe Future Implementation Options

#### Option 1 — Production DB Verification First (Recommended first step)

Run the **read-only** SQL checks below against production. If all counts return zero:
- B4/B5 may be deleted (no backfill needed).
- B6/B7 may be deleted (no duplicates to guard against).
- A new PR can remove all four blocks cleanly.

If counts are non-zero, proceed with Option 2.

#### Option 2 — Safe Idempotent Data Migration

Create a new EF migration that:
1. **Deduplicates first** — NULLs duplicate `Phone`/`WhatsApp`-derived normalized values (keeping the oldest row per group) before attempting to set normalized fields. This prevents unique-index violations.
2. **Backfills normalized fields** — Updates `NormalizedPhone`/`NormalizedWhatsApp` using the same normalization logic as B4/B5 for rows where `Phone`/`WhatsApp` is set but the normalized field is NULL.
3. **Is idempotent** — Uses `WHERE "NormalizedPhone" IS NULL AND "Phone" IS NOT NULL AND "Phone" != ''` conditions so re-running never overwrites already-normalized values.
4. **Does not overwrite non-NULL values** — Only fills rows where normalized field is NULL.

Only after this migration is verified applied can B4/B5/B6/B7 be removed from `Program.cs`.

### Recommended Read-Only Production SQL Checks

> **Do NOT run these automatically.** Run them manually via Railway console or `psql` against production DB.

**1. Count Phone rows needing NormalizedPhone backfill:**
```sql
SELECT COUNT(*)
FROM "Patients"
WHERE "Phone" IS NOT NULL
  AND "Phone" != ''
  AND ("NormalizedPhone" IS NULL OR "NormalizedPhone" = '');
```

**2. Count WhatsApp rows needing NormalizedWhatsApp backfill:**
```sql
SELECT COUNT(*)
FROM "Patients"
WHERE "WhatsApp" IS NOT NULL
  AND "WhatsApp" != ''
  AND ("NormalizedWhatsApp" IS NULL OR "NormalizedWhatsApp" = '');
```

**3. Check duplicate existing NormalizedPhone values:**
```sql
SELECT "NormalizedPhone", COUNT(*)
FROM "Patients"
WHERE "NormalizedPhone" IS NOT NULL
  AND "NormalizedPhone" != ''
GROUP BY "NormalizedPhone"
HAVING COUNT(*) > 1;
```

**4. Check duplicate existing NormalizedWhatsApp values:**
```sql
SELECT "NormalizedWhatsApp", COUNT(*)
FROM "Patients"
WHERE "NormalizedWhatsApp" IS NOT NULL
  AND "NormalizedWhatsApp" != ''
GROUP BY "NormalizedWhatsApp"
HAVING COUNT(*) > 1;
```

**5. Sample rows needing phone backfill (optional, limit 20):**
```sql
SELECT "Id", "PatientNumber", "Phone", "NormalizedPhone", "CreatedAt"
FROM "Patients"
WHERE "Phone" IS NOT NULL
  AND "Phone" != ''
  AND ("NormalizedPhone" IS NULL OR "NormalizedPhone" = '')
ORDER BY "CreatedAt"
LIMIT 20;
```

**6. Sample rows needing WhatsApp backfill (optional, limit 20):**
```sql
SELECT "Id", "PatientNumber", "WhatsApp", "NormalizedWhatsApp", "CreatedAt"
FROM "Patients"
WHERE "WhatsApp" IS NOT NULL
  AND "WhatsApp" != ''
  AND ("NormalizedWhatsApp" IS NULL OR "NormalizedWhatsApp" = '')
ORDER BY "CreatedAt"
LIMIT 20;
```

### Recommended Next PR (After Verification)

#### Path A — If all backfill counts are zero (no rows need backfill)

**PR title:** `refactor: remove obsolete patient phone backfill blocks (TD-020 Phase C1-f)`

- Document production verification results (query outputs showing zero rows).
- Remove B4/B5/B6/B7 from `Program.cs`.
- Raw SQL: Program.cs 42 → 38, total backend 44 → 40.
- No migration needed.

#### Path B — If any backfill counts are non-zero

**PR title:** `refactor: add safe patient phone normalization data backfill (TD-020 Phase C1-f)`

- Create a new idempotent EF migration:
  1. Deduplicate potential collisions before backfill.
  2. Backfill `NormalizedPhone` / `NormalizedWhatsApp` for rows where `Phone`/`WhatsApp` exist but normalized fields are NULL.
  3. `Down()` no-op.
- Do NOT remove B4/B5/B6/B7 in this PR.
- After the migration is confirmed applied in production with no violations:
  - Open a follow-up PR to remove B4/B5/B6/B7.

### Blocks NOT Touched

| Block | Status | Reason |
|-------|--------|--------|
| A1-A4 (admin password reset) | Unchanged | Not in scope |
| B1/B47 (advisory locks) | Unchanged | Infrastructure |
| B4/B5 (phone normalization backfills) | **Unchanged — retained** | Legacy data backfill; removal requires production verification or replacement migration |
| B6/B7 (deduplication) | **Unchanged — retained** | Dedup safety; removal requires B4/B5 strategy to be complete |
| B14-B46 | Unchanged | Not in scope for this phase |
| ClinicQueueController.cs (Q1/Q2) | Unchanged | Not in scope |
| Frontend | Unchanged | Not in scope |
| Program.cs application behavior | Unchanged | Docs-only phase |
| Auth/password behavior | Unchanged | Not in scope |
| `ENABLE_STARTUP_DB_MAINTENANCE` | Unchanged | Not enabled |

---

## Phase C1-f — Safe Patient Phone Normalization Repair

**PR:** #106 (pending review)
**Branch:** `td-020-phase-c1f-safe-phone-normalization-repair`
**Type:** `fix` — make stuck EF migration idempotent
**Raw SQL delta:** Program.cs unchanged (42); total backend unchanged (44)

### Production Finding Summary

These findings were determined by reading Railway postgres and aqlan-dental service logs via `railway logs` (direct SQL access to the production DB was not possible from the sandbox — Railway postgres has no public TCP proxy/`DATABASE_PUBLIC_URL` configured).

| Finding | Detail |
|---------|--------|
| `ENABLE_STARTUP_DB_MAINTENANCE` | **true** in production — all B-blocks run on every startup |
| Stuck migration (Problem A) | `20260430221054_AddPhoneNormalizationAndArchive` NOT in `__EFMigrationsHistory`; retries and fails every startup |
| Root cause of A | Migration's `AddColumn NormalizedPhone/NormalizedWhatsApp` calls are non-idempotent; columns already added by B-blocks before EF ran → 42701 `column already exists` → whole migration rolls back → never recorded |
| Next-in-line stuck migration (Problem B) | `20260430221624_AddConversationPatientAndType` also NOT in B-block history inserts; would become the next stuck migration immediately after A is fixed |
| B5 backfill failure (Problem C) | `duplicate key value violates unique constraint "IX_Patients_NormalizedWhatsApp"` — Key `(967711752823)` already exists — fires on **every** startup |
| Known duplicate raw WhatsApp | `0711752823` → normalized `967711752823`; two patients share this raw WhatsApp number |
| One patient stuck with `NormalizedWhatsApp = NULL` | B6/B7 previously nulled it out when creating `IX_Patients_NormalizedWhatsApp`; B5 keeps trying (and failing) to re-populate it |
| `IX_Patients_NormalizedWhatsApp` exists | Confirmed by constraint violation error |
| `IX_Patients_WhatsApp` (raw) does NOT exist | Fails every startup: `Key ("WhatsApp")=(0711752823) is duplicated` |
| EF migration loop cascade | `20260430221054` failure prevents EF from recording any later migrations; app starts successfully despite errors (health 200) |
| Q1 (NormalizedPhone NULL count) | Likely 0 — no NormalizedPhone UPDATE errors visible in logs; backfill appears complete |
| Q2 (NormalizedWhatsApp NULL count) | ≥ 1 confirmed — at least the patient with WhatsApp `0711752823` |
| Q3 (duplicate NormalizedPhone) | Likely 0 — `IX_Patients_NormalizedPhone` unique constraint active, no violations |
| Q4 (duplicate NormalizedWhatsApp) | 0 active violations; 1 patient cannot be filled without conflict |

### Why B4/B5/B6/B7 Cannot Simply Be Deleted

| Block | Why NOT deletable yet |
|-------|----------------------|
| B4 (NormalizedPhone backfill) | Q1 is probably 0 but unconfirmed without a direct query; premature deletion risks leaving legacy rows un-normalized |
| B5 (NormalizedWhatsApp backfill) | Q2 ≥ 1 confirmed; at least one patient still has `NormalizedWhatsApp = NULL`; B5 is the only automated fill path for that row (even though it currently fails) |
| B6 (NormalizedPhone dedup CTE) | Dedup safety guard before `IX_Patients_NormalizedPhone`; removal safe only after normalized index is confirmed stable |
| B7 (NormalizedWhatsApp dedup CTE) | Same as B6 but for WhatsApp; original dedup run created the unique constraint |

### Why `IX_Patients_WhatsApp` Must Not Be Created

The raw `WhatsApp` column has at least two patients with value `0711752823`. `CREATE UNIQUE INDEX` on the raw `WhatsApp` column will fail until the duplicate is corrected. The index must not be created in any migration until:
1. Staff corrects the duplicate via the patient edit UI (change one patient's phone to the real number).
2. A follow-up read-only check confirms zero raw WhatsApp duplicates.

### Why the First Approach in This PR Was Wrong

An initial attempt created a new migration `20260525000000_RepairPatientPhoneNormalizationState`
with `__EFMigrationsHistory` inserts intended to unblock `20260430221054`.

**This approach cannot work.** EF Core applies migrations in `MigrationId` order (timestamp
ascending). If `20260430221054` fails before being recorded, EF stops immediately — it never
reaches `20260525000000`. The history inserts inside the later migration are unreachable.

The correct fix is to make `20260430221054_AddPhoneNormalizationAndArchive` itself idempotent,
because EF IS reaching that migration — it just fails when it gets there.

### Why Modifying the Old Migration Is Safe

This migration has never been successfully applied (it is NOT in `__EFMigrationsHistory`). EF Core
will execute the modified version as if it is being applied for the first time. Since the migration
has never run, there is no risk of re-applying state that was already recorded.

The `.Designer.cs` file is left unchanged — it contains the model snapshot at the time of the
original migration generation and does not affect execution.

### What Was Changed in `20260430221054_AddPhoneNormalizationAndArchive.cs`

All non-idempotent EF method calls (`AddColumn`, `CreateTable`, `CreateIndex`, `AddForeignKey`)
were replaced with raw idempotent SQL.

| Operation type | Original (broken) | Fixed (idempotent) |
|---------------|-------------------|-------------------|
| `AddColumn NormalizedPhone` | `migrationBuilder.AddColumn<string>(...)` — throws 42701 if column exists | `DO $$ BEGIN IF NOT EXISTS column THEN ALTER TABLE ADD COLUMN END $$` |
| `AddColumn NormalizedWhatsApp` | Same | Same pattern |
| `CreateTable` × 9 tables | `migrationBuilder.CreateTable(...)` — throws if table exists | `CREATE TABLE IF NOT EXISTS` |
| `CreateIndex` / `CreateUniqueIndex` × 18 | `migrationBuilder.CreateIndex(...)` — throws if index exists | `CREATE [UNIQUE] INDEX IF NOT EXISTS` |
| `AddForeignKey` × 15 | Embedded in CreateTable constraints; ignored for existing tables | Separate `DO $$ BEGIN IF NOT EXISTS pg_constraint THEN ALTER TABLE ADD CONSTRAINT END $$` |
| `Down()` | Drops all tables (catastrophic for production data) | **No-op** — comment explains why |

The original migration did **not** include phone/WhatsApp data backfill. Backfill (B4/B5
equivalents) remains in Program.cs B-blocks and is addressed in Phase C1-g.

EF will record `20260430221054_AddPhoneNormalizationAndArchive` in `__EFMigrationsHistory`
automatically after it completes successfully — no manual history insert is needed.

### What the Fix Intentionally Does NOT Do

| Not Done | Reason |
|----------|--------|
| Create `IX_Patients_WhatsApp` (raw unique) | Raw WhatsApp duplicates exist; index creation would fail |
| Add NormalizedPhone/WhatsApp data backfill to old migration | Original migration had no backfill; backfill addressed separately |
| Pre-emptively mark `20260430221624` as applied | It may not be stuck; wait for production verification after `20260430221054` is resolved |
| Insert anything into `__EFMigrationsHistory` | EF records the migration automatically after successful completion |
| Resolve the duplicate raw WhatsApp `0711752823` | Requires human judgment — which patient has the correct number |
| Disable `ENABLE_STARTUP_DB_MAINTENANCE` | Done manually after production stability is confirmed |
| Remove B4/B5/B6/B7 | Reserved for Phase C1-g after deployment verification |

### Production Verification Steps After Deployment

After this migration is deployed and the app restarts:

**Step 1 — Confirm stuck migration loop is resolved:**
```
railway logs --service aqlan-dental | grep -E "Applying migration.*20260430221054"
```
Expected: no output (migration no longer retrying).

**Step 2 — Confirm B5 NormalizedWhatsApp error is gone:**
```
railway logs --service postgres | grep "IX_Patients_NormalizedWhatsApp"
```
Expected: no output (backfill conflict no longer thrown).

**Step 3 — Check if `20260430221624_AddConversationPatientAndType` becomes the next stuck migration:**
```
railway logs --service aqlan-dental | grep "Applying migration.*20260430221624"
```
- If no output: it was already in `__EFMigrationsHistory` (likely applied correctly at some point). ✅
- If it appears and fails: open a narrow follow-up PR making `20260430221624` idempotent.

**Step 4 — Turn off `ENABLE_STARTUP_DB_MAINTENANCE` in Railway:**
- Go to Railway dashboard → aqlan-dental service → Variables
- Set `ENABLE_STARTUP_DB_MAINTENANCE=false` (or delete the variable)
- Redeploy to confirm clean startup with no B-block errors

**Step 5 — Open Phase C1-g to remove B4/B5/B6/B7:**
- Only after Steps 1–4 are all verified clean.
- PR title: `refactor: remove obsolete phone normalization backfill blocks (TD-020 Phase C1-g)`
- Raw SQL: Program.cs 42 → 38, total backend 44 → 40.

**Step 6 — Manual clinic action (out of band):**
- Staff must open both patients with raw WhatsApp `0711752823` in the patient edit UI.
- Correct one patient's WhatsApp to their actual phone number.
- After correction, one patient will have `NormalizedWhatsApp` auto-populated on next update.
- After both patients have valid unique WhatsApp, `IX_Patients_WhatsApp` raw unique index can be created in Phase C1-h.

### Blocks NOT Touched

| Block | Status | Reason |
|-------|--------|--------|
| A1-A4 (admin password reset) | Unchanged | Not in scope |
| B1/B47 (advisory locks) | Unchanged | Infrastructure |
| B4/B5/B6/B7 (phone backfill/dedup) | **Unchanged — retained** | Removal reserved for Phase C1-g after production verification |
| B14-B46 | Unchanged | Not in scope |
| ClinicQueueController.cs (Q1/Q2) | Unchanged | Not in scope |
| Frontend | Unchanged | Not in scope |
| Auth/password behavior | Unchanged | Not in scope |
| `ENABLE_STARTUP_DB_MAINTENANCE` | Unchanged in code | Turn off manually in Railway after deployment verification |
