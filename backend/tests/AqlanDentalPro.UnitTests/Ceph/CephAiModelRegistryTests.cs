using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Migrations;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephAiModelRegistryTests
{
    private sealed class TestLandmarkProvider(string providerName) : ICephLandmarkDraftProvider
    {
        public string ProviderName => providerName;
        public string ApiKeyEnvVar => "TEST_CEPH_AI_KEY";

        public Task<IReadOnlyList<CephAiLandmarkPoint>> GenerateAsync(
            byte[] imageBytes,
            string mimeType,
            string model,
            string apiKey,
            CephAiPrecision precision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CephAiLandmarkPoint?> RefineAsync(
            byte[] imageBytes,
            string mimeType,
            string model,
            string apiKey,
            CephLandmarkRefineTarget target,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestableLineageMigration : AddCephAiModelLineage
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }

    [Fact]
    public void MigrationUp_IsLimitedToCephLineageObjects()
    {
        var operations = new TestableLineageMigration().BuildUpOperations();

        operations.OfType<CreateTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "CephAiModelVersions",
            "CephAiModelDeployments",
            "CephAiInferenceRuns");
        var addedColumn = operations.OfType<AddColumnOperation>().Should().ContainSingle().Which;
        addedColumn.Table.Should().Be("CephLandmarks");
        addedColumn.Name.Should().Be("SourceInferenceRunId");
        operations.OfType<AlterColumnOperation>().Should().BeEmpty();
        operations.OfType<DropTableOperation>().Should().BeEmpty();
        operations.OfType<CreateIndexOperation>()
            .Should().OnlyContain(item => item.Table.StartsWith("Ceph", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RuntimeModel_IsObservedDeterministically_AndCannotBePinned()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var first = await service.ResolveForInferenceAsync("Gemini", "ceph-model-v1");
        var second = await service.ResolveForInferenceAsync("gemini", "ceph-model-v1");

        second.Id.Should().Be(first.Id);
        first.Status.Should().Be("observed");
        first.ArtifactSha256.Should().BeNull();
        first.PreprocessingVersion.Should().Be(CephAiModelRegistryService.RuntimePreprocessingVersion);
        first.IdentitySha256.Should().MatchRegex("^[0-9a-f]{64}$");
        (await db.CephAiModelVersions.CountAsync()).Should().Be(1);

        var pin = async () => await service.PinAsync(first.Id);
        await pin.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*approved*");
    }

    [Fact]
    public async Task ApprovalRequiresAllEvidence_ThenPinAndRollbackPreserveBothVersions()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var service = CreateService(db, userId);
        var first = await service.RegisterCandidateAsync(Candidate("model-v1", 'a'));
        var second = await service.RegisterCandidateAsync(Candidate("model-v2", 'b'));

        var incomplete = async () => await service.ApproveAsync(first.Id, new()
        {
            ValidationEvidenceId = "study-001",
            StatisticsApprovalId = "stats-001",
        });
        await incomplete.Should().ThrowAsync<ArgumentException>();

        var approvedFirst = await service.ApproveAsync(first.Id, Approval("001"));
        approvedFirst!.Status.Should().Be("approved");
        approvedFirst.ApprovedAt.Should().NotBeNull();
        await service.PinAsync(first.Id);

        await service.ApproveAsync(second.Id, Approval("002"));
        var promoted = await service.PinAsync(second.Id);
        promoted.ActiveModelVersionId.Should().Be(second.Id);
        promoted.PreviousModelVersionId.Should().Be(first.Id);

        var rolledBack = await service.RollbackAsync();
        rolledBack.ActiveModelVersionId.Should().Be(first.Id);
        rolledBack.PreviousModelVersionId.Should().Be(second.Id);
        (await db.CephAiModelVersions.SingleAsync(item => item.Id == first.Id))
            .ApprovedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task PinRejectsApprovedModel_WhenItsProviderIsNotInstalled()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var candidate = await service.RegisterCandidateAsync(
            Candidate("model-v1", 'a', "not-installed"));
        await service.ApproveAsync(candidate.Id, Approval("001"));

        var pin = async () => await service.PinAsync(candidate.Id);

        await pin.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*installed*");
        (await db.CephAiModelDeployments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PinRejectsApprovedModel_WhenRuntimeContractDoesNotMatch()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var candidate = await service.RegisterCandidateAsync(
            Candidate("model-v1", 'a', preprocessingVersion: "ADP-CEPH-PREPROCESS-v2"));
        await service.ApproveAsync(candidate.Id, Approval("001"));

        var pin = async () => await service.PinAsync(candidate.Id);

        await pin.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not supported by this runtime*");
        (await db.CephAiModelDeployments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task InferenceRunStoresHashesAndOriginalOutput_ThenBecomesImmutable()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = Guid.NewGuid(),
            AnalysisType = "steiner",
        };
        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync();
        var model = await service.ResolveForInferenceAsync("gemini", "ceph-model-v1");

        var run = await service.BeginInferenceAsync(
            analysis.Id, model, "ceph_landmark_draft", "draft",
            1200, 800, [1, 2, 3, 4]);
        await service.CompleteInferenceAsync(run.Id,
        [
            new { key = "S", xNormalized = 100d, yNormalized = 200d, confidence = 0.91d },
        ]);

        var persisted = await db.CephAiInferenceRuns.SingleAsync();
        persisted.Status.Should().Be("succeeded");
        persisted.ImageSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        persisted.OutputSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        persisted.OriginalPredictionsJson.Should().Contain("xNormalized");
        persisted.OriginalPredictionsJson.Should().NotContain("patient");
        persisted.CompletedAt.Should().NotBeNull();

        var mutate = async () => await service.CompleteInferenceAsync(run.Id, Array.Empty<object>());
        await mutate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
        (await service.TryFailInferenceAsync(run.Id, "late_failure")).Should().BeFalse();
        (await db.CephAiInferenceRuns.SingleAsync()).Status.Should().Be("succeeded");
    }

    [Fact]
    public async Task FailedInferenceStoresOnlyBoundedFailureCode()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = Guid.NewGuid(),
            AnalysisType = "steiner",
        };
        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync();
        var model = await service.ResolveForInferenceAsync("gemini", "ceph-model-v1");
        var run = await service.BeginInferenceAsync(
            analysis.Id, model, "ceph_landmark_draft", "high", 1200, 800, [1]);

        await service.FailInferenceAsync(run.Id, "vision_timeout");

        var persisted = await db.CephAiInferenceRuns.SingleAsync();
        persisted.Status.Should().Be("failed");
        persisted.FailureCode.Should().Be("vision_timeout");
        persisted.OriginalPredictionsJson.Should().BeNull();
        persisted.OutputSha256.Should().BeNull();
    }

    [Fact]
    public async Task SaveLandmarks_RejectsInferenceRunFromAnotherAnalysis()
    {
        await using var db = CreateDb();
        var currentUser = CurrentUser();
        var registry = new CephAiModelRegistryService(
            db, [new TestLandmarkProvider("gemini")], currentUser.Object);
        var target = Analysis();
        var other = Analysis();
        db.CephAnalyses.AddRange(target, other);
        await db.SaveChangesAsync();
        var model = await registry.ResolveForInferenceAsync("gemini", "ceph-model-v1");
        var run = await registry.BeginInferenceAsync(
            other.Id, model, "ceph_landmark_draft", "draft", 1000, 800, [1]);
        await registry.CompleteInferenceAsync(run.Id,
            [new { key = "S", xNormalized = 100d, yNormalized = 200d, confidence = 0.9d }]);
        var ceph = new CephService(db, currentUser.Object, Mock.Of<ILogger<CephService>>());

        var save = async () => await ceph.SaveLandmarksAsync(target.Id, LandmarkRequest(run.Id));

        await save.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*another analysis*");
        (await db.CephLandmarks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveLandmarks_LinksDoctorCorrectionToSuccessfulInferenceRun()
    {
        await using var db = CreateDb();
        var currentUser = CurrentUser();
        var registry = new CephAiModelRegistryService(
            db, [new TestLandmarkProvider("gemini")], currentUser.Object);
        var analysis = Analysis();
        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync();
        var model = await registry.ResolveForInferenceAsync("gemini", "ceph-model-v1");
        var run = await registry.BeginInferenceAsync(
            analysis.Id, model, "ceph_landmark_draft", "draft", 1000, 800, [1]);
        await registry.CompleteInferenceAsync(run.Id,
            [new { key = "S", xNormalized = 100d, yNormalized = 200d, confidence = 0.9d }]);
        var ceph = new CephService(db, currentUser.Object, Mock.Of<ILogger<CephService>>());

        (await ceph.SaveLandmarksAsync(
            analysis.Id, LandmarkRequest(run.Id, sourceModelId: "spoofed/model"))).Should().BeTrue();

        var saved = await db.CephLandmarks.SingleAsync();
        saved.SourceInferenceRunId.Should().Be(run.Id);
        saved.SourceModelId.Should().Be("gemini/ceph-model-v1");
        saved.ReviewErrorMm.Should().BeApproximately(0.5m, 0.0001m);
    }

    [Fact]
    public async Task SaveLandmarks_RejectsLandmarkAbsentFromInferenceOutput()
    {
        await using var db = CreateDb();
        var currentUser = CurrentUser();
        var registry = new CephAiModelRegistryService(
            db, [new TestLandmarkProvider("gemini")], currentUser.Object);
        var analysis = Analysis();
        db.CephAnalyses.Add(analysis);
        await db.SaveChangesAsync();
        var model = await registry.ResolveForInferenceAsync("gemini", "ceph-model-v1");
        var run = await registry.BeginInferenceAsync(
            analysis.Id, model, "ceph_landmark_draft", "draft", 1000, 800, [1]);
        await registry.CompleteInferenceAsync(run.Id,
            [new { key = "S", xNormalized = 100d, yNormalized = 200d, confidence = 0.9d }]);
        var ceph = new CephService(db, currentUser.Object, Mock.Of<ILogger<CephService>>());

        var save = async () => await ceph.SaveLandmarksAsync(
            analysis.Id, LandmarkRequest(run.Id, landmarkKey: "N"));

        await save.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not contain landmark 'N'*");
        (await db.CephLandmarks.CountAsync()).Should().Be(0);
    }

    private static RegisterCephAiModelVersionRequest Candidate(
        string modelVersion,
        char hash,
        string provider = "aqlan-local",
        string preprocessingVersion = "ADP-CEPH-PREPROCESS-v1") => new()
    {
        Provider = provider,
        ModelId = "ceph-landmarks",
        ModelVersion = modelVersion,
        PreprocessingVersion = preprocessingVersion,
        DatasetVersion = "ceph-gold-v1",
        LandmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
        ArtifactSha256 = new string(hash, 64),
    };

    private static ApproveCephAiModelVersionRequest Approval(string suffix) => new()
    {
        ValidationEvidenceId = $"locked-study-{suffix}",
        StatisticsApprovalId = $"statistics-signoff-{suffix}",
        ClinicalApprovalId = $"orthodontist-signoff-{suffix}",
    };

    private static CephAiModelRegistryService CreateService(AppDbContext db, Guid? userId = null)
    {
        var currentUser = CurrentUser(userId);
        return new CephAiModelRegistryService(
            db,
            [new TestLandmarkProvider("gemini"), new TestLandmarkProvider("aqlan-local")],
            currentUser.Object);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid? userId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(item => item.UserId).Returns(userId ?? Guid.NewGuid());
        return currentUser;
    }

    private static CephAnalysis Analysis() => new()
    {
        Id = Guid.NewGuid(),
        OrthoCaseId = Guid.NewGuid(),
        AnalysisType = "steiner",
    };

    private static SaveLandmarksRequest LandmarkRequest(
        Guid inferenceRunId,
        string landmarkKey = "S",
        string? sourceModelId = null) => new()
        {
            PixelsPerMm = 2,
            ImageWidth = 1000,
            ImageHeight = 800,
            Landmarks =
        [
            new LandmarkInput
            {
                Key = landmarkKey,
                Name = "Sella",
                X = 101,
                Y = 200,
                IsAiPlaced = true,
                PlacementSource = "ai",
                SourceInferenceRunId = inferenceRunId,
                SourceModelId = sourceModelId,
                AiProposalX = 100,
                AiProposalY = 200,
                IsReviewed = true,
                Confidence = 0.9,
            },
        ],
        };

    private static AppDbContext CreateDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
