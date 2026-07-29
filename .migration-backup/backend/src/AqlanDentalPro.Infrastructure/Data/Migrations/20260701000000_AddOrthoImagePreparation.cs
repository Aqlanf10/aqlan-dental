using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrthoImagePreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrthoImagePreparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrthoClinicalPhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CropX = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    CropY = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    CropWidth = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false, defaultValue: 1m),
                    CropHeight = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false, defaultValue: 1m),
                    Zoom = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 1m),
                    RotationDegrees = table.Column<int>(type: "integer", nullable: false),
                    Brightness = table.Column<int>(type: "integer", nullable: false),
                    Contrast = table.Column<int>(type: "integer", nullable: false),
                    FlipHorizontal = table.Column<bool>(type: "boolean", nullable: false),
                    FlipVertical = table.Column<bool>(type: "boolean", nullable: false),
                    AspectRatio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Original"),
                    Preset = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "PreparedForReport"),
                    PreparedImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrthoImagePreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrthoImagePreparations_OrthoClinicalPhotos_OrthoClinicalPho~",
                        column: x => x.OrthoClinicalPhotoId,
                        principalTable: "OrthoClinicalPhotos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrthoImagePreparations_OrthoClinicalPhotoId",
                table: "OrthoImagePreparations",
                column: "OrthoClinicalPhotoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrthoImagePreparations_Status",
                table: "OrthoImagePreparations",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrthoImagePreparations");
        }
    }
}
