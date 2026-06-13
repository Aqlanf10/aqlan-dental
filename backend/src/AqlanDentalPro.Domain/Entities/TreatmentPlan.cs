namespace AqlanDentalPro.Domain.Entities;

public class TreatmentPlan : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public int PlanVersion { get; set; } = 1;
    public string PlanLabel { get; set; } = "A";
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
    public string? MechanicsPlan { get; set; }
    public string? AuxiliaryAppliances { get; set; }
    public string? SpaceManagementPlan { get; set; }
    public string? InterdisciplinaryPlan { get; set; }
    public string PatientDecisionStatus { get; set; } = "NotPresented";
    public DateTime? PresentedAt { get; set; }
    public DateTime? PatientDecisionAt { get; set; }
    public string? PatientDecisionBy { get; set; }
    public string? PatientConsentMethod { get; set; }
    public string? PatientDecisionNotes { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? ApprovedByDoctor { get; set; }
    public ICollection<TreatmentPlanObjective> Objectives { get; set; } = [];
    public ICollection<TreatmentPlanPhase> Phases { get; set; } = [];
}
