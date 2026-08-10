using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public sealed class CephPilotReviewPoint : BaseEntity
{
    public Guid ReviewSessionId { get; set; }
    public string LandmarkKey { get; set; } = string.Empty;
    public bool IsCore { get; set; }
    public CephPilotLandmarkVisibility Visibility { get; set; }
    public decimal? XCoordPx { get; set; }
    public decimal? YCoordPx { get; set; }
    public CephPilotContourDecision ContourDecision { get; set; }
    public DateTime AnnotatedAt { get; set; } = DateTime.UtcNow;

    public CephPilotReviewSession ReviewSession { get; set; } = null!;
}
