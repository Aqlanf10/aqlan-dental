namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Audit row for every call to the orthodontic LLM assistant (Ceph batch C-D).
/// One row per attempt — success AND failure — so the owner can always answer
/// "who asked the AI for what, when, and did it work?".
///
/// Privacy rules (enforced by CephAiDraftService + tests):
/// - InputSummary holds COUNTS ONLY (e.g. "measurements:18;diagnosisPresent:true"),
///   never patient identifiers.
/// - ErrorSummary is a short Arabic/English reason (e.g. "HTTP 500"), never an
///   exception dump and never any secret.
/// </summary>
public class OrthodonticAiLog : BaseEntity
{
    /// <summary>The cephalometric analysis the call was made for.</summary>
    public Guid AnalysisId { get; set; }

    /// <summary>User who triggered the call (null when unauthenticated context).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Action key, e.g. "ceph_draft_diagnosis".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>LLM model id used (e.g. "claude-sonnet-4-6"); null when never reached the API.</summary>
    public string? ModelId { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>Short failure reason — NO secrets, NO exception dumps.</summary>
    public string? ErrorSummary { get; set; }

    /// <summary>Counts-only input description — never patient identifiers.</summary>
    public string? InputSummary { get; set; }

    /// <summary>Character length of the LLM output (0 on failure).</summary>
    public int OutputLength { get; set; }
}
