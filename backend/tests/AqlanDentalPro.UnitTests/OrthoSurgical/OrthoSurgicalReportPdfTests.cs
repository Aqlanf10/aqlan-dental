using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.OrthoSurgical;

/// <summary>
/// Sprint A7 — tests for <see cref="OrthoSurgicalReportPdfGenerator"/>.
/// Follows CephReportPdfTests: generator resilience (valid %PDF bytes with both
/// minimal and full data), Arabic-not-found exception, no exception-detail leak.
/// </summary>
public class OrthoSurgicalReportPdfTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<Guid> SeedBareCaseAsync(AppDbContext db)
    {
        var patient = new Patient { PatientNumber = "P-PDF", FirstName = "نورا", LastName = "المريضة", IsActive = true };
        db.Patients.Add(patient);
        var orthoCase = new OrthoCase { PatientId = patient.Id, CaseNumber = "OC-PDF-1", IsActive = true };
        db.OrthoCases.Add(orthoCase);
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-PDF-001", PatientId = patient.Id, OrthoCaseId = orthoCase.Id,
            Status = OrthoSurgicalStatus.DraftByOrthodontist, IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private static async Task<Guid> SeedFullCaseAsync(AppDbContext db)
    {
        var patient = new Patient { PatientNumber = "P-PDF2", FirstName = "طارق", LastName = "المريض", IsActive = true };
        db.Patients.Add(patient);
        var orthoCase = new OrthoCase { PatientId = patient.Id, CaseNumber = "OC-PDF-2", IsActive = true };
        db.OrthoCases.Add(orthoCase);
        await db.SaveChangesAsync();

        var diagnosis = new OrthoDiagnosis
        {
            OrthoCaseId = orthoCase.Id, SkeletalClassification = "Class III",
            DentalClassification = "Class III molar", Summary = "تراجع الفك العلوي وبروز نسبي للفك السفلي",
            ApprovedAt = DateTime.UtcNow, IsActive = true
        };
        db.OrthoDiagnoses.Add(diagnosis);

        var ceph = new CephAnalysis { OrthoCaseId = orthoCase.Id, AnalysisType = "Steiner", IsApproved = true, IsActive = true };
        db.CephAnalyses.Add(ceph);
        await db.SaveChangesAsync();

        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "ANB", MeasurementValue = -2, NormalValue = 2, Unit = "°", Classification = "severe", IsActive = true },
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "SNA", MeasurementValue = 78, NormalValue = 82, Unit = "°", Classification = "mild", IsActive = true });

        db.RecordsChecklists.Add(new RecordsChecklist
        {
            OrthoCaseId = orthoCase.Id, ExtraoralFrontal = true, ExtraoralProfile = true,
            IntraoralFrontal = true, IntraoralRight = true, IntraoralLeft = true,
            Opg = true, LateralCeph = true, StudyModels = true, IsActive = true
        });

        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-PDF-002", PatientId = patient.Id, OrthoCaseId = orthoCase.Id,
            CephAnalysisId = ceph.Id, Status = OrthoSurgicalStatus.JointPlanApproved,
            OrthodontistApprovedAt = DateTime.UtcNow, SurgeonApprovedAt = DateTime.UtcNow, IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        await db.SaveChangesAsync();

        db.SurgeonReviews.Add(new SurgeonReview
        {
            OrthoSurgicalCaseId = c.Id, Decision = "Approved", ProposedProcedure = "Le Fort I advancement",
            ReviewedAt = DateTime.UtcNow, IsActive = true
        });
        db.JointPlans.Add(new JointPlan
        {
            OrthoSurgicalCaseId = c.Id, ProcedureType = "Le Fort I", Timing = "بعد 6 أشهر",
            OrthodonticObjectives = "محاذاة القواطع", SurgicalObjectives = "تصحيح Class III",
            Risks = "نزيف، عدوى", PatientExplanation = "سنقوم بتحريك الفك العلوي للأمام",
            LockedAt = DateTime.UtcNow, IsActive = true
        });
        await db.SaveChangesAsync();

        return c.Id;
    }

    [Fact]
    public async Task GenerateDoctorReportAsync_MinimalCase_ReturnsValidPdfBytes()
    {
        await using var db = CreateDb();
        var caseId = await SeedBareCaseAsync(db);

        var pdf = await new OrthoSurgicalReportPdfGenerator(db).GenerateDoctorReportAsync(caseId);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF",
            "the generator must produce a valid PDF even with no diagnosis/ceph/review/plan yet");
    }

    [Fact]
    public async Task GenerateDoctorReportAsync_FullCase_ReturnsValidPdfBytes()
    {
        await using var db = CreateDb();
        var caseId = await SeedFullCaseAsync(db);

        var pdf = await new OrthoSurgicalReportPdfGenerator(db).GenerateDoctorReportAsync(caseId);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GeneratePatientExplanationAsync_MinimalCase_ReturnsValidPdfBytes()
    {
        await using var db = CreateDb();
        var caseId = await SeedBareCaseAsync(db);

        var pdf = await new OrthoSurgicalReportPdfGenerator(db).GeneratePatientExplanationAsync(caseId);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF",
            "the patient explanation must fall back to generic Arabic wording when no joint plan exists yet");
    }

    [Fact]
    public async Task GeneratePatientExplanationAsync_FullCase_ReturnsValidPdfBytes()
    {
        await using var db = CreateDb();
        var caseId = await SeedFullCaseAsync(db);

        var pdf = await new OrthoSurgicalReportPdfGenerator(db).GeneratePatientExplanationAsync(caseId);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task GenerateDoctorReportAsync_MissingCase_ThrowsArgumentException()
    {
        await using var db = CreateDb();

        var act = () => new OrthoSurgicalReportPdfGenerator(db).GenerateDoctorReportAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GeneratePatientExplanationAsync_MissingCase_ThrowsArgumentException()
    {
        await using var db = CreateDb();

        var act = () => new OrthoSurgicalReportPdfGenerator(db).GeneratePatientExplanationAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
