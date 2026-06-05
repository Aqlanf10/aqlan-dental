using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    public partial class AddPasswordResetSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- ============================================================
                -- AddColumn: InvoiceLineItems commission/billing fields
                -- ============================================================

                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""CenterShareAmount"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""CommissionApprovedAt"" timestamp with time zone NULL;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""CommissionApprovedBy"" uuid NULL;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""CommissionBaseRule"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""CommissionNotes"" text NULL;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""CommissionStatus"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""DoctorCommissionAmount"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""DoctorCommissionPercentage"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""DoctorId"" uuid NULL;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""LabCost"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""LabOrderId"" uuid NULL;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""LineDiscountAmount"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""MaterialCost"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""NetCommissionableAmount"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""InvoiceLineItems"" ADD COLUMN IF NOT EXISTS ""OtherDirectCost"" numeric NOT NULL DEFAULT 0;

                -- ============================================================
                -- AddColumn: ClinicServices commission fields
                -- ============================================================

                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""CommissionBaseRule"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""CommissionRecognitionMode"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""DefaultDoctorCommissionPercentage"" numeric NULL;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""DefaultLabCost"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""DefaultMaterialCost"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""ClinicServices"" ADD COLUMN IF NOT EXISTS ""DefaultMaterialCostType"" integer NOT NULL DEFAULT 0;

                -- ============================================================
                -- CreateTable: DoctorCommissionPayments
                -- ============================================================

                CREATE TABLE IF NOT EXISTS ""DoctorCommissionPayments"" (
                    ""Id"" uuid NOT NULL,
                    ""DoctorId"" uuid NOT NULL,
                    ""Amount"" numeric NOT NULL,
                    ""PaymentDate"" date NOT NULL,
                    ""PaymentMethod"" text NULL,
                    ""ReferenceNumber"" text NULL,
                    ""Notes"" text NULL,
                    ""PaidBy"" uuid NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    ""IsActive"" boolean NOT NULL,
                    ""DeletedAt"" timestamp with time zone NULL,
                    ""DeletedBy"" uuid NULL,
                    CONSTRAINT ""PK_DoctorCommissionPayments"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_DoctorCommissionPayments_Doctors_DoctorId""
                        FOREIGN KEY (""DoctorId"") REFERENCES ""Doctors""(""Id"") ON DELETE CASCADE
                );

                -- ============================================================
                -- CreateTable: PasswordResetRequests
                -- ============================================================

                CREATE TABLE IF NOT EXISTS ""PasswordResetRequests"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NULL,
                    ""UsernameOrEmail"" character varying(200) NOT NULL,
                    ""RequestedAt"" timestamp with time zone NOT NULL,
                    ""Status"" character varying(20) NOT NULL,
                    ""RequestedIpHash"" text NULL,
                    ""UserAgent"" text NULL,
                    ""ApprovedByUserId"" uuid NULL,
                    ""ApprovedAt"" timestamp with time zone NULL,
                    ""Notes"" text NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    ""IsActive"" boolean NOT NULL,
                    CONSTRAINT ""PK_PasswordResetRequests"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_PasswordResetRequests_Users_ApprovedByUserId""
                        FOREIGN KEY (""ApprovedByUserId"") REFERENCES ""Users""(""Id"") ON DELETE SET NULL,
                    CONSTRAINT ""FK_PasswordResetRequests_Users_UserId""
                        FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE SET NULL
                );

                -- ============================================================
                -- CreateTable: PasswordResetTokens
                -- ============================================================

                CREATE TABLE IF NOT EXISTS ""PasswordResetTokens"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""TokenHash"" character varying(200) NOT NULL,
                    ""ExpiresAt"" timestamp with time zone NOT NULL,
                    ""IsUsed"" boolean NOT NULL,
                    ""UsedAt"" timestamp with time zone NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_PasswordResetTokens"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_PasswordResetTokens_Users_UserId""
                        FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE
                );

                -- ============================================================
                -- CreateIndex (non-unique)
                -- ============================================================

                CREATE INDEX IF NOT EXISTS ""IX_InvoiceLineItems_DoctorId"" ON ""InvoiceLineItems"" (""DoctorId"");
                CREATE INDEX IF NOT EXISTS ""IX_InvoiceLineItems_LabOrderId"" ON ""InvoiceLineItems"" (""LabOrderId"");
                CREATE INDEX IF NOT EXISTS ""IX_DoctorCommissionPayments_DoctorId"" ON ""DoctorCommissionPayments"" (""DoctorId"");
                CREATE INDEX IF NOT EXISTS ""IX_PasswordResetRequests_ApprovedByUserId"" ON ""PasswordResetRequests"" (""ApprovedByUserId"");
                CREATE INDEX IF NOT EXISTS ""IX_PasswordResetRequests_Status"" ON ""PasswordResetRequests"" (""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_PasswordResetRequests_UserId"" ON ""PasswordResetRequests"" (""UserId"");
                CREATE INDEX IF NOT EXISTS ""IX_PasswordResetTokens_UserId"" ON ""PasswordResetTokens"" (""UserId"");

                -- ============================================================
                -- CreateIndex (unique)
                -- ============================================================

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PasswordResetTokens_TokenHash"" ON ""PasswordResetTokens"" (""TokenHash"");

                -- ============================================================
                -- AddForeignKey: InvoiceLineItems -> Doctors
                -- ============================================================

                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_Doctors_DoctorId') THEN
                        ALTER TABLE ""InvoiceLineItems""
                            ADD CONSTRAINT ""FK_InvoiceLineItems_Doctors_DoctorId""
                            FOREIGN KEY (""DoctorId"") REFERENCES ""Doctors""(""Id"");
                    END IF;
                END $$;

                -- ============================================================
                -- AddForeignKey: InvoiceLineItems -> LabOrders
                -- ============================================================

                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId') THEN
                        ALTER TABLE ""InvoiceLineItems""
                            ADD CONSTRAINT ""FK_InvoiceLineItems_LabOrders_LabOrderId""
                            FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders""(""Id"");
                    END IF;
                END $$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_Doctors_DoctorId",
                table: "InvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "DoctorCommissionPayments");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_DoctorId",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CenterShareAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionApprovedAt",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionApprovedBy",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionBaseRule",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionNotes",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionStatus",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DoctorCommissionAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DoctorCommissionPercentage",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LabCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LineDiscountAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "MaterialCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "NetCommissionableAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "OtherDirectCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionBaseRule",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "CommissionRecognitionMode",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultDoctorCommissionPercentage",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultLabCost",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultMaterialCost",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultMaterialCostType",
                table: "ClinicServices");
        }
    }
}
