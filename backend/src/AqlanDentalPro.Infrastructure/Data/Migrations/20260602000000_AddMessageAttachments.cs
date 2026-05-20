using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds MessageAttachments table for multi-attachment support on messages.
/// Backward compatible: existing single-attachment fields on Message are retained.
/// </summary>
public partial class AddMessageAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MessageAttachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageAttachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_MessageAttachments_Messages_MessageId",
                    column: x => x.MessageId,
                    principalTable: "Messages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MessageAttachments_MessageId",
            table: "MessageAttachments",
            column: "MessageId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MessageAttachments");
    }
}
