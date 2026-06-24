using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TreatmentPlanEntity = AqlanDentalPro.Domain.Entities.TreatmentPlan;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Sprint 3 — Tests for treatment plan delete endpoint:
///   DELETE /api/ortho-cases/{id}/treatment-plans/{planId} → DeleteTreatmentPlan
///
/// Verifies:
///   - Non-approved plan is soft-deleted (IsActive=false), audit log written
///   - Approved plan is rejected with HTTP 400 + Arabic message
///   - Unknown planId / cross-case planId → HTTP 404 Arabic
///
/// Mirrors the controller-construction pattern in OrthoCasesControllerCreateAccessTests.
/// </summary>
public class OrthoTreatmentPlanDeleteTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoTreatmentPlanDeleteTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ortho-plan-delete-{Guid.NewGuid()}")
            .Options);
    }

    public void Dispose() => _db.Dispose();

    private static Mock<ICurrentUserService> AdminCurrentUser()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        mock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        mock.SetupGet(c => c.IsAdmin).Returns(true);
        return mock;
    }

    private static Mock<IPatientAccessService> FullAccess()
    {
        var mock = new Mock<IPatientAccessService>();
        mock.SetupGet(p => p.IsDoctor).Returns(false);
        mock.SetupGet(p => p.HasFullAccess).Returns(true);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        return mock;
    }

    private static OrthoCasesController BuildController(
        AppDbContext db,
        Mock<IAuditService>? audit = null)
    {
        var currentUser = AdminCurrentUser();
        return new OrthoCasesController(
            new OrthoService(db, currentUser.Object),
            db,
            currentUser.Object,
            FullAccess().Object,
            (audit ?? new Mock<IAuditService>()).Object);
    }

    private async Task<(Guid caseId, Guid planId)> SeedPlan(bool approved)
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            CaseNumber = "OR-PLAN-DEL-001",
            IsActive = true,
        });
        var plan = new TreatmentPlanEntity
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCaseId,
            PlanLabel = "A",
            IsApproved = approved,
            ApprovedAt = approved ? DateTime.UtcNow : null,
            TreatmentGoals = "هدف تجريبي",
        };
        _db.TreatmentPlans.Add(plan);
        await _db.SaveChangesAsync();
        return (orthoCaseId, plan.Id);
    }

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }

    [Fact]
    public async Task DeletePlan_NonApproved_SoftDeletes()
    {
        var (caseId, planId) = await SeedPlan(approved: false);
        var auditMock = new Mock<IAuditService>();
        var controller = BuildController(_db, auditMock);

        var result = await controller.DeleteTreatmentPlan(caseId, planId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Contain("تم حذف خطة العلاج");

        // Soft-deleted (IsActive=false), NOT hard-deleted. Other fields preserved.
        var plan = await _db.TreatmentPlans.IgnoreQueryFilters().FirstAsync(p => p.Id == planId);
        plan.IsActive.Should().BeFalse("soft-delete only — never hard-delete");
        plan.DeletedAt.Should().NotBeNull();
        plan.PlanLabel.Should().Be("A", "the row itself is preserved for audit history");
        plan.TreatmentGoals.Should().Be("هدف تجريبي");

        // Audit log written
        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.Delete,
                "TreatmentPlan",
                planId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "DeleteTreatmentPlan must produce exactly one audit LogAsync call");
    }

    [Fact]
    public async Task DeletePlan_Approved_Returns400_Arabic()
    {
        var (caseId, planId) = await SeedPlan(approved: true);
        var auditMock = new Mock<IAuditService>();
        var controller = BuildController(_db, auditMock);

        var result = await controller.DeleteTreatmentPlan(caseId, planId);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        var msg = ExtractMessage(bad.Value);
        msg.Should().Contain("لا يمكن حذف خطة معتمدة", "approved plans cannot be deleted (data integrity)");
        msg.Should().Contain("ألغِ الاعتماد", "the message must tell the user to un-approve first");
        msg.Should().NotContain("23505", "no database internals may be exposed");

        // Plan UNTOUCHED
        var plan = await _db.TreatmentPlans.FindAsync(planId);
        plan!.IsActive.Should().BeTrue("an approved plan must NOT be soft-deleted");
        plan.IsApproved.Should().BeTrue("the approval flag must be intact");

        // No audit log for the rejected attempt
        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.Delete,
                "TreatmentPlan",
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Never,
            "a rejected delete must NOT produce an audit log entry");
    }

    [Fact]
    public async Task DeletePlan_UnknownId_Returns404_Arabic()
    {
        var (caseId, _) = await SeedPlan(approved: false);
        var controller = BuildController(_db);

        var result = await controller.DeleteTreatmentPlan(caseId, Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value)
            .Should().Contain("خطة العلاج")
            .And.Contain("غير موجودة");
    }

    [Fact]
    public async Task DeletePlan_PlanBelongsToDifferentCase_Returns404()
    {
        var (caseA, planIdA) = await SeedPlan(approved: false);
        // Second case — planA does NOT belong to it
        var patientB = new Patient { Id = Guid.NewGuid(), FirstName = "B", LastName = "B", IsActive = true };
        var caseB = new OrthoCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = "OR-PLAN-DEL-002",
            PatientId = patientB.Id,
            IsActive = true,
        };
        _db.AddRange(patientB, caseB);
        await _db.SaveChangesAsync();

        var controller = BuildController(_db);
        var result = await controller.DeleteTreatmentPlan(caseB.Id, planIdA);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);

        // Plan A untouched
        var planA = await _db.TreatmentPlans.FindAsync(planIdA);
        planA!.IsActive.Should().BeTrue("a delete on a different case must NOT touch this plan");
    }
}
