using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Insurance & Installments Migration (Phase V4):
/// 1. Create InsuranceCompanies table
/// 2. Create InsuranceClaims table with FK → InsuranceCompanies, Invoices, Patients
/// 3. Create InstallmentPlans table with FK → Contracts, Patients
/// 4. Create Installments table with FK → InstallmentPlans, Payments
/// 5. Add TaxPercentage, TaxAmount, Currency, ExchangeRate, TotalCostOfGoodsSold,
///    InsuranceClaimId columns to Invoices table
/// 6. Add InsuranceClaimId index and FK on Invoices
///
/// Preflight check (run before deploying):
///   SELECT COUNT(*) FROM "Invoices" WHERE "TaxPercentage" IS NOT NULL;
///   → Should return 0 (column doesn't exist yet)
///
/// Post-deploy verification:
///   SELECT "Id", "Name", "DefaultCoveragePercentage" FROM "InsuranceCompanies" LIMIT 5;
///   SELECT "Id", "Status", "TotalAmount", "CoveredAmount" FROM "InsuranceClaims" LIMIT 5;
///   SELECT "Id", "IsCompleted", "MonthlyAmount" FROM "InstallmentPlans" LIMIT 5;
/// </summary>
public partial class AddInsuranceAndInstallments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── 1. Create InsuranceCompanies table ──────────────────────────────
        migrationBuilder.CreateTable(
            name: "InsuranceCompanies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                DefaultCoveragePercentage = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsuranceCompanies", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceCompanies_Name",
            table: "InsuranceCompanies",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceCompanies_IsActive",
            table: "InsuranceCompanies",
            column: "IsActive");

        // ─── 2. Create InsuranceClaims table ─────────────────────────────────
        migrationBuilder.CreateTable(
            name: "InsuranceClaims",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                InsuranceCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                CoveredAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                PatientCoPay = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InsuranceClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_InsuranceClaims_InsuranceCompanies_InsuranceCompanyId",
                    column: x => x.InsuranceCompanyId,
                    principalTable: "InsuranceCompanies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InsuranceClaims_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InsuranceClaims_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceClaims_InvoiceId",
            table: "InsuranceClaims",
            column: "InvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceClaims_InsuranceCompanyId",
            table: "InsuranceClaims",
            column: "InsuranceCompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceClaims_PatientId",
            table: "InsuranceClaims",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_InsuranceClaims_Status",
            table: "InsuranceClaims",
            column: "Status");

        // ─── 3. Create InstallmentPlans table ────────────────────────────────
        migrationBuilder.CreateTable(
            name: "InstallmentPlans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                DownPayment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                NumberOfMonths = table.Column<int>(type: "integer", nullable: false),
                MonthlyAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InstallmentPlans", x => x.Id);
                table.ForeignKey(
                    name: "FK_InstallmentPlans_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InstallmentPlans_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InstallmentPlans_ContractId",
            table: "InstallmentPlans",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_InstallmentPlans_PatientId",
            table: "InstallmentPlans",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_InstallmentPlans_IsCompleted",
            table: "InstallmentPlans",
            column: "IsCompleted");

        // ─── 4. Create Installments table ────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Installments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstallmentPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PaidDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Installments", x => x.Id);
                table.ForeignKey(
                    name: "FK_Installments_InstallmentPlans_InstallmentPlanId",
                    column: x => x.InstallmentPlanId,
                    principalTable: "InstallmentPlans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Installments_Payments_PaymentId",
                    column: x => x.PaymentId,
                    principalTable: "Payments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Installments_InstallmentPlanId",
            table: "Installments",
            column: "InstallmentPlanId");

        migrationBuilder.CreateIndex(
            name: "IX_Installments_DueDate",
            table: "Installments",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_Installments_Status",
            table: "Installments",
            column: "Status");

        // ─── 5. Add new columns to Invoices table ────────────────────────────
        // SAFETY: TaxAmount column already exists (from AddInvoicesAndInvoiceLineItems migration)
        // as nullable. We must ALTER it to NOT NULL instead of ADD.
        // Other columns are truly new and can use ADD COLUMN IF NOT EXISTS.

        // TaxPercentage — truly new column
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""TaxPercentage"" numeric(5,2) NOT NULL DEFAULT 0");

        // TaxAmount — ALREADY EXISTS as nullable from original migration.
        // Step 1: Update any existing NULL values to 0 (old invoices had no tax)
        migrationBuilder.Sql(
            @"UPDATE ""Invoices"" SET ""TaxAmount"" = 0 WHERE ""TaxAmount"" IS NULL");
        // Step 2: Alter column to NOT NULL DEFAULT 0
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ALTER COLUMN ""TaxAmount"" SET NOT NULL");
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ALTER COLUMN ""TaxAmount"" SET DEFAULT 0");

        // Currency — truly new column
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""Currency"" character varying(10) NOT NULL DEFAULT 'YER'");
        // ExchangeRate — truly new column
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ExchangeRate"" numeric(12,6) NOT NULL DEFAULT 1.0");
        // TotalCostOfGoodsSold — truly new column
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""TotalCostOfGoodsSold"" numeric(12,2) NOT NULL DEFAULT 0");
        // InsuranceClaimId — truly new column (nullable by design)
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""InsuranceClaimId"" uuid NULL");

        // ─── 6. Add InsuranceClaimId index and FK on Invoices ────────────────
        migrationBuilder.Sql(
            @"CREATE INDEX IF NOT EXISTS ""IX_Invoices_InsuranceClaimId"" ON ""Invoices"" (""InsuranceClaimId"")");

        migrationBuilder.Sql(
            @"DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_Invoices_InsuranceClaims_InsuranceClaimId'
                ) THEN
                    ALTER TABLE ""Invoices""
                        ADD CONSTRAINT ""FK_Invoices_InsuranceClaims_InsuranceClaimId""
                        FOREIGN KEY (""InsuranceClaimId"") REFERENCES ""InsuranceClaims""(""Id"")
                        ON DELETE SET NULL;
                END IF;
            END $$");

        // ─── Seed: Add sample insurance company for smoke testing ─────────────
        migrationBuilder.Sql(
            @"INSERT INTO ""InsuranceCompanies"" (""Id"", ""Name"", ""ContactEmail"", ""Phone"", ""DefaultCoveragePercentage"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
             VALUES (
                '11111111-1111-1111-1111-111111111111',
                'التأمين الاجتماعي',
                'claims@social-insurance.ye',
                '+9671234567',
                0.8000,
                NOW(), NOW(), true
             )");
        migrationBuilder.Sql(
            @"INSERT INTO ""InsuranceCompanies"" (""Id"", ""Name"", ""ContactEmail"", ""Phone"", ""DefaultCoveragePercentage"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
             VALUES (
                '22222222-2222-2222-2222-222222222222',
                'بوبا العربية',
                'claims@bupa-arabia.com',
                '+96612345678',
                0.7500,
                NOW(), NOW(), true
             )");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ─── Drop FK from Invoices first ──────────────────────────────────────
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" DROP CONSTRAINT IF EXISTS ""FK_Invoices_InsuranceClaims_InsuranceClaimId""");
        migrationBuilder.Sql(
            @"DROP INDEX IF EXISTS ""IX_Invoices_InsuranceClaimId""");

        // ─── Revert Invoices column changes ───────────────────────────────────
        // TaxPercentage — truly new, can be dropped
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" DROP COLUMN IF EXISTS ""TaxPercentage""");
        // TaxAmount — existed before, revert to nullable (drop NOT NULL, drop default)
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ALTER COLUMN ""TaxAmount"" DROP NOT NULL");
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" ALTER COLUMN ""TaxAmount"" DROP DEFAULT");
        // Currency — truly new, can be dropped
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" DROP COLUMN IF EXISTS ""Currency""");
        // ExchangeRate — truly new, can be dropped
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" DROP COLUMN IF EXISTS ""ExchangeRate""");
        // TotalCostOfGoodsSold — truly new, can be dropped
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" DROP COLUMN IF EXISTS ""TotalCostOfGoodsSold""");
        // InsuranceClaimId — truly new, can be dropped
        migrationBuilder.Sql(
            @"ALTER TABLE ""Invoices"" DROP COLUMN IF EXISTS ""InsuranceClaimId""");

        // ─── Drop tables in reverse dependency order ──────────────────────────
        migrationBuilder.DropTable(name: "Installments");
        migrationBuilder.DropTable(name: "InsuranceClaims");
        migrationBuilder.DropTable(name: "InstallmentPlans");
        migrationBuilder.DropTable(name: "InsuranceCompanies");
    }
}
