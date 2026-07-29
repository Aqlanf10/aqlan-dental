namespace AqlanDentalPro.Application.DTOs.Ceph;

public sealed class RegisterCephAiModelVersionRequest
{
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string ModelVersion { get; init; } = string.Empty;
    public string PreprocessingVersion { get; init; } = string.Empty;
    public string DatasetVersion { get; init; } = string.Empty;
    public string LandmarkDefinitionVersion { get; init; } = string.Empty;
    public string ArtifactSha256 { get; init; } = string.Empty;
}

public sealed class ApproveCephAiModelVersionRequest
{
    public string ValidationEvidenceId { get; init; } = string.Empty;
    public string StatisticsApprovalId { get; init; } = string.Empty;
    public string ClinicalApprovalId { get; init; } = string.Empty;
}

public sealed class CephAiModelVersionDto
{
    public Guid Id { get; init; }
    public string RegistryKey { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string ModelVersion { get; init; } = string.Empty;
    public string PreprocessingVersion { get; init; } = string.Empty;
    public string DatasetVersion { get; init; } = string.Empty;
    public string LandmarkDefinitionVersion { get; init; } = string.Empty;
    public string IdentitySha256 { get; init; } = string.Empty;
    public string? ArtifactSha256 { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ValidationEvidenceId { get; init; }
    public string? StatisticsApprovalId { get; init; }
    public string? ClinicalApprovalId { get; init; }
    public DateTime? ApprovedAt { get; init; }
}

public sealed class CephAiModelRegistryDto
{
    public string Slot { get; init; } = string.Empty;
    public Guid? ActiveModelVersionId { get; init; }
    public Guid? PreviousModelVersionId { get; init; }
    public DateTime? PinnedAt { get; init; }
    public List<CephAiModelVersionDto> Models { get; init; } = [];
}

public sealed class CephAiInferenceRunDto
{
    public Guid Id { get; init; }
    public Guid ModelVersionId { get; init; }
    public string RegistryKey { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string Precision { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? FailureCode { get; init; }
    public string ImageSha256 { get; init; } = string.Empty;
    public string? OutputSha256 { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
