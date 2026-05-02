using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

public partial class AddPatientPortalPasswordAuth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new columns to PatientAccounts
        migrationBuilder.AddColumn<string>(
            name: "Username",
            table: "PatientAccounts",
            maxLength: 50,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            table: "PatientAccounts",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PasswordSalt",
            table: "PatientAccounts",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "PatientAccounts",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "PortalAccountActive",
            table: "PatientAccounts",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LinkedUserId",
            table: "PatientAccounts",
            nullable: true);

        // Backfill Username from Patient's PatientNumber
        migrationBuilder.Sql(@"
            UPDATE ""PatientAccounts""
            SET ""Username"" = p.""PatientNumber"",
                ""PortalAccountActive"" = true,
                ""MustChangePassword"" = true
            FROM ""Patients"" p
            WHERE ""PatientAccounts"".""PatientId"" = p.""Id""
              AND (""PatientAccounts"".""Username"" = '' OR ""PatientAccounts"".""Username"" IS NULL)
        ");

        // Create index on Username (unique)
        migrationBuilder.CreateIndex(
            name: "IX_PatientAccounts_Username",
            table: "PatientAccounts",
            column: "Username",
            unique: true);

        // Create foreign key to Users for messaging integration
        migrationBuilder.CreateIndex(
            name: "IX_PatientAccounts_LinkedUserId",
            table: "PatientAccounts",
            column: "LinkedUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_PatientAccounts_Users_LinkedUserId",
            table: "PatientAccounts",
            column: "LinkedUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PatientAccounts_Users_LinkedUserId",
            table: "PatientAccounts");

        migrationBuilder.DropIndex(
            name: "IX_PatientAccounts_LinkedUserId",
            table: "PatientAccounts");

        migrationBuilder.DropIndex(
            name: "IX_PatientAccounts_Username",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "Username",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "PasswordHash",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "PasswordSalt",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "MustChangePassword",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "PortalAccountActive",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "LinkedUserId",
            table: "PatientAccounts");
    }
}
