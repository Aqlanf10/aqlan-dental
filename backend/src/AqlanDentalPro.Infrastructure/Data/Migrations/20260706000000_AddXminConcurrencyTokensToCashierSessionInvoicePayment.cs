using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// DB-02: Add xmin concurrency tokens to CashierSession, Invoice, Payment.
    /// Mirrors the FIN-06 Treasury fix (migration 20260620200023_AddTreasuryXminConcurrencyToken).
    /// Up/Down are intentionally empty: xmin is a PostgreSQL system column that exists on every
    /// table automatically. UseXminAsConcurrencyToken only tells EF Core to include the xmin value
    /// in UPDATE WHERE clauses — no schema change is required. The only effect of this migration
    /// is to advance the ModelSnapshot so EF's runtime model includes the concurrency token.
    /// </summary>
    public partial class AddXminConcurrencyTokensToCashierSessionInvoicePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
