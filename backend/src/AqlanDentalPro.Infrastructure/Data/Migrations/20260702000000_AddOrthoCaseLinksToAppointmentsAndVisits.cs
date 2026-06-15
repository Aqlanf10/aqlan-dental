using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrthoCaseLinksToAppointmentsAndVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrthoCaseId",
                table: "Visits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrthoCaseId",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_OrthoCaseId",
                table: "Visits",
                column: "OrthoCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_OrthoCaseId",
                table: "Appointments",
                column: "OrthoCaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_OrthoCases_OrthoCaseId",
                table: "Appointments",
                column: "OrthoCaseId",
                principalTable: "OrthoCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_OrthoCases_OrthoCaseId",
                table: "Visits",
                column: "OrthoCaseId",
                principalTable: "OrthoCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_OrthoCases_OrthoCaseId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Visits_OrthoCases_OrthoCaseId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_OrthoCaseId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_OrthoCaseId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "OrthoCaseId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "OrthoCaseId",
                table: "Appointments");
        }
    }
}
