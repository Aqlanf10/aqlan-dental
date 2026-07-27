using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateLabPayablesWithFinanceV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupplierBills_LabOrderId",
                table: "SupplierBills");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Suppliers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "MedicalVendor");

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "Labs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierBillId",
                table: "LabPayables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "LabOrders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "YER");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToYer",
                table: "LabOrders",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_LabOrderId",
                table: "SupplierBills",
                column: "LabOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Labs_SupplierId",
                table: "Labs",
                column: "SupplierId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabPayables_SupplierBillId",
                table: "LabPayables",
                column: "SupplierBillId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LabPayables_SupplierBills_SupplierBillId",
                table: "LabPayables",
                column: "SupplierBillId",
                principalTable: "SupplierBills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Labs_Suppliers_SupplierId",
                table: "Labs",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabPayables_SupplierBills_SupplierBillId",
                table: "LabPayables");

            migrationBuilder.DropForeignKey(
                name: "FK_Labs_Suppliers_SupplierId",
                table: "Labs");

            migrationBuilder.DropIndex(
                name: "IX_SupplierBills_LabOrderId",
                table: "SupplierBills");

            migrationBuilder.DropIndex(
                name: "IX_Labs_SupplierId",
                table: "Labs");

            migrationBuilder.DropIndex(
                name: "IX_LabPayables_SupplierBillId",
                table: "LabPayables");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Labs");

            migrationBuilder.DropColumn(
                name: "SupplierBillId",
                table: "LabPayables");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "ExchangeRateToYer",
                table: "LabOrders");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Suppliers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "MedicalVendor",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_LabOrderId",
                table: "SupplierBills",
                column: "LabOrderId");
        }
    }
}
