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
/// Sprint A5 — unit tests for <see cref="OrthoSurgicalCasesController.UpsertJointPlan"/>
/// and the dual-approval lock (fixed here: locking previously silently no-op'd when no
/// JointPlan row existed yet — approval now auto-creates one).
/// </summary>
public class OrthoSurgicalJointPlanTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-jointplan-{Guid.NewGuid()}")
            .Options);

    private static Mock<ICurrentUserService> User(UserRole role)
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        m.SetupGet(c => c.Role).Returns(role);
        m.SetupGet(c => c.IsAdmin).Returns(role == UserRole.Admin);
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

    private static OrthoSurgicalCase SeedCase(AppDbContext db, OrthoSurgicalStatus status, bool withJointPlan = false, DateTime? lockedAt = null)
    {
        var patient = new Patient { PatientNumber = "P-JP", FirstName = "فيصل", LastName = "المريض", IsActive = true };
        db.Patients.Add(patient);
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-2026-004",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            Status = status,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        if (withJointPlan)
            db.JointPlans.Add(new JointPlan { OrthoSurgicalCaseId = c.Id, ProcedureType = "Le Fort I", LockedAt = lockedAt, IsActive = true });
        db.SaveChanges();
        return c;
    }

    [Fact]
    public async Task UpsertJointPlan_NoExistingPlan_CreatesOne()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpsertJointPlan(c.Id, new UpsertJointPlanRequest
        {
            ProcedureType = "Bimaxillary (Le Fort I + BSSO)",
            Timing = "بعد 6 أشهر من انتهاء تحضير التقويم",
            OrthodonticObjectives = "محاذاة القواطع وتصحيح ال crowding",
            SurgicalObjectives = "تصحيح Class III الهيكلي",
            Risks = "نزيف، عدوى، خدر مؤقت"
        });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.JointPlans.SingleAsync(j => j.OrthoSurgicalCaseId == c.Id);
        saved.ProcedureType.Should().Be("Bimaxillary (Le Fort I + BSSO)");
        saved.LockedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpsertJointPlan_ExistingUnlockedPlan_UpdatesInPlace_NoDuplicateRow()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending, withJointPlan: true);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpsertJointPlan(c.Id, new UpsertJointPlanRequest { ProcedureType = "Maxillary advancement only" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.JointPlans.CountAsync(j => j.OrthoSurgicalCaseId == c.Id)).Should().Be(1);
        (await db.JointPlans.SingleAsync(j => j.OrthoSurgicalCaseId == c.Id)).ProcedureType.Should().Be("Maxillary advancement only");
    }

    [Fact]
    public async Task UpsertJointPlan_AlreadyLocked_Returns400_AndDoesNotModify()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.JointPlanApproved, withJointPlan: true, lockedAt: DateTime.UtcNow.AddDays(-1));
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpsertJointPlan(c.Id, new UpsertJointPlanRequest { ProcedureType = "محاولة تعديل بعد القفل" });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.JointPlans.SingleAsync(j => j.OrthoSurgicalCaseId == c.Id)).ProcedureType.Should().Be("Le Fort I",
            "a locked joint plan must remain unchanged");
    }

    [Fact]
    public async Task DualApproval_WithNoPreExistingJointPlan_StillLocksOnSecondApproval()
    {
        // Regression test for the A1 bug: ApplyApproval used to require c.JointPlan to already
        // exist before it would set LockedAt, so a case where neither side drafted a joint plan
        // before approving would silently never lock. A5 fixes this by auto-creating the row.
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending, withJointPlan: false);
        var admin = Build(db, User(UserRole.Admin));

        await admin.ApproveOrthodontist(c.Id);
        var r2 = await admin.ApproveSurgeon(c.Id);

        r2.Should().BeOfType<OkObjectResult>();
        var plan = await db.JointPlans.SingleOrDefaultAsync(j => j.OrthoSurgicalCaseId == c.Id);
        plan.Should().NotBeNull("the joint plan must be auto-created so it can be locked");
        plan!.LockedAt.Should().NotBeNull();

        var updatedCase = await db.OrthoSurgicalCases.FindAsync(c.Id);
        updatedCase!.Status.Should().Be(OrthoSurgicalStatus.JointPlanApproved);
    }

    [Fact]
    public async Task UpsertJointPlan_WithoutPermission_Returns403()
    {
        await using var db = CreateDb();
        // No RolePermissions seeded → CanAsync('edit') is false for a non-admin role.
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending);
        var controller = Build(db, User(UserRole.Orthodontist));

        var result = await controller.UpsertJointPlan(c.Id, new UpsertJointPlanRequest { ProcedureType = "x" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }
}
