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
