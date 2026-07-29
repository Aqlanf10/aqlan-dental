using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// TD-020 Phase C1-f repair: this migration was stuck in an infinite retry loop because its
    /// original non-idempotent AddColumn calls (NormalizedPhone / NormalizedWhatsApp) threw
    /// "42701: column already exists" — the columns had already been added by Program.cs B-blocks
    /// before EF ran.  EF never recorded the migration in __EFMigrationsHistory, so it retried on
    /// every startup.
    ///
    /// All operations are now idempotent via raw SQL guards (IF NOT EXISTS / CREATE TABLE IF NOT
    /// EXISTS / CREATE INDEX IF NOT EXISTS / pg_constraint existence check).  EF will record this
    /// migration automatically after it completes successfully.
    ///
    /// Down() is intentionally a no-op: the tables created here (Conversations, Messages,
    /// PatientAccounts, etc.) contain active production data.  Dropping them would be catastrophic.
    /// Manual steps with a data backup are required if rollback is ever truly needed.
    ///
    /// The original migration did NOT include phone/WhatsApp data backfill.  Backfill (B4/B5
    /// equivalents) remains in Program.cs B-blocks for now and will be addressed in Phase C1-g
    /// after this migration loop is resolved.
    ///
    /// Raw WhatsApp unique index (IX_Patients_WhatsApp) is intentionally NOT created: production
    /// has duplicate raw WhatsApp value 0711752823.  That index must not be created until staff
    /// correct the duplicate via the patient edit UI.
    /// </remarks>
    public partial class AddPhoneNormalizationAndArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =====================================================================
            // A. Patients.NormalizedPhone — idempotent ADD COLUMN
            // =====================================================================
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (
                           SELECT 1 FROM information_schema.columns
                           WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone'
                       ) THEN
                        ALTER TABLE "Patients" ADD COLUMN "NormalizedPhone" character varying(20) NULL;
                    END IF;
                END $$;
                """);

            // =====================================================================
            // B. Patients.NormalizedWhatsApp — idempotent ADD COLUMN
            // =====================================================================
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (
                           SELECT 1 FROM information_schema.columns
                           WHERE table_name = 'Patients' AND column_name = 'NormalizedWhatsApp'
                       ) THEN
                        ALTER TABLE "Patients" ADD COLUMN "NormalizedWhatsApp" character varying(20) NULL;
                    END IF;
                END $$;
                """);

            // =====================================================================
            // C. Tables — CREATE TABLE IF NOT EXISTS
            //    If a table already exists (created by Program.cs B-blocks in production)
            //    the statement is a safe no-op.
            // =====================================================================

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "Conversations" (
                    "Id" uuid NOT NULL,
                    "Title" character varying(200) NOT NULL,
                    "IsGroup" boolean NOT NULL,
                    "CreatedBy" uuid NULL,
                    "LastMessageAt" timestamp with time zone NULL,
                    "LastMessagePreview" character varying(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_Conversations" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "GeneralTreatmentPlanItems" (
                    "Id" uuid NOT NULL,
                    "PatientId" uuid NOT NULL,
                    "ToothNumber" text NULL,
                    "Treatment" text NOT NULL,
                    "Priority" text NOT NULL,
                    "Status" text NOT NULL,
                    "EstimatedCost" numeric NULL,
                    "Notes" text NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    "DoctorId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_GeneralTreatmentPlanItems" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "PatientAccounts" (
                    "Id" uuid NOT NULL,
                    "PatientId" uuid NOT NULL,
                    "PhoneNumber" text NOT NULL,
                    "VerificationCode" text NULL,
                    "VerificationCodeExpiry" timestamp with time zone NULL,
                    "IsVerified" boolean NOT NULL,
                    "LastLogin" timestamp with time zone NULL,
                    "DeviceToken" text NULL,
                    "RefreshToken" text NULL,
                    "RefreshTokenExpiry" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_PatientAccounts" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "PerioRecords" (
                    "Id" uuid NOT NULL,
                    "PatientId" uuid NOT NULL,
                    "ToothNumber" integer NOT NULL,
                    "ProbingDepth" numeric NOT NULL,
                    "ClinicalAttachment" numeric NOT NULL,
                    "BleedingOnProbing" boolean NOT NULL,
                    "PlaqueIndex" integer NOT NULL,
                    "GingivalIndex" integer NOT NULL,
                    "Furcation" integer NOT NULL,
                    "Mobility" integer NOT NULL,
                    "Notes" text NULL,
                    "DoctorId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_PerioRecords" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "WhatsAppTemplates" (
                    "Id" uuid NOT NULL,
                    "TemplateKey" text NOT NULL,
                    "NameAr" text NOT NULL,
                    "ContentTemplate" text NOT NULL,
                    "IsActive" boolean NOT NULL,
                    "Category" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_WhatsAppTemplates" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "ConversationParticipants" (
                    "Id" uuid NOT NULL,
                    "ConversationId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "IsAdmin" boolean NOT NULL,
                    "LastReadAt" timestamp with time zone NULL,
                    "IsMuted" boolean NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_ConversationParticipants" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "Messages" (
                    "Id" uuid NOT NULL,
                    "ConversationId" uuid NOT NULL,
                    "SenderId" uuid NOT NULL,
                    "Content" text NOT NULL,
                    "AttachmentUrl" character varying(1000) NULL,
                    "AttachmentName" character varying(255) NULL,
                    "AttachmentType" character varying(50) NULL,
                    "ReplyToId" uuid NULL,
                    "IsSystemMessage" boolean NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_Messages" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "WhatsAppMessages" (
                    "Id" uuid NOT NULL,
                    "PatientId" uuid NOT NULL,
                    "PhoneNumber" text NOT NULL,
                    "TemplateType" text NOT NULL,
                    "MessageContent" text NOT NULL,
                    "Status" text NOT NULL,
                    "ExternalId" text NULL,
                    "ErrorMessage" text NULL,
                    "RetryCount" integer NOT NULL,
                    "SentAt" timestamp with time zone NULL,
                    "DeliveredAt" timestamp with time zone NULL,
                    "RelatedEntityId" uuid NULL,
                    "RelatedEntityType" text NULL,
                    "WhatsAppTemplateId" uuid NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_WhatsAppMessages" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "MessageReads" (
                    "Id" uuid NOT NULL,
                    "MessageId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "ReadAt" timestamp with time zone NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL,
                    CONSTRAINT "PK_MessageReads" PRIMARY KEY ("Id")
                );
                """);

            // =====================================================================
            // D. Foreign keys — idempotent ADD CONSTRAINT
            //    CREATE TABLE IF NOT EXISTS is a no-op when the table exists, so
            //    inline CONSTRAINT clauses are ignored for existing tables.
            //    Each FK is added separately only if absent.
            // =====================================================================

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Users_CreatedBy') THEN
                        ALTER TABLE "Conversations"
                            ADD CONSTRAINT "FK_Conversations_Users_CreatedBy"
                            FOREIGN KEY ("CreatedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'GeneralTreatmentPlanItems')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Doctors')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_GeneralTreatmentPlanItems_Doctors_DoctorId') THEN
                        ALTER TABLE "GeneralTreatmentPlanItems"
                            ADD CONSTRAINT "FK_GeneralTreatmentPlanItems_Doctors_DoctorId"
                            FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id");
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'GeneralTreatmentPlanItems')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_GeneralTreatmentPlanItems_Patients_PatientId') THEN
                        ALTER TABLE "GeneralTreatmentPlanItems"
                            ADD CONSTRAINT "FK_GeneralTreatmentPlanItems_Patients_PatientId"
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PatientAccounts')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PatientAccounts_Patients_PatientId') THEN
                        ALTER TABLE "PatientAccounts"
                            ADD CONSTRAINT "FK_PatientAccounts_Patients_PatientId"
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PerioRecords')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Doctors')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PerioRecords_Doctors_DoctorId') THEN
                        ALTER TABLE "PerioRecords"
                            ADD CONSTRAINT "FK_PerioRecords_Doctors_DoctorId"
                            FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id");
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PerioRecords')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PerioRecords_Patients_PatientId') THEN
                        ALTER TABLE "PerioRecords"
                            ADD CONSTRAINT "FK_PerioRecords_Patients_PatientId"
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ConversationParticipants')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Conversations_ConversationId') THEN
                        ALTER TABLE "ConversationParticipants"
                            ADD CONSTRAINT "FK_ConversationParticipants_Conversations_ConversationId"
                            FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ConversationParticipants')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Users_UserId') THEN
                        ALTER TABLE "ConversationParticipants"
                            ADD CONSTRAINT "FK_ConversationParticipants_Users_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Conversations_ConversationId') THEN
                        ALTER TABLE "Messages"
                            ADD CONSTRAINT "FK_Messages_Conversations_ConversationId"
                            FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Messages_ReplyToId') THEN
                        ALTER TABLE "Messages"
                            ADD CONSTRAINT "FK_Messages_Messages_ReplyToId"
                            FOREIGN KEY ("ReplyToId") REFERENCES "Messages"("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Users_SenderId') THEN
                        ALTER TABLE "Messages"
                            ADD CONSTRAINT "FK_Messages_Users_SenderId"
                            FOREIGN KEY ("SenderId") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WhatsAppMessages')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_WhatsAppMessages_Patients_PatientId') THEN
                        ALTER TABLE "WhatsAppMessages"
                            ADD CONSTRAINT "FK_WhatsAppMessages_Patients_PatientId"
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WhatsAppMessages')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WhatsAppTemplates')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_WhatsAppMessages_WhatsAppTemplates_WhatsAppTemplateId') THEN
                        ALTER TABLE "WhatsAppMessages"
                            ADD CONSTRAINT "FK_WhatsAppMessages_WhatsAppTemplates_WhatsAppTemplateId"
                            FOREIGN KEY ("WhatsAppTemplateId") REFERENCES "WhatsAppTemplates"("Id");
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageReads')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Messages_MessageId') THEN
                        ALTER TABLE "MessageReads"
                            ADD CONSTRAINT "FK_MessageReads_Messages_MessageId"
                            FOREIGN KEY ("MessageId") REFERENCES "Messages"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageReads')
                       AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')
                       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Users_UserId') THEN
                        ALTER TABLE "MessageReads"
                            ADD CONSTRAINT "FK_MessageReads_Users_UserId"
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
                """);

            // =====================================================================
            // E. Indexes — CREATE [UNIQUE] INDEX IF NOT EXISTS
            // =====================================================================

            // Normalized patient phone indexes (the core repair)
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedPhone"
                    ON "Patients" ("NormalizedPhone")
                    WHERE "NormalizedPhone" IS NOT NULL AND "NormalizedPhone" != '';
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedWhatsApp"
                    ON "Patients" ("NormalizedWhatsApp")
                    WHERE "NormalizedWhatsApp" IS NOT NULL AND "NormalizedWhatsApp" != '';
                """);

            // ConversationParticipants indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ConversationParticipants') THEN
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConversationParticipants_ConversationId_UserId"
                            ON "ConversationParticipants" ("ConversationId", "UserId");
                        CREATE INDEX IF NOT EXISTS "IX_ConversationParticipants_UserId"
                            ON "ConversationParticipants" ("UserId");
                    END IF;
                END $$;
                """);

            // Conversations indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
                        CREATE INDEX IF NOT EXISTS "IX_Conversations_CreatedBy"
                            ON "Conversations" ("CreatedBy");
                        CREATE INDEX IF NOT EXISTS "IX_Conversations_LastMessageAt"
                            ON "Conversations" ("LastMessageAt");
                    END IF;
                END $$;
                """);

            // GeneralTreatmentPlanItems indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'GeneralTreatmentPlanItems') THEN
                        CREATE INDEX IF NOT EXISTS "IX_GeneralTreatmentPlanItems_DoctorId"
                            ON "GeneralTreatmentPlanItems" ("DoctorId");
                        CREATE INDEX IF NOT EXISTS "IX_GeneralTreatmentPlanItems_PatientId"
                            ON "GeneralTreatmentPlanItems" ("PatientId");
                    END IF;
                END $$;
                """);

            // MessageReads indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageReads') THEN
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MessageReads_MessageId_UserId"
                            ON "MessageReads" ("MessageId", "UserId");
                        CREATE INDEX IF NOT EXISTS "IX_MessageReads_UserId"
                            ON "MessageReads" ("UserId");
                    END IF;
                END $$;
                """);

            // Messages indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages') THEN
                        CREATE INDEX IF NOT EXISTS "IX_Messages_ConversationId"
                            ON "Messages" ("ConversationId");
                        CREATE INDEX IF NOT EXISTS "IX_Messages_CreatedAt"
                            ON "Messages" ("CreatedAt");
                        CREATE INDEX IF NOT EXISTS "IX_Messages_ReplyToId"
                            ON "Messages" ("ReplyToId");
                        CREATE INDEX IF NOT EXISTS "IX_Messages_SenderId"
                            ON "Messages" ("SenderId");
                    END IF;
                END $$;
                """);

            // PatientAccounts index
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PatientAccounts') THEN
                        CREATE INDEX IF NOT EXISTS "IX_PatientAccounts_PatientId"
                            ON "PatientAccounts" ("PatientId");
                    END IF;
                END $$;
                """);

            // PerioRecords indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PerioRecords') THEN
                        CREATE INDEX IF NOT EXISTS "IX_PerioRecords_DoctorId"
                            ON "PerioRecords" ("DoctorId");
                        CREATE INDEX IF NOT EXISTS "IX_PerioRecords_PatientId"
                            ON "PerioRecords" ("PatientId");
                    END IF;
                END $$;
                """);

            // WhatsAppMessages indexes
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WhatsAppMessages') THEN
                        CREATE INDEX IF NOT EXISTS "IX_WhatsAppMessages_PatientId"
                            ON "WhatsAppMessages" ("PatientId");
                        CREATE INDEX IF NOT EXISTS "IX_WhatsAppMessages_WhatsAppTemplateId"
                            ON "WhatsAppMessages" ("WhatsAppTemplateId");
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op.
            //
            // The original Down() dropped Conversations, Messages, PatientAccounts,
            // PerioRecords, WhatsAppMessages, MessageReads, ConversationParticipants,
            // GeneralTreatmentPlanItems, WhatsAppTemplates, and the NormalizedPhone /
            // NormalizedWhatsApp columns.  These tables contain active production data.
            // Executing that rollback would cause catastrophic data loss.
            //
            // If rollback is ever truly needed, it must be performed manually with
            // a full database backup and explicit team sign-off.
        }
    }
}
