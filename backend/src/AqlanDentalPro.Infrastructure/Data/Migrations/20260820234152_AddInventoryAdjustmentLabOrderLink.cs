using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAdjustmentLabOrderLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LabOrderId",
                table: "InventoryAdjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_LabOrderId",
                table: "InventoryAdjustments",
                column: "LabOrderId",
                filter: "\"LabOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryAdjustments_LabOrderId",
                table: "InventoryAdjustments");

            migrationBuilder.DropColumn(
                name: "LabOrderId",
                table: "InventoryAdjustments");
        }
    }
}
