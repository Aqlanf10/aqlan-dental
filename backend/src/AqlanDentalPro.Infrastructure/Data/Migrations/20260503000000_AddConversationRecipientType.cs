using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[Migration("20260503000000_AddConversationRecipientType")]
public partial class AddConversationRecipientType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecipientType",
            table: "Conversations",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RecipientUserId",
            table: "Conversations",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_RecipientType",
            table: "Conversations",
            column: "RecipientType");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Conversations_RecipientType",
            table: "Conversations");

        migrationBuilder.DropColumn(
            name: "RecipientType",
            table: "Conversations");

        migrationBuilder.DropColumn(
            name: "RecipientUserId",
            table: "Conversations");
    }
}
