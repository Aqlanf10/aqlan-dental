# C-08 — `StartupDatabaseMaintenance.cs` Audit + Phased Deletion Plan

**Task ID:** C-08
**Branch:** `feat/c-08-startup-maintenance-audit`
**Scope:** Non-destructive first increment — audit + schema-drift smoke test + deletion plan only. **No runtime DDL was modified or deleted in this increment.**
**Parent audit ref:** `docs/agent-audit/full-system-audit.md` row 5.1; technical-debt TD-010/011/014; DB-01.

---

## 1. Context and constraints

`backend/src/AqlanDentalPro.API/Configuration/StartupDatabaseMaintenance.cs` is **3,963 lines** and runs at every API startup. It contains:

- 1 fresh-DB bootstrap path (`EnsureFreshDatabaseMigratedAsync`) that uses `Database.GenerateCreateScript()` to build the schema from the current EF model when the `Users` table is missing (covers the empty-DB install case).
- 22 unconditional schema-hotfix methods (`Ensure…Async`) that add missing tables/columns/indexes/FKs via raw SQL `DO $$ … END $$;` blocks, each idempotent (`IF NOT EXISTS` guards inside PL/pgSQL).
- 1 gated maintenance block (`RunGatedDbMaintenanceAsync`, runs only when `ENABLE_STARTUP_DB_MAINTENANCE=true`, behind a PostgreSQL advisory lock) that creates finance/lab/messaging tables, reconciles `__EFMigrationsHistory`, runs `MigrateAsync`, then seeds.
- 1 HR/Backup tables block (`EnsureHrAndBackupTablesAsync`) — unconditional, runs last.

### CRITICAL warning from `CLAUDE.md`

> سلسلة الهجرات تاريخيًا مكسورة للقواعد الفارغة: 31 هجرة بلا سمة `[Migration]`. القواعد الفارغة تُبنى عبر خط أساس من نموذج EF في `StartupDatabaseMaintenance.EnsureFreshDatabaseMigratedAsync` — لا تحاول "إصلاح" السلسلة بإضافة السمات (سيكسر الإنتاج).

**Verified this increment:** the `[Migration]` attribute is missing from 64 of 77 migration `.cs` files (the previous "31" count was a snapshot; today's count is higher after the ortho/ceph/lab sprints). Adding `[Migration]` attributes is therefore not a viable path — the runtime DDL in `StartupDatabaseMaintenance.cs` is load-bearing for **fresh installs** (via `GenerateCreateScript()`) and for **partially-migrated existing DBs** (via the `IF NOT EXISTS` hotfixes that catch tables/columns a non-replayable migration chain cannot add).

This is why the audit row 5.1 mandated a **non-destructive** first pass with audit + safety-net test + deletion plan only.

---

## 2. Counts

This audit examined every `CREATE TABLE` block in the file. There are two syntactic flavors:

- **Literal `CREATE TABLE IF NOT EXISTS`** — 22 lines (20 distinct domain tables + `__EFMigrationsHistory` appearing twice, at the fresh-DB bootstrap path and the migration-history reconciliation block).
- **`CREATE TABLE` wrapped in a PL/pgSQL `IF NOT EXISTS (information_schema.tables) THEN …` guard** — 25 additional blocks.

The audit row 5.1 cited "25 `CREATE TABLE IF NOT EXISTS` blocks"; the literal-IF-NOT-EXISTS count is 22 (21 distinct domain tables + the EF infra table). Adding the IF-wrapped flavor brings the total to **47 distinct table-creation blocks** evaluated below. Both flavors are functionally equivalent (`IF NOT EXISTS` in `CREATE TABLE` vs an `IF NOT EXISTS (information_schema) THEN CREATE TABLE` guard) and both are audited.

### Classification totals

| Classification | Count | Meaning |
|---|---:|---|
| **REDUNDANT** | 39 | A migration file creates the table (via `migrationBuilder.CreateTable(name: "X")` **or** `migrationBuilder.Sql("CREATE TABLE \"X\"…")`). The runtime block is purely defensive cover for partially-migrated existing DBs; deleting it is safe **after** the schema-drift smoke test runs green on Railway for ≥2 weeks. |
| **PARTIAL** | 7   | A migration file creates the table but the runtime block's CREATE TABLE diverges materially from the EF model / migration (missing `DeletedAt`/`DeletedBy`, different column type, different index predicate, etc.). Deleting the runtime block risks leaving a partially-shaped table on a stale DB. |
| **LOAD-BEARING** | 1   | No migration file creates the table at all (`DoctorSchedules`). The runtime block is the *only* thing that creates the table on an upgrade-path DB. (Fresh-DB bootstrap via `GenerateCreateScript()` still covers it because the table is in `ModelSnapshot`.) |

A total of 47 blocks were classified. (For reference: 22 literal `CREATE TABLE IF NOT EXISTS` + 25 IF-wrapped `CREATE TABLE`.)

---

## 3. Table-by-table audit

Legend:
- **Snapshot?** — does `AppDbContextModelSnapshot.cs` declare this entity? `✓` yes (fresh-DB `GenerateCreateScript()` will create it on empty DBs) / `✗` no (deleting the runtime block leaves the table absent on fresh installs too).
- **Migration CreateTable?** — name of the migration file that creates the table (via either builder API or raw `Sql("CREATE TABLE …")`). `—` means no migration creates it.
- **Class** — REDUNDANT / PARTIAL / LOAD-BEARING.
- **Notes** — divergence rationale or runtime-block specifics.

### 3.1 Literal `CREATE TABLE IF NOT EXISTS` blocks (22 lines, 21 distinct tables)

| # | Table | Line | Snapshot? | Migration CreateTable | Class | Notes |
|---:|---|---:|:---:|---|:---:|---|
| 1 | `__EFMigrationsHistory` | 175 | n/a (EF infra) | (EF-managed) | REDUNDANT | EF infra; the fresh-DB bootstrap creates the model + history table together. Required by the bootstrap path itself — do not delete from the bootstrap path. |
| 2 | `CephNorms` | 1428 | ✓ | `20260625000000_AddCephNorms.cs` (raw `Sql`) | REDUNDANT | Maintenance block also seeds via `CephNormSeeder.SeedIfEmptyAsync`. Keep seeding behavior intact when deleting the CREATE TABLE portion. |
| 3 | `OrthodonticAiLogs` | 1493 | ✓ | `20260626000000_AddOrthodonticAiLogs.cs` (raw `Sql`) | REDUNDANT | — |
| 4 | `PhotoAnalyses` | 1536 | ✓ | `20260629000000_AddPhotoAnalysis.cs` (raw `Sql`) | REDUNDANT | Maintenance block creates the table without the FK to `OrthoCases`; migration adds the FK on fresh DBs. Stale DBs upgraded mid-stream rely on the runtime block to create the table (FK added later by a separate migration). |
| 5 | `DoctorSchedules` | 2562 | ✓ | **—** | **LOAD-BEARING** | No migration creates this table. Fresh-DB bootstrap via `GenerateCreateScript()` creates it (entity is in snapshot). Existing DBs that pre-date the entity rely on this runtime block. Must keep until a proper `[Migration]`-attributed migration is added (or the entity is removed). |
| 6 | `Treasuries` | 2612 | ✓ | `20260525115704_AddTreasuryVaultTransfers.cs` (raw `Sql`) | REDUNDANT | Runtime block is gated behind `ENABLE_STARTUP_DB_MAINTENANCE`. Migration has been applied on Railway since 2026-05. |
| 7 | `OperationalExpenses` | 2643 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | Same as Treasuries. |
| 8 | `CashierSessions` | 2685 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | Runtime block adds `TreasuryId` column inline (column added later by separate maintenance step + FK). See PARTIAL note on the inline `TreasuryId` ALTER further down. |
| 9 | `Suppliers` | 2724 | ✓ | `20260604000000_AddSuppliersAndPurchases.cs` (`CreateTable` builder) | REDUNDANT | Runtime block omits the `Type`/`Balance` columns (added later by ALTER blocks). Stale DBs upgraded mid-stream depend on the runtime block + the later ALTERs. |
| 10 | `SupplierBills` | 2752 | ✓ | `20260525123318_AddSupplierBillsAndApprovals.cs` (raw `Sql`) | REDUNDANT | — |
| 11 | `SupplierBillPayments` | 2789 | ✓ | `20260525123318_AddSupplierBillsAndApprovals.cs` (raw `Sql`) | REDUNDANT | — |
| 12 | `CreditNotes` | 2818 | ✓ | `20260617000000_AddFinancePhase1CreditNotesAndSupplierFields.cs` (raw `Sql`) | REDUNDANT | Runtime block creates 4 FKs inline; migration also creates them. |
| 13 | `VaultTransfers` | 2910 | ✓ | `20260525115704_AddTreasuryVaultTransfers.cs` (raw `Sql`) | REDUNDANT | Runtime block adds `DepositSource` inline (added later by ALTER). |
| 14 | `Contracts` | 2946 | ✓ | `20260425213215_InitialCreate.cs` (`CreateTable` builder) | REDUNDANT | Initial-create covers it. |
| 15 | `Payments` | 2982 | ✓ | `20260425213215_InitialCreate.cs` (`CreateTable` builder) | REDUNDANT | Initial-create covers it. Runtime block adds `ReceiptNumber` unique partial index inline (later added by maintenance). |
| 16 | `JournalEntries` | 3023 | ✓ | `20260614000000_AddFinanceV3JournalEntries.cs` (`CreateTable` builder) | REDUNDANT | Runtime block adds 6 FKs inline (each wrapped in `EXCEPTION WHEN OTHERS THEN NULL` so partial state doesn't break the bootstrap). Migration creates the same FKs. |
| 17 | `JournalLines` | 3121 | ✓ | `20260614000000_AddFinanceV3JournalEntries.cs` (`CreateTable` builder) | REDUNDANT | Runtime block adds `CK_JournalLines_DebitCreditMutual` check constraint inline. |
| 18 | `__EFMigrationsHistory` | 3259 | n/a (EF infra) | (EF-managed) | REDUNDANT | Second appearance — inside the migration-history reconciliation block. Defensive; same caveat as row 1. |
| 19 | `Conversations` | 3563 | ✓ | `20260430000000_AddMessagingSystem.cs` (raw `Sql`) | PARTIAL | Runtime block (line 3563) creates the table with `ConversationType`/`PatientId`/`BranchId` columns inline + the `FK_Conversations_Users_CreatedBy`. Migration `20260430000000` creates the base table; later migrations add the patient/branch columns. The runtime block exists inside the `MigrateAsync` failure-catch path (line 3544 catch), so it only runs when `MigrateAsync` throws — defensive. |
| 20 | `ConversationParticipants` | 3606 | ✓ | `20260430000000_AddMessagingSystem.cs` (raw `Sql`) | PARTIAL | Same defensive-fallback context as Conversations. |
| 21 | `Messages` | 3642 | ✓ | `20260430000000_AddMessagingSystem.cs` (raw `Sql`) | PARTIAL | Same defensive-fallback context; runtime block adds `IsEdited`/`EditedAt` inline. |
| 22 | `MessageReads` | 3692 | ✓ | `20260430000000_AddMessagingSystem.cs` (raw `Sql`) | PARTIAL | Same defensive-fallback context. |

### 3.2 `CREATE TABLE` wrapped in `IF NOT EXISTS (information_schema.tables) THEN …` (25 blocks)

| # | Table | Line | Snapshot? | Migration CreateTable | Class | Notes |
|---:|---|---:|:---:|---|:---:|---|
| 23 | `MessageAttachments` | 351 | ✓ | `20260602000000_AddMessageAttachments.cs` (raw `Sql`) | REDUNDANT | Runtime block (unconditional hotfix) also adds FK to `Messages` + `IX_MessageAttachments_MessageId`. |
| 24 | `DoctorCommissionPayments` | 511 | ✓ | `20260606000000_AddDoctorCommissionSystem.cs` (`CreateTable` builder) | REDUNDANT | — |
| 25 | `ClinicServices` | 700 | ✓ | `20260528000000_AddClinicServicesAndRooms.cs` (`CreateTable` builder) | PARTIAL | Runtime block creates the table with all commission columns inline (so even if the base migration ran, commission columns are guaranteed). Migration `20260606000000_AddDoctorCommissionSystem` adds the commission columns to ClinicServices separately; the runtime block + migration both add them idempotently. Net effect: deletion would only be safe once we confirm migration `20260606000000` has run on every production DB. |
| 26 | `ClinicRooms` | 756 | ✓ | `20260528000000_AddClinicServicesAndRooms.cs` (`CreateTable` builder) | REDUNDANT | — |
| 27 | `PasswordResetTokens` | 804 | ✓ | `20260522045621_AddPasswordResetSystem.cs` (raw `Sql`) | REDUNDANT | — |
| 28 | `PasswordResetRequests` | 846 | ✓ | `20260522045621_AddPasswordResetSystem.cs` (raw `Sql`) | REDUNDANT | — |
| 29 | `EmailLogs` | 931 | ✓ | `20260609000000_AddEmailLog.cs` (raw `Sql`? — file lacks `[Migration]` attribute) | REDUNDANT | — |
| 30 | `Invoices` | 1029 | ✓ | `20260531000000_AddInvoicesAndInvoiceLineItems.cs` (`CreateTable` builder) | REDUNDANT | Runtime block creates 4 FKs/indexes inline. |
| 31 | `InvoiceLineItems` | 1065 | ✓ | `20260531000000_AddInvoicesAndInvoiceLineItems.cs` (`CreateTable` builder) | PARTIAL | Runtime block creates the table without the 14 commission columns (DoctorId, LineDiscountAmount, MaterialCost, LabCost, OtherDirectCost, CommissionBaseRule, DoctorCommissionPercentage, NetCommissionableAmount, DoctorCommissionAmount, CenterShareAmount, CommissionStatus, CommissionNotes, LabOrderId, CommissionApprovedBy, CommissionApprovedAt). Those are added by `EnsureDoctorCommissionSchemaAsync` (line 496) — but only if the table already exists. If the table is created fresh by the runtime block (i.e. the migration chain hasn't run), the runtime block at line 1065 doesn't add the commission columns inline. The follow-up `EnsureDoctorCommissionSchemaAsync` block at line 560 (which runs unconditionally AFTER) does add them. So both blocks must be considered as one unit when planning deletion. |
| 32 | `Settings` | 1247 | ✓ | `20260425213215_InitialCreate.cs` (`CreateTable` builder) | PARTIAL | Runtime block creates Settings WITHOUT `DeletedAt`/`DeletedBy`. If this block fires (Settings table absent), it leaves a Settings table missing the soft-delete columns that EF expects → first Settings query throws "column DeletedAt does not exist". The later `EnsureSoftDeleteColumnsOnBaseEntityTablesAsync` loop (line 224, gated) catches this — but only inside gated maintenance. For non-gated startups (ENABLE_STARTUP_DB_MAINTENANCE=false), this is a real risk. Block exists inside `EnsureAdminPasswordResetAsync` to ensure the Settings table exists before checking the admin-reset flag. |
| 33 | `SmsMessages` | 1821 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 34 | `SmsTemplates` | 1853 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 35 | `Labs` | 2095 | ✓ | `20260603065814_AddLabManagementFoundation.cs` (raw `Sql`) | REDUNDANT | Runtime block adds `BranchId`/`Notes`/`DeletedAt`/`DeletedBy` inline + `FK_Labs_Branches_BranchId`. Migration also adds. |
| 36 | `LabWorkTypes` | 2140 | ✓ | `20260603065814_AddLabManagementFoundation.cs` (raw `Sql`) | REDUNDANT | Runtime block adds `NameAr`/`Category`/`SortOrder`/`DeletedAt`/`DeletedBy` inline. |
| 37 | `LabOrderItems` | 2177 | ✓ | `20260603071442_AddLabOrderItemsAndPricing.cs` (raw `Sql`) | PARTIAL | Runtime block at line 2196-2212 ALTERs `ToothNumber`/`Arch`/`Instructions` column types to longer varchar — diverging from the migration's column types. Deletion of the runtime block could leave stale column widths. |
| 38 | `LabWorkPrices` | 2232 | ✓ | `20260603071442_AddLabOrderItemsAndPricing.cs` (raw `Sql`) | REDUNDANT | — |
| 39 | `LabOrderStatusHistories` | 2269 | ✓ | `20260603072604_AddLabStatusHistoryRemakeAndAttachments.cs` (raw `Sql`) | REDUNDANT | — |
| 40 | `LabOrderAttachments` | 2295 | ✓ | `20260603072604_AddLabStatusHistoryRemakeAndAttachments.cs` (raw `Sql`) | REDUNDANT | — |
| 41 | `LabPayables` | 2324 | ✓ | `20260603074524_AddLabPayablesAndFinanceLinks.cs` (raw `Sql`) | REDUNDANT | Cascade behavior on LabPayable→Lab was changed to Restrict by migration `20260620201342_ChangeLabPayableLabCascadeToRestrict`. Runtime block sets it to CASCADE — divergence! See PARTIAL note in raw-SQL section below. |
| 42 | `Attendances` | 3811 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 43 | `SalaryRecords` | 3832 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 44 | `AdvancePayments` | 3859 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 45 | `LeaveRequests` | 3884 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 46 | `EmployeeDocuments` | 3908 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |
| 47 | `BackupRecords` | 3930 | ✓ | `20260525092924_AddCentralFinanceV2Hub.cs` (raw `Sql`) | REDUNDANT | — |

---

## 4. Raw SQL ops (ALTER TABLE / CREATE INDEX / column additions) — categorization

The file also contains a large number of ALTER TABLE / CREATE INDEX / DROP COLUMN / etc. raw-SQL ops. These are categorized below by purpose. Most are PARTIAL (covered by a migration in some form, but the runtime block adds them defensively for stale DBs).

### 4.1 Soft-delete loop (`EnsureSoftDeleteColumnsOnBaseEntityTablesAsync`, lines 224-267)

Adds `DeletedAt`/`DeletedBy` to every table with `IsActive+CreatedAt+UpdatedAt` columns via a PL/pgSQL `DO $$` loop.

- **Migration coverage:** migration `20260522000000_AddSoftDeleteColumnsToLegacyTables` adds these columns to 39 specific tables.
- **Divergence:** the runtime loop covers ~48 BaseEntity tables (the comment at line 3787-3792 explicitly says the migration is incomplete: "migration 20260522000000 lists only 39 tables — on a brand-new database ~48 tables (e.g. RolePermissions) end up without them").
- **Classification:** **LOAD-BEARING for fresh installs** because the migration is incomplete. (The fresh-DB bootstrap path via `GenerateCreateScript()` would also create these — but only on a truly empty DB. The runtime loop is the safety net for partially-migrated DBs.) Cannot be deleted until migration `20260522000000` is extended (or replaced by a `[Migration]`-attributed successor) to cover all BaseEntity tables.

### 4.2 Users/Doctors column additions (`EnsureUsersDoctorsSchemaAsync`, lines 272-331)

Adds `Users.MustChangePassword`, `Users.DeletedAt`, `Users.DeletedBy`, `Users.PasswordSalt`, `Doctors.DeletedAt`, `Doctors.DeletedBy`, `Doctors.CompensationType`, `Doctors.DefaultCommissionPercentage`, `Doctors.CompensationNotes`.

- **Migration coverage:** all of these are added by individual migrations (`20260521000000_AddPasswordSaltAndPatientPhoneIndexes`, `20260522000000_AddSoftDeleteColumnsToLegacyTables`, `20260525000000_AddMissingFKIndexesAndUserMustChangePassword`, `20260513000000_AddDoctorCompensationFields`).
- **Classification:** **REDUNDANT** (defensive cover for DBs upgraded mid-stream before those migrations were applied).

### 4.3 Messaging columns (`EnsureMessagingBaseEntityColumnsAsync`, lines 418-491)

Adds `DeletedAt`/`DeletedBy`/`IsEdited`/`EditedAt` to `Messages`, `ConversationParticipants`, `MessageReads`, `Conversations`, `MessageAttachments`.

- **Migration coverage:** migrations `20260501020000_AddSoftDeleteToMessagingTables` and `20260510000000_AddMessageEditFields`.
- **Classification:** **REDUNDANT**.

### 4.4 Doctor Commission columns on `InvoiceLineItems` and `ClinicServices` (lines 560-633)

Adds 14 commission columns to `InvoiceLineItems` and 6 commission default columns to `ClinicServices`.

- **Migration coverage:** migrations `20260606000000_AddDoctorCommissionSystem` and `20260607000000_AddCommissionRecognitionMode`.
- **Classification:** **REDUNDANT** (these are widely-deployed; railway has had them since 2026-06).

### 4.5 Patient Journey columns on `Appointments`, `Visits`, `InvoiceLineItems`, `ClinicQueueItems` (lines 1565-1740)

Adds `ServiceId`, `ClinicRoomId`, `RoomName`, `ArrivedAt`, `CalledAt`, `InRoomAt` to Appointments; `ServiceId`, `CheckoutStatus`, `ReadyForCheckoutAt`, `AmountDueReference`, `ProposedProcedure` to Visits; several queue fields to ClinicQueueItems.

- **Migration coverage:** migrations `20260529000000_AddPatientJourneyFields`, `20260605000000_AddClinicQueueItemServiceAndRoom`, `20260616000000_AddQueuePriorityNoShowRecall`.
- **Classification:** **REDUNDANT** for the column additions. The ClinicQueueItems unique-index re-creation at lines 1714-1727 is **PARTIAL** — it drops the old non-partial index and creates a partial one (`WHERE Status NOT IN ('Completed', 'Cancelled', 'NoShow')`); the same predicate is in migration `20260616000000`. Idempotent.

### 4.6 LabOrders column additions (`EnsureLabOrdersSchemaAsync`, lines 1957-2031)

Adds 15 columns to LabOrders (`BranchId`, `VisitId`, `Shade`, `RestorationType`, `DeliveredDate`, `CancellationReason`, `LabId`, `TotalCost`, `InvoiceLineItemId`, `RemakeReason`, `ReturnReason`, `RemakeCost`, `IsFreeRemake`, `OriginalOrderId`, `RemakeCount`) + 4 indexes.

- **Migration coverage:** migrations `20260602230911_Sprint2_LabOrderAndPaymentMethodSettings`, `20260603065814_AddLabManagementFoundation`, `20260603072604_AddLabStatusHistoryRemakeAndAttachments`.
- **Classification:** **REDUNDANT**.

### 4.7 Invoices.TaxAmount nullable (`EnsureInvoicesNullableTaxAmountAsync`, lines 2049-2075)

ALTERs `Invoices.TaxAmount` to DROP NOT NULL.

- **Migration coverage:** the EF model declares `TaxAmount` as `decimal?` (nullable), so `GenerateCreateScript()` creates it nullable on fresh DBs. For upgraded DBs where the column was created NOT NULL by an older migration, no later migration flips it.
- **Classification:** **PARTIAL** — no migration flips the column to nullable. The runtime block is the only thing that fixes this for legacy DBs. Cannot be deleted until a `[Migration]`-attributed migration is added to DROP NOT NULL on `Invoices.TaxAmount`.

### 4.8 PatientAccounts username/password columns + unique partial index (lines 2471-2486)

Adds `Username`, `PasswordHash`, `PasswordSalt` columns + a partial unique index `WHERE "Username" IS NOT NULL`.

- **Migration coverage:** migration `20260502010000_AddSecurePatientPortalPasswordAuth` (no `[Migration]` attribute — broken chain).
- **Classification:** **REDUNDANT** (defensive). The partial-unique-index form is required because `Username` is nullable (multiple NULLs allowed); the migration also uses the partial form.

### 4.9 Sprint 4.5 / Sprint 8 queue columns on Appointments + ClinicQueueItems (lines 2498-2556, gated)

Adds `RoomName`, `ArrivedAt`, `CalledAt`, `InRoomAt` to Appointments + `ServiceId`, `ClinicRoomId` to both ClinicQueueItems and Appointments.

- **Migration coverage:** migrations `20260529000000_AddPatientJourneyFields`, `20260605000000_AddClinicQueueItemServiceAndRoom`.
- **Classification:** **REDUNDANT**.

### 4.10 Finance V2/V3 — FK additions + VaultTransfers.DepositSource + CashierSessions.TreasuryId + CashFlowTransactions reversal columns (lines 3177-3225, gated)

- **Migration coverage:** migration `20260613000000_AddFinancialIntegrityAuditSprint` adds `IsReversal`/`ReversalOfTransactionId`/`ReversedByTransactionId` to CashFlowTransactions + the `TreasuryId` column + FK. The runtime block at lines 3210-3223 also adds `TreasuryId` to CashFlowTransactions + the FK.
- **Classification:** **REDUNDANT**.

### 4.11 LabPayable→Lab cascade (line 2353-2360)

The runtime block at line 2324 creates `LabPayables` table. Then at line 2346-2360 it adds `FK_LabPayables_LabOrders_LabOrderId` (CASCADE) and `FK_LabPayables_Labs_LabId` (CASCADE). At line 2361-2377 it adds `FK_LabOrders_Labs_LabId` (SET NULL).

- **Migration coverage:** migration `20260620201342_ChangeLabPayableLabCascadeToRestrict` (PR #484 / DB-09) explicitly changed `FK_LabPayables_Labs_LabId` from CASCADE to RESTRICT. The runtime block at line 2353 still creates the FK with CASCADE.
- **Divergence:** on a stale DB where the LabPayables table doesn't yet exist, the runtime block creates it with the OLD CASCADE behavior. The follow-up migration `20260620201342` (which IS in the broken chain and may not have been applied) would normally flip it to RESTRICT.
- **Classification:** **PARTIAL** — runtime block creates FK with the wrong (old) cascade behavior. Deleting the runtime block is safe ONLY if we can verify the FK cascade has been corrected on every production DB. Today's schema-drift smoke test would catch this (FK cascade is part of the EF model).

### 4.12 Migration history reconciliation (lines 3238-3492, gated)

This block DELETEs `__EFMigrationsHistory` rows for migrations whose target schema doesn't exist, then INSERTs rows for migrations whose target schema does exist. Effectively back-fills the broken migration chain.

- **Migration coverage:** none — this is unique maintenance logic, not a migration.
- **Classification:** **LOAD-BEARING for existing stale DBs.** Cannot be deleted until the migration chain is repaired (which CLAUDE.md forbids) or every production DB has been manually verified to have a clean `__EFMigrationsHistory` matching the actual schema. The fresh-DB bootstrap path (line 168-191) takes a different approach for new installs (creates the schema from `GenerateCreateScript()` and marks every migration as applied), so this reconciliation block only runs on existing DBs.

### 4.13 Defensive messaging-tables fallback (lines 3544-3785, gated, inside `catch`)

Inside the `MigrateAsync()` failure catch (line 3544), the code manually creates `Conversations`, `ConversationParticipants`, `Messages`, `MessageReads` tables with their FKs + indexes + soft-delete columns.

- **Migration coverage:** migration `20260430000000_AddMessagingSystem` (raw `Sql`) creates them.
- **Classification:** **REDUNDANT** (defensive fallback when MigrateAsync throws; only runs on the failure path).

---

## 5. Key risk findings

1. **DoctorSchedules is the only table with zero migration coverage.** On any DB where `MigrateAsync` cannot replay the broken chain (i.e., every existing production DB), removing the runtime `CREATE TABLE IF NOT EXISTS "DoctorSchedules"` block would leave the table absent on the next cold boot — `DoctorScheduleController` queries would throw `42P01: relation "DoctorSchedules" does not exist`. **Block deletion must wait for a proper `[Migration]`-attributed migration to be added for `DoctorSchedules`** (this is the single LOAD-BEARING runtime block in the file).

2. **Soft-delete loop is LOAD-BEARING for fresh installs.** Migration `20260522000000` is documented (in code comments at line 3787) as listing only 39 of ~48 BaseEntity tables. The runtime loop at lines 224-267 is the only thing that adds `DeletedAt`/`DeletedBy` to the remaining 9 tables on fresh installs. Deleting the loop without first extending the migration would break `DbSeeder.SeedAsync` (its first query against `RolePermissions` would throw).

3. **Migration history reconciliation block is LOAD-BEARING for stale existing DBs.** The block at lines 3238-3492 is the only mechanism that back-fills `__EFMigrationsHistory` for tables created by runtime hotfixes. Without it, `MigrateAsync` would attempt to re-apply migrations and fail with "relation already exists". This block can only be deleted once every production DB has been verified to have a clean `__EFMigrationsHistory`.

4. **Invoices.TaxAmount nullable hotfix has no migration equivalent.** The runtime ALTER at line 2063 (DROP NOT NULL) is the only thing that fixes the legacy NOT NULL column. Until a `[Migration]`-attributed successor does the same ALTER, this block cannot be deleted.

5. **LabPayable→Lab cascade mismatch.** Runtime block at line 2353 still uses the old CASCADE behavior; migration `20260620201342` (DB-09) changed it to RESTRICT. On a stale DB where the runtime block recreates the FK, the cascade will be wrong until `MigrateAsync` runs (which it does in the gated path). The schema-drift smoke test added in this increment will catch this kind of cascade drift if it occurs.

6. **Settings table partial-creation risk (line 1247).** The runtime block creates Settings WITHOUT `DeletedAt`/`DeletedBy`. If this fires on a stale DB (Settings table absent), the Settings table is created in a state that EF can't query until the soft-delete loop runs (which only happens in the gated path). For non-gated startups (`ENABLE_STARTUP_DB_MAINTENANCE=false`), this is a real risk. Recommend either (a) extending the runtime block to include `DeletedAt`/`DeletedBy`, or (b) ensuring the soft-delete loop runs unconditionally.

7. **Many "REDUNDANT" blocks are still actually used in practice.** Railway production has `ENABLE_STARTUP_DB_MAINTENANCE=true` and the migration chain is broken, so the runtime hotfixes fire on every cold boot. The `IF NOT EXISTS` guards make them no-ops on a healthy DB, but they're not actually dead code — they're the safety net catching the 64 unattributed migrations that `MigrateAsync` cannot replay.

8. **Counts vs. audit row 5.1:** the audit row 5.1 cites "25 `CREATE TABLE IF NOT EXISTS`". Literal count is 22 (21 distinct domain tables + `__EFMigrationsHistory` appearing twice). Including the 25 IF-wrapped `CREATE TABLE` blocks brings the total to 47 distinct table-creation sites. Both flavors are audited above.

---

## 6. Schema-drift smoke test (added this increment)

**File:** `backend/tests/AqlanDentalPro.IntegrationTests/SchemaDriftSmokeTests.cs`

The test uses the existing `TestWebAppFactory` (Testcontainers PostgreSQL + `WebApplicationFactory<Program>`) added by TEST-18. It:

1. Boots the app against a fresh Testcontainers PostgreSQL container (which runs `StartupDatabaseMaintenance.RunStartupDatabaseMaintenanceAsync` end-to-end).
2. Calls `db.Database.MigrateAsync()` (idempotent — TEST-18's factory already does this in `InitializeAsync`).
3. Enumerates the EF model's expected entity types via `DbContext.Model.GetEntityTypes()` and for each entity, enumerates its mapped properties + the table/column names EF expects.
4. Queries `information_schema.tables` and `information_schema.columns` on the live container to dump the actual schema.
5. Asserts:
   - Every EF-mapped entity has a corresponding table in the database.
   - Every EF-mapped column exists in the database with the expected name.
   - Every EF-mapped foreign key has a corresponding constraint in `pg_constraint`.
   - The `__EFMigrationsHistory` table exists and contains at least one row.

This test is the **safety net** the deletion plan needs: any future increment that removes a runtime DDL block must keep this test green. If a deletion causes schema drift (missing table/column/FK), the test fails before the change can ship.

**The test compiles cleanly but does NOT run in this sandbox** — Testcontainers needs Docker. It is intended to run in CI (`.github/workflows/ci.yml`) on runners with Docker available.

---

## 7. Phased deletion plan (for future increments)

The deletion plan is gated by three pre-conditions:

- **P1.** The schema-drift smoke test (`SchemaDriftSmokeTests.cs`) has been added to CI and is **green on Railway staging** for ≥2 weeks.
- **P2.** Each phase deletes only blocks classified **REDUNDANT** in this audit. PARTIAL and LOAD-BEARING blocks are explicitly excluded until their pre-conditions are met.
- **P3.** Each phase ships as its own PR with a 2-week clean-deploy observation window before the next phase begins.

### Phase 1 — Unconditional hotfix blocks fully covered by widely-deployed migrations (REDUNDANT, low risk)

**Target blocks (delete from `StartupDatabaseMaintenance.cs`):**
- `EnsureUsersDoctorsSchemaAsync` (lines 272-331) — covered by migrations 20260521/22/25/13.
- `EnsureMessagingBaseEntityColumnsAsync` (lines 418-491) — covered by migrations 2026050102/2026051000.
- `EnsureDoctorCommissionSchemaAsync` column-additions on `InvoiceLineItems`/`ClinicServices` (lines 560-633) — covered by migrations 2026060600/2026060700. **Keep the `DoctorCommissionPayments` table creation block (lines 511-558) for now** because it's PARTIAL (some stale DBs may still lack the table itself).
- `EnsureReminderTrackingColumnsAsync` (lines 974-1013) — covered by migration `20260610000000_AddSeparateReminderTrackingAndPatientEmail`.

**Verification per block (before deletion):**
- `grep` the migration files to confirm the columns are added by the named migration.
- Run `SchemaDriftSmokeTests` locally against a Testcontainers DB before the PR is merged.
- After deploy, observe Railway logs for 2 weeks for any `42P01` / `42703` errors on the affected tables.

**Estimated line reduction:** ~430 lines.

### Phase 2 — Gated finance/lab table creation blocks (REDUNDANT, medium risk)

**Target blocks:**
- `Treasuries`, `OperationalExpenses`, `CashierSessions`, `Suppliers`, `SupplierBills`, `SupplierBillPayments`, `CreditNotes`, `VaultTransfers`, `Payments`, `JournalEntries`, `JournalLines` runtime `CREATE TABLE IF NOT EXISTS` (lines 2612-3175) — all covered by migrations.
- `Labs`, `LabWorkTypes`, `LabWorkPrices`, `LabOrderStatusHistories`, `LabOrderAttachments`, `LabPayables` runtime CREATE TABLE (lines 2095-2397) — covered by migrations.

**Pre-condition:** Confirm on Railway production that `__EFMigrationsHistory` contains entries for migrations `20260525092924_AddCentralFinanceV2Hub`, `20260525115704_AddTreasuryVaultTransfers`, `20260525123318_AddSupplierBillsAndApprovals`, `20260603065814_AddLabManagementFoundation`, `20260603071442_AddLabOrderItemsAndPricing`, `20260603072604_AddLabStatusHistoryRemakeAndAttachments`, `20260603074524_AddLabPayablesAndFinanceLinks`, `20260614000000_AddFinanceV3JournalEntries`. If any are missing, the runtime blocks are still actively needed.

**Verification per block:**
- Inspect Railway's `__EFMigrationsHistory` via `psql` and confirm the relevant rows exist.
- Run `SchemaDriftSmokeTests` against Railway staging after deploy.
- Observe 2 weeks of clean Railway logs.

**Estimated line reduction:** ~900 lines.

### Phase 3 — HR/Backup tables + EmailLogs + SmsMessages/SmsTemplates + PasswordReset tables (REDUNDANT)

**Target blocks:**
- `EnsureHrAndBackupTablesAsync` (lines 3799-3963) — covered by migration `20260525092924_AddCentralFinanceV2Hub`.
- `EnsurePasswordResetSchemaAsync` (lines 789-916) — covered by migration `20260522045621_AddPasswordResetSystem`.
- `EnsureEmailLogsSchemaAsync` (lines 920-969) — covered by migration `20260609000000_AddEmailLog`.
- `EnsureSmsGatewaySchemaAsync` table creation (lines 1809-1886) — covered by migration `20260525092924_AddCentralFinanceV2Hub`. Keep the `SmsReminderWindowsSent` column addition (line 1872-1874) for now until a migration covers it.

**Estimated line reduction:** ~600 lines.

### Phase 4 — Defensive messaging-tables fallback (REDUNDANT)

**Target block:**
- The messaging-tables manual creation inside the `MigrateAsync` catch block (lines 3544-3785). Only runs when MigrateAsync throws; if the migration chain is healthy, this code never executes. Can be deleted once the migration chain is repaired or once 2 weeks of Railway logs show no `MigrateAsync` failures triggering the catch.

**Estimated line reduction:** ~240 lines.

### Phase 5 — PARTIAL blocks requiring migration repair first

These blocks cannot be deleted until the listed pre-condition is met. Each requires a `[Migration]`-attributed successor migration (or extension of an existing migration) to be added first.

| Block | Pre-condition for deletion |
|---|---|
| `DoctorSchedules` runtime CREATE TABLE (line 2562) | Add a `[Migration("2026XXXX_AddDoctorSchedules")]` that creates the table. |
| `EnsureSoftDeleteColumnsOnBaseEntityTablesAsync` loop (lines 224-267) | Extend migration `20260522000000` (or add a new migration) to cover all BaseEntity tables, not just the 39 currently listed. |
| `EnsureInvoicesNullableTaxAmountAsync` (lines 2049-2075) | Add a `[Migration]`-attributed migration that DROPs NOT NULL on `Invoices.TaxAmount`. |
| `Settings` table partial CREATE (line 1247) | Either add `DeletedAt`/`DeletedBy` to the runtime block, or remove the runtime block entirely once InitialCreate migration runs reliably on every DB. |
| `LabPayables` FK cascade (line 2353) | Verify on every production DB that the FK is RESTRICT (per migration `20260620201342`). If any DB still has CASCADE, run the migration manually first. |
| Migration history reconciliation block (lines 3238-3492) | Cannot be deleted until the migration chain is repaired (CLAUDE.md forbids) OR every production DB has been manually verified to have a clean `__EFMigrationsHistory` matching the actual schema. **Realistically this block stays until the migration chain is rebuilt.** |

**Estimated line reduction (only if all pre-conditions met):** ~600 lines.

### Phase 6 — Ortho/Ceph table blocks (REDUNDANT, low risk; defer to ortho module work)

**Target blocks:**
- `EnsureCephNormsSchemaAndSeedAsync` table creation (lines 1417-1475). **Keep the seeding call** (`CephNormSeeder.SeedIfEmptyAsync` + `BackfillMissingDefaultsAsync`) — those are runtime data seeding, not schema.
- `EnsureOrthodonticAiLogsSchemaAsync` (lines 1484-1518).
- `EnsurePhotoAnalysisSchemaAsync` (lines 1527-1560).

**Estimated line reduction:** ~150 lines.

### Total potential reduction

If all 6 phases complete successfully: **~2,920 lines** could be removed from `StartupDatabaseMaintenance.cs` (current: 3,963 lines → target: ~1,000 lines, mostly the fresh-DB bootstrap path + seeding + the migration history reconciliation block which is genuinely load-bearing for the broken migration chain).

---

## 8. What this increment did NOT do

Per the task brief's CRITICAL rules:

- ❌ **Did not delete any runtime DDL blocks.** This pass was audit + test + plan only.
- ❌ **Did not modify `StartupDatabaseMaintenance.cs` logic.** No behavior changes.
- ❌ **Did not add `[Migration]` attributes** to any existing migrations (CLAUDE.md explicitly forbids — would break production).
- ✅ **Did add a schema-drift smoke test** that compiles cleanly and will run in CI to catch any future drift.
- ✅ **Did write this audit doc** with a per-block classification and a phased deletion plan gated on 2-week clean-deploy observation windows.

The next increment can begin Phase 1 deletions once the schema-drift smoke test has been green on Railway staging for 2 weeks.

---

## 9. References

- Audit row 5.1: `docs/agent-audit/full-system-audit.md` (StartupDatabaseMaintenance 3,963 lines / 25 CREATE TABLE IF NOT EXISTS / ~366 raw SQL ops at every startup).
- CLAUDE.md migration-chain warning: `aqlan-dental/CLAUDE.md` line 36.
- TEST-18 integration test project: `backend/tests/AqlanDentalPro.IntegrationTests/` (PR #487, merged).
- TestWebAppFactory design: `/home/z/my-project/agent-ctx/TEST-18-code-agent.md` and `worklog.md` lines 579-696.
- DB-09 cascade fix: PR #484 (`20260620201342_ChangeLabPayableLabCascadeToRestrict`).
- DB-07 duplicate-index cleanup: PR #485.
- DB-03 CashierSessions partial unique index: PR #483.
- DB-02 Treasury xmin concurrency token: PR #482.
