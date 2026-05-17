using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// CON-02 FIX: Adds a unique index on LabOrders.OrderNumber to prevent duplicate
/// order numbers at the database level. This is a safety net in addition to the
/// advisory lock used during order creation. The filter allows NULL OrderNumbers
/// for any legacy data that might not have an order number assigned.
///
/// Strategy: advisory lock (serializes generation) + unique constraint (safety net) + retry.
/// </summary>
public partial class AddLabOrderNumberUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // CON-02 FIX: Create unique filtered index on OrderNumber
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE indexname = 'IX_LabOrders_OrderNumber'
                ) THEN
                    CREATE UNIQUE INDEX ""IX_LabOrders_OrderNumber""
                    ON ""LabOrders"" (""OrderNumber"")
                    WHERE ""OrderNumber"" IS NOT NULL;
                END IF;
            END
            $$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE indexname = 'IX_LabOrders_OrderNumber'
                ) THEN
                    DROP INDEX ""IX_LabOrders_OrderNumber"";
                END IF;
            END
            $$;
        ");
    }
}
