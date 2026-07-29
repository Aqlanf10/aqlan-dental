namespace AqlanDentalPro.Application.DTOs.Ceph;

/// <summary>
/// Result of a successful LLM draft-diagnosis generation (Ceph batch C-D).
/// The draft is NEVER auto-saved — the doctor explicitly copies it into the
/// FinalDiagnosis field and saves via the existing flow.
/// </summary>
public class CephAiDraftResultDto
{
    public string Draft { get; set; } = string.Empty;

    /// <summary>Model id that actually produced the draft.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Mandatory Arabic disclaimer — every LLM output is a draft requiring
    /// specialist review and approval before any clinical use.
    /// </summary>
    public string Disclaimer { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }
}
