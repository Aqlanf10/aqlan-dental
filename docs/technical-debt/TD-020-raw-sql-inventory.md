# TD-020: Raw SQL Inventory & Classification

**Created:** 2026-05-13
**Production commit:** `2d18ec1`
**Status:** Phase B2 — DbSeeder raw SQL eliminated (4 → 0)

---

## Executive Summary

| Metric | Count |
|--------|-------|
| Files containing raw SQL | 2 (was 4; DbSeeder + MessagesController eliminated) |
| Total `ExecuteSqlRawAsync` calls | 49 (was 84; −31 Phase B1 from MessagesController, −4 Phase B2 from DbSeeder) |
| Total `ExecuteSqlRaw` (sync) calls | 0 |
| Total `FromSqlRaw` / `FromSqlInterpolated` | 0 |
| `CreateCommand()` / `CommandText` calls | 7 |
| String-interpolated SQL (`$"..."`) | 5 |
| Parameterized SQL (`{0}`, `{1}`) | 3 |
| Pure literal SQL (no interpolation) | ~45 |

**SQL Injection verdict:** No exploitable vectors found. All interpolated values are either `int` from configuration or hardcoded `string[]` arrays — none derive from user input.

---

## Files Containing Raw SQL

| # | File | Blocks | Guard | Category |
|---|------|--------|-------|----------|
| 1 | `src/AqlanDentalPro.API/Program.cs` | 51 | Ungated (A1-A4) + `ENABLE_STARTUP_DB_MAINTENANCE` (B1-B47) | Startup maintenance + Admin setup |
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

| # | Line | Purpose | Risk | Action |
|---|------|---------|------|--------|
| B1 | 482 | Advisory lock acquisition (`pg_try_advisory_lock`) | Low | C — Keep as advisory lock |
| B2 | 521 | `ALTER TABLE ... ADD COLUMN "DeletedAt"/"DeletedBy"` — loop over 34 tables | Medium | A — Convert to EF migration |
| B3 | 541 | `ADD COLUMN "NormalizedPhone"/"NormalizedWhatsApp" TO "Patients"` | Low | A — Convert to EF migration |
| B4 | 553 | `UPDATE "Patients" SET "NormalizedPhone" = CASE ...` (phone normalization) | Medium | E — Keep temporarily, then D |
| B5 | 574 | `UPDATE "Patients" SET "NormalizedWhatsApp" = CASE ...` (WhatsApp normalization) | Medium | E — Keep temporarily, then D |
| B6 | 597 | Deduplicate `NormalizedPhone` (complex CTE) | Low | D — Delete as obsolete |
| B7 | 608 | Deduplicate `NormalizedWhatsApp` (complex CTE) | Low | D — Delete as obsolete |
| B8 | 619 | `CREATE UNIQUE INDEX "IX_Patients_NormalizedPhone"` | Low | A — Convert to EF migration |
| B9 | 624 | `CREATE UNIQUE INDEX "IX_Patients_NormalizedWhatsApp"` | Low | A — Convert to EF migration |
| B10 | 631 | `ADD COLUMN "ConversationType"/"PatientId"/"BranchId" TO "Conversations"` | Low | A — Convert to EF migration |
| B11 | 645 | `CREATE INDEX "IX_Conversations_PatientId"` | Low | A — Convert to EF migration |
| B12 | 648 | `CREATE INDEX "IX_Conversations_ConversationType"` | Low | A — Convert to EF migration |
| B13 | 653 | `ADD FK "FK_Conversations_Patients_PatientId"` | Low | A — Convert to EF migration |
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
| B40 | 1117 | `CREATE TABLE "ClinicQueueItems" (...)` | High | A — Convert to EF migration |
| B41 | 1149 | `ADD FKs for ClinicQueueItems` | Low | A — Convert to EF migration |
| B42 | 1171 | `INSERT INTO "__EFMigrationsHistory" '20260514000000_AddClinicQueueItem'` | Medium | D — Delete as obsolete |
| B43 | 1193 | `ADD COLUMN "AddedByUserId"/"CalledByUserId"/"Notes" TO "ClinicQueueItems"` | Medium | A — Convert to EF migration |
| B44 | 1208 | Data migration: move "CalledBy" → "CalledByUserId" | Medium | E — Keep temporarily, then D |
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

### Phase B: Safe Parameterization
Convert non-schema raw SQL to parameterized `ExecuteSqlAsync` / `FormattableString`:
- Advisory locks: Q1, Q2, B1, B47 (4 blocks)
- Admin password reset: A3 (1 block)
- Data backfills: B4, B5, B44, S2 (4 blocks)

### Phase C: Convert Schema Hotfixes to EF Migrations
Create formal EF Core migrations for all schema changes currently done via raw SQL:
- 9 table creations (PatientAccounts, DoctorSchedules, ClinicQueueItems, Conversations, ConversationParticipants, Messages, MessageReads, Settings)
- ~20 column additions across multiple tables
- ~10 index creations
- ~8 foreign key additions

### Phase D: Remove Obsolete Blocks
Delete all raw SQL that has been superseded by EF migrations:
- All 31 blocks in `MessagesController.EnsureSchema()` endpoint
- All `INSERT INTO "__EFMigrationsHistory"` fake entries (B14-B16, B21-B23, B28, B30, B34, B39, B42, B46)
- Deduplication queries (B6, B7) — already applied

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
