using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds a filtered unique composite index on Treasuries (BranchId, Type, Name)
/// where IsActive = true. This prevents duplicate active treasuries for the same branch/type/name
/// combination while allowing soft-deleted treasuries with the same combination to coexist.
/// This migration is safe only if no duplicate active (BranchId, Type, Name) triples exist in production data.
/// If duplicates exist, the migration will fail at runtime and manual deduplication will be required first.
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
