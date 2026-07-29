using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260512000000_AddRadiographFileMetadata")]
public partial class AddRadiographFileMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FileName",
            table: "Radiographs",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "FileSize",
            table: "Radiographs",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MimeType",
            table: "Radiographs",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FileName",
            table: "Radiographs");

        migrationBuilder.DropColumn(
            name: "FileSize",
            table: "Radiographs");

        migrationBuilder.DropColumn(
            name: "MimeType",
            table: "Radiographs");
    }
}
