namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// The oral surgeon's review of an <see cref="OrthoSurgicalCase"/>: their decision,
/// proposed procedure and required records. One review per case (latest wins / upsert).
/// </summary>
public class SurgeonReview : BaseEntity
{
    public Guid OrthoSurgicalCaseId { get; set; }
    /// <summary>Doctors.Id of the reviewing surgeon.</summary>
    public Guid? SurgeonId { get; set; }

    /// <summary>Approved | RequestChanges | NotCandidate | NeedsImaging.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? ProposedProcedure { get; set; }
    public string? RequiredRecords { get; set; }
    public string? Risks { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public OrthoSurgicalCase OrthoSurgicalCase { get; set; } = null!;
}
