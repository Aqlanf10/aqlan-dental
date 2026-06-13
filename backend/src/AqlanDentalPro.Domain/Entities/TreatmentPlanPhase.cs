namespace AqlanDentalPro.Domain.Entities;

public class TreatmentPlanPhase : BaseEntity
{
    public Guid TreatmentPlanId { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string? ObjectiveSummary { get; set; }
    public string? PlannedAppliance { get; set; }
    public string? Mechanics { get; set; }
    public int? TargetDurationMonths { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public string Status { get; set; } = "Planned";
    public string? Notes { get; set; }

    public TreatmentPlan TreatmentPlan { get; set; } = null!;
}
