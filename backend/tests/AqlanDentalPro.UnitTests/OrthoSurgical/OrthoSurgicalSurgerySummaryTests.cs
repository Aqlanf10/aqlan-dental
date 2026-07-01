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
/// Sprint A6 — unit tests for <see cref="OrthoSurgicalCasesController.GetSurgerySummary"/>,
/// a pure READ projection over the existing SurgeryCase/PreopReport/OperativeReport/
/// PostopRecord entities once create-surgery-case has linked one. Nothing is duplicated;
/// the full surgery record stays owned by the surgery module (/surgery/{id}).
/// </summary>
public class OrthoSurgicalSurgerySummaryTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-surgsummary-{Guid.NewGuid()}")
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

    private static (OrthoSurgicalCase Case, Patient Patient) SeedBareCase(AppDbContext db)
    {
        var patient = new Patient { PatientNumber = "P-SUMM", FirstName = "ريم", LastName = "المريضة", IsActive = true };
        db.Patients.Add(patient);
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-2026-005",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            Status = OrthoSurgicalStatus.SurgeryScheduled,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        db.SaveChanges();
        return (c, patient);
    }

    [Fact]
    public async Task NoSurgeryCaseLinked_ReturnsLinkedFalse()
    {
        await using var db = CreateDb();
        var (c, _) = SeedBareCase(db);
        var controller = Build(db, AdminUser());

        var result = await controller.GetSurgerySummary(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((bool)ok.Value!.GetType().GetProperty("linked")!.GetValue(ok.Value)!).Should().BeFalse();
    }

    [Fact]
    public async Task LinkedSurgeryCase_WithNoReportsYet_ReturnsBasicsAndNullSections()
    {
        await using var db = CreateDb();
        var (c, patient) = SeedBareCase(db);
        var surgery = new SurgeryCase
        {
            CaseNumber = "SU-2026-001", PatientId = patient.Id,
            SurgeryType = "Le Fort I + BSSO", Status = SurgeryCaseStatus.Scheduled, IsActive = true
        };
        db.SurgeryCases.Add(surgery);
        await db.SaveChangesAsync();
        c.SurgeryCaseId = surgery.Id;
        await db.SaveChangesAsync();

        var controller = Build(db, AdminUser());
        var result = await controller.GetSurgerySummary(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        ((bool)t.GetProperty("Linked")!.GetValue(ok.Value)!).Should().BeTrue();
        t.GetProperty("CaseNumber")!.GetValue(ok.Value).Should().Be("SU-2026-001");
        t.GetProperty("Status")!.GetValue(ok.Value).Should().Be("Scheduled");
        t.GetProperty("Preop")!.GetValue(ok.Value).Should().BeNull();
        t.GetProperty("Operative")!.GetValue(ok.Value).Should().BeNull();
        t.GetProperty("Postop")!.GetValue(ok.Value).Should().BeNull();
    }

    [Fact]
    public async Task LinkedSurgeryCase_WithFullReports_SummarizesEachSection()
    {
        await using var db = CreateDb();
        var (c, patient) = SeedBareCase(db);
        var surgery = new SurgeryCase
        {
            CaseNumber = "SU-2026-002", PatientId = patient.Id,
            SurgeryType = "Bimaxillary", Status = SurgeryCaseStatus.Completed, IsActive = true
        };
        db.SurgeryCases.Add(surgery);
        await db.SaveChangesAsync();
        c.SurgeryCaseId = surgery.Id;

        db.PreopReports.Add(new PreopReport { SurgeryCaseId = surgery.Id, ConsentSigned = true, SurgeryDate = DateOnly.FromDateTime(DateTime.UtcNow), IsActive = true });
        db.OperativeReports.Add(new OperativeReport { SurgeryCaseId = surgery.Id, Outcome = "ناجحة بدون مضاعفات", ApprovedAt = DateTime.UtcNow, IsActive = true });
        db.PostopRecords.Add(new PostopRecord { SurgeryCaseId = surgery.Id, Instructions = "تجنب المضغ لمدة أسبوعين", IsActive = true });
        await db.SaveChangesAsync();

        var controller = Build(db, AdminUser());
        var result = await controller.GetSurgerySummary(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();

        var preop = t.GetProperty("Preop")!.GetValue(ok.Value);
        preop.Should().NotBeNull();
        ((bool)preop!.GetType().GetProperty("ConsentSigned")!.GetValue(preop)!).Should().BeTrue();

        var operative = t.GetProperty("Operative")!.GetValue(ok.Value);
        operative.Should().NotBeNull();
        operative!.GetType().GetProperty("Outcome")!.GetValue(operative).Should().Be("ناجحة بدون مضاعفات");

        var postop = t.GetProperty("Postop")!.GetValue(ok.Value);
        postop.Should().NotBeNull();
        ((bool)postop!.GetType().GetProperty("HasInstructions")!.GetValue(postop)!).Should().BeTrue();
    }

    [Fact]
    public async Task WithoutPermission_Returns403()
    {
        await using var db = CreateDb();
        var (c, _) = SeedBareCase(db);
        // No RolePermissions seeded → CanAsync('view') is false for a non-admin role.
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        m.SetupGet(x => x.Role).Returns(UserRole.Orthodontist);
        m.SetupGet(x => x.IsAdmin).Returns(false);
        m.SetupGet(x => x.IsAuthenticated).Returns(true);
        var controller = Build(db, m);

        var result = await controller.GetSurgerySummary(c.Id);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }
}
