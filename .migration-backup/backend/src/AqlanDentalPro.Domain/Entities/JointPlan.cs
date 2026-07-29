namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// The final joint treatment plan for an <see cref="OrthoSurgicalCase"/>, agreed
/// between the orthodontist and the oral surgeon. Becomes immutable (<see cref="LockedAt"/>)
/// only after BOTH sides approve — enforced in the Backend, not just the UI.
/// </summary>
public class JointPlan : BaseEntity
{
    public Guid OrthoSurgicalCaseId { get; set; }

    public string? OrthodonticObjectives { get; set; }
    public string? SurgicalObjectives { get; set; }
    public string? ProcedureType { get; set; }
    public string? Timing { get; set; }
    public string? PreSurgicalRequirements { get; set; }
    public string? PostSurgicalPlan { get; set; }
    public string? Risks { get; set; }
    public string? PatientExplanation { get; set; }

    public DateTime? OrthodontistApprovedAt { get; set; }
    public DateTime? SurgeonApprovedAt { get; set; }
    /// <summary>Set once both approvals are present; the plan is then read-only.</summary>
    public DateTime? LockedAt { get; set; }

    public OrthoSurgicalCase OrthoSurgicalCase { get; set; } = null!;
}
