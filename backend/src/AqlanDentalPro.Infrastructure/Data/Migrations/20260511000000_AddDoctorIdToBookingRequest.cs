using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[Migration("20260511000000_AddDoctorIdToBookingRequest")]
public partial class AddDoctorIdToBookingRequest : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DoctorId",
            table: "BookingRequests",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BookingRequests_DoctorId",
            table: "BookingRequests",
            column: "DoctorId");

        migrationBuilder.AddForeignKey(
            name: "FK_BookingRequests_Doctors_DoctorId",
            table: "BookingRequests",
            column: "DoctorId",
            principalTable: "Doctors",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BookingRequests_Doctors_DoctorId",
            table: "BookingRequests");

        migrationBuilder.DropIndex(
            name: "IX_BookingRequests_DoctorId",
            table: "BookingRequests");

        migrationBuilder.DropColumn(
            name: "DoctorId",
            table: "BookingRequests");
    }
}
