using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds the core messaging system tables: Conversations, ConversationParticipants, Messages, MessageReads.
/// Converted to idempotent raw SQL so it can be re-run safely if objects already exist
/// (e.g., created by Program.cs HOTFIX blocks before MigrateAsync).
/// EXPERIMENTAL TEST: [DbContext] attribute added directly here (no Designer.cs) to verify
/// whether EF Core's migration discovery only needs this attribute, not a full snapshot.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260430000000_AddMessagingSystem")]
public partial class AddMessagingSystem : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- Conversations table
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Conversations') THEN
        CREATE TABLE ""Conversations"" (
            ""Id""              uuid                     NOT NULL,
            ""Title""           character varying(200)   NOT NULL,
            ""IsGroup""         boolean                  NOT NULL,
            ""CreatedBy""       uuid                     NULL,
            ""LastMessageAt""   timestamp with time zone NULL,
            ""LastMessagePreview"" character varying(500) NULL,
            ""CreatedAt""       timestamp with time zone NOT NULL,
            ""UpdatedAt""       timestamp with time zone NOT NULL,
            ""IsActive""        boolean                  NOT NULL,
            CONSTRAINT ""PK_Conversations"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- FK: Conversations → Users (CreatedBy)
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Users_CreatedBy') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""Conversations""
                ADD CONSTRAINT ""FK_Conversations_Users_CreatedBy""
                FOREIGN KEY (""CreatedBy"") REFERENCES ""Users""(""Id"") ON DELETE SET NULL;
        END IF;
    END IF;

    -- Index on LastMessageAt
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_LastMessageAt') THEN
        CREATE INDEX ""IX_Conversations_LastMessageAt"" ON ""Conversations"" (""LastMessageAt"");
    END IF;

    -- ConversationParticipants table
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ConversationParticipants') THEN
        CREATE TABLE ""ConversationParticipants"" (
            ""Id""              uuid                     NOT NULL,
            ""ConversationId""  uuid                     NOT NULL,
            ""UserId""          uuid                     NOT NULL,
            ""IsAdmin""         boolean                  NOT NULL,
            ""LastReadAt""      timestamp with time zone NULL,
            ""IsMuted""         boolean                  NOT NULL,
            ""CreatedAt""       timestamp with time zone NOT NULL,
            ""UpdatedAt""       timestamp with time zone NOT NULL,
            ""IsActive""        boolean                  NOT NULL,
            CONSTRAINT ""PK_ConversationParticipants"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- FK: ConversationParticipants → Conversations
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Conversations_ConversationId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
            ALTER TABLE ""ConversationParticipants""
                ADD CONSTRAINT ""FK_ConversationParticipants_Conversations_ConversationId""
                FOREIGN KEY (""ConversationId"") REFERENCES ""Conversations""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;

    -- FK: ConversationParticipants → Users
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Users_UserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""ConversationParticipants""
                ADD CONSTRAINT ""FK_ConversationParticipants_Users_UserId""
                FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;

    -- Unique index on ConversationId + UserId
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'ConversationParticipants' AND indexname = 'IX_ConversationParticipants_ConversationId_UserId') THEN
        CREATE UNIQUE INDEX ""IX_ConversationParticipants_ConversationId_UserId""
            ON ""ConversationParticipants"" (""ConversationId"", ""UserId"");
    END IF;

    -- Messages table
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Messages') THEN
        CREATE TABLE ""Messages"" (
            ""Id""              uuid                     NOT NULL,
            ""ConversationId""  uuid                     NOT NULL,
            ""SenderId""        uuid                     NOT NULL,
            ""Content""         text                     NOT NULL,
            ""AttachmentUrl""   character varying(1000)  NULL,
            ""AttachmentName""  character varying(255)   NULL,
            ""AttachmentType""  character varying(50)    NULL,
            ""ReplyToId""       uuid                     NULL,
            ""IsSystemMessage"" boolean                  NOT NULL,
            ""CreatedAt""       timestamp with time zone NOT NULL,
            ""UpdatedAt""       timestamp with time zone NOT NULL,
            ""IsActive""        boolean                  NOT NULL,
            CONSTRAINT ""PK_Messages"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- FK: Messages → Conversations
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Conversations_ConversationId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
            ALTER TABLE ""Messages""
                ADD CONSTRAINT ""FK_Messages_Conversations_ConversationId""
                FOREIGN KEY (""ConversationId"") REFERENCES ""Conversations""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;

    -- FK: Messages → Users (SenderId)
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Users_SenderId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""Messages""
                ADD CONSTRAINT ""FK_Messages_Users_SenderId""
                FOREIGN KEY (""SenderId"") REFERENCES ""Users""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;

    -- FK: Messages → Messages (ReplyToId) — self-reference
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Messages_ReplyToId') THEN
        ALTER TABLE ""Messages""
            ADD CONSTRAINT ""FK_Messages_Messages_ReplyToId""
            FOREIGN KEY (""ReplyToId"") REFERENCES ""Messages""(""Id"") ON DELETE SET NULL;
    END IF;

    -- Index on ConversationId
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Messages' AND indexname = 'IX_Messages_ConversationId') THEN
        CREATE INDEX ""IX_Messages_ConversationId"" ON ""Messages"" (""ConversationId"");
    END IF;

    -- Index on CreatedAt
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Messages' AND indexname = 'IX_Messages_CreatedAt') THEN
        CREATE INDEX ""IX_Messages_CreatedAt"" ON ""Messages"" (""CreatedAt"");
    END IF;

    -- MessageReads table
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MessageReads') THEN
        CREATE TABLE ""MessageReads"" (
            ""Id""         uuid                     NOT NULL,
            ""MessageId""  uuid                     NOT NULL,
            ""UserId""     uuid                     NOT NULL,
            ""ReadAt""     timestamp with time zone NOT NULL,
            ""CreatedAt""  timestamp with time zone NOT NULL,
            ""UpdatedAt""  timestamp with time zone NOT NULL,
            ""IsActive""   boolean                  NOT NULL,
            CONSTRAINT ""PK_MessageReads"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- FK: MessageReads → Messages
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Messages_MessageId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages') THEN
            ALTER TABLE ""MessageReads""
                ADD CONSTRAINT ""FK_MessageReads_Messages_MessageId""
                FOREIGN KEY (""MessageId"") REFERENCES ""Messages""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;

    -- FK: MessageReads → Users
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Users_UserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""MessageReads""
                ADD CONSTRAINT ""FK_MessageReads_Users_UserId""
                FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;

    -- Unique index on MessageId + UserId
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'MessageReads' AND indexname = 'IX_MessageReads_MessageId_UserId') THEN
        CREATE UNIQUE INDEX ""IX_MessageReads_MessageId_UserId""
            ON ""MessageReads"" (""MessageId"", ""UserId"");
    END IF;
END $$;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP TABLE IF EXISTS ""MessageReads"";
            DROP TABLE IF EXISTS ""Messages"";
            DROP TABLE IF EXISTS ""ConversationParticipants"";
            DROP TABLE IF EXISTS ""Conversations"";
        ");
    }
}
