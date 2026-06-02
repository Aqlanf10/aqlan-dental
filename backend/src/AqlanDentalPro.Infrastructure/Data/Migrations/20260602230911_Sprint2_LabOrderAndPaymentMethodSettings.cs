using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_LabOrderAndPaymentMethodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClinicQueueItems_PatientId_QueueDate",
                table: "ClinicQueueItems");

            migrationBuilder.DropIndex(
                name: "IX_CashierSessions_CashierId",
                table: "CashierSessions");

            migrationBuilder.AddColumn<string>(
                name: "ProposedProcedure",
                table: "Visits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Suppliers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Suppliers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "MedicalVendor");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "SupplierBills",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "SupplierBills",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unpaid",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "SupplierBills",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "SupplierBills",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "BillNumber",
                table: "SupplierBills",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "SupplierBillPayments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethod",
                table: "SupplierBillPayments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SupplierBillPayments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "OperationalExpenses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethod",
                table: "OperationalExpenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ExpenseNumber",
                table: "OperationalExpenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "OperationalExpenses",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "LabOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveredDate",
                table: "LabOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestorationType",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shade",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId",
                table: "LabOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoShowAt",
                table: "ClinicQueueItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "ClinicQueueItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<int>(
                name: "RecallCount",
                table: "ClinicQueueItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ClinicQueueItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "ShortageOrSurplus",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OpeningBalance",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedClosingCash",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedClosingCard",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedClosingBank",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualClosingCash",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualClosingCard",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualClosingBank",
                table: "CashierSessions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionNumber",
                table: "CashFlowTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "CashFlowTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethod",
                table: "CashFlowTransactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CashFlowTransactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "CashFlowTransactions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    RefundPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Payments_RefundPaymentId",
                        column: x => x.RefundPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethodSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequiresReferenceNumber = table.Column<bool>(type: "boolean", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethodSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierBills_Status",
                table: "SupplierBills",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalExpenses_ApprovalStatus",
                table: "OperationalExpenses",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalExpenses_ExpenseDate",
                table: "OperationalExpenses",
                column: "ExpenseDate");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_BranchId",
                table: "LabOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_VisitId",
                table: "LabOrders",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_CommissionStatus",
                table: "InvoiceLineItems",
                column: "CommissionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueItems_PatientId_QueueDate",
                table: "ClinicQueueItems",
                columns: new[] { "PatientId", "QueueDate" },
                unique: true,
                filter: "\"Status\" NOT IN ('Completed', 'Cancelled', 'NoShow')");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueItems_QueueDate_Priority_SortOrder",
                table: "ClinicQueueItems",
                columns: new[] { "QueueDate", "Priority", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierSessions_CashierId_Status",
                table: "CashierSessions",
                columns: new[] { "CashierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowTransactions_Category",
                table: "CashFlowTransactions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowTransactions_IsActive",
                table: "CashFlowTransactions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowTransactions_TransactionDate",
                table: "CashFlowTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowTransactions_Type",
                table: "CashFlowTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_BranchId",
                table: "CreditNotes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_InvoiceId",
                table: "CreditNotes",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_PatientId",
                table: "CreditNotes",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_RefundPaymentId",
                table: "CreditNotes",
                column: "RefundPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_Status",
                table: "CreditNotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodSettings_BranchId",
                table: "PaymentMethodSettings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodSettings_Code",
                table: "PaymentMethodSettings",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_Visits_VisitId",
                table: "LabOrders",
                column: "VisitId",
                principalTable: "Visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_Visits_VisitId",
                table: "LabOrders");

            migrationBuilder.DropTable(
                name: "CreditNotes");

            migrationBuilder.DropTable(
                name: "PaymentMethodSettings");

            migrationBuilder.DropIndex(
                name: "IX_SupplierBills_Status",
                table: "SupplierBills");

            migrationBuilder.DropIndex(
                name: "IX_OperationalExpenses_ApprovalStatus",
                table: "OperationalExpenses");

            migrationBuilder.DropIndex(
                name: "IX_OperationalExpenses_ExpenseDate",
                table: "OperationalExpenses");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_BranchId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_VisitId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_CommissionStatus",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_ClinicQueueItems_PatientId_QueueDate",
                table: "ClinicQueueItems");

            migrationBuilder.DropIndex(
                name: "IX_ClinicQueueItems_QueueDate_Priority_SortOrder",
                table: "ClinicQueueItems");

            migrationBuilder.DropIndex(
                name: "IX_CashierSessions_CashierId_Status",
                table: "CashierSessions");

            migrationBuilder.DropIndex(
                name: "IX_CashFlowTransactions_Category",
                table: "CashFlowTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CashFlowTransactions_IsActive",
                table: "CashFlowTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CashFlowTransactions_TransactionDate",
                table: "CashFlowTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CashFlowTransactions_Type",
                table: "CashFlowTransactions");

            migrationBuilder.DropColumn(
                name: "ProposedProcedure",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "DeliveredDate",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "RestorationType",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "Shade",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "NoShowAt",
                table: "ClinicQueueItems");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ClinicQueueItems");

            migrationBuilder.DropColumn(
                name: "RecallCount",
                table: "ClinicQueueItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ClinicQueueItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "SupplierBills",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "SupplierBills",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Unpaid");

            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "SupplierBills",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "SupplierBills",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "BillNumber",
                table: "SupplierBills",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "SupplierBillPayments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethod",
                table: "SupplierBillPayments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "SupplierBillPayments",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "OperationalExpenses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethod",
                table: "OperationalExpenses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ExpenseNumber",
                table: "OperationalExpenses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "OperationalExpenses",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ShortageOrSurplus",
                table: "CashierSessions",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OpeningBalance",
                table: "CashierSessions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedClosingCash",
                table: "CashierSessions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedClosingCard",
                table: "CashierSessions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExpectedClosingBank",
                table: "CashierSessions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualClosingCash",
                table: "CashierSessions",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualClosingCard",
                table: "CashierSessions",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualClosingBank",
                table: "CashierSessions",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionNumber",
                table: "CashFlowTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "CashFlowTransactions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethod",
                table: "CashFlowTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CashFlowTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "CashFlowTransactions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicQueueItems_PatientId_QueueDate",
                table: "ClinicQueueItems",
                columns: new[] { "PatientId", "QueueDate" },
                unique: true,
                filter: "\"Status\" NOT IN ('Completed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_CashierSessions_CashierId",
                table: "CashierSessions",
                column: "CashierId");
        }
    }
}
