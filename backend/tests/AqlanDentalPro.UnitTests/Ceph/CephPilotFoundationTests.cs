using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Migrations;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using SkiaSharp;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephPilotFoundationTests
{
    private sealed class TestMigration : AddCephPilotFoundation
    {
        public IReadOnlyList<MigrationOperation> UpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }

    private sealed class TestCurrentUser(Guid userId, bool isAdmin = false) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Username => "pilot-test-user";
        public UserRole? Role => isAdmin ? UserRole.Admin : UserRole.Orthodontist;
        public Guid? BranchId => null;
        public bool IsAdmin => isAdmin;
        public bool IsAuthenticated => true;
        public Guid? OriginalUserId => null;
        public bool IsImpersonating => false;
    }

    private sealed class TestAuditService : IAuditService
    {
        public List<(AuditAction Action, string Resource, Guid? ResourceId)> Entries { get; } = [];

        public Task LogAsync(AuditAction action, string resource, Guid? resourceId = null,
            object? oldData = null, object? newData = null, string? details = null)
        {
            Entries.Add((action, resource, resourceId));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void MigrationUp_IsAdditiveAndLimitedToPilotTables()
    {
        var operations = new TestMigration().UpOperations();
        var allowedOperationTypes = new[]
        {
            typeof(CreateTableOperation), typeof(CreateIndexOperation), typeof(AddCheckConstraintOperation),
        };
        operations.Select(operation => operation.GetType()).Should()
            .OnlyContain(type => allowedOperationTypes.Contains(type));
        operations.OfType<CreateTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "CephPilotProjects", "CephPilotCases", "CephPilotExportArtifacts");
        var destructiveOperationTypes = new[]
        {
            typeof(DropTableOperation), typeof(DropColumnOperation), typeof(AlterColumnOperation), typeof(DeleteDataOperation),
        };
        operations.Select(operation => operation.GetType()).Should()
            .NotContain(type => destructiveOperationTypes.Contains(type));
    }

    [Fact]
    public void PilotEntities_HaveNoClinicalPatientRelationship_AndPinRatifiedVersion()
    {
        var propertyNames = new[]
        {
            typeof(CephPilotProject), typeof(CephPilotCase), typeof(CephPilotExportArtifact),
        }.SelectMany(type => type.GetProperties()).Select(property => property.Name).ToArray();

        propertyNames.Should().NotContain("PatientId");
        propertyNames.Should().NotContain("Patient");
        new CephPilotProject().LandmarkDefinitionVersion.Should().Be("ADP-LM-LAT-v1.0");
        new CephPilotProject().IsAiHidden.Should().BeTrue();
        new CephPilotProject().IsWebCephHidden.Should().BeTrue();
        new CephPilotExportArtifact().IsComparatorOnly.Should().BeTrue();
    }

    [Theory]
    [InlineData(CephPilotProjectStatus.Draft, CephPilotProjectStatus.ReadyForAnnotation, true)]
    [InlineData(CephPilotProjectStatus.Draft, CephPilotProjectStatus.GoldStandardComplete, false)]
    [InlineData(CephPilotProjectStatus.Archived, CephPilotProjectStatus.Draft, false)]
    public void ProjectStateTransitions_AreServerDefined(
        CephPilotProjectStatus current,
        CephPilotProjectStatus next,
        bool expected) =>
        CephPilotStateTransitions.CanTransition(current, next).Should().Be(expected);

    [Fact]
    public async Task CreateProject_RejectsSameReviewerForAAndB()
    {
        await using var db = CreateDb();
        var admin = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        db.Users.AddRange(User(admin, UserRole.Admin), User(reviewer, UserRole.Orthodontist));
        await db.SaveChangesAsync();
        var controller = Controller(db, admin, isAdmin: true);

        var result = await controller.CreateProject(new()
        {
            Name = "Pilot",
            Code = "PILOT-001",
            ReviewerAUserId = reviewer,
            ReviewerBUserId = reviewer,
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.CephPilotProjects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnassignedReviewer_CannotReadProjectOrDiscoverItsCase()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB);
        var pilotCase = Case(project);
        db.Users.AddRange(User(reviewerA), User(reviewerB), User(outsider));
        db.AddRange(project, pilotCase);
        await db.SaveChangesAsync();
        var controller = Controller(db, outsider);

        (await controller.GetProject(project.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.GetCase(pilotCase.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.GetCaseImage(pilotCase.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AssignedReviewerCaseResponse_DoesNotExposeComparatorOrPrivateStorageMetadata()
    {
        await using var db = CreateDb();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB);
        var pilotCase = Case(project);
        pilotCase.ImageStorageKey = "private/secret/source.png";
        pilotCase.ImageSha256 = new string('a', 64);
        pilotCase.PatientGroupToken = new string('b', 64);
        pilotCase.SourceType = CephPilotSourceType.AccountOwnedWebCephExport;
        pilotCase.ExportArtifacts.Add(new CephPilotExportArtifact
        {
            ArtifactType = CephPilotExportArtifactType.WebCephLandmarkTable,
            StorageKey = "private/secret/table.csv",
            Sha256 = new string('c', 64),
            ContentType = "text/csv",
            SizeBytes = 100,
        });
        db.Users.AddRange(User(reviewerA), User(reviewerB));
        db.AddRange(project, pilotCase);
        await db.SaveChangesAsync();
        var controller = Controller(db, reviewerA);

        var response = (await controller.GetCase(pilotCase.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(response.Value).ToLowerInvariant();

        json.Should().NotContain("webceph");
        json.Should().NotContain("artifact");
        json.Should().NotContain("storage");
        json.Should().NotContain("sha256");
        json.Should().NotContain("patientgroup");
        json.Should().NotContain(reviewerB.ToString());

        var projectResponse = (await controller.GetProject(project.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>().Subject;
        var projectJson = JsonSerializer.Serialize(projectResponse.Value).ToLowerInvariant();
        projectJson.Should().NotContain("reviewerauserid");
        projectJson.Should().NotContain("reviewerbuserid");
        projectJson.Should().NotContain("ishidden");
        projectJson.Should().NotContain("webceph");
    }

    [Fact]
    public void ImageSanitizer_ReencodesToMetadataFreePng_AndHashesSanitizedBytes()
    {
        using var bitmap = new SKBitmap(320, 280);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.LightGray);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        var staged = CephPilotController.SanitizeImage(jpeg.ToArray());

        staged.Width.Should().Be(320);
        staged.Height.Should().Be(280);
        staged.Bytes.AsSpan(0, 8).ToArray().Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
        staged.Sha256.Should().Be(Convert.ToHexString(SHA256.HashData(staged.Bytes)).ToLowerInvariant());
    }

    [Fact]
    public async Task CreateCase_RejectsUnknownPatientIdFormField()
    {
        await using var db = CreateDb();
        var admin = Guid.NewGuid();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB, admin);
        db.Users.AddRange(User(admin, UserRole.Admin), User(reviewerA), User(reviewerB));
        db.Add(project);
        await db.SaveChangesAsync();
        var controller = Controller(db, admin, isAdmin: true);
        SetForm(controller, new Dictionary<string, string> { ["PatientId"] = Guid.NewGuid().ToString() });

        var result = await controller.CreateCase(project.Id, new(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.CephPilotCases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateCase_RejectsDuplicateSanitizedImageWithinProject()
    {
        await using var db = CreateDb();
        var admin = Guid.NewGuid();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB, admin);
        db.Users.AddRange(User(admin, UserRole.Admin), User(reviewerA), User(reviewerB));
        db.Add(project);
        await db.SaveChangesAsync();
        var bytes = SyntheticJpeg();
        var controller = Controller(db, admin, isAdmin: true);
        var firstRequest = ValidCaseRequest(bytes);
        SetForm(controller, files: [firstRequest.Image]);
        (await controller.CreateCase(project.Id, firstRequest, CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();

        var secondRequest = ValidCaseRequest(bytes);
        SetForm(controller, files: [secondRequest.Image]);
        var duplicate = await controller.CreateCase(project.Id, secondRequest, CancellationToken.None);

        duplicate.Should().BeOfType<ConflictObjectResult>();
        (await db.CephPilotCases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateCalibration_MakesUploadedCaseReady_AndIncrementsRevision()
    {
        await using var db = CreateDb();
        var admin = Guid.NewGuid();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB, admin);
        var pilotCase = Case(project);
        pilotCase.MmPerPixel = null;
        pilotCase.CalibrationSource = null;
        pilotCase.Status = CephPilotCaseStatus.Uploaded;
        db.Users.AddRange(User(admin, UserRole.Admin), User(reviewerA), User(reviewerB));
        db.AddRange(project, pilotCase);
        await db.SaveChangesAsync();
        var controller = Controller(db, admin, isAdmin: true);

        var result = await controller.UpdateCalibration(pilotCase.Id, new()
        {
            ExpectedRevision = 1,
            MmPerPixel = 0.22336386m,
            CalibrationSource = CephPilotCalibrationSource.CalibrationRuler,
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        pilotCase.MmPerPixel.Should().Be(0.22336386m);
        pilotCase.Status.Should().Be(CephPilotCaseStatus.Ready);
        pilotCase.Revision.Should().Be(2);
    }

    [Theory]
    [InlineData(CephPilotProjectStatus.ReadyForAnnotation, CephPilotCaseStatus.Ready)]
    [InlineData(CephPilotProjectStatus.Draft, CephPilotCaseStatus.ReviewerAInProgress)]
    public async Task UpdateCalibration_IsLockedWhenProjectOrReviewHasStarted(
        CephPilotProjectStatus projectStatus,
        CephPilotCaseStatus caseStatus)
    {
        await using var db = CreateDb();
        var admin = Guid.NewGuid();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB, admin);
        project.Status = projectStatus;
        var pilotCase = Case(project);
        pilotCase.Status = caseStatus;
        db.Users.AddRange(User(admin, UserRole.Admin), User(reviewerA), User(reviewerB));
        db.AddRange(project, pilotCase);
        await db.SaveChangesAsync();
        var controller = Controller(db, admin, isAdmin: true);

        var result = await controller.UpdateCalibration(pilotCase.Id, new()
        {
            ExpectedRevision = pilotCase.Revision,
            MmPerPixel = 0.25m,
            CalibrationSource = CephPilotCalibrationSource.CalibrationRuler,
        }, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        pilotCase.MmPerPixel.Should().Be(0.2m);
        pilotCase.Revision.Should().Be(1);
    }

    [Fact]
    public async Task UpdateCalibration_RejectsUnknownCalibrationSource()
    {
        await using var db = CreateDb();
        var admin = Guid.NewGuid();
        var reviewerA = Guid.NewGuid();
        var reviewerB = Guid.NewGuid();
        var project = Project(reviewerA, reviewerB, admin);
        var pilotCase = Case(project);
        db.Users.AddRange(User(admin, UserRole.Admin), User(reviewerA), User(reviewerB));
        db.AddRange(project, pilotCase);
        await db.SaveChangesAsync();
        var controller = Controller(db, admin, isAdmin: true);

        var result = await controller.UpdateCalibration(pilotCase.Id, new()
        {
            ExpectedRevision = 1,
            MmPerPixel = 0.25m,
            CalibrationSource = (CephPilotCalibrationSource)999,
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        pilotCase.MmPerPixel.Should().Be(0.2m);
        pilotCase.Revision.Should().Be(1);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CephPilotController Controller(AppDbContext db, Guid userId, bool isAdmin = false)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(Environments.Development);
        environment.SetupGet(item => item.ApplicationName).Returns("AqlanDentalPro.UnitTests");
        environment.SetupGet(item => item.ContentRootPath).Returns(Path.GetTempPath());
        environment.SetupGet(item => item.ContentRootFileProvider).Returns(Mock.Of<IFileProvider>());
        var controller = new CephPilotController(db, new TestCurrentUser(userId, isAdmin), new TestAuditService(), environment.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return controller;
    }

    private static User User(Guid id, UserRole role = UserRole.Orthodontist) => new()
    {
        Id = id,
        Username = $"user-{id:N}",
        PasswordHash = "test",
        PasswordSalt = "test",
        Role = role,
    };

    private static CephPilotProject Project(Guid reviewerA, Guid reviewerB, Guid? createdBy = null) => new()
    {
        Name = "Synthetic Pilot",
        Code = $"PILOT-{Guid.NewGuid():N}",
        ReviewerAUserId = reviewerA,
        ReviewerBUserId = reviewerB,
        CreatedByUserId = createdBy ?? reviewerA,
    };

    private static CephPilotCase Case(CephPilotProject project) => new()
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

    private static byte[] SyntheticJpeg()
    {
        using var bitmap = new SKBitmap(320, 280);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.DarkGray);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return encoded.ToArray();
    }

    private static CreateCephPilotCaseRequest ValidCaseRequest(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new CreateCephPilotCaseRequest
        {
            Image = new FormFile(stream, 0, bytes.Length, "Image", "synthetic.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" },
            SourceType = CephPilotSourceType.AccountOwnedWebCephExport,
            SourceReference = "WC-RECORD-001",
            MmPerPixel = 0.2m,
            CalibrationSource = CephPilotCalibrationSource.CalibrationRuler,
            SiteCode = "SITE-01",
            DeviceCode = "DEVICE-01",
            PatientGroupToken = new string('a', 64),
            QualityCategory = CephPilotQualityCategory.Normal,
            DeIdentificationVisuallyConfirmed = true,
            PixelInspectionConfirmed = true,
            NoBarcodeOrQrConfirmed = true,
            LegalBasisConfirmed = true,
            OrientationFacingRightConfirmed = true,
        };
    }

    private static void SetForm(
        ControllerBase controller,
        IDictionary<string, string>? fields = null,
        IReadOnlyList<IFormFile>? files = null)
    {
        var values = (fields ?? new Dictionary<string, string>())
            .ToDictionary(item => item.Key, item => new Microsoft.Extensions.Primitives.StringValues(item.Value));
        var formFiles = new FormFileCollection();
        if (files is not null)
            foreach (var file in files) formFiles.Add(file);
        controller.HttpContext.Request.ContentType = "multipart/form-data; boundary=test";
        controller.HttpContext.Request.Form = new FormCollection(values, formFiles);
    }
}
