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
/// Unit tests for <see cref="OrthoSurgicalCasesController"/> — the dual-approval workflow,
/// status-transition validation and role restrictions. Create/CreateSurgeryCase use raw SQL
/// (advisory locks) which EF InMemory cannot run, so those happy paths are asserted only up
/// to the pre-SQL gates (mirroring the SurgeryController test approach); the rest of the
/// workflow (approvals/status/surgeon-review) is fully exercised by seeding a case directly.
/// </summary>
public class OrthoSurgicalCasesControllerTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-{Guid.NewGuid()}")
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
        // Non-doctor access mock: DenyIfDoctorCannotAccess short-circuits (IsDoctor=false),
        // so per-patient checks pass and we exercise the workflow logic directly.
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(p => p.IsDoctor).Returns(false);
        access.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var logger = new Mock<ILogger<OrthoSurgicalCasesController>>().Object;
        return new OrthoSurgicalCasesController(db, logger, access.Object, new Mock<IAuditService>().Object, user.Object);
    }

    // Seed the RolePermissions the non-admin roles need so CanAsync() passes and the
    // controller's role-specific guards (not the generic permission gate) are what we test.
    private static async Task SeedPermissionsAsync(AppDbContext db)
    {
        db.RolePermissions.AddRange(
            new RolePermission { Role = "Orthodontist", Resource = "ortho_surgical", CanView = true, CanCreate = true, CanEdit = true, CanApprove = true },
            new RolePermission { Role = "OralSurgeon", Resource = "ortho_surgical", CanView = true, CanEdit = true, CanApprove = true });
        await db.SaveChangesAsync();
    }

    private static OrthoSurgicalCase SeedCase(AppDbContext db, OrthoSurgicalStatus status, bool withJointPlan = false)
    {
        var patient = new Patient { PatientNumber = "P-OS", FirstName = "خالد", LastName = "المريض", IsActive = true };
        db.Patients.Add(patient);
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-2026-001",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            Status = status,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        if (withJointPlan)
            db.JointPlans.Add(new JointPlan { OrthoSurgicalCaseId = c.Id, ProcedureType = "Le Fort I", IsActive = true });
        db.SaveChanges();
        return c;
    }

    // ── Dual approval ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveSurgeon_BeforeReview_Returns400()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.DraftByOrthodontist);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.ApproveSurgeon(c.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        var msg = ((BadRequestObjectResult)result).Value!.GetType().GetProperty("message")!.GetValue(((BadRequestObjectResult)result).Value);
        msg.Should().Be("لا يمكن الاعتماد قبل مراجعة الجراح");
    }

    [Fact]
    public async Task DualApproval_WhenBothApprove_LocksJointPlan_AndAdvancesToJointPlanApproved()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending, withJointPlan: true);
        var admin = Build(db, User(UserRole.Admin));

        // First approval (orthodontist) — not both yet.
        var r1 = await admin.ApproveOrthodontist(c.Id);
        r1.Should().BeOfType<OkObjectResult>();
        (await db.OrthoSurgicalCases.FindAsync(c.Id))!.Status.Should().Be(OrthoSurgicalStatus.SurgeonReviewPending);

        // Second approval (surgeon) — now both → locked + advanced.
        var r2 = await admin.ApproveSurgeon(c.Id);
        r2.Should().BeOfType<OkObjectResult>();

        var updated = await db.OrthoSurgicalCases.Include(x => x.JointPlan).FirstAsync(x => x.Id == c.Id);
        updated.Status.Should().Be(OrthoSurgicalStatus.JointPlanApproved);
        updated.OrthodontistApprovedAt.Should().NotBeNull();
        updated.SurgeonApprovedAt.Should().NotBeNull();
        updated.JointPlan!.LockedAt.Should().NotBeNull("the joint plan must lock once both sides approve");
    }

    [Fact]
    public async Task ApproveOrthodontist_BySurgeonRole_Returns403_RoleRestricted()
    {
        await using var db = CreateDb();
        await SeedPermissionsAsync(db);
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending);
        var controller = Build(db, User(UserRole.OralSurgeon));

        var result = await controller.ApproveOrthodontist(c.Id);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
        obj.Value!.GetType().GetProperty("message")!.GetValue(obj.Value)
            .Should().Be("اعتماد التقويم مقتصر على أخصائي التقويم");
    }

    // ── Surgeon review role restriction ──────────────────────────────────────────

    [Fact]
    public async Task SurgeonReview_ByOrthodontist_Returns403()
    {
        await using var db = CreateDb();
        await SeedPermissionsAsync(db);
        var c = SeedCase(db, OrthoSurgicalStatus.SentToSurgeon);
        var controller = Build(db, User(UserRole.Orthodontist));

        var result = await controller.UpsertSurgeonReview(c.Id, new SurgeonReviewRequest { Decision = "Approved" });

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SurgeonReview_RequestChanges_MovesStatusToSurgeonRequestedChanges()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.SentToSurgeon);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpsertSurgeonReview(c.Id,
            new SurgeonReviewRequest { Decision = "RequestChanges", Notes = "يلزم تحضير تقويمي إضافي" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.OrthoSurgicalCases.FindAsync(c.Id))!.Status
            .Should().Be(OrthoSurgicalStatus.SurgeonRequestedChanges);
        (await db.SurgeonReviews.FirstAsync(r => r.OrthoSurgicalCaseId == c.Id)).Decision.Should().Be("RequestChanges");
    }

    [Fact]
    public async Task SurgeonReview_InvalidDecision_Returns400()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.SentToSurgeon);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpsertSurgeonReview(c.Id, new SurgeonReviewRequest { Decision = "Maybe" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Status transitions ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns400_WithArabicMessage()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.DraftByOrthodontist);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpdateStatus(c.Id,
            new UpdateOrthoSurgicalStatusRequest { Status = nameof(OrthoSurgicalStatus.ReadyForSurgery) });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value)!.ToString()
            .Should().Contain("لا يمكن تغيير حالة");
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_Succeeds()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.CephReady);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.UpdateStatus(c.Id,
            new UpdateOrthoSurgicalStatusRequest { Status = nameof(OrthoSurgicalStatus.SentToSurgeon) });

        result.Should().BeOfType<OkObjectResult>();
        (await db.OrthoSurgicalCases.FindAsync(c.Id))!.Status.Should().Be(OrthoSurgicalStatus.SentToSurgeon);
    }

    // ── Create-surgery-case gate ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateSurgeryCase_WhenNotReadyForSurgery_Returns400()
    {
        await using var db = CreateDb();
        var c = SeedCase(db, OrthoSurgicalStatus.JointPlanApproved);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.CreateSurgeryCase(c.Id, new CreateSurgeryFromPlanRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.SurgeryCases.CountAsync()).Should().Be(0, "no surgery case may be created before ReadyForSurgery");
    }

    // ── Permission gate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_WithoutPermission_Returns403()
    {
        await using var db = CreateDb();
        // No RolePermissions seeded → CanAsync('approve') is false for a non-admin role.
        var c = SeedCase(db, OrthoSurgicalStatus.SurgeonReviewPending);
        var controller = Build(db, User(UserRole.Orthodontist));

        var result = await controller.ApproveOrthodontist(c.Id);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }
}
