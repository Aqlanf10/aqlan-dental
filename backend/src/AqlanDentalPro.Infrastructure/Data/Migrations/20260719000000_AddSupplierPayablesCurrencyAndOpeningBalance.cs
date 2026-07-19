using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Makes supplier payables safe for Yemen's multi-currency operation. Historical
/// rows are backfilled as YER at rate 1, while every new payable and disbursement
/// retains its source currency and the rate used at posting time.
/// </summary>
public partial class AddSupplierPayablesCurrencyAndOpeningBalance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Currency", table: "SupplierBills", type: "character varying(3)", maxLength: 3,
            nullable: false, defaultValue: "YER");
        migrationBuilder.AddColumn<decimal>(
            name: "ExchangeRateToYer", table: "SupplierBills", type: "numeric(18,6)", precision: 18, scale: 6,
            nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<string>(
            name: "ExchangeRateSource", table: "SupplierBills", type: "character varying(30)", maxLength: 30,
            nullable: false, defaultValue: "same_currency");
        migrationBuilder.AddColumn<bool>(
            name: "IsOpeningBalance", table: "SupplierBills", type: "boolean",
            nullable: false, defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Currency", table: "SupplierBillPayments", type: "character varying(3)", maxLength: 3,
            nullable: false, defaultValue: "YER");
        migrationBuilder.AddColumn<decimal>(
            name: "ExchangeRateToYer", table: "SupplierBillPayments", type: "numeric(18,6)", precision: 18, scale: 6,
            nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<string>(
            name: "ExchangeRateSource", table: "SupplierBillPayments", type: "character varying(30)", maxLength: 30,
            nullable: false, defaultValue: "same_currency");

        migrationBuilder.AddColumn<string>(
            name: "Currency", table: "JournalEntries", type: "character varying(3)", maxLength: 3,
            nullable: false, defaultValue: "YER");
        migrationBuilder.AddColumn<decimal>(
            name: "ExchangeRateToYer", table: "JournalEntries", type: "numeric(18,6)", precision: 18, scale: 6,
            nullable: false, defaultValue: 1m);

        migrationBuilder.CreateIndex(
            name: "IX_SupplierBills_SupplierId_Currency", table: "SupplierBills",
            columns: new[] { "SupplierId", "Currency" });
        migrationBuilder.CreateIndex(
            name: "IX_SupplierBills_SupplierId_IsOpeningBalance", table: "SupplierBills",
            columns: new[] { "SupplierId", "IsOpeningBalance" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SupplierBills_SupplierId_Currency", table: "SupplierBills");
        migrationBuilder.DropIndex(name: "IX_SupplierBills_SupplierId_IsOpeningBalance", table: "SupplierBills");

        migrationBuilder.DropColumn(name: "Currency", table: "SupplierBills");
        migrationBuilder.DropColumn(name: "ExchangeRateToYer", table: "SupplierBills");
        migrationBuilder.DropColumn(name: "ExchangeRateSource", table: "SupplierBills");
        migrationBuilder.DropColumn(name: "IsOpeningBalance", table: "SupplierBills");
        migrationBuilder.DropColumn(name: "Currency", table: "SupplierBillPayments");
        migrationBuilder.DropColumn(name: "ExchangeRateToYer", table: "SupplierBillPayments");
        migrationBuilder.DropColumn(name: "ExchangeRateSource", table: "SupplierBillPayments");
        migrationBuilder.DropColumn(name: "Currency", table: "JournalEntries");
        migrationBuilder.DropColumn(name: "ExchangeRateToYer", table: "JournalEntries");
    }
}
