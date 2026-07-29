using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

public partial class AddAccountingPeriods : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountingPeriods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Open"),
                ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_AccountingPeriods", x => x.Id);
                table.ForeignKey("FK_AccountingPeriods_Branches_BranchId", x => x.BranchId, "Branches", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_AccountingPeriods_Users_ClosedBy", x => x.ClosedBy, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_AccountingPeriods_BranchId_StartDate_EndDate", table: "AccountingPeriods", columns: new[] { "BranchId", "StartDate", "EndDate" });
        migrationBuilder.CreateIndex(name: "IX_AccountingPeriods_ClosedBy", table: "AccountingPeriods", column: "ClosedBy");
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION prevent_closed_period_posting() RETURNS trigger AS $$
            BEGIN
              IF EXISTS (SELECT 1 FROM "AccountingPeriods" p WHERE p."IsActive" = TRUE AND p."Status" = 'Closed' AND p."BranchId" = NEW."BranchId" AND NEW."EntryDate" BETWEEN p."StartDate" AND p."EndDate") THEN
                RAISE EXCEPTION 'Cannot post a journal entry to a closed accounting period';
              END IF;
              RETURN NEW;
            END; $$ LANGUAGE plpgsql;
            CREATE TRIGGER journal_entries_closed_period_guard BEFORE INSERT ON "JournalEntries" FOR EACH ROW EXECUTE FUNCTION prevent_closed_period_posting();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS journal_entries_closed_period_guard ON \"JournalEntries\"; DROP FUNCTION IF EXISTS prevent_closed_period_posting();");
        migrationBuilder.DropTable(name: "AccountingPeriods");
    }
}
