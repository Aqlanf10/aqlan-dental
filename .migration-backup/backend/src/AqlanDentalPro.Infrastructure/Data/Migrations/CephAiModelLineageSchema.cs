namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive production reconciliation for the cephalometric AI lineage schema.
/// Railway can run with EF migrations disabled, so startup maintenance and the
/// EF migration intentionally execute this same idempotent SQL contract.
/// </summary>
public static class CephAiModelLineageSchema
{
    public const string LandmarkReadCompatibilitySql = """
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephLandmarks') THEN
                ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "SourceInferenceRunId" uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_CephLandmarks_SourceInferenceRunId"
                    ON "CephLandmarks" ("SourceInferenceRunId");
            END IF;
        END $$;
        """;

    public const string UpSql = LandmarkReadCompatibilitySql + """

        CREATE TABLE IF NOT EXISTS "CephAiModelVersions" (
            "Id" uuid NOT NULL,
            "RegistryKey" character varying(200) NOT NULL,
            "Provider" character varying(50) NOT NULL,
            "ModelId" character varying(150) NOT NULL,
            "ModelVersion" character varying(100) NOT NULL,
            "PreprocessingVersion" character varying(100) NOT NULL,
            "DatasetVersion" character varying(100) NOT NULL,
            "LandmarkDefinitionVersion" character varying(100) NOT NULL,
            "IdentitySha256" character(64) NOT NULL,
            "ArtifactSha256" character(64) NULL,
            "Status" character varying(20) NOT NULL,
            "ValidationEvidenceId" character varying(150) NULL,
            "StatisticsApprovalId" character varying(150) NULL,
            "ClinicalApprovalId" character varying(150) NULL,
            "ApprovedByUserId" uuid NULL,
            "ApprovedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "IsActive" boolean NOT NULL,
            "DeletedAt" timestamp with time zone NULL,
            "DeletedBy" uuid NULL,
            CONSTRAINT "PK_CephAiModelVersions" PRIMARY KEY ("Id")
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CephAiModelVersions_RegistryKey"
            ON "CephAiModelVersions" ("RegistryKey");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CephAiModelVersions_IdentitySha256"
            ON "CephAiModelVersions" ("IdentitySha256");
        CREATE INDEX IF NOT EXISTS "IX_CephAiModelVersions_Status_Provider_ModelId"
            ON "CephAiModelVersions" ("Status", "Provider", "ModelId");

        CREATE TABLE IF NOT EXISTS "CephAiModelDeployments" (
            "Id" uuid NOT NULL,
            "Slot" character varying(80) NOT NULL,
            "ActiveModelVersionId" uuid NOT NULL,
            "PreviousModelVersionId" uuid NULL,
            "PinnedByUserId" uuid NULL,
            "PinnedAt" timestamp with time zone NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "IsActive" boolean NOT NULL,
            "DeletedAt" timestamp with time zone NULL,
            "DeletedBy" uuid NULL,
            CONSTRAINT "PK_CephAiModelDeployments" PRIMARY KEY ("Id")
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_CephAiModelDeployments_Slot"
            ON "CephAiModelDeployments" ("Slot");
        CREATE INDEX IF NOT EXISTS "IX_CephAiModelDeployments_ActiveModelVersionId"
            ON "CephAiModelDeployments" ("ActiveModelVersionId");
        CREATE INDEX IF NOT EXISTS "IX_CephAiModelDeployments_PreviousModelVersionId"
            ON "CephAiModelDeployments" ("PreviousModelVersionId");

        CREATE TABLE IF NOT EXISTS "CephAiInferenceRuns" (
            "Id" uuid NOT NULL,
            "AnalysisId" uuid NOT NULL,
            "ModelVersionId" uuid NOT NULL,
            "Operation" character varying(40) NOT NULL,
            "Precision" character varying(20) NOT NULL,
            "ImageWidth" integer NOT NULL,
            "ImageHeight" integer NOT NULL,
            "ImageSha256" character(64) NOT NULL,
            "Status" character varying(20) NOT NULL,
            "FailureCode" character varying(100) NULL,
            "OriginalPredictionsJson" jsonb NULL,
            "OutputSha256" character(64) NULL,
            "TriggeredByUserId" uuid NULL,
            "StartedAt" timestamp with time zone NOT NULL,
            "CompletedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone NOT NULL,
            "IsActive" boolean NOT NULL,
            "DeletedAt" timestamp with time zone NULL,
            "DeletedBy" uuid NULL,
            CONSTRAINT "PK_CephAiInferenceRuns" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_CephAiInferenceRuns_AnalysisId_StartedAt"
            ON "CephAiInferenceRuns" ("AnalysisId", "StartedAt");
        CREATE INDEX IF NOT EXISTS "IX_CephAiInferenceRuns_ModelVersionId_Status_StartedAt"
            ON "CephAiInferenceRuns" ("ModelVersionId", "Status", "StartedAt");

        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephAiModelDeployments_CephAiModelVersions_ActiveModelVersionId') THEN
                ALTER TABLE "CephAiModelDeployments"
                    ADD CONSTRAINT "FK_CephAiModelDeployments_CephAiModelVersions_ActiveModelVersionId"
                    FOREIGN KEY ("ActiveModelVersionId") REFERENCES "CephAiModelVersions" ("Id") ON DELETE RESTRICT;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephAiModelDeployments_CephAiModelVersions_PreviousModelVersionId') THEN
                ALTER TABLE "CephAiModelDeployments"
                    ADD CONSTRAINT "FK_CephAiModelDeployments_CephAiModelVersions_PreviousModelVersionId"
                    FOREIGN KEY ("PreviousModelVersionId") REFERENCES "CephAiModelVersions" ("Id") ON DELETE RESTRICT;
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephAnalyses')
               AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephAiInferenceRuns_CephAnalyses_AnalysisId') THEN
                ALTER TABLE "CephAiInferenceRuns"
                    ADD CONSTRAINT "FK_CephAiInferenceRuns_CephAnalyses_AnalysisId"
                    FOREIGN KEY ("AnalysisId") REFERENCES "CephAnalyses" ("Id") ON DELETE RESTRICT;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephAiInferenceRuns_CephAiModelVersions_ModelVersionId') THEN
                ALTER TABLE "CephAiInferenceRuns"
                    ADD CONSTRAINT "FK_CephAiInferenceRuns_CephAiModelVersions_ModelVersionId"
                    FOREIGN KEY ("ModelVersionId") REFERENCES "CephAiModelVersions" ("Id") ON DELETE RESTRICT;
            END IF;
        END $$;

        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephLandmarks') THEN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephLandmarks_CephAiInferenceRuns_SourceInferenceRunId') THEN
                    ALTER TABLE "CephLandmarks"
                        ADD CONSTRAINT "FK_CephLandmarks_CephAiInferenceRuns_SourceInferenceRunId"
                        FOREIGN KEY ("SourceInferenceRunId") REFERENCES "CephAiInferenceRuns" ("Id") ON DELETE RESTRICT;
                END IF;
            END IF;
        END $$;
        """;
}
