using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierBillsAndApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "OperationalExpenses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "OperationalExpenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "OperationalExpenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "OperationalExpenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CashFlowTransactionId",
                table: "OperationalExpenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPostedToLedger",
                table: "OperationalExpenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SupplierBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillNumber = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BillDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LabOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierBills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierBills_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierBills_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierBills_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierBills_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierBillPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierBillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PaidBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CashFlowTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierBillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierBillPayments_CashFlowTransactions_CashFlowTransacti~",
                        column: x => x.CashFlowTransactionId,
                        principalTable: "CashFlowTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierBillPayments_SupplierBills_SupplierBillId",
                        column: x => x.SupplierBillId,
                        principalTable: "SupplierBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalExpenses_ApprovedById",
                table: "OperationalExpenses",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalExpenses_CashFlowTransactionId",
                table: "OperationalExpenses",
                column: "CashFlowTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBillPayments_CashFlowTransactionId",
                table: "SupplierBillPayments",
                column: "CashFlowTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBillPayments_SupplierBillId",
                table: "SupplierBillPayments",
                column: "SupplierBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_BranchId",
                table: "SupplierBills",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_LabOrderId",
                table: "SupplierBills",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_PurchaseOrderId",
                table: "SupplierBills",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_SupplierId",
                table: "SupplierBills",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalExpenses_CashFlowTransactions_CashFlowTransactio~",
                table: "OperationalExpenses",
                column: "CashFlowTransactionId",
                principalTable: "CashFlowTransactions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationalExpenses_Users_ApprovedById",
                table: "OperationalExpenses",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationalExpenses_CashFlowTransactions_CashFlowTransactio~",
                table: "OperationalExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationalExpenses_Users_ApprovedById",
                table: "OperationalExpenses");

            migrationBuilder.DropTable(
                name: "SupplierBillPayments");

            migrationBuilder.DropTable(
                name: "SupplierBills");

            migrationBuilder.DropIndex(
                name: "IX_OperationalExpenses_ApprovedById",
                table: "OperationalExpenses");

            migrationBuilder.DropIndex(
                name: "IX_OperationalExpenses_CashFlowTransactionId",
                table: "OperationalExpenses");

            migrationBuilder.DropColumn(
                name: "ApprovalNotes",
                table: "OperationalExpenses");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "OperationalExpenses");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "OperationalExpenses");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "OperationalExpenses");

            migrationBuilder.DropColumn(
                name: "CashFlowTransactionId",
                table: "OperationalExpenses");

            migrationBuilder.DropColumn(
                name: "IsPostedToLedger",
                table: "OperationalExpenses");
        }
    }
}
