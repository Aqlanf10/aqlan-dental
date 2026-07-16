using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public sealed class CephPilotReviewSession : BaseEntity
{
    public const string CurrentToolVersion = "ADP-CEPH-REVIEW-v1";

    public Guid CaseId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public CephPilotReviewerSlot ReviewerSlot { get; set; }
    public CephPilotReviewStatus Status { get; set; } = CephPilotReviewStatus.Draft;
    public string LandmarkDefinitionVersion { get; set; } = CephPilotProject.RatifiedLandmarkDefinitionVersion;
    public string ToolVersion { get; set; } = CurrentToolVersion;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSavedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int Revision { get; set; } = 1;

    public CephPilotCase Case { get; set; } = null!;
    public ICollection<CephPilotReviewPoint> Points { get; set; } = [];
}
