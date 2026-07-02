using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for startup database maintenance.
/// Extracted from Program.cs for cleaner startup configuration.
/// All blocks preserve the same SQL, execution order, env guards,
/// try/catch, logging, transactions, and service scope usage as the original.
/// </summary>
public static class StartupDatabaseMaintenance
{
    /// <summary>
    /// Runs all startup database maintenance tasks including:
    /// - Unconditional schema hotfixes (run regardless of ENABLE_STARTUP_DB_MAINTENANCE)
    /// - Gated maintenance (with advisory lock + MigrateAsync, only when ENABLE_STARTUP_DB_MAINTENANCE=true)
    /// - HR/Backup table creation (unconditional)
    /// </summary>
    public static async Task RunStartupDatabaseMaintenanceAsync(
        this WebApplication app, IConfiguration configuration)
    {
        // ── Fresh-database bootstrap ───────────────────────────────────
        // Must run BEFORE the unconditional hotfixes: on a brand-new empty
        // database those hotfixes create partial schema (e.g. "Settings"),
        // which then makes the InitialCreate migration fail with
        // "relation already exists" and leaves the database incomplete.
        // Existing databases (Users table present) are not touched here.
        await EnsureFreshDatabaseMigratedAsync(app, configuration);

        // ── Unconditional schema hotfixes ──────────────────────────────
        await EnsureUsersDoctorsSchemaAsync(app);
        await EnsureMessageAttachmentsSchemaAsync(app);
        await EnsureMessagingBaseEntityColumnsAsync(app);
        await EnsureDoctorCommissionSchemaAsync(app);
        await EnsureClinicServicesAndRoomsSchemaAsync(app);
        await EnsureDoctorDefaultRoomColumnAsync(app);
        await EnsurePasswordResetSchemaAsync(app);
        await EnsureEmailLogsSchemaAsync(app);
        await EnsureReminderTrackingColumnsAsync(app);
        await EnsureInvoicesAndMigrationHistoryAsync(app);
        await EnsureAdminPasswordResetAsync(app);
        await EnsureWebsiteSettingsSeedAsync(app);
        await EnsurePatientJourneyColumnsAsync(app);
        await EnsurePatientJourneyPermissionsAsync(app);
        await EnsureSmsGatewaySchemaAsync(app);
        await EnsureSmsGatewaySettingsSeedAsync(app, configuration);
        await EnsureLabOrdersSchemaAsync(app);
        await EnsureInvoicesNullableTaxAmountAsync(app);
        await EnsureLabTablesSchemaAsync(app);
        await EnsureCephNormsSchemaAndSeedAsync(app);
        await EnsureOrthodonticAiLogsSchemaAsync(app);
        await EnsurePhotoAnalysisSchemaAsync(app);
        await EnsureCephAnalysisVersionsSchemaAsync(app);
        await EnsureCephApprovalColumnsAsync(app);
        await EnsurePaymentCurrencyColumnAsync(app);
        await EnsureMultiCurrencyColumnsAsync(app);
        await EnsureAppointmentEnhancementsSchemaAsync(app);
        await EnsureServicePackagesConsumablesSchemaAsync(app);
        await EnsureInventoryEnhancementsSchemaAsync(app);
        await EnsurePatientSegmentsSchemaAsync(app);
        await EnsureOrthoSurgicalSchemaAsync(app);
        await EnsureOrthoSurgicalCommentsSchemaAsync(app);
        await EnsureOrthoSurgicalVtoSchemaAsync(app);
        await EnsureOrthoSurgicalPermissionsAsync(app);

        // ── Gated DB maintenance (ENABLE_STARTUP_DB_MAINTENANCE) ──────
        await RunGatedDbMaintenanceAsync(app, configuration);

        // ── HR/Backup tables (unconditional) ──────────────────────────
        await EnsureHrAndBackupTablesAsync(app);
    }

    /// <summary>
    /// Fresh-database bootstrap: if the core "Users" table does not exist
    /// (brand-new empty database), apply all EF migrations immediately —
    /// before any unconditional hotfix can create partial schema that would
    /// break InitialCreate. No-op on existing databases and non-relational
    /// providers. Guarded by the same advisory lock key as gated maintenance
    /// so concurrent instances don't migrate simultaneously.
    /// </summary>
    private static async Task EnsureFreshDatabaseMigratedAsync(WebApplication app, IConfiguration configuration)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            if (!db.Database.IsRelational()) return;

            var usersTableExists = false;
            await db.Database.OpenConnectionAsync();
            using (var checkCmd = db.Database.GetDbConnection().CreateCommand())
            {
                checkCmd.CommandText =
                    "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users')";
                usersTableExists = await checkCmd.ExecuteScalarAsync() is bool b && b;
            }

            if (usersTableExists) return;

            var lockKey = configuration.GetValue<int>("DB_MAINTENANCE_LOCK_KEY", 918273645);
            try
            {
                // BLOCKING acquire (Codex review, PR #353): a loser that merely
                // skipped here would continue into the unconditional hotfixes and
                // create partial tables while the winner is still building the
                // baseline — making the winner's GenerateCreateScript fail with
                // "relation already exists". Instead, wait until the winner
                // finishes, then re-check below whether bootstrap is still needed.
                using var lockCmd = db.Database.GetDbConnection().CreateCommand();
                lockCmd.CommandText = $"SELECT pg_advisory_lock({lockKey})";
                lockCmd.CommandTimeout = 600; // up to 10 min for a full baseline
                await lockCmd.ExecuteNonQueryAsync();
            }
            catch (Exception lockEx)
            {
                logger.LogWarning(lockEx, "Fresh-DB bootstrap: advisory lock unavailable, proceeding without lock");
            }

            try
            {
                // Re-check inside the lock: another instance may have completed
                // the bootstrap while we were waiting.
                using (var recheckCmd = db.Database.GetDbConnection().CreateCommand())
                {
                    recheckCmd.CommandText =
                        "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users')";
                    if (await recheckCmd.ExecuteScalarAsync() is bool again && again)
                    {
                        logger.LogInformation("Fresh-DB bootstrap: another instance completed the baseline while we waited. Skipping.");
                        return;
                    }
                }

                // Decide strategy by history presence:
                //  - Truly empty DB (no history): create the FULL schema from the
                //    current EF model and record every discoverable migration as
                //    applied (baseline). The historical migration chain cannot
                //    replay on an empty database — several hand-written
                //    migrations lack [Migration] attributes so EF can't even see
                //    them, and others reference tables created later.
                //  - Partial DB (history exists but Users missing): fall back to
                //    MigrateAsync and let gated maintenance reconcile.
                var hasHistory = false;
                using (var histCmd = db.Database.GetDbConnection().CreateCommand())
                {
                    histCmd.CommandText =
                        "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory')";
                    hasHistory = await histCmd.ExecuteScalarAsync() is bool h && h;
                }

                // Partial-state guard: the baseline script has no IF NOT EXISTS —
                // it only works on a truly empty schema. Leftover tables from a
                // crashed earlier boot (no history, no Users, but some tables)
                // would break it, so fall back to MigrateAsync + gated
                // reconciliation in that case.
                var existingTableCount = 0L;
                using (var tcCmd = db.Database.GetDbConnection().CreateCommand())
                {
                    tcCmd.CommandText =
                        "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'";
                    existingTableCount = await tcCmd.ExecuteScalarAsync() is long c ? c : 0L;
                }

                if (!hasHistory && existingTableCount > 0)
                {
                    logger.LogWarning(
                        "Fresh-DB bootstrap: database has {Count} tables but no migration history (partial state from a previous failed boot?). Falling back to MigrateAsync.",
                        existingTableCount);
                    hasHistory = true; // route to the MigrateAsync branch below
                }

                if (!hasHistory)
                {
                    logger.LogInformation("Empty database detected — creating full schema from the current EF model (baseline)");
                    var createScript = db.Database.GenerateCreateScript();
                    await db.Database.ExecuteSqlRawAsync(createScript);

                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                            "MigrationId" character varying(150) NOT NULL,
                            "ProductVersion" character varying(32) NOT NULL,
                            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                        );
                        """);

                    var productVersion = Microsoft.EntityFrameworkCore.Infrastructure.ProductInfo.GetVersion();
                    foreach (var migrationId in db.Database.GetMigrations())
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ({0}, {1}) ON CONFLICT DO NOTHING""",
                            migrationId, productVersion);
                    }

                    logger.LogInformation("Fresh-DB baseline complete: schema created from model, migration history recorded");
                }
                else
                {
                    logger.LogInformation("Fresh database detected (no Users table) — applying all EF migrations before startup hotfixes");
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Fresh-DB bootstrap: all migrations applied successfully");
                }
            }
            finally
            {
                try
                {
                    using var unlockCmd = db.Database.GetDbConnection().CreateCommand();
                    unlockCmd.CommandText = $"SELECT pg_advisory_unlock({lockKey})";
                    await unlockCmd.ExecuteScalarAsync();
                }
                catch { /* connection teardown releases the lock anyway */ }
            }
        }
        catch (Exception ex)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Fresh-DB bootstrap failed — startup continues; gated maintenance may still apply migrations");
        }
    }

    /// <summary>
    /// Ensures DeletedAt/DeletedBy exist on every table that has the BaseEntity
    /// shape (IsActive + CreatedAt + UpdatedAt). Adding a nullable column to a
    /// table whose entity doesn't use it is harmless; missing it where the EF
    /// model expects it breaks every query against that table.
    /// </summary>
    private static async Task EnsureSoftDeleteColumnsOnBaseEntityTablesAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                DO $$
                DECLARE t record;
                BEGIN
                    FOR t IN
                        SELECT c1.table_name
                        FROM information_schema.columns c1
                        WHERE c1.table_schema = 'public' AND c1.column_name = 'IsActive'
                          AND EXISTS (SELECT 1 FROM information_schema.columns c2
                                      WHERE c2.table_schema = 'public' AND c2.table_name = c1.table_name AND c2.column_name = 'CreatedAt')
                          AND EXISTS (SELECT 1 FROM information_schema.columns c2
                                      WHERE c2.table_schema = 'public' AND c2.table_name = c1.table_name AND c2.column_name = 'UpdatedAt')
                    LOOP
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns c3
                                       WHERE c3.table_schema = 'public' AND c3.table_name = t.table_name AND c3.column_name = 'DeletedAt') THEN
                            EXECUTE format('ALTER TABLE %I ADD COLUMN "DeletedAt" timestamp with time zone NULL', t.table_name);
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns c3
                                       WHERE c3.table_schema = 'public' AND c3.table_name = t.table_name AND c3.column_name = 'DeletedBy') THEN
                            EXECUTE format('ALTER TABLE %I ADD COLUMN "DeletedBy" uuid NULL', t.table_name);
                        END IF;
                    END LOOP;
                END $$;
                """);

            logger.LogInformation("Soft-delete columns verified on all BaseEntity-shaped tables");
        }
        catch (Exception ex)
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to ensure soft-delete columns on BaseEntity tables (non-fatal)");
        }
    }

    /// <summary>
    /// Users/Doctors table schema verification — MustChangePassword, DeletedAt, DeletedBy, PasswordSalt, CompensationType columns.
    /// </summary>
    private static async Task EnsureUsersDoctorsSchemaAsync(WebApplication app)
    {
        try
        {
            using var schemaScope = app.Services.CreateScope();
            var schemaDb = schemaScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var schemaLogger = schemaScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await schemaDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- Users.MustChangePassword (SEC-02 FIX, migration 20260525000000)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword') THEN
                            ALTER TABLE "Users" ADD COLUMN "MustChangePassword" boolean NOT NULL DEFAULT false;
                        END IF;
                        -- Users.DeletedAt (soft-delete, migration 20260522000000)
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "Users" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        -- Users.DeletedBy (soft-delete, migration 20260522000000)
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "Users" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                        -- Users.PasswordSalt (per-user salt, migration 20260521000000)
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt') THEN
                            ALTER TABLE "Users" ADD COLUMN "PasswordSalt" text NOT NULL DEFAULT '';
                        END IF;
                    END IF;

                    -- Doctors.DeletedAt (soft-delete, migration 20260522000000)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Doctors') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "Doctors" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "Doctors" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                        -- Doctors.CompensationType (Sprint 6)
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType') THEN
                            ALTER TABLE "Doctors" ADD COLUMN "CompensationType" character varying(20) NOT NULL DEFAULT 'None';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'DefaultCommissionPercentage') THEN
                            ALTER TABLE "Doctors" ADD COLUMN "DefaultCommissionPercentage" numeric(5,2) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationNotes') THEN
                            ALTER TABLE "Doctors" ADD COLUMN "CompensationNotes" character varying(500) NULL;
                        END IF;
                    END IF;
                END $$;
            """);

            schemaLogger.LogInformation("HOTFIX: Users/Doctors table schema verified — critical columns ensured");
        }
        catch (Exception ex)
        {
            var schemaLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            schemaLogger2.LogError(ex, "HOTFIX: Failed to ensure Users/Doctors table schema. Staff login may fail with PostgresException!");
        }

    }

    /// <summary>
    /// MessageAttachments table creation + FK + index + DeletedBy column.
    /// </summary>
    private static async Task EnsureMessageAttachmentsSchemaAsync(WebApplication app)
    {
        try
        {
            using var maScope = app.Services.CreateScope();
            var maDb     = maScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var maLogger = maScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await maDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- Create MessageAttachments table if not present (migration 20260602000000)
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'MessageAttachments'
                    ) THEN
                        CREATE TABLE "MessageAttachments" (
                            "Id"        uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "MessageId" uuid                     NOT NULL,
                            "FileUrl"   character varying(1000)  NOT NULL,
                            "FileName"  character varying(255)   NOT NULL,
                            "FileSize"  bigint                   NOT NULL DEFAULT 0,
                            "MimeType"  character varying(100)   NOT NULL,
                            "IsActive"  boolean                  NOT NULL DEFAULT true,
                            "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                            "DeletedAt" timestamp with time zone NULL,
                            "DeletedBy" uuid                     NULL,
                            CONSTRAINT "PK_MessageAttachments" PRIMARY KEY ("Id")
                        );
                    END IF;

                    -- CRITICAL FIX: Add DeletedBy column if missing (causes "column DeletedBy does not exist" → 500 on conversation open)
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'MessageAttachments' AND column_name = 'DeletedBy'
                    ) THEN
                        ALTER TABLE "MessageAttachments" ADD COLUMN "DeletedBy" uuid NULL;
                    END IF;

                    -- Add FK to Messages if missing
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_MessageAttachments_Messages_MessageId'
                    ) THEN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.tables
                            WHERE table_schema = 'public' AND table_name = 'Messages'
                        ) THEN
                            ALTER TABLE "MessageAttachments"
                                ADD CONSTRAINT "FK_MessageAttachments_Messages_MessageId"
                                FOREIGN KEY ("MessageId")
                                REFERENCES "Messages"("Id")
                                ON DELETE CASCADE;
                        END IF;
                    END IF;

                    -- Create index on MessageId if missing
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND tablename  = 'MessageAttachments'
                          AND indexname  = 'IX_MessageAttachments_MessageId'
                    ) THEN
                        CREATE INDEX "IX_MessageAttachments_MessageId"
                            ON "MessageAttachments" ("MessageId");
                    END IF;
                END $$;
            """);

            maLogger.LogInformation("HOTFIX: MessageAttachments table schema ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var maLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            maLogger2.LogError(ex, "HOTFIX: Failed to ensure MessageAttachments schema. Opening conversations will return 500!");
        }

    }

    /// <summary>
    /// BaseEntity columns (DeletedAt/DeletedBy) on all messaging tables: Messages, ConversationParticipants, MessageReads, Conversations.
    /// </summary>
    private static async Task EnsureMessagingBaseEntityColumnsAsync(WebApplication app)
    {
        try
        {
            using var msgSchemaScope = app.Services.CreateScope();
            var msgSchemaDb = msgSchemaScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var msgSchemaLogger = msgSchemaScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await msgSchemaDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- MessageAttachments: add DeletedBy if missing
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MessageAttachments') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageAttachments' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "MessageAttachments" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END IF;

                    -- Messages: add DeletedAt/DeletedBy/IsEdited/EditedAt if missing
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Messages') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "Messages" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "Messages" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                            ALTER TABLE "Messages" ADD COLUMN "IsEdited" boolean NOT NULL DEFAULT false;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
                            ALTER TABLE "Messages" ADD COLUMN "EditedAt" timestamp with time zone NULL;
                        END IF;
                    END IF;

                    -- ConversationParticipants: add DeletedAt/DeletedBy if missing
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ConversationParticipants') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END IF;

                    -- MessageReads: add DeletedAt/DeletedBy if missing
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MessageReads') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "MessageReads" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "MessageReads" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END IF;

                    -- Conversations: add DeletedAt/DeletedBy if missing
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Conversations') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END IF;
                END $$;
            """);

            msgSchemaLogger.LogInformation("HOTFIX: All messaging tables BaseEntity columns ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var msgSchemaLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            msgSchemaLogger2.LogError(ex, "HOTFIX: Failed to ensure messaging tables BaseEntity columns. Messaging may return 500!");
        }

    }

    /// <summary>
    /// Doctor Commission tables/columns: DoctorCommissionPayments table, InvoiceLineItems commission columns, ClinicServices commission defaults, FKs, indexes.
    /// </summary>
    private static async Task EnsureDoctorCommissionSchemaAsync(WebApplication app)
    {
        try
        {
            using var commScope = app.Services.CreateScope();
            var commDb     = commScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var commLogger = commScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await commDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- DoctorCommissionPayments table (migration 20260606000000)
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'DoctorCommissionPayments'
                    ) THEN
                        CREATE TABLE "DoctorCommissionPayments" (
                            "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "DoctorId"      uuid                     NOT NULL,
                            "Amount"        numeric                  NOT NULL,
                            "PaymentDate"   date                     NOT NULL,
                            "PaymentMethod" character varying(50)    NULL,
                            "ReferenceNumber" character varying(100)  NULL,
                            "Notes"         character varying(500)    NULL,
                            "PaidBy"        uuid                     NULL,
                            "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"      boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"     timestamp with time zone  NULL,
                            "DeletedBy"     uuid                     NULL,
                            CONSTRAINT "PK_DoctorCommissionPayments" PRIMARY KEY ("Id")
                        );
                    END IF;

                    -- FK: DoctorCommissionPayments → Doctors
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_DoctorCommissionPayments_Doctors_DoctorId'
                    ) THEN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.tables
                            WHERE table_schema = 'public' AND table_name = 'Doctors'
                        ) THEN
                            ALTER TABLE "DoctorCommissionPayments"
                                ADD CONSTRAINT "FK_DoctorCommissionPayments_Doctors_DoctorId"
                                FOREIGN KEY ("DoctorId")
                                REFERENCES "Doctors"("Id")
                                ON DELETE RESTRICT;
                        END IF;
                    END IF;

                    -- Indexes on DoctorCommissionPayments
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public' AND tablename = 'DoctorCommissionPayments' AND indexname = 'IX_DoctorCommissionPayments_DoctorId'
                    ) THEN
                        CREATE INDEX "IX_DoctorCommissionPayments_DoctorId" ON "DoctorCommissionPayments" ("DoctorId");
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public' AND tablename = 'DoctorCommissionPayments' AND indexname = 'IX_DoctorCommissionPayments_PaymentDate'
                    ) THEN
                        CREATE INDEX "IX_DoctorCommissionPayments_PaymentDate" ON "DoctorCommissionPayments" ("PaymentDate");
                    END IF;

                    -- InvoiceLineItems: commission columns (migration 20260606000000)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'InvoiceLineItems') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorId') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "DoctorId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LineDiscountAmount') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "LineDiscountAmount" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'MaterialCost') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "MaterialCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabCost') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "LabCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'OtherDirectCost') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "OtherDirectCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionBaseRule') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionBaseRule" integer NOT NULL DEFAULT 2;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionPercentage') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "DoctorCommissionPercentage" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'NetCommissionableAmount') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "NetCommissionableAmount" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionAmount') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "DoctorCommissionAmount" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CenterShareAmount') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "CenterShareAmount" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionStatus') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionStatus" integer NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionNotes') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionNotes" text NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabOrderId') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "LabOrderId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionApprovedBy') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionApprovedBy" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionApprovedAt') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionApprovedAt" timestamp with time zone NULL;
                        END IF;
                    END IF;

                    -- ClinicServices: commission defaults columns (migration 20260606000000)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCost') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCostType') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCostType" integer NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultLabCost') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultLabCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultDoctorCommissionPercentage') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultDoctorCommissionPercentage" numeric NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionBaseRule') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "CommissionBaseRule" integer NOT NULL DEFAULT 2;
                        END IF;
                    END IF;

                    -- ClinicServices: CommissionRecognitionMode column (migration 20260607000000)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "CommissionRecognitionMode" integer NOT NULL DEFAULT 1;
                        END IF;
                    END IF;

                    -- FK: InvoiceLineItems → Doctors
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_Doctors_DoctorId'
                    ) THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorId') THEN
                            ALTER TABLE "InvoiceLineItems"
                                ADD CONSTRAINT "FK_InvoiceLineItems_Doctors_DoctorId"
                                FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id") ON DELETE SET NULL;
                        END IF;
                    END IF;

                    -- FK: InvoiceLineItems → LabOrders
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId'
                    ) THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabOrderId') THEN
                            ALTER TABLE "InvoiceLineItems"
                                ADD CONSTRAINT "FK_InvoiceLineItems_LabOrders_LabOrderId"
                                FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders"("Id") ON DELETE SET NULL;
                        END IF;
                    END IF;

                    -- Indexes on InvoiceLineItems commission columns (only if table exists)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'InvoiceLineItems') THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_indexes
                            WHERE schemaname = 'public' AND tablename = 'InvoiceLineItems' AND indexname = 'IX_InvoiceLineItems_DoctorId'
                        ) THEN
                            CREATE INDEX "IX_InvoiceLineItems_DoctorId" ON "InvoiceLineItems" ("DoctorId");
                        END IF;
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_indexes
                            WHERE schemaname = 'public' AND tablename = 'InvoiceLineItems' AND indexname = 'IX_InvoiceLineItems_LabOrderId'
                        ) THEN
                            CREATE INDEX "IX_InvoiceLineItems_LabOrderId" ON "InvoiceLineItems" ("LabOrderId");
                        END IF;
                    END IF;
                END $$;
            """);

            commLogger.LogInformation("HOTFIX: Doctor Commission tables/columns schema ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var commLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            commLogger2.LogError(ex, "HOTFIX: Failed to ensure Doctor Commission schema. Commission endpoints may return 404/500!");
        }

    }

    /// <summary>
    /// ClinicServices and ClinicRooms tables creation + commission columns if missing.
    /// </summary>
    private static async Task EnsureClinicServicesAndRoomsSchemaAsync(WebApplication app)
    {
        try
        {
            using var svcScope = app.Services.CreateScope();
            var svcDb     = svcScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var svcLogger = svcScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await svcDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- ── Create ClinicServices table if not exists ───────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ClinicServices') THEN
                        CREATE TABLE "ClinicServices" (
                            "Id"                              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "ArabicName"                      character varying(200)   NOT NULL,
                            "EnglishName"                     character varying(200)   NULL,
                            "Code"                            character varying(50)    NOT NULL,
                            "Department"                      character varying(100)   NULL,
                            "Category"                        character varying(30)    NOT NULL DEFAULT 'Other',
                            "Description"                     character varying(1000)  NULL,
                            "DefaultDurationMinutes"          integer                  NOT NULL DEFAULT 30,
                            "DefaultPrice"                    numeric(12,2)            NOT NULL DEFAULT 0,
                            "RequiresDoctor"                  boolean                  NOT NULL DEFAULT true,
                            "RequiresConsultationFee"         boolean                  NOT NULL DEFAULT false,
                            "ShowInBooking"                   boolean                  NOT NULL DEFAULT true,
                            "ShowInReception"                 boolean                  NOT NULL DEFAULT true,
                            "ShowInTreatmentPlan"             boolean                  NOT NULL DEFAULT true,
                            "SortOrder"                       integer                  NOT NULL DEFAULT 0,
                            "DefaultMaterialCost"             numeric                  NOT NULL DEFAULT 0,
                            "DefaultMaterialCostType"         integer                  NOT NULL DEFAULT 0,
                            "DefaultLabCost"                  numeric                  NOT NULL DEFAULT 0,
                            "DefaultDoctorCommissionPercentage" numeric                NULL,
                            "CommissionBaseRule"              integer                  NOT NULL DEFAULT 2,
                            "CommissionRecognitionMode"       integer                  NOT NULL DEFAULT 1,
                            "IsActive"                        boolean                  NOT NULL DEFAULT true,
                            "CreatedAt"                       timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"                       timestamp with time zone  NOT NULL DEFAULT now(),
                            "DeletedAt"                       timestamp with time zone  NULL,
                            "DeletedBy"                       uuid                     NULL,
                            CONSTRAINT "PK_ClinicServices" PRIMARY KEY ("Id")
                        );
                        CREATE UNIQUE INDEX "IX_ClinicServices_Code" ON "ClinicServices" ("Code");
                    END IF;

                    -- ── Add commission columns if table exists but columns are missing ──
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCost') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCostType') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCostType" integer NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultLabCost') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultLabCost" numeric NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultDoctorCommissionPercentage') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "DefaultDoctorCommissionPercentage" numeric NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionBaseRule') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "CommissionBaseRule" integer NOT NULL DEFAULT 2;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "CommissionRecognitionMode" integer NOT NULL DEFAULT 1;
                        END IF;
                    END IF;

                    -- YOLO-S2: Color column (nullable hex string for calendar/queue display)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'Color') THEN
                            ALTER TABLE "ClinicServices" ADD COLUMN "Color" character varying(20) NULL;
                        END IF;
                    END IF;

                    -- ── Create ClinicRooms table if not exists ──────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ClinicRooms') THEN
                        CREATE TABLE "ClinicRooms" (
                            "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "ArabicName"    character varying(200)   NOT NULL,
                            "EnglishName"   character varying(200)   NULL,
                            "Code"          character varying(50)    NOT NULL,
                            "RoomType"      character varying(30)    NOT NULL DEFAULT 'Treatment',
                            "SortOrder"     integer                  NOT NULL DEFAULT 0,
                            "IsActive"      boolean                  NOT NULL DEFAULT true,
                            "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "DeletedAt"     timestamp with time zone  NULL,
                            "DeletedBy"     uuid                     NULL,
                            CONSTRAINT "PK_ClinicRooms" PRIMARY KEY ("Id")
                        );
                        CREATE UNIQUE INDEX "IX_ClinicRooms_Code" ON "ClinicRooms" ("Code");
                        CREATE INDEX "IX_ClinicRooms_RoomType" ON "ClinicRooms" ("RoomType");
                    END IF;
                END $$;
            """);

            svcLogger.LogInformation("HOTFIX: ClinicServices and ClinicRooms tables schema ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var svcLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            svcLogger2.LogError(ex, "HOTFIX: Failed to ensure ClinicServices/ClinicRooms schema. Services and Commission endpoints may return 500!");
        }

    }

    /// <summary>
    /// PasswordResetTokens and PasswordResetRequests tables + indexes + FKs + Users.EmailConfirmed column.
    /// </summary>
    private static async Task EnsurePasswordResetSchemaAsync(WebApplication app)
    {
        try
        {
            using var prsScope = app.Services.CreateScope();
            var prsDb     = prsScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var prsLogger = prsScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await prsDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- PasswordResetTokens table
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'PasswordResetTokens'
                    ) THEN
                        CREATE TABLE "PasswordResetTokens" (
                            "Id"        uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "UserId"    uuid                     NOT NULL,
                            "TokenHash" character varying(200)   NOT NULL,
                            "ExpiresAt" timestamp with time zone NOT NULL,
                            "IsUsed"    boolean                  NOT NULL DEFAULT false,
                            "UsedAt"    timestamp with time zone  NULL,
                            "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                            CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY ("Id")
                        );
                    END IF;

                    -- Index on TokenHash (unique)
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public' AND tablename = 'PasswordResetTokens' AND indexname = 'IX_PasswordResetTokens_TokenHash'
                    ) THEN
                        CREATE UNIQUE INDEX "IX_PasswordResetTokens_TokenHash" ON "PasswordResetTokens" ("TokenHash");
                    END IF;

                    -- Index on UserId
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public' AND tablename = 'PasswordResetTokens' AND indexname = 'IX_PasswordResetTokens_UserId'
                    ) THEN
                        CREATE INDEX "IX_PasswordResetTokens_UserId" ON "PasswordResetTokens" ("UserId");
                    END IF;

                    -- FK: PasswordResetTokens → Users
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_PasswordResetTokens_Users_UserId'
                    ) THEN
                        ALTER TABLE "PasswordResetTokens"
                            ADD CONSTRAINT "FK_PasswordResetTokens_Users_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;

                    -- PasswordResetRequests table
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'PasswordResetRequests'
                    ) THEN
                        CREATE TABLE "PasswordResetRequests" (
                            "Id"                uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "UserId"            uuid                     NULL,
                            "UsernameOrEmail"   character varying(200)   NOT NULL,
                            "RequestedAt"       timestamp with time zone NOT NULL,
                            "Status"            character varying(20)    NOT NULL DEFAULT 'Pending',
                            "RequestedIpHash"   character varying(200)   NULL,
                            "UserAgent"         character varying(500)   NULL,
                            "ApprovedByUserId"  uuid                     NULL,
                            "ApprovedAt"        timestamp with time zone  NULL,
                            "Notes"             character varying(500)   NULL,
                            "CreatedAt"         timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"         timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"          boolean                  NOT NULL DEFAULT true,
                            CONSTRAINT "PK_PasswordResetRequests" PRIMARY KEY ("Id")
                        );
                    END IF;

                    -- Index on Status
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public' AND tablename = 'PasswordResetRequests' AND indexname = 'IX_PasswordResetRequests_Status'
                    ) THEN
                        CREATE INDEX "IX_PasswordResetRequests_Status" ON "PasswordResetRequests" ("Status");
                    END IF;

                    -- Index on UserId
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public' AND tablename = 'PasswordResetRequests' AND indexname = 'IX_PasswordResetRequests_UserId'
                    ) THEN
                        CREATE INDEX "IX_PasswordResetRequests_UserId" ON "PasswordResetRequests" ("UserId");
                    END IF;

                    -- FK: PasswordResetRequests → Users (UserId)
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_PasswordResetRequests_Users_UserId'
                    ) THEN
                        ALTER TABLE "PasswordResetRequests"
                            ADD CONSTRAINT "FK_PasswordResetRequests_Users_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE SET NULL;
                    END IF;

                    -- FK: PasswordResetRequests → Users (ApprovedByUserId)
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_PasswordResetRequests_Users_ApprovedByUserId'
                    ) THEN
                        ALTER TABLE "PasswordResetRequests"
                            ADD CONSTRAINT "FK_PasswordResetRequests_Users_ApprovedByUserId"
                            FOREIGN KEY ("ApprovedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL;
                    END IF;

                    -- Users.EmailConfirmed column (for admin email verification)
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'EmailConfirmed') THEN
                            ALTER TABLE "Users" ADD COLUMN "EmailConfirmed" boolean NOT NULL DEFAULT false;
                        END IF;
                    END IF;
                END $$;
            """);

            prsLogger.LogInformation("HOTFIX: PasswordResetTokens/PasswordResetRequests tables schema ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var prsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            prsLogger2.LogError(ex, "HOTFIX: Failed to ensure PasswordReset tables schema. Forgot-password endpoint may return 500!");
        }

    }

    /// <summary>
    /// EmailLogs table + indexes for email statistics and daily limit monitoring.
    /// </summary>
    private static async Task EnsureEmailLogsSchemaAsync(WebApplication app)
    {
        try
        {
            using var elScope = app.Services.CreateScope();
            var elDb     = elScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var elLogger = elScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await elDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'EmailLogs') THEN
                        CREATE TABLE "EmailLogs" (
                            "Id"                uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "ToEmail"           text                     NOT NULL,
                            "Subject"           text                     NOT NULL,
                            "Category"          character varying(50)    NOT NULL DEFAULT 'general',
                            "Provider"          character varying(20)    NULL,
                            "IsSent"            boolean                  NOT NULL DEFAULT false,
                            "ErrorMessage"      text                     NULL,
                            "ExternalId"        character varying(100)   NULL,
                            "RelatedEntityType" character varying(50)    NULL,
                            "RelatedEntityId"   uuid                     NULL,
                            "CreatedAt"         timestamp with time zone NOT NULL DEFAULT now(),
                            CONSTRAINT "PK_EmailLogs" PRIMARY KEY ("Id")
                        );
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EmailLogs' AND indexname = 'IX_EmailLogs_IsSent_CreatedAt') THEN
                        CREATE INDEX "IX_EmailLogs_IsSent_CreatedAt" ON "EmailLogs" ("IsSent", "CreatedAt");
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EmailLogs' AND indexname = 'IX_EmailLogs_Category_CreatedAt') THEN
                        CREATE INDEX "IX_EmailLogs_Category_CreatedAt" ON "EmailLogs" ("Category", "CreatedAt");
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EmailLogs' AND indexname = 'IX_EmailLogs_RelatedEntity') THEN
                        CREATE INDEX "IX_EmailLogs_RelatedEntity" ON "EmailLogs" ("RelatedEntityType", "RelatedEntityId");
                    END IF;
                END $$;
            """);

            elLogger.LogInformation("HOTFIX: EmailLogs table schema ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var elLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            elLogger2.LogError(ex, "HOTFIX: Failed to ensure EmailLogs table schema. Email statistics and reminder tracking may fail!");
        }

    }

    /// <summary>
    /// Reminder tracking columns (EmailReminderSentAt, WhatsAppReminderSentAt, EmailReminderWindowsSent) on Appointments + Email on Patients.
    /// </summary>
    private static async Task EnsureReminderTrackingColumnsAsync(WebApplication app)
    {
        try
        {
            using var rtScope = app.Services.CreateScope();
            var rtDb     = rtScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rtLogger = rtScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await rtDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- Add Email column to Patients if missing
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Patients' AND column_name = 'Email') THEN
                        ALTER TABLE "Patients" ADD COLUMN "Email" text NULL;
                    END IF;

                    -- Add reminder tracking columns to Appointments if missing
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Appointments' AND column_name = 'EmailReminderSentAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "EmailReminderSentAt" timestamp with time zone NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Appointments' AND column_name = 'WhatsAppReminderSentAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "WhatsAppReminderSentAt" timestamp with time zone NULL;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Appointments' AND column_name = 'EmailReminderWindowsSent') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "EmailReminderWindowsSent" text NULL;
                    END IF;
                END $$;
            """);

            rtLogger.LogInformation("HOTFIX: Reminder tracking columns ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var rtLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            rtLogger2.LogError(ex, "HOTFIX: Failed to ensure reminder tracking columns. Appointment/patient queries may return 500!");
        }

    }

    /// <summary>
    /// Invoices/InvoiceLineItems/Payments schema + comprehensive __EFMigrationsHistory reconciliation.
    /// </summary>
    private static async Task EnsureInvoicesAndMigrationHistoryAsync(WebApplication app)
    {
        try
        {
            using var invScope = app.Services.CreateScope();
            var invDb     = invScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invLogger = invScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await invDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- ── Create Invoices table if not exists ──────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Invoices') THEN
                        CREATE TABLE "Invoices" (
                            "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "PatientId"     uuid                     NOT NULL,
                            "VisitId"       uuid                     NULL,
                            "AppointmentId" uuid                     NULL,
                            "InvoiceNumber" character varying(50)    NOT NULL,
                            "Status"        character varying(20)    NOT NULL,
                            "Subtotal"      numeric(12,2)            NOT NULL DEFAULT 0,
                            "DiscountAmount" numeric(12,2)           NULL,
                            "TaxAmount"     numeric(12,2)            NULL,
                            "TotalAmount"   numeric(12,2)            NOT NULL DEFAULT 0,
                            "Notes"         text                     NULL,
                            "CreatedBy"     uuid                     NULL,
                            "UpdatedBy"     uuid                     NULL,
                            "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"      boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"     timestamp with time zone  NULL,
                            "DeletedBy"     uuid                     NULL,
                            CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_Invoices_PatientId" ON "Invoices" ("PatientId");
                        CREATE INDEX "IX_Invoices_VisitId" ON "Invoices" ("VisitId");
                        CREATE INDEX "IX_Invoices_AppointmentId" ON "Invoices" ("AppointmentId");
                        CREATE INDEX "IX_Invoices_Status" ON "Invoices" ("Status");
                        CREATE UNIQUE INDEX "IX_Invoices_InvoiceNumber" ON "Invoices" ("InvoiceNumber");

                        -- FK: Invoices → Patients
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Invoices_Patients_PatientId') THEN
                            ALTER TABLE "Invoices" ADD CONSTRAINT "FK_Invoices_Patients_PatientId"
                                FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE RESTRICT;
                        END IF;
                    END IF;

                    -- ── Create InvoiceLineItems table if not exists ────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'InvoiceLineItems') THEN
                        CREATE TABLE "InvoiceLineItems" (
                            "Id"                          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "InvoiceId"                   uuid                     NOT NULL,
                            "ServiceId"                   uuid                     NULL,
                            "ServiceNameSnapshot"         character varying(200)   NOT NULL DEFAULT '',
                            "Description"                 character varying(500)   NOT NULL DEFAULT '',
                            "Quantity"                    integer                  NOT NULL DEFAULT 1,
                            "UnitPrice"                   numeric(12,2)            NOT NULL DEFAULT 0,
                            "TotalPrice"                  numeric(12,2)            NOT NULL DEFAULT 0,
                            "RelatedTreatmentPlanStepId"  uuid                     NULL,
                            "RelatedVisitId"              uuid                     NULL,
                            "SortOrder"                   integer                  NOT NULL DEFAULT 0,
                            "DoctorId"                    uuid                     NULL,
                            "LineDiscountAmount"          numeric                  NOT NULL DEFAULT 0,
                            "MaterialCost"                numeric                  NOT NULL DEFAULT 0,
                            "LabCost"                     numeric                  NOT NULL DEFAULT 0,
                            "OtherDirectCost"             numeric                  NOT NULL DEFAULT 0,
                            "CommissionBaseRule"           integer                  NOT NULL DEFAULT 2,
                            "DoctorCommissionPercentage"  numeric                  NOT NULL DEFAULT 0,
                            "NetCommissionableAmount"     numeric                  NOT NULL DEFAULT 0,
                            "DoctorCommissionAmount"      numeric                  NOT NULL DEFAULT 0,
                            "CenterShareAmount"           numeric                  NOT NULL DEFAULT 0,
                            "CommissionStatus"            integer                  NOT NULL DEFAULT 0,
                            "CommissionNotes"             text                     NULL,
                            "LabOrderId"                  uuid                     NULL,
                            "CommissionApprovedBy"        uuid                     NULL,
                            "CommissionApprovedAt"        timestamp with time zone  NULL,
                            "CreatedAt"                   timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"                   timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"                    boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"                   timestamp with time zone  NULL,
                            "DeletedBy"                   uuid                     NULL,
                            CONSTRAINT "PK_InvoiceLineItems" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_InvoiceLineItems_InvoiceId" ON "InvoiceLineItems" ("InvoiceId");
                        CREATE INDEX "IX_InvoiceLineItems_ServiceId" ON "InvoiceLineItems" ("ServiceId");
                        CREATE INDEX "IX_InvoiceLineItems_DoctorId" ON "InvoiceLineItems" ("DoctorId");
                        CREATE INDEX "IX_InvoiceLineItems_LabOrderId" ON "InvoiceLineItems" ("LabOrderId");

                        -- FK: InvoiceLineItems → Invoices
                        ALTER TABLE "InvoiceLineItems" ADD CONSTRAINT "FK_InvoiceLineItems_Invoices_InvoiceId"
                            FOREIGN KEY ("InvoiceId") REFERENCES "Invoices"("Id") ON DELETE CASCADE;
                        -- FK: InvoiceLineItems → ClinicServices
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                            ALTER TABLE "InvoiceLineItems" ADD CONSTRAINT "FK_InvoiceLineItems_ClinicServices_ServiceId"
                                FOREIGN KEY ("ServiceId") REFERENCES "ClinicServices"("Id") ON DELETE SET NULL;
                        END IF;
                        -- FK: InvoiceLineItems → Doctors
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Doctors') THEN
                            ALTER TABLE "InvoiceLineItems" ADD CONSTRAINT "FK_InvoiceLineItems_Doctors_DoctorId"
                                FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id") ON DELETE SET NULL;
                        END IF;
                    END IF;

                    -- ── Add InvoiceId to Payments if missing ───────────────────
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Payments') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId') THEN
                            ALTER TABLE "Payments" ADD COLUMN "InvoiceId" uuid NULL;
                            CREATE INDEX "IX_Payments_InvoiceId" ON "Payments" ("InvoiceId");
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_Invoices_InvoiceId') THEN
                            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId') THEN
                                ALTER TABLE "Payments" ADD CONSTRAINT "FK_Payments_Invoices_InvoiceId"
                                    FOREIGN KEY ("InvoiceId") REFERENCES "Invoices"("Id") ON DELETE SET NULL;
                            END IF;
                        END IF;
                    END IF;

                    -- ── Fix __EFMigrationsHistory ──────────────────────────────
                    -- Comprehensive cleanup: delete records for migrations whose schema doesn't exist,
                    -- then insert records for schema that does exist.
                    -- This fixes the problem where a previous reconciliation inserted ALL migration
                    -- records, causing MigrateAsync() to skip creating missing tables like ClinicServices.

                    -- DELETE records where primary schema element doesn't exist
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'PatientId');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502010000_AddSecurePatientPortalPasswordAuth'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddConversationRecipientType'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'RecipientType');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260507000000_AddBookingRequests'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508052207_AddBookingRequest'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditFields'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511000000_AddDoctorIdToBookingRequest'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'BookingRequests' AND column_name = 'DoctorId');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_AddRadiographFileMetadata'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicalPhotos' AND column_name = 'FileType');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513000000_AddDoctorCompensationFields'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514000000_AddClinicQueueItem'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520000000_AddClinicQueueItemTrackingFields'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledAt');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520202816_SyncAuditPhase2Configurations'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AuditLogs');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddPasswordSaltAndPatientPhoneIndexes'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522000000_AddSoftDeleteColumnsToLegacyTables'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260524000000_AddConversationPatientBranchFieldsAndIndexes'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525000000_AddMissingFKIndexesAndUserMustChangePassword'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528000000_AddClinicServicesAndRooms'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529000000_AddPatientJourneyFields'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'ReferralSource');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260530000000_AddPatientTreatmentPlanSteps'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'TreatmentPlanSteps');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260531000000_AddInvoicesAndInvoiceLineItems'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Invoices');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddInvoicePaymentLink'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260602000000_AddMessageAttachments'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageAttachments');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603000000_AddOrthoDiagnosisRetentionPhotos'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OrthoDiagnoses' AND column_name = 'RetentionPhotoLeft');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260604000000_AddSuppliersAndPurchases'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260606000000_AddDoctorCommissionSystem'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionPercentage');
                    DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607000000_AddCommissionRecognitionMode'
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode');

                    -- INSERT records for schema that DOES exist (created by HOTFIX blocks)
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType')
                        AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType') THEN
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260430221624_AddConversationPatientAndType', '8.0');
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Invoices')
                        AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260531000000_AddInvoicesAndInvoiceLineItems') THEN
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260531000000_AddInvoicesAndInvoiceLineItems', '8.0');
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId')
                        AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddInvoicePaymentLink') THEN
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260601000000_AddInvoicePaymentLink', '8.0');
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'DoctorCommissionPayments')
                        AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260606000000_AddDoctorCommissionSystem') THEN
                        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260606000000_AddDoctorCommissionSystem', '8.0');
                    END IF;
                END $$;
            """);
            invLogger.LogInformation("HOTFIX: Invoices/InvoiceLineItems/Payments schema ensured and migration history reconciled (idempotent)");
        }
        catch (Exception ex)
        {
            var invLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            invLogger2.LogError(ex, "HOTFIX: Failed to ensure Invoices/InvoiceLineItems schema. Invoice and commission endpoints may return 500!");
        }

    }

    /// <summary>
    /// One-time admin password reset from environment variables (SEC-03 FIX).
    /// </summary>
    private static async Task EnsureAdminPasswordResetAsync(WebApplication app)
    {
        try
        {
            using var resetScope = app.Services.CreateScope();
            var resetDb     = resetScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var resetLogger = resetScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            // Check if reset has already been done
            var alreadyReset = await resetDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Settings') THEN
                        CREATE TABLE "Settings" (
                            "Id" uuid NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                            "Key" character varying(200) NOT NULL,
                            "Value" text NULL,
                            "Category" character varying(50) NULL,
                            "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                            "IsActive" boolean NOT NULL DEFAULT true
                        );
                    END IF;
                END $$;
            """);

            // Check if the admin password reset flag exists using EF Core LINQ
            var flagExists = await resetDb.Settings.AnyAsync(s => s.Key == "admin.password.reset.2026");

            if (!flagExists)
            {
                // SEC-03 FIX: In production, ADMIN_DEFAULT_PASSWORD env var is REQUIRED — no fallback.
                // In development only, a clearly-marked dev default is allowed for convenience.
                var newPassword = Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");

                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    if (app.Environment.IsProduction())
                    {
                        // SEC-03 FIX: Production MUST set ADMIN_DEFAULT_PASSWORD. No fallback allowed.
                        resetLogger.LogCritical(
                            "SEC-03: ADMIN_DEFAULT_PASSWORD environment variable is NOT set in production. " +
                            "Admin password reset SKIPPED for security. " +
                            "Set ADMIN_DEFAULT_PASSWORD and restart, or use ADMIN_RESET_PASSWORD in DbSeeder.");
                        // Skip the password reset entirely — admin keeps existing password
                        goto AdminResetDone;
                    }

                    // SEC-03 FIX: Development-only fallback. The #if DEBUG guard and IsDevelopment() check
                    // ensure this can NEVER be active in production, even if code is misconfigured.
                    if (app.Environment.IsDevelopment())
                    {
                        newPassword = "DevOnly2026!ChangeMe";
                        resetLogger.LogWarning(
                            "SEC-03: ADMIN_DEFAULT_PASSWORD not set. Using DEVELOPMENT-ONLY fallback. " +
                            "This fallback is NEVER active in production (IsDevelopment check). " +
                            "Set ADMIN_DEFAULT_PASSWORD for production deployments.");
                    }
                    else
                    {
                        // Non-production, non-development (e.g., Staging) — also require env var
                        resetLogger.LogCritical(
                            "SEC-03: ADMIN_DEFAULT_PASSWORD not set in {Environment} environment. " +
                            "Admin password reset SKIPPED. Set the variable and restart.",
                            app.Environment.EnvironmentName);
                        goto AdminResetDone;
                    }
                }

                var salt = AqlanDentalPro.Application.Services.AuthService.GenerateSalt();
                var hash = AqlanDentalPro.Application.Services.AuthService.HashPassword(newPassword, salt);

                // Update admin password using EF Core LINQ
                var adminUser = await resetDb.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                if (adminUser != null)
                {
                    adminUser.PasswordHash = hash;
                    adminUser.PasswordSalt = salt;
                    adminUser.IsActive = true;
                }

                // Set the flag using EF Core so this never runs again
                resetDb.Settings.Add(new AqlanDentalPro.Domain.Entities.Setting
                {
                    Key = "admin.password.reset.2026",
                    Value = "done",
                    Category = "system",
                    UpdatedAt = DateTime.UtcNow
                });

                await resetDb.SaveChangesAsync();

                resetLogger.LogInformation("SEC-03: Admin initial password has been set. Username: admin. Change password after first login.");
            }
            else
            {
                resetLogger.LogInformation("SEC-03: Admin password reset already applied, skipping");
            }
            AdminResetDone:;
        }
        catch (Exception ex)
        {
            var resetLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            resetLogger2.LogWarning(ex, "SEC-03: Admin password reset failed (non-fatal)");
        }

    }

    /// <summary>
    /// Website settings seed (Arabic clinic name, hero text, contact info, etc.).
    /// </summary>
    private static async Task EnsureWebsiteSettingsSeedAsync(WebApplication app)
    {
        try
        {
            using var wsScope = app.Services.CreateScope();
            var wsDb     = wsScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wsLogger = wsScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var websiteDefaults = new Dictionary<string, string>
            {
                ["website.clinicName"]           = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
                ["website.heroTitle"]            = "ابتسامة تجمع بين دقة العلم ولمسة الفن",
                ["website.heroSubtitle"]         = "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
                ["website.marketingSlogan"]      = "قيادة طبية… وابتسامة بثقة",
                ["website.aboutText"]            = "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
                ["website.phone"]                = "04-253028",
                ["website.whatsapp"]             = "967770245745",
                ["website.address"]              = "تعز، اليمن — شارع التحرير الأعلى",
                ["website.workingHours"]         = "السبت – الخميس: 8 ص – 8 م",
                ["website.facebook"]             = "",
                ["website.instagram"]            = "",
                ["website.logoUrl"]              = "",
                ["website.heroImageUrl"]         = "",
                ["website.servicesSectionTitle"] = "حلول طبية متكاملة لابتسامة صحية وواثقة",
                ["website.bookingButtonText"]    = "احجز موعدك الآن",
                ["website.whatsappButtonText"]   = "تواصل عبر الواتساب",
            };

            var existingKeys = await wsDb.Settings
                .Where(s => s.Category == "website")
                .Select(s => s.Key)
                .ToListAsync();

            foreach (var (key, value) in websiteDefaults)
            {
                if (!existingKeys.Contains(key))
                {
                    wsDb.Settings.Add(new AqlanDentalPro.Domain.Entities.Setting
                    {
                        Key = key,
                        Value = value,
                        Category = "website",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            if (wsDb.ChangeTracker.HasChanges())
            {
                await wsDb.SaveChangesAsync();
                wsLogger.LogInformation("Website settings seeded ({Count} new keys)", websiteDefaults.Count - existingKeys.Count);
            }
            else
            {
                wsLogger.LogInformation("Website settings already exist, no seeding needed");
            }
        }
        catch (Exception ex)
        {
            var wsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            wsLogger2.LogWarning(ex, "Website settings seed hotfix failed (non-fatal)");
        }

    }

    /// <summary>
    /// CephNorms table (configurable cephalometric norms) + factory seed.
    /// Schema creation is idempotent (CREATE TABLE IF NOT EXISTS — same DDL as
    /// migration 20260625000000_AddCephNorms, which gated maintenance may not
    /// have applied yet). Seeding via CephNormSeeder only inserts when the
    /// table is empty, so admin-edited norms are never overwritten.
    /// </summary>
    private static async Task EnsureCephNormsSchemaAndSeedAsync(WebApplication app)
    {
        try
        {
            using var cnScope = app.Services.CreateScope();
            var cnDb     = cnScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cnLogger = cnScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            if (cnDb.Database.IsRelational())
            {
                await cnDb.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "CephNorms" (
                        "Id" uuid NOT NULL,
                        "MeasurementName" character varying(50) NOT NULL,
                        "NameAr" character varying(200) NULL,
                        "AnalysisGroup" character varying(30) NOT NULL,
                        "NormalValue" numeric NOT NULL,
                        "StdDeviation" numeric NOT NULL,
                        "MinNormal" numeric NULL,
                        "MaxNormal" numeric NULL,
                        "Unit" character varying(10) NOT NULL,
                        "Category" character varying(30) NULL,
                        "InterpretationBelow" character varying(300) NULL,
                        "InterpretationNormal" character varying(300) NULL,
                        "InterpretationAbove" character varying(300) NULL,
                        "SortOrder" integer NOT NULL DEFAULT 0,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        "UpdatedAt" timestamp with time zone NOT NULL,
                        "IsActive" boolean NOT NULL DEFAULT true,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL,
                        "AgeMin" integer NULL,
                        "AgeMax" integer NULL,
                        "Sex" character varying(1) NULL,
                        CONSTRAINT "PK_CephNorms" PRIMARY KEY ("Id")
                    );
                    """);

                // CLIN-10 — age/gender stratification. For clinics whose CephNorms
                // table predates the 20260704000000 migration (created by the
                // runtime DDL above without AgeMin/AgeMax/Sex), ALTER ADD COLUMN
                // IF NOT EXISTS brings them forward. DROP the legacy unique index
                // so the stratified seeder rows (same MeasurementName +
                // AnalysisGroup, different age band / sex) don't violate it.
                // CREATE the composite index that supports the best-match lookup.
                // All statements are idempotent (PostgreSQL IF EXISTS / IF NOT
                // EXISTS guards) so this block is a no-op on clinics already on
                // the new schema.
                await cnDb.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "CephNorms" ADD COLUMN IF NOT EXISTS "AgeMin" integer NULL;
                    ALTER TABLE "CephNorms" ADD COLUMN IF NOT EXISTS "AgeMax" integer NULL;
                    ALTER TABLE "CephNorms" ADD COLUMN IF NOT EXISTS "Sex" character varying(1) NULL;
                    DROP INDEX IF EXISTS "IX_CephNorms_MeasurementName_AnalysisGroup";
                    CREATE INDEX IF NOT EXISTS "IX_CephNorms_MeasurementName_AnalysisGroup_AgeMin_AgeMax_Sex"
                        ON "CephNorms" ("MeasurementName", "AnalysisGroup", "AgeMin", "AgeMax", "Sex");
                    """);
            }

            var inserted = await CephNormSeeder.SeedIfEmptyAsync(cnDb);
            if (inserted > 0)
            {
                cnLogger.LogInformation("Ceph norms seeded ({Count} factory rows)", inserted);
            }
            else
            {
                // Already-seeded clinics: insert only norms missing for newly
                // added analyses (e.g. Jarabak) without touching customized rows.
                var backfilled = await CephNormSeeder.BackfillMissingDefaultsAsync(cnDb);
                if (backfilled > 0)
                    cnLogger.LogInformation("Ceph norms backfilled ({Count} missing factory rows)", backfilled);
                else
                    cnLogger.LogInformation("Ceph norms already exist, no seeding needed");
            }
        }
        catch (Exception ex)
        {
            var cnLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            cnLogger2.LogWarning(ex, "Ceph norms schema/seed hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// OrthodonticAiLogs audit table (Ceph AI assistant — batch C-D). Idempotent
    /// CREATE TABLE IF NOT EXISTS so databases predating the migration still have
    /// it; without it the AI auto-trace / draft-diagnosis audit insert would throw
    /// and mask the honest "AI not configured" message behind a generic 500.
    /// Schema mirrors OrthodonticAiLog (BaseEntity columns + audit fields).
    /// </summary>
    private static async Task EnsureOrthodonticAiLogsSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "OrthodonticAiLogs" (
                    "Id" uuid NOT NULL,
                    "AnalysisId" uuid NOT NULL,
                    "UserId" uuid NULL,
                    "Action" character varying(50) NOT NULL,
                    "ModelId" character varying(100) NULL,
                    "Succeeded" boolean NOT NULL DEFAULT false,
                    "ErrorSummary" character varying(300) NULL,
                    "InputSummary" character varying(300) NULL,
                    "OutputLength" integer NOT NULL DEFAULT 0,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_OrthodonticAiLogs" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_OrthodonticAiLogs_AnalysisId" ON "OrthodonticAiLogs" ("AnalysisId");
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "OrthodonticAiLogs schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// PhotoAnalyses table (saved facial photo analyses). Idempotent
    /// CREATE TABLE IF NOT EXISTS so databases predating migration
    /// 20260629000000_AddPhotoAnalysis still have it. Mirrors PhotoAnalysis +
    /// BaseEntity columns. The FK to OrthoCases is created by the migration on
    /// fresh databases; the app also validates case existence at write time.
    /// </summary>
    private static async Task EnsurePhotoAnalysisSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PhotoAnalyses" (
                    "Id" uuid NOT NULL,
                    "OrthoCaseId" uuid NOT NULL,
                    "ViewType" character varying(20) NOT NULL,
                    "ImageFileUrl" character varying(1000) NOT NULL,
                    "LandmarksJson" text NULL,
                    "MeasurementsJson" text NULL,
                    "DoctorId" uuid NULL,
                    "Notes" character varying(2000) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_PhotoAnalyses" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_PhotoAnalyses_OrthoCaseId" ON "PhotoAnalyses" ("OrthoCaseId");
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "PhotoAnalyses schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// CephAnalysisVersions table (CEPH-EPIC batch C-B — named snapshots of a
    /// ceph analysis for longitudinal progress tracking). Idempotent CREATE
    /// TABLE IF NOT EXISTS so databases predating migration
    /// 20260708000000_AddCephAnalysisVersions still have it. Mirrors
    /// CephAnalysisVersion + BaseEntity columns. The FK to CephAnalyses is
    /// added only when both the parent table and the constraint are absent
    /// (the migration creates it on fresh databases; this hotfix backfills it
    /// on existing databases). Per C-08: a missing column/table here must
    /// NEVER break a ceph save — this hotfix is best-effort and logs on failure.
    /// </summary>
    private static async Task EnsureCephAnalysisVersionsSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "CephAnalysisVersions" (
                    "Id" uuid NOT NULL,
                    "CephAnalysisId" uuid NOT NULL,
                    "Label" character varying(100) NOT NULL,
                    "LandmarksJson" text NOT NULL,
                    "MeasurementsJson" text NOT NULL,
                    "DiagnosisJson" text NULL,
                    "SnapshotDate" date NOT NULL,
                    "CreatedByUserId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_CephAnalysisVersions" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_CephAnalysisVersions_CephAnalysisId"
                    ON "CephAnalysisVersions" ("CephAnalysisId");
                CREATE INDEX IF NOT EXISTS "IX_CephAnalysisVersions_CephAnalysisId_SnapshotDate"
                    ON "CephAnalysisVersions" ("CephAnalysisId", "SnapshotDate");
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephAnalyses')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephAnalysisVersions_CephAnalyses_CephAnalysisId') THEN
                        ALTER TABLE "CephAnalysisVersions"
                            ADD CONSTRAINT "FK_CephAnalysisVersions_CephAnalyses_CephAnalysisId"
                            FOREIGN KEY ("CephAnalysisId") REFERENCES "CephAnalyses" ("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "CephAnalysisVersions schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// Ortho-Surgical (orthognathic) bridge schema (Sprint A1): creates the
    /// "OrthoSurgicalCases", "SurgeonReviews" and "JointPlans" tables on existing
    /// databases. Fresh databases get these from the EF model baseline. Idempotent
    /// (CREATE TABLE / INDEX / CONSTRAINT IF NOT EXISTS) so it runs safely on every
    /// startup. FK constraints are added only when the referenced tables exist.
    /// </summary>
    private static async Task EnsureOrthoSurgicalSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "OrthoSurgicalCases" (
                    "Id" uuid NOT NULL,
                    "CaseNumber" character varying(30) NOT NULL,
                    "PatientId" uuid NOT NULL,
                    "OrthoCaseId" uuid NOT NULL,
                    "CephAnalysisId" uuid NULL,
                    "SurgeryCaseId" uuid NULL,
                    "OrthodontistId" uuid NULL,
                    "SurgeonId" uuid NULL,
                    "BranchId" uuid NULL,
                    "Status" character varying(30) NOT NULL,
                    "DiagnosisSummary" character varying(4000) NULL,
                    "OrthodontistApprovedAt" timestamp with time zone NULL,
                    "SurgeonApprovedAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_OrthoSurgicalCases" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_OrthoSurgicalCases_CaseNumber"
                    ON "OrthoSurgicalCases" ("CaseNumber");
                CREATE INDEX IF NOT EXISTS "IX_OrthoSurgicalCases_PatientId"
                    ON "OrthoSurgicalCases" ("PatientId");
                CREATE INDEX IF NOT EXISTS "IX_OrthoSurgicalCases_OrthoCaseId"
                    ON "OrthoSurgicalCases" ("OrthoCaseId");
                CREATE INDEX IF NOT EXISTS "IX_OrthoSurgicalCases_SurgeryCaseId"
                    ON "OrthoSurgicalCases" ("SurgeryCaseId");
                CREATE INDEX IF NOT EXISTS "IX_OrthoSurgicalCases_Status"
                    ON "OrthoSurgicalCases" ("Status");

                CREATE TABLE IF NOT EXISTS "SurgeonReviews" (
                    "Id" uuid NOT NULL,
                    "OrthoSurgicalCaseId" uuid NOT NULL,
                    "SurgeonId" uuid NULL,
                    "Decision" character varying(40) NOT NULL,
                    "ProposedProcedure" character varying(2000) NULL,
                    "RequiredRecords" character varying(2000) NULL,
                    "Risks" character varying(2000) NULL,
                    "Notes" character varying(4000) NULL,
                    "ReviewedAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_SurgeonReviews" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SurgeonReviews_OrthoSurgicalCaseId"
                    ON "SurgeonReviews" ("OrthoSurgicalCaseId");

                CREATE TABLE IF NOT EXISTS "JointPlans" (
                    "Id" uuid NOT NULL,
                    "OrthoSurgicalCaseId" uuid NOT NULL,
                    "OrthodonticObjectives" character varying(4000) NULL,
                    "SurgicalObjectives" character varying(4000) NULL,
                    "ProcedureType" character varying(200) NULL,
                    "Timing" character varying(500) NULL,
                    "PreSurgicalRequirements" character varying(4000) NULL,
                    "PostSurgicalPlan" character varying(4000) NULL,
                    "Risks" character varying(4000) NULL,
                    "PatientExplanation" character varying(4000) NULL,
                    "OrthodontistApprovedAt" timestamp with time zone NULL,
                    "SurgeonApprovedAt" timestamp with time zone NULL,
                    "LockedAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_JointPlans" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_JointPlans_OrthoSurgicalCaseId"
                    ON "JointPlans" ("OrthoSurgicalCaseId");

                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoSurgicalCases_Patients_PatientId') THEN
                        ALTER TABLE "OrthoSurgicalCases" ADD CONSTRAINT "FK_OrthoSurgicalCases_Patients_PatientId"
                            FOREIGN KEY ("PatientId") REFERENCES "Patients" ("Id") ON DELETE RESTRICT;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OrthoCases')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoSurgicalCases_OrthoCases_OrthoCaseId') THEN
                        ALTER TABLE "OrthoSurgicalCases" ADD CONSTRAINT "FK_OrthoSurgicalCases_OrthoCases_OrthoCaseId"
                            FOREIGN KEY ("OrthoCaseId") REFERENCES "OrthoCases" ("Id") ON DELETE RESTRICT;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SurgeonReviews_OrthoSurgicalCases_OrthoSurgicalCaseId') THEN
                        ALTER TABLE "SurgeonReviews" ADD CONSTRAINT "FK_SurgeonReviews_OrthoSurgicalCases_OrthoSurgicalCaseId"
                            FOREIGN KEY ("OrthoSurgicalCaseId") REFERENCES "OrthoSurgicalCases" ("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JointPlans_OrthoSurgicalCases_OrthoSurgicalCaseId') THEN
                        ALTER TABLE "JointPlans" ADD CONSTRAINT "FK_JointPlans_OrthoSurgicalCases_OrthoSurgicalCaseId"
                            FOREIGN KEY ("OrthoSurgicalCaseId") REFERENCES "OrthoSurgicalCases" ("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "OrthoSurgical schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// Ortho-Surgical discussion-thread schema (Sprint A4): creates "OrthoSurgicalComments"
    /// on existing databases. Fresh databases get it from the EF model baseline. Idempotent,
    /// non-fatal — mirrors <see cref="EnsureOrthoSurgicalSchemaAsync"/>.
    /// </summary>
    private static async Task EnsureOrthoSurgicalCommentsSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "OrthoSurgicalComments" (
                    "Id" uuid NOT NULL,
                    "OrthoSurgicalCaseId" uuid NOT NULL,
                    "AuthorUserId" uuid NULL,
                    "AuthorRole" character varying(40) NULL,
                    "Body" character varying(2000) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_OrthoSurgicalComments" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_OrthoSurgicalComments_OrthoSurgicalCaseId"
                    ON "OrthoSurgicalComments" ("OrthoSurgicalCaseId");

                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoSurgicalComments_OrthoSurgicalCases_OrthoSurgicalCaseId') THEN
                        ALTER TABLE "OrthoSurgicalComments" ADD CONSTRAINT "FK_OrthoSurgicalComments_OrthoSurgicalCases_OrthoSurgicalCaseId"
                            FOREIGN KEY ("OrthoSurgicalCaseId") REFERENCES "OrthoSurgicalCases" ("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "OrthoSurgicalComments schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// Ortho-Surgical VTO (Visual Treatment Objective) schema (Sprint A9): creates the
    /// "OrthoSurgicalVtos" table on existing databases. Fresh databases get it from the EF
    /// model baseline. Idempotent (CREATE TABLE / INDEX / CONSTRAINT IF NOT EXISTS) so it
    /// runs safely on every startup. FK to OrthoSurgicalCases is ON DELETE CASCADE (a deleted
    /// case takes its VTO scenarios with it); FK to CephAnalyses is ON DELETE SET NULL (the
    /// baseline analysis may be archived without losing the stored scenario — predicted
    /// values are already snapshotted). Non-fatal — mirrors
    /// <see cref="EnsureOrthoSurgicalSchemaAsync"/>.
    /// </summary>
    private static async Task EnsureOrthoSurgicalVtoSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "OrthoSurgicalVtos" (
                    "Id" uuid NOT NULL,
                    "OrthoSurgicalCaseId" uuid NOT NULL,
                    "CephAnalysisId" uuid NULL,
                    "MaxillaMoveMm" numeric(6,2) NULL,
                    "MandibleMoveMm" numeric(6,2) NULL,
                    "ChinMoveMm" numeric(6,2) NULL,
                    "RotationDegree" numeric(6,2) NULL,
                    "PredictedSNA" numeric(6,2) NULL,
                    "PredictedSNB" numeric(6,2) NULL,
                    "PredictedANB" numeric(6,2) NULL,
                    "PredictedWits" numeric(6,2) NULL,
                    "PredictedOverjet" numeric(6,2) NULL,
                    "Notes" character varying(4000) NULL,
                    "CreatedBy" uuid NULL,
                    "IsApprovedByOrthodontist" boolean NOT NULL DEFAULT false,
                    "ApprovedAt" timestamp with time zone NULL,
                    "ApprovedByUserId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_OrthoSurgicalVtos" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_OrthoSurgicalVtos_OrthoSurgicalCaseId"
                    ON "OrthoSurgicalVtos" ("OrthoSurgicalCaseId");

                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoSurgicalVtos_OrthoSurgicalCases_OrthoSurgicalCaseId') THEN
                        ALTER TABLE "OrthoSurgicalVtos" ADD CONSTRAINT "FK_OrthoSurgicalVtos_OrthoSurgicalCases_OrthoSurgicalCaseId"
                            FOREIGN KEY ("OrthoSurgicalCaseId") REFERENCES "OrthoSurgicalCases" ("Id") ON DELETE CASCADE;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephAnalyses')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoSurgicalVtos_CephAnalyses_CephAnalysisId') THEN
                        ALTER TABLE "OrthoSurgicalVtos" ADD CONSTRAINT "FK_OrthoSurgicalVtos_CephAnalyses_CephAnalysisId"
                            FOREIGN KEY ("CephAnalysisId") REFERENCES "CephAnalyses" ("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "OrthoSurgicalVto schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// Seeds RolePermissions for the "ortho_surgical" resource (Sprint A1). INSERT-ONLY:
    /// never overwrites an owner's existing customization for a role. Orthodontist and
    /// OralSurgeon get view/create/edit/approve; Admin is implicitly allowed everywhere.
    /// </summary>
    /// <summary>
    /// Doctor room assignments ("تعيينات غرف الأطباء" — CLAUDE.md priority): adds the
    /// nullable Doctors.DefaultClinicRoomId column on existing databases. Fresh databases
    /// get it from the EF model baseline. Idempotent (ADD COLUMN IF NOT EXISTS); the FK is
    /// added only when the ClinicRooms table exists, with ON DELETE SET NULL so deleting a
    /// room clears the assignment rather than blocking.
    /// </summary>
    private static async Task EnsureDoctorDefaultRoomColumnAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Doctors" ADD COLUMN IF NOT EXISTS "DefaultClinicRoomId" uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_Doctors_DefaultClinicRoomId"
                    ON "Doctors" ("DefaultClinicRoomId");
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicRooms')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Doctors_ClinicRooms_DefaultClinicRoomId') THEN
                        ALTER TABLE "Doctors" ADD CONSTRAINT "FK_Doctors_ClinicRooms_DefaultClinicRoomId"
                            FOREIGN KEY ("DefaultClinicRoomId") REFERENCES "ClinicRooms" ("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Doctors.DefaultClinicRoomId schema hotfix failed (non-fatal)");
        }
    }

    private static async Task EnsureOrthoSurgicalPermissionsAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            // (Role, View, Create, Edit, Approve)
            var perms = new (string Role, bool View, bool Create, bool Edit, bool Approve)[]
            {
                ("Admin",        true,  true,  true,  true),
                ("Orthodontist", true,  true,  true,  true),
                ("OralSurgeon",  true,  false, true,  true),
                ("Reception",    true,  false, false, false),
            };

            var existing = await db.RolePermissions
                .Where(rp => rp.Resource == "ortho_surgical")
                .Select(rp => rp.Role)
                .ToListAsync();

            var toAdd = new List<RolePermission>();
            foreach (var (role, view, create, edit, approve) in perms)
            {
                if (existing.Contains(role)) continue;
                toAdd.Add(new RolePermission
                {
                    Role = role,
                    Resource = "ortho_surgical",
                    CanView = view,
                    CanCreate = create,
                    CanEdit = edit,
                    CanDelete = false,
                    CanExport = false,
                    CanApprove = approve
                });
            }

            if (toAdd.Count > 0)
            {
                await db.RolePermissions.AddRangeAsync(toAdd);
                await db.SaveChangesAsync();
                logger.LogInformation("HOTFIX: Seeded {Count} ortho_surgical permissions", toAdd.Count);
            }
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogError(ex, "HOTFIX: Failed to seed ortho_surgical permissions");
        }
    }

    /// <summary>
    /// CEPH-EPIC clinical approval gate (Sprint 6): adds the approval columns to
    /// the "CephAnalyses" table on existing databases. Fresh databases already
    /// get these from the EF model baseline. Idempotent (ADD COLUMN IF NOT
    /// EXISTS) so it runs safely on every startup. Existing analyses are NOT
    /// auto-approved — IsApproved defaults to false.
    /// </summary>
    private static async Task EnsureCephApprovalColumnsAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephAnalyses') THEN
                        ALTER TABLE "CephAnalyses" ADD COLUMN IF NOT EXISTS "IsApproved" boolean NOT NULL DEFAULT false;
                        ALTER TABLE "CephAnalyses" ADD COLUMN IF NOT EXISTS "ApprovedByUserId" uuid NULL;
                        ALTER TABLE "CephAnalyses" ADD COLUMN IF NOT EXISTS "ApprovedAt" timestamp with time zone NULL;
                        ALTER TABLE "CephAnalyses" ADD COLUMN IF NOT EXISTS "ApprovalNotes" text NULL;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "CephAnalyses approval-columns hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// Patient Journey columns on Appointments (ServiceId, ClinicRoomId, RoomName, ArrivedAt, CalledAt, InRoomAt) and Visits (ServiceId, CheckoutStatus, ReadyForCheckoutAt, AmountDueReference).
    /// </summary>
    private static async Task EnsurePatientJourneyColumnsAsync(WebApplication app)
    {
        try
        {
            using var pjScope = app.Services.CreateScope();
            var pjDb     = pjScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pjLogger = pjScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await pjDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- ── Appointments: add ServiceId if missing ───────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ServiceId') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "ServiceId" uuid NULL;
                        CREATE INDEX IF NOT EXISTS "IX_Appointments_ServiceId" ON "Appointments" ("ServiceId");
                    END IF;

                    -- ── Appointments: add ClinicRoomId if missing ───────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ClinicRoomId') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "ClinicRoomId" uuid NULL;
                    END IF;

                    -- ── Appointments: add RoomName if missing (Sprint 4.5 queue fields) ──
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'RoomName') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "RoomName" character varying(50) NULL;
                    END IF;

                    -- ── Appointments: add ArrivedAt if missing ──────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ArrivedAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "ArrivedAt" timestamp with time zone NULL;
                    END IF;

                    -- ── Appointments: add CalledAt if missing ───────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'CalledAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "CalledAt" timestamp with time zone NULL;
                    END IF;

                    -- ── Appointments: add InRoomAt if missing ───────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'InRoomAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "InRoomAt" timestamp with time zone NULL;
                    END IF;

                    -- ── Visits: add ServiceId if missing ─────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'ServiceId') THEN
                        ALTER TABLE "Visits" ADD COLUMN "ServiceId" uuid NULL;
                    END IF;

                    -- ── Visits: add CheckoutStatus if missing ────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'CheckoutStatus') THEN
                        ALTER TABLE "Visits" ADD COLUMN "CheckoutStatus" character varying(30) NULL;
                        CREATE INDEX IF NOT EXISTS "IX_Visits_CheckoutStatus" ON "Visits" ("CheckoutStatus");
                    END IF;

                    -- ── Visits: add ReadyForCheckoutAt if missing ────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'ReadyForCheckoutAt') THEN
                        ALTER TABLE "Visits" ADD COLUMN "ReadyForCheckoutAt" timestamp with time zone NULL;
                    END IF;

                    -- ── Visits: add AmountDueReference if missing ────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'AmountDueReference') THEN
                        ALTER TABLE "Visits" ADD COLUMN "AmountDueReference" numeric(12,2) NULL;
                    END IF;

                    -- ── Visits: add ProposedProcedure if missing ──────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'ProposedProcedure') THEN
                        ALTER TABLE "Visits" ADD COLUMN "ProposedProcedure" character varying(500) NULL;
                    END IF;

                    -- ── Visits: add ServiceId if missing (needed for draft invoice flow) ──
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'ServiceId') THEN
                        ALTER TABLE "Visits" ADD COLUMN "ServiceId" uuid NULL;
                    END IF;

                    -- ── InvoiceLineItems: add ServiceNameSnapshot if missing ──
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'InvoiceLineItems') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'ServiceNameSnapshot') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "ServiceNameSnapshot" character varying(200) NULL;
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'ServiceId') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "ServiceId" uuid NULL;
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'RelatedVisitId') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "RelatedVisitId" uuid NULL;
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'MaterialCost') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "MaterialCost" numeric(12,2) NOT NULL DEFAULT 0;
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LineDiscountAmount') THEN
                            ALTER TABLE "InvoiceLineItems" ADD COLUMN "LineDiscountAmount" numeric NOT NULL DEFAULT 0;
                        END IF;
                    END IF;

                    -- ── ClinicQueueItems: add Priority if missing (migration 20260616) ──
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'Priority') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "Priority" character varying(20) NOT NULL DEFAULT 'Normal';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'SortOrder') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "SortOrder" integer NOT NULL DEFAULT 0;
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'RecallCount') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "RecallCount" integer NOT NULL DEFAULT 0;
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'NoShowAt') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "NoShowAt" timestamp with time zone NULL;
                        END IF;

                        -- ── ClinicQueueItems: add ServiceId if missing (daily-ops flow) ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ServiceId') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "ServiceId" uuid NULL;
                        END IF;

                        -- ── ClinicQueueItems: add ClinicRoomId if missing (daily-ops flow) ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ClinicRoomId') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "ClinicRoomId" uuid NULL;
                        END IF;

                        -- ── ClinicQueueItems: add VisitId if missing (daily-ops flow) ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'VisitId') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "VisitId" uuid NULL;
                        END IF;

                        -- ── ClinicQueueItems: add CalledByUserId if missing ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledByUserId') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "CalledByUserId" uuid NULL;
                        END IF;

                        -- ── ClinicQueueItems: add AddedByUserId if missing ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'AddedByUserId') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "AddedByUserId" uuid NULL;
                        END IF;

                        -- ── ClinicQueueItems: add CancelledAt if missing ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CancelledAt') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "CancelledAt" timestamp with time zone NULL;
                        END IF;

                        -- ── ClinicQueueItems: add EstimatedWaitMinutes if missing ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'EstimatedWaitMinutes') THEN
                            ALTER TABLE "ClinicQueueItems" ADD COLUMN "EstimatedWaitMinutes" integer NULL;
                        END IF;

                        -- Recreate unique index with NoShow filter if old index exists
                        IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'ClinicQueueItems' AND indexname = 'IX_ClinicQueueItems_PatientId_QueueDate') THEN
                            DROP INDEX IF EXISTS "IX_ClinicQueueItems_PatientId_QueueDate";
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'ClinicQueueItems' AND indexname = 'IX_ClinicQueueItems_PatientId_QueueDate') THEN
                            CREATE UNIQUE INDEX "IX_ClinicQueueItems_PatientId_QueueDate"
                                ON "ClinicQueueItems" ("PatientId", "QueueDate")
                                WHERE "Status" NOT IN ('Completed', 'Cancelled', 'NoShow');
                        END IF;

                        -- Priority-based ordering index
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'ClinicQueueItems' AND indexname = 'IX_ClinicQueueItems_QueueDate_Priority_SortOrder') THEN
                            CREATE INDEX "IX_ClinicQueueItems_QueueDate_Priority_SortOrder"
                                ON "ClinicQueueItems" ("QueueDate", "Priority", "SortOrder");
                        END IF;
                    END IF;
                END $$;
            """);

            pjLogger.LogInformation("HOTFIX: Patient Journey columns ensured on Appointments, Visits, and ClinicQueueItems (idempotent)");
        }
        catch (Exception ex)
        {
            var pjLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            pjLogger2.LogError(ex, "HOTFIX: Failed to ensure Patient Journey columns. Patient journey endpoint may return 500!");
        }

    }

    /// <summary>
    /// Seed patient_journey permissions for all roles.
    /// </summary>
    private static async Task EnsurePatientJourneyPermissionsAsync(WebApplication app)
    {
        try
        {
            using var pjpScope = app.Services.CreateScope();
            var pjpDb     = pjpScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pjpLogger = pjpScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var journeyPerms = new (string Role, bool View, bool Create, bool Edit)[]{
                ("Admin",          true, true, true),
                ("Reception",      true, true, true),
                ("Orthodontist",   true, true, false),
                ("GeneralDentist", true, true, false),
                ("OralSurgeon",    true, true, false),
                ("Accountant",     true, false, false),
                ("Assistant",      true, false, false),
            };

            var existingJourney = await pjpDb.RolePermissions
                .Where(rp => rp.Resource == "patient_journey")
                .Select(rp => rp.Role)
                .ToListAsync();

            var toAddJourney = new List<RolePermission>();
            foreach (var (role, view, create, edit) in journeyPerms)
            {
                if (!existingJourney.Contains(role))
                {
                    toAddJourney.Add(new RolePermission
                    {
                        Role = role,
                        Resource = "patient_journey",
                        CanView = view,
                        CanCreate = create,
                        CanEdit = edit,
                        CanDelete = false,
                        CanExport = false,
                        CanApprove = false
                    });
                }
            }

            if (toAddJourney.Count > 0)
            {
                await pjpDb.RolePermissions.AddRangeAsync(toAddJourney);
                await pjpDb.SaveChangesAsync();
                pjpLogger.LogInformation("HOTFIX: Seeded {Count} patient_journey permissions", toAddJourney.Count);
            }
            else
            {
                pjpLogger.LogInformation("HOTFIX: patient_journey permissions already exist, no seeding needed");
            }
        }
        catch (Exception ex)
        {
            var pjpLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            pjpLogger2.LogError(ex, "HOTFIX: Failed to seed patient_journey permissions. Journey Hub may be hidden in sidebar!");
        }

    }

    /// <summary>
    /// SMS Gateway tables (SmsMessages, SmsTemplates) + SmsReminderWindowsSent column on Appointments.
    /// </summary>
    private static async Task EnsureSmsGatewaySchemaAsync(WebApplication app)
    {
        try
        {
            var smsLogger = app.Services.GetRequiredService<ILogger<Program>>();
            using var smsScope = app.Services.CreateScope();
            var smsDb = smsScope.ServiceProvider.GetRequiredService<AqlanDentalPro.Infrastructure.Data.AppDbContext>();

            await smsDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- ── SmsMessages table ──────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SmsMessages') THEN
                        CREATE TABLE "SmsMessages" (
                            "Id" uuid PRIMARY KEY,
                            "PatientId" uuid NOT NULL,
                            "PhoneNumber" character varying(20) NOT NULL,
                            "TemplateType" character varying(50) NOT NULL DEFAULT 'custom',
                            "MessageContent" character varying(1000) NOT NULL,
                            "Status" character varying(20) NOT NULL DEFAULT 'pending',
                            "ExternalId" character varying(100) NULL,
                            "ErrorMessage" character varying(500) NULL,
                            "RetryCount" integer NOT NULL DEFAULT 0,
                            "SentAt" timestamp with time zone NULL,
                            "DeliveredAt" timestamp with time zone NULL,
                            "RelatedEntityId" uuid NULL,
                            "RelatedEntityType" character varying(50) NULL,
                            "Gateway" character varying(30) NOT NULL DEFAULT 'local_android',
                            "CharacterCount" integer NOT NULL DEFAULT 0,
                            "SegmentCount" integer NOT NULL DEFAULT 0,
                            "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                            "IsActive" boolean NOT NULL DEFAULT true,
                            "DeletedAt" timestamp with time zone NULL,
                            "DeletedBy" uuid NULL
                        );
                        CREATE INDEX "IX_SmsMessages_PatientId" ON "SmsMessages" ("PatientId");
                        CREATE INDEX "IX_SmsMessages_Status" ON "SmsMessages" ("Status");
                        CREATE INDEX "IX_SmsMessages_CreatedAt" ON "SmsMessages" ("CreatedAt");
                        CREATE INDEX "IX_SmsMessages_TemplateType" ON "SmsMessages" ("TemplateType");
                        CREATE INDEX "IX_SmsMessages_CreatedAt_Status" ON "SmsMessages" ("CreatedAt", "Status");
                    END IF;

                    -- ── SmsTemplates table ─────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SmsTemplates') THEN
                        CREATE TABLE "SmsTemplates" (
                            "Id" uuid PRIMARY KEY,
                            "TemplateKey" character varying(50) NOT NULL,
                            "NameAr" character varying(100) NOT NULL,
                            "ContentTemplate" character varying(500) NOT NULL,
                            "IsTemplateActive" boolean NOT NULL DEFAULT true,
                            "Category" character varying(30) NOT NULL DEFAULT 'general',
                            "MaxLength" integer NOT NULL DEFAULT 160,
                            "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                            "IsActive" boolean NOT NULL DEFAULT true,
                            "DeletedAt" timestamp with time zone NULL,
                            "DeletedBy" uuid NULL
                        );
                        CREATE UNIQUE INDEX "IX_SmsTemplates_TemplateKey" ON "SmsTemplates" ("TemplateKey");
                        CREATE INDEX "IX_SmsTemplates_Category" ON "SmsTemplates" ("Category");
                    END IF;

                    -- ── Appointments: add SmsReminderWindowsSent if missing ──
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'SmsReminderWindowsSent') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "SmsReminderWindowsSent" character varying(200) NULL;
                    END IF;
                END $$;
            """);

            smsLogger.LogInformation("HOTFIX: SMS Gateway tables + SmsReminderWindowsSent column ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var smsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            smsLogger2.LogError(ex, "HOTFIX: Failed to ensure SMS Gateway tables. SMS endpoints may return 500!");
        }

    }

    /// <summary>
    /// SMS Gateway settings seed (enabled, gateway_mode, sender_name, daily_limit, etc.).
    /// </summary>
    private static async Task EnsureSmsGatewaySettingsSeedAsync(WebApplication app, IConfiguration configuration)
    {
        try
        {
            using var smsSettingsScope = app.Services.CreateScope();
            var smsSettingsDb = smsSettingsScope.ServiceProvider.GetRequiredService<AqlanDentalPro.Infrastructure.Data.AppDbContext>();

            var smsApiUrl = configuration["Sms:ApiUrl"] ?? "";
            var smsApiKey = configuration["Sms:ApiKey"] ?? "";
            var smsGatewayMode = configuration["Sms:GatewayMode"] ?? "cloud_api";

            var smsSettingsSeed = new Dictionary<string, (string Value, string Category)>
            {
                ["sms.enabled"] = ("true", "sms"),
                ["sms.gateway_mode"] = (smsGatewayMode, "sms"),
                ["sms.sender_name"] = ("AqlanDental", "sms"),
                ["sms.daily_limit"] = ("500", "sms"),
                ["sms.send_appointment_reminders"] = ("true", "sms"),
                ["sms.send_payment_reminders"] = ("true", "sms"),
                ["sms.reminder_hours"] = ("24,2", "sms"),
            };

            // Only seed URL and API key from env vars if they are provided
            if (!string.IsNullOrWhiteSpace(smsApiUrl))
                smsSettingsSeed["sms.api_url"] = (smsApiUrl, "sms");
            if (!string.IsNullOrWhiteSpace(smsApiKey))
                smsSettingsSeed["sms.api_key"] = (smsApiKey, "sms");

            foreach (var (key, (value, category)) in smsSettingsSeed)
            {
                var existing = await smsSettingsDb.Settings.FirstOrDefaultAsync(s => s.Key == key);
                if (existing == null)
                {
                    smsSettingsDb.Settings.Add(new AqlanDentalPro.Domain.Entities.Setting
                    {
                        Key = key,
                        Value = value,
                        Category = category,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else if (key == "sms.gateway_mode" && existing.Value != smsGatewayMode)
                {
                    existing.Value = value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await smsSettingsDb.SaveChangesAsync();
            var smsSettingsLogger = app.Services.GetRequiredService<ILogger<Program>>();
            smsSettingsLogger.LogInformation("SMS Gateway settings seeded (mode={Mode}, hasUrl={HasUrl}, hasKey={HasKey})", smsGatewayMode, !string.IsNullOrWhiteSpace(smsApiUrl), !string.IsNullOrWhiteSpace(smsApiKey));
        }
        catch (Exception ex)
        {
            var smsSettingsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            smsSettingsLogger2.LogError(ex, "Failed to seed SMS Gateway settings");
        }

    }

    /// <summary>
    /// LabOrders table schema hotfix — adds BranchId, VisitId, Shade, RestorationType, DeliveredDate,
    /// CancellationReason, LabId, TotalCost, InvoiceLineItemId, RemakeReason, ReturnReason, RemakeCost,
    /// IsFreeRemake, OriginalOrderId, RemakeCount columns if missing.
    /// Fixes "column l.BranchId does not exist" 500 error on patient-journey/today.
    /// </summary>
    private static async Task EnsureLabOrdersSchemaAsync(WebApplication app)
    {
        try
        {
            using var labScope = app.Services.CreateScope();
            var labDb     = labScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var labLogger = labScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await labDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabOrders') THEN
                        -- Sprint 2 — Daily Operations extended fields
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'BranchId') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "BranchId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'VisitId') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "VisitId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'Shade') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "Shade" character varying(100) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RestorationType') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "RestorationType" character varying(200) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'DeliveredDate') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "DeliveredDate" date NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'CancellationReason') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "CancellationReason" character varying(500) NULL;
                        END IF;
                        -- Lab Sprint 2 — Lab entity reference
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'LabId') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "LabId" uuid NULL;
                        END IF;
                        -- Lab Sprint 3 — Professional order fields
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'TotalCost') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "TotalCost" numeric NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'InvoiceLineItemId') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "InvoiceLineItemId" uuid NULL;
                        END IF;
                        -- Lab Sprint 4 — Remake/Return fields
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RemakeReason') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "RemakeReason" character varying(500) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'ReturnReason') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "ReturnReason" character varying(500) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RemakeCost') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "RemakeCost" numeric NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'IsFreeRemake') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "IsFreeRemake" boolean NOT NULL DEFAULT false;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'OriginalOrderId') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "OriginalOrderId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RemakeCount') THEN
                            ALTER TABLE "LabOrders" ADD COLUMN "RemakeCount" integer NOT NULL DEFAULT 0;
                        END IF;
                        -- Indexes for FK lookups
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'LabOrders' AND indexname = 'IX_LabOrders_BranchId') THEN
                            CREATE INDEX "IX_LabOrders_BranchId" ON "LabOrders" ("BranchId");
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'LabOrders' AND indexname = 'IX_LabOrders_VisitId') THEN
                            CREATE INDEX "IX_LabOrders_VisitId" ON "LabOrders" ("VisitId");
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'LabOrders' AND indexname = 'IX_LabOrders_LabId') THEN
                            CREATE INDEX "IX_LabOrders_LabId" ON "LabOrders" ("LabId");
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'LabOrders' AND indexname = 'IX_LabOrders_OriginalOrderId') THEN
                            CREATE INDEX "IX_LabOrders_OriginalOrderId" ON "LabOrders" ("OriginalOrderId");
                        END IF;
                    END IF;
                END $$;
            """);

            labLogger.LogInformation("HOTFIX: LabOrders table schema ensured — BranchId, VisitId, LabId, TotalCost, remake columns verified (idempotent)");
        }
        catch (Exception ex)
        {
            var labLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            labLogger2.LogError(ex, "HOTFIX: Failed to ensure LabOrders schema. PatientJourney.GetToday may return 500!");
        }
    }

    /// <summary>
    /// Invoices.TaxAmount nullable hotfix — the EF model declares TaxAmount as decimal?
    /// but the database column was created NOT NULL, causing 23502 on insert when TaxAmount
    /// is not explicitly set. This hotfix alters the column to allow NULL.
    /// Fixes: "null value in column \"TaxAmount\" of relation \"Invoices\" violates not-null constraint"
    /// </summary>
    private static async Task EnsureInvoicesNullableTaxAmountAsync(WebApplication app)
    {
        try
        {
            using var invScope = app.Services.CreateScope();
            var invDb     = invScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invLogger = invScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await invDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Invoices' AND column_name = 'TaxAmount' AND is_nullable = 'NO'
                    ) THEN
                        ALTER TABLE "Invoices" ALTER COLUMN "TaxAmount" DROP NOT NULL;
                    END IF;
                END $$;
            """);

            invLogger.LogInformation("HOTFIX: Invoices.TaxAmount column altered to nullable (idempotent)");
        }
        catch (Exception ex)
        {
            var invLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            invLogger2.LogError(ex, "HOTFIX: Failed to alter Invoices.TaxAmount to nullable. Draft invoice creation may fail with 500!");
        }
    }

    /// <summary>
    /// Lab tables creation hotfix — creates Labs, LabWorkTypes, LabOrderItems, LabWorkPrices,
    /// LabOrderStatusHistories, LabOrderAttachments, and LabPayables tables if they don't exist.
    /// Also adds missing columns to existing tables. Fixes 500 errors on /api/labs, /api/lab-work-types,
    /// and /api/lab-orders (full list with Lab include).
    /// </summary>
    private static async Task EnsureLabTablesSchemaAsync(WebApplication app)
    {
        try
        {
            using var ltScope = app.Services.CreateScope();
            var ltDb     = ltScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ltLogger = ltScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await ltDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- ── Labs table ────────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Labs') THEN
                        CREATE TABLE "Labs" (
                            "Id"            uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "Name"          character varying(200)    NOT NULL,
                            "Phone"         character varying(30)     NULL,
                            "WhatsApp"      character varying(30)     NULL,
                            "Address"       character varying(500)    NULL,
                            "ContactPerson" character varying(200)    NULL,
                            "Email"         character varying(200)    NULL,
                            "Notes"         text                      NULL,
                            "BranchId"      uuid                      NULL,
                            "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"      boolean                   NOT NULL DEFAULT true,
                            "DeletedAt"     timestamp with time zone  NULL,
                            "DeletedBy"     uuid                      NULL,
                            CONSTRAINT "PK_Labs" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_Labs_Name" ON "Labs" ("Name");
                        CREATE INDEX "IX_Labs_BranchId" ON "Labs" ("BranchId");
                    END IF;
                    -- Add missing columns to Labs if table exists but columns missing
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Labs') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Labs' AND column_name = 'BranchId') THEN
                            ALTER TABLE "Labs" ADD COLUMN "BranchId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Labs' AND column_name = 'Notes') THEN
                            ALTER TABLE "Labs" ADD COLUMN "Notes" text NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Labs' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "Labs" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Labs' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "Labs" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END IF;
                    -- FK: Labs → Branches
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Labs_Branches_BranchId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Branches') THEN
                            ALTER TABLE "Labs" ADD CONSTRAINT "FK_Labs_Branches_BranchId"
                                FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE SET NULL;
                        END IF;
                    END IF;

                    -- ── LabWorkTypes table ────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabWorkTypes') THEN
                        CREATE TABLE "LabWorkTypes" (
                            "Id"        uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "Name"      character varying(100)    NOT NULL,
                            "NameAr"    character varying(100)    NULL,
                            "Category"  character varying(50)     NULL,
                            "SortOrder" integer                   NOT NULL DEFAULT 0,
                            "CreatedAt" timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt" timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"  boolean                   NOT NULL DEFAULT true,
                            "DeletedAt" timestamp with time zone  NULL,
                            "DeletedBy" uuid                      NULL,
                            CONSTRAINT "PK_LabWorkTypes" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_LabWorkTypes_Name" ON "LabWorkTypes" ("Name");
                        CREATE INDEX "IX_LabWorkTypes_SortOrder" ON "LabWorkTypes" ("SortOrder");
                    END IF;
                    -- Add missing columns to LabWorkTypes
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabWorkTypes') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabWorkTypes' AND column_name = 'NameAr') THEN
                            ALTER TABLE "LabWorkTypes" ADD COLUMN "NameAr" character varying(100) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabWorkTypes' AND column_name = 'Category') THEN
                            ALTER TABLE "LabWorkTypes" ADD COLUMN "Category" character varying(50) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabWorkTypes' AND column_name = 'SortOrder') THEN
                            ALTER TABLE "LabWorkTypes" ADD COLUMN "SortOrder" integer NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabWorkTypes' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "LabWorkTypes" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabWorkTypes' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "LabWorkTypes" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END IF;

                    -- ── LabOrderItems table ───────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabOrderItems') THEN
                        CREATE TABLE "LabOrderItems" (
                            "Id"              uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "LabOrderId"      uuid                      NOT NULL,
                            "WorkTypeId"      uuid                      NOT NULL,
                            "ToothNumber"     character varying(50)     NULL,
                            "Arch"            character varying(10)     NULL,
                            "Shade"           character varying(50)     NULL,
                            "RestorationType" character varying(100)    NULL,
                            "UnitsCount"      integer                   NOT NULL DEFAULT 1,
                            "UnitPrice"       numeric                   NULL,
                            "TotalPrice"      numeric                   NULL,
                            "Instructions"    character varying(2000)   NULL,
                            "SortOrder"       integer                   NOT NULL DEFAULT 0,
                            "CreatedAt"       timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"       timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"        boolean                   NOT NULL DEFAULT true,
                            "DeletedAt"       timestamp with time zone  NULL,
                            "DeletedBy"       uuid                      NULL,
                            CONSTRAINT "PK_LabOrderItems" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_LabOrderItems_LabOrderId" ON "LabOrderItems" ("LabOrderId");
                        CREATE INDEX "IX_LabOrderItems_WorkTypeId" ON "LabOrderItems" ("WorkTypeId");
                    END IF;
                    -- Fix column type mismatches if table already exists
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabOrderItems') THEN
                        -- ToothNumber: widen from varchar(20) to varchar(50) to match EF config
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrderItems' AND column_name = 'ToothNumber' AND character_maximum_length < 50) THEN
                            ALTER TABLE "LabOrderItems" ALTER COLUMN "ToothNumber" TYPE character varying(50);
                        END IF;
                        -- Arch: narrow from varchar(20) to varchar(10) to match EF config
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrderItems' AND column_name = 'Arch' AND character_maximum_length > 10) THEN
                            ALTER TABLE "LabOrderItems" ALTER COLUMN "Arch" TYPE character varying(10);
                        END IF;
                        -- Instructions: change from text to varchar(2000) to match EF config
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrderItems' AND column_name = 'Instructions' AND data_type = 'text') THEN
                            ALTER TABLE "LabOrderItems" ALTER COLUMN "Instructions" TYPE character varying(2000);
                        END IF;
                    END IF;
                    -- FK: LabOrderItems → LabOrders
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderItems_LabOrders_LabOrderId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
                            ALTER TABLE "LabOrderItems" ADD CONSTRAINT "FK_LabOrderItems_LabOrders_LabOrderId"
                                FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;
                    -- FK: LabOrderItems → LabWorkTypes
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderItems_LabWorkTypes_WorkTypeId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabWorkTypes') THEN
                            ALTER TABLE "LabOrderItems" ADD CONSTRAINT "FK_LabOrderItems_LabWorkTypes_WorkTypeId"
                                FOREIGN KEY ("WorkTypeId") REFERENCES "LabWorkTypes"("Id") ON DELETE RESTRICT;
                        END IF;
                    END IF;

                    -- ── LabWorkPrices table ───────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabWorkPrices') THEN
                        CREATE TABLE "LabWorkPrices" (
                            "Id"                 uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "LabId"              uuid                      NOT NULL,
                            "WorkTypeId"         uuid                      NOT NULL,
                            "UnitPrice"          numeric                   NOT NULL,
                            "UrgentSurcharge"    numeric                   NULL,
                            "UrgentSurchargeType" character varying(20)    NULL,
                            "EstimatedDays"      integer                   NULL,
                            "Notes"              text                      NULL,
                            "CreatedAt"          timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"          timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"           boolean                   NOT NULL DEFAULT true,
                            "DeletedAt"          timestamp with time zone  NULL,
                            "DeletedBy"          uuid                      NULL,
                            CONSTRAINT "PK_LabWorkPrices" PRIMARY KEY ("Id")
                        );
                        CREATE UNIQUE INDEX "IX_LabWorkPrices_LabId_WorkTypeId" ON "LabWorkPrices" ("LabId", "WorkTypeId");
                        CREATE INDEX "IX_LabWorkPrices_LabId" ON "LabWorkPrices" ("LabId");
                        CREATE INDEX "IX_LabWorkPrices_WorkTypeId" ON "LabWorkPrices" ("WorkTypeId");
                    END IF;
                    -- FK: LabWorkPrices → Labs
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabWorkPrices_Labs_LabId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Labs') THEN
                            ALTER TABLE "LabWorkPrices" ADD CONSTRAINT "FK_LabWorkPrices_Labs_LabId"
                                FOREIGN KEY ("LabId") REFERENCES "Labs"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;
                    -- FK: LabWorkPrices → LabWorkTypes
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabWorkPrices_LabWorkTypes_WorkTypeId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabWorkTypes') THEN
                            ALTER TABLE "LabWorkPrices" ADD CONSTRAINT "FK_LabWorkPrices_LabWorkTypes_WorkTypeId"
                                FOREIGN KEY ("WorkTypeId") REFERENCES "LabWorkTypes"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;

                    -- ── LabOrderStatusHistories table ────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabOrderStatusHistories') THEN
                        CREATE TABLE "LabOrderStatusHistories" (
                            "Id"              uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "LabOrderId"      uuid                      NOT NULL,
                            "FromStatus"      character varying(50)     NOT NULL,
                            "ToStatus"        character varying(50)     NOT NULL,
                            "ChangedByUserId" uuid                      NULL,
                            "Reason"          text                      NULL,
                            "CreatedAt"       timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"       timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"        boolean                   NOT NULL DEFAULT true,
                            "DeletedAt"       timestamp with time zone  NULL,
                            "DeletedBy"       uuid                      NULL,
                            CONSTRAINT "PK_LabOrderStatusHistories" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_LabOrderStatusHistories_LabOrderId" ON "LabOrderStatusHistories" ("LabOrderId");
                    END IF;
                    -- FK: LabOrderStatusHistories → LabOrders
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderStatusHistories_LabOrders_LabOrderId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
                            ALTER TABLE "LabOrderStatusHistories" ADD CONSTRAINT "FK_LabOrderStatusHistories_LabOrders_LabOrderId"
                                FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;

                    -- ── LabOrderAttachments table ────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabOrderAttachments') THEN
                        CREATE TABLE "LabOrderAttachments" (
                            "Id"              uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "LabOrderId"      uuid                      NOT NULL,
                            "LabOrderItemId"  uuid                      NULL,
                            "FileName"        character varying(255)    NOT NULL,
                            "ContentType"     character varying(100)    NOT NULL,
                            "FileSize"        bigint                    NOT NULL DEFAULT 0,
                            "Category"        character varying(50)     NOT NULL DEFAULT 'photo',
                            "StoragePath"     character varying(1000)   NOT NULL,
                            "UploadedBy"      uuid                      NULL,
                            "CreatedAt"       timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"       timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"        boolean                   NOT NULL DEFAULT true,
                            "DeletedAt"       timestamp with time zone  NULL,
                            "DeletedBy"       uuid                      NULL,
                            CONSTRAINT "PK_LabOrderAttachments" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_LabOrderAttachments_LabOrderId" ON "LabOrderAttachments" ("LabOrderId");
                    END IF;
                    -- FK: LabOrderAttachments → LabOrders
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderAttachments_LabOrders_LabOrderId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
                            ALTER TABLE "LabOrderAttachments" ADD CONSTRAINT "FK_LabOrderAttachments_LabOrders_LabOrderId"
                                FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;

                    -- ── LabPayables table ────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabPayables') THEN
                        CREATE TABLE "LabPayables" (
                            "Id"         uuid                      NOT NULL DEFAULT gen_random_uuid(),
                            "LabOrderId" uuid                      NOT NULL,
                            "LabId"      uuid                      NOT NULL,
                            "Amount"     numeric                   NOT NULL,
                            "PaidAmount" numeric                   NOT NULL DEFAULT 0,
                            "DueDate"    timestamp with time zone  NULL,
                            "Status"     character varying(20)     NOT NULL DEFAULT 'pending',
                            "Notes"      text                      NULL,
                            "CreatedAt"  timestamp with time zone  NOT NULL DEFAULT now(),
                            "UpdatedAt"  timestamp with time zone  NOT NULL DEFAULT now(),
                            "IsActive"   boolean                   NOT NULL DEFAULT true,
                            "DeletedAt"  timestamp with time zone  NULL,
                            "DeletedBy"  uuid                      NULL,
                            CONSTRAINT "PK_LabPayables" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_LabPayables_LabOrderId" ON "LabPayables" ("LabOrderId");
                        CREATE INDEX "IX_LabPayables_LabId" ON "LabPayables" ("LabId");
                    END IF;
                    -- FK: LabPayables → LabOrders
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabPayables_LabOrders_LabOrderId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
                            ALTER TABLE "LabPayables" ADD CONSTRAINT "FK_LabPayables_LabOrders_LabOrderId"
                                FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;
                    -- FK: LabPayables → Labs
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabPayables_Labs_LabId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Labs') THEN
                            ALTER TABLE "LabPayables" ADD CONSTRAINT "FK_LabPayables_Labs_LabId"
                                FOREIGN KEY ("LabId") REFERENCES "Labs"("Id") ON DELETE CASCADE;
                        END IF;
                    END IF;

                    -- ── FK: LabOrders → Labs (if Labs just created) ──────────
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrders_Labs_LabId') THEN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'LabId') THEN
                            ALTER TABLE "LabOrders" ADD CONSTRAINT "FK_LabOrders_Labs_LabId"
                                FOREIGN KEY ("LabId") REFERENCES "Labs"("Id") ON DELETE SET NULL;
                        END IF;
                    END IF;
                END $$;
            """);

            ltLogger.LogInformation("HOTFIX: Lab tables (Labs, LabWorkTypes, LabOrderItems, LabWorkPrices, LabOrderStatusHistories, LabOrderAttachments, LabPayables) schema ensured (idempotent)");

            // Diagnostic: check if LabOrderItems table was actually created/found
            try
            {
                using var diagConn = ltDb.Database.GetDbConnection();
                await diagConn.OpenAsync();
                using var diagCmd = diagConn.CreateCommand();
                diagCmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LabOrderItems'";
                var itemsCount = Convert.ToInt32(await diagCmd.ExecuteScalarAsync());
                if (itemsCount == 0)
                {
                    ltLogger.LogWarning("HOTFIX: LabOrderItems table was NOT found after schema maintenance. Lab order PDF endpoints will work without Items (fallback to 'غير محدد'). Controller IsMissingTableOrColumnError will handle gracefully.");
                }
                else
                {
                    ltLogger.LogInformation("HOTFIX: LabOrderItems table verified present after schema maintenance");
                }
            }
            catch (Exception diagEx)
            {
                ltLogger.LogWarning(diagEx, "HOTFIX: Could not verify LabOrderItems table existence (non-fatal diagnostic)");
            }
        }
        catch (Exception ex)
        {
            var ltLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            ltLogger2.LogError(ex, "HOTFIX: Failed to ensure Lab tables schema. /api/labs, /api/lab-work-types, /api/lab-orders may return 500!");
        }
    }

    /// <summary>
    /// Gated DB maintenance: advisory lock, pre-migration checks, table creation, MigrateAsync, DbSeeder, PatientAccounts seed.
    /// </summary>
    private static async Task RunGatedDbMaintenanceAsync(WebApplication app, IConfiguration configuration)
    {
        // ── DB Maintenance (gated by ENABLE_STARTUP_DB_MAINTENANCE + advisory lock) ────
        // TD-020: remaining raw SQL blocks — see docs/technical-debt/TD-020-raw-sql-inventory.md
        var enableStartupDbMaintenance =
            configuration.GetValue<bool>("ENABLE_STARTUP_DB_MAINTENANCE");

        if (enableStartupDbMaintenance)
        {
            using (var scope = app.Services.CreateScope())
            {
                var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            // ── Acquire advisory lock ───────────────────────────────────────────────
            var lockKey = configuration.GetValue<int>("DB_MAINTENANCE_LOCK_KEY", 918273645);
            var acquiredLock = false;
            try
            {
                await db.Database.OpenConnectionAsync();
                using (var lockCmd = db.Database.GetDbConnection().CreateCommand())
                {
                    lockCmd.CommandText = $"SELECT pg_try_advisory_lock({lockKey})";
                    var lockResult = await lockCmd.ExecuteScalarAsync();
                    acquiredLock = lockResult is bool b && b;
                }
            }
            catch (Exception lockEx)
            {
                logger.LogWarning(lockEx, "Failed to acquire advisory lock for DB maintenance, proceeding without lock");
                acquiredLock = true; // Proceed without lock if advisory locks aren't supported
            }

            if (!acquiredLock)
            {
                logger.LogInformation("DB maintenance advisory lock not acquired — another instance is running maintenance. Skipping.");
            }
            else
            {
                logger.LogInformation("DB maintenance advisory lock acquired — proceeding with schema maintenance");

            // Pre-migration: Add new columns that EF Core expects but may not exist yet
            try
            {
                // B2 (soft-delete columns) removed in TD-020 Phase C1-a --
                // now handled by EF migration 20260522000000_AddSoftDeleteColumnsToLegacyTables

                // B3/B8/B9 normalized phone schema removed in TD-020 Phase C1-b;
                // now handled by EF migration 20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes.

                // NormalizedPhone/NormalizedWhatsApp backfill + dedup removed in TD-020 Phase C1-e;
                // now handled by EF migrations 20260501000000 and 20260523000000.

                // B10-B13 conversation schema removed in TD-020 Phase C1-d;
                // now handled by EF migration 20260524000000_AddConversationPatientBranchFieldsAndIndexes.

                logger.LogInformation("Pre-migration schema updates applied successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply pre-migration schema updates");
            }

            // PatientAccounts table creation — now handled by EF migration 20260430120000_AddPatientPortal
            // (removed in TD-020 Phase C1-e)

            // Add Username/PasswordHash/PasswordSalt columns to PatientAccounts
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'Username') THEN
                            ALTER TABLE "PatientAccounts" ADD COLUMN "Username" character varying(50) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash') THEN
                            ALTER TABLE "PatientAccounts" ADD COLUMN "PasswordHash" character varying(256) NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordSalt') THEN
                            ALTER TABLE "PatientAccounts" ADD COLUMN "PasswordSalt" character varying(128) NULL;
                        END IF;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PatientAccounts_Username" ON "PatientAccounts" ("Username") WHERE "Username" IS NOT NULL;
                """);
                logger.LogInformation("PatientAccounts username/password columns ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add username/password columns to PatientAccounts");
            }

            // Visits/Documents columns — now handled by EF migration 20260502000000_AddVisitsDocumentsFields
            // (removed in TD-020 Phase C1-e)

            // Ensure Sprint 4.5 queue columns exist on Appointments table
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Appointments') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'RoomName') THEN
                                ALTER TABLE "Appointments" ADD COLUMN "RoomName" character varying(50) NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ArrivedAt') THEN
                                ALTER TABLE "Appointments" ADD COLUMN "ArrivedAt" timestamp with time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'CalledAt') THEN
                                ALTER TABLE "Appointments" ADD COLUMN "CalledAt" timestamp with time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'InRoomAt') THEN
                                ALTER TABLE "Appointments" ADD COLUMN "InRoomAt" timestamp with time zone NULL;
                            END IF;
                            CREATE INDEX IF NOT EXISTS "IX_Appointments_AppointmentDate" ON "Appointments" ("AppointmentDate");
                        END IF;
                    END $$;
                """);

                logger.LogInformation("Sprint 4.5 queue columns ensured on Appointments table");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Sprint 4.5 queue columns");
            }

            // Ensure Sprint 8 queue/appointment integration columns exist
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ServiceId') THEN
                                ALTER TABLE "ClinicQueueItems" ADD COLUMN "ServiceId" uuid NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ClinicRoomId') THEN
                                ALTER TABLE "ClinicQueueItems" ADD COLUMN "ClinicRoomId" uuid NULL;
                            END IF;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Appointments') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ServiceId') THEN
                                ALTER TABLE "Appointments" ADD COLUMN "ServiceId" uuid NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ClinicRoomId') THEN
                                ALTER TABLE "Appointments" ADD COLUMN "ClinicRoomId" uuid NULL;
                            END IF;
                        END IF;
                    END $$;
                """);

                logger.LogInformation("Sprint 8 queue/appointment integration columns ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Sprint 8 integration columns");
            }

            // Ensure Sprint 5 DoctorSchedules table exists
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "DoctorSchedules" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "DoctorId" uuid NOT NULL,
                        "DayOfWeek" integer NOT NULL,
                        "StartTime" time without time zone NOT NULL,
                        "EndTime" time without time zone NOT NULL,
                        "IsWorking" boolean NOT NULL DEFAULT TRUE,
                        "BreakStart" time without time zone NULL,
                        "BreakEnd" time without time zone NULL,
                        "SlotDurationMinutes" integer NOT NULL DEFAULT 30,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);

                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_DoctorSchedules_Doctors_DoctorId') THEN
                            ALTER TABLE "DoctorSchedules" ADD CONSTRAINT "FK_DoctorSchedules_Doctors_DoctorId"
                                FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id") ON DELETE CASCADE;
                        END IF;
                    END $$;
                """);

                await db.Database.ExecuteSqlRawAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_DoctorSchedules_DoctorId_DayOfWeek"
                        ON "DoctorSchedules" ("DoctorId", "DayOfWeek")
                        WHERE "IsActive" = TRUE;
                """);

                logger.LogInformation("Sprint 5 DoctorSchedules table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Sprint 5 DoctorSchedules table");
            }

            // ── Finance V2/V3: Ensure ALL missing finance tables exist ──────────────
            // Production database on Railway is missing core finance tables because
            // ENABLE_STARTUP_DB_MAINTENANCE was not set and EF migrations never ran.
            // These must run BEFORE the JournalEntries block because JournalEntries
            // has FK references to CashierSessions and Treasuries.

            // ── Treasuries ─────────────────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "Treasuries" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "Name" text NOT NULL,
                        "Type" integer NOT NULL,
                        "Balance" numeric(12,2) NOT NULL DEFAULT 0,
                        "BranchId" uuid NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId" ON "Treasuries" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type" ON "Treasuries" ("BranchId", "Type")""");
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type_Name_Unique"
                        ON "Treasuries" ("BranchId", "Type", "Name")
                        WHERE "IsActive" = true
                """);
                logger.LogInformation("Treasuries table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Treasuries table");
            }

            // DB-02 NOTE: CashierSessions, Invoices, and Payments now use PostgreSQL's xmin
            // system column as an EF Core optimistic concurrency token (mirrors the FIN-06 fix
            // applied to Treasuries). xmin is a PostgreSQL system column that exists on every
            // table automatically — there is no DDL to run here. EF Core's runtime model
            // (configured in CashierSessionConfiguration / InvoiceConfiguration /
            // PaymentConfiguration via UseXminAsConcurrencyToken) includes the xmin value in
            // UPDATE WHERE clauses, so concurrent edits throw DbUpdateConcurrencyException,
            // which the global ErrorHandlingMiddleware converts to HTTP 409 Conflict with an
            // Arabic message. The empty migration 20260706000000 advances the ModelSnapshot
            // only. No startup maintenance DDL is required for these three tables.

            // ── OperationalExpenses ────────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "OperationalExpenses" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "ExpenseNumber" character varying(50) NOT NULL,
                        "Title" character varying(300) NOT NULL,
                        "Category" integer NOT NULL,
                        "Amount" numeric(12,2) NOT NULL,
                        "ExpenseDate" date NOT NULL,
                        "PaymentMethod" character varying(50) NOT NULL DEFAULT 'cash',
                        "SupplierId" uuid NULL,
                        "LabOrderId" uuid NULL,
                        "Notes" text NULL,
                        "ReceiptAttachmentUrl" text NULL,
                        "PaidBy" uuid NOT NULL,
                        "BranchId" uuid NOT NULL,
                        "ApprovalStatus" integer NOT NULL DEFAULT 0,
                        "ApprovedById" uuid NULL,
                        "ApprovedAt" timestamp with time zone NULL,
                        "ApprovalNotes" text NULL,
                        "IsPostedToLedger" boolean NOT NULL DEFAULT FALSE,
                        "CashFlowTransactionId" uuid NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_OperationalExpenses_BranchId" ON "OperationalExpenses" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_OperationalExpenses_ExpenseDate" ON "OperationalExpenses" ("ExpenseDate")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_OperationalExpenses_ApprovalStatus" ON "OperationalExpenses" ("ApprovalStatus")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_OperationalExpenses_SupplierId" ON "OperationalExpenses" ("SupplierId")""");
                logger.LogInformation("OperationalExpenses table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure OperationalExpenses table");
            }

            // ── CashierSessions ────────────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "CashierSessions" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "SessionNumber" character varying(100) NOT NULL,
                        "CashierId" uuid NOT NULL,
                        "BranchId" uuid NOT NULL,
                        "OpeningTime" timestamp with time zone NOT NULL,
                        "ClosingTime" timestamp with time zone NULL,
                        "OpeningBalance" numeric(12,2) NOT NULL DEFAULT 0,
                        "ExpectedClosingCash" numeric(12,2) NOT NULL DEFAULT 0,
                        "ActualClosingCash" numeric(12,2) NULL,
                        "ExpectedClosingCard" numeric(12,2) NOT NULL DEFAULT 0,
                        "ActualClosingCard" numeric(12,2) NULL,
                        "ExpectedClosingBank" numeric(12,2) NOT NULL DEFAULT 0,
                        "ActualClosingBank" numeric(12,2) NULL,
                        "ShortageOrSurplus" numeric(12,2) NULL,
                        "Status" integer NOT NULL DEFAULT 0,
                        "Notes" text NULL,
                        "TreasuryId" uuid NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashierSessions_CashierId_Status" ON "CashierSessions" ("CashierId", "Status")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashierSessions_BranchId" ON "CashierSessions" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashierSessions_TreasuryId" ON "CashierSessions" ("TreasuryId")""");
                logger.LogInformation("CashierSessions table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure CashierSessions table");
            }

            // ── Suppliers ──────────────────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "Suppliers" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "Name" character varying(200) NOT NULL,
                        "ContactPerson" character varying(100) NULL,
                        "Phone" character varying(30) NULL,
                        "Email" character varying(200) NULL,
                        "Address" character varying(500) NULL,
                        "Notes" text NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Suppliers_Name" ON "Suppliers" ("Name")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Suppliers_Phone" ON "Suppliers" ("Phone")""");
                logger.LogInformation("Suppliers table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Suppliers table");
            }

            // ── SupplierBills ──────────────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "SupplierBills" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "BillNumber" character varying(50) NOT NULL,
                        "SupplierId" uuid NOT NULL,
                        "Description" character varying(500) NOT NULL,
                        "TotalAmount" numeric(12,2) NOT NULL,
                        "PaidAmount" numeric(12,2) NOT NULL DEFAULT 0,
                        "Status" integer NOT NULL DEFAULT 0,
                        "BillDate" date NOT NULL,
                        "DueDate" date NULL,
                        "PurchaseOrderId" uuid NULL,
                        "LabOrderId" uuid NULL,
                        "AttachmentUrl" text NULL,
                        "Notes" text NULL,
                        "BranchId" uuid NOT NULL,
                        "CreatedBy" uuid NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_SupplierBills_BranchId" ON "SupplierBills" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_SupplierBills_SupplierId" ON "SupplierBills" ("SupplierId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_SupplierBills_Status" ON "SupplierBills" ("Status")""");
                logger.LogInformation("SupplierBills table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure SupplierBills table");
            }

            // ── SupplierBillPayments ───────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "SupplierBillPayments" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "SupplierBillId" uuid NOT NULL,
                        "Amount" numeric(12,2) NOT NULL,
                        "PaymentMethod" character varying(50) NOT NULL DEFAULT 'cash',
                        "PaymentDate" date NOT NULL,
                        "ReferenceNumber" character varying(100) NULL,
                        "Notes" text NULL,
                        "PaidBy" uuid NOT NULL,
                        "CashFlowTransactionId" uuid NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_SupplierBillPayments_SupplierBillId" ON "SupplierBillPayments" ("SupplierBillId")""");
                logger.LogInformation("SupplierBillPayments table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure SupplierBillPayments table");
            }

            // ── Finance Phase 1: CreditNotes + Supplier.Type/Balance ─────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "CreditNotes" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "InvoiceId" uuid NOT NULL,
                        "PatientId" uuid NOT NULL,
                        "Amount" numeric(12,2) NOT NULL,
                        "Reason" character varying(500) NOT NULL DEFAULT '',
                        "Status" integer NOT NULL DEFAULT 0,
                        "RefundPaymentId" uuid NULL,
                        "BranchId" uuid NOT NULL,
                        "CreatedBy" uuid NOT NULL,
                        "Notes" text NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CreditNotes_InvoiceId" ON "CreditNotes" ("InvoiceId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CreditNotes_PatientId" ON "CreditNotes" ("PatientId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CreditNotes_BranchId" ON "CreditNotes" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CreditNotes_Status" ON "CreditNotes" ("Status")""");

                // Supplier.Type column (integer enum: 0=DentalLab, 1=MedicalVendor, 2=GeneralService)
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Suppliers' AND column_name = 'Type') THEN
                            ALTER TABLE "Suppliers" ADD COLUMN "Type" integer NOT NULL DEFAULT 1;
                        END IF;
                    END $$;
                """);

                // Supplier.Balance column (default 0)
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Suppliers' AND column_name = 'Balance') THEN
                            ALTER TABLE "Suppliers" ADD COLUMN "Balance" numeric(12,2) NOT NULL DEFAULT 0;
                        END IF;
                    END $$;
                """);

                // FK: CreditNotes -> Invoices (Restrict)
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CreditNotes_Invoices_InvoiceId') THEN
                            ALTER TABLE "CreditNotes" ADD CONSTRAINT "FK_CreditNotes_Invoices_InvoiceId"
                                FOREIGN KEY ("InvoiceId") REFERENCES "Invoices"("Id") ON DELETE RESTRICT;
                        END IF;
                    END $$;
                """);

                // FK: CreditNotes -> Patients
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CreditNotes_Patients_PatientId') THEN
                            ALTER TABLE "CreditNotes" ADD CONSTRAINT "FK_CreditNotes_Patients_PatientId"
                                FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE CASCADE;
                        END IF;
                    END $$;
                """);

                // FK: CreditNotes -> Payments (RefundPaymentId)
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CreditNotes_Payments_RefundPaymentId') THEN
                            ALTER TABLE "CreditNotes" ADD CONSTRAINT "FK_CreditNotes_Payments_RefundPaymentId"
                                FOREIGN KEY ("RefundPaymentId") REFERENCES "Payments"("Id") ON DELETE SET NULL;
                        END IF;
                    END $$;
                """);

                // FK: CreditNotes -> Branches
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CreditNotes_Branches_BranchId') THEN
                            ALTER TABLE "CreditNotes" ADD CONSTRAINT "FK_CreditNotes_Branches_BranchId"
                                FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE CASCADE;
                        END IF;
                    END $$;
                """);

                logger.LogInformation("Finance Phase 1: CreditNotes table + Supplier.Type/Balance columns ensured (idempotent)");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Finance Phase 1 CreditNotes/Supplier schema. Credit note and refund endpoints may return 500!");
            }

            // ── VaultTransfers ─────────────────────────────────────────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "VaultTransfers" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "TransferNumber" character varying(100) NOT NULL,
                        "SourceTreasuryId" uuid NULL,
                        "DestinationTreasuryId" uuid NOT NULL,
                        "CashierSessionId" uuid NULL,
                        "Amount" numeric(12,2) NOT NULL,
                        "TransferDate" timestamp with time zone NOT NULL,
                        "PerformedBy" uuid NOT NULL,
                        "ApprovedBy" uuid NULL,
                        "ApprovalDate" timestamp with time zone NULL,
                        "Status" integer NOT NULL DEFAULT 0,
                        "Notes" text NULL,
                        "DepositSource" text NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_VaultTransfers_SourceTreasuryId" ON "VaultTransfers" ("SourceTreasuryId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_VaultTransfers_DestinationTreasuryId" ON "VaultTransfers" ("DestinationTreasuryId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_VaultTransfers_CashierSessionId" ON "VaultTransfers" ("CashierSessionId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_VaultTransfers_Status" ON "VaultTransfers" ("Status")""");
                logger.LogInformation("VaultTransfers table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure VaultTransfers table");
            }

            // ── Contracts (ensure exists — used by finance module) ─────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "Contracts" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "PatientId" uuid NOT NULL,
                        "Specialty" character varying(100) NULL,
                        "RelatedCaseId" uuid NULL,
                        "TotalAmount" numeric(12,2) NOT NULL,
                        "DownPayment" numeric(12,2) NOT NULL DEFAULT 0,
                        "InstallmentsCount" integer NOT NULL DEFAULT 1,
                        "InstallmentAmount" numeric(12,2) NULL,
                        "StartDate" date NULL,
                        "DiscountAmount" numeric(12,2) NOT NULL DEFAULT 0,
                        "DiscountReason" character varying(300) NULL,
                        "Status" character varying(20) NOT NULL DEFAULT 'Active',
                        "Notes" character varying(1000) NULL,
                        "CreatedBy" uuid NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Contracts_PatientId" ON "Contracts" ("PatientId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Contracts_Status" ON "Contracts" ("Status")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Contracts_CreatedBy" ON "Contracts" ("CreatedBy")""");
                logger.LogInformation("Contracts table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Contracts table");
            }

            // ── Payments (ensure exists — used by finance module) ──────────────────
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "Payments" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "ContractId" uuid NULL,
                        "InvoiceId" uuid NULL,
                        "PatientId" uuid NOT NULL,
                        "Amount" numeric(12,2) NOT NULL,
                        "PaymentDate" date NOT NULL,
                        "PaymentMethod" character varying(30) NULL,
                        "Specialty" character varying(100) NULL,
                        "ServiceDescription" character varying(500) NULL,
                        "DoctorId" uuid NULL,
                        "BranchId" uuid NULL,
                        "ReceivedBy" uuid NULL,
                        "ReceiptNumber" character varying(50) NULL,
                        "Notes" character varying(1000) NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_ContractId" ON "Payments" ("ContractId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_InvoiceId" ON "Payments" ("InvoiceId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_PatientId" ON "Payments" ("PatientId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_DoctorId" ON "Payments" ("DoctorId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_BranchId" ON "Payments" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_ReceiptNumber" ON "Payments" ("ReceiptNumber") WHERE "ReceiptNumber" IS NOT NULL""");
                logger.LogInformation("Payments table ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Payments table");
            }

            // ── Finance V3: Ensure JournalEntries & JournalLines tables exist ────────
            // These tables are required by the double-entry ledger migration. Without them,
            // ALL FinanceV3 endpoints fail with PostgresException: relation does not exist.
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "JournalEntries" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "EntryNumber" character varying(100) NOT NULL,
                        "FinancialDocumentId" uuid NOT NULL,
                        "FinancialDocumentType" character varying(30) NOT NULL,
                        "Description" character varying(500) NOT NULL,
                        "EntryDate" date NOT NULL,
                        "BranchId" uuid NOT NULL,
                        "CashierSessionId" uuid NULL,
                        "TreasuryId" uuid NULL,
                        "PerformedBy" uuid NOT NULL,
                        "IsReversal" boolean NOT NULL DEFAULT FALSE,
                        "ReversalOfEntryId" uuid NULL,
                        "ReversedByEntryId" uuid NULL,
                        "IsPosted" boolean NOT NULL DEFAULT FALSE,
                        "PostedAt" timestamp with time zone NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);

                // FKs for JournalEntries (each wrapped in EXCEPTION handler to prevent
                // one failed FK from aborting the entire try block — root cause of JournalLines
                // not being created: FK to CashierSessions threw because table didn't exist)
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_Branches_BranchId') THEN
                            ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_Branches_BranchId"
                                FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE RESTRICT;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_CashierSessions_CashierSessionId') THEN
                            ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_CashierSessions_CashierSessionId"
                                FOREIGN KEY ("CashierSessionId") REFERENCES "CashierSessions"("Id") ON DELETE SET NULL;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_Treasuries_TreasuryId') THEN
                            ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_Treasuries_TreasuryId"
                                FOREIGN KEY ("TreasuryId") REFERENCES "Treasuries"("Id") ON DELETE SET NULL;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_Users_PerformedBy') THEN
                            ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_Users_PerformedBy"
                                FOREIGN KEY ("PerformedBy") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_JournalEntries_ReversalOfEntryId') THEN
                            ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_JournalEntries_ReversalOfEntryId"
                                FOREIGN KEY ("ReversalOfEntryId") REFERENCES "JournalEntries"("Id") ON DELETE RESTRICT;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_JournalEntries_ReversedByEntryId') THEN
                            ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_JournalEntries_ReversedByEntryId"
                                FOREIGN KEY ("ReversedByEntryId") REFERENCES "JournalEntries"("Id") ON DELETE RESTRICT;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);

                // Indexes for JournalEntries
                await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_JournalEntries_EntryNumber" ON "JournalEntries" ("EntryNumber")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_BranchId" ON "JournalEntries" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_EntryDate" ON "JournalEntries" ("EntryDate")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_FinancialDocumentType" ON "JournalEntries" ("FinancialDocumentType")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_IsPosted" ON "JournalEntries" ("IsPosted")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_IsReversal" ON "JournalEntries" ("IsReversal")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_CashierSessionId" ON "JournalEntries" ("CashierSessionId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_TreasuryId" ON "JournalEntries" ("TreasuryId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_PerformedBy" ON "JournalEntries" ("PerformedBy")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_BranchId_EntryDate" ON "JournalEntries" ("BranchId", "EntryDate")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_ReversalOfEntryId" ON "JournalEntries" ("ReversalOfEntryId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_ReversedByEntryId" ON "JournalEntries" ("ReversedByEntryId")""");

                // JournalLines table
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS "JournalLines" (
                        "Id" uuid NOT NULL PRIMARY KEY,
                        "JournalEntryId" uuid NOT NULL,
                        "AccountType" character varying(30) NOT NULL,
                        "AccountId" uuid NOT NULL,
                        "Debit" numeric(12,2) NOT NULL DEFAULT 0,
                        "Credit" numeric(12,2) NOT NULL DEFAULT 0,
                        "Description" character varying(500) NULL,
                        "BranchId" uuid NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "IsActive" boolean NOT NULL DEFAULT TRUE,
                        "DeletedAt" timestamp with time zone NULL,
                        "DeletedBy" uuid NULL
                    );
                """);

                // FKs for JournalLines (EXCEPTION handler prevents one failed FK from
                // aborting the rest of the try block)
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalLines_JournalEntries_JournalEntryId') THEN
                            ALTER TABLE "JournalLines" ADD CONSTRAINT "FK_JournalLines_JournalEntries_JournalEntryId"
                                FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries"("Id") ON DELETE CASCADE;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalLines_Branches_BranchId') THEN
                            ALTER TABLE "JournalLines" ADD CONSTRAINT "FK_JournalLines_Branches_BranchId"
                                FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE RESTRICT;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);

                // Check constraint for Debit/Credit mutual exclusivity
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_JournalLines_DebitCreditMutual') THEN
                            ALTER TABLE "JournalLines" ADD CONSTRAINT "CK_JournalLines_DebitCreditMutual"
                                CHECK ("Debit" >= 0 AND "Credit" >= 0 AND ("Debit" > 0 OR "Credit" > 0));
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);

                // Indexes for JournalLines
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_JournalEntryId" ON "JournalLines" ("JournalEntryId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_AccountType" ON "JournalLines" ("AccountType")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_AccountId" ON "JournalLines" ("AccountId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_BranchId" ON "JournalLines" ("BranchId")""");
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_AccountType_AccountId" ON "JournalLines" ("AccountType", "AccountId")""");

                // Ensure VaultTransfers.DepositSource column exists
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'DepositSource') THEN
                            ALTER TABLE "VaultTransfers" ADD COLUMN "DepositSource" text NULL;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);

                // Ensure CashierSessions.TreasuryId column exists
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'CashierSessions' AND column_name = 'TreasuryId') THEN
                            ALTER TABLE "CashierSessions" ADD COLUMN "TreasuryId" uuid NULL;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashierSessions_TreasuryId" ON "CashierSessions" ("TreasuryId")""");
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashierSessions_Treasuries_TreasuryId') THEN
                            ALTER TABLE "CashierSessions" ADD CONSTRAINT "FK_CashierSessions_Treasuries_TreasuryId"
                                FOREIGN KEY ("TreasuryId") REFERENCES "Treasuries"("Id") ON DELETE SET NULL;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);

                // Ensure CashFlowTransactions.TreasuryId column exists
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "TreasuryId" uuid NULL;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);
                await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_TreasuryId" ON "CashFlowTransactions" ("TreasuryId")""");
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_Treasuries_TreasuryId') THEN
                            ALTER TABLE "CashFlowTransactions" ADD CONSTRAINT "FK_CashFlowTransactions_Treasuries_TreasuryId"
                                FOREIGN KEY ("TreasuryId") REFERENCES "Treasuries"("Id") ON DELETE SET NULL;
                        END IF;
                    EXCEPTION WHEN OTHERS THEN NULL;
                    END $$;
                """);

                logger.LogInformation("Finance V3 JournalEntries/JournalLines tables and columns ensured");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ensure Finance V3 tables");
            }

            // NOTE: Conversation RecipientType/RecipientUserId columns, BookingRequests table,
            // Message IsEdited/EditedAt fields, BookingRequests DoctorId, and Sprint 6 Doctor
            // compensation columns were previously in unconditional hotfix blocks.
            // They have been consolidated into this gated maintenance block with advisory lock.
            // The remaining pre-migration blocks above already ensure all required columns.

            // ── Migration History Reconciliation ────────────────────────────────────
            // HOTFIX: Previous deployments used raw SQL blocks to create tables/columns
            // that are also defined in EF Core migrations. When MigrateAsync() runs, it
            // sees these migrations as "not applied" (missing from __EFMigrationsHistory)
            // but the schema already exists, causing "already exists" errors that block
            // ALL subsequent migrations (including Invoices, Commission, etc.).
            //
            // Additionally, a previous reconciliation attempt incorrectly inserted migration
            // records for ALL detected schema, even when the underlying tables/columns were
            // only partially present. This caused MigrateAsync() to report "No migrations applied"
            // while critical tables like Invoices and InvoiceLineItems were missing.
            //
            // This block:
            // 1. Removes migration records for tables/columns that DON'T actually exist
            // 2. Inserts migration records for tables/columns that DO exist but aren't recorded
            // This ensures MigrateAsync() only applies truly missing migrations.
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        -- Ensure __EFMigrationsHistory table exists
                        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                            "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                            "ProductVersion" character varying(32) NOT NULL
                        );

                        -- ═══ STEP 1: Remove migration records for non-existent schema ═══
                        -- These were incorrectly inserted by the previous reconciliation.
                        -- We remove them so MigrateAsync() can re-apply them properly.

                        -- 20260531000000 requires Invoices table
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260531000000_AddInvoicesAndInvoiceLineItems'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Invoices');

                        -- 20260601000000 requires InvoiceId on Payments
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddInvoicePaymentLink'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId');

                        -- 20260606000000 requires commission columns on InvoiceLineItems
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260606000000_AddDoctorCommissionSystem'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionPercentage');

                        -- 20260607000000 requires CommissionRecognitionMode on ClinicServices
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607000000_AddCommissionRecognitionMode'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode');

                        -- Also remove any migration record where the PRIMARY table doesn't exist
                        -- This catches cases where a HOTFIX created a partial schema
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'PatientId');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502010000_AddSecurePatientPortalPasswordAuth'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddConversationRecipientType'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'RecipientType');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260507000000_AddBookingRequests'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508052207_AddBookingRequest'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditFields'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511000000_AddDoctorIdToBookingRequest'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'BookingRequests' AND column_name = 'DoctorId');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_AddRadiographFileMetadata'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicalPhotos' AND column_name = 'FileType');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513000000_AddDoctorCompensationFields'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514000000_AddClinicQueueItem'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520000000_AddClinicQueueItemTrackingFields'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledAt');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520202816_SyncAuditPhase2Configurations'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AuditLogs');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddPasswordSaltAndPatientPhoneIndexes'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522000000_AddSoftDeleteColumnsToLegacyTables'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260524000000_AddConversationPatientBranchFieldsAndIndexes'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525000000_AddMissingFKIndexesAndUserMustChangePassword'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528000000_AddClinicServicesAndRooms'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260530000000_AddPatientTreatmentPlanSteps'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'TreatmentPlanSteps');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260602000000_AddMessageAttachments'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageAttachments');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603000000_AddOrthoDiagnosisRetentionPhotos'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OrthoDiagnoses' AND column_name = 'RetentionPhotoLeft');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260604000000_AddSuppliersAndPurchases'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605000000_AddClinicQueueItemServiceAndRoom'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ServiceId');

                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260608000000_AddRolePermissionUniqueIndex';
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260609000000_AddEmailLog';
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260610000000_AddSeparateReminderTrackingAndPatientEmail';

                        -- Financial Integrity Sprint: remove record if IsReversal column doesn't exist yet
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613000000_AddFinancialIntegrityAuditSprint'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'CashFlowTransactions' AND column_name = 'IsReversal');

                        -- Remove Treasury/VaultTransfer migration records if tables don't exist
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525115704_AddTreasuryVaultTransfers'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Treasuries');

                        -- Remove SupplierBills migration record if tables don't exist
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525123318_AddSupplierBillsAndApprovals'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SupplierBills');

                        -- Remove CashFlowTransactions migration records if table doesn't exist
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525092924_AddCentralFinanceV2Hub'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CashFlowTransactions');

                        -- Remove OperationalExpenses migration if table doesn't exist
                        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '2026052%'
                            AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OperationalExpenses');

                        -- ═══ STEP 2: Insert missing records for existing schema ═══
                        -- These were created by HOTFIX blocks but not recorded in __EFMigrationsHistory.

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260430221624_AddConversationPatientAndType', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260501000000_AddNormalizedPhoneFields', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'PatientId') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260501010000_AddPatientConversationSupport', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260501020000_AddSoftDeleteToMessagingTables', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260502000000_AddVisitsDocumentsFields', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502010000_AddSecurePatientPortalPasswordAuth')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260502010000_AddSecurePatientPortalPasswordAuth', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddConversationRecipientType')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'RecipientType') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260503000000_AddConversationRecipientType', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260507000000_AddBookingRequests')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260507000000_AddBookingRequests', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508052207_AddBookingRequest')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260508052207_AddBookingRequest', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditFields')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260510000000_AddMessageEditFields', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511000000_AddDoctorIdToBookingRequest')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'BookingRequests' AND column_name = 'DoctorId') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260511000000_AddDoctorIdToBookingRequest', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513000000_AddDoctorCompensationFields')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260513000000_AddDoctorCompensationFields', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514000000_AddClinicQueueItem')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260514000000_AddClinicQueueItem', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520000000_AddClinicQueueItemTrackingFields')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledAt') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260520000000_AddClinicQueueItemTrackingFields', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520202816_SyncAuditPhase2Configurations')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AuditLogs') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260520202816_SyncAuditPhase2Configurations', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddPasswordSaltAndPatientPhoneIndexes')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260521000000_AddPasswordSaltAndPatientPhoneIndexes', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522000000_AddSoftDeleteColumnsToLegacyTables')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260522000000_AddSoftDeleteColumnsToLegacyTables', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525000000_AddMissingFKIndexesAndUserMustChangePassword')
                           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260525000000_AddMissingFKIndexesAndUserMustChangePassword', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528000000_AddClinicServicesAndRooms')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260528000000_AddClinicServicesAndRooms', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260602000000_AddMessageAttachments')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageAttachments') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260602000000_AddMessageAttachments', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260604000000_AddSuppliersAndPurchases')
                           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers') THEN
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260604000000_AddSuppliersAndPurchases', '8.0');
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260608000000_AddRolePermissionUniqueIndex')
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260608000000_AddRolePermissionUniqueIndex', '8.0');
                        IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260609000000_AddEmailLog')
                            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260609000000_AddEmailLog', '8.0');
                    END $$;
                """);
                logger.LogInformation("Migration history reconciliation completed — cleaned incorrect records and inserted verified records");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Migration history reconciliation failed (non-fatal) — MigrateAsync may encounter errors");
            }

            try
            {
                // Pre-migration: ensure Financial Integrity Sprint columns exist before EF Core tries to use them
                // This is needed because the migration SQL might fail silently on some PostgreSQL versions
                try
                {
                    await db.Database.ExecuteSqlRawAsync("""
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "IsReversal" boolean NOT NULL DEFAULT false;
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversalOfTransactionId" uuid NULL;
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversedByTransactionId" uuid NULL;
                        CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversalOfTransactionId" ON "CashFlowTransactions" ("ReversalOfTransactionId");
                        CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversedByTransactionId" ON "CashFlowTransactions" ("ReversedByTransactionId");
                        CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId" ON "Treasuries" ("BranchId");
                        CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type" ON "Treasuries" ("BranchId", "Type");
                    """);
                    logger.LogInformation("Financial Integrity Sprint columns verified/created pre-migration");
                }
                catch (Exception exPre)
                {
                    logger.LogWarning(exPre, "Pre-migration column check failed (non-fatal)");
                }

                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Migration failed, attempting to ensure critical tables exist manually");

                // If migration fails, ensure the Financial Integrity Sprint columns exist manually
                try
                {
                    await db.Database.ExecuteSqlRawAsync("""
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "IsReversal" boolean NOT NULL DEFAULT false;
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversalOfTransactionId" uuid NULL;
                        ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversedByTransactionId" uuid NULL;
                        CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversalOfTransactionId" ON "CashFlowTransactions" ("ReversalOfTransactionId");
                        CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversedByTransactionId" ON "CashFlowTransactions" ("ReversedByTransactionId");
                        CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId" ON "Treasuries" ("BranchId");
                        CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type" ON "Treasuries" ("BranchId", "Type");
                    """);
                    logger.LogInformation("Financial Integrity Sprint columns created manually after migration failure");
                }
                catch (Exception ex2)
                {
                    logger.LogError(ex2, "Failed to create Financial Integrity Sprint columns manually");
                }

                // Manually create messaging tables if they don't exist
                try
                {
                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TABLE IF NOT EXISTS "Conversations" (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "Title" character varying(200) NOT NULL,
                            "IsGroup" boolean NOT NULL,
                            "CreatedBy" uuid NULL,
                            "LastMessageAt" timestamp with time zone NULL,
                            "LastMessagePreview" character varying(500) NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            "IsActive" boolean NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS "IX_Conversations_LastMessageAt" ON "Conversations" ("LastMessageAt");

                        ALTER TABLE "Conversations" DROP CONSTRAINT IF EXISTS "FK_Conversations_Users_CreatedBy";
                        DO $$ BEGIN
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Users_CreatedBy') THEN
                                ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Users_CreatedBy" 
                                    FOREIGN KEY ("CreatedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;
                            END IF;
                        END $$;

                        -- Add Phase 1-4 columns to Conversations
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                        END IF;
                        -- RecipientType/RecipientUserId columns are ensured by the unconditional hotfix block above
                        CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId" ON "Conversations" ("PatientId");
                        CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType" ON "Conversations" ("ConversationType");
                    """);

                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TABLE IF NOT EXISTS "ConversationParticipants" (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "ConversationId" uuid NOT NULL,
                            "UserId" uuid NOT NULL,
                            "IsAdmin" boolean NOT NULL,
                            "LastReadAt" timestamp with time zone NULL,
                            "IsMuted" boolean NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            "IsActive" boolean NOT NULL,
                            "DeletedAt" timestamp with time zone NULL,
                            "DeletedBy" uuid NULL
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConversationParticipants_ConversationId_UserId" 
                            ON "ConversationParticipants" ("ConversationId", "UserId");

                        DO $$ BEGIN
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Conversations_ConversationId') THEN
                                ALTER TABLE "ConversationParticipants" ADD CONSTRAINT "FK_ConversationParticipants_Conversations_ConversationId" 
                                    FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Users_UserId') THEN
                                ALTER TABLE "ConversationParticipants" ADD CONSTRAINT "FK_ConversationParticipants_Users_UserId" 
                                    FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                            END IF;
                            -- Ensure DeletedAt/DeletedBy columns exist on ConversationParticipants
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedAt') THEN
                                ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedBy') THEN
                                ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedBy" uuid NULL;
                            END IF;
                        END $$;
                    """);

                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TABLE IF NOT EXISTS "Messages" (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "ConversationId" uuid NOT NULL,
                            "SenderId" uuid NOT NULL,
                            "Content" text NOT NULL,
                            "AttachmentUrl" character varying(1000) NULL,
                            "AttachmentName" character varying(255) NULL,
                            "AttachmentType" character varying(50) NULL,
                            "ReplyToId" uuid NULL,
                            "IsSystemMessage" boolean NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            "IsActive" boolean NOT NULL,
                            "DeletedAt" timestamp with time zone NULL,
                            "DeletedBy" uuid NULL
                        );
                        CREATE INDEX IF NOT EXISTS "IX_Messages_ConversationId" ON "Messages" ("ConversationId");
                        CREATE INDEX IF NOT EXISTS "IX_Messages_CreatedAt" ON "Messages" ("CreatedAt");

                        DO $$ BEGIN
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Conversations_ConversationId') THEN
                                ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Conversations_ConversationId" 
                                    FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Users_SenderId') THEN
                                ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Users_SenderId" 
                                    FOREIGN KEY ("SenderId") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Messages_ReplyToId') THEN
                                ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Messages_ReplyToId" 
                                    FOREIGN KEY ("ReplyToId") REFERENCES "Messages"("Id") ON DELETE SET NULL;
                            END IF;
                            -- Ensure DeletedAt/DeletedBy columns exist on Messages
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                                ALTER TABLE "Messages" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedBy') THEN
                                ALTER TABLE "Messages" ADD COLUMN "DeletedBy" uuid NULL;
                            END IF;
                            -- Ensure IsEdited/EditedAt columns exist on Messages
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                                ALTER TABLE "Messages" ADD COLUMN "IsEdited" boolean NOT NULL DEFAULT false;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
                                ALTER TABLE "Messages" ADD COLUMN "EditedAt" timestamp with time zone NULL;
                            END IF;
                        END $$;
                    """);

                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TABLE IF NOT EXISTS "MessageReads" (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "MessageId" uuid NOT NULL,
                            "UserId" uuid NOT NULL,
                            "ReadAt" timestamp with time zone NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            "IsActive" boolean NOT NULL,
                            "DeletedAt" timestamp with time zone NULL,
                            "DeletedBy" uuid NULL
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MessageReads_MessageId_UserId" 
                            ON "MessageReads" ("MessageId", "UserId");

                        DO $$ BEGIN
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Messages_MessageId') THEN
                                ALTER TABLE "MessageReads" ADD CONSTRAINT "FK_MessageReads_Messages_MessageId" 
                                    FOREIGN KEY ("MessageId") REFERENCES "Messages"("Id") ON DELETE CASCADE;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Users_UserId') THEN
                                ALTER TABLE "MessageReads" ADD CONSTRAINT "FK_MessageReads_Users_UserId" 
                                    FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                            END IF;
                            -- Ensure DeletedAt/DeletedBy columns exist on MessageReads
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedAt') THEN
                                ALTER TABLE "MessageReads" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedBy') THEN
                                ALTER TABLE "MessageReads" ADD COLUMN "DeletedBy" uuid NULL;
                            END IF;
                        END $$;
                    """);

                    logger.LogInformation("Messaging tables created manually as fallback");
                }
                catch (Exception innerEx)
                {
                    logger.LogError(innerEx, "Failed to create messaging tables manually");
                }
            }

            // ClinicQueueItems table + tracking fields — now handled by EF migrations
            // 20260514000000_AddClinicQueueItems and 20260520000000_AddClinicQueueTrackingFields
            // (TD-010 / TD-010 safety nets removed in TD-020 Phase C1-e)

            // Fresh-install fix: the EF model expects DeletedAt/DeletedBy on every
            // BaseEntity table, but migration 20260522000000 lists only 39 tables —
            // on a brand-new database ~48 tables (e.g. RolePermissions) end up
            // without them and DbSeeder's first query fails with
            // "column r.DeletedAt does not exist". Idempotent, no-op when present.
            await EnsureSoftDeleteColumnsOnBaseEntityTablesAsync(app);

            await DbSeeder.SeedAsync(db, logger);

            // Seed PatientAccounts for existing patients
            try
            {
                using var seedScope = app.Services.CreateScope();
                var portalSvc = seedScope.ServiceProvider.GetRequiredService<IPatientPortalService>();
                var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var patientsWithoutAccount = await seedDb.Patients
                    .Where(p => p.IsActive && !seedDb.PatientAccounts.Any(a => a.PatientId == p.Id))
                    .Take(100)
                    .ToListAsync();
                foreach (var p in patientsWithoutAccount)
                {
                    await portalSvc.EnsurePatientAccountAsync(p.Id, p.PatientNumber, p.Phone);
                }
                if (patientsWithoutAccount.Count > 0)
                    logger.LogInformation("Seeded PatientAccounts for {Count} existing patients", patientsWithoutAccount.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed PatientAccounts for existing patients");
            }
            } // end else (acquiredLock)

            // ── Release advisory lock ───────────────────────────────────────────────
            if (acquiredLock)
            {
                try
                {
                    using var releaseCmd = db.Database.GetDbConnection().CreateCommand();
                    releaseCmd.CommandText = $"SELECT pg_advisory_unlock({lockKey})";
                    await releaseCmd.ExecuteNonQueryAsync();
                    logger.LogInformation("DB maintenance advisory lock released");
                }
                catch (Exception releaseEx)
                {
                    logger.LogWarning(releaseEx, "Failed to release advisory lock (will auto-release on connection close)");
                }
            }

            try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }

            } // end using scope
        } // end if (enableStartupDbMaintenance)
        else
        {
            app.Logger.LogInformation("Startup DB maintenance is disabled (ENABLE_STARTUP_DB_MAINTENANCE=false). Skipping migrations and seed.");
        }

    }

    /// <summary>
    /// HR tables (Attendances, SalaryRecords, AdvancePayments, LeaveRequests, EmployeeDocuments) + BackupRecords table.
    /// </summary>
    private static async Task EnsureHrAndBackupTablesAsync(WebApplication app)
    {
        try
        {
            using var hrScope = app.Services.CreateScope();
            var hrDb     = hrScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hrLogger = hrScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            await hrDb.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    -- ── Attendances table ──────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Attendances') THEN
                        CREATE TABLE "Attendances" (
                            "Id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "EmployeeId"  uuid                     NOT NULL,
                            "Date"        date                     NOT NULL,
                            "CheckIn"     interval                 NULL,
                            "CheckOut"    interval                 NULL,
                            "Status"      integer                  NOT NULL DEFAULT 0,
                            "Notes"       character varying(500)   NULL,
                            "CreatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"    boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"   timestamp with time zone NULL,
                            "DeletedBy"   uuid                     NULL,
                            CONSTRAINT "PK_Attendances" PRIMARY KEY ("Id")
                        );
                        CREATE UNIQUE INDEX "IX_Attendances_EmployeeId_Date" ON "Attendances" ("EmployeeId", "Date");
                        CREATE INDEX "IX_Attendances_EmployeeId" ON "Attendances" ("EmployeeId");
                    END IF;

                    -- ── SalaryRecords table ────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'SalaryRecords') THEN
                        CREATE TABLE "SalaryRecords" (
                            "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "EmployeeId"    uuid                     NOT NULL,
                            "Year"          integer                  NOT NULL,
                            "Month"         integer                  NOT NULL,
                            "BaseSalary"    numeric(12,2)            NOT NULL DEFAULT 0,
                            "Deductions"    numeric(12,2)            NOT NULL DEFAULT 0,
                            "Advances"      numeric(12,2)            NOT NULL DEFAULT 0,
                            "Bonuses"       numeric(12,2)            NOT NULL DEFAULT 0,
                            "NetSalary"     numeric(12,2)            NOT NULL DEFAULT 0,
                            "PaidAt"        timestamp with time zone NULL,
                            "PaidBy"        uuid                     NULL,
                            "PaymentMethod" character varying(50)    NULL,
                            "Notes"         character varying(500)   NULL,
                            "CreatedAt"     timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"     timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"      boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"     timestamp with time zone NULL,
                            "DeletedBy"     uuid                     NULL,
                            CONSTRAINT "PK_SalaryRecords" PRIMARY KEY ("Id")
                        );
                        CREATE UNIQUE INDEX "IX_SalaryRecords_EmployeeId_Year_Month" ON "SalaryRecords" ("EmployeeId", "Year", "Month");
                        CREATE INDEX "IX_SalaryRecords_EmployeeId" ON "SalaryRecords" ("EmployeeId");
                    END IF;

                    -- ── AdvancePayments table ──────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AdvancePayments') THEN
                        CREATE TABLE "AdvancePayments" (
                            "Id"              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "EmployeeId"      uuid                     NOT NULL,
                            "Amount"          numeric(12,2)            NOT NULL,
                            "Reason"          character varying(500)   NULL,
                            "RequestDate"     timestamp with time zone NOT NULL DEFAULT now(),
                            "Status"          integer                  NOT NULL DEFAULT 0,
                            "ApprovedBy"      uuid                     NULL,
                            "ApprovedAt"      timestamp with time zone NULL,
                            "RejectionReason" character varying(500)   NULL,
                            "DeductFromMonth" integer                  NULL,
                            "DeductFromYear"  integer                  NULL,
                            "IsDeducted"      boolean                  NOT NULL DEFAULT false,
                            "CreatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"        boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"       timestamp with time zone NULL,
                            "DeletedBy"       uuid                     NULL,
                            CONSTRAINT "PK_AdvancePayments" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_AdvancePayments_EmployeeId" ON "AdvancePayments" ("EmployeeId");
                    END IF;

                    -- ── LeaveRequests table ────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LeaveRequests') THEN
                        CREATE TABLE "LeaveRequests" (
                            "Id"              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "EmployeeId"      uuid                     NOT NULL,
                            "LeaveType"       integer                  NOT NULL DEFAULT 0,
                            "StartDate"       date                     NOT NULL,
                            "EndDate"         date                     NOT NULL,
                            "TotalDays"       integer                  NOT NULL,
                            "Reason"          character varying(500)   NULL,
                            "Status"          integer                  NOT NULL DEFAULT 0,
                            "ApprovedBy"      uuid                     NULL,
                            "ApprovedAt"      timestamp with time zone NULL,
                            "RejectionReason" character varying(500)   NULL,
                            "CreatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"        boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"       timestamp with time zone NULL,
                            "DeletedBy"       uuid                     NULL,
                            CONSTRAINT "PK_LeaveRequests" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_LeaveRequests_EmployeeId" ON "LeaveRequests" ("EmployeeId");
                    END IF;

                    -- ── EmployeeDocuments table ────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'EmployeeDocuments') THEN
                        CREATE TABLE "EmployeeDocuments" (
                            "Id"           uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "EmployeeId"   uuid                     NOT NULL,
                            "DocumentType" character varying(100)   NOT NULL,
                            "Title"        character varying(200)   NOT NULL,
                            "FilePath"     character varying(500)   NOT NULL,
                            "FileName"     character varying(300)   NULL,
                            "ContentType"  character varying(100)   NULL,
                            "FileSize"     bigint                   NULL,
                            "UploadedBy"   uuid                     NOT NULL,
                            "CreatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"     boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"    timestamp with time zone NULL,
                            "DeletedBy"    uuid                     NULL,
                            CONSTRAINT "PK_EmployeeDocuments" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_EmployeeDocuments_EmployeeId" ON "EmployeeDocuments" ("EmployeeId");
                    END IF;

                    -- ── BackupRecords table ────────────────────────────────────────
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'BackupRecords') THEN
                        CREATE TABLE "BackupRecords" (
                            "Id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                            "Type"        integer                  NOT NULL DEFAULT 0,
                            "Status"      integer                  NOT NULL DEFAULT 0,
                            "StartedAt"   timestamp with time zone NOT NULL,
                            "CompletedAt" timestamp with time zone NULL,
                            "SizeBytes"   bigint                   NULL,
                            "FilePath"    character varying(500)   NULL,
                            "ErrorMessage" character varying(2000) NULL,
                            "TriggeredBy" uuid                     NULL,
                            "IsAutomatic" boolean                  NOT NULL DEFAULT false,
                            "CreatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                            "UpdatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                            "IsActive"    boolean                  NOT NULL DEFAULT true,
                            "DeletedAt"   timestamp with time zone NULL,
                            "DeletedBy"   uuid                     NULL,
                            CONSTRAINT "PK_BackupRecords" PRIMARY KEY ("Id")
                        );
                        CREATE INDEX "IX_BackupRecords_StartedAt" ON "BackupRecords" ("StartedAt");
                    END IF;
                END $$;
            """);

            hrLogger.LogInformation("HOTFIX: HR and Backup tables ensured (idempotent)");
        }
        catch (Exception ex)
        {
            var hrLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
            hrLogger2.LogError(ex, "HOTFIX: Failed to ensure HR/Backup tables. HR and Backup endpoints may return 500!");
        }

    }

    /// <summary>
    /// MULTI-CURRENCY: Idempotently adds the nullable Currency column to Payments
    /// so patients can pay in SAR/USD in addition to YER. Mirrors the migration
    /// 20260710000000_AddPaymentCurrency for partially-migrated DBs (C-08 pattern).
    /// </summary>
    private static async Task EnsurePaymentCurrencyColumnAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NULL;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Payment Currency column hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// MULTI-CURRENCY (FIX): idempotently adds the remaining multi-currency columns and
    /// indexes that migration 20260711000000_AddFinanceAccountCurrencyAndTreasuryCurrency
    /// introduces, for databases where that migration did not apply cleanly (the historical
    /// migration chain is known-broken — see CLAUDE.md). EF SELECTs every mapped column, so a
    /// missing Treasury/CashFlow/Invoice/Contract "Currency" column makes ANY query loading
    /// those entities throw "column does not exist" — which breaks finance pages and the
    /// cashier open/close flow (it resolves a treasury filtered by Currency and writes a
    /// CashFlowTransaction). All statements use ADD COLUMN IF NOT EXISTS so this is a no-op
    /// once the columns are present. Does NOT touch the migration baseline (C-08 pattern).
    /// </summary>
    private static async Task EnsureMultiCurrencyColumnsAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Payments"
                    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NULL,
                    ADD COLUMN IF NOT EXISTS "AccountCurrency" character varying(3) NOT NULL DEFAULT 'YER',
                    ADD COLUMN IF NOT EXISTS "ExchangeRateToAccountCurrency" numeric(18,6) NOT NULL DEFAULT 1,
                    ADD COLUMN IF NOT EXISTS "AppliedAmount" numeric(12,2) NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "ExchangeRateSource" character varying(50);

                ALTER TABLE "Contracts"            ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';
                ALTER TABLE "Invoices"             ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';
                ALTER TABLE "Treasuries"           ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';

                -- Backfill any pre-existing NULL/empty currency to the base YER so reads are consistent.
                UPDATE "Treasuries"           SET "Currency" = 'YER' WHERE "Currency" IS NULL OR "Currency" = '';
                UPDATE "CashFlowTransactions" SET "Currency" = 'YER' WHERE "Currency" IS NULL OR "Currency" = '';

                -- Treasury uniqueness now includes currency (one drawer/account per currency).
                DROP INDEX IF EXISTS "IX_Treasuries_BranchId_Type_Name_Unique";
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type_Currency_Name_Unique"
                    ON "Treasuries" ("BranchId", "Type", "Currency", "Name") WHERE "IsActive" = true;
                CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type_Currency"
                    ON "Treasuries" ("BranchId", "Type", "Currency");
                CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_BranchId_Currency_TransactionDate"
                    ON "CashFlowTransactions" ("BranchId", "Currency", "TransactionDate");
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Multi-currency columns hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// YOLO-S1 (C-08 pattern): Idempotently creates the TreatmentPackages table and
    /// adds the Appointment enhancement columns (CompanionName/CompanionPhone/
    /// CompanionRelationship/AppointmentColor/PackageId + FK + index) for databases
    /// where migration 20260712000000_AddAppointmentEnhancements did not apply cleanly
    /// (the historical migration chain is known-broken — see CLAUDE.md). EF SELECTs
    /// every mapped column, so a missing CompanionName column makes ANY query loading
    /// Appointments throw "column does not exist" — which breaks daily-ops + appointment
    /// pages. All statements use CREATE TABLE IF NOT EXISTS / ADD COLUMN IF NOT EXISTS
    /// so this is a no-op once the schema is present. Does NOT touch the migration
    /// baseline. Companion-aware WhatsApp reminders depend on CompanionPhone being
    /// queryable, so this block must run before AppointmentReminderJob ticks.
    /// </summary>
    private static async Task EnsureAppointmentEnhancementsSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                -- ── TreatmentPackages table ──────────────────────────────────────
                CREATE TABLE IF NOT EXISTS "TreatmentPackages" (
                    "Id"           uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "Name"         character varying(200)   NOT NULL,
                    "Description"  character varying(1000)  NULL,
                    "TotalPrice"   numeric(12,2)            NOT NULL DEFAULT 0,
                    "SessionCount" integer                  NOT NULL DEFAULT 1,
                    "Color"        character varying(20)    NULL,
                    "IsActive"     boolean                  NOT NULL DEFAULT true,
                    "CreatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                    "DeletedAt"    timestamp with time zone NULL,
                    "DeletedBy"    uuid                     NULL,
                    CONSTRAINT "PK_TreatmentPackages" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_TreatmentPackages_Name_IsActive"
                    ON "TreatmentPackages" ("Name", "IsActive");

                -- ── Appointments: YOLO-S1 enhancement columns ────────────────────
                ALTER TABLE "Appointments"
                    ADD COLUMN IF NOT EXISTS "CompanionName"         character varying(150) NULL,
                    ADD COLUMN IF NOT EXISTS "CompanionPhone"        character varying(30)  NULL,
                    ADD COLUMN IF NOT EXISTS "CompanionRelationship" character varying(50)  NULL,
                    ADD COLUMN IF NOT EXISTS "AppointmentColor"      character varying(20)  NULL,
                    ADD COLUMN IF NOT EXISTS "PackageId"             uuid                   NULL;

                CREATE INDEX IF NOT EXISTS "IX_Appointments_PackageId"
                    ON "Appointments" ("PackageId");

                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_Appointments_TreatmentPackages_PackageId'
                    ) THEN
                        ALTER TABLE "Appointments"
                            ADD CONSTRAINT "FK_Appointments_TreatmentPackages_PackageId"
                            FOREIGN KEY ("PackageId") REFERENCES "TreatmentPackages"("Id")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Appointment enhancements (YOLO-S1) schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// YOLO-S2: Idempotently adds Contract.PackageId + TreatmentPackageServices +
    /// ServiceConsumables tables + ClinicServices.Color column (C-08 pattern).
    /// </summary>
    private static async Task EnsureServicePackagesConsumablesSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Contracts" ADD COLUMN IF NOT EXISTS "PackageId" uuid NULL;
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Contracts_TreatmentPackages_PackageId') THEN
                        ALTER TABLE "Contracts" ADD CONSTRAINT "FK_Contracts_TreatmentPackages_PackageId"
                            FOREIGN KEY ("PackageId") REFERENCES "TreatmentPackages"("Id") ON DELETE SET NULL;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS "TreatmentPackageServices" (
                    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "TreatmentPackageId" uuid NOT NULL,
                    "ClinicServiceId" uuid NOT NULL,
                    "Quantity" integer NOT NULL DEFAULT 1,
                    "OverridePrice" numeric(12,2) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_TreatmentPackageServices" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_TreatmentPackageServices_TreatmentPackageId" ON "TreatmentPackageServices" ("TreatmentPackageId");
                CREATE INDEX IF NOT EXISTS "IX_TreatmentPackageServices_ClinicServiceId" ON "TreatmentPackageServices" ("ClinicServiceId");
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TreatmentPackageServices_TreatmentPackages_TreatmentPackageId') THEN
                        ALTER TABLE "TreatmentPackageServices" ADD CONSTRAINT "FK_TreatmentPackageServices_TreatmentPackages_TreatmentPackageId"
                            FOREIGN KEY ("TreatmentPackageId") REFERENCES "TreatmentPackages"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TreatmentPackageServices_ClinicServices_ClinicServiceId') THEN
                        ALTER TABLE "TreatmentPackageServices" ADD CONSTRAINT "FK_TreatmentPackageServices_ClinicServices_ClinicServiceId"
                            FOREIGN KEY ("ClinicServiceId") REFERENCES "ClinicServices"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS "ServiceConsumables" (
                    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "ClinicServiceId" uuid NOT NULL,
                    "InventoryItemId" uuid NOT NULL,
                    "Quantity" numeric(12,2) NOT NULL DEFAULT 1,
                    "Notes" character varying(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL,
                    CONSTRAINT "PK_ServiceConsumables" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_ServiceConsumables_ClinicServiceId" ON "ServiceConsumables" ("ClinicServiceId");
                CREATE INDEX IF NOT EXISTS "IX_ServiceConsumables_InventoryItemId" ON "ServiceConsumables" ("InventoryItemId");
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ServiceConsumables_ClinicServices_ClinicServiceId') THEN
                        ALTER TABLE "ServiceConsumables" ADD CONSTRAINT "FK_ServiceConsumables_ClinicServices_ClinicServiceId"
                            FOREIGN KEY ("ClinicServiceId") REFERENCES "ClinicServices"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ServiceConsumables_InventoryItems_InventoryItemId') THEN
                        ALTER TABLE "ServiceConsumables" ADD CONSTRAINT "FK_ServiceConsumables_InventoryItems_InventoryItemId"
                            FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'Color') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "Color" character varying(20) NULL;
                END IF;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Service packages/consumables (YOLO-S2) schema hotfix failed (non-fatal)");
        }
    }


    /// <summary>
    /// YOLO-S4: Inventory enhancements hotfix — mirrors migration
    /// 20260714000000_AddInventoryEnhancements. Idempotent (ADD COLUMN IF NOT
    /// EXISTS) so it is safe on databases where the migration has not yet been
    /// applied. Runs unconditionally on every boot so the app stays healthy
    /// even if EF MigrateAsync is disabled (ENABLE_STARTUP_DB_MAINTENANCE=false).
    /// </summary>
    private static async Task EnsureInventoryEnhancementsSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                -- ── Inventory: YOLO-S4 enhancement columns ────────────────────────
                ALTER TABLE "Inventory"
                    ADD COLUMN IF NOT EXISTS "MinStockLevel"     numeric(12,2)          NULL,
                    ADD COLUMN IF NOT EXISTS "PurchaseUnit"      character varying(30)  NULL,
                    ADD COLUMN IF NOT EXISTS "ConsumptionUnit"   character varying(30)  NULL,
                    ADD COLUMN IF NOT EXISTS "ImageUrl"          character varying(500) NULL,
                    ADD COLUMN IF NOT EXISTS "WarehouseLocation" character varying(100) NULL;

                CREATE INDEX IF NOT EXISTS "IX_Inventory_WarehouseLocation"
                    ON "Inventory" ("WarehouseLocation");
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Inventory enhancements (YOLO-S4) schema hotfix failed (non-fatal)");
        }
    }

    /// <summary>
    /// YOLO-S5: Patient segments hotfix — mirrors migration
    /// 20260715000000_AddPatientSegments. Idempotent (CREATE TABLE IF NOT
    /// EXISTS + DO $$ ... END $$ guard for FKs) so it is safe on databases
    /// where the migration has not yet been applied.
    /// </summary>
    private static async Task EnsurePatientSegmentsSchemaAsync(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.Database.IsRelational()) return;

            await db.Database.ExecuteSqlRawAsync("""
                -- ── PatientSegments table ────────────────────────────────────────
                CREATE TABLE IF NOT EXISTS "PatientSegments" (
                    "Id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "Name"        character varying(200)   NOT NULL,
                    "Description" character varying(1000)  NULL,
                    "Color"       character varying(20)    NULL,
                    "IsDynamic"   boolean                  NOT NULL DEFAULT false,
                    "QueryJson"   text                     NULL,
                    "CreatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"    boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"   timestamp with time zone NULL,
                    "DeletedBy"   uuid                     NULL,
                    CONSTRAINT "PK_PatientSegments" PRIMARY KEY ("Id")
                );

                CREATE INDEX IF NOT EXISTS "IX_PatientSegments_Name"
                    ON "PatientSegments" ("Name");
                CREATE INDEX IF NOT EXISTS "IX_PatientSegments_IsActive"
                    ON "PatientSegments" ("IsActive");

                -- ── PatientSegmentMembers table ──────────────────────────────────
                CREATE TABLE IF NOT EXISTS "PatientSegmentMembers" (
                    "Id"         uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "SegmentId"  uuid                     NOT NULL,
                    "PatientId"  uuid                     NOT NULL,
                    "AddedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                    "CreatedAt"  timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"  timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"   boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"  timestamp with time zone NULL,
                    "DeletedBy"  uuid                     NULL,
                    CONSTRAINT "PK_PatientSegmentMembers" PRIMARY KEY ("Id"),
                    CONSTRAINT "UQ_PatientSegmentMembers_SegmentId_PatientId"
                        UNIQUE ("SegmentId", "PatientId")
                );

                CREATE INDEX IF NOT EXISTS "IX_PatientSegmentMembers_SegmentId"
                    ON "PatientSegmentMembers" ("SegmentId");
                CREATE INDEX IF NOT EXISTS "IX_PatientSegmentMembers_PatientId"
                    ON "PatientSegmentMembers" ("PatientId");

                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_PatientSegmentMembers_PatientSegments_SegmentId'
                    ) THEN
                        ALTER TABLE "PatientSegmentMembers"
                            ADD CONSTRAINT "FK_PatientSegmentMembers_PatientSegments_SegmentId"
                            FOREIGN KEY ("SegmentId") REFERENCES "PatientSegments"("Id")
                            ON DELETE CASCADE;
                    END IF;
                END $$;

                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_PatientSegmentMembers_Patients_PatientId'
                    ) THEN
                        ALTER TABLE "PatientSegmentMembers"
                            ADD CONSTRAINT "FK_PatientSegmentMembers_Patients_PatientId"
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id")
                            ON DELETE CASCADE;
                    END IF;
                END $$;
                """);
        }
        catch (Exception ex)
        {
            app.Services.GetRequiredService<ILogger<Program>>()
                .LogWarning(ex, "Patient segments (YOLO-S5) schema hotfix failed (non-fatal)");
        }
    }

}
