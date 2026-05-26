using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds a filtered unique composite index on Treasuries (BranchId, Type, Name)
/// where IsActive = true. This prevents duplicate active treasuries for the same branch/type/name
/// combination while allowing soft-deleted treasuries with the same combination to coexist.
///
/// BLOCKER 6 — DEFERRED: This migration must NOT be applied until all code blockers are resolved
/// and CI passes. Before applying to production, run the following read-only preflight query:
///
///   SELECT "BranchId", "Type", "Name", COUNT(*) AS duplicate_count
///   FROM "Treasuries"
///   WHERE "IsActive" = true
///   GROUP BY "BranchId", "Type", "Name"
///   HAVING COUNT(*) > 1;
///
/// Do NOT merge until:
///   1. Code blockers are fixed
///   2. CI passes
///   3. Production query returns zero rows
///
/// If any rows are returned, deduplicate them manually before applying this migration.
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
