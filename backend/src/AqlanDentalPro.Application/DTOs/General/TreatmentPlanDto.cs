namespace AqlanDentalPro.Application.DTOs.General;

public class TreatmentPlanItemDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string? ToothNumber { get; set; }
    public string Treatment { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public string? DoctorName { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
}

public class CreateTreatmentPlanItemRequest
{
    public Guid PatientId { get; set; }
    public string? ToothNumber { get; set; }
    public string Treatment { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium";
    public decimal? EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public Guid? DoctorId { get; set; }
}

public class UpdateTreatmentPlanStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
