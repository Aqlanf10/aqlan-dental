using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// MULTI-CURRENCY: Adds nullable Currency (ISO 4217) to Payments so patients
    /// can pay in SAR (Saudi Riyal) or USD (US Dollar) in addition to YER.
    /// Null = YER (legacy default). Treasury/dashboard YER sums filter YER-only.
    /// </summary>
    public partial class AddPaymentCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The guarded startup repair may add this column before EF reaches
            // the migration. Keep the migration safe for that production path.
            migrationBuilder.Sql("""
                ALTER TABLE "Payments"
                    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");
        }
    }
}
