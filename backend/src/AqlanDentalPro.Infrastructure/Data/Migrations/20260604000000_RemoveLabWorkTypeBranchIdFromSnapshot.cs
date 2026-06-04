using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Empty migration that forces EF Core to regenerate the model snapshot based on
/// current entity definitions, removing the erroneous BranchId property from the
/// LabWorkType snapshot entry.
///
/// Root cause: A stale model snapshot included LabWorkType.BranchId, which does not
/// exist on the entity class, the Fluent API configuration, or the production database.
/// EF Core was generating SELECT queries with "l.BranchId", causing:
///   Npgsql.PostgresException: 42703: column l.BranchId does not exist
///
/// Fix: This empty migration advances the migration chain so that EF Core's model
/// is derived from the current AppDbContextModelSnapshot, which correctly reflects
/// the LabWorkType entity without BranchId. No schema changes are made.
/// </summary>
public partial class RemoveLabWorkTypeBranchIdFromSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty — no schema changes required.
        // LabWorkType.BranchId never existed in the database; this migration
        // only corrects the EF Core model snapshot so queries no longer
        // attempt to SELECT a non-existent column.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty — nothing to revert.
    }
}
