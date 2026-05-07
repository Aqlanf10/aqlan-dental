using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// إضافة أعمدة الحذف الناعم (DeletedAt, DeletedBy) لجداول المراسلة
/// Migration 20260501020000
/// </summary>
public partial class AddSoftDeleteToMessagingTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add DeletedAt and DeletedBy to Conversations (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "Conversations");

        // Add DeletedAt and DeletedBy to ConversationParticipants (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "ConversationParticipants");

        // Add DeletedAt and DeletedBy to Messages (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "Messages");

        // Add DeletedAt and DeletedBy to MessageReads (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "MessageReads");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeletedAt", table: "MessageReads");
        migrationBuilder.DropColumn(name: "DeletedBy", table: "MessageReads");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Messages");
        migrationBuilder.DropColumn(name: "DeletedBy", table: "Messages");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "ConversationParticipants");
        migrationBuilder.DropColumn(name: "DeletedBy", table: "ConversationParticipants");
        migrationBuilder.DropColumn(name: "DeletedAt", table: "Conversations");
        migrationBuilder.DropColumn(name: "DeletedBy", table: "Conversations");
    }

    private static void AddSoftDeleteColumns(MigrationBuilder migrationBuilder, string tableName)
    {
        // Use raw SQL with IF NOT EXISTS to be idempotent
        migrationBuilder.Sql($@"
            DO $soft_delete_{tableName}$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{tableName}' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE ""{tableName}"" ADD COLUMN ""DeletedAt"" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{tableName}' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE ""{tableName}"" ADD COLUMN ""DeletedBy"" uuid NULL;
                END IF;
            END $soft_delete_{tableName}$;
        ");
    }
}
