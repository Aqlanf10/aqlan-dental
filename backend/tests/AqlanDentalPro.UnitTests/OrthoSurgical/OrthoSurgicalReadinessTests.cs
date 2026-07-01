using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.OrthoSurgical;

/// <summary>
/// Sprint A3 — unit tests for <see cref="OrthoSurgicalCasesController.GetReadiness"/>.
/// The endpoint is a pure READ over RecordsChecklist/OrthoDiagnosis/CephAnalysis —
/// nothing is written or duplicated, so these tests seed those existing entities
/// directly and assert the computed gates + missing-items list.
/// </summary>
public class OrthoSurgicalReadinessTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-readiness-{Guid.NewGuid()}")
            .Options);

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        m.SetupGet(c => c.Role).Returns(UserRole.Admin);
        m.SetupGet(c => c.IsAdmin).Returns(true);
        m.SetupGet(c => c.IsAuthenticated).Returns(true);
        return m;
    }

    private static OrthoSurgicalCasesController Build(AppDbContext db, Mock<ICurrentUserService> user)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(p => p.IsDoctor).Returns(false);
        access.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var logger = new Mock<ILogger<OrthoSurgicalCasesController>>().Object;
        return new OrthoSurgicalCasesController(db, logger, access.Object, new Mock<IAuditService>().Object, user.Object);
    }

    private static (OrthoSurgicalCase Case, Guid OrthoCaseId) SeedBareCase(AppDbContext db)
    {
        var patient = new Patient { PatientNumber = "P-RDY", FirstName = "سلمى", LastName = "المريضة", IsActive = true };
        db.Patients.Add(patient);
        var orthoCaseId = Guid.NewGuid();
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-2026-002",
            PatientId = patient.Id,
            OrthoCaseId = orthoCaseId,
            Status = OrthoSurgicalStatus.DraftByOrthodontist,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        db.SaveChanges();
        return (c, orthoCaseId);
    }

    private static RecordsChecklist CompleteChecklist(Guid orthoCaseId) => new()
    {
        OrthoCaseId = orthoCaseId,
        ExtraoralFrontal = true,
        ExtraoralProfile = true,
        ExtraoralSmile = true,
        IntraoralFrontal = true,
        IntraoralRight = true,
        IntraoralLeft = true,
        UpperOcclusal = true,
        LowerOcclusal = true,
        Opg = true,
        LateralCeph = true,
        Cbct = false,
        StudyModels = true,
        Consent = true,
        Contract = true,
        IsActive = true
    };

    [Fact]
    public async Task NothingSeeded_AllGatesFalse_AndListsWhatIsMissing()
    {
        await using var db = CreateDb();
        var (c, _) = SeedBareCase(db);
        var controller = Build(db, AdminUser());

        var result = await controller.GetReadiness(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        ((bool)t.GetProperty("RecordsReady")!.GetValue(ok.Value)!).Should().BeFalse();
        ((bool)t.GetProperty("CephReady")!.GetValue(ok.Value)!).Should().BeFalse();
        ((bool)t.GetProperty("DiagnosisReady")!.GetValue(ok.Value)!).Should().BeFalse();
        ((bool)t.GetProperty("SurgeonReviewReady")!.GetValue(ok.Value)!).Should().BeFalse();

        var missing = (System.Collections.Generic.List<string>)t.GetProperty("Missing")!.GetValue(ok.Value)!;
        missing.Should().Contain("لم تُحفظ قائمة السجلات بعد");
        missing.Should().Contain("لا يوجد تحليل سيفالو مرتبط بالحالة");
        missing.Should().Contain("لا يوجد تشخيص تقويمي محفوظ");
    }

    [Fact]
    public async Task CompleteChecklist_ApprovedCeph_ApprovedDiagnosis_AllGatesTrue()
    {
        await using var db = CreateDb();
        var (c, orthoCaseId) = SeedBareCase(db);

        db.RecordsChecklists.Add(CompleteChecklist(orthoCaseId));
        db.CephAnalyses.Add(new CephAnalysis
        {
            OrthoCaseId = orthoCaseId,
            AnalysisType = "Steiner",
            IsApproved = true,
            IsActive = true
        });
        db.OrthoDiagnoses.Add(new OrthoDiagnosis
        {
            OrthoCaseId = orthoCaseId,
            SkeletalClassification = "Class II",
            ApprovedAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = Build(db, AdminUser());
        var result = await controller.GetReadiness(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        ((bool)t.GetProperty("RecordsReady")!.GetValue(ok.Value)!).Should().BeTrue();
        ((bool)t.GetProperty("CephReady")!.GetValue(ok.Value)!).Should().BeTrue();
        ((bool)t.GetProperty("DiagnosisReady")!.GetValue(ok.Value)!).Should().BeTrue();
        ((bool)t.GetProperty("SurgeonReviewReady")!.GetValue(ok.Value)!).Should().BeTrue();

        var missing = (System.Collections.Generic.List<string>)t.GetProperty("Missing")!.GetValue(ok.Value)!;
        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task UnapprovedCeph_CephReadyIsFalse_EvenWhenAnalysisExists()
    {
        await using var db = CreateDb();
        var (c, orthoCaseId) = SeedBareCase(db);
        db.CephAnalyses.Add(new CephAnalysis { OrthoCaseId = orthoCaseId, AnalysisType = "Steiner", IsApproved = false, IsActive = true });
        await db.SaveChangesAsync();

        var controller = Build(db, AdminUser());
        var result = await controller.GetReadiness(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        ((bool)t.GetProperty("CephReady")!.GetValue(ok.Value)!).Should().BeFalse();
        var missing = (System.Collections.Generic.List<string>)t.GetProperty("Missing")!.GetValue(ok.Value)!;
        missing.Should().Contain("تحليل السيفالو لم يُعتمد بعد");
    }

    [Fact]
    public async Task IncompleteChecklist_MissingCorePhotos_RecordsReadyFalse_WithSpecificMissingItem()
    {
        await using var db = CreateDb();
        var (c, orthoCaseId) = SeedBareCase(db);
        var checklist = CompleteChecklist(orthoCaseId);
        checklist.Opg = false; // missing panoramic X-ray
        db.RecordsChecklists.Add(checklist);
        await db.SaveChangesAsync();

        var controller = Build(db, AdminUser());
        var result = await controller.GetReadiness(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        ((bool)t.GetProperty("RecordsReady")!.GetValue(ok.Value)!).Should().BeFalse();
        var missing = (System.Collections.Generic.List<string>)t.GetProperty("Missing")!.GetValue(ok.Value)!;
        missing.Should().Contain("صورة بانوراما OPG");
    }

    [Fact]
    public async Task CephAnalysisId_ExplicitlyLinked_UsesThatAnalysis_NotJustLatest()
    {
        await using var db = CreateDb();
        var (c, orthoCaseId) = SeedBareCase(db);

        // Older, approved analysis + newer, unapproved analysis — the case links the OLD one explicitly.
        var oldAnalysis = new CephAnalysis
        {
            OrthoCaseId = orthoCaseId, AnalysisType = "Steiner", IsApproved = true, IsActive = true,
            AnalysisDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
        };
        var newAnalysis = new CephAnalysis
        {
            OrthoCaseId = orthoCaseId, AnalysisType = "Steiner", IsApproved = false, IsActive = true,
            AnalysisDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        db.CephAnalyses.AddRange(oldAnalysis, newAnalysis);
        await db.SaveChangesAsync();

        c.CephAnalysisId = oldAnalysis.Id;
        await db.SaveChangesAsync();

        var controller = Build(db, AdminUser());
        var result = await controller.GetReadiness(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        ((bool)t.GetProperty("CephReady")!.GetValue(ok.Value)!).Should().BeTrue(
            "the case explicitly links the approved old analysis, not the newer unapproved one");
    }
}
