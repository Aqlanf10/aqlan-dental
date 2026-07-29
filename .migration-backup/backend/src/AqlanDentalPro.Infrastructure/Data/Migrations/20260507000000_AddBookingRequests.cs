using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[Migration("20260507000000_AddBookingRequests")]
public partial class AddBookingRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BookingRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PatientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                ServiceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PreferredDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PreferredTime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                StaffNotes = table.Column<string>(type: "text", nullable: true),
                ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ConvertedToAppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BookingRequests", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BookingRequests_Status",
            table: "BookingRequests",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_BookingRequests_CreatedAt",
            table: "BookingRequests",
            column: "CreatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BookingRequests");
    }
}
