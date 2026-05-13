using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// TD-020 Phase C1-f — Repair patient phone normalization startup loops.
///
/// Problem A — stuck migration retry loop (20260430221054):
///   Migration 20260430221054_AddPhoneNormalizationAndArchive contains non-idempotent
///   AddColumn calls for Patients.NormalizedPhone and Patients.NormalizedWhatsApp.
///   Because Program.cs B-blocks added these columns before EF ran its migrations,
///   every startup attempt throws "42701: column already exists", the migration rolls
///   back, and EF never records it in __EFMigrationsHistory.  The same loop repeats
///   on every deploy/restart.
///
/// Problem B — NormalizedWhatsApp backfill conflict (B5 block):
///   B5 (Program.cs) attempts to backfill NormalizedWhatsApp for rows where it is NULL.
///   At least two patients share the raw WhatsApp number 0711752823 which normalizes
///   to 967711752823.  One patient already holds NormalizedWhatsApp = 967711752823;
///   the B5 UPDATE fails with "duplicate key value violates unique constraint
///   IX_Patients_NormalizedWhatsApp" every startup.
///
/// Problem C — 20260430221624 would become the next stuck migration:
///   After 20260430221054 is unblocked, EF would immediately try
///   20260430221624_AddConversationPatientAndType.  That migration adds ConversationType
///   and PatientId to Conversations — both already present in production (added by
///   B-blocks and the idempotent C1-d migration 20260524000000).  Without pre-emptive
///   handling it would create a new identical retry loop.
///
/// This migration:
///   1. Idempotently ensures all schema objects from 20260430221054 exist.
///   2. Idempotently ensures all schema objects from 20260430221624 exist.
///   3. Safely backfills NormalizedPhone — skips rows whose normalized value would
///      conflict with an existing NormalizedPhone on another patient.
///   4. Safely backfills NormalizedWhatsApp — skips conflicting rows (including the
///      known 0711752823 → 967711752823 conflict). Staff must correct the duplicate
///      raw WhatsApp via the UI; see TD-020 Phase C1-f docs.
///   5. Inserts both stuck migration IDs into __EFMigrationsHistory only if absent.
///
/// Down() is intentionally a no-op.  This is a production repair migration; rollback
/// must not delete normalized patient data or undo migration history entries.
/// </summary>
[Migration("20260525000000_RepairPatientPhoneNormalizationState")]
public partial class RepairPatientPhoneNormalizationState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // =====================================================================
        // A. Ensure Patients.NormalizedPhone column exists (idempotent)
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
        // B. Ensure Patients.NormalizedWhatsApp column exists (idempotent)
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
        // C. Ensure all tables from 20260430221054 exist
        //    (CREATE TABLE IF NOT EXISTS — safe no-op when table already present)
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
        // D. Ensure foreign keys from 20260430221054 exist (idempotent)
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
        // E. Ensure indexes from 20260430221054 exist (idempotent)
        // =====================================================================

        // Normalized patient phone indexes — the core of the repair
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

        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PatientAccounts') THEN
                    CREATE INDEX IF NOT EXISTS "IX_PatientAccounts_PatientId"
                        ON "PatientAccounts" ("PatientId");
                END IF;
            END $$;
            """);

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

        // =====================================================================
        // F. Ensure columns/indexes from 20260430221624_AddConversationPatientAndType
        //    exist (idempotent).  Without this block, 20260430221624 would become
        //    the next stuck migration immediately after 20260430221054 is unblocked.
        //    The type for ConversationType is "text" to match the EF model snapshot.
        // =====================================================================
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Conversations' AND column_name = 'ConversationType'
                   ) THEN
                    ALTER TABLE "Conversations" ADD COLUMN "ConversationType" text NOT NULL DEFAULT '';
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Conversations' AND column_name = 'PatientId'
                   ) THEN
                    ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
                    CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId"
                        ON "Conversations" ("PatientId");
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                   AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
                    ALTER TABLE "Conversations"
                        ADD CONSTRAINT "FK_Conversations_Patients_PatientId"
                        FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
            """);

        // =====================================================================
        // G. Safe NormalizedPhone backfill
        //    Matches B4 (Program.cs) normalization logic exactly.
        //    Skips rows where the computed normalized value would conflict with
        //    an existing NormalizedPhone on a different patient row.
        //    Does NOT overwrite existing non-null NormalizedPhone values.
        // =====================================================================
        migrationBuilder.Sql("""
            UPDATE "Patients" p
            SET "NormalizedPhone" = LTRIM(RTRIM(
                CASE
                    WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                        '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                    WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                        '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                    WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                        '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                    WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '')) = 9
                         AND REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                        '967' || REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '')
                    ELSE REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '')
                END
            ))
            WHERE p."NormalizedPhone" IS NULL
              AND p."Phone" IS NOT NULL AND p."Phone" != ''
              AND NOT EXISTS (
                  SELECT 1 FROM "Patients" c
                  WHERE c."Id" <> p."Id"
                    AND c."NormalizedPhone" = LTRIM(RTRIM(
                        CASE
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '')) = 9
                                 AND REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                                '967' || REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '')
                            ELSE REPLACE(REPLACE(REPLACE(REPLACE(p."Phone", ' ', ''), '-', ''), '(', ''), ')', '')
                        END
                    ))
              );
            """);

        // =====================================================================
        // H. Safe NormalizedWhatsApp backfill
        //    Matches B5 (Program.cs) normalization logic exactly.
        //    Skips rows where the computed normalized value would conflict with
        //    an existing NormalizedWhatsApp on a different patient row.
        //    This specifically protects against the known production conflict:
        //      raw WhatsApp 0711752823 → normalized 967711752823 (already taken).
        //    Rows that cannot be normalized without conflict retain
        //    NormalizedWhatsApp = NULL.  The clinic must correct the duplicate
        //    raw WhatsApp numbers manually via the patient edit UI.
        //    Raw WhatsApp unique index IX_Patients_WhatsApp is intentionally NOT
        //    created here: raw WhatsApp duplicates still exist in production and
        //    the index would fail. It should be created only after staff resolve
        //    the 0711752823 duplicate via the UI.
        // =====================================================================
        migrationBuilder.Sql("""
            UPDATE "Patients" p
            SET "NormalizedWhatsApp" = LTRIM(RTRIM(
                CASE
                    WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                        '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                    WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                        '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                    WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                        '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                    WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')) = 9
                         AND REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                        '967' || REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')
                    ELSE REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')
                END
            ))
            WHERE p."NormalizedWhatsApp" IS NULL
              AND p."WhatsApp" IS NOT NULL AND p."WhatsApp" != ''
              AND NOT EXISTS (
                  SELECT 1 FROM "Patients" c
                  WHERE c."Id" <> p."Id"
                    AND c."NormalizedWhatsApp" = LTRIM(RTRIM(
                        CASE
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')) = 9
                                 AND REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                                '967' || REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')
                            ELSE REPLACE(REPLACE(REPLACE(REPLACE(p."WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')
                        END
                    ))
              );
            """);

        // =====================================================================
        // I. Insert stuck migration IDs into __EFMigrationsHistory if absent.
        //    Safety: each INSERT is guarded by WHERE NOT EXISTS so it is
        //    idempotent even if run multiple times.
        // =====================================================================

        // 20260430221054_AddPhoneNormalizationAndArchive:
        //   All schema objects it would have created are now ensured by sections
        //   A–E above. Safe to mark as applied.
        migrationBuilder.Sql("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260430221054_AddPhoneNormalizationAndArchive', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260430221054_AddPhoneNormalizationAndArchive'
            );
            """);

        // 20260430221624_AddConversationPatientAndType:
        //   All schema objects it would have created (ConversationType, PatientId,
        //   IX_Conversations_PatientId, FK_Conversations_Patients_PatientId) are
        //   ensured by section F above. Safe to mark as applied.
        migrationBuilder.Sql("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260430221624_AddConversationPatientAndType', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no-op. This is a production repair migration.
        // Rollback must not delete normalized patient data or undo migration
        // history entries — both would break production.
    }
}
