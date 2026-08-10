using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// SEQ-59 — the reviewers must not be able to see each other's answers.
/// <para>
/// This is the guarantee the whole Pilot rests on. Two orthodontists annotate the same
/// radiograph independently, and the distance between their answers is the measurement the
/// study exists to produce. If either can see the other's points — even accidentally, even
/// once — that distance stops measuring inter-observer agreement and starts measuring how
/// persuasive the first reviewer was, and nothing downstream would reveal it: the numbers
/// would simply look better than they are.
/// </para>
/// <para>
/// The existing suite covers authorization, coordinate bounds, completeness, immutability and
/// the ComparisonReady transition, but nothing asserted isolation itself. The controller does
/// enforce it — every read resolves the session by <c>ReviewerUserId == userId</c> — so these
/// tests pin behaviour that is currently correct rather than report a defect.
/// </para>
/// </summary>
public sealed class CephPilotReviewerIsolationTests
{
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
        public Task LogAsync(AuditAction action, string resource, Guid? resourceId = null,
            object? oldData = null, object? newData = null, string? details = null)
            => Task.CompletedTask;
    }

    // Two disjoint coordinate bands, so any point belonging to the other reviewer is
    // identifiable by value alone.
    private const decimal ReviewerAOriginX = 100m;
    private const decimal ReviewerBOriginX = 700m;

    /// <summary>
    /// The complete set of top-level fields a reviewer may receive.
    /// <para>
    /// Checked as an allowlist rather than by searching the payload for the other reviewer's
    /// numbers. A substring search over JSON is worse than useless here — it reported a leak on
    /// the first run because <c>"ImageHeight":1000</c> contains "100" — and, more importantly,
    /// it would not notice a new field carrying peer or AI data under a name nobody searched
    /// for. An allowlist fails the moment anything new appears, which is the actual risk.
    /// </para>
    /// </summary>
    private static readonly string[] AllowedSessionFields =
    [
        "Id", "CaseId", "reviewerSlot", "status", "LandmarkDefinitionVersion", "ToolVersion",
        "StartedAt", "LastSavedAt", "SubmittedAt", "Revision", "CaseCode", "ImageWidth",
        "ImageHeight", "MmPerPixel", "coreLandmarkCount", "points",
    ];

    /// <summary>Every X coordinate the response actually carries.</summary>
    private static List<decimal> PointXs(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("points").EnumerateArray()
            .Select(point => point.GetProperty("XCoordPx"))
            .Where(x => x.ValueKind == JsonValueKind.Number)
            .Select(x => x.GetDecimal())
            .ToList();
    }

    private static void AssertNoUnexpectedFields(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeSubsetOf(AllowedSessionFields,
                "a field the blind does not account for is exactly how peer, AI or WebCeph data " +
                "would reach a reviewer");
    }

    [Fact]
    public async Task ReviewerA_ReadingTheirOwnReview_SeesNoneOfReviewerBsCoordinates()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();

        var a = Controller(db, reviewerA);
        var b = Controller(db, reviewerB);

        await a.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        await b.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();

        await a.SaveMyReview(pilotCase.Id, Review(ReviewerAOriginX), CancellationToken.None);
        db.ChangeTracker.Clear();
        await b.SaveMyReview(pilotCase.Id, Review(ReviewerBOriginX), CancellationToken.None);
        db.ChangeTracker.Clear();

        var payload = Serialize(await a.GetMyReview(pilotCase.Id, CancellationToken.None));

        var xs = PointXs(payload);
        xs.Should().NotBeEmpty("reviewer A must still receive their own answers");
        xs.Should().OnlyContain(x => x >= ReviewerAOriginX && x < ReviewerBOriginX,
            "every point returned must be reviewer A's own");
        xs.Should().NotContain(x => x >= ReviewerBOriginX,
            "none of reviewer B's coordinates may appear in reviewer A's response");

        payload.Should().NotContain(reviewerB.ToString(),
            "even the other reviewer's identity is part of the blind");
        AssertNoUnexpectedFields(payload);
    }

    [Fact]
    public async Task AfterBothSubmit_TheReviewersStillCannotSeeEachOther()
    {
        // The riskiest moment: once the case is ComparisonReady the data has served its
        // purpose, and it would be easy to relax the read. Comparison and adjudication are a
        // later, separate stage — until then the blind holds.
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();

        var a = Controller(db, reviewerA);
        var b = Controller(db, reviewerB);

        await a.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        await b.StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();

        await a.SaveMyReview(pilotCase.Id, Review(ReviewerAOriginX), CancellationToken.None);
        db.ChangeTracker.Clear();
        await a.SubmitMyReview(pilotCase.Id, new() { ExpectedRevision = 2 }, CancellationToken.None);
        db.ChangeTracker.Clear();
        await b.SaveMyReview(pilotCase.Id, Review(ReviewerBOriginX), CancellationToken.None);
        db.ChangeTracker.Clear();
        await b.SubmitMyReview(pilotCase.Id, new() { ExpectedRevision = 2 }, CancellationToken.None);
        db.ChangeTracker.Clear();

        (await db.CephPilotCases.FindAsync(pilotCase.Id))!.Status
            .Should().Be(CephPilotCaseStatus.ComparisonReady);
        db.ChangeTracker.Clear();

        var forA = Serialize(await a.GetMyReview(pilotCase.Id, CancellationToken.None));
        db.ChangeTracker.Clear();
        var forB = Serialize(await b.GetMyReview(pilotCase.Id, CancellationToken.None));

        PointXs(forA).Should().OnlyContain(x => x < ReviewerBOriginX);
        PointXs(forB).Should().OnlyContain(x => x >= ReviewerBOriginX);
        AssertNoUnexpectedFields(forA);
        AssertNoUnexpectedFields(forB);
    }

    [Fact]
    public async Task SomeoneWhoIsNotAReviewerOnTheProjectGetsNothing()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var pilotCase = SeedCase(db, reviewerA, reviewerB);
        await db.SaveChangesAsync();

        await Controller(db, reviewerA).StartMyReview(pilotCase.Id, CancellationToken.None);
        db.ChangeTracker.Clear();
        await Controller(db, reviewerA).SaveMyReview(pilotCase.Id, Review(ReviewerAOriginX), CancellationToken.None);
        db.ChangeTracker.Clear();

        var outsider = Guid.NewGuid();
        db.Users.Add(User(outsider));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await Controller(db, outsider).GetMyReview(pilotCase.Id, CancellationToken.None))
            .Should().BeOfType<ForbidResult>("a third party is not part of the blinded pair");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string Serialize(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return JsonSerializer.Serialize(ok.Value);
    }

    private static SaveCephPilotReviewRequest Review(decimal originX) => new()
    {
        ExpectedRevision = 1,
        Points = CephPilotLandmarkCatalog.CoreKeys
            .Select((key, index) => new CephPilotReviewPointRequest
            {
                LandmarkKey = key,
                Visibility = CephPilotLandmarkVisibility.Visible,
                XCoordPx = originX + index,
                YCoordPx = 30 + index,
                ContourDecision = CephPilotContourDecision.NotApplicable,
            })
            .ToArray(),
    };

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CephPilotReviewController Controller(AppDbContext db, Guid userId) =>
        new(db, new TestCurrentUser(userId), new TestAuditService());

    private static User User(Guid id) => new()
    {
        Id = id,
        Username = $"reviewer-{id:N}",
        PasswordHash = "x",
        Role = UserRole.Orthodontist,
        IsActive = true,
    };

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
            ImageWidth = 1200,
            ImageHeight = 1000,
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
}
