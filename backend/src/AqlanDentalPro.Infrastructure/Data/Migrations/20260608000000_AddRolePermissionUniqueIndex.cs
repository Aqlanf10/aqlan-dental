using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Safe pre-migration deduplication ──────────────────────────────
            // If duplicate (Role, Resource) rows exist, keep the one with the
            // latest CreatedAt and delete the rest. This must happen BEFORE the
            // unique index is created, otherwise the migration will fail.
            //
            // We use raw SQL so it runs as part of the migration transaction.
            // The DELETE only fires when duplicates exist; zero-row deletes are
            // harmless.
            migrationBuilder.Sql(@"
DELETE FROM ""RolePermissions""
WHERE ""Id"" IN (
    SELECT rp.""Id""
    FROM ""RolePermissions"" rp
    INNER JOIN (
        SELECT ""Role"", ""Resource"", MAX(""CreatedAt"") AS MaxCreated
        FROM ""RolePermissions""
        GROUP BY ""Role"", ""Resource""
        HAVING COUNT(*) > 1
    ) dup ON rp.""Role"" = dup.""Role""
         AND rp.""Resource"" = dup.""Resource""
         AND rp.""CreatedAt"" < dup.""MaxCreated""
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
