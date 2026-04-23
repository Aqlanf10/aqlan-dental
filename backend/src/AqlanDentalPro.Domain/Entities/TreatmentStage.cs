namespace AqlanDentalPro.Domain.Entities;

public class TreatmentStage : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int StageOrder { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }
    public int? TargetDurationMonths { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "pending";

    public OrthoCase OrthoCase { get; set; } = null!;
}
