using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260604000000_RemoveLabWorkTypeBranchIdFromSnapshot")]
    public partial class RemoveLabWorkTypeBranchIdFromSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Empty migration — no schema changes.
            // Purpose: Force EF Core to regenerate the model snapshot based on current entity definitions.
            // This removes the erroneous BranchId from LabWorkType snapshot.
            // The LabWorkType entity class has NEVER had a BranchId property,
            // but the EF Core model snapshot erroneously included one, causing:
            // Npgsql.PostgresException 42703: column l.BranchId does not exist
            // This broke /api/patient-journey/today, /api/lab-orders, and related endpoints.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty migration — no schema changes to undo.
        }
    }
}
