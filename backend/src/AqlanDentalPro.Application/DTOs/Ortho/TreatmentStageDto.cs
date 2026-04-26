namespace AqlanDentalPro.Application.DTOs.Ortho;

public class TreatmentStageDto
{
    public Guid Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int StageOrder { get; set; }
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public int? TargetDurationMonths { get; set; }
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }
}
