using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260608000000_AddRolePermissionUniqueIndex")]
    public partial class AddRolePermissionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Safe pre-migration deduplication ──────────────────────────────
            // If duplicate (Role, Resource) rows exist, keep exactly one row per
            // (Role, Resource) and delete the rest. This must happen BEFORE the
            // unique index is created, otherwise the migration will fail.
            //
            // Deterministic tie-breaking: prefer the row with the latest CreatedAt,
            // then highest Id as a secondary tie-breaker (newest row wins both ways).
            // This handles the case where duplicate rows have identical CreatedAt values,
            // which the previous MAX(CreatedAt) approach missed.
            //
            // We use raw SQL so it runs as part of the migration transaction.
            // The DELETE only fires when duplicates exist; zero-row deletes are
            // harmless. Non-duplicate rows are never touched.
            migrationBuilder.Sql(@"
DELETE FROM ""RolePermissions""
WHERE ""Id"" NOT IN (
    SELECT DISTINCT ON (""Role"", ""Resource"") ""Id""
    FROM ""RolePermissions""
    ORDER BY ""Role"", ""Resource"", ""CreatedAt"" DESC, ""Id"" DESC
);");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Role_Resource",
                table: "RolePermissions",
                columns: new[] { "Role", "Resource" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_Role_Resource",
                table: "RolePermissions");
        }
    }
}
