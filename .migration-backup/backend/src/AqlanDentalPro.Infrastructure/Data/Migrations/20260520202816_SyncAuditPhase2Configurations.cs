using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncAuditPhase2Configurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop shadow FK columns created by buggy WithMany() configuration.
            // These columns (PatientId1, VisitId1, UserId1) were created by EF Core
            // because the FluentAPI configurations used .WithMany() without specifying
            // the navigation property, causing EF Core to create duplicate relationships
            // with shadow FK properties instead of using the existing ones.
            // 
            // Each drop is guarded with IF EXISTS to handle cases where the columns
            // may not exist (e.g., if the buggy config was never deployed).

            // GeneralTreatments.VisitId1
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'GeneralTreatments' AND column_name = 'VisitId1') THEN
                        ALTER TABLE ""GeneralTreatments"" DROP CONSTRAINT IF EXISTS ""FK_GeneralTreatments_Visits_VisitId1"";
                        DROP INDEX IF EXISTS ""IX_GeneralTreatments_VisitId1"";
                        ALTER TABLE ""GeneralTreatments"" DROP COLUMN ""VisitId1"";
                    END IF;
                END $$;
            ");

            // InternalReferrals.PatientId1
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InternalReferrals' AND column_name = 'PatientId1') THEN
                        ALTER TABLE ""InternalReferrals"" DROP CONSTRAINT IF EXISTS ""FK_InternalReferrals_Patients_PatientId1"";
                        DROP INDEX IF EXISTS ""IX_InternalReferrals_PatientId1"";
                        ALTER TABLE ""InternalReferrals"" DROP COLUMN ""PatientId1"";
                    END IF;
                END $$;
            ");

            // Notifications.UserId1
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Notifications' AND column_name = 'UserId1') THEN
                        ALTER TABLE ""Notifications"" DROP CONSTRAINT IF EXISTS ""FK_Notifications_Users_UserId1"";
                        DROP INDEX IF EXISTS ""IX_Notifications_UserId1"";
                        ALTER TABLE ""Notifications"" DROP COLUMN ""UserId1"";
                    END IF;
                END $$;
            ");

            // Prescriptions.PatientId1
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Prescriptions' AND column_name = 'PatientId1') THEN
                        ALTER TABLE ""Prescriptions"" DROP CONSTRAINT IF EXISTS ""FK_Prescriptions_Patients_PatientId1"";
                        DROP INDEX IF EXISTS ""IX_Prescriptions_PatientId1"";
                        ALTER TABLE ""Prescriptions"" DROP COLUMN ""PatientId1"";
                    END IF;
                END $$;
            ");

            // Prescriptions.VisitId1
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Prescriptions' AND column_name = 'VisitId1') THEN
                        ALTER TABLE ""Prescriptions"" DROP CONSTRAINT IF EXISTS ""FK_Prescriptions_Visits_VisitId1"";
                        DROP INDEX IF EXISTS ""IX_Prescriptions_VisitId1"";
                        ALTER TABLE ""Prescriptions"" DROP COLUMN ""VisitId1"";
                    END IF;
                END $$;
            ");

            // SurgeryCases.PatientId1
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SurgeryCases' AND column_name = 'PatientId1') THEN
                        ALTER TABLE ""SurgeryCases"" DROP CONSTRAINT IF EXISTS ""FK_SurgeryCases_Patients_PatientId1"";
                        DROP INDEX IF EXISTS ""IX_SurgeryCases_PatientId1"";
                        ALTER TABLE ""SurgeryCases"" DROP COLUMN ""PatientId1"";
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PatientId1",
                table: "SurgeryCases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientId1",
                table: "Prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId1",
                table: "Prescriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientId1",
                table: "InternalReferrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId1",
                table: "GeneralTreatments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurgeryCases_PatientId1",
                table: "SurgeryCases",
                column: "PatientId1");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId1",
                table: "Prescriptions",
                column: "PatientId1");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_VisitId1",
                table: "Prescriptions",
                column: "VisitId1");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId1",
                table: "Notifications",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_InternalReferrals_PatientId1",
                table: "InternalReferrals",
                column: "PatientId1");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralTreatments_VisitId1",
                table: "GeneralTreatments",
                column: "VisitId1");

            migrationBuilder.AddForeignKey(
                name: "FK_GeneralTreatments_Visits_VisitId1",
                table: "GeneralTreatments",
                column: "VisitId1",
                principalTable: "Visits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InternalReferrals_Patients_PatientId1",
                table: "InternalReferrals",
                column: "PatientId1",
                principalTable: "Patients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId1",
                table: "Notifications",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Patients_PatientId1",
                table: "Prescriptions",
                column: "PatientId1",
                principalTable: "Patients",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Visits_VisitId1",
                table: "Prescriptions",
                column: "VisitId1",
                principalTable: "Visits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SurgeryCases_Patients_PatientId1",
                table: "SurgeryCases",
                column: "PatientId1",
                principalTable: "Patients",
                principalColumn: "Id");
        }
    }
}
