using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabStatusHistoryRemakeAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFreeRemake",
                table: "LabOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOrderId",
                table: "LabOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemakeCost",
                table: "LabOrders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemakeCount",
                table: "LabOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RemakeReason",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabOrderAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabOrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrderAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrderAttachments_LabOrderItems_LabOrderItemId",
                        column: x => x.LabOrderItemId,
                        principalTable: "LabOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabOrderAttachments_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrderAttachments_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabOrderStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrderStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrderStatusHistories_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrderStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_OriginalOrderId",
                table: "LabOrders",
                column: "OriginalOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderAttachments_LabOrderId",
                table: "LabOrderAttachments",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderAttachments_LabOrderItemId",
                table: "LabOrderAttachments",
                column: "LabOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderAttachments_UploadedBy",
                table: "LabOrderAttachments",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderStatusHistories_ChangedByUserId",
                table: "LabOrderStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderStatusHistories_LabOrderId",
                table: "LabOrderStatusHistories",
                column: "LabOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_LabOrders_OriginalOrderId",
                table: "LabOrders",
                column: "OriginalOrderId",
                principalTable: "LabOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_LabOrders_OriginalOrderId",
                table: "LabOrders");

            migrationBuilder.DropTable(
                name: "LabOrderAttachments");

            migrationBuilder.DropTable(
                name: "LabOrderStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_OriginalOrderId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "IsFreeRemake",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "OriginalOrderId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "RemakeCost",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "RemakeCount",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "RemakeReason",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "LabOrders");
        }
    }
}
