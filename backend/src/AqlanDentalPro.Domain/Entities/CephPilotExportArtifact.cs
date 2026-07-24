using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public sealed class CephPilotExportArtifact : BaseEntity
{
    public Guid CaseId { get; set; }
    public CephPilotExportArtifactType ArtifactType { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsComparatorOnly { get; set; } = true;
    public string ImportStatus { get; set; } = "staged";

    public CephPilotCase Case { get; set; } = null!;
}
