using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds InvoiceId FK to Payments table, linking payments to invoices.
/// Enables the Invoice → Payment connection for the Draft → Issued → Paid lifecycle.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260601000000_AddInvoicePaymentLink")]
public partial class AddInvoicePaymentLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add nullable InvoiceId column to Payments
        migrationBuilder.AddColumn<Guid>(
            name: "InvoiceId",
            table: "Payments",
            type: "uuid",
            nullable: true);

        // Create index on InvoiceId for fast lookup
        migrationBuilder.CreateIndex(
            name: "IX_Payments_InvoiceId",
            table: "Payments",
            column: "InvoiceId");

        // Add FK from Payments.InvoiceId → Invoices.Id with SetNull on delete
        migrationBuilder.AddForeignKey(
            name: "FK_Payments_Invoices_InvoiceId",
            table: "Payments",
            column: "InvoiceId",
            principalTable: "Invoices",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Payments_Invoices_InvoiceId",
            table: "Payments");

        migrationBuilder.DropIndex(
            name: "IX_Payments_InvoiceId",
            table: "Payments");

        migrationBuilder.DropColumn(
            name: "InvoiceId",
            table: "Payments");
    }
}
