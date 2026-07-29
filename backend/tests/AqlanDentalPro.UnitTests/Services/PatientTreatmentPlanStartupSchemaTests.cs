using AqlanDentalPro.API.Configuration;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

public class PatientTreatmentPlanStartupSchemaTests
{
    private const string Sql = StartupDatabaseMaintenance.PatientTreatmentPlanStepsSchemaSql;

    [Fact]
    public void SchemaRepair_CreatesTheCorrectTableIdempotently()
    {
        Assert.Contains(
            "CREATE TABLE IF NOT EXISTS \"PatientTreatmentPlanSteps\"",
            Sql);
        Assert.DoesNotContain("table_name = 'TreatmentPlanSteps'", Sql);
    }

    [Theory]
    [InlineData("IX_PatientTreatmentPlanSteps_PatientId")]
    [InlineData("IX_PatientTreatmentPlanSteps_ServiceId")]
    [InlineData("IX_PatientTreatmentPlanSteps_Status")]
    [InlineData("IX_PatientTreatmentPlanSteps_ResponsibleDoctorId")]
    [InlineData("IX_PatientTreatmentPlanSteps_RelatedAppointmentId")]
    [InlineData("IX_PatientTreatmentPlanSteps_RelatedVisitId")]
    [InlineData("IX_PatientTreatmentPlanSteps_SequenceNumber")]
    public void SchemaRepair_CreatesIndexesIdempotently(string indexName)
    {
        Assert.Contains($"CREATE INDEX IF NOT EXISTS \"{indexName}\"", Sql);
    }

    [Theory]
    [InlineData("FK_PatientTreatmentPlanSteps_Patients_PatientId")]
    [InlineData("FK_PatientTreatmentPlanSteps_ClinicServices_ServiceId")]
    [InlineData("FK_PatientTreatmentPlanSteps_Doctors_ResponsibleDoctorId")]
    [InlineData("FK_PatientTreatmentPlanSteps_Appointments_RelatedAppointmentId")]
    [InlineData("FK_PatientTreatmentPlanSteps_Visits_RelatedVisitId")]
    [InlineData("FK_InvoiceLineItems_PatientTreatmentPlanSteps_RelatedTreatmentPlanStepId")]
    public void SchemaRepair_GuardsEveryForeignKey(string constraintName)
    {
        Assert.Contains($"conname = '{constraintName}'", Sql);
        Assert.Contains($"ADD CONSTRAINT \"{constraintName}\"", Sql);
    }

    [Fact]
    public void SchemaRepair_ReconcilesMigrationHistoryWithThePhysicalTable()
    {
        Assert.Contains(
            "20260530000000_AddPatientTreatmentPlanSteps",
            Sql);
        Assert.Contains(
            "INSERT INTO \"__EFMigrationsHistory\"",
            Sql);
    }

    [Fact]
    public void SchemaRepair_IsAdditive()
    {
        Assert.DoesNotContain("DROP TABLE", Sql);
        Assert.DoesNotContain("DROP COLUMN", Sql);
        Assert.DoesNotContain("TRUNCATE", Sql);
        Assert.DoesNotContain("DELETE FROM", Sql);
    }
}
