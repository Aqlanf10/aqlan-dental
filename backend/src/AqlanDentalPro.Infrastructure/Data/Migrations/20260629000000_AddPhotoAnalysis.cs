using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrthoCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImageFileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LandmarksJson = table.Column<string>(type: "text", nullable: true),
                    MeasurementsJson = table.Column<string>(type: "text", nullable: true),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoAnalyses_OrthoCases_OrthoCaseId",
                        column: x => x.OrthoCaseId,
                        principalTable: "OrthoCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoAnalyses_OrthoCaseId",
                table: "PhotoAnalyses",
                column: "OrthoCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoAnalyses");
        }
    }
}
