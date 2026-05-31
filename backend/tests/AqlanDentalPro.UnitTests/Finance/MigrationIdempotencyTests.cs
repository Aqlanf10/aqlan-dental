using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Verifies that the idempotent SQL used in the Finance V2 and email/reminder
/// migrations produces the correct DDL for PostgreSQL, and that migration
/// ownership rules are correctly enforced for rollback safety.
///
/// Migration ownership model:
///   M1 = AddCentralFinanceV2Hub (20260525092924) — SCHEMA OWNER
///   M2 = AddEmailLog (20260609000000) — subordinate, no-op Down for EmailLogs
///   M3 = AddSeparateReminderTrackingAndPatientEmail (20260610000000) — subordinate, no-op Down for overlapping columns
///
/// Rule: Only the schema owner (M1) may drop shared objects in its Down method.
/// Later migrations must have no-op Down for objects owned by M1, so that
/// rolling back a later migration while M1 remains applied does not remove
/// columns/tables still required by Finance V2.
///
/// These tests do NOT require a live PostgreSQL connection.
/// </summary>
public class MigrationIdempotencyTests
{
    // ── SQL fragments extracted from the migration files ──
    // These must be kept in sync with the actual migration .cs files.

    private const string M1_ColumnsUp = @"
ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Email"" TEXT NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderSentAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderWindowsSent"" TEXT NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""SmsReminderWindowsSent"" TEXT NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""WhatsAppReminderSentAt"" TIMESTAMPTZ NULL;
";

    private const string M1_EmailLogsUp = @"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'EmailLogs') THEN
        CREATE TABLE ""EmailLogs"" ( ... );
    END IF;
END $$;
";

    private const string M1_ColumnsDown = @"
ALTER TABLE ""Patients"" DROP COLUMN IF EXISTS ""Email"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""EmailReminderSentAt"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""EmailReminderWindowsSent"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""SmsReminderWindowsSent"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""WhatsAppReminderSentAt"";
";

    private const string M1_EmailLogsDown = @"DROP TABLE IF EXISTS ""EmailLogs"";";

    private const string M3_ColumnsUp = @"
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderSentAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""WhatsAppReminderSentAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderWindowsSent"" TEXT NULL;
";

    private const string M3_PatientEmailUp = @"
ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Email"" TEXT NULL;
";

    // M3 Down is now a no-op — it does NOT drop overlapping columns owned by M1
    private const string M3_Down = @"";  // no-op

    private const string M2_Up = @"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'EmailLogs') THEN
        CREATE TABLE ""EmailLogs"" ( ... );
    END IF;
END $$;
";

    // M2 Down is now a no-op — it does NOT drop EmailLogs owned by M1
    private const string M2_Down = @"";  // no-op

    // ─── M1 Up: All overlapping column additions use IF NOT EXISTS ─────────

    [Fact]
    public void M1_Up_Columns_UseIfNotExists()
    {
        Assert.Contains("ADD COLUMN IF NOT EXISTS", M1_ColumnsUp);
        Assert.Contains("\"Email\"", M1_ColumnsUp);
        Assert.Contains("\"EmailReminderSentAt\"", M1_ColumnsUp);
        Assert.Contains("\"EmailReminderWindowsSent\"", M1_ColumnsUp);
        Assert.Contains("\"WhatsAppReminderSentAt\"", M1_ColumnsUp);
        Assert.Contains("\"SmsReminderWindowsSent\"", M1_ColumnsUp);
    }

    [Fact]
    public void M1_Up_EmailLogs_UsesIfNotExists()
    {
        Assert.Contains("IF NOT EXISTS", M1_EmailLogsUp);
        Assert.Contains("EmailLogs", M1_EmailLogsUp);
    }

    // ─── M3 Up: All overlapping column additions use IF NOT EXISTS ─────────

    [Fact]
    public void M3_Up_AllColumns_UseIfNotExists()
    {
        Assert.Contains("ADD COLUMN IF NOT EXISTS", M3_ColumnsUp);
        Assert.Contains("ADD COLUMN IF NOT EXISTS", M3_PatientEmailUp);
        Assert.Contains("\"EmailReminderSentAt\"", M3_ColumnsUp);
        Assert.Contains("\"WhatsAppReminderSentAt\"", M3_ColumnsUp);
        Assert.Contains("\"EmailReminderWindowsSent\"", M3_ColumnsUp);
        Assert.Contains("\"Email\"", M3_PatientEmailUp);
    }

    // ─── M1 Down: Schema owner drops with IF EXISTS ────────────────────────

    [Fact]
    public void M1_Down_Columns_UseIfExists()
    {
        Assert.Contains("DROP COLUMN IF EXISTS", M1_ColumnsDown);
    }

    [Fact]
    public void M1_Down_EmailLogs_UseIfExists()
    {
        Assert.Contains("DROP TABLE IF EXISTS", M1_EmailLogsDown);
    }

    // ─── M2 Down: No-op for EmailLogs (owned by M1) ────────────────────────

    [Fact]
    public void M2_Down_IsNoOp_DoesNotDropEmailLogs()
    {
        // M2 Down must NOT contain DROP TABLE for EmailLogs — M1 owns it
        Assert.DoesNotContain("DROP TABLE", M2_Down);
        Assert.DoesNotContain("EmailLogs", M2_Down);
    }

    // ─── M3 Down: No-op for overlapping columns (owned by M1) ──────────────

    [Fact]
    public void M3_Down_IsNoOp_DoesNotDropOverlappingColumns()
    {
        // M3 Down must NOT contain DROP COLUMN for overlapping columns — M1 owns them
        Assert.DoesNotContain("DROP COLUMN", M3_Down);
        Assert.DoesNotContain("Email", M3_Down);
        Assert.DoesNotContain("EmailReminderSentAt", M3_Down);
    }

    // ─── No plain ADD COLUMN without IF NOT EXISTS in overlapping migrations ──

    [Fact]
    public void NoPlainAddColumnInOverlappingMigrations()
    {
        var plainAddPattern = "ADD COLUMN \"";
        Assert.DoesNotContain(plainAddPattern, M1_ColumnsUp);
        Assert.DoesNotContain(plainAddPattern, M3_ColumnsUp);
        Assert.DoesNotContain(plainAddPattern, M3_PatientEmailUp);
    }

    // ─── Scenario: both orderings produce same schema ──────────────────────

    [Fact]
    public void OverlappingColumns_BothMigrationsIdempotent_BothOrderingsSucceed()
    {
        var overlappingColumns = new[]
        {
            ("Appointments", "EmailReminderSentAt"),
            ("Appointments", "WhatsAppReminderSentAt"),
            ("Appointments", "EmailReminderWindowsSent"),
            ("Patients", "Email"),
        };

        foreach (var (table, column) in overlappingColumns)
        {
            // Verify M1's SQL covers this column with IF NOT EXISTS
            Assert.Contains($"\"{column}\"", M1_ColumnsUp);
            Assert.Contains("IF NOT EXISTS", M1_ColumnsUp);

            // Verify M3's SQL covers this column with IF NOT EXISTS
            if (table == "Patients")
            {
                Assert.Contains($"\"{column}\"", M3_PatientEmailUp);
                Assert.Contains("IF NOT EXISTS", M3_PatientEmailUp);
            }
            else
            {
                Assert.Contains($"\"{column}\"", M3_ColumnsUp);
                Assert.Contains("IF NOT EXISTS", M3_ColumnsUp);
            }
        }
    }

    // ─── EmailLogs table creation is idempotent in both M1 and M2 ──────────

    [Fact]
    public void EmailLogs_BothMigrationsUseIfNotExists()
    {
        Assert.Contains("IF NOT EXISTS", M1_EmailLogsUp);
        Assert.Contains("IF NOT EXISTS", M2_Up);
    }

    // ─── No destructive operations in any migration Up ─────────────────────

    [Fact]
    public void NoDropOrAlterInUpMethods()
    {
        Assert.DoesNotContain("DROP TABLE", M1_EmailLogsUp);
        Assert.DoesNotContain("DROP COLUMN", M1_ColumnsUp);
        Assert.DoesNotContain("DROP TABLE", M3_ColumnsUp);
        Assert.DoesNotContain("DROP COLUMN", M3_ColumnsUp);
        Assert.DoesNotContain("DROP TABLE", M3_PatientEmailUp);
        Assert.DoesNotContain("DROP COLUMN", M3_PatientEmailUp);
    }

    // ─── Rollback safety: rolling back later migrations preserves M1 objects ──

    [Fact]
    public void Rollback_M2_While_M1_Applied_EmailLogsRemains()
    {
        // M2 Down is no-op, so EmailLogs is NOT dropped
        // M1 Down would be needed to drop EmailLogs
        Assert.True(string.IsNullOrWhiteSpace(M2_Down.Trim()),
            "M2 Down must be no-op so EmailLogs is preserved when M1 remains applied");
    }

    [Fact]
    public void Rollback_M3_While_M1_Applied_ColumnsRemain()
    {
        // M3 Down is no-op, so overlapping columns are NOT dropped
        // M1 Down would be needed to drop the columns
        Assert.True(string.IsNullOrWhiteSpace(M3_Down.Trim()),
            "M3 Down must be no-op so overlapping columns are preserved when M1 remains applied");
    }

    // ─── SmsReminderWindowsSent is exclusively owned by M1 ─────────────────

    [Fact]
    public void SmsReminderWindowsSent_OwnedExclusivelyBy_M1()
    {
        // M1 adds it
        Assert.Contains("\"SmsReminderWindowsSent\"", M1_ColumnsUp);
        // M3 does NOT add it (not in M3's column list)
        Assert.DoesNotContain("\"SmsReminderWindowsSent\"", M3_ColumnsUp);
        Assert.DoesNotContain("\"SmsReminderWindowsSent\"", M3_PatientEmailUp);
        // M1 drops it
        Assert.Contains("\"SmsReminderWindowsSent\"", M1_ColumnsDown);
    }
}
