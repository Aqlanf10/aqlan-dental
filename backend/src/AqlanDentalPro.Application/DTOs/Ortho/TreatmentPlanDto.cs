namespace AqlanDentalPro.Application.DTOs.Ortho;

public class TreatmentPlanDto
{
    public Guid Id { get; set; }
    public Guid OrthoCaseId { get; set; }
    public int PlanVersion { get; set; }
    public string PlanLabel { get; set; } = "A";
    public bool IsSelected { get; set; }
    public bool IsApproved { get; set; }
    public string? ApplianceType { get; set; }
    public string? BracketSystem { get; set; }
    public string? InitialWire { get; set; }
    public string? ExtractionPlan { get; set; }
    public string? AnchoragePlan { get; set; }
    public bool UseTads { get; set; }
    public bool UseElastics { get; set; }
    public int? ExpectedDurationMonths { get; set; }
    public string? RetentionPlan { get; set; }
    public string? TreatmentGoals { get; set; }
    public string? RisksLimitations { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovedAt { get; set; }
}

public class TreatmentPlanListDto
{
    public List<TreatmentPlanDto> Plans { get; set; } = [];
    public TreatmentPlanDto? SelectedPlan { get; set; }
}

public class UpsertTreatmentPlanServiceRequest
{
    /// <summary>Plan label: "A" (default) or "B" (alternative)</summary>
    public string? PlanLabel { get; set; }
    public string? ApplianceType { get; set; }
    public string? BracketSystem { get; set; }
    public string? InitialWire { get; set; }
    public string? ExtractionPlan { get; set; }
    public string? AnchoragePlan { get; set; }
    public bool UseTads { get; set; }
    public bool UseElastics { get; set; }
    public int? ExpectedDurationMonths { get; set; }
    public string? RetentionPlan { get; set; }
    public string? TreatmentGoals { get; set; }
    public string? RisksLimitations { get; set; }
}
