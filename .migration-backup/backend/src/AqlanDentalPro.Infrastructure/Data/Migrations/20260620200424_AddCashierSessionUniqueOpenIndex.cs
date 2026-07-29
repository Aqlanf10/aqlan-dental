using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierSessionUniqueOpenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CashierSessions_OneOpenPerCashier",
                table: "CashierSessions",
                column: "CashierId",
                unique: true,
                filter: "\"Status\" = 0 AND \"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashierSessions_OneOpenPerCashier",
                table: "CashierSessions");
        }
    }
}
