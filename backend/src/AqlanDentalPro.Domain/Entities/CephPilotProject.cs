using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public sealed class CephPilotProject : BaseEntity
{
    public const string RatifiedLandmarkDefinitionVersion = "ADP-LM-LAT-v1.0";

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LandmarkDefinitionVersion { get; set; } = RatifiedLandmarkDefinitionVersion;
    public CephPilotProjectStatus Status { get; set; } = CephPilotProjectStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? DatasetVersion { get; set; }
    public Guid ReviewerAUserId { get; set; }
    public Guid ReviewerBUserId { get; set; }
    public Guid? AdjudicatorUserId { get; set; }
    public bool IsAiHidden { get; set; } = true;
    public bool IsWebCephHidden { get; set; } = true;
    public int Revision { get; set; } = 1;

    public ICollection<CephPilotCase> Cases { get; set; } = [];
}
