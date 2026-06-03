using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabOrderItemsAndPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceLineItemId",
                table: "LabOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "LabOrders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LabOrderId1",
                table: "InvoiceLineItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToothNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Arch = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Shade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RestorationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UnitsCount = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrderItems_LabWorkTypes_WorkTypeId",
                        column: x => x.WorkTypeId,
                        principalTable: "LabWorkTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabWorkPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    UrgentSurcharge = table.Column<decimal>(type: "numeric", nullable: true),
                    UrgentSurchargeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstimatedDays = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabWorkPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabWorkPrices_LabWorkTypes_WorkTypeId",
                        column: x => x.WorkTypeId,
                        principalTable: "LabWorkTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabWorkPrices_Labs_LabId",
                        column: x => x.LabId,
                        principalTable: "Labs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_LabOrderId1",
                table: "InvoiceLineItems",
                column: "LabOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_LabOrderId",
                table: "LabOrderItems",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderItems_WorkTypeId",
                table: "LabOrderItems",
                column: "WorkTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LabWorkPrices_LabId_WorkTypeId",
                table: "LabWorkPrices",
                columns: new[] { "LabId", "WorkTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabWorkPrices_WorkTypeId",
                table: "LabWorkPrices",
                column: "WorkTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId1",
                table: "InvoiceLineItems",
                column: "LabOrderId1",
                principalTable: "LabOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId1",
                table: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "LabOrderItems");

            migrationBuilder.DropTable(
                name: "LabWorkPrices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_LabOrderId1",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "InvoiceLineItemId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "LabOrderId1",
                table: "InvoiceLineItems");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_LabOrderId",
                table: "InvoiceLineItems",
                column: "LabOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId",
                table: "InvoiceLineItems",
                column: "LabOrderId",
                principalTable: "LabOrders",
                principalColumn: "Id");
        }
    }
}
