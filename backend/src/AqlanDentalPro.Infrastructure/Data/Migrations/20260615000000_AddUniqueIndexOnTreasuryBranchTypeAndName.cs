using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds a filtered unique composite index on Treasuries (BranchId, Type, Name)
/// where IsActive = true. This prevents duplicate active treasuries for the same branch/type/name
/// combination while allowing soft-deleted treasuries with the same combination to coexist.
///
/// BLOCKER 7 — DEFERRED: This migration must NOT be applied until all code blockers are resolved.
/// Before applying to production, run the following read-only preflight query to check for duplicates:
///
///   SELECT "BranchId", "Type", "Name", COUNT(*) AS cnt
///   FROM "Treasuries"
///   WHERE "IsActive" = true
///   GROUP BY "BranchId", "Type", "Name"
///   HAVING COUNT(*) > 1;
///
/// If any rows are returned, deduplicate them manually before applying this migration.
/// If the migration fails at runtime due to existing duplicates, manual deduplication is required.
/// </summary>
public partial class AddUniqueIndexOnTreasuryBranchTypeAndName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Treasuries_BranchId_Type_Name_Unique",
            table: "Treasuries",
            columns: new[] { "BranchId", "Type", "Name" },
            unique: true,
            filter: "\"IsActive\" = true");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Treasuries_BranchId_Type_Name_Unique",
            table: "Treasuries");
    }
}
