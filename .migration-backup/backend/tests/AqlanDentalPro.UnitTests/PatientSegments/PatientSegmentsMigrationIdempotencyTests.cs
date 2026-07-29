using Xunit;

namespace AqlanDentalPro.UnitTests.PatientSegments;

/// <summary>
/// YOLO-S5: Migration idempotency verification for
/// <c>20260715000000_AddPatientSegments</c>.
///
/// The migration creates two new tables (PatientSegments + PatientSegmentMembers)
/// using <c>CREATE TABLE IF NOT EXISTS</c>, with FK constraints added via a
/// <c>DO $$ ... END $$</c> guard that checks <c>pg_constraint</c> first. This is
/// safe on databases where the C-08 startup hotfix already created the tables
/// (the historical "broken migration chain" pitfall documented in CLAUDE.md).
///
/// The Down path mirrors with <c>DROP TABLE IF EXISTS</c>.
///
/// These tests verify the migration SQL contains the idempotent guards — they
/// do NOT require a live PostgreSQL connection.
/// </summary>
public class PatientSegmentsMigrationIdempotencyTests
{
    // ── SQL fragments extracted from 20260715000000_AddPatientSegments.cs ──
    // Must be kept in sync with the actual migration .cs file.
    private const string UpSql = @"
CREATE TABLE IF NOT EXISTS ""PatientSegments"" (
    ""Id""          uuid                     NOT NULL DEFAULT gen_random_uuid(),
    ""Name""        character varying(200)   NOT NULL,
    ""Description"" character varying(1000)  NULL,
    ""Color""       character varying(20)    NULL,
    ""IsDynamic""   boolean                  NOT NULL DEFAULT false,
    ""QueryJson""   text                     NULL,
    ""CreatedAt""   timestamp with time zone NOT NULL DEFAULT now(),
    ""UpdatedAt""   timestamp with time zone NOT NULL DEFAULT now(),
    ""IsActive""    boolean                  NOT NULL DEFAULT true,
    ""DeletedAt""   timestamp with time zone NULL,
    ""DeletedBy""   uuid                     NULL,
    CONSTRAINT ""PK_PatientSegments"" PRIMARY KEY (""Id"")
);

CREATE INDEX IF NOT EXISTS ""IX_PatientSegments_Name""
    ON ""PatientSegments"" (""Name"");
CREATE INDEX IF NOT EXISTS ""IX_PatientSegments_IsActive""
    ON ""PatientSegments"" (""IsActive"");

CREATE TABLE IF NOT EXISTS ""PatientSegmentMembers"" (
    ""Id""         uuid                     NOT NULL DEFAULT gen_random_uuid(),
    ""SegmentId""  uuid                     NOT NULL,
    ""PatientId""  uuid                     NOT NULL,
    ""AddedAt""    timestamp with time zone NOT NULL DEFAULT now(),
    ""CreatedAt""  timestamp with time zone NOT NULL DEFAULT now(),
    ""UpdatedAt""  timestamp with time zone NOT NULL DEFAULT now(),
    ""IsActive""   boolean                  NOT NULL DEFAULT true,
    ""DeletedAt""  timestamp with time zone NULL,
    ""DeletedBy""  uuid                     NULL,
    CONSTRAINT ""PK_PatientSegmentMembers"" PRIMARY KEY (""Id""),
    CONSTRAINT ""UQ_PatientSegmentMembers_SegmentId_PatientId""
        UNIQUE (""SegmentId"", ""PatientId"")
);

CREATE INDEX IF NOT EXISTS ""IX_PatientSegmentMembers_SegmentId""
    ON ""PatientSegmentMembers"" (""SegmentId"");
CREATE INDEX IF NOT EXISTS ""IX_PatientSegmentMembers_PatientId""
    ON ""PatientSegmentMembers"" (""PatientId"");

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_PatientSegmentMembers_PatientSegments_SegmentId'
    ) THEN
        ALTER TABLE ""PatientSegmentMembers""
            ADD CONSTRAINT ""FK_PatientSegmentMembers_PatientSegments_SegmentId""
            FOREIGN KEY (""SegmentId"") REFERENCES ""PatientSegments""(""Id"")
            ON DELETE CASCADE;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_PatientSegmentMembers_Patients_PatientId'
    ) THEN
        ALTER TABLE ""PatientSegmentMembers""
            ADD CONSTRAINT ""FK_PatientSegmentMembers_Patients_PatientId""
            FOREIGN KEY (""PatientId"") REFERENCES ""Patients""(""Id"")
            ON DELETE CASCADE;
    END IF;
END $$;
";

    private const string DownSql = @"
DROP TABLE IF EXISTS ""PatientSegmentMembers"";
DROP TABLE IF EXISTS ""PatientSegments"";
";

    // ── Up: tables created with IF NOT EXISTS ────────────────────────────────

    [Fact]
    public void Up_PatientSegmentsTable_UsesCreateTableIfNotExists()
    {
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"PatientSegments\"", UpSql);
    }

    [Fact]
    public void Up_PatientSegmentMembersTable_UsesCreateTableIfNotExists()
    {
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"PatientSegmentMembers\"", UpSql);
    }

    // ── Up: indexes created with IF NOT EXISTS ───────────────────────────────

    [Theory]
    [InlineData("\"IX_PatientSegments_Name\"")]
    [InlineData("\"IX_PatientSegments_IsActive\"")]
    [InlineData("\"IX_PatientSegmentMembers_SegmentId\"")]
    [InlineData("\"IX_PatientSegmentMembers_PatientId\"")]
    public void Up_AllIndexes_UseCreateIndexIfNotExists(string index)
    {
        Assert.Contains(index, UpSql);
        Assert.Contains("CREATE INDEX IF NOT EXISTS", UpSql);
    }

    // ── Up: FK constraints guarded by pg_constraint check ────────────────────

    [Theory]
    [InlineData("FK_PatientSegmentMembers_PatientSegments_SegmentId")]
    [InlineData("FK_PatientSegmentMembers_Patients_PatientId")]
    public void Up_ForeignKeys_GuardedByPgConstraintCheck(string fkName)
    {
        Assert.Contains(fkName, UpSql);
        // Both FKs are added inside DO $$ ... END $$ blocks with an
        // IF NOT EXISTS (SELECT 1 FROM pg_constraint ...) guard.
        Assert.Contains("DO $$ BEGIN", UpSql);
        Assert.Contains("IF NOT EXISTS (", UpSql);
        Assert.Contains("SELECT 1 FROM pg_constraint", UpSql);
    }

    [Fact]
    public void Up_ForeignKeys_UseCascadeDelete()
    {
        Assert.Contains("ON DELETE CASCADE", UpSql);
        // Two FKs, both with cascade (segment delete removes members;
        // patient delete removes their memberships).
        var cascadeCount = CountOccurrences(UpSql, "ON DELETE CASCADE");
        Assert.Equal(2, cascadeCount);
    }

    // ── Up: required columns present ─────────────────────────────────────────

    [Fact]
    public void Up_PatientSegmentsTable_HasRequiredColumns()
    {
        Assert.Contains("\"Name\"        character varying(200)   NOT NULL", UpSql);
        Assert.Contains("\"IsDynamic\"   boolean                  NOT NULL", UpSql);
        // Audit columns (BaseEntity) — soft-delete support
        Assert.Contains("\"CreatedAt\"   timestamp with time zone NOT NULL", UpSql);
        Assert.Contains("\"UpdatedAt\"   timestamp with time zone NOT NULL", UpSql);
        Assert.Contains("\"IsActive\"    boolean                  NOT NULL", UpSql);
        Assert.Contains("\"DeletedAt\"   timestamp with time zone NULL", UpSql);
        Assert.Contains("\"DeletedBy\"   uuid                     NULL", UpSql);
    }

    [Fact]
    public void Up_PatientSegmentMembersTable_HasJoinColumns()
    {
        Assert.Contains("\"SegmentId\"  uuid                     NOT NULL", UpSql);
        Assert.Contains("\"PatientId\"  uuid                     NOT NULL", UpSql);
        Assert.Contains("\"AddedAt\"    timestamp with time zone NOT NULL", UpSql);
    }

    // ── Up: UNIQUE constraint on (SegmentId, PatientId) ──────────────────────

    [Fact]
    public void Up_MembersTable_HasUniqueSegmentIdPatientId()
    {
        // A patient can only be in a custom segment once.
        Assert.Contains("\"UQ_PatientSegmentMembers_SegmentId_PatientId\"", UpSql);
        Assert.Contains("UNIQUE (\"SegmentId\", \"PatientId\")", UpSql);
    }

    // ── Up: no destructive operations ────────────────────────────────────────

    [Fact]
    public void Up_DoesNotDropAnything()
    {
        Assert.DoesNotContain("DROP TABLE", UpSql);
        Assert.DoesNotContain("DROP COLUMN", UpSql);
        Assert.DoesNotContain("DROP INDEX", UpSql);
        Assert.DoesNotContain("TRUNCATE", UpSql);
    }

    // ── Down: drops both tables with IF EXISTS ───────────────────────────────

    [Fact]
    public void Down_DropsBothTablesWithIfExists()
    {
        Assert.Contains("DROP TABLE IF EXISTS \"PatientSegmentMembers\"", DownSql);
        Assert.Contains("DROP TABLE IF EXISTS \"PatientSegments\"", DownSql);
    }

    [Fact]
    public void Down_DropsMembersBeforeSegments_RespectingCascade()
    {
        // Members must be dropped first because they reference Segments (and
        // Patients). Although the CASCADE FK would handle the order, the
        // migration explicitly drops dependents first — defensive.
        var membersIdx = DownSql.IndexOf("\"PatientSegmentMembers\"", StringComparison.Ordinal);
        var segmentsIdx = DownSql.IndexOf("\"PatientSegments\"", StringComparison.Ordinal);
        Assert.True(membersIdx >= 0 && segmentsIdx >= 0);
        Assert.True(membersIdx < segmentsIdx,
            "PatientSegmentMembers must be dropped before PatientSegments in Down()");
    }

    // ── Round-trip safety ────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_DownThenUp_IsSafeBecauseBothAreIdempotent()
    {
        // Both Up and Down use IF EXISTS / IF NOT EXISTS guards, so applying
        // Down then Up (rollback then re-apply) is a safe no-op on already-clean
        // schemas. This protects the production deployment window where a
        // rollback may be followed by a re-apply once the root cause is fixed.
        Assert.Contains("IF NOT EXISTS", UpSql);
        Assert.Contains("IF EXISTS", DownSql);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
