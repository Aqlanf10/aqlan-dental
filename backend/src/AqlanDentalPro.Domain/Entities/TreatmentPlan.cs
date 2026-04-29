namespace AqlanDentalPro.Domain.Entities;

public class TreatmentPlan : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public int PlanVersion { get; set; } = 1;
    /// <summary>Plan label: "A" or "B" (alternative plan)</summary>
    public string PlanLabel { get; set; } = "A";
    /// <summary>Whether this is the selected/active plan</summary>
    public bool IsSelected { get; set; } = true;
    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApplianceType { get; set; }
    public string? BracketSystem { get; set; }
    public string? InitialWire { get; set; }
    public string? ExtractionPlan { get; set; }
    public string? AnchoragePlan { get; set; }
    public bool UseTads { get; set; } = false;
    public bool UseElastics { get; set; } = false;
    public int? ExpectedDurationMonths { get; set; }
    public string? RetentionPlan { get; set; }
    public string? TreatmentGoals { get; set; }
    public string? RisksLimitations { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? ApprovedByDoctor { get; set; }
}
