using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public sealed class CephAiModelRegistryService(
    AppDbContext db,
    IEnumerable<ICephLandmarkDraftProvider> providers,
    ICurrentUserService currentUser)
{
    public const string DeploymentSlot = "ceph-landmark-draft";
    public const string RuntimePreprocessingVersion = "ADP-CEPH-PREPROCESS-v1";
    public const string ProviderManagedDatasetVersion = "provider-managed-undisclosed";

    private static readonly JsonSerializerOptions PredictionJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HashSet<string> installedProviders = providers
        .Select(item => item.ProviderName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<CephAiModelVersion> ResolveForInferenceAsync(
        string configuredProvider,
        string configuredModelId,
        CancellationToken cancellationToken = default)
    {
        var deployment = await db.CephAiModelDeployments
            .AsNoTracking()
            .Include(item => item.ActiveModelVersion)
            .SingleOrDefaultAsync(item => item.Slot == DeploymentSlot, cancellationToken);
        if (deployment?.ActiveModelVersion is { Status: "approved" } pinned)
        {
            EnsureExecutableContract(pinned);
            return pinned;
        }

        var provider = Required(configuredProvider, nameof(configuredProvider), 50).ToLowerInvariant();
        var modelId = Required(configuredModelId, nameof(configuredModelId), 150);
        const string modelVersion = "provider-managed";
        var identity = BuildIdentitySha256(
            provider,
            modelId,
            modelVersion,
            RuntimePreprocessingVersion,
            ProviderManagedDatasetVersion,
            CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
            artifactSha256: null);

        var existing = await db.CephAiModelVersions
            .SingleOrDefaultAsync(item => item.IdentitySha256 == identity, cancellationToken);
        if (existing is not null)
            return existing;

        var observed = new CephAiModelVersion
        {
            RegistryKey = $"observed:{identity}",
            Provider = provider,
            ModelId = modelId,
            ModelVersion = modelVersion,
            PreprocessingVersion = RuntimePreprocessingVersion,
            DatasetVersion = ProviderManagedDatasetVersion,
            LandmarkDefinitionVersion = CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
            IdentitySha256 = identity,
            Status = "observed",
        };
        db.CephAiModelVersions.Add(observed);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return observed;
        }
        catch (DbUpdateException)
        {
            db.Entry(observed).State = EntityState.Detached;
            return await db.CephAiModelVersions
                .SingleAsync(item => item.IdentitySha256 == identity, cancellationToken);
        }
    }

    public async Task<CephAiModelVersionDto> RegisterCandidateAsync(
        RegisterCephAiModelVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = Required(request.Provider, nameof(request.Provider), 50).ToLowerInvariant();
        var modelId = Required(request.ModelId, nameof(request.ModelId), 150);
        var modelVersion = Required(request.ModelVersion, nameof(request.ModelVersion), 100);
        var preprocessing = Required(request.PreprocessingVersion, nameof(request.PreprocessingVersion), 100);
        var dataset = Required(request.DatasetVersion, nameof(request.DatasetVersion), 100);
        var definitions = Required(request.LandmarkDefinitionVersion, nameof(request.LandmarkDefinitionVersion), 100);
        var artifact = RequiredSha256(request.ArtifactSha256, nameof(request.ArtifactSha256));
        var identity = BuildIdentitySha256(
            provider, modelId, modelVersion, preprocessing, dataset, definitions, artifact);

        var existing = await db.CephAiModelVersions
            .SingleOrDefaultAsync(item => item.IdentitySha256 == identity, cancellationToken);
        if (existing is not null)
            return Map(existing);

        var candidate = new CephAiModelVersion
        {
            RegistryKey = $"candidate:{identity}",
            Provider = provider,
            ModelId = modelId,
            ModelVersion = modelVersion,
            PreprocessingVersion = preprocessing,
            DatasetVersion = dataset,
            LandmarkDefinitionVersion = definitions,
            IdentitySha256 = identity,
            ArtifactSha256 = artifact,
            Status = "candidate",
        };
        db.CephAiModelVersions.Add(candidate);
        await db.SaveChangesAsync(cancellationToken);
        return Map(candidate);
    }

    public async Task<CephAiModelVersionDto?> ApproveAsync(
        Guid modelVersionId,
        ApproveCephAiModelVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = await db.CephAiModelVersions
            .SingleOrDefaultAsync(item => item.Id == modelVersionId, cancellationToken);
        if (model is null)
            return null;
        if (model.Status == "approved")
            return Map(model);
        if (model.Status != "candidate" || string.IsNullOrWhiteSpace(model.ArtifactSha256))
            throw new InvalidOperationException("Only a versioned candidate with an artifact SHA-256 may be approved.");

        model.ValidationEvidenceId = Required(
            request.ValidationEvidenceId, nameof(request.ValidationEvidenceId), 150);
        model.StatisticsApprovalId = Required(
            request.StatisticsApprovalId, nameof(request.StatisticsApprovalId), 150);
        model.ClinicalApprovalId = Required(
            request.ClinicalApprovalId, nameof(request.ClinicalApprovalId), 150);
        model.Status = "approved";
        model.ApprovedByUserId = currentUser.UserId;
        model.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Map(model);
    }

    public async Task<CephAiModelRegistryDto> PinAsync(
        Guid modelVersionId,
        CancellationToken cancellationToken = default)
    {
        var model = await db.CephAiModelVersions
            .SingleOrDefaultAsync(item => item.Id == modelVersionId, cancellationToken)
            ?? throw new KeyNotFoundException("Model version was not found.");
        if (model.Status != "approved")
            throw new InvalidOperationException("Only an independently approved model version may be pinned.");
        if (!installedProviders.Contains(model.Provider))
            throw new InvalidOperationException(
                $"No installed cephalometric landmark provider can execute '{model.Provider}'.");
        EnsureExecutableContract(model);

        var deployment = await db.CephAiModelDeployments
            .SingleOrDefaultAsync(item => item.Slot == DeploymentSlot, cancellationToken);
        if (deployment is null)
        {
            deployment = new CephAiModelDeployment
            {
                Slot = DeploymentSlot,
                ActiveModelVersionId = model.Id,
            };
            db.CephAiModelDeployments.Add(deployment);
        }
        else if (deployment.ActiveModelVersionId != model.Id)
        {
            deployment.PreviousModelVersionId = deployment.ActiveModelVersionId;
            deployment.ActiveModelVersionId = model.Id;
        }

        deployment.PinnedByUserId = currentUser.UserId;
        deployment.PinnedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetRegistryAsync(cancellationToken);
    }

    public async Task<CephAiModelRegistryDto> RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        var deployment = await db.CephAiModelDeployments
            .SingleOrDefaultAsync(item => item.Slot == DeploymentSlot, cancellationToken)
            ?? throw new InvalidOperationException("No pinned model deployment exists.");
        if (deployment.PreviousModelVersionId is not Guid previousId)
            throw new InvalidOperationException("No previous model version is available for rollback.");

        var previous = await db.CephAiModelVersions
            .SingleOrDefaultAsync(item => item.Id == previousId, cancellationToken);
        if (previous?.Status != "approved")
            throw new InvalidOperationException("The previous model version is not approved and cannot be restored.");
        if (!installedProviders.Contains(previous.Provider))
            throw new InvalidOperationException(
                $"No installed cephalometric landmark provider can execute '{previous.Provider}'.");
        EnsureExecutableContract(previous);

        (deployment.ActiveModelVersionId, deployment.PreviousModelVersionId) =
            (previousId, deployment.ActiveModelVersionId);
        deployment.PinnedByUserId = currentUser.UserId;
        deployment.PinnedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetRegistryAsync(cancellationToken);
    }

    public async Task<CephAiModelRegistryDto> GetRegistryAsync(
        CancellationToken cancellationToken = default)
    {
        var deployment = await db.CephAiModelDeployments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Slot == DeploymentSlot, cancellationToken);
        var models = await db.CephAiModelVersions
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return new CephAiModelRegistryDto
        {
            Slot = DeploymentSlot,
            ActiveModelVersionId = deployment?.ActiveModelVersionId,
            PreviousModelVersionId = deployment?.PreviousModelVersionId,
            PinnedAt = deployment?.PinnedAt,
            Models = models.Select(Map).ToList(),
        };
    }

    public async Task<CephAiInferenceRun> BeginInferenceAsync(
        Guid analysisId,
        CephAiModelVersion model,
        string operation,
        string precision,
        int imageWidth,
        int imageHeight,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var run = new CephAiInferenceRun
        {
            AnalysisId = analysisId,
            ModelVersionId = model.Id,
            Operation = Required(operation, nameof(operation), 40),
            Precision = Required(precision, nameof(precision), 20),
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            ImageSha256 = Sha256Hex(imageBytes),
            TriggeredByUserId = currentUser.UserId,
        };
        db.CephAiInferenceRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task CompleteInferenceAsync<T>(
        Guid inferenceRunId,
        IReadOnlyList<T> originalPredictions,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRunningRunAsync(inferenceRunId, cancellationToken);
        var json = JsonSerializer.Serialize(originalPredictions, PredictionJsonOptions);
        run.OriginalPredictionsJson = json;
        run.OutputSha256 = Sha256Hex(Encoding.UTF8.GetBytes(json));
        run.Status = "succeeded";
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailInferenceAsync(
        Guid inferenceRunId,
        string failureCode,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRunningRunAsync(inferenceRunId, cancellationToken);
        run.FailureCode = Required(failureCode, nameof(failureCode), 100);
        run.Status = "failed";
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryFailInferenceAsync(
        Guid inferenceRunId,
        string failureCode,
        CancellationToken cancellationToken = default)
    {
        var run = await db.CephAiInferenceRuns
            .SingleOrDefaultAsync(item => item.Id == inferenceRunId, cancellationToken);
        if (run is null || run.Status != "running" || run.CompletedAt.HasValue)
            return false;

        run.FailureCode = Required(failureCode, nameof(failureCode), 100);
        run.Status = "failed";
        run.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<CephAiInferenceRunDto>> GetInferenceRunsAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default) =>
        await db.CephAiInferenceRuns
            .AsNoTracking()
            .Where(item => item.AnalysisId == analysisId)
            .OrderByDescending(item => item.StartedAt)
            .Select(item => new CephAiInferenceRunDto
            {
                Id = item.Id,
                ModelVersionId = item.ModelVersionId,
                RegistryKey = item.ModelVersion.RegistryKey,
                Operation = item.Operation,
                Precision = item.Precision,
                Status = item.Status,
                FailureCode = item.FailureCode,
                ImageSha256 = item.ImageSha256,
                OutputSha256 = item.OutputSha256,
                StartedAt = item.StartedAt,
                CompletedAt = item.CompletedAt,
            })
            .ToListAsync(cancellationToken);

    private async Task<CephAiInferenceRun> GetRunningRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await db.CephAiInferenceRuns
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Inference run was not found.");
        if (run.Status != "running" || run.CompletedAt.HasValue)
            throw new InvalidOperationException("A completed inference run is immutable.");
        return run;
    }

    private static void EnsureExecutableContract(CephAiModelVersion model)
    {
        if (!string.Equals(
                model.PreprocessingVersion,
                RuntimePreprocessingVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                model.LandmarkDefinitionVersion,
                CephBenchmarkManifestValidator.LandmarkDefinitionVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The model preprocessing or landmark-definition contract is not supported by this runtime.");
        }
    }

    private static CephAiModelVersionDto Map(CephAiModelVersion item) => new()
    {
        Id = item.Id,
        RegistryKey = item.RegistryKey,
        Provider = item.Provider,
        ModelId = item.ModelId,
        ModelVersion = item.ModelVersion,
        PreprocessingVersion = item.PreprocessingVersion,
        DatasetVersion = item.DatasetVersion,
        LandmarkDefinitionVersion = item.LandmarkDefinitionVersion,
        IdentitySha256 = item.IdentitySha256,
        ArtifactSha256 = item.ArtifactSha256,
        Status = item.Status,
        ValidationEvidenceId = item.ValidationEvidenceId,
        StatisticsApprovalId = item.StatisticsApprovalId,
        ClinicalApprovalId = item.ClinicalApprovalId,
        ApprovedAt = item.ApprovedAt,
    };

    private static string BuildIdentitySha256(
        string provider,
        string modelId,
        string modelVersion,
        string preprocessing,
        string dataset,
        string definitions,
        string? artifactSha256) =>
        Sha256Hex(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            provider,
            modelId,
            modelVersion,
            preprocessing,
            dataset,
            definitions,
            artifactSha256 ?? "artifact-unavailable",
        ])));

    private static string Sha256Hex(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string Required(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{name} is required.");
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{name} exceeds {maxLength} characters.");
        return normalized;
    }

    private static string RequiredSha256(string? value, string name)
    {
        var normalized = Required(value, name, 64).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException($"{name} must be a 64-character SHA-256 hex digest.");
        return normalized;
    }
}
