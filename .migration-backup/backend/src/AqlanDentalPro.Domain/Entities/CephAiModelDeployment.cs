namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Current and immediately previous approved model for a clinical inference slot.
/// Existing inference runs retain their own model version when this pointer moves.
/// </summary>
public sealed class CephAiModelDeployment : BaseEntity
{
    public string Slot { get; set; } = string.Empty;
    public Guid ActiveModelVersionId { get; set; }
    public Guid? PreviousModelVersionId { get; set; }
    public Guid? PinnedByUserId { get; set; }
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;

    public CephAiModelVersion ActiveModelVersion { get; set; } = null!;
    public CephAiModelVersion? PreviousModelVersion { get; set; }
}
