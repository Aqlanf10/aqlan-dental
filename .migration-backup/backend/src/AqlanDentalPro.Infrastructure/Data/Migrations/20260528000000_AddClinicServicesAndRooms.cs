using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Add ClinicServices and ClinicRooms tables for configurable services catalog and room management.
/// Additive only — no drops, no destructive changes.
/// </summary>
public partial class AddClinicServicesAndRooms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ClinicServices table ─────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ClinicServices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ArabicName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Other"),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DefaultDurationMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                DefaultPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                RequiresDoctor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                RequiresConsultationFee = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ShowInBooking = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ShowInReception = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ShowInTreatmentPlan = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClinicServices", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClinicServices_Code",
            table: "ClinicServices",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ClinicServices_Category",
            table: "ClinicServices",
            column: "Category");

        // ── ClinicRooms table ────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ClinicRooms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ArabicName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                EnglishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RoomType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Treatment"),
                SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClinicRooms", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClinicRooms_Code",
            table: "ClinicRooms",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ClinicRooms_RoomType",
            table: "ClinicRooms",
            column: "RoomType");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClinicRooms");
        migrationBuilder.DropTable(name: "ClinicServices");
    }
}
