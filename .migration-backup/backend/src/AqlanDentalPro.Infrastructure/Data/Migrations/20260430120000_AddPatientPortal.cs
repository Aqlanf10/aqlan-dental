using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260430120000_AddPatientPortal")]
public partial class AddPatientPortal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PatientAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PatientId = table.Column<Guid>(nullable: false),
                PhoneNumber = table.Column<string>(maxLength: 20, nullable: false),
                VerificationCode = table.Column<string>(maxLength: 10, nullable: true),
                VerificationCodeExpiry = table.Column<DateTime>(nullable: true),
                IsVerified = table.Column<bool>(nullable: false, defaultValue: false),
                LastLogin = table.Column<DateTime>(nullable: true),
                DeviceToken = table.Column<string>(maxLength: 500, nullable: true),
                RefreshToken = table.Column<string>(maxLength: 256, nullable: true),
                RefreshTokenExpiry = table.Column<DateTime>(nullable: true),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PatientAccounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_PatientAccounts_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PatientAccounts_PatientId",
            table: "PatientAccounts",
            column: "PatientId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PatientAccounts_PhoneNumber",
            table: "PatientAccounts",
            column: "PhoneNumber",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PatientAccounts");
    }
}
