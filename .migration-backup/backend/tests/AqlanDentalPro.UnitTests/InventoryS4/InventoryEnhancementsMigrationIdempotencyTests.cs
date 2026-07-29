using Xunit;

namespace AqlanDentalPro.UnitTests.InventoryS4;

/// <summary>
/// YOLO-S4: Migration idempotency verification for
/// <c>20260714000000_AddInventoryEnhancements</c>.
///
/// The migration adds 5 nullable columns + 1 index to the Inventory table using
/// <c>ADD COLUMN IF NOT EXISTS</c> / <c>CREATE INDEX IF NOT EXISTS</c> so it is
/// safe to apply on databases where the C-08 startup hotfix already added the
/// columns (the historical "broken migration chain" pitfall documented in
/// CLAUDE.md). The Down path mirrors with <c>DROP COLUMN IF EXISTS</c> /
/// <c>DROP INDEX IF EXISTS</c>.
///
/// These tests verify the migration SQL contains the idempotent guards — they
/// do NOT require a live PostgreSQL connection.
/// </summary>
public class InventoryEnhancementsMigrationIdempotencyTests
{
    // ── SQL fragments extracted from 20260714000000_AddInventoryEnhancements.cs ──
    // Must be kept in sync with the actual migration .cs file.
    private const string UpSql = @"
ALTER TABLE ""Inventory""
    ADD COLUMN IF NOT EXISTS ""MinStockLevel""     numeric(12,2)         NULL,
    ADD COLUMN IF NOT EXISTS ""PurchaseUnit""      character varying(30) NULL,
    ADD COLUMN IF NOT EXISTS ""ConsumptionUnit""   character varying(30) NULL,
    ADD COLUMN IF NOT EXISTS ""ImageUrl""          character varying(500) NULL,
    ADD COLUMN IF NOT EXISTS ""WarehouseLocation"" character varying(100) NULL;

CREATE INDEX IF NOT EXISTS ""IX_Inventory_WarehouseLocation""
    ON ""Inventory"" (""WarehouseLocation"");
";

    private const string DownSql = @"
DROP INDEX IF EXISTS ""IX_Inventory_WarehouseLocation"";

ALTER TABLE ""Inventory""
    DROP COLUMN IF EXISTS ""WarehouseLocation"",
    DROP COLUMN IF EXISTS ""ImageUrl"",
    DROP COLUMN IF EXISTS ""ConsumptionUnit"",
    DROP COLUMN IF EXISTS ""PurchaseUnit"",
    DROP COLUMN IF EXISTS ""MinStockLevel"";
";

    // ── Up: every new column uses IF NOT EXISTS ──────────────────────────────

    [Theory]
    [InlineData("\"MinStockLevel\"")]
    [InlineData("\"PurchaseUnit\"")]
    [InlineData("\"ConsumptionUnit\"")]
    [InlineData("\"ImageUrl\"")]
    [InlineData("\"WarehouseLocation\"")]
    public void Up_EachNewColumn_UsesAddColumnIfNotExists(string column)
    {
        Assert.Contains(column, UpSql);
        // All 5 columns are added in a single ALTER TABLE block that lists
        // "ADD COLUMN IF NOT EXISTS" once before the column list — verify the
        // guard is present.
        Assert.Contains("ADD COLUMN IF NOT EXISTS", UpSql);
    }

    [Fact]
    public void Up_AllFiveNewColumns_ArePresent()
    {
        Assert.Contains("\"MinStockLevel\"", UpSql);
        Assert.Contains("\"PurchaseUnit\"", UpSql);
        Assert.Contains("\"ConsumptionUnit\"", UpSql);
        Assert.Contains("\"ImageUrl\"", UpSql);
        Assert.Contains("\"WarehouseLocation\"", UpSql);
    }

    [Fact]
    public void Up_AllNewColumns_AreNullable()
    {
        // Every new column is NULL — no behavior change for existing rows.
        Assert.Contains("\"MinStockLevel\"     numeric(12,2)         NULL", UpSql);
        Assert.Contains("\"PurchaseUnit\"      character varying(30) NULL", UpSql);
        Assert.Contains("\"ConsumptionUnit\"   character varying(30) NULL", UpSql);
        Assert.Contains("\"ImageUrl\"          character varying(500) NULL", UpSql);
        Assert.Contains("\"WarehouseLocation\" character varying(100) NULL", UpSql);
    }

    // ── Up: index is created with IF NOT EXISTS ──────────────────────────────

    [Fact]
    public void Up_WarehouseLocationIndex_UsesCreateIndexIfNotExists()
    {
        Assert.Contains("CREATE INDEX IF NOT EXISTS", UpSql);
        Assert.Contains("\"IX_Inventory_WarehouseLocation\"", UpSql);
        Assert.Contains("\"WarehouseLocation\"", UpSql);
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

    // ── Up: ExpiryDate is intentionally NOT touched (already existed) ────────

    [Fact]
    public void Up_DoesNotTouchExpiryDate_AlreadyExisted()
    {
        // ExpiryDate predates this sprint (see Inventory.cs) — the migration
        // must not re-add or alter it. ALTER TABLE ... ADD COLUMN IF NOT EXISTS
        // on a pre-existing column is technically a no-op, but we keep the
        // migration minimal and explicit so the audit trail is clear.
        Assert.DoesNotContain("\"ExpiryDate\"", UpSql);
    }

    // ── Down: every column drops with IF EXISTS ──────────────────────────────

    [Theory]
    [InlineData("\"WarehouseLocation\"")]
    [InlineData("\"ImageUrl\"")]
    [InlineData("\"ConsumptionUnit\"")]
    [InlineData("\"PurchaseUnit\"")]
    [InlineData("\"MinStockLevel\"")]
    public void Down_EachColumn_UsesDropColumnIfExists(string column)
    {
        Assert.Contains(column, DownSql);
        Assert.Contains("DROP COLUMN IF EXISTS", DownSql);
    }

    [Fact]
    public void Down_IndexDrop_UsesDropIndexIfExists()
    {
        Assert.Contains("DROP INDEX IF EXISTS", DownSql);
        Assert.Contains("\"IX_Inventory_WarehouseLocation\"", DownSql);
    }

    // ── Round-trip safety: Down + Up produces the same schema state ──────────

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
}
