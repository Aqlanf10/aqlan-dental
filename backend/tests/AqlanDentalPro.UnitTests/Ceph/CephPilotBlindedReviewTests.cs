using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Migrations;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephPilotBlindedReviewTests
{
    private sealed class TestMigration : AddCephPilotBlindedReviews
    {
        public new IReadOnlyList<MigrationOperation> UpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Username => "pilot-reviewer";
        public UserRole? Role => UserRole.Orthodontist;
        public Guid? BranchId => null;
        public bool IsAdmin => false;
        public bool IsAuthenticated => true;
        public Guid? OriginalUserId => null;
        public bool IsImpersonating => false;
    }

    private sealed class TestAuditService : IAuditService
    {
        public List<(AuditAction Action, string Resource, object? Data)> Entries { get; } = [];

        public Task LogAsync(AuditAction action, string resource, Guid? resourceId = null,
            object? oldData = null, object? newData = null, string? details = null)
        {
            Entries.Add((action, resource, newData));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void MigrationUp_IsAdditiveAndCreatesOnlyReviewTables()
    {
        var operations = new TestMigration().UpOperations();
        operations.OfType<CreateTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "CephPilotReviewSessions", "CephPilotReviewPoints");
        var destructiveTypes = new[] { typeof(DropTableOperation), typeof(DropColumnOperation), typeof(AlterColumnOperation) };
        operations.Select(operation => operation.GetType()).Should().NotContain(type => destructiveTypes.Contains(type));
    }

    [Fact]
    public async Task UnassignedReviewer_CannotStartOrReadReview()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();
        var controller = Controller(db, outsider);

        (await controller.StartMyReview(pilotCase.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.GetMyReview(pilotCase.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await db.CephPilotReviewSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveReview_RejectsCoordinatesOutsideOriginalImage()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();
        var controller = Controller(db, reviewerA);
        await controller.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();

        var result = await controller.SaveMyReview(pilotCase.Id, new SaveCephPilotReviewRequest
        {
            ExpectedRevision = 1,
            Points = [Point("S", x: 1001, y: 20)],
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.CephPilotReviewPoints.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitReview_RequiresDecisionForEveryCoreLandmark()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();
        var controller = Controller(db, reviewerA);
        await controller.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        await controller.SaveMyReview(pilotCase.Id, new SaveCephPilotReviewRequest
        {
            ExpectedRevision = 1,
            Points = [Point("S")],
        }, CancellationToken.None);
        db.ChangeTracker.Clear();

        var result = await controller.SubmitMyReview(pilotCase.Id, new SubmitCephPilotReviewRequest
        {
            ExpectedRevision = 2,
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        (await db.CephPilotReviewSessions.SingleAsync()).Status.Should().Be(CephPilotReviewStatus.Draft);
    }

    [Fact]
    public async Task IndependentReviews_BecomeComparisonReadyOnlyAfterBothSubmit()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();
        var controllerA = Controller(db, reviewerA);
        var controllerB = Controller(db, reviewerB);
        await controllerA.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        await controllerB.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        (await db.CephPilotReviewSessions.SingleAsync(item => item.ReviewerUserId == reviewerA)).Revision.Should().Be(1);
        db.ChangeTracker.Clear();

        (await controllerA.SaveMyReview(pilotCase.Id, CompleteReview(), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        db.ChangeTracker.Clear();
        (await controllerA.SubmitMyReview(pilotCase.Id, new() { ExpectedRevision = 2 }, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        db.ChangeTracker.Clear();
        (await db.CephPilotCases.FindAsync(pilotCase.Id))!.Status.Should().Be(CephPilotCaseStatus.ReviewerASubmitted);
        db.ChangeTracker.Clear();

        (await controllerB.SaveMyReview(pilotCase.Id, CompleteReview(), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        db.ChangeTracker.Clear();
        (await controllerB.SubmitMyReview(pilotCase.Id, new() { ExpectedRevision = 2 }, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        db.ChangeTracker.Clear();

        (await db.CephPilotCases.FindAsync(pilotCase.Id))!.Status.Should().Be(CephPilotCaseStatus.ComparisonReady);
        db.ChangeTracker.Clear();
        (await db.CephPilotReviewSessions.CountAsync(item => item.Status == CephPilotReviewStatus.Submitted)).Should().Be(2);
        (await controllerA.GetMyReview(pilotCase.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmittedReview_IsImmutable()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();
        var controller = Controller(db, reviewerA);
        await controller.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        (await db.CephPilotReviewSessions.SingleAsync()).Revision.Should().Be(1);
        db.ChangeTracker.Clear();
        (await controller.SaveMyReview(pilotCase.Id, CompleteReview(), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        db.ChangeTracker.Clear();
        await controller.SubmitMyReview(pilotCase.Id, new() { ExpectedRevision = 2 }, CancellationToken.None);
        db.ChangeTracker.Clear();

        var result = await controller.SaveMyReview(pilotCase.Id, new SaveCephPilotReviewRequest
        {
            ExpectedRevision = 3,
            Points = [Point("S", x: 40, y: 40)],
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        (await db.CephPilotReviewPoints.SingleAsync(item => item.LandmarkKey == "S")).XCoordPx.Should().Be(20);
    }

    [Fact]
    public async Task ClosedProject_RejectsFurtherDraftChanges()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();
        var controller = Controller(db, reviewerA);
        await controller.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();

        var project = await db.CephPilotProjects.SingleAsync();
        project.Status = CephPilotProjectStatus.Archived;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await controller.SaveMyReview(pilotCase.Id, new SaveCephPilotReviewRequest
        {
            ExpectedRevision = 1,
            Points = [Point("S")],
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        (await db.CephPilotReviewPoints.CountAsync()).Should().Be(0);
    }

    private static SaveCephPilotReviewRequest CompleteReview() => new()
    {
        ExpectedRevision = 1,
        Points = CephPilotLandmarkCatalog.CoreKeys.Select((key, index) => Point(key, 20 + index, 30 + index)).ToArray(),
    };

    private static CephPilotReviewPointRequest Point(string key, decimal x = 20, decimal y = 30) => new()
    {
        LandmarkKey = key,
        Visibility = CephPilotLandmarkVisibility.Visible,
        XCoordPx = x,
        YCoordPx = y,
        ContourDecision = CephPilotContourDecision.NotApplicable,
    };

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CephPilotReviewController Controller(AppDbContext db, Guid userId) =>
        new(db, new TestCurrentUser(userId), new TestAuditService());

    private static CephPilotCase SeedCase(AppDbContext db, Guid reviewerA, Guid reviewerB)
    {
        db.Users.AddRange(User(reviewerA), User(reviewerB));
        var project = new CephPilotProject
        {
            Name = "Synthetic Pilot",
            Code = $"PILOT-{Guid.NewGuid():N}",
            ReviewerAUserId = reviewerA,
            ReviewerBUserId = reviewerB,
            CreatedByUserId = reviewerA,
            Status = CephPilotProjectStatus.ReadyForAnnotation,
        };
        var pilotCase = new CephPilotCase
        {
            Project = project,
            ProjectId = project.Id,
            CaseSequence = 1,
            CaseCode = "PILOT-001",
            SourceType = CephPilotSourceType.ApprovedDeidentifiedUpload,
            ImageStorageKey = "synthetic/source.png",
            ImageSha256 = new string('a', 64),
            ImageWidth = 1000,
            ImageHeight = 800,
            MmPerPixel = 0.2m,
            CalibrationSource = CephPilotCalibrationSource.CalibrationRuler,
            SiteCode = "SITE-01",
            DeviceCode = "DEVICE-01",
            PatientGroupToken = new string('b', 64),
            QualityCategory = CephPilotQualityCategory.Normal,
            DeIdentificationConfirmed = true,
            PixelInspectionConfirmed = true,
            NoBarcodeOrQrConfirmed = true,
            LegalBasisConfirmed = true,
            MetadataSanitized = true,
            OrientationConfirmed = true,
            Status = CephPilotCaseStatus.Ready,
        };
        db.AddRange(project, pilotCase);
        return pilotCase;
    }

    private static User User(Guid id) => new()
    {
        Id = id,
        Username = $"reviewer-{id:N}",
        PasswordHash = "test",
        PasswordSalt = "test",
        Role = UserRole.Orthodontist,
    };
}
