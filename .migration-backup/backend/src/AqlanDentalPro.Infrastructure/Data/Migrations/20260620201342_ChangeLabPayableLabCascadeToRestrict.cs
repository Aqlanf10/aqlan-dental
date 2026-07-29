using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeLabPayableLabCascadeToRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabPayables_Labs_LabId",
                table: "LabPayables");

            migrationBuilder.AddForeignKey(
                name: "FK_LabPayables_Labs_LabId",
                table: "LabPayables",
                column: "LabId",
                principalTable: "Labs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabPayables_Labs_LabId",
                table: "LabPayables");

            migrationBuilder.AddForeignKey(
                name: "FK_LabPayables_Labs_LabId",
                table: "LabPayables",
                column: "LabId",
                principalTable: "Labs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
