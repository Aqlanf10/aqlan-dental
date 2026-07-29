using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLabPayablesAndFinanceLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. CreateTable LabPayables
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LabPayables"" (
    ""Id"" uuid NOT NULL,
    ""LabOrderId"" uuid NULL,
    ""LabId"" uuid NULL,
    ""Amount"" numeric NOT NULL,
    ""PaidAmount"" numeric NOT NULL,
    ""DueDate"" timestamptz NULL,
    ""Status"" character varying(20) NULL,
    ""Notes"" character varying(1000) NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_LabPayables"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_LabPayables_LabOrders_LabOrderId"" FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_LabPayables_Labs_LabId"" FOREIGN KEY (""LabId"") REFERENCES ""Labs"" (""Id"") ON DELETE CASCADE
);
");

        // 2. Create indexes
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabPayables_LabId"" ON ""LabPayables"" (""LabId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabPayables_LabOrderId"" ON ""LabPayables"" (""LabOrderId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabPayables_Status"" ON ""LabPayables"" (""Status"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LabPayables");
    }
}
