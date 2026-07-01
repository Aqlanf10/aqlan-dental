namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A discussion-thread comment on an <see cref="OrthoSurgicalCase"/> — the back-and-forth
/// between the orthodontist and the oral surgeon during collaboration/review rounds.
/// Distinct from <see cref="SurgeonReview"/> (a single upsertable decision record): comments
/// accumulate, so a "request changes" round-trip keeps its full history instead of being
/// overwritten by the next review.
/// </summary>
public class OrthoSurgicalComment : BaseEntity
{
    public Guid OrthoSurgicalCaseId { get; set; }
    public Guid? AuthorUserId { get; set; }
    /// <summary>Snapshot of the author's role at post time (Orthodontist/OralSurgeon/Admin) for display.</summary>
    public string? AuthorRole { get; set; }
    public string Body { get; set; } = string.Empty;

    public OrthoSurgicalCase OrthoSurgicalCase { get; set; } = null!;
}
