namespace AqlanDentalPro.Domain.Entities;

public class TreatmentPlanObjective : BaseEntity
{
    public Guid TreatmentPlanId { get; set; }
    public string Category { get; set; } = "Other";
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; } = 2;
    public int SortOrder { get; set; }
    public bool IsAchieved { get; set; }
    public DateTime? AchievedAt { get; set; }

    public TreatmentPlan TreatmentPlan { get; set; } = null!;
}
