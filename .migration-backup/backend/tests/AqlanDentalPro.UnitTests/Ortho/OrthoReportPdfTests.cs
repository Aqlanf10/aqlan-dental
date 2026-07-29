using System.Text;
using System.Text.Json;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Sprint 2 — Arabic PDF reports inside the ortho Reports tab:
/// - Model/Cast analysis report renders valid %PDF from the saved ModelAnalysis
///   (top-level summary columns + deserialized DentalModelAnalysisResult);
/// - unified Case Summary report aggregates existing data and renders valid %PDF;
/// - both degrade gracefully on sparse data and throw ArgumentException when the
///   target is missing (mapped to an Arabic 404 by the controllers).
/// </summary>
public class OrthoReportPdfTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void AssertPdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    private static string SampleResultJson()
    {
        var result = new DentalModelAnalysisResult(
            Bolton: new BoltonResult(91.5m, 78.2m, 1.2m, -0.4m, "النسبة ضمن الطبيعي", "زيادة طفيفة"),
            UpperArch: new ArchSpaceResult(95m, 92m, -3m, "ازدحام علوي بسيط"),
            LowerArch: new ArchSpaceResult(90m, 88m, -2m, "ازدحام سفلي بسيط"),
            Pont: new PontResult(31m, 36m, 47m, 35m, 46m, -1m, -1m),
            Howe: null,
            Moyers: new MoyersResult(75, 23m, new MixedDentitionPrediction(21.5m, 21m, -1.5m, -1m)),
            TanakaJohnston: new TanakaJohnstonResult(23m, new MixedDentitionPrediction(21.8m, 21.2m, null, null)),
            Huckaba: new List<HuckabaToothResult>(),
            Warnings: new List<string> { "مؤشر بونت مرجع وصفي فقط." });
        return JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static async Task<(Guid caseId, Guid analysisId)> SeedModelAsync(AppDbContext db, bool approved, bool withJson)
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "ريم", LastName = "الحُبيشي", PatientNumber = "P-21", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-21", IsActive = true };
        var analysis = new ModelAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisDate = DateOnly.FromDateTime(DateTime.Today),
            DentitionStage = "Mixed",
            BoltonOverall = 91.5m, BoltonAnterior = 78.2m,
            UpperAld = -3m, LowerAld = -2m, PontIndex = 31m,
            ApprovedAt = approved ? DateTime.UtcNow : null,
            ResultDataJson = withJson ? SampleResultJson() : "{}",
            IsActive = true,
        };
        db.AddRange(patient, orthoCase, analysis);
        await db.SaveChangesAsync();
        return (orthoCase.Id, analysis.Id);
    }

    [Fact]
    public async Task ModelReport_WithFullResultJson_ProducesValidPdf()
    {
        await using var db = CreateDb();
        var (caseId, _) = await SeedModelAsync(db, approved: true, withJson: true);
        AssertPdf(await new OrthoModelAnalysisReportPdfGenerator(db).GenerateLatestAsync(caseId));
    }

    [Fact]
    public async Task ModelReport_ByAnalysisId_WithoutJson_StillRendersFromSummaryColumns()
    {
        await using var db = CreateDb();
        var (_, analysisId) = await SeedModelAsync(db, approved: false, withJson: false);
        AssertPdf(await new OrthoModelAnalysisReportPdfGenerator(db).GenerateAsync(analysisId));
    }

    [Fact]
    public async Task ModelReport_MissingCase_ThrowsArgumentException()
    {
        await using var db = CreateDb();
        var act = async () => await new OrthoModelAnalysisReportPdfGenerator(db).GenerateLatestAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CaseSummary_FullData_ProducesValidPdf()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            Id = Guid.NewGuid(), FirstName = "ريم", MiddleName = "علي", LastName = "الحُبيشي",
            PatientNumber = "P-22", Gender = Gender.Female, Phone = "0777000000",
            DateOfBirth = new DateOnly(2008, 4, 1), IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-22",
            ApplianceType = "تقويم ثابت", CurrentStage = "المحاذاة", StagePercentage = 40,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3)),
            Status = OrthoCaseStatus.Active, IsActive = true,
        };
        var exam = new OrthoClinicalExam
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, MolarRelation = "Class II",
            Overjet = 5.5m, Overbite = 4m, Crossbite = false, OpenBite = false,
            UpperCrowdingMm = 3m, LowerCrowdingMm = 2m, IsActive = true,
        };
        var dx = new OrthoDiagnosis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, SkeletalClassification = "Class II",
            DentalClassification = "Class II div 1", Summary = "سوء إطباق صنف ثاني", ApprovedAt = DateTime.UtcNow, IsActive = true,
        };
        var plan = new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, PlanLabel = "A", PlanVersion = 1,
            IsApproved = true, ApplianceType = "تقويم ثابت", ExpectedDurationMonths = 18,
            TreatmentGoals = "تصحيح البزوغ", IsActive = true,
        };
        var visit = new OrthoVisit
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, VisitNumber = 1,
            VisitDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),
            NextAppointmentDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)), IsActive = true,
        };
        var retention = new RetentionRecord
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, Status = "active",
            UpperRetainer = "Essix", LowerRetainer = "Bonded", IsActive = true,
        };
        var ceph = new CephAnalysis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisType = "steiner",
            AnalysisDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)), IsActive = true,
        };
        var model = new ModelAnalysis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, BoltonOverall = 91.5m, BoltonAnterior = 78m,
            UpperAld = -3m, LowerAld = -2m, PontIndex = 31m, DentitionStage = "Permanent",
            ApprovedAt = DateTime.UtcNow, ResultDataJson = "{}", IsActive = true,
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, RelatedCaseId = orthoCase.Id,
            Specialty = "orthodontics", TotalAmount = 300000m, DiscountAmount = 0m, IsActive = true,
        };
        var payment = new Payment { Id = Guid.NewGuid(), ContractId = contract.Id, PatientId = patient.Id, Amount = 120000m, IsActive = true };

        db.AddRange(patient, orthoCase, exam, dx, plan, visit, retention, ceph, model, contract, payment);
        await db.SaveChangesAsync();

        AssertPdf(await new OrthoCaseSummaryReportPdfGenerator(db).GenerateAsync(orthoCase.Id));
    }

    [Fact]
    public async Task CaseSummary_SparseCase_StillProducesValidPdf()
    {
        await using var db = CreateDb();
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "علي", LastName = "ناصر", PatientNumber = "P-23", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-23", Status = OrthoCaseStatus.Active, IsActive = true };
        db.AddRange(patient, orthoCase);
        await db.SaveChangesAsync();

        AssertPdf(await new OrthoCaseSummaryReportPdfGenerator(db).GenerateAsync(orthoCase.Id));
    }

    [Fact]
    public async Task CaseSummary_MissingCase_ThrowsArgumentException()
    {
        await using var db = CreateDb();
        var act = async () => await new OrthoCaseSummaryReportPdfGenerator(db).GenerateAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
