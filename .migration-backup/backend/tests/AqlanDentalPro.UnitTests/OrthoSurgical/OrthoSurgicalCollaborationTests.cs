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
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.OrthoSurgical;

/// <summary>
/// Sprint A4 — unit tests for the discussion-comments thread
/// (<see cref="OrthoSurgicalCasesController.GetComments"/>/<see cref="OrthoSurgicalCasesController.AddComment"/>)
/// and the per-case audit trail (<see cref="OrthoSurgicalCasesController.GetAuditTrail"/>),
/// which is a pure read over the existing AuditLogs table (no new logging mechanism).
/// </summary>
public class OrthoSurgicalCollaborationTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-collab-{Guid.NewGuid()}")
            .Options);

    private static Mock<ICurrentUserService> User(UserRole role, Guid? userId = null)
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.UserId).Returns(userId ?? Guid.NewGuid());
        m.SetupGet(c => c.Role).Returns(role);
        m.SetupGet(c => c.IsAdmin).Returns(role == UserRole.Admin);
        m.SetupGet(c => c.IsAuthenticated).Returns(true);
        return m;
    }

    private static OrthoSurgicalCasesController Build(AppDbContext db, Mock<ICurrentUserService> user, Mock<IAuditService>? auditMock = null)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(p => p.IsDoctor).Returns(false);
        access.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var logger = new Mock<ILogger<OrthoSurgicalCasesController>>().Object;
        return new OrthoSurgicalCasesController(db, logger, access.Object, (auditMock ?? new Mock<IAuditService>()).Object, user.Object);
    }

    private static OrthoSurgicalCase SeedCase(AppDbContext db)
    {
        var patient = new Patient { PatientNumber = "P-COLLAB", FirstName = "منى", LastName = "المريضة", IsActive = true };
        db.Patients.Add(patient);
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-2026-003",
            PatientId = patient.Id,
            OrthoCaseId = Guid.NewGuid(),
            Status = OrthoSurgicalStatus.SurgeonReviewPending,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        db.SaveChanges();
        return c;
    }

    // ── Comments ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddComment_EmptyBody_Returns400()
    {
        await using var db = CreateDb();
        var c = SeedCase(db);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.AddComment(c.Id, new CreateOrthoSurgicalCommentRequest { Body = "   " });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.OrthoSurgicalComments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddComment_TooLong_Returns400()
    {
        await using var db = CreateDb();
        var c = SeedCase(db);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.AddComment(c.Id, new CreateOrthoSurgicalCommentRequest { Body = new string('س', 2001) });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddComment_ValidBody_PersistsWithAuthorRoleAndAudits()
    {
        await using var db = CreateDb();
        var c = SeedCase(db);
        db.RolePermissions.Add(new RolePermission { Role = "OralSurgeon", Resource = "ortho_surgical", CanView = true, CanEdit = true });
        await db.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var auditMock = new Mock<IAuditService>();
        var controller = Build(db, User(UserRole.OralSurgeon, userId), auditMock);

        var result = await controller.AddComment(c.Id, new CreateOrthoSurgicalCommentRequest { Body = "يلزم تحضير تقويمي إضافي قبل الجراحة" });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.OrthoSurgicalComments.SingleAsync(m => m.OrthoSurgicalCaseId == c.Id);
        saved.AuthorUserId.Should().Be(userId);
        saved.AuthorRole.Should().Be("OralSurgeon");
        saved.Body.Should().Be("يلزم تحضير تقويمي إضافي قبل الجراحة");

        auditMock.Verify(a => a.LogAsync(AuditAction.Create, "OrthoSurgicalComment", saved.Id,
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task GetComments_ReturnsInChronologicalOrder()
    {
        // NOTE: AppDbContext.SaveChangesAsync unconditionally stamps CreatedAt = DateTime.UtcNow
        // on every Added BaseEntity, overwriting any explicit value set here. So ordering must be
        // established via separate sequential SaveChangesAsync calls (real wall-clock order),
        // not by pre-setting CreatedAt on entities added in a single batch.
        await using var db = CreateDb();
        var c = SeedCase(db);

        db.OrthoSurgicalComments.Add(new OrthoSurgicalComment { OrthoSurgicalCaseId = c.Id, Body = "الأول", AuthorRole = "Orthodontist", IsActive = true });
        await db.SaveChangesAsync();
        db.OrthoSurgicalComments.Add(new OrthoSurgicalComment { OrthoSurgicalCaseId = c.Id, Body = "الثاني", AuthorRole = "OralSurgeon", IsActive = true });
        await db.SaveChangesAsync();
        // Soft-deleted comment must not appear.
        db.OrthoSurgicalComments.Add(new OrthoSurgicalComment { OrthoSurgicalCaseId = c.Id, Body = "محذوف", AuthorRole = "Admin", IsActive = false });
        await db.SaveChangesAsync();

        var controller = Build(db, User(UserRole.Admin));
        var result = await controller.GetComments(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var items = data.Cast<object>().ToList();
        items.Should().HaveCount(2, "the soft-deleted comment must be excluded");
        items[0].GetType().GetProperty("Body")!.GetValue(items[0]).Should().Be("الأول");
        items[1].GetType().GetProperty("Body")!.GetValue(items[1]).Should().Be("الثاني");
    }

    [Fact]
    public async Task AddComment_WithoutPermission_Returns403()
    {
        await using var db = CreateDb();
        // No RolePermissions seeded → CanAsync('edit') is false for a non-admin role.
        var c = SeedCase(db);
        var controller = Build(db, User(UserRole.Orthodontist));

        var result = await controller.AddComment(c.Id, new CreateOrthoSurgicalCommentRequest { Body = "تعليق" });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    // ── Audit trail ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AuditTrail_ReturnsOnlyEntriesForThisCase_NewestFirst()
    {
        await using var db = CreateDb();
        var c = SeedCase(db);
        var otherCaseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "د.تقويم", PasswordHash = "x", PasswordSalt = "y", Role = UserRole.Orthodontist, IsActive = true });

        db.AuditLogs.AddRange(
            new AuditLog
            {
                Resource = "OrthoSurgicalCase", ResourceId = c.Id, Action = AuditAction.Create,
                UserId = userId, CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                NewData = JsonSerializer.SerializeToDocument(new { c.CaseNumber })
            },
            new AuditLog
            {
                Resource = "OrthoSurgicalCase", ResourceId = c.Id, Action = AuditAction.Update,
                UserId = userId, CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                NewData = JsonSerializer.SerializeToDocument(new { from = "Draft", to = "SentToSurgeon" })
            },
            // Different case — must NOT leak into this case's trail.
            new AuditLog
            {
                Resource = "OrthoSurgicalCase", ResourceId = otherCaseId, Action = AuditAction.Create,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var controller = Build(db, User(UserRole.Admin));
        var result = await controller.GetAuditTrail(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var items = data.Cast<object>().ToList();
        items.Should().HaveCount(2, "only entries for this case's ResourceId must be returned");

        var first = items[0];
        first.GetType().GetProperty("Action")!.GetValue(first).Should().Be("Update", "newest entry (Update) must come first");
        first.GetType().GetProperty("Username")!.GetValue(first).Should().Be("د.تقويم");
    }

    [Fact]
    public async Task AuditTrail_NullUser_ShowsSystemLabel()
    {
        await using var db = CreateDb();
        var c = SeedCase(db);
        db.AuditLogs.Add(new AuditLog
        {
            Resource = "OrthoSurgicalCase", ResourceId = c.Id, Action = AuditAction.Update,
            UserId = null, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = Build(db, User(UserRole.Admin));
        var result = await controller.GetAuditTrail(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var item = data.Cast<object>().Single();
        item.GetType().GetProperty("Username")!.GetValue(item).Should().Be("النظام");
    }
}
