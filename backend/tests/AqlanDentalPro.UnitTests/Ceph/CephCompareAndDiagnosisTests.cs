using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// C-B tests: pre/post comparison between two analyses of the same case,
/// and the strengthened rule-based diagnosis engine (Wits corroboration,
/// vertical agreement, surgical/extraction indicators).
/// </summary>
public class CephCompareAndDiagnosisTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static CephService CreateService(AppDbContext db)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());
        user.Setup(u => u.IsAdmin).Returns(true);
        return new CephService(db, user.Object, new Mock<ILogger<CephService>>().Object);
    }

    private static async Task<(Guid caseId, Guid baseId, Guid targetId)> SeedTwoAnalysesAsync(AppDbContext db)
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = "مريض", LastName = "تقويم", PatientNumber = "GM-001", IsActive = true };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, CaseNumber = "OC-1", IsActive = true };
        var baseA = new CephAnalysis { Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisDate = new DateOnly(2026, 1, 1), AnalysisType = "steiner", IsActive = true };
        var targetA = new CephAnalysis { Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisDate = new DateOnly(2026, 6, 1), AnalysisType = "steiner", IsActive = true };
        db.AddRange(patient, orthoCase, baseA, targetA);

        // SNA: base 86 (dev +4), target 84 (dev +2) → improved (normal 82)
        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = baseA.Id, MeasurementName = "SNA", MeasurementValue = 86, NormalValue = 82, StdDeviation = 2, Unit = "°", Classification = "mild", IsActive = true },
            new CephMeasurement { AnalysisId = targetA.Id, MeasurementName = "SNA", MeasurementValue = 84, NormalValue = 82, StdDeviation = 2, Unit = "°", Classification = "normal", IsActive = true },
            // ANB: base 6 → target 7 (normal 2) → worsened
            new CephMeasurement { AnalysisId = baseA.Id, MeasurementName = "ANB", MeasurementValue = 6, NormalValue = 2, StdDeviation = 1, Unit = "°", Classification = "severe", IsActive = true },
            new CephMeasurement { AnalysisId = targetA.Id, MeasurementName = "ANB", MeasurementValue = 7, NormalValue = 2, StdDeviation = 1, Unit = "°", Classification = "severe", IsActive = true },
            // FMA only on base → one-sided row
            new CephMeasurement { AnalysisId = baseA.Id, MeasurementName = "FMA", MeasurementValue = 25, NormalValue = 25, StdDeviation = 4, Unit = "°", Classification = "normal", IsActive = true });
        await db.SaveChangesAsync();
        return (orthoCase.Id, baseA.Id, targetA.Id);
    }

    // ─── Compare ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Compare_SameCase_ComputesDeltaAndImprovement()
    {
        await using var db = CreateDb();
        var (_, baseId, targetId) = await SeedTwoAnalysesAsync(db);

        var (result, error) = await CreateService(db).CompareAsync(baseId, targetId);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.PatientName.Should().Contain("مريض");

        var sna = result.Rows.Single(r => r.MeasurementName == "SNA");
        sna.Delta.Should().Be(-2);
        sna.Improved.Should().BeTrue("84 is closer to the 82 norm than 86");

        var anb = result.Rows.Single(r => r.MeasurementName == "ANB");
        anb.Delta.Should().Be(1);
        anb.Improved.Should().BeFalse("7 is farther from the 2 norm than 6");

        var fma = result.Rows.Single(r => r.MeasurementName == "FMA");
        fma.TargetValue.Should().BeNull();
        fma.Delta.Should().BeNull();
        fma.Improved.Should().BeNull();
    }

    [Fact]
    public async Task Compare_DifferentCases_ReturnsArabicError()
    {
        await using var db = CreateDb();
        var (_, baseId, _) = await SeedTwoAnalysesAsync(db);
        var otherCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), CaseNumber = "OC-2", IsActive = true };
        var foreign = new CephAnalysis { Id = Guid.NewGuid(), OrthoCaseId = otherCase.Id, AnalysisDate = new DateOnly(2026, 3, 1), AnalysisType = "steiner", IsActive = true };
        db.AddRange(otherCase, foreign);
        await db.SaveChangesAsync();

        var (result, error) = await CreateService(db).CompareAsync(baseId, foreign.Id);

        result.Should().BeNull();
        error.Should().Be("التحليلان لا يخصان نفس الحالة");
    }

    [Fact]
    public async Task Compare_MissingAnalysis_ReturnsNotFoundError()
    {
        await using var db = CreateDb();
        var (_, baseId, _) = await SeedTwoAnalysesAsync(db);

        var (result, error) = await CreateService(db).CompareAsync(baseId, Guid.NewGuid());

        result.Should().BeNull();
        error.Should().Be("التحليل غير موجود");
    }

    [Fact]
    public async Task Compare_UsesNormFromCephNormsTable_WhenPresent()
    {
        await using var db = CreateDb();
        var (_, baseId, targetId) = await SeedTwoAnalysesAsync(db);
        db.CephNorms.Add(new CephNorm
        {
            MeasurementName = "SNA", AnalysisGroup = "steiner", NameAr = "زاوية SNA",
            NormalValue = 80, StdDeviation = 3, Unit = "°", IsActive = true,
        });
        await db.SaveChangesAsync();

        var (result, _) = await CreateService(db).CompareAsync(baseId, targetId);

        var sna = result!.Rows.Single(r => r.MeasurementName == "SNA");
        sna.NormalValue.Should().Be(80, "DB norm wins over the stored measurement norm");
        sna.NameAr.Should().Be("زاوية SNA");
        sna.AnalysisGroup.Should().Be("steiner");
        // with norm 80: base |86-80|=6, target |84-80|=4 → still improved
        sna.Improved.Should().BeTrue();
    }

    // ─── Rule engine (via SaveLandmarks → ComputeMeasurements → diagnosis) ───

    private static async Task<CephDiagnosis> RunDiagnosisAsync(AppDbContext db, params (string Name, decimal Value, decimal Normal, decimal Sd)[] ms)
    {
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), CaseNumber = "OC-D", IsActive = true };
        var analysis = new CephAnalysis { Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisDate = DateOnly.FromDateTime(DateTime.Today), AnalysisType = "full", IsActive = true };
        db.AddRange(orthoCase, analysis);
        foreach (var (name, value, normal, sd) in ms)
            db.CephMeasurements.Add(new CephMeasurement
            {
                AnalysisId = analysis.Id, MeasurementName = name, MeasurementValue = value,
                NormalValue = normal, StdDeviation = sd, Unit = "°", Classification = "normal", IsActive = true,
            });
        await db.SaveChangesAsync();

        await InvokeDiagnosisAsync(db, analysis.Id);

        return await db.CephDiagnoses.SingleAsync(d => d.AnalysisId == analysis.Id);
    }

    private static async Task InvokeDiagnosisAsync(AppDbContext db, Guid analysisId)
    {
        // GenerateDiagnosisAsync is private — exercise via the public compute path's
        // sibling: call it through reflection to keep the test focused on rules.
        var svc = CreateService(db);
        var method = typeof(CephService).GetMethod("GenerateDiagnosisAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, [analysisId])!;
    }

    [Fact]
    public async Task Diagnosis_AnbAndWitsAgree_ConfirmationMentioned()
    {
        await using var db = CreateDb();
        var diag = await RunDiagnosisAsync(db,
            ("ANB", 6, 2, 1), ("Wits", 4, 0, 1.5m));

        diag.SkeletalClass.Should().Be("Class II");
        diag.AiRecommendation.Should().Contain("مؤكد بمؤشر Wits");
    }

    [Fact]
    public async Task Diagnosis_AnbAndWitsDisagree_WarnsAboutMismatch()
    {
        await using var db = CreateDb();
        var diag = await RunDiagnosisAsync(db,
            ("ANB", 6, 2, 1), ("Wits", -4, 0, 1.5m));

        diag.AiRecommendation.Should().Contain("غير متوافقة مع ANB");
    }

    [Fact]
    public async Task Diagnosis_SevereAnb_AddsSurgicalIndicator()
    {
        await using var db = CreateDb();
        var diag = await RunDiagnosisAsync(db, ("ANB", 8, 2, 1));

        diag.AiRecommendation.Should().Contain("الخيار الجراحي");
        diag.AiRecommendation.Should().Contain("مؤشرات قاعدية آلية");
    }

    [Fact]
    public async Task Diagnosis_DentalProtrusionWithLowInterincisal_AddsExtractionStudyIndicator()
    {
        await using var db = CreateDb();
        var diag = await RunDiagnosisAsync(db,
            ("ANB", 2, 2, 1),
            ("U1-NA_mm", 9, 4, 2),
            ("U1-L1", 110, 131, 6));

        diag.AiRecommendation.Should().Contain("دراسة القلع");
    }

    [Fact]
    public async Task Diagnosis_VerticalConflict_WarnsAboutGoGnVsFma()
    {
        await using var db = CreateDb();
        var diag = await RunDiagnosisAsync(db,
            ("GoGn-SN", 42, 32, 4),  // hyperdivergent
            ("FMA", 25, 25, 4));     // normodivergent

        diag.VerticalPattern.Should().Be("Hyperdivergent", "GoGn-SN is the primary reference");
        diag.AiRecommendation.Should().Contain("نمطين رأسيين مختلفين");
    }

    [Fact]
    public async Task Diagnosis_NormalCase_HasNoIndicatorsSection()
    {
        await using var db = CreateDb();
        var diag = await RunDiagnosisAsync(db,
            ("ANB", 2, 2, 1), ("Wits", 0, 0, 1.5m), ("GoGn-SN", 32, 32, 4), ("FMA", 25, 25, 4));

        diag.SkeletalClass.Should().Be("Class I");
        diag.AiRecommendation.Should().NotContain("مؤشرات قاعدية آلية");
        diag.AiRecommendation.Should().Contain("مؤكد بمؤشر Wits");
        diag.AiRecommendation.Should().Contain("متوافق بين GoGn-SN وFMA");
    }

    [Fact]
    public async Task Diagnosis_NewCephMeasurements_AutoSyncUnapprovedOrthoDiagnosis()
    {
        await using var db = CreateDb();
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            CaseNumber = "OC-SYNC",
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisDate = DateOnly.FromDateTime(DateTime.Today),
            AnalysisType = "full",
            IsActive = true,
        };
        db.AddRange(orthoCase, analysis);
        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "ANB", MeasurementValue = 6, IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "Wits", MeasurementValue = 4, IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "FMA", MeasurementValue = 31, IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "SNA", MeasurementValue = 84, IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "SNB", MeasurementValue = 78, IsActive = true },
            new CephMeasurement { AnalysisId = analysis.Id, MeasurementName = "IMPA", MeasurementValue = 96, IsActive = true });
        await db.SaveChangesAsync();

        await InvokeDiagnosisAsync(db, analysis.Id);

        var synced = await db.OrthoDiagnoses.SingleAsync(d => d.OrthoCaseId == orthoCase.Id);
        synced.SkeletalClassification.Should().Be("Class II");
        synced.FacialPattern.Should().Be("Hyperdivergent");
        synced.ANB.Should().Be(6);
        synced.Wits.Should().Be(4);
        synced.FMA.Should().Be(31);
        synced.SNA.Should().Be(84);
        synced.SNB.Should().Be(78);
        synced.IMPA.Should().Be(96);
        synced.CephSourceAnalysisId.Should().Be(analysis.Id);
        synced.CephSyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Diagnosis_CephSync_PreservesManualClinicalFields()
    {
        await using var db = CreateDb();
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            CaseNumber = "OC-MANUAL",
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisDate = DateOnly.FromDateTime(DateTime.Today),
            AnalysisType = "full",
            IsActive = true,
        };
        var diagnosis = new OrthoDiagnosis
        {
            OrthoCaseId = orthoCase.Id,
            DentalClassification = "Angle II div 1",
            FunctionalDiagnosis = "تنفس فموي",
            Etiology = "وراثي ووظيفي",
            Summary = "ملخص الطبيب",
            SoftTissueDiagnosis = "وصف أنسجة رخوة يدوي",
            IsActive = true,
        };
        db.AddRange(orthoCase, analysis, diagnosis);
        db.CephMeasurements.Add(new CephMeasurement
        {
            AnalysisId = analysis.Id,
            MeasurementName = "ANB",
            MeasurementValue = -2,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        await InvokeDiagnosisAsync(db, analysis.Id);

        var synced = await db.OrthoDiagnoses.SingleAsync(d => d.OrthoCaseId == orthoCase.Id);
        synced.SkeletalClassification.Should().Be("Class III");
        synced.DentalClassification.Should().Be("Angle II div 1");
        synced.FunctionalDiagnosis.Should().Be("تنفس فموي");
        synced.Etiology.Should().Be("وراثي ووظيفي");
        synced.Summary.Should().Be("ملخص الطبيب");
        synced.SoftTissueDiagnosis.Should().Be("وصف أنسجة رخوة يدوي");
    }

    [Fact]
    public async Task Diagnosis_ApprovedOrthoDiagnosis_IsNeverOverwrittenByCephSync()
    {
        await using var db = CreateDb();
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            CaseNumber = "OC-APPROVED",
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisDate = DateOnly.FromDateTime(DateTime.Today),
            AnalysisType = "full",
            IsActive = true,
        };
        var originalSource = Guid.NewGuid();
        var diagnosis = new OrthoDiagnosis
        {
            OrthoCaseId = orthoCase.Id,
            SkeletalClassification = "Class I",
            ANB = 2,
            CephSourceAnalysisId = originalSource,
            CephSyncedAt = DateTime.UtcNow.AddDays(-10),
            ApprovedAt = DateTime.UtcNow.AddDays(-5),
            IsActive = true,
        };
        db.AddRange(orthoCase, analysis, diagnosis);
        db.CephMeasurements.Add(new CephMeasurement
        {
            AnalysisId = analysis.Id,
            MeasurementName = "ANB",
            MeasurementValue = 8,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        await InvokeDiagnosisAsync(db, analysis.Id);

        var approved = await db.OrthoDiagnoses.SingleAsync(d => d.OrthoCaseId == orthoCase.Id);
        approved.SkeletalClassification.Should().Be("Class I");
        approved.ANB.Should().Be(2);
        approved.CephSourceAnalysisId.Should().Be(originalSource);
    }
}
