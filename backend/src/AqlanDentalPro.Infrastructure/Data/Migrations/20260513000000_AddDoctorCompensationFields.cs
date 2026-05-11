using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[Migration("20260513000000_AddDoctorCompensationFields")]
public partial class AddDoctorCompensationFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CompensationType",
            table: "Doctors",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "None");

        migrationBuilder.AddColumn<decimal>(
            name: "DefaultCommissionPercentage",
            table: "Doctors",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CompensationNotes",
            table: "Doctors",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CompensationType",
            table: "Doctors");

        migrationBuilder.DropColumn(
            name: "DefaultCommissionPercentage",
            table: "Doctors");

        migrationBuilder.DropColumn(
            name: "CompensationNotes",
            table: "Doctors");
    }
}
