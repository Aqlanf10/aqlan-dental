using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// YOLO-S4: Inventory enhancements.
///
/// Adds to Inventory:
///   - MinStockLevel      numeric(12,2) NULL  (parallel decimal threshold to the
///                                                legacy int MinQuantity — kept
///                                                distinct to avoid changing
///                                                existing low-stock logic)
///   - PurchaseUnit       varchar(30)   NULL
///   - ConsumptionUnit    varchar(30)   NULL
///   - ImageUrl           varchar(500)  NULL
///   - WarehouseLocation  varchar(100)  NULL  (+ index for fast bin lookups)
///
/// ExpiryDate already existed before this sprint — left untouched.
///
/// Rules honored:
///   - All new columns are nullable → no behavior change for existing rows.
///   - Idempotent DDL (ADD COLUMN IF NOT EXISTS / CREATE INDEX IF NOT EXISTS)
///     so the migration is safe to apply on databases where the C-08 startup
///     hotfix already added the columns. Mirrors the existing
///     20260712000000_AddAppointmentEnhancements pattern (raw SQL because the
///     EF migration chain is historically broken — see CLAUDE.md pitfall).
///   - No behavior change for existing inventory. Existing low-stock logic
///     (Quantity <= MinQuantity) is untouched.
///
/// Down: drops the index + columns. Safe to roll back because all new fields
/// are optional and have no production data dependencies yet.
/// </summary>
public partial class AddInventoryEnhancements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Inventory: YOLO-S4 enhancement columns ────────────────────────────
        migrationBuilder.Sql("""
            ALTER TABLE "Inventory"
                ADD COLUMN IF NOT EXISTS "MinStockLevel"     numeric(12,2)         NULL,
                ADD COLUMN IF NOT EXISTS "PurchaseUnit"      character varying(30) NULL,
                ADD COLUMN IF NOT EXISTS "ConsumptionUnit"   character varying(30) NULL,
                ADD COLUMN IF NOT EXISTS "ImageUrl"          character varying(500) NULL,
                ADD COLUMN IF NOT EXISTS "WarehouseLocation" character varying(100) NULL;

            CREATE INDEX IF NOT EXISTS "IX_Inventory_WarehouseLocation"
                ON "Inventory" ("WarehouseLocation");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_Inventory_WarehouseLocation";

            ALTER TABLE "Inventory"
                DROP COLUMN IF EXISTS "WarehouseLocation",
                DROP COLUMN IF EXISTS "ImageUrl",
                DROP COLUMN IF EXISTS "ConsumptionUnit",
                DROP COLUMN IF EXISTS "PurchaseUnit",
                DROP COLUMN IF EXISTS "MinStockLevel";
            """);
    }
}
