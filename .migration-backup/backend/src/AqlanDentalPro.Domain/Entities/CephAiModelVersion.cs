namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Immutable identity and evidence metadata for one cephalometric landmark model.
/// Provider/model names alone are not sufficient lineage, so preprocessing,
/// dataset, landmark-definition, and artifact versions are pinned as well.
/// </summary>
public sealed class CephAiModelVersion : BaseEntity
{
    public string RegistryKey { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string PreprocessingVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public string LandmarkDefinitionVersion { get; set; } = string.Empty;
    public string IdentitySha256 { get; set; } = string.Empty;
    public string? ArtifactSha256 { get; set; }
    public string Status { get; set; } = "observed";
    public string? ValidationEvidenceId { get; set; }
    public string? StatisticsApprovalId { get; set; }
    public string? ClinicalApprovalId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public ICollection<CephAiInferenceRun> InferenceRuns { get; set; } = [];
}
