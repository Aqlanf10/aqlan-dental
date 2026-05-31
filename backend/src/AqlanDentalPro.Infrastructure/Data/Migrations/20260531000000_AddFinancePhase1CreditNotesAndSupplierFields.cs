using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Finance Phase 1: Add CreditNotes table, Supplier.Type and Supplier.Balance columns.
/// The CreditNote entity and Supplier.Type/Balance were added to the domain model
/// but no migration was created for them, causing 500 errors at runtime.
/// </summary>
public partial class AddFinancePhase1CreditNotesAndSupplierFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. Add Supplier.Type column (nvarchar(30), default 'MedicalVendor') ──
        migrationBuilder.AddColumn<string>(
            name: "Type",
            table: "Suppliers",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "MedicalVendor");

        // ── 2. Add Supplier.Balance column (decimal(18,2), default 0) ──
        migrationBuilder.AddColumn<decimal>(
            name: "Balance",
            table: "Suppliers",
            type: "numeric(18,2)",
            nullable: false,
            defaultValue: 0m);

        // ── 3. Create CreditNotes table ──
        migrationBuilder.CreateTable(
            name: "CreditNotes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                RefundPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CreditNotes", x => x.Id);
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
            });

        migrationBuilder.CreateIndex(
            name: "IX_CreditNotes_InvoiceId",
            table: "CreditNotes",
            column: "InvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_CreditNotes_PatientId",
            table: "CreditNotes",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_CreditNotes_Status",
            table: "CreditNotes",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_CreditNotes_BranchId",
            table: "CreditNotes",
            column: "BranchId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CreditNotes");

        migrationBuilder.DropColumn(name: "Balance", table: "Suppliers");
        migrationBuilder.DropColumn(name: "Type", table: "Suppliers");
    }
}
