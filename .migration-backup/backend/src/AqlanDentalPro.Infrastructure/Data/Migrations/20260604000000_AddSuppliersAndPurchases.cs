using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Add Suppliers, PurchaseOrders, PurchaseOrderLineItems, InventoryAdjustments tables,
/// and add BatchNumber, ExpiryDate, DefaultSupplierId columns to Inventory table.
/// Additive only — new tables and columns, no destructive changes.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260604000000_AddSuppliersAndPurchases")]
public partial class AddSuppliersAndPurchases20260604000000 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Suppliers table ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContactPerson = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_Name",
            table: "Suppliers",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_Phone",
            table: "Suppliers",
            column: "Phone");

        // ── PurchaseOrders table ─────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "PurchaseOrders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                ReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                TaxAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true),
                BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                table.ForeignKey(
                    name: "FK_PurchaseOrders_Suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalTable: "Suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseOrders_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_SupplierId",
            table: "PurchaseOrders",
            column: "SupplierId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_Status",
            table: "PurchaseOrders",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_OrderNumber",
            table: "PurchaseOrders",
            column: "OrderNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_BranchId",
            table: "PurchaseOrders",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_OrderDate",
            table: "PurchaseOrders",
            column: "OrderDate");

        // ── PurchaseOrderLineItems table ─────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "PurchaseOrderLineItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ItemDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                UnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseOrderLineItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_PurchaseOrderLineItems_PurchaseOrders_PurchaseOrderId",
                    column: x => x.PurchaseOrderId,
                    principalTable: "PurchaseOrders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PurchaseOrderLineItems_Inventory_InventoryItemId",
                    column: x => x.InventoryItemId,
                    principalTable: "Inventory",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrderLineItems_PurchaseOrderId",
            table: "PurchaseOrderLineItems",
            column: "PurchaseOrderId");

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrderLineItems_InventoryItemId",
            table: "PurchaseOrderLineItems",
            column: "InventoryItemId");

        // ── InventoryAdjustments table ───────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "InventoryAdjustments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousQuantity = table.Column<int>(type: "integer", nullable: false),
                NewQuantity = table.Column<int>(type: "integer", nullable: false),
                Delta = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                AdjustmentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                PurchaseOrderLineItemId = table.Column<Guid>(type: "uuid", nullable: true),
                AdjustedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventoryAdjustments", x => x.Id);
                table.ForeignKey(
                    name: "FK_InventoryAdjustments_Inventory_InventoryItemId",
                    column: x => x.InventoryItemId,
                    principalTable: "Inventory",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InventoryAdjustments_InventoryItemId",
            table: "InventoryAdjustments",
            column: "InventoryItemId");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryAdjustments_AdjustmentType",
            table: "InventoryAdjustments",
            column: "AdjustmentType");

        migrationBuilder.CreateIndex(
            name: "IX_InventoryAdjustments_CreatedAt",
            table: "InventoryAdjustments",
            column: "CreatedAt");

        // ── Add columns to Inventory table ───────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "BatchNumber",
            table: "Inventory",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "ExpiryDate",
            table: "Inventory",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DefaultSupplierId",
            table: "Inventory",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Inventory_BatchNumber",
            table: "Inventory",
            column: "BatchNumber");

        migrationBuilder.CreateIndex(
            name: "IX_Inventory_ExpiryDate",
            table: "Inventory",
            column: "ExpiryDate");

        migrationBuilder.CreateIndex(
            name: "IX_Inventory_DefaultSupplierId",
            table: "Inventory",
            column: "DefaultSupplierId");

        migrationBuilder.AddForeignKey(
            name: "FK_Inventory_Suppliers_DefaultSupplierId",
            table: "Inventory",
            column: "DefaultSupplierId",
            principalTable: "Suppliers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Inventory_Suppliers_DefaultSupplierId",
            table: "Inventory");

        migrationBuilder.DropTable(
            name: "InventoryAdjustments");

        migrationBuilder.DropTable(
            name: "PurchaseOrderLineItems");

        migrationBuilder.DropTable(
            name: "PurchaseOrders");

        migrationBuilder.DropTable(
            name: "Suppliers");

        migrationBuilder.DropIndex(
            name: "IX_Inventory_BatchNumber",
            table: "Inventory");

        migrationBuilder.DropIndex(
            name: "IX_Inventory_ExpiryDate",
            table: "Inventory");

        migrationBuilder.DropIndex(
            name: "IX_Inventory_DefaultSupplierId",
            table: "Inventory");

        migrationBuilder.DropColumn(
            name: "BatchNumber",
            table: "Inventory");

        migrationBuilder.DropColumn(
            name: "ExpiryDate",
            table: "Inventory");

        migrationBuilder.DropColumn(
            name: "DefaultSupplierId",
            table: "Inventory");
    }
}
