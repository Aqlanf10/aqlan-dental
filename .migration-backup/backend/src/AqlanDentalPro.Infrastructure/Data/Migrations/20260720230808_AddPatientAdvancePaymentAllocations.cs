using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds the auditable many-to-many bridge between patient payments and invoices.
/// Earlier migrations in this repository are hand-authored and the previous EF snapshot
/// predates several of them, so this migration deliberately contains only this table.
/// </summary>
public partial class AddPatientAdvancePaymentAllocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PaymentAllocations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "YER"),
                JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                table.ForeignKey(
                    name: "FK_PaymentAllocations_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentAllocations_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentAllocations_JournalEntries_JournalEntryId",
                    column: x => x.JournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentAllocations_Payments_PaymentId",
                    column: x => x.PaymentId,
                    principalTable: "Payments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_BranchId",
            table: "PaymentAllocations",
            column: "BranchId");
        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_InvoiceId",
            table: "PaymentAllocations",
            column: "InvoiceId");
        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_InvoiceId_IsActive",
            table: "PaymentAllocations",
            columns: new[] { "InvoiceId", "IsActive" });
        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_JournalEntryId",
            table: "PaymentAllocations",
            column: "JournalEntryId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_PaymentId",
            table: "PaymentAllocations",
            column: "PaymentId");
        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_PaymentId_IsActive",
            table: "PaymentAllocations",
            columns: new[] { "PaymentId", "IsActive" });
        migrationBuilder.CreateIndex(
            name: "IX_PaymentAllocations_PaymentId_InvoiceId",
            table: "PaymentAllocations",
            columns: new[] { "PaymentId", "InvoiceId" },
            unique: true,
            filter: "\"IsActive\" = TRUE");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PaymentAllocations");
}
